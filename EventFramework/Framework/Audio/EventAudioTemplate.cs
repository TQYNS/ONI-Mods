// EventAudioTemplate.cs — 事件框架 · 【音频模板 + 模板工厂】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public enum EventMediaKind
    {
        sound,       // 一次性音效（3D，随距离衰减）
        ui_sound,    // 一次性 UI 音（2D，不衰减）
        loop_sound,  // 循环音（事件期间持续，结束时停）
        music,       // 原版曲目（MusicManager，事件期间 BGM）
        snapshot,    // 混音快照（整体音景切换，如 EventPopupSnapshot）
        voice,       // 语音（走一次性音效通道，独立标记便于统一管理）
        ui_enum,     // 原版 UI 枚举音（UISounds.Sound.xxx）
        file,        // 自定义音频文件（Mod 的 sounds/ 下的 wav / ogg / mp3，走 FMOD core 解码）
    }

    public sealed class EventAudioTemplate
    {
        public string key { get; set; }                        // 模板 key（唯一，yaml 里引用的就是它）
        // 音源类别（决定句柄里装什么）
        public EventMediaKind kind { get; set; } = EventMediaKind.sound;

        // 音源名：原版音效名（SoundTable 短名）/ "event:/..." 完整路径 / 曲名 / 快照字段名 / UISounds 枚举名
        public string name { get; set; } = null;

        // 音量（1 = 原始）
        public float volume { get; set; } = 1f;

        // ---- 播放分类（once = 一次性；loop = 循环，停止条件见下）----
        public EventPlayMode play_mode { get; set; } = EventPlayMode.once;
        public bool loop { get; set; } = false;                // 兼容旧写法：true ⇔ play_mode = loop
        public bool stop_on_end { get; set; } = true;          // 停止条件①：效果结束时停（默认）
        public string stop_on_signal { get; set; } = null;     // 停止条件③：订阅推送（见下），触发即停
        //   · 带 '.' 或 ':' 的名字 → 作者自定义信号（EventSignal.Raise）
        public bool pause_with_game { get; set; } = false;     // 循环音是否跟游戏一起暂停（默认不暂停）
        public string stop_mode { get; set; } = "allowfadeout"; // allowfadeout | immediate

        // ---- 时间控制（粒度 = 框架 Tick 的 1 游戏秒）----
        public float delay { get; set; } = 0f;                 // 相对槽位时刻，延后 N 游戏秒起播
        public float play_seconds { get; set; } = 0f;          // 起播后播 N 游戏秒自动停（<=0 = 不停）
        public int start_ms { get; set; } = 0;                 // 从音频时间轴第 N 毫秒开始（需句柄）
        public int end_ms { get; set; } = 0;                   // 播到第 N 毫秒停（需句柄）

        public string param { get; set; } = null;              // FMOD 参数名（可选）
        // FMOD 参数值
        public float param_value { get; set; } = 0f;
        public string labeled_param_value { get; set; } = null; // 标签型参数值（写了它就用 WithLabeledParameter）

        public string position { get; set; } = "listener";     // listener | zero（sound / voice 的位置）

        public bool spatial { get; set; } = false;             // file 类型：true = 3D（起播时取一次位置，不跟随）
        public bool dedup { get; set; } = true;                // 同源音效已在响 ⇒ 本次不播（防叠加）
        public List<string> mute_when { get; set; } = null;    // 互斥：列出的音效正在响 ⇒ 本次静默不播（单向）
        // 3D 衰减最小距离
        public float min_distance { get; set; } = 1f;
        // 3D 衰减最大距离
        public float max_distance { get; set; } = 60f;

        // ---- 音效库 yaml 生成的那些（AudioLibraryEntry → 这里）----
        public List<AudioVariant> variants { get; set; } = null;   // 音源变体：每次播放加权抽一个
        public float volume_jitter { get; set; } = 0f;             // 音量 ±抖动
        public float pitch { get; set; } = 1f;                     // 音高（只有 file 类型能生效）
        // 音高 ±抖动
        public float pitch_jitter { get; set; } = 0f;
        public string play_as { get; set; } = null;                // sfx | bgm | voice | ambient（轨道）
        public float cooldown { get; set; } = 0f;                  // 同一 key 两次播放的最小间隔（秒）
        public int max_concurrent { get; set; } = 0;               // 同一 key 同时最多几条（0 = 不限）
        public List<string> fire_on { get; set; } = null;          // 收到这些信号就播（EventSignal）
        public string flag { get; set; } = null;                   // 绑定的音频开关（AudioSwitch；运行态不存盘）

        public EventAudioTemplate() { }

        public EventAudioTemplate(string key, EventMediaKind kind, string name)
        {
            this.key = key;
            this.kind = kind;
            this.name = name;
        }

        // 按 yaml 原值临时造一个模板（未注册时的降级路径）
        public static EventAudioTemplate Raw(string name, EventMediaKind kind)
        {
            return new EventAudioTemplate(name, kind, name);
        }

        // 播放时长（秒）：play_seconds 与 end_ms-start_ms 取较短的有效值；0 = 不限
        public float GetPlaySeconds()
        {
            float byRange = (end_ms > start_ms) ? (end_ms - start_ms) / 1000f : 0f;
            if (play_seconds > 0f && byRange > 0f) return Mathf.Min(play_seconds, byRange);
            if (play_seconds > 0f) return play_seconds;
            return byRange;
        }

        // 是否循环播放（play_mode=loop，或旧的 loop: true，或 kind=loop_sound）
        public bool IsLoop
        {
            get
            {
                return play_mode == EventPlayMode.loop
                       || loop
                       || kind == EventMediaKind.loop_sound;
            }
        }

        // 一次性音效里需要【自持句柄】的（限时停 / 片段区间）⇒ 走 BeginOneShot + start。
        public bool NeedsSelfHandle
        {
            get { return !IsLoop && (start_ms > 0 || end_ms > 0 || play_seconds > 0f); }
        }

        // 加权抽一个变体；没有任何可用变体时返回 null
        public AudioVariant PickVariant()
        {
            if (variants == null || variants.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                if (v != null && v.weight > 0f) total += v.weight;
            }
            if (total <= 0f) return null;

            float r = UnityEngine.Random.value * total;
            AudioVariant last = null;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                if (v == null || v.weight <= 0f) continue;
                last = v;
                r -= v.weight;
                if (r <= 0f) return v;
            }
            return last;
        }

        // 复制一份并套上变体（key / 播放参数原样继承，只改音源与类型）
        public EventAudioTemplate Derive(AudioVariant v)
        {
            var d = new EventAudioTemplate();

            d.key = key;
            d.kind = kind;
            d.name = name;
            d.volume = volume;
            d.volume_jitter = volume_jitter;
            d.pitch = pitch;
            d.pitch_jitter = pitch_jitter;
            d.play_mode = play_mode;
            d.loop = loop;
            d.stop_on_end = stop_on_end;
            d.stop_on_signal = stop_on_signal;
            d.pause_with_game = pause_with_game;
            d.stop_mode = stop_mode;
            d.delay = delay;
            d.play_seconds = play_seconds;
            d.start_ms = start_ms;
            d.end_ms = end_ms;
            d.param = param;
            d.param_value = param_value;
            d.labeled_param_value = labeled_param_value;
            d.position = position;
            d.spatial = spatial;
            d.dedup = dedup;
            d.mute_when = mute_when;
            d.min_distance = min_distance;
            d.max_distance = max_distance;
            d.variants = variants;
            d.play_as = play_as;
            d.cooldown = cooldown;
            d.max_concurrent = max_concurrent;
            d.fire_on = fire_on;
            d.flag = flag;

            if (v != null)
            {
                if (!string.IsNullOrEmpty(v.file))
                {
                    d.kind = EventMediaKind.file;
                    d.name = v.file;
                }
                else if (!string.IsNullOrEmpty(v.name))
                {
                    d.name = v.name;
                }
            }
            return d;
        }
    }

    public static class EventAudioFactory
    {
        private static readonly Dictionary<string, EventAudioTemplate> _map =
            new Dictionary<string, EventAudioTemplate>(StringComparer.Ordinal);

        // 重定向：key → 挑一个模板 key（返回 null / 空 = 这次不播）。用于"看情况选音效"
        private static readonly Dictionary<string, System.Func<EventInstance, string>> _redirects =
            new Dictionary<string, System.Func<EventInstance, string>>(StringComparer.Ordinal);

        // 内置音频是否已注册
        private static bool _builtinsDone;

        // 重定向最大深度（防环）
        public const int MAX_REDIRECT_DEPTH = 8;

        public static int Count { get { return _map.Count; } }
        public static int RedirectCount { get { return _redirects.Count; } }
        public static IEnumerable<EventAudioTemplate> All { get { return _map.Values; } }

        // 全部 key（静态模板 + 重定向）—— 命名空间自检按这个算
        public static IEnumerable<string> AllKeys
        {
            get
            {
                foreach (var k in _map.Keys) yield return k;
                foreach (var k in _redirects.Keys) yield return k;
            }
        }

        // 注册 / 查询
        public static void Register(EventAudioTemplate t)
        {
            if (t == null || string.IsNullOrEmpty(t.key))
            {
                Debug.LogWarning("[MoreFood/Event][Audio] Register：模板为空或 key 为空，忽略");
                return;
            }
            if (string.IsNullOrEmpty(t.name))
            {
                Debug.LogWarning("[MoreFood/Event][Audio] 模板 '" + t.key + "' 的 name 为空，忽略");
                return;
            }
            if (_map.ContainsKey(t.key))
            {
                Debug.LogWarning("[MoreFood/Event][Audio] 音频模板键 '" + t.key
                               + "' 已被注册过，本次注册被拒（先到者胜）。"
                               + "要接替请先 Unregister，或改用带命名空间的键");
                return;
            }

            _map[t.key] = t;
        }

        // 顺手注册（与 CreatureDropFactory 同款写法）
        public static EventAudioTemplate Add(string key, EventMediaKind kind, string name)
        {
            var t = new EventAudioTemplate(key, kind, name);
            Register(t);
            return t;
        }

        // 按 key 取回静态音频模板（响度 / 时长 / 片段），未注册返回 null
        public static EventAudioTemplate Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EventAudioTemplate t;
            return _map.TryGetValue(key, out t) ? t : null;
        }

        // 该静态模板 key 是否已注册
        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && _map.ContainsKey(key);
        }

        // 注册重定向：函数只负责【挑】模板 key，音量/时长/片段仍是静态模板的数据
        public static void RegisterRedirect(string key, System.Func<EventInstance, string> picker)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[MoreFood/Event][Audio] RegisterRedirect：key 为空，忽略");
                return;
            }
            if (picker == null)
            {
                Debug.LogWarning("[MoreFood/Event][Audio] RegisterRedirect('" + key + "')：函数为 null，忽略");
                return;
            }
            if (_map.ContainsKey(key) || _redirects.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event][Audio] 音频键 '" + key
                               + "' 已被注册过，本次重定向被拒（先到者胜）");
                return;
            }

            _redirects[key] = picker;
        }

        // 取该 key 的重定向函数（按事件上下文挑模板 key），未注册返回 null
        public static System.Func<EventInstance, string> GetRedirect(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            System.Func<EventInstance, string> fn;
            return _redirects.TryGetValue(key, out fn) ? fn : null;
        }

        // key 认不认识（静态模板或重定向）—— yaml 自检用这个，别用 Has
        public static bool IsKnownKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _map.ContainsKey(key) || _redirects.ContainsKey(key);
        }

        // 带上下文解析：key → 模板。重定向会逐层跟到底（最多 MAX_REDIRECT_DEPTH 层）。
        public static EventAudioTemplate Resolve(string key, EventInstance inst)
        {
            if (string.IsNullOrEmpty(key)) return null;

            HashSet<string> seen = null;

            for (int depth = 0; depth < MAX_REDIRECT_DEPTH; depth++)
            {
                var t = Get(key);
                if (t != null) return t;

                var fn = GetRedirect(key);
                if (fn == null) return null;                 // 根本不认识这个 key

                if (seen == null) seen = new HashSet<string>(StringComparer.Ordinal);
                if (!seen.Add(key))
                {
                    Debug.LogWarning("[MoreFood/Event][Audio] 音频重定向成环: " + key + "（已中断）");
                    return null;
                }

                string next = null;
                try { next = fn(inst); }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][Audio] 音频重定向 '" + key + "' 抛异常（这次不播）: " + e);
                    return null;
                }

                if (string.IsNullOrEmpty(next))
                {
                    Debug.Log("[MoreFood/Event][Audio] 音频重定向 '" + key + "' 选择本次不播");
                    return null;
                }
                key = next;
            }

            Debug.LogWarning("[MoreFood/Event][Audio] 音频重定向链超过 " + MAX_REDIRECT_DEPTH
                           + " 层（疑似成环），已中断");
            return null;
        }

        // 注销模板 key（静态模板与重定向一起清）
        public static void Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _map.Remove(key);
            _redirects.Remove(key);
        }

        public static void Clear()
        {
            _map.Clear();
            _redirects.Clear();
            _builtinsDone = false;
        }

        public static string Describe()
        {
            var names = Enum.GetNames(typeof(EventMediaKind));
            int[] byKind = new int[names.Length];          // 长度跟 EventMediaKind 同步
            foreach (var t in _map.Values)
            {
                if (t == null) continue;
                int k = (int)t.kind;
                if (k >= 0 && k < byKind.Length) byKind[k]++;
            }

            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
                if (byKind[i] > 0) parts.Add(names[i] + "=" + byKind[i]);

            return "共 " + _map.Count + " 个音频模板（" + string.Join(" ", parts.ToArray()) + "）"
                   + "，重定向 " + _redirects.Count + " 个";
        }

        // 内置模板（全部为原版实证可用件）—— 幂等，重复调用只跑一次
        public static void EnsureBuiltins()
        {
            if (_builtinsDone) return;

            try
            {
                Builtin("eventfw.ui.select",    EventMediaKind.ui_enum, "Select");
                Builtin("eventfw.ui.back",      EventMediaKind.ui_enum, "Back");
                Builtin("eventfw.ui.click",     EventMediaKind.ui_enum, "ClickHUD");
                Builtin("eventfw.ui.mouseover", EventMediaKind.ui_enum, "HUD_Mouseover");
                Builtin("eventfw.ui.positive",  EventMediaKind.ui_enum, "PositiveNotification");
                Builtin("eventfw.ui.negative",  EventMediaKind.ui_enum, "NegativeNotification");

                Builtin("eventfw.snapshot.event_popup", EventMediaKind.snapshot, "EventPopupSnapshot");
                Builtin("eventfw.snapshot.mute_music",  EventMediaKind.snapshot, "MuteDynamicMusicSnapshot");
                Builtin("eventfw.snapshot.esc_pause",   EventMediaKind.snapshot, "ESCPauseSnapshot");
                Builtin("eventfw.snapshot.victory",     EventMediaKind.snapshot, "VictoryMessageSnapshot");

                Builtin("eventfw.file.keqi", EventMediaKind.file, "sounds/KeQi.wav");

                var f2 = Builtin("eventfw.file.ogg", EventMediaKind.file, "sounds/test.ogg");
                if (f2 != null) f2.play_seconds = 10f;

                _builtinsDone = true;   // 全部成功才置位：中途异常下次还能重试
                Debug.Log("[MoreFood/Event][Audio] 内置音频模板已就绪：" + Describe());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Audio] 内置模板注册异常（下次启动会重试）: " + e);
            }
        }

        // 内置件：已存在就跳过（不参与"先到者胜"的冲突告警，重试也不会刷 ERROR）
        private static EventAudioTemplate Builtin(string key, EventMediaKind kind, string name)
        {
            if (_map.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event][Audio] 内置模板 '" + key
                                 + "' 已被注册过 → 跳过内置件，保留先注册的那个");
                return null;
            }
            return Add(key, kind, name);
        }
    }
}
