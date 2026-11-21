// EventHandlerRegistry.cs — 事件框架 · 【代码端处理器注册表】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    // 单个事件的代码端行为集合（除 install/uninstall 外都可空）
    public class EventHandler
    {
        // 事件被触发时（框架统一入口调用）
        public System.Action<EventInstance> onTrigger;
        // 进入 Active 时
        public System.Action<EventInstance> onStart;
        // 事件结束时
        public System.Action<EventInstance> onEnd;

        // 选项 action 键 → 回调
        public Dictionary<string, System.Action<EventInstance>> options;

        // Autonomous 事件：挂监听
        public System.Action install;
        // Autonomous 事件：摘监听
        public System.Action uninstall;
    }

    public static class EventHandlerRegistry
    {
        private static readonly Dictionary<string, EventHandler> _map =
            new Dictionary<string, EventHandler>();

        public static int Count { get { return _map.Count; } }

        // 注册 / 查询
        public static void Register(string key, EventHandler handler)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[MoreFood/Event] EventHandlerRegistry.Register：key 为空，忽略");
                return;
            }
            if (handler == null)
            {
                Debug.LogWarning("[MoreFood/Event] EventHandlerRegistry.Register('" + key + "')：handler 为 null，忽略");
                return;
            }

            if (_map.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event] 处理器键 '" + key
                               + "' 已被注册过，本次注册被拒（先到者胜）。"
                               + "要接替请先 EventHandlerRegistry 里清掉同名键，或改用带命名空间的键");
                return;
            }

            _map[key] = handler;
        }

        // 按 key 取回事件处理器；未注册返回 null
        public static EventHandler Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EventHandler h;
            return _map.TryGetValue(key, out h) ? h : null;
        }

        // 该处理器键是否已注册
        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && _map.ContainsKey(key);
        }

        public static IEnumerable<string> AllKeys { get { return _map.Keys; } }

        public static void Clear() { _map.Clear(); }

        // 安全调用（统一切口，所有异常都在这里被吃掉并记录）
        public static void SafeCallTrigger(EventHandler h, EventInstance inst, string eventId)
        {
            if (h == null) return;
            try { if (h.onTrigger != null) h.onTrigger(inst); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] '" + eventId + "' onTrigger 异常: " + e); }
        }

        // 安全调用处理器的 onStart（异常只打日志，不中断事件流程）
        public static void SafeCallStart(EventHandler h, EventInstance inst, string eventId)
        {
            if (h == null) return;
            try { if (h.onStart != null) h.onStart(inst); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] '" + eventId + "' onStart 异常: " + e); }
        }

        // 安全调用处理器的 onEnd（异常只打日志）
        public static void SafeCallEnd(EventHandler h, EventInstance inst, string eventId)
        {
            if (h == null) return;
            try { if (h.onEnd != null) h.onEnd(inst); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] '" + eventId + "' onEnd 异常: " + e); }
        }

        // 调用某个选项的 action 回调。返回是否命中。
        public static bool SafeCallOption(EventHandler h, string actionKey, EventInstance inst, string eventId)
        {
            if (h == null || h.options == null || string.IsNullOrEmpty(actionKey)) return false;

            System.Action<EventInstance> cb;
            if (!h.options.TryGetValue(actionKey, out cb) || cb == null)
            {
                Debug.LogWarning("[MoreFood/Event] '" + eventId + "' 找不到选项回调: " + actionKey);
                return false;
            }
            try { cb(inst); return true; }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] '" + eventId + "' 选项 '" + actionKey + "' 异常: " + e);
                return false;
            }
        }

        // 安全调用处理器的 install（异常只打日志）
        public static void SafeCallInstall(EventHandler h, string eventId)
        {
            if (h == null) return;
            try { if (h.install != null) h.install(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] '" + eventId + "' install 异常: " + e); }
        }

        // 安全调用处理器的 uninstall（异常只打日志）
        public static void SafeCallUninstall(EventHandler h, string eventId)
        {
            if (h == null) return;
            try { if (h.uninstall != null) h.uninstall(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] '" + eventId + "' uninstall 异常: " + e); }
        }
    }
}
