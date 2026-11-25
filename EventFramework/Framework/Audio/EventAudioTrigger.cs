using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventAudioTrigger
    {
        // 信号触发上下文的事件键
        public const string CTX_SIGNAL = "audio_signal";
        // 旗标触发上下文的事件键
        public const string CTX_FLAG = "audio_switch";

        private static readonly Dictionary<string, List<string>> _bySignal =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<string>> _byFlag =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, EventMediaPlayer.Active> _flagLive =
            new Dictionary<string, EventMediaPlayer.Active>(StringComparer.Ordinal);
        private static readonly Dictionary<string, System.Action<object>> _signalCbs =
            new Dictionary<string, System.Action<object>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _signalHashes =
            new Dictionary<string, int>(StringComparer.Ordinal);

        // 绑定表是否已建
        private static bool _built;
        // 当前绑定到的 Game 实例（换档后需对新实例重建）
        private static Game _subscribedGame;
        // 是否已挂上旗标变更回调
        private static bool _flagHooked;

        public static int SignalBindingCount { get { return _bySignal.Count; } }
        public static int FlagBindingCount { get { return _byFlag.Count; } }

        public static void EnsureBuilt()
        {
            var game = Game.Instance;

            // 已绑定到【当前这个 Game】→ 幂等返回
            if (_built && ReferenceEquals(_subscribedGame, game)) return;

            // 换档（读档 / 退主菜单 / 开新档）：旧 Game 的订阅表已随旧实例消失 → 先清理再对新实例重建
            if (_built)
            {
                Debug.Log("[MoreFood/Event][AudioTrig] 检测到 Game 实例更换 → 重建音频触发绑定");
                Shutdown();
            }

            _subscribedGame = game;
            _built = true;

            HookFlags();
            BuildBindings();
        }

        public static void Shutdown()
        {
            foreach (var kv in _signalCbs)
            {
                try
                {
                    int hash;
                    if (_signalHashes.TryGetValue(kv.Key, out hash))
                    {
                        var game = _subscribedGame != null ? _subscribedGame : Game.Instance;   // 用绑定时的实例退订
                        if (game != null) game.Unsubscribe(hash, kv.Value);
                    }
                    else
                    {
                        EventSignal.Unsubscribe(kv.Key, kv.Value);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][AudioTrig] 退订信号 '" + kv.Key + "' 异常: " + e);
                }
            }

            foreach (var kv in _flagLive)
                EventMediaPlayer.StopStandalone(kv.Value, "音频触发层重置");

            _signalCbs.Clear();
            _signalHashes.Clear();
            _flagLive.Clear();
            _bySignal.Clear();
            _byFlag.Clear();

            // 退订开关变化：否则换档后会重复订阅（旧回调仍挂在静态事件上）
            if (_flagHooked)
            {
                AudioSwitch.OnChanged -= OnFlagChanged;
                _flagHooked = false;
            }

            _built = false;
            _subscribedGame = null;
        }

        public static void RefreshFlags()
        {
            foreach (var kv in _byFlag) SyncFlag(kv.Key);
        }

        private static void HookFlags()
        {
            if (_flagHooked) return;
            _flagHooked = true;
            AudioSwitch.OnChanged += OnFlagChanged;
        }

        private static void BuildBindings()
        {
            int signalBindings = 0;
            int flagBindings = 0;

            foreach (var t in EventAudioFactory.All)
            {
                if (t == null) continue;

                if (t.fire_on != null)
                {
                    for (int i = 0; i < t.fire_on.Count; i++)
                    {
                        string sig = t.fire_on[i];
                        if (string.IsNullOrEmpty(sig)) continue;

                        sig = sig.Trim();
                        AddBinding(_bySignal, sig, t.key);
                        SubscribeSignal(sig);
                        signalBindings++;
                    }
                }

                if (!string.IsNullOrEmpty(t.flag))
                {
                    string flag = t.flag.Trim();
                    AddBinding(_byFlag, flag, t.key);
                    AudioSwitch.Declare(flag);
                    flagBindings++;
                }
            }

            if (signalBindings > 0 || flagBindings > 0)
                Debug.Log("[MoreFood/Event][AudioTrig] 音频触发绑定：信号 " + signalBindings
                          + " 条（去重后 " + _bySignal.Count + " 个信号），开关 " + flagBindings
                          + " 条（去重后 " + _byFlag.Count + " 个开关）");
        }

        private static void AddBinding(Dictionary<string, List<string>> map, string name, string key)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key)) return;

            List<string> list;
            if (!map.TryGetValue(name, out list))
            {
                list = new List<string>();
                map[name] = list;
            }
            if (!list.Contains(key)) list.Add(key);
        }

        private static void SubscribeSignal(string sig)
        {
            if (_signalCbs.ContainsKey(sig)) return;

            string self = sig;
            System.Action<object> cb = delegate(object data) { FireSignal(self); };

            try
            {
                if (EventSignal.IsCustomName(sig))
                {
                    EventSignal.Subscribe(sig, cb);
                }
                else
                {
                    GameHashes hash;
                    if (!Enum.TryParse<GameHashes>(sig, true, out hash))
                    {
                        Debug.LogWarning("[MoreFood/Event][AudioTrig] fire_on '" + sig
                                       + "' 既不是自定义信号（要带 '.' 或 ':'），也不是 GameHashes 里的名字 → 不订阅");
                        return;
                    }

                    var game = Game.Instance;
                    if (game == null)
                    {
                        Debug.LogWarning("[MoreFood/Event][AudioTrig] Game.Instance 为 null，fire_on '"
                                         + sig + "' 未订阅");
                        return;
                    }

                    game.Subscribe((int)hash, cb);
                    _signalHashes[sig] = (int)hash;
                }

                _signalCbs[sig] = cb;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AudioTrig] 订阅 '" + sig + "' 异常: " + e);
            }
        }

        private static void FireSignal(string sig)
        {
            List<string> keys;
            if (!_bySignal.TryGetValue(sig, out keys) || keys == null) return;

            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (!EventAudioFactory.IsKnownKey(key))
                {
                    EventWarnOnce.Warn("trig:" + key, "[MoreFood/Event][AudioTrig] 信号 '" + sig
                                       + "' 绑定的音效 '" + key + "' 已不存在 → 跳过");
                    continue;
                }

                var a = EventMediaPlayer.PlayStandalone(key, CTX_SIGNAL + ":" + sig, "信号 " + sig);
                if (a != null && !a.stoppable) EventMediaPlayer.DetachStandalone(a);
            }
        }

        private static void OnFlagChanged(string flag, bool on)
        {
            SyncFlag(flag);
        }

        private static void SyncFlag(string flag)
        {
            List<string> keys;
            if (!_byFlag.TryGetValue(flag, out keys) || keys == null) return;

            bool on = AudioSwitch.IsOn(flag);

            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];

                EventMediaPlayer.Active a;
                _flagLive.TryGetValue(key, out a);
                bool live = a != null && !a.stopped;

                if (on)
                {
                    if (live) continue;
                    if (!EventAudioFactory.IsKnownKey(key))
                    {
                        EventWarnOnce.Warn("trigflag:" + key, "[MoreFood/Event][AudioTrig] 开关 '" + flag
                                           + "' 绑定的音效 '" + key + "' 已不存在 → 跳过");
                        continue;
                    }

                    var played = EventMediaPlayer.PlayStandalone(key, CTX_FLAG + ":" + key,
                                                                 "开关 " + flag + " 打开");
                    if (played != null) _flagLive[key] = played;
                }
                else
                {
                    if (!live) { _flagLive.Remove(key); continue; }
                    EventMediaPlayer.StopStandalone(a, "开关 " + flag + " 关闭");
                    _flagLive.Remove(key);
                }
            }
        }
    }
}
