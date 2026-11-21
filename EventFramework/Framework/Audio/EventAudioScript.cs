// EventAudioScript.cs — 事件框架 · 【音频脚本：yaml 填函数键，代码端做播放决策】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public enum EventPlayMode
    {
        once = 0,   // 一次性：播完即止
        loop = 1,   // 循环：持续到事件结束 / 到时长 / 到条件
    }

    // 脚本拿到的播放句柄（用于自己停）
    public sealed class EventAudioHandle
    {
        internal EventMediaPlayer.Active active;   // 播放器内部条目（internal，不对外暴露）

        // 解析后的模板 key（重定向已跟到底）
        public string Key { get; internal set; }
        // 调用方传入的原始键（yaml / 脚本里写的那一串，原样保留；解析结果见 Key）
        public string SourceKey { get; internal set; }
        public string SoundName { get; internal set; }
        public string Slot { get; internal set; }

        public bool IsPlaying
        {
            get { return EventMediaPlayer.HandleIsPlaying(active); }
        }

        public void Stop()
        {
            EventMediaPlayer.HandleStop(active, "脚本 Stop()");
        }
    }

    // 脚本上下文：播放 / 停止 / 查询（面尽量小，剩下的走 EventMediaPlayer 公开件）
    public sealed class EventAudioContext
    {
        public EventDef Def { get; internal set; }
        public EventInstance Inst { get; internal set; }
        public string Slot { get; internal set; }

        // 按模板 key 播（一次 / 循环由模板的 play_mode 决定；模板自带的 delay 照常生效）
        public void Play(string key)
        {
            EventMediaPlayer.ScriptPlay(Def, Inst, Slot, key, null, null);
        }

        // 延后 delaySeconds（游戏秒；走闹钟，非轮询）再播（覆盖模板自带的 delay）
        public void Play(string key, float delaySeconds)
        {
            EventMediaPlayer.ScriptPlay(Def, Inst, Slot, key, delaySeconds, null);
        }

        // 强制按循环播（不管模板写的什么），拿到句柄自己控。
        public EventAudioHandle PlayLoop(string key)
        {
            return EventMediaPlayer.ScriptPlay(Def, Inst, Slot, key, null, true);
        }

        // 停掉这个事件当前所有的音频（含延时中未起播的）
        public void StopAll()
        {
            EventMediaPlayer.ScriptStopAll(Inst);
        }

        // 某个模板 key 现在还在播吗
        public bool IsPlaying(string key)
        {
            return EventMediaPlayer.ScriptIsPlaying(Inst, key);
        }

        // 手动走一次重定向：拿到它这次挑中的模板 key（传的不是重定向 / 挑不到 → null）
        public string Pick(string redirectKey)
        {
            if (string.IsNullOrEmpty(redirectKey)) return null;

            if (EventAudioFactory.GetRedirect(redirectKey) == null)
            {
                Debug.LogWarning("[MoreFood/Event][AudioScript] Pick('" + redirectKey
                                 + "') 不是重定向键（用 Get/Has 查静态模板）→ 返回 null");
                return null;
            }

            var t = EventAudioFactory.Resolve(redirectKey, Inst);
            return t != null ? t.key : null;
        }
    }

    public static class EventAudioScriptRegistry
    {
        private static readonly Dictionary<string, System.Action<EventAudioContext>> _map =
            new Dictionary<string, System.Action<EventAudioContext>>(StringComparer.Ordinal);

        public static int Count { get { return _map.Count; } }
        public static IEnumerable<string> AllKeys { get { return _map.Keys; } }

        // 注册 yaml 里写的「音频脚本」函数键 → 播放决策函数
        public static void Register(string key, System.Action<EventAudioContext> fn)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[MoreFood/Event][AudioScript] Register：key 为空，忽略");
                return;
            }
            if (fn == null)
            {
                Debug.LogWarning("[MoreFood/Event][AudioScript] Register('" + key + "')：函数为 null，忽略");
                return;
            }
            if (_map.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event][AudioScript] 音频脚本键 '" + key
                               + "' 已被注册过，本次注册被拒（先到者胜）");
                return;
            }

            _map[key] = fn;
            EventWarnOnce.Reset("audioscript:" + key);
        }

        // 按 key 取回音频脚本函数；未注册返回 null
        public static System.Action<EventAudioContext> Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            System.Action<EventAudioContext> fn;
            return _map.TryGetValue(key, out fn) ? fn : null;
        }

        // 该音频脚本 key 是否已注册
        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && _map.ContainsKey(key);
        }

        // 注销音频脚本 key（并重置未注册告警，便于换实现后重新提醒）
        public static void Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _map.Remove(key);
            EventWarnOnce.Reset("audioscript:" + key);   // 换实现再注册时，未注册告警要能重新提醒
        }

        public static void Clear()
        {
            _map.Clear();
            EventWarnOnce.ResetPrefix("audioscript:");
        }

        // 调一个脚本。空键 = 什么都不做；未注册 / 异常 = 记日志，不影响事件本体。
        public static bool Invoke(string key, EventDef def, EventInstance inst, string slot)
        {
            if (string.IsNullOrEmpty(key)) return true;

            var fn = Get(key);
            if (fn == null)
            {
                EventWarnOnce.Warn("audioscript:" + key,
                    "[MoreFood/Event][AudioScript] 脚本键 '" + key + "' 未注册（事件 '"
                    + (def != null ? def.id : "?") + "' 的 " + slot + "）→ 本次不执行");
                return false;
            }

            try
            {
                fn(new EventAudioContext { Def = def, Inst = inst, Slot = slot });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AudioScript] 脚本 '" + key + "'（事件 '"
                               + (def != null ? def.id : "?") + " " + slot + "'）抛异常（已忽略）: " + e);
                return false;
            }
        }
    }
}
