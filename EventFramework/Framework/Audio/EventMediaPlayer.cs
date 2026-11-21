// EventMediaPlayer.cs — 事件框架 · 【音频播放器】

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EventFramework
{
    public static class EventMediaPlayer
    {
        internal class Active
        {
            public string instId;                        // 所属事件实例 id（按它分组播放表；独立音源为 null）
            public EventInstance inst;                  // 所属事件实例（取事件上下文用；独立音源为 null）
            public List<Active> owner;                  // 宿主音源（快照 / 子音源挂在父音源下，去重与连带停止用）

            public EventMediaKind kind;                 // 音源类别：循环音 / 自持音 / 快照（决定 handle 里装什么）
            public string key;          // 解析后的 key：重定向已跟到底，取自最终模板（含未注册 key 的 Raw 降级模板）
            public string srcKey;       // 调用方传入的原始键（yaml / 脚本里写的那一串，原样保留；解析结果见 key）
            public string name;                          // 播放名（日志与去重展示用）
            public string slot;                          // 所属叙事 / 事件槽位（停某槽位时连带停它）
            public EventAudioTemplate t;               // 解析到的音频模板（响度 / 时长 / 片段）
            public bool forceLoop;                       // 强制循环（模板允许时覆盖一次性播放）

            public object handle;       // 循环音：LoopingSoundManager 句柄（装箱）；自持音：EventInstance（装箱）；快照：EventReference（装箱）
            public object loopOwner;    // 循环音的 LoopingSoundManager 实例（换局后句柄失效，用它校验）
            public bool stoppable;                       // 是否可被停止（一次性短音可能设为不可停）
            public bool stopped;                         // 已停止标记（防重复停止 / 重复结算）
            public string stopMode;                      // 停止条件：时长 / 信号 / 事件结束
            public string stopSignal;   // 停止条件③：GameHashes 名 或 作者自定义信号名
            public bool signalCustom;   // true = 走 EventSignal；false = 走 Game.Instance.Subscribe
            public int signalHash;      // GameHashes 已订阅的哈希（0 = 没订）
            public Game signalGame;     // ★ 订阅时的 Game 实例（换档后旧实例已销毁，退订必须用它，否则漏订）
            public System.Action<object> signalCb;      // 订阅停止信号时的回调（取消订阅需用同一实例）
            public bool honorStopOnEnd; // stop_on_end=false 的：效果结束不停，留到收尾

            public bool started;                         // 是否已真正起播（延时起播前为 false）
            public SchedulerHandle delayHandle;   // 延时起播闹钟
            public SchedulerHandle stopHandle;    // 到时长停闹钟
            public string track;                  // play_as 轨道（sfx 不限；bgm / voice / ambient 独占）
            public float startTime;               // 起播时刻（Time.unscaledTime，音频按真实时间走）
            public float audioSeconds;            // 这条音源本身的时长（0 = 未知）
        }

        private static readonly Dictionary<string, List<Active>> _active =
            new Dictionary<string, List<Active>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Active> _tracks =
            new Dictionary<string, Active>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> _lastPlayed =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private const float UNKNOWN_LENGTH_WINDOW = 2f;   // 时长未知的音源：起播后该窗口内都算「在响」
        private static int _dynamicMusicHeld;             // 已占用的动态音乐轨（bgm / voice / ambient）计数；>0 时压住原版动态音乐

        public static int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _active) if (kv.Value != null) n += kv.Value.Count;
                return n;
            }
        }

        // 播放策略：轨道（play_as）/ 抢占 / 冷却 / 并发上限
        public static string TrackOf(EventAudioTemplate t)
        {
            if (t == null) return "sfx";
            if (!string.IsNullOrEmpty(t.play_as)) return t.play_as;

            switch (t.kind)
            {
                case EventMediaKind.music:      return "bgm";
                case EventMediaKind.voice:      return "voice";
                case EventMediaKind.loop_sound: return "ambient";
            }
            return "sfx";
        }

        private static bool IsExclusive(string track)
        {
            return !string.IsNullOrEmpty(track) && !string.Equals(track, "sfx", StringComparison.Ordinal);
        }

        // 统计某个模板 key 当前还在播的实例数（去重用）
        public static int CountLive(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;

            int n = 0;
            foreach (var kv in _active)
            {
                var list = kv.Value;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i];
                    if (a == null || a.stopped) continue;
                    if (a.key == key || a.srcKey == key) n++;
                }
            }
            return n;
        }

        // 同源音效的规范化标识（跨模板比较用：同一个文件 / 同一个原版音源 / 同一首曲目…）
        public static string SourceKeyOf(EventAudioTemplate t)
        {
            if (t == null || string.IsNullOrEmpty(t.name)) return null;

            switch (t.kind)
            {
                case EventMediaKind.file:
                    return "file:" + (EventAudioFileLibrary.Resolve(t.name) ?? t.name);
                case EventMediaKind.music:    return "music:" + t.name;
                case EventMediaKind.snapshot: return "snap:" + t.name;
                case EventMediaKind.ui_enum:  return "uienum:" + t.name;
                default:                      return "snd:" + (ResolveSoundPath(t.name) ?? t.name);
            }
        }

        // 这条音源现在还在响吗（用于去重）——
        private static bool StillAudible(Active a)
        {
            if (a == null || !a.started || a.stopped || a.t == null) return false;
            if (a.t.IsLoop) return true;

            // file 类自建 channel：查真实播放状态（时间窗口对长音频不准）
            if (a.kind == EventMediaKind.file && a.handle != null)
                return HandleIsPlaying(a);

            float age = Time.unscaledTime - a.startTime;
            if (age < 0f) return true;

            float len = a.audioSeconds > 0f ? a.audioSeconds : UNKNOWN_LENGTH_WINDOW;
            return age < len;
        }

        // 去重：同一个音源已经在响 ⇒ 本次不播（防叠加）。可按模板 dedup=false 关掉
        private static bool PassDedup(Active a)
        {
            if (a == null || a.t == null || !a.t.dedup) return true;

            string key = SourceKeyOf(a.t);
            if (string.IsNullOrEmpty(key)) return true;

            foreach (var kv in _active)
            {
                var list = kv.Value;
                if (list == null) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    var other = list[i];
                    if (other == null || ReferenceEquals(other, a)) continue;
                    if (!StillAudible(other)) continue;
                    if (SourceKeyOf(other.t) != key) continue;

                    Debug.Log("[MoreFood/Event][Media] 同一个音源已在响（'" + other.key + "' → " + key
                              + "）→ 本次不播（'" + a.key + "'）");
                    return false;
                }
            }
            return true;
        }

        // 互斥静默：模板 mute_when 里列出的音效【正在响】⇒ 本次不播（单向：只约束自己，不约束对方）。
        private static bool PassMute(Active a)
        {
            if (a == null || a.t == null) return true;

            var list = a.t.mute_when;
            if (list == null || list.Count == 0) return true;

            for (int k = 0; k < list.Count; k++)
            {
                string other = list[k];
                if (string.IsNullOrEmpty(other)) continue;
                other = other.Trim();

                foreach (var kv in _active)
                {
                    var actives = kv.Value;
                    if (actives == null) continue;

                    for (int i = 0; i < actives.Count; i++)
                    {
                        var live = actives[i];
                        if (live == null || ReferenceEquals(live, a)) continue;
                        if (!StillAudible(live)) continue;
                        if (live.key != other && live.srcKey != other) continue;

                        Debug.Log("[MoreFood/Event][Media] 音效 '" + a.key + "' 静默：互斥的 '"
                                  + other + "' 正在播放（mute_when）");
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool PassPolicy(EventAudioTemplate t)
        {
            if (t == null) return true;

            if (t.cooldown > 0f)
            {
                float last;
                if (_lastPlayed.TryGetValue(t.key, out last))
                {
                    float left = t.cooldown - (EventSystem.GetGameTime() - last);
                    if (left > 0f)
                    {
                        Debug.Log("[MoreFood/Event][Media] 音效 '" + t.key + "' 冷却中（还剩 "
                                  + left.ToString("F1") + "s）→ 这次不播");
                        return false;
                    }
                }
            }

            if (t.max_concurrent > 0 && CountLive(t.key) >= t.max_concurrent)
            {
                Debug.Log("[MoreFood/Event][Media] 音效 '" + t.key + "' 已达并发上限 "
                          + t.max_concurrent + " → 这次不播");
                return false;
            }
            return true;
        }

        private static void TakeTrack(Active a)
        {
            if (a == null) return;

            string track = TrackOf(a.t);
            a.track = track;
            if (!IsExclusive(track)) return;

            Active cur;
            if (_tracks.TryGetValue(track, out cur) && cur != null && !ReferenceEquals(cur, a) && !cur.stopped)
            {
                Debug.Log("[MoreFood/Event][Media] 轨道 " + track + " 被 '" + a.key
                          + "' 抢占（原占用者 '" + cur.key + "'）");
                StopOne(cur, "被 " + a.key + " 抢占");
            }

            _tracks[track] = a;
            if (string.Equals(track, "bgm", StringComparison.Ordinal)) HoldDynamicMusic();
        }

        private static void ReleaseTrack(Active a)
        {
            if (a == null) return;

            string track = a.track;
            a.track = null;
            if (!IsExclusive(track)) return;

            Active cur;
            if (_tracks.TryGetValue(track, out cur) && ReferenceEquals(cur, a))
            {
                _tracks.Remove(track);
                if (string.Equals(track, "bgm", StringComparison.Ordinal)) ReleaseDynamicMusic();
            }
        }

        private static void HoldDynamicMusic()
        {
            try
            {
                var mm = MusicManager.instance;
                if (mm == null) return;
                mm.SetDynamicMusicPaused();
                _dynamicMusicHeld++;
                Debug.Log("[MoreFood/Event][Media] 已暂停原版动态音乐（自定义 BGM 独占中）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 暂停动态音乐失败: " + e);
            }
        }

        private static void ReleaseDynamicMusic()
        {
            if (_dynamicMusicHeld <= 0) return;

            _dynamicMusicHeld--;
            if (_dynamicMusicHeld > 0) return;

            try
            {
                var mm = MusicManager.instance;
                if (mm != null) mm.SetDynamicMusicUnpaused();
                Debug.Log("[MoreFood/Event][Media] 已恢复原版动态音乐");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 恢复动态音乐失败: " + e);
            }
        }

        private static void ReleaseDynamicMusicAll()
        {
            if (_dynamicMusicHeld <= 0) return;
            _dynamicMusicHeld = 0;
            try
            {
                var mm = MusicManager.instance;
                if (mm != null) mm.SetDynamicMusicUnpaused();
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:ReleaseDynamicMusicAll", "[Media] ReleaseDynamicMusicAll 失败 → 已跳过本步，流程继续", e); }
        }

        // 外部（信号 / 开关）用的独立播放通道：没有事件实例也能播
        internal static Active PlayStandalone(string key, string contextId, string reason)
        {
            return PlanCore(contextId, null, "external:" + (reason ?? "?"), key,
                            EventMediaKind.sound, null, null);
        }

        // 停止一个独立音源（不属于任何事件实例时用它），可给出停止原因
        internal static void StopStandalone(Active a, string reason)
        {
            if (a != null) StopOne(a, reason ?? "外部停止");
        }

        // 把一个独立音源从播放表摘掉（标记已停 + 从所属实例列表移除）
        internal static void DetachStandalone(Active a)
        {
            if (a == null) return;
            a.stopped = true;
            DropFromOwner(a);
        }

        // 三个时机入口（由 EventSystem 调用）
        public static void PlayOnTrigger(EventDef def, EventInstance inst)
        {
            if (def == null || inst == null || def.media == null) return;

            var b = def.media.on_trigger;
            if (b != null)
            {
                Plan(def, inst, "on_trigger.sound", b.sound, EventMediaKind.sound, null, null);
                Plan(def, inst, "on_trigger.music", b.music, EventMediaKind.music, null, null);
                Plan(def, inst, "on_trigger.voice", b.voice, EventMediaKind.voice, null, null);
                EventAudioScriptRegistry.Invoke(b.script, def, inst, "on_trigger");
            }
        }

        // 玩家 / 保底做出选择时
        public static void PlayOnChoice(EventDef def, EventInstance inst)
        {
            if (def == null || inst == null || def.media == null) return;

            var b = def.media.on_choice;
            if (b != null)
            {
                Plan(def, inst, "on_choice.sound", b.sound, EventMediaKind.sound, null, null);
                Plan(def, inst, "on_choice.music", b.music, EventMediaKind.music, null, null);
                EventAudioScriptRegistry.Invoke(b.script, def, inst, "on_choice");
            }
        }

        // 事件效果结束（duration 到期 / 被强制收尾）：停掉持续类音频 + 播 on_end
        public static void OnEffectEnd(EventDef def, EventInstance inst)
        {
            if (inst == null) return;
            StopAll(inst, "效果结束", true);   // stop_on_end=false 的留到收尾再停

            if (def == null || def.media == null || def.media.on_end == null) return;
            var b = def.media.on_end;
            Plan(def, inst, "on_end.sound", b.sound, EventMediaKind.sound, null, null);
            Plan(def, inst, "on_end.music", b.music, EventMediaKind.music, null, null);
            EventAudioScriptRegistry.Invoke(b.script, def, inst, "on_end");
        }

        // ★读档重接：事件还在跑，但它 on_trigger 起的音频不会自己回来（媒体表不存盘）。
        public static int ReplayPersistent(EventDef def, EventInstance inst)
        {
            if (def == null || inst == null || def.media == null) return 0;

            var b = def.media.on_trigger;
            if (b == null) return 0;

            int n = 0;
            n += ReplaySlot(def, inst, "on_trigger.sound", b.sound, EventMediaKind.sound);
            n += ReplaySlot(def, inst, "on_trigger.music", b.music, EventMediaKind.music);
            n += ReplaySlot(def, inst, "on_trigger.voice", b.voice, EventMediaKind.voice);
            return n;
        }

        // ★读档重接：决策效果还在跑时，重接 on_choice 起的持续类音频
        public static int ReplayPersistentChoice(EventDef def, EventInstance inst)
        {
            if (def == null || inst == null || def.media == null) return 0;

            var c = def.media.on_choice;
            if (c == null) return 0;

            int n = 0;
            n += ReplaySlot(def, inst, "on_choice.sound", c.sound, EventMediaKind.sound);
            n += ReplaySlot(def, inst, "on_choice.music", c.music, EventMediaKind.music);
            return n;
        }

        private static int ReplaySlot(EventDef def, EventInstance inst, string slot, string value,
                                      EventMediaKind defaultKind)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            try
            {
                if (!EventAudioFactory.IsKnownKey(value)) return 0;   // 读档路径不重复刷告警

                var t = EventAudioFactory.Resolve(value, inst);
                if (t == null) return 0;

                bool persistent = t.IsLoop
                                  || t.kind == EventMediaKind.music
                                  || t.kind == EventMediaKind.snapshot;
                if (!persistent) return 0;

                return Plan(def, inst, slot + "(读档重接)", value, defaultKind, null, null) != null ? 1 : 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 读档重接 '" + value + "' 异常: " + e);
                return 0;
            }
        }

        // 收尾：停掉剩余（含 on_end 起的）+ 丢弃还在延时中的
        public static void Cleanup(EventInstance inst)
        {
            if (inst == null) return;
            StopAll(inst, "事件收尾");
        }

        public static void Clear()
        {
            try
            {
                foreach (var kv in _active)
                {
                    var list = kv.Value;
                    if (list == null) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var a = list[i];
                        if (a == null) continue;
                        CancelTimers(a);
                        if (a.started) TryStop(a, "重置");
                        a.stopped = true;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Media] Clear 异常: " + e); }

            _active.Clear();
            _tracks.Clear();
            _lastPlayed.Clear();
            ReleaseDynamicMusicAll();
            // 注意顺序：上面已 TryStop 掉全部 active（含 file 类 channel）→ 此处 release 才安全
            EventAudioFilePlayer.ReleaseAll();
            EventWarnOnce.ResetPrefix("media:");     // 新一轮/重置后，告警可以重新提醒
        }

        // 定时（全部走 GameScheduler 闹钟，没有每秒轮询）
        private static void ScheduleDelay(Active a, float seconds)
        {
            var gs = GameScheduler.Instance;
            if (gs == null)
            {
                WarnOnce("sched:null", "GameScheduler 不可用 → delay 被忽略，立即起播");
                StartActive(a);
                return;
            }

            var self = a;
            try
            {
                a.delayHandle = gs.Schedule("MoreFood_EventAudio_Delay", seconds,
                    new System.Action<object>(delegate(object data)
                    {
                        if (self.stopped) return;
                        StartActive(self);
                    }), null, null);

                Debug.Log("[MoreFood/Event][Media] " + a.slot + " 已排期 '" + a.key + "'，"
                          + seconds.ToString("F1") + "s 后起播");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] delay 排闹钟失败（改为立即起播）: " + e);
                StartActive(a);
            }
        }

        private static void ScheduleStop(Active a, float seconds)
        {
            var gs = GameScheduler.Instance;
            if (gs == null)
            {
                WarnOnce("sched:stop", "GameScheduler 不可用 → play_seconds 被忽略（该音频不会自动停）");
                return;
            }

            var self = a;
            try
            {
                a.stopHandle = gs.Schedule("MoreFood_EventAudio_Stop", seconds,
                    new System.Action<object>(delegate(object data)
                    {
                        if (self.stopped) return;
                        StopOne(self, "到点");
                    }), null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 到点停 排闹钟失败: " + e);
            }
        }

        // 没播成：告警 + 标记 + 立刻从跟踪表摘掉（不留"僵尸 Active"，ActiveCount 也准）
        private static void FailActive(Active a, string tag, string msg)
        {
            WarnOnce(tag, msg);
            if (a == null) return;
            a.stoppable = false;
            a.stopped = true;
            ReleaseTrack(a);
            DropFromOwner(a);
        }

        private static void DropFromOwner(Active a)
        {
            try
            {
                List<Active> owner;
                if (a.owner != null) a.owner.Remove(a);
                if (a.owner != null && a.owner.Count == 0
                    && _active.TryGetValue(a.instId, out owner) && ReferenceEquals(owner, a.owner))
                    _active.Remove(a.instId);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:DropFromOwner", "[Media] DropFromOwner 失败 → 已跳过本步，流程继续", e); }
        }

        private static void CancelTimers(Active a)
        {
            if (a == null) return;
            try { if (a.delayHandle.IsValid) a.delayHandle.ClearScheduler(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:CancelTimers", "[Media] CancelTimers 失败 → 已跳过本步，流程继续", e); }
            try { if (a.stopHandle.IsValid) a.stopHandle.ClearScheduler(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:CancelTimers#2", "[Media] CancelTimers 失败 → 已跳过本步，流程继续", e); }
            a.delayHandle = default(SchedulerHandle);
            a.stopHandle = default(SchedulerHandle);
        }

        // 脚本 API 后端（EventAudioContext 调这些）
        public static EventAudioHandle ScriptPlay(EventDef def, EventInstance inst, string slot,
                                                  string key, float? delaySeconds, bool? forceLoop)
        {
            var a = Plan(def, inst, "script:" + (slot ?? "?"), key, EventMediaKind.sound,
                         delaySeconds, forceLoop);
            return MakeHandle(a, key);
        }

        // 脚本入口：停掉某个事件实例名下的全部音源
        public static void ScriptStopAll(EventInstance inst)
        {
            if (inst == null) return;
            StopAll(inst, "脚本 StopAll");
        }

        // 脚本入口：判断某个事件实例名下这个 key 是否还在播
        public static bool ScriptIsPlaying(EventInstance inst, string key)
        {
            if (inst == null || string.IsNullOrEmpty(key)) return false;
            List<Active> list;
            if (!_active.TryGetValue(inst.instanceId, out list) || list == null) return false;

            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || !a.started || a.stopped) continue;
                // 解析后的 key 与 yaml/脚本里写的原值都算命中（重定向场景也能查得到）
                if (a.key == key || a.srcKey == key) return true;
            }
            return false;
        }

        // 判断一个音源是否"正在播"（已起播且未被停）
        internal static bool HandleIsPlaying(Active a)
        {
            return a != null && a.started && !a.stopped;
        }

        // 处理一次停止请求（原因缺省为「脚本」）
        internal static void HandleStop(Active a, string reason)
        {
            if (a != null) StopOne(a, reason ?? "脚本");
        }

        // 没有真的排上期时也给一个【空句柄】（IsPlaying=false、Stop() 空操作），
        private static EventAudioHandle MakeHandle(Active a, string requestedKey)
        {
            if (a == null)
            {
                return new EventAudioHandle
                {
                    active = null,
                    Key = requestedKey,
                    SourceKey = requestedKey,
                    SoundName = null,
                    Slot = null,
                };
            }

            return new EventAudioHandle
            {
                active = a,
                Key = a.key,
                SourceKey = a.srcKey,
                SoundName = a.name,
                Slot = a.slot,
            };
        }

        // 排期：key / 原值 → 模板 → Active（延时到点才起播）
        private static Active Plan(EventDef def, EventInstance inst, string slot, string value,
                                   EventMediaKind defaultKind, float? delayOverride, bool? forceLoop)
        {
            if (inst == null) return null;
            return PlanCore(inst.instanceId, inst, slot, value, defaultKind, delayOverride, forceLoop);
        }

        private static Active PlanCore(string instId, EventInstance inst, string slot, string value,
                                       EventMediaKind defaultKind, float? delayOverride, bool? forceLoop)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (string.IsNullOrEmpty(instId)) return null;

            Active a = null;
            try
            {
                EventAudioTemplate t;
                if (!EventAudioFactory.IsKnownKey(value))
                {
                    Debug.LogWarning("[MoreFood/Event][Media] 音频模板键 '" + value + "'（" + slot
                                   + "）未注册 → 降级按原版音源名直接播放；"
                                   + "若这不是有意为之，检查键名拼写 / 大小写");
                    t = EventAudioTemplate.Raw(value, defaultKind);
                }
                else
                {
                    // 重定向在这里跟到底；返回 null = 这次不播（有意为之，不是错误）
                    t = EventAudioFactory.Resolve(value, inst);
                    if (t == null) return null;

                    if (t.variants != null && t.variants.Count > 0)
                    {
                        var pick = t.PickVariant();
                        if (pick == null)
                        {
                            Debug.LogWarning("[MoreFood/Event][Media] 音效 '" + t.key
                                           + "' 没有任何可用变体（weight 全 ≤0？）→ 这次不播");
                            return null;
                        }
                        t = t.Derive(pick);
                    }
                }

                if (!PassPolicy(t)) return null;

                float delay = delayOverride.HasValue ? delayOverride.Value : t.delay;

                a = new Active
                {
                    instId = instId,
                    inst = inst,
                    kind = t.kind,
                    key = t.key,
                    srcKey = value,
                    name = t.name,
                    slot = slot,
                    t = t,
                    forceLoop = forceLoop.HasValue ? forceLoop.Value : false,
                    stoppable = false,
                    honorStopOnEnd = t.stop_on_end,
                    stopMode = t.stop_mode,
                    stopSignal = t.stop_on_signal,
                };

                List<Active> list;
                if (!_active.TryGetValue(instId, out list))
                {
                    list = new List<Active>();
                    _active[instId] = list;
                }
                a.owner = list;
                list.Add(a);

                if (delay > 0f) ScheduleDelay(a, delay);
                else StartActive(a);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] " + slot + " 排期异常（不影响事件本体）: " + e);
                if (a != null) { DropFromOwner(a); a = null; }
            }
            return a;
        }

        private static void StartActive(Active a)
        {
            if (a.started || a.stopped) return;
            a.started = true;
            a.startTime = Time.unscaledTime;

            if (!PassDedup(a) || !PassMute(a))
            {
                a.stopped = true;
                DropFromOwner(a);
                return;
            }

            TakeTrack(a);
            if (a.t != null) _lastPlayed[a.key] = EventSystem.GetGameTime();

            try
            {
                switch (a.kind)
                {
                    case EventMediaKind.ui_enum:  PlayUIEnum(a); break;
                    case EventMediaKind.snapshot: PlaySnapshot(a); break;
                    case EventMediaKind.music:    PlayMusic(a); break;
                    case EventMediaKind.file:     PlayFile(a); break;
                    default:                      PlaySoundSlot(a); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 起播 '" + a.key + "' 异常: " + e);
                a.stoppable = false;
                a.stopped = true;
                DropFromOwner(a);
            }

            // 停止条件②：到点（闹钟，非轮询）
            if (!a.stopped && a.t != null)
            {
                float secs = a.t.GetPlaySeconds();
                if (secs > 0f) ScheduleStop(a, secs);
            }
        }

        // ---- 一次性 / 循环音效 ----
        private static void PlaySoundSlot(Active a)
        {
            var t = a.t;
            bool ui = t.kind == EventMediaKind.ui_sound;

            string path = ResolveSoundPath(t.name);
            if (path == null)
            {
                FailActive(a, "unknown:" + t.name, "找不到音效 '" + t.name + "' → 跳过");
                return;
            }

            bool hasParam = !string.IsNullOrEmpty(t.param);
            bool hasLabel = hasParam && !string.IsNullOrEmpty(t.labeled_param_value);
            Vector3 pos = ui ? Vector3.zero : ResolvePosition(t);
            bool isLoop = a.forceLoop || t.IsLoop;

            // ① 循环 + 要跟游戏一起暂停 ⇒ 原版 LoopingSoundManager（暂停行为/剔除/音量总线归它管，代价：不能 seek）
            if (isLoop && t.pause_with_game)
            {
                if (t.start_ms > 0 || t.end_ms > 0)
                    WarnOnce("looprange:" + t.key,
                             "pause_with_game=true 的循环音走原版 LoopingSoundManager，实例由它创建 → "
                             + "start_ms / end_ms 被忽略（想要片段区间请把 pause_with_game 设为 false）");

                var mgr = LoopingSoundManager.Get();
                if (mgr == null) { FailActive(a, "loopmgr:null", "LoopingSoundManager 不可用 → 循环音跳过"); return; }

                object h = CallLoopStart(path, pos, true);
                if (h == null) { FailActive(a, "loopstart:" + t.name, "循环音起播失败 '" + t.name + "' → 跳过"); return; }

                if (hasParam) CallLoopParam(h, mgr, t.param, t.param_value);
                a.handle = h;
                a.loopOwner = mgr;
                a.stoppable = true;
                SubscribeSignal(a);

                Debug.Log("[MoreFood/Event][Media] 已起播循环 '" + a.key + "'（" + a.name
                          + "，原版托管：跟游戏暂停" + StopDesc(a) + "）");
                return;
            }

            // ② 需要自持句柄（循环的默认路径 / 一次性带片段或限时）⇒ 能 seek、能停，且暂停时继续响
            if (isLoop || t.NeedsSelfHandle)
            {
                object h = CallBeginOneShot(path, pos, ui ? 1f : t.volume);
                if (h == null) { FailActive(a, "begin:" + t.name, "创建音频实例失败 '" + t.name + "' → 跳过"); return; }

                if (t.start_ms > 0) EvSetTimeline(h, t.start_ms);
                if (hasLabel) EvSetParamLabel(h, t.param, t.labeled_param_value);
                else if (hasParam) EvSetParam(h, t.param, t.param_value);
                EvStart(h);

                a.handle = h;
                a.stoppable = true;
                SubscribeSignal(a);

                Debug.Log("[MoreFood/Event][Media] 已起播" + (isLoop ? "循环" : "") + " '" + a.key + "'（"
                          + a.name + "，" + DescribeRange(t)
                          + (isLoop ? "，暂停时继续响" : "") + StopDesc(a) + "）");
                return;
            }

            // ③ 普通一次性 ⇒ 走原版一次性接口（零反射）
            if (ui)
            {
                if (hasLabel) KFMOD.PlayUISoundWithLabeledParameter(path, t.param, t.labeled_param_value);
                else if (hasParam) KFMOD.PlayUISoundWithParameter(path, t.param, t.param_value);
                else CallPlayUISound(path);
            }
            else
            {
                if (hasLabel) KFMOD.PlayOneShotWithLabeledParameter(path, pos, t.param, t.labeled_param_value, t.volume);
                else if (hasParam) KFMOD.PlayOneShotWithParameter(path, pos, t.param, t.param_value, t.volume);
                else KFMOD.PlayOneShot(path, pos, t.volume);
            }

            a.stoppable = false;
            SubscribeSignal(a);
            Debug.Log("[MoreFood/Event][Media] 已播放 '" + a.key + "'（" + t.name + "）");
        }

        private static string StopDesc(Active a)
        {
            if (a == null) return "";
            if (!string.IsNullOrEmpty(a.stopSignal)) return "，停于信号 " + a.stopSignal;
            return "";
        }

        // 停止条件③：订阅推送（零轮询）
        private static void SubscribeSignal(Active a)
        {
            if (a == null || string.IsNullOrEmpty(a.stopSignal)) return;

            try
            {
                var self = a;
                System.Action<object> cb = delegate(object data)
                {
                    Debug.Log("[MoreFood/Event][Media] 收到信号 " + self.stopSignal
                              + " → 停 '" + self.key + "'");
                    StopOne(self, "信号 " + self.stopSignal);
                };

                if (EventSignal.IsCustomName(a.stopSignal))
                {
                    EventSignal.Subscribe(a.stopSignal, cb);
                    a.signalCustom = true;
                    a.signalCb = cb;
                    return;
                }

                GameHashes hash;
                if (!Enum.TryParse<GameHashes>(a.stopSignal, true, out hash))
                {
                    Debug.LogWarning("[MoreFood/Event][Media] 模板 '" + a.key + "' 的 stop_on_signal '"
                                   + a.stopSignal + "' 既不是自定义信号（要带 '.' 或 ':'），"
                                   + "也不是 GameHashes 里的名字 → 不订阅");
                    return;
                }

                var game = Game.Instance;
                if (game == null) { WarnOnce("sig:game", "Game.Instance 为 null，stop_on_signal 未订阅"); return; }

                game.Subscribe((int)hash, cb);
                a.signalCustom = false;
                a.signalHash = (int)hash;
                a.signalGame = game;        // ★ 记录订阅实例（退订必须用同一个）
                a.signalCb = cb;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 订阅 stop_on_signal 异常: " + e);
            }
        }

        private static void UnsubscribeSignal(Active a)
        {
            if (a == null || a.signalCb == null) return;

            try
            {
                if (a.signalCustom)
                {
                    EventSignal.Unsubscribe(a.stopSignal, a.signalCb);
                }
                else
                {
                    var game = a.signalGame != null ? a.signalGame : Game.Instance;   // ★ 用订阅时的实例退订
                    if (game != null) game.Unsubscribe(a.signalHash, a.signalCb);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 退订 stop_on_signal 异常: " + e);
            }

            a.signalCb = null;
            a.signalGame = null;
            a.signalHash = 0;
            a.signalCustom = false;
        }

        // ---- 自定义音频文件（Mod 的 sounds/ 下，走 FMOD core 解码）----
        private static void PlayFile(Active a)
        {
            var t = a.t;

            string path = EventAudioFileLibrary.Resolve(t.name);
            if (path == null)
            {
                FailActive(a, "file:" + t.name, "找不到自定义音频文件 '" + t.name
                         + "'（在 Mod 根目录的 sounds/ 下没找到）→ 跳过");
                return;
            }

            if (!string.IsNullOrEmpty(t.param) || !string.IsNullOrEmpty(t.labeled_param_value))
                WarnOnce("fileparam:" + t.key,
                         "自定义音频文件没有 FMOD 参数，param / labeled_param_value 被忽略（" + t.key + "）");
            if (t.spatial)
            {
                WarnOnce("filespatial:" + t.key,
                         "自定义文件的空间音只在【起播那一刻】取一次位置，不跟随实体（" + t.key
                         + "，position=" + (t.position ?? "-") + "）");
                if (!string.Equals(t.position, "zero", StringComparison.OrdinalIgnoreCase))
                    WarnOnce("filespatial2:" + t.key,
                             "spatial=true 且 position=listener ⇒ 音源就在听者身上，等于不衰减（" + t.key + "）");
            }

            bool isLoop = a.forceLoop || t.IsLoop;
            float secs = t.GetPlaySeconds();

            string detail;
            float audioSeconds;
            object ch = EventAudioFilePlayer.Play(t, path, isLoop, out detail, out audioSeconds);
            a.audioSeconds = audioSeconds;
            if (ch == null)
            {
                FailActive(a, "fileplay:" + t.name,
                           "自定义音频起播失败 '" + t.name + "'（" + detail + "）→ 跳过");
                return;
            }

            a.handle = ch;
            a.stoppable = isLoop || secs > 0f;
            SubscribeSignal(a);

            Debug.Log("[MoreFood/Event][Media] 已起播自定义文件 '" + a.key + "'（" + t.name + "，2D，"
                      + (isLoop ? "循环" : (secs > 0f ? "限时 " + secs.ToString("F0") + "s" : "一次"))
                      + "，" + detail + "，" + (a.stoppable ? "可停" : "自然播完") + StopDesc(a) + "）");
        }

        // ---- 原版曲目 ----
        private static void PlayMusic(Active a)
        {
            var t = a.t;
            var mm = MusicManager.instance;
            if (mm == null) { FailActive(a, "music:null", "MusicManager 不可用 → 跳过"); return; }

            var map = mm.SongMap;
            if (map != null && !map.ContainsKey(t.name))
            {
                FailActive(a, "song:" + t.name, "曲名 '" + t.name + "' 不在原版曲目表里 → 跳过");
                return;
            }

            bool alreadyPlaying = false;
            try { alreadyPlaying = mm.SongIsPlaying(t.name); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:PlayMusic", "[Media] PlayMusic 失败 → 已跳过本步，流程继续", e); }

            mm.PlaySong(t.name, false);

            if (alreadyPlaying)
                WarnOnce("music:shared:" + t.name, "曲目 '" + t.name
                         + "' 在我们起播前就已在播（原版或别的 Mod 起的）→ 只共用，结束时不会去停它");

            if (t.start_ms > 0 && !SeekMusic(t.name, t.start_ms))
                WarnOnce("seek:" + t.name, "曲目 '" + t.name + "' 无法定位到 " + t.start_ms
                                           + "ms（原版内部结构变了？）→ 从头播");

            a.name = t.name;
            a.stoppable = !alreadyPlaying;      // 别人起的曲目不归我们停
            a.honorStopOnEnd = t.stop_on_end;
            SubscribeSignal(a);
            Debug.Log("[MoreFood/Event][Media] 已起播曲目 '" + t.name + "'（" + DescribeRange(t)
                      + StopDesc(a) + "）");
        }

        // ---- 混音快照 ----
        private static void PlaySnapshot(Active a)
        {
            var t = a.t;

            if (t.start_ms > 0 || t.end_ms > 0)
                WarnOnce("snaprange:" + t.key, "快照没有时间轴概念，start_ms / end_ms 被忽略（" + t.key + "）");

            object evRef = ResolveSnapshot(t.name);
            if (evRef == null) { FailActive(a, "snap:" + t.name, "找不到快照 '" + t.name + "' → 跳过"); return; }

            var mixer = AudioMixer.instance;
            if (mixer == null) { FailActive(a, "mixer:null", "AudioMixer 不可用 → 跳过"); return; }

            var mi = FindMethod(typeof(AudioMixer), "Start", 1, evRef.GetType());
            if (mi == null) { FailActive(a, "mixer:start", "AudioMixer.Start 签名变了 → 跳过"); return; }

            bool alreadyActive = SnapshotIsActive(evRef);
            mi.Invoke(mixer, new object[] { evRef });

            if (alreadyActive)
                WarnOnce("snap:shared:" + t.name, "快照 '" + t.name
                         + "' 在我们切换前就已生效（原版或别的 Mod 切的）→ 只共用，结束时不会去停它");

            a.handle = evRef;
            a.stoppable = !alreadyActive;
            a.honorStopOnEnd = t.stop_on_end;
            SubscribeSignal(a);
            Debug.Log("[MoreFood/Event][Media] 已切换快照 '" + t.name + "'"
                      + (t.GetPlaySeconds() > 0f ? "（" + t.GetPlaySeconds().ToString("F0") + "s 后停）" : "")
                      + StopDesc(a));
        }

        // ---- 原版 UI 枚举音 ----
        private static void PlayUIEnum(Active a)
        {
            var t = a.t;

            if (t.NeedsSelfHandle || t.IsLoop)
                WarnOnce("uienumtime:" + t.key,
                         "UISounds 枚举音拿不到句柄，loop / play_seconds / start_ms / end_ms 被忽略（" + t.key + "）");

            if (!string.IsNullOrEmpty(t.stop_on_signal))
                WarnOnce("uienumsig:" + t.key,
                         "UISounds 枚举音不持有句柄，stop_on_signal 没有可停的目标，被忽略（" + t.key + "）");

            UISounds.Sound s;
            if (!Enum.TryParse<UISounds.Sound>(t.name, true, out s) || !Enum.IsDefined(typeof(UISounds.Sound), s))
            {
                FailActive(a, "uienum:" + t.name, "UISounds.Sound 里没有 '" + t.name + "' → 跳过");
                return;
            }
            if (UISounds.Instance == null) { FailActive(a, "uienum:null", "UISounds 未就绪 → 跳过"); return; }

            UISounds.PlaySound(s);
            a.stoppable = false;
            Debug.Log("[MoreFood/Event][Media] 已播放 UI 音 '" + t.name + "'");
        }

        // 停止
        private static void StopAll(EventInstance inst, string reason, bool onlyStopOnEnd = false)
        {
            List<Active> list;
            if (!_active.TryGetValue(inst.instanceId, out list)) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var a = list[i];
                if (a == null) { list.RemoveAt(i); continue; }

                if (!a.started)
                {
                    Debug.Log("[MoreFood/Event][Media] 丢弃还在延时中的 '" + a.key + "'（" + reason + "）");
                    CancelTimers(a);
                    a.stopped = true;
                    list.RemoveAt(i);
                    continue;
                }

                if (onlyStopOnEnd && !a.honorStopOnEnd) continue;   // 留给收尾

                CancelTimers(a);
                TryStop(a, reason);
                a.stopped = true;
                list.RemoveAt(i);
            }

            if (list.Count == 0) _active.Remove(inst.instanceId);
        }

        private static void StopOne(Active a, string reason)
        {
            if (a == null || a.stopped) return;
            CancelTimers(a);
            TryStop(a, reason);
            a.stopped = true;

            DropFromOwner(a);
        }

        private static void TryStop(Active a, string reason)
        {
            if (a == null) return;
            UnsubscribeSignal(a);            // 任何一次下线都退订（不只是为了停止），避免回调悬挂
            ReleaseTrack(a);
            if (!a.stoppable) return;
            try
            {
                switch (a.kind)
                {
                    case EventMediaKind.music:
                        StopMusic(a.name, a.stopMode, reason);
                        break;
                    case EventMediaKind.snapshot:
                        StopSnapshot(a.handle, a.stopMode, reason);
                        break;
                    case EventMediaKind.file:
                        Debug.Log("[MoreFood/Event][Media] 已停自定义文件 '" + a.key + "'（" + reason
                                  + "，" + EventAudioFilePlayer.Stop(a.handle) + "）");
                        break;
                    default:
                        if (a.handle == null) break;
                        if (a.loopOwner != null) CallLoopStop(a.handle, a.loopOwner, a.key, reason);
                        else EvStopAndRelease(a.handle, a.stopMode);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Media] 停止 '" + (a.key ?? a.name) + "' 异常: " + e);
            }
        }

        private static void StopMusic(string songName, string stopMode, string reason)
        {
            var mm = MusicManager.instance;
            if (mm == null || string.IsNullOrEmpty(songName)) return;

            var mi = FindStopSong();
            if (mi == null) return;

            object mode = ParseStopMode(mi.GetParameters()[2].ParameterType, stopMode);
            mi.Invoke(mm, new object[] { songName, false, mode });
            Debug.Log("[MoreFood/Event][Media] 已停曲目 '" + songName + "'（" + reason + "）");
        }

        private static void StopSnapshot(object evRef, string stopMode, string reason)
        {
            if (evRef == null) return;
            var mixer = AudioMixer.instance;
            if (mixer == null) return;

            var mi = FindMethod(typeof(AudioMixer), "Stop", 2, evRef.GetType());
            if (mi == null) return;

            object mode = ParseStopMode(mi.GetParameters()[1].ParameterType, stopMode);
            mi.Invoke(mixer, new object[] { evRef, mode });
        }

        // FMOD.Studio.STOP_MODE 的两个名字取自原版签名，按名字解析（不依赖枚举数值）
        private static object ParseStopMode(Type enumType, string stopMode)
        {
            string n = string.IsNullOrEmpty(stopMode) ? "ALLOWFADEOUT" : stopMode;
            try { return Enum.Parse(enumType, n, true); }
            catch
            {
                WarnOnce("stopmode:" + n, "未知的 stop_mode '" + n + "' → 用 ALLOWFADEOUT");
                return Enum.ToObject(enumType, 0);
            }
        }

        private static string DescribeRange(EventAudioTemplate t)
        {
            if (t == null) return "-";
            if (t.start_ms <= 0 && t.end_ms <= 0)
                return (t.GetPlaySeconds() > 0f) ? "限时 " + t.GetPlaySeconds().ToString("F0") + "s" : "一次";
            return "片段 " + t.start_ms + "~" + (t.end_ms > 0 ? t.end_ms.ToString() : "末尾") + "ms";
        }

        // 音源解析
        public static string ResolveSoundPath(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            try
            {
                string p = GlobalAssets.GetSound(name, true);
                if (!string.IsNullOrEmpty(p)) return p;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:ResolveSoundPath", "[Media] ResolveSoundPath 失败 → 已跳过本步，流程继续", e); }

            if (name.IndexOf(":/", StringComparison.Ordinal) >= 0) return name;
            return null;
        }

        // 解析音频模板的播放位置：position=zero 用世界原点，否则按模板配置的锚点
        internal static Vector3 ResolvePosition(EventAudioTemplate t)
        {
            if (t != null && string.Equals(t.position, "zero", StringComparison.OrdinalIgnoreCase))
                return Vector3.zero;

            try
            {
                var sl = SoundListenerController.Instance;
                if (sl != null) return sl.transform.position;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:ResolvePosition", "[Media] ResolvePosition 失败 → 已跳过本步，流程继续", e); }
            return Vector3.zero;
        }

        // 快照字段名 → 装箱的 EventReference（FMODUnity 类型，csproj 未引用，只能反射）
        public static object ResolveSnapshot(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return null;

            try
            {
                var snaps = AudioMixerSnapshots.Get();
                if (snaps == null) return null;

                var f = typeof(AudioMixerSnapshots).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (f == null || f.FieldType != GetEventReferenceType()) return null;
                return f.GetValue(snaps);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:ResolveSnapshot", "[Media] ResolveSnapshot 失败 → 已按兜底处理", e); return null; }
        }

        // 用反射访问原版曲目内部结构（MusicManager.SongInfo 为 internal）—— 反射字段名以当前游戏版本为准，升级游戏后需重新核对
        private static bool SeekMusic(string songName, int ms)
        {
            try
            {
                var mm = MusicManager.instance;
                if (mm == null) return false;

                var fAct = typeof(MusicManager).GetField("activeSongs",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var dict = fAct != null ? fAct.GetValue(mm) as IDictionary : null;
                if (dict == null || !dict.Contains(songName)) return false;

                object info = dict[songName];
                if (info == null) return false;

                var fEv = info.GetType().GetField("ev",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object ev = fEv != null ? fEv.GetValue(info) : null;
                if (ev == null) return false;

                EvSetTimeline(ev, ms);
                return true;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:SeekMusic", "[Media] SeekMusic 失败 → 已按兜底处理", e); return false; }
        }

        // 反射缓存
        private static MethodInfo _miBegin;               // 反射缓存：KFMOD.BeginOneShot（一次性音源的原版起播入口）
        private static Type _evType;                      // 反射缓存：FMOD EventInstance 类型
        private static MethodInfo _miEvStart;             // 反射缓存：EventInstance.start
        private static MethodInfo _miEvStop;              // 反射缓存：EventInstance.stop
        private static MethodInfo _miEvRelease;           // 反射缓存：EventInstance.release
        private static MethodInfo _miEvSetTimeline;       // 反射缓存：EventInstance.setTimelinePosition
        private static MethodInfo _miEvSetParam;          // 反射缓存：EventInstance.setParameterByName
        private static MethodInfo _miEvSetParamLabel;     // 反射缓存：EventInstance.setParameterByNameWithLabel
        private static MethodInfo _miLoopStart;           // 反射缓存：LoopingSoundManager 起播方法
        private static MethodInfo _miLoopStop;            // 反射缓存：LoopingSoundManager 停止方法
        private static MethodInfo _miLoopParam;           // 反射缓存：LoopingSoundManager 参数设置方法
        private static Type _evRefType;                   // 反射缓存：FMOD EventReference 类型
        private static System.Action<string> _playUISound; // 反射缓存：原版 UI 音效播放入口

        private static void EnsureReflection()
        {
            if (_miBegin == null)
                _miBegin = typeof(KFMOD).GetMethod("BeginOneShot",
                    new Type[] { typeof(string), typeof(Vector3), typeof(float) });

            if (_evType == null && _miBegin != null) _evType = _miBegin.ReturnType;
            if (_evType == null) return;

            const BindingFlags F = BindingFlags.Public | BindingFlags.Instance;
            if (_miEvStart == null) _miEvStart = _evType.GetMethod("start", F, null, Type.EmptyTypes, null);
            if (_miEvStop == null) _miEvStop = FindMethod(_evType, "stop", 1, null);
            if (_miEvRelease == null) _miEvRelease = _evType.GetMethod("release", F, null, Type.EmptyTypes, null);
            if (_miEvSetTimeline == null) _miEvSetTimeline = FindMethod(_evType, "setTimelinePosition", 1, typeof(int));
            if (_miEvSetParam == null) _miEvSetParam = FindMethod(_evType, "setParameterByName", 3, typeof(string));
            if (_miEvSetParamLabel == null) _miEvSetParamLabel = FindMethod(_evType, "setParameterByNameWithLabel", 3, typeof(string));
        }

        // LoopingSoundManager 的句柄类型是 internal，直接调会 CS0122 ⇒ Start/Stop/参数都走反射
        private static void EnsureLoopReflection()
        {
            if (_miLoopStart == null)
                _miLoopStart = typeof(LoopingSoundManager).GetMethod("StartSound",
                    new Type[] { typeof(string), typeof(Vector3), typeof(bool), typeof(bool) });
            if (_miLoopStop == null)
                _miLoopStop = FindMethod(typeof(LoopingSoundManager), "StopSound", 1, null);
            if (_miLoopParam == null)
                _miLoopParam = typeof(LoopingSoundManager).GetMethod("UpdateFirstParameter");
        }

        private static bool SnapshotIsActive(object evRef)
        {
            try
            {
                if (evRef == null) return false;
                var mixer = AudioMixer.instance;
                if (mixer == null) return false;

                var mi = FindMethod(typeof(AudioMixer), "SnapshotIsActive", 1, evRef.GetType());
                if (mi == null) return false;
                return (bool)mi.Invoke(mixer, new object[] { evRef });
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:SnapshotIsActive", "[Media] SnapshotIsActive 失败 → 已按兜底处理", e); return false; }
        }

        private static Type GetEventReferenceType()
        {
            if (_evRefType == null)
            {
                var mi = FindMethod(typeof(AudioMixer), "Start", 1, null);
                if (mi != null) _evRefType = mi.GetParameters()[0].ParameterType;
            }
            return _evRefType;
        }

        private static object CallLoopStart(string path, Vector3 pos, bool pauseWithGame)
        {
            EnsureLoopReflection();
            if (_miLoopStart == null) return null;
            return _miLoopStart.Invoke(null, new object[] { path, pos, pauseWithGame, true });
        }

        private static void CallLoopStop(object handle, object manager, string key, string reason)
        {
            EnsureLoopReflection();
            if (_miLoopStop == null || handle == null || manager == null) return;

            // 换局后 LoopingSoundManager 会换实例，旧句柄属于旧管理器 ⇒ 不能拿去 Stop
            if (!ReferenceEquals(LoopingSoundManager.Get(), manager)) return;

            InvokeLoopMethod(_miLoopStop, manager, new object[] { handle });
            Debug.Log("[MoreFood/Event][Media] 已停循环 '" + key + "'（" + reason + "）");
        }

        private static void CallLoopParam(object handle, object manager, string param, float value)
        {
            EnsureLoopReflection();
            if (_miLoopParam == null || handle == null) return;

            var ps = _miLoopParam.GetParameters();
            if (ps.Length != 3) return;

            object name = param;
            if (ps[1].ParameterType != typeof(string))
            {
                // HashedString：用它的 (string) 构造
                try
                {
                    var ctor = ps[1].ParameterType.GetConstructor(new Type[] { typeof(string) });
                    if (ctor == null) return;
                    name = ctor.Invoke(new object[] { param });
                }
                catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:CallLoopParam", "[Media] CallLoopParam 失败 → 已按兜底处理", e); return; }
            }

            InvokeLoopMethod(_miLoopParam, manager, new object[] { handle, name, value });
        }

        // LoopingSoundManager 的方法有静态（StopSound）也有实例（UpdateFirstParameter）——按 IsStatic 决定目标
        private static void InvokeLoopMethod(MethodInfo mi, object manager, object[] args)
        {
            if (mi == null) return;
            try { mi.Invoke(mi.IsStatic ? null : manager, args); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Media] LoopMethod 异常: " + e); }
        }

        // 创建 + 配置（位置 / 音量），【不启动】—— 启动由 EvStart 负责
        private static object CallBeginOneShot(string path, Vector3 pos, float volume)
        {
            EnsureReflection();
            if (_miBegin == null) return null;
            return _miBegin.Invoke(null, new object[] { path, pos, volume });
        }

        private static void EvStart(object handle)
        {
            EnsureReflection();
            if (_miEvStart == null || handle == null) return;
            _miEvStart.Invoke(handle, null);
        }

        private static void EvStopAndRelease(object handle, string stopMode)
        {
            EnsureReflection();
            if (handle == null) return;

            if (_miEvStop != null)
            {
                object mode = ParseStopMode(_miEvStop.GetParameters()[0].ParameterType, stopMode);
                _miEvStop.Invoke(handle, new object[] { mode });
            }
            if (_miEvRelease != null) _miEvRelease.Invoke(handle, null);
        }

        private static void EvSetTimeline(object handle, int ms)
        {
            EnsureReflection();
            if (_miEvSetTimeline == null || handle == null) return;
            _miEvSetTimeline.Invoke(handle, new object[] { ms });
        }

        private static void EvSetParam(object handle, string name, float value)
        {
            EnsureReflection();
            if (_miEvSetParam == null || handle == null) return;
            _miEvSetParam.Invoke(handle, new object[] { name, value, false });
        }

        private static void EvSetParamLabel(object handle, string name, string label)
        {
            EnsureReflection();
            if (_miEvSetParamLabel == null || handle == null) return;
            _miEvSetParamLabel.Invoke(handle, new object[] { name, label, false });
        }

        // KFMOD.PlayUISound 有 EventReference 重载（FMODUnity 未引用）→ 直接调会 CS0012，改走委托
        private static void CallPlayUISound(string path)
        {
            if (_playUISound == null)
            {
                var mi = typeof(KFMOD).GetMethod("PlayUISound", new Type[] { typeof(string) });
                if (mi == null) return;
                _playUISound = (System.Action<string>)Delegate.CreateDelegate(typeof(System.Action<string>), mi);
            }
            _playUISound(path);
        }

        // 反射查找匹配「名字 + 参数个数 + 首参类型」的方法（JS 桥接用）
        internal static MethodInfo FindMethod(Type type, string name, int paramCount, Type first)
        {
            var arr = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < arr.Length; i++)
            {
                var m = arr[i];
                if (m.Name != name) continue;
                var ps = m.GetParameters();
                if (ps.Length != paramCount) continue;
                if (first != null && ps[0].ParameterType != first) continue;
                return m;
            }
            return null;
        }

        // MusicManager.StopSong(string, bool, STOP_MODE)：三个参数类型逐一核对，
        private static MethodInfo FindStopSong()
        {
            var arr = typeof(MusicManager).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < arr.Length; i++)
            {
                var m = arr[i];
                if (m.Name != "StopSong") continue;
                var ps = m.GetParameters();
                if (ps.Length != 3) continue;
                if (ps[0].ParameterType != typeof(string)) continue;
                if (ps[1].ParameterType != typeof(bool)) continue;
                if (!ps[2].ParameterType.IsEnum) continue;      // STOP_MODE
                return m;
            }
            return null;
        }

        private static void WarnOnce(string tag, string msg)
        {
            EventWarnOnce.Warn("media:" + tag, "[MoreFood/Event][Media] " + msg);
        }

        // 校验 / 清单（给配 yaml 的人用）
        public static int ValidateAll()
        {
            int bad = 0;
            foreach (var t in EventAudioFactory.All)
            {
                if (t == null) continue;

                bool ok;
                switch (t.kind)
                {
                    case EventMediaKind.ui_enum:
                        UISounds.Sound s;
                        ok = Enum.TryParse<UISounds.Sound>(t.name, true, out s)
                             && Enum.IsDefined(typeof(UISounds.Sound), s);
                        break;

                    case EventMediaKind.snapshot:
                        if (AudioMixerSnapshots.Get() == null) continue;   // 资源没加载完，不判
                        ok = ResolveSnapshot(t.name) != null;
                        break;

                    case EventMediaKind.music:
                        var mm = MusicManager.instance;
                        if (mm == null || mm.SongMap == null) continue;    // 时机不到，不判
                        ok = mm.SongMap.ContainsKey(t.name);
                        break;

                    case EventMediaKind.file:
                        ok = EventAudioFileLibrary.Exists(t.name);
                        break;

                    default:
                        var table = GetSoundTable();
                        if (table == null || table.Count == 0) continue;   // SoundTable 还没建，不判
                        ok = ResolveSoundPath(t.name) != null;
                        break;
                }

                if (!ok)
                {
                    bad++;
                    Debug.LogWarning("[MoreFood/Event][Audio] 模板 '" + t.key + "'（" + t.kind
                                     + "）的音源 '" + t.name + "' 解析不了：" + DescribeMiss(t)
                                     + "；播放时会被跳过");
                }

                // 自定义信号（带 '.' / ':'）没法在启动时判定（作者何时 Raise 不可知），只校验原版事件名
                if (!string.IsNullOrEmpty(t.stop_on_signal) && !EventSignal.IsCustomName(t.stop_on_signal))
                {
                    GameHashes sigHash;
                    if (!Enum.TryParse<GameHashes>(t.stop_on_signal, true, out sigHash))
                    {
                        bad++;
                        Debug.LogWarning("[MoreFood/Event][Audio] 模板 '" + t.key + "' 的 stop_on_signal '"
                                       + t.stop_on_signal + "' 既不是自定义信号（要带 '.' 或 ':'），"
                                       + "也不是 GameHashes 里的名字（该音频收不到信号）");
                    }
                }
            }
            return bad;
        }

        // 说明音源为什么解析不了（给配 yaml 的人看）
        private static string DescribeMiss(EventAudioTemplate t)
        {
            if (t == null) return "模板为空";
            string name = t.name;
            if (string.IsNullOrEmpty(name)) return "音源名为空";

            switch (t.kind)
            {
                case EventMediaKind.ui_enum:
                    return "UISounds.Sound 枚举里没有这个名字（要写枚举成员名，如 ClickButton）";

                case EventMediaKind.snapshot:
                    return "AudioMixerSnapshots 里没有这个字段，或字段类型不是 EventReference";

                case EventMediaKind.music:
                    return "MusicManager.SongMap 里没有这首曲目（要写曲名，不是文件名）";

                case EventMediaKind.file:
                    return "sounds/ 下找不到该文件，或扩展名不支持（只认 wav / ogg / mp3，路径相对 sounds/）";

                default:
                    {
                        string near = NearSoundNames(name);
                        return "既不是 SoundTable 里的短名，也不是带 ':/' 的完整路径"
                               + "（短名不带目录和扩展名；完整路径形如 event:/UI/Click）"
                               + (string.IsNullOrEmpty(near) ? "" : "；SoundTable 里相近的有：" + near);
                    }
            }
        }

        // SoundTable 里与给定名字相近的键（最多 3 个，用于定位拼写错误）
        private static string NearSoundNames(string name)
        {
            try
            {
                var table = GetSoundTable();
                if (table == null || table.Count == 0) return null;

                var hits = new List<string>();
                foreach (DictionaryEntry kv in table)
                {
                    string k = kv.Key as string;
                    if (string.IsNullOrEmpty(k)) continue;
                    if (k.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    hits.Add(k);
                    if (hits.Count >= 3) break;
                }
                return hits.Count == 0 ? null : string.Join(" / ", hits.ToArray());
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:NearSoundNames", "[Media] NearSoundNames 失败 → 已按兜底处理", e); return null; }
        }

        // 启动自检：yaml 里引用的模板键 / 脚本键是否都注册过
        public static int ValidateYamlRefs(IEnumerable<EventDef> defs)
        {
            if (defs == null) return 0;
            int bad = 0;

            foreach (var def in defs)
            {
                if (def == null || def.media == null) continue;

                var t = def.media.on_trigger;
                if (t != null)
                {
                    bad += CheckRef(def, "media.on_trigger.sound", t.sound);
                    bad += CheckRef(def, "media.on_trigger.music", t.music);
                    bad += CheckRef(def, "media.on_trigger.voice", t.voice);
                    bad += CheckScript(def, "media.on_trigger.script", t.script);
                }

                var c = def.media.on_choice;
                if (c != null)
                {
                    bad += CheckRef(def, "media.on_choice.sound", c.sound);
                    bad += CheckRef(def, "media.on_choice.music", c.music);
                    bad += CheckScript(def, "media.on_choice.script", c.script);
                }

                var e = def.media.on_end;
                if (e != null)
                {
                    bad += CheckRef(def, "media.on_end.sound", e.sound);
                    bad += CheckRef(def, "media.on_end.music", e.music);
                    bad += CheckScript(def, "media.on_end.script", e.script);
                }
            }

            if (bad > 0)
                Debug.LogWarning("[MoreFood/Event][Audio] yaml 里有 " + bad
                               + " 处引用了未注册的音频模板键 / 脚本键（见上）");
            return bad;
        }

        private static int CheckRef(EventDef def, string slot, string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (EventAudioFactory.IsKnownKey(value)) return 0;   // 含重定向

            Debug.LogWarning("[MoreFood/Event][Audio] 事件 '" + def.id + "' 的 " + slot + " = '"
                           + value + "' 不是已注册的模板键（将按原版音源名直接播）");
            return 1;
        }

        private static int CheckScript(EventDef def, string slot, string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (EventAudioScriptRegistry.Has(value)) return 0;

            Debug.LogWarning("[MoreFood/Event][Audio] 事件 '" + def.id + "' 的 " + slot + " = '"
                           + value + "' 不是已注册的音频脚本键（该时机的脚本不会执行）");
            return 1;
        }

        // maxPerKind：maxper 类别
        public static void DumpAvailableAudio(int maxPerKind = 40)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[MoreFood/Event][Audio] ── 可用音源清单 ──");

                var songs = MusicManager.instance != null ? MusicManager.instance.SongMap : null;
                if (songs != null)
                {
                    sb.Append("曲目（music，共 " + songs.Count + "）：");
                    int n = 0;
                    foreach (var k in songs.Keys) { if (n++ >= maxPerKind) { sb.Append(" ..."); break; } sb.Append(" " + k); }
                    sb.AppendLine();
                }

                var snaps = AudioMixerSnapshots.Get();
                if (snaps != null)
                {
                    sb.Append("快照（snapshot）：");
                    var fs = typeof(AudioMixerSnapshots).GetFields(BindingFlags.Public | BindingFlags.Instance);
                    int n = 0;
                    for (int i = 0; i < fs.Length; i++)
                    {
                        if (fs[i].FieldType != GetEventReferenceType()) continue;
                        if (n++ >= maxPerKind) { sb.Append(" ..."); break; }
                        sb.Append(" " + fs[i].Name);
                    }
                    sb.AppendLine();
                }

                sb.Append("UI 枚举音（ui_enum）：");
                var names = Enum.GetNames(typeof(UISounds.Sound));
                for (int i = 0; i < names.Length && i < maxPerKind; i++) sb.Append(" " + names[i]);
                sb.AppendLine();

                var table = GetSoundTable();
                if (table != null)
                {
                    sb.Append("音效短名（sound / ui_sound / voice，共 " + table.Count + "）：");
                    int n = 0;
                    foreach (var k in table.Keys) { if (n++ >= maxPerKind) { sb.Append(" ..."); break; } sb.Append(" " + k); }
                    sb.AppendLine();
                }

                Debug.Log(sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Audio] DumpAvailableAudio 异常: " + e);
            }
        }

        private static IDictionary GetSoundTable()
        {
            try
            {
                var f = typeof(GlobalAssets).GetField("SoundTable",
                    BindingFlags.NonPublic | BindingFlags.Static);
                return f != null ? f.GetValue(null) as IDictionary : null;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("media:GetSoundTable", "[Media] GetSoundTable 失败 → 已按兜底处理", e); return null; }
        }
    }
}
