// EventConditionRegistry.cs — 事件框架 · 【前置条件函数键注册表】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventConditionRegistry
    {
        private class Entry
        {
            // 条件求值函数
            public System.Func<EventInstance, bool> fn;
            public string[] deps;      // null = 未声明（自检会报）；string[0] = 显式无依赖
        }

        private static readonly Dictionary<string, Entry> _map =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        private static readonly HashSet<string> _missingWarned = new HashSet<string>(StringComparer.Ordinal);

        public static int Count { get { return _map.Count; } }
        public static IEnumerable<string> AllKeys { get { return _map.Keys; } }

        // 注册 / 查询
        public static void Register(string key, System.Func<EventInstance, bool> fn, string[] dependencies = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[MoreFood/Event][Cond] Register：key 为空，忽略");
                return;
            }
            if (fn == null)
            {
                Debug.LogWarning("[MoreFood/Event][Cond] Register('" + key + "')：函数为 null，忽略");
                return;
            }
            if (_map.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event][Cond] 前置条件键 '" + key
                               + "' 已被注册过，本次注册被拒（先到者胜）。"
                               + "要接替请先 Unregister，或改用带命名空间的键");
                return;
            }

            _map[key] = new Entry { fn = fn, deps = dependencies };
            _missingWarned.Remove(key);
        }

        // 注销一个条件函数键
        public static void Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _map.Remove(key);
        }

        // 该条件函数键是否已注册
        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && _map.ContainsKey(key);
        }

        // 按 key 取回条件判定函数；未注册返回 null
        public static System.Func<EventInstance, bool> Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Entry e;
            return _map.TryGetValue(key, out e) ? e.fn : null;
        }

        // 取该条件函数依赖的其他条件函数键（自检用）；未注册返回 null
        public static string[] GetDependencies(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Entry e;
            return _map.TryGetValue(key, out e) ? e.deps : null;
        }

        // 把所有注册项声明的依赖汇总（供历史按需写入的名单）
        public static IEnumerable<string> AllDependencies()
        {
            var list = new List<string>();
            foreach (var kv in _map)
            {
                var d = kv.Value != null ? kv.Value.deps : null;
                if (d == null) continue;
                for (int i = 0; i < d.Length; i++)
                    if (!string.IsNullOrEmpty(d[i])) list.Add(d[i]);
            }
            return list;
        }

        public static void Clear()
        {
            _map.Clear();
            _missingWarned.Clear();
        }

        // 求值（唯一入口）
        public static bool Evaluate(string key, EventInstance inst, string eventId)
        {
            if (string.IsNullOrEmpty(key)) return true;

            var fn = Get(key);
            if (fn == null)
            {
                WarnMissingOnce(key, eventId);
                return false;
            }

            try { return fn(inst); }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Cond] 事件 '" + (eventId ?? "?") + "' 的前置条件 '"
                               + key + "' 抛异常（按不通过处理）: " + e);
                return false;
            }
        }

        // 判定事件的整体前置条件是否满足（def 无 condition.function 时视为通过）
        public static bool Evaluate(EventDef def, EventInstance inst)
        {
            if (def == null) return true;
            string key = def.condition != null ? def.condition.function : null;
            return Evaluate(key, inst, def.id);
        }

        private static void WarnMissingOnce(string key, string eventId)
        {
            if (!_missingWarned.Add(key)) return;
            Debug.LogWarning("[MoreFood/Event][Cond] 前置条件 '" + key + "' 未注册（事件 '"
                             + (eventId ?? "?") + "' 配了它）→ 该事件不会触发");
        }

        // 启动校验
        public static int Validate(IEnumerable<EventDef> defs)
        {
            if (defs == null) return 0;
            int missing = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var def in defs)
            {
                string key = def != null && def.condition != null ? def.condition.function : null;
                if (string.IsNullOrEmpty(key)) continue;
                if (Has(key)) continue;
                missing++;
                if (seen.Add(key))
                    Debug.LogWarning("[MoreFood/Event][Cond] 事件 '" + def.id + "' 配的 condition.function '"
                                     + key + "' 代码端未注册（该事件不会触发）");
            }
            return missing;
        }

 // ：每个注册项都必须声明 dependencies（null = 没声明）
        public static int ValidateDependencies()
        {
            int bad = 0;
            foreach (var kv in _map)
            {
                var e = kv.Value;
                if (e != null && e.deps != null) continue;    // string[0] 也算"已声明"

                bad++;
                Debug.LogWarning("[MoreFood/Event][Cond] 前置条件键 '" + kv.Key
                               + "' 未声明 dependencies（注册时第三参；无依赖请传 new string[0]）"
                               + " → 历史按需写入无法覆盖它引用的状态键");
            }
            return bad;
        }
    }
}
