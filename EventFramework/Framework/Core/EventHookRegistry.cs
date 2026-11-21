// EventHookRegistry.cs — 事件框架 · 【行为函数键注册表】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventHookRegistry
    {
        private static readonly Dictionary<string, System.Action<EventInstance>> _map =
            new Dictionary<string, System.Action<EventInstance>>(StringComparer.Ordinal);

        public static int Count { get { return _map.Count; } }
        public static IEnumerable<string> AllKeys { get { return _map.Keys; } }

        // 注册 yaml 里写的「行为函数键」→ 行为实现
        public static void Register(string key, System.Action<EventInstance> fn)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[MoreFood/Event][Hook] Register：key 为空，忽略");
                return;
            }
            if (fn == null)
            {
                Debug.LogWarning("[MoreFood/Event][Hook] Register('" + key + "')：函数为 null，忽略");
                return;
            }
            if (_map.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event][Hook] 行为函数键 '" + key
                               + "' 已被注册过，本次注册被拒（先到者胜）");
                return;
            }

            _map[key] = fn;
            EventWarnOnce.Reset("hook:" + key);
        }

        // 按 key 取回行为函数；未注册返回 null
        public static System.Action<EventInstance> Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            System.Action<EventInstance> fn;
            return _map.TryGetValue(key, out fn) ? fn : null;
        }

        // 该行为函数键是否已注册
        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && _map.ContainsKey(key);
        }

        // 注销行为函数键（并重置未注册告警，便于换实现后重新提醒）
        public static void Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _map.Remove(key);
            EventWarnOnce.Reset("hook:" + key);
        }

        public static void Clear()
        {
            _map.Clear();
            EventWarnOnce.ResetPrefix("hook:");
        }

        // 调一个行为函数；空键 = 什么都不做；未注册 / 异常 = 记日志，不影响事件本体
        public static bool Invoke(string key, EventInstance inst, string eventId, string where)
        {
            if (string.IsNullOrEmpty(key)) return true;

            var fn = Get(key);
            if (fn == null)
            {
                EventWarnOnce.Warn("hook:" + key, "[MoreFood/Event][Hook] 行为函数键 '" + key
                                   + "' 未注册（事件 '" + (eventId ?? "?") + "' 的 " + where + "）→ 本次不执行");
                return false;
            }

            try
            {
                fn(inst);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Hook] 行为函数 '" + key + "'（事件 '"
                               + (eventId ?? "?") + " " + where + "'）抛异常（已忽略）: " + e);
                return false;
            }
        }

        // 自检：检查事件用到的行为键是否都已注册，返回缺失条数
        public static int Validate(IEnumerable<EventDef> defs)
        {
            if (defs == null) return 0;
            int missing = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var def in defs)
            {
                if (def == null) continue;
                missing += CheckOne(def.start_fn, def.id, "start_fn", seen);
                missing += CheckOne(def.end_fn, def.id, "end_fn", seen);
                if (def.options == null) continue;
                for (int i = 0; i < def.options.Count; i++)
                {
                    var o = def.options[i];
                    if (o == null) continue;
                    missing += CheckOne(o.start_fn, def.id, "options[" + i + "].start_fn", seen);
                    missing += CheckOne(o.end_fn, def.id, "options[" + i + "].end_fn", seen);
                }
            }
            return missing;
        }

        private static int CheckOne(string key, string eventId, string where, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            if (Has(key)) return 0;
            if (seen.Add(key))
                Debug.LogWarning("[MoreFood/Event][Hook] 事件 '" + eventId + "' 的 " + where + " = '" + key
                               + "' 未注册（该钩子不会执行）");
            return 1;
        }
    }
}
