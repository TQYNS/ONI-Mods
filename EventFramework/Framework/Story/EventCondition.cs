// EventCondition.cs — 事件框架 · 【条件预编译与求值】
//
// 统一条件模型（对应 EventConditionBlock）：
//   A. 历史：require_events / require_any_events / forbid_events / require_choices / forbid_choices / require_event_counts
//   C. 世界：require_buildings / require_research（原版增量字典，O(1)）
//   D. 自定义函数：不在本类，由 EventConditionRegistry 求值（见 EventSystem）

using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    // 选项引用（事件 + 选项）
    public sealed class ChoiceRef
    {
        // 目标事件 id
        public string @event { get; set; } = null;
        // 选项 id
        public string option { get; set; } = null;
    }

    // 预编译结果（运行时只读；挂在 EventDef.compiled / EventOption.compiled 上）
    public sealed class EventCondition
    {
        // ---- A. 历史 ----
        public string[] RequireEvents = EMPTY;
        public string[] RequireAnyEvents = EMPTY;
        public string[] ForbidEvents = EMPTY;
        public string[] RequireChoiceKeys = EMPTY;   // 预拼 "事件\u0001选项"
        public string[] ForbidChoiceKeys = EMPTY;
        public EventCountCond[] RequireEventCounts = EMPTY_CC;

        // ---- C. 世界 ----
        public BuildingCond[] RequireBuildings = EMPTY_BC;
        public string[] RequireResearch = EMPTY;

        // 没有任何条件 ⇒ 求值直接通过（短路，零开销）
        public bool IsEmpty = true;

        private static readonly string[] EMPTY = new string[0];
        private static readonly EventCountCond[] EMPTY_CC = new EventCountCond[0];
        private static readonly BuildingCond[] EMPTY_BC = new BuildingCond[0];

        // 求值
        public bool Check()
        {
            string ignored;
            return Check(out ignored);
        }

        // 求值并给出【不通过的原因】（给抽取追踪 / 排错用）；通过时 reason = null
        public bool Check(out string reason)
        {
            reason = null;
            if (IsEmpty) return true;

            // ══ A. 历史 ══
            for (int i = 0; i < RequireEvents.Length; i++)
            {
                if (EventHistory.HasTriggered(RequireEvents[i])) continue;
                reason = "require_events '" + RequireEvents[i] + "' 没触发过";
                return false;
            }

            if (RequireAnyEvents.Length > 0)
            {
                bool any = false;
                for (int i = 0; i < RequireAnyEvents.Length; i++)
                    if (EventHistory.HasTriggered(RequireAnyEvents[i])) { any = true; break; }

                if (!any)
                {
                    reason = "require_any_events 一个都没满足（" + string.Join(" / ", RequireAnyEvents) + "）";
                    return false;
                }
            }

            for (int i = 0; i < ForbidEvents.Length; i++)
            {
                if (!EventHistory.HasTriggered(ForbidEvents[i])) continue;
                reason = "forbid_events '" + ForbidEvents[i] + "' 已触发过";
                return false;
            }

            for (int i = 0; i < RequireChoiceKeys.Length; i++)
            {
                if (EventHistory.HasChosenKey(RequireChoiceKeys[i])) continue;
                reason = "require_choices '" + ShowChoiceKey(RequireChoiceKeys[i]) + "' 没选过";
                return false;
            }

            for (int i = 0; i < ForbidChoiceKeys.Length; i++)
            {
                if (!EventHistory.HasChosenKey(ForbidChoiceKeys[i])) continue;
                reason = "forbid_choices '" + ShowChoiceKey(ForbidChoiceKeys[i]) + "' 已选过";
                return false;
            }

            // 次数阈值（单事件 key 与多事件 events 合计相加）
            for (int i = 0; i < RequireEventCounts.Length; i++)
            {
                var c = RequireEventCounts[i];
                if (c == null) continue;

                int total = 0;
                if (!string.IsNullOrEmpty(c.key)) total += EventHistory.GetTriggeredCount(c.key);
                if (c.events != null)
                {
                    for (int k = 0; k < c.events.Count; k++)
                        if (!string.IsNullOrEmpty(c.events[k])) total += EventHistory.GetTriggeredCount(c.events[k]);
                }

                if (!CompareInt(total, c.op, c.value))
                {
                    reason = "require_event_counts 合计 " + total + "，不满足 "
                             + (string.IsNullOrEmpty(c.op) ? "==" : c.op) + " " + c.value;
                    return false;
                }
            }

            // ══ C. 世界：建筑 ══
            if (RequireBuildings.Length > 0)
            {
                var inv = BuildingInventory.Instance;
                if (inv == null)
                {
                    reason = "建筑索引不可用（BuildingInventory.Instance 为 null）";
                    return false;
                }

                for (int i = 0; i < RequireBuildings.Length; i++)
                {
                    var b = RequireBuildings[i];
                    if (b == null || string.IsNullOrEmpty(b.id)) continue;

                    int need = b.min > 0 ? b.min : 1;
                    int have = inv.BuildingCount(new Tag(b.id));
                    if (have >= need) continue;

                    reason = "require_buildings '" + b.id + "' 现有 " + have + " < " + need;
                    return false;
                }
            }

            // ══ C. 世界：科技 ══
            if (RequireResearch.Length > 0)
            {
                var r = Research.Instance;
                if (r == null)
                {
                    reason = "研究系统不可用（Research.Instance 为 null）";
                    return false;
                }

                for (int i = 0; i < RequireResearch.Length; i++)
                {
                    string techId = RequireResearch[i];
                    if (string.IsNullOrEmpty(techId)) continue;

                    var ti = r.GetTechInstance(techId);
                    if (ti != null && ti.IsComplete()) continue;

                    reason = "require_research '" + techId + "' 未完成";
                    return false;
                }
            }

            return true;
        }

        private static string ShowChoiceKey(string key)
        {
            return string.IsNullOrEmpty(key) ? "?" : key.Replace('\u0001', '/');
        }

        // 整数比较（op 为空 / 非法 ⇒ 视为不通过并告警一次，不抛异常）
        public static bool CompareInt(int v, string op, int target)
        {
            if (string.IsNullOrEmpty(op)) return v == target;

            switch (op)
            {
                case ">=": return v >= target;
                case "<=": return v <= target;
                case ">":  return v > target;
                case "<":  return v < target;
                case "==": return v == target;
                case "!=": return v != target;
                default:
                    WarnOp(op);
                    return false;
            }
        }

        // ---- 预编译（启动时一次）----
        public static EventCondition Compile(EventDef def)
        {
            if (def == null) return new EventCondition();
            return Build(def.condition);
        }

        public static EventCondition Compile(EventOption opt)
        {
            if (opt == null) return new EventCondition();
            return Build(opt.condition);
        }

        private static EventCondition Build(EventConditionBlock blk)
        {
            var c = new EventCondition();
            if (blk == null) return c;

            c.RequireEvents = Arr(blk.require_events);
            c.RequireAnyEvents = Arr(blk.require_any_events);
            c.ForbidEvents = Arr(blk.forbid_events);
            c.RequireChoiceKeys = ChoiceKeys(blk.require_choices);
            c.ForbidChoiceKeys = ChoiceKeys(blk.forbid_choices);
            c.RequireEventCounts = (blk.require_event_counts != null && blk.require_event_counts.Count > 0)
                                   ? blk.require_event_counts.ToArray() : EMPTY_CC;
            c.RequireBuildings = (blk.require_buildings != null && blk.require_buildings.Count > 0)
                                 ? blk.require_buildings.ToArray() : EMPTY_BC;
            c.RequireResearch = Arr(blk.require_research);

            c.IsEmpty = c.RequireEvents.Length == 0 && c.RequireAnyEvents.Length == 0
                        && c.ForbidEvents.Length == 0
                        && c.RequireChoiceKeys.Length == 0 && c.ForbidChoiceKeys.Length == 0
                        && c.RequireEventCounts.Length == 0
                        && c.RequireBuildings.Length == 0 && c.RequireResearch.Length == 0;
            return c;
        }

        private static string[] Arr(List<string> src)
        {
            if (src == null || src.Count == 0) return EMPTY;

            var list = new List<string>();
            for (int i = 0; i < src.Count; i++)
                if (!string.IsNullOrEmpty(src[i])) list.Add(src[i]);
            return list.Count == 0 ? EMPTY : list.ToArray();
        }

        private static string[] ChoiceKeys(List<ChoiceRef> src)
        {
            if (src == null || src.Count == 0) return EMPTY;

            var list = new List<string>();
            for (int i = 0; i < src.Count; i++)
            {
                var r = src[i];
                if (r == null || string.IsNullOrEmpty(r.@event) || string.IsNullOrEmpty(r.option)) continue;
                list.Add(EventHistory.MakeChoiceKey(r.@event, r.option));
            }
            return list.Count == 0 ? EMPTY : list.ToArray();
        }

        private static void WarnOp(string op)
        {
            EventWarnOnce.Warn("condop:" + op, "[MoreFood/Event][Cond] 非法比较符 '" + op
                               + "'（只用 >= <= > < == !=）→ 该条件按不通过处理");
        }
    }
}
