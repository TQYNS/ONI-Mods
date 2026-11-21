// EventHistory.cs — 事件框架 · 【事件历史层】（static）
//
// 两张表：
//   _triggered：事件 id → 触发次数（Dictionary；用于 require_event_counts）
//   _chosen   ："事件\u0001选项" → 是否选过（HashSet；只判有无，不记次数）
// ★ 惰性写入：只有被条件引用过的事件/选项才记录（_refEvents / _refChoices 由启动扫描给出）。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventHistory
    {
        private const char Sep = '\u0001';   // 不可见分隔符：避免事件 id / 选项 id 里的 '.' 歧义

        private static readonly Dictionary<string, int> _triggered =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> _chosen = new HashSet<string>(StringComparer.Ordinal);

        private static HashSet<string> _refEvents = null;    // 启动扫描得到
        // 参考用的选项键集合（懒构建）
        private static HashSet<string> _refChoices = null;
        // 警告只打一次
        private static bool _refWarned = false;

        // 去重后的事件数 / 选项组合数
        public static int TriggeredCount { get { return _triggered.Count; } }
        public static int ChosenCount { get { return _chosen.Count; } }

        // 名单（启动扫描后设置）
        public static void SetReferenced(HashSet<string> events, HashSet<string> choices)
        {
            _refEvents = events;
            _refChoices = choices;
        }

        // 拼「事件 + 选项」复合键（记录已选过的组合）
        public static string MakeChoiceKey(string e, string o)
        {
            return (e ?? "") + Sep + (o ?? "");
        }

        // 写（按需）
        public static void NotifyTriggered(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            if (_refEvents == null)
            {
                WarnNoList("NotifyTriggered");
                Bump(eventId);                    // 退化为全写
                return;
            }
            if (!_refEvents.Contains(eventId)) return;   // 没人查 ⇒ 不写

            Bump(eventId);
        }

        private static void Bump(string eventId)
        {
            int n;
            _triggered.TryGetValue(eventId, out n);
            _triggered[eventId] = n + 1;
        }

        // 记下这次选择（随默认走的也记；不区分主动/保底）
        public static void NotifyChosen(string eventId, string optionId)
        {
            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(optionId)) return;

            string key = MakeChoiceKey(eventId, optionId);

            if (_refChoices == null)
            {
                WarnNoList("NotifyChosen");
                _chosen.Add(key);                 // 退化为全写
                return;
            }
            if (!_refChoices.Contains(key)) return;      // 没人查 ⇒ 不写

            _chosen.Add(key);
        }

        // 读
        public static bool HasTriggered(string eventId)
        {
            return !string.IsNullOrEmpty(eventId) && _triggered.ContainsKey(eventId);
        }

        // 该事件累计触发了多少次（未记录 ⇒ 0）
        public static int GetTriggeredCount(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return 0;
            int n;
            return _triggered.TryGetValue(eventId, out n) ? n : 0;
        }

        // 该「事件 + 选项」组合是否已经被选过
        public static bool HasChosenKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _chosen.Contains(key);
        }

        // eventId：事件 id、optionId：标识 id
        public static bool HasChosen(string eventId, string optionId)
        {
            return HasChosenKey(MakeChoiceKey(eventId, optionId));
        }

        // 生命周期 / 调试
        public static void Clear()
        {
            _triggered.Clear();
            _chosen.Clear();
        }

        // 打印调试信息（诊断用）
        public static string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[MoreFood/Event][History] 事件历史（triggered=" + _triggered.Count
                          + " chosen=" + _chosen.Count + "；名单 evt="
                          + (_refEvents == null ? "未扫描" : _refEvents.Count.ToString())
                          + " opt=" + (_refChoices == null ? "未扫描" : _refChoices.Count.ToString()) + "）");

            var ts = new List<string>();
            foreach (var kv in _triggered) ts.Add(kv.Key + "×" + kv.Value);
            ts.Sort(StringComparer.Ordinal);
            sb.Append("  triggered: ");
            for (int i = 0; i < ts.Count; i++) sb.Append(ts[i]).Append(" ");
            sb.AppendLine();

            var cs = new List<string>();
            foreach (var c in _chosen) cs.Add(c);
            cs.Sort(StringComparer.Ordinal);
            sb.Append("  chosen: ");
            for (int i = 0; i < cs.Count; i++) sb.Append(cs[i].Replace(Sep, '+')).Append(" ");
            sb.AppendLine();

            return sb.ToString();
        }

        // 存档（扁平并行 List；读档【不按名单过滤】—— 存档里的键是事实）
        public static void ExportTo(IEventSaveData d)
        {
            if (d == null) return;
            try
            {
                d.histTriggeredKeys.Clear();
                d.histTriggeredCounts.Clear();
                d.histTriggered.Clear();
                foreach (var kv in _triggered)
                {
                    d.histTriggeredKeys.Add(kv.Key);
                    d.histTriggeredCounts.Add(kv.Value);
                    d.histTriggered.Add(kv.Key);   // 兼容字段：老版本读新档时至少知道"发生过"
                }

                d.histChosen.Clear();
                foreach (var c in _chosen) d.histChosen.Add(c);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][History] ExportTo 异常: " + e);
            }
        }

        // 从存档数据恢复历史（覆盖式：先清空再导入）
        public static void ImportFrom(IEventSaveData d)
        {
            if (d == null) return;
            _triggered.Clear();
            _chosen.Clear();

            try
            {
                bool usedNew = false;
                if (d.histTriggeredKeys != null && d.histTriggeredKeys.Count > 0)
                {
                    usedNew = true;
                    for (int i = 0; i < d.histTriggeredKeys.Count; i++)
                    {
                        string k = d.histTriggeredKeys[i];
                        if (string.IsNullOrEmpty(k)) continue;

                        int n = 1;
                        if (d.histTriggeredCounts != null && i < d.histTriggeredCounts.Count)
                            n = d.histTriggeredCounts[i];

                        _triggered[k] = n > 0 ? n : 1;
                    }
                }

                // 老档回落：只有键列表，没有次数 ⇒ 每项记 1 次
                if (!usedNew && d.histTriggered != null)
                {
                    for (int i = 0; i < d.histTriggered.Count; i++)
                        if (!string.IsNullOrEmpty(d.histTriggered[i])) _triggered[d.histTriggered[i]] = 1;
                }

                for (int i = 0; i < d.histChosen.Count; i++)
                    if (!string.IsNullOrEmpty(d.histChosen[i])) _chosen.Add(d.histChosen[i]);

                Debug.Log("[MoreFood/Event][History] 事件历史已恢复：triggered=" + _triggered.Count
                          + " chosen=" + _chosen.Count + "（次数格式=" + (usedNew ? "新" : "老档按1次") + "）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][History] ImportFrom 异常: " + e);
            }
        }

        private static void WarnNoList(string who)
        {
            if (_refWarned) return;
            _refWarned = true;
            Debug.LogWarning("[MoreFood/Event][History] " + who + " 时引用名单还没建立"
                           + "（启动扫描没跑？）→ 本次退化为【全写】以保数据完整");
        }
    }
}
