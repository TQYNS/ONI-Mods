// EventHistoryScanner.cs — 事件框架 · 【叙事状态层的启动扫描与自检】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventHistoryScanner
    {
        // 启动自检：扫描事件定义的历史状态问题，返回问题条数
        public static int RunAll(IEnumerable<EventDef> defs)
        {
            int problems = 0;
            try
            {
                var list = new List<EventDef>();
                if (defs != null)
                {
                    foreach (var d in defs) if (d != null) list.Add(d);
                }

                CompileConditions(list);
                CompileItems(list);   // ★v3.4：提交 / 奖励预编译 + 分级汇报

                var refEvents = new HashSet<string>(StringComparer.Ordinal);
                var refChoices = new HashSet<string>(StringComparer.Ordinal);
                ScanReferenced(list, refEvents, refChoices);
                EventHistory.SetReferenced(refEvents, refChoices);

                problems += ValidateRefs(list, refEvents, refChoices);
                problems += ValidateBranches(list);
                problems += EventConditionRegistry.ValidateDependencies();

                Debug.Log("[MoreFood/Event][History] 静态扫描完成：惰性记录名单 事件 " + refEvents.Count
                          + " 个 / 选项 " + refChoices.Count + " 个，自检问题 " + problems + " 处");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][History] RunAll 异常: " + e);
            }
            return problems;
        }

        // ① 条件预编译
        private static void CompileConditions(List<EventDef> defs)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                d.compiled = EventCondition.Compile(d);
                if (d.options == null) continue;

                for (int j = 0; j < d.options.Count; j++)
                {
                    var o = d.options[j];
                    if (o == null) continue;
                    o.compiled = EventCondition.Compile(o);
                }
            }
        }

 // ①b 提交 / 奖励预编译 + 分级汇报
        private static void CompileItems(List<EventDef> defs)
        {
            int errors = 0, warns = 0, deferred = 0;
            bool assetsReady = ItemStackParser.IsAssetIndexReady();

            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                if (d.options == null) continue;

                for (int j = 0; j < d.options.Count; j++)
                {
                    var o = d.options[j];
                    if (o == null) continue;

                    string owner = d.id + ".options[" + j + "]";
                    bool warnEnabled = o.Warning;

                    // ---- submit_materials：只在 yaml 里【配了这个键】时才编译+汇报 ----
                    if (o.submit_materials != null)
                    {
                        o.submit_compiled = ItemStackParser.Parse(o.submit_materials);
                        if (assetsReady)
                        {
                            ItemStackParser.Validate(o.submit_compiled);
                            if (ItemStackParser.Report(o.submit_compiled, owner + ".submit_materials", warnEnabled))
                            {
                                if (o.submit_compiled.formatError || o.submit_compiled.IsAllFailed) errors++;
                                else warns++;
                            }
                        }
                        else if (!o.submit_compiled.formatError) deferred++;
                    }
                    else
                    {
                        o.submit_compiled = new ItemParseResult();   // 未配 → 空结果，IsEmpty==true
                        o.submit_compiled.notConfigured = true;
                    }

                    // ---- grant_rewards：同上，且允许"奖励池"引用 ----
                    if (o.grant_rewards != null)
                    {
                        o.reward_compiled = ItemStackParser.Parse(o.grant_rewards, true);
                        if (assetsReady)
                        {
                            ItemStackParser.Validate(o.reward_compiled);
                            if (ItemStackParser.Report(o.reward_compiled, owner + ".grant_rewards", warnEnabled))
                            {
                                if (o.reward_compiled.formatError || o.reward_compiled.IsAllFailed) errors++;
                                else warns++;
                            }
                        }
                        else if (!o.reward_compiled.formatError) deferred++;
                    }
                    else
                    {
                        o.reward_compiled = new ItemParseResult();
                        o.reward_compiled.notConfigured = true;
                    }

                    ValidateLabels(o, owner);
                }
            }

            if (deferred > 0)
                Debug.Log("[MoreFood/Event][Item] 资产索引尚未就绪（Assets.PrefabsByTag 为空，Game.OnSpawn 时点太早）"
                          + " → " + deferred + " 组物品校验推迟到「面板打开 / 点选项」时进行（不影响游戏运行）");

            if (errors + warns > 0)
                Debug.Log("[MoreFood/Event][Item] 启动校验：有 " + errors + " 组完全用不了、"
                          + warns + " 组部分条目被跳过（以上都只是配置提示，不影响游戏运行）");
        }

 // ①c 信息栏文案（submit_label / grant_label）的启动校验
        private const int MAX_LABEL_LENGTH = 200;

        private static void ValidateLabels(EventOption o, string owner)
        {
            o.submit_label = CheckLabel(o.submit_label, o.submit_materials,
                                        owner + ".submit_label", owner + ".submit_materials");
            o.grant_label = CheckLabel(o.grant_label, o.grant_rewards,
                                       owner + ".grant_label", owner + ".grant_rewards",
                                       true);
        }

        private static string CheckLabel(string label, List<object> group, string labelSlot, string groupSlot,
                                         bool allowDisplayOnly = false)
        {
            if (label == null) return null;
            if (label.Trim().Length == 0) return null;

            if ((group == null || group.Count == 0) && !allowDisplayOnly)
            {
                Debug.LogWarning("[MoreFood/Event][Item] " + labelSlot
                                 + " 配了文案，但没有 " + groupSlot + " → 忽略该文案");
                return null;
            }

            if (label.Length > MAX_LABEL_LENGTH)
            {
                Debug.LogWarning("[MoreFood/Event][Item] " + labelSlot + " 超长（"
                                 + label.Length + " > " + MAX_LABEL_LENGTH + "）→ 已截断");
                return label.Substring(0, MAX_LABEL_LENGTH);
            }
            return label;
        }

        // ② 收集名单
        private static void ScanReferenced(List<EventDef> defs, HashSet<string> evts, HashSet<string> choices)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                var c = d.condition;
                AddEvents(evts, c != null ? c.require_events : null);
                AddEvents(evts, c != null ? c.require_any_events : null);
                AddEvents(evts, c != null ? c.forbid_events : null);
                AddChoices(choices, c != null ? c.require_choices : null);
                AddChoices(choices, c != null ? c.forbid_choices : null);
                AddCountEvents(evts, c != null ? c.require_event_counts : null);

                if (d.options == null) continue;
                for (int j = 0; j < d.options.Count; j++)
                {
                    var o = d.options[j];
                    if (o == null) continue;
                    var oc = o.condition;
                    AddEvents(evts, oc != null ? oc.require_events : null);
                    AddEvents(evts, oc != null ? oc.require_any_events : null);
                    AddEvents(evts, oc != null ? oc.forbid_events : null);
                    AddChoices(choices, oc != null ? oc.require_choices : null);
                    AddChoices(choices, oc != null ? oc.forbid_choices : null);
                    AddCountEvents(evts, oc != null ? oc.require_event_counts : null);
                }
            }

            // precondition 声明的依赖（evt: / opt: 前缀进名单；flag:/counter:/var: 只参与自检）
            foreach (var dep in EventConditionRegistry.AllDependencies())
            {
                if (string.IsNullOrEmpty(dep) || dep.Length < 5) continue;
                if (dep.StartsWith("evt:", StringComparison.Ordinal)) evts.Add(dep.Substring(4));
                else if (dep.StartsWith("opt:", StringComparison.Ordinal)) choices.Add(dep.Substring(4));
            }
        }

        private static void AddEvents(HashSet<string> set, List<string> src)
        {
            if (src == null) return;
            for (int i = 0; i < src.Count; i++)
                if (!string.IsNullOrEmpty(src[i])) set.Add(src[i]);
        }

        // 次数条件引用的事件也要进名单（否则不会被计数 → 条件永远 false）
        private static void AddCountEvents(HashSet<string> set, List<EventCountCond> src)
        {
            if (src == null) return;
            for (int i = 0; i < src.Count; i++)
            {
                var c = src[i];
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.key)) set.Add(c.key);
                AddEvents(set, c.events);
            }
        }

        private static void AddChoices(HashSet<string> set, List<ChoiceRef> src)
        {
            if (src == null) return;
            for (int i = 0; i < src.Count; i++)
            {
                var r = src[i];
                if (r == null || string.IsNullOrEmpty(r.@event) || string.IsNullOrEmpty(r.option)) continue;
                set.Add(EventHistory.MakeChoiceKey(r.@event, r.option));
            }
        }

        // ③ 引用存在性 + op 合法性
        private static int ValidateRefs(List<EventDef> defs, HashSet<string> evts, HashSet<string> choices)
        {
            int bad = 0;

            foreach (var id in evts)
            {
                if (EventRegistry.Has(id)) continue;
                bad++;
                Debug.LogWarning("[MoreFood/Event][Story] require/forbid_events 引用了不存在的事件 id: '" + id + "'");
            }

            foreach (var key in choices)
            {
                int sep = key.LastIndexOf('\u0001');
                if (sep <= 0)
                {
                    bad++;
                    Debug.LogWarning("[MoreFood/Event][Story] 选项键格式非法: '" + key + "'");
                    continue;
                }

                string eventId = key.Substring(0, sep);
                string optionId = key.Substring(sep + 1);

                var d = EventRegistry.Get(eventId);
                if (d == null)
                {
                    bad++;
                    Debug.LogWarning("[MoreFood/Event][Story] require/forbid_choices 引用了不存在的事件: '" + eventId + "'");
                    continue;
                }

                bool found = false;
                if (d.options != null)
                {
                    for (int i = 0; i < d.options.Count; i++)
                    {
                        var o = d.options[i];
                        if (o != null && o.id == optionId) { found = true; break; }
                    }
                }
                if (!found)
                {
                    bad++;
                    Debug.LogWarning("[MoreFood/Event][Story] require/forbid_choices 引用了不存在的选项: '"
                                   + eventId + "' 的 '" + optionId + "'");
                }
            }

            // op 合法性（require_event_counts 用 >= <= > < == !=）
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                bad += CheckCountOps(d.condition != null ? d.condition.require_event_counts : null, d.id);
                if (d.options == null) continue;
                for (int j = 0; j < d.options.Count; j++)
                {
                    var o = d.options[j];
                    if (o == null) continue;
                    bad += CheckCountOps(o.condition != null ? o.condition.require_event_counts : null,
                                         d.id + ".options[" + j + "]");
                }
            }

            return bad;
        }

        private static int CheckCountOps(List<EventCountCond> cs, string owner)
        {
            int bad = 0;
            if (cs == null) return 0;

            for (int i = 0; i < cs.Count; i++)
            {
                var c = cs[i];
                if (c == null || string.IsNullOrEmpty(c.op)) continue;
                if (!IsCounterOp(c.op))
                {
                    bad++;
                    Debug.LogWarning("[MoreFood/Event][Story] '" + owner + "' 的 require_event_counts["
                                   + i + "] op 非法：'" + c.op + "'（只用 >= <= > < == !=）");
                }
            }
            return bad;
        }

        private static bool IsCounterOp(string op)
        {
            return op == ">=" || op == "<=" || op == ">" || op == "<" || op == "==" || op == "!=";
        }

        // ③b next_event 引用校验（启动时带日志跑一次；运行时的高频检查是静默的）
        private static int ValidateBranches(List<EventDef> defs)
        {
            int bad = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                bad += CheckBranches(d.next_event, d.id + ".next_event");

                if (d.options == null) continue;
                for (int j = 0; j < d.options.Count; j++)
                {
                    var o = d.options[j];
                    if (o == null) continue;
                    bad += CheckBranches(o.next_event, d.id + ".options[" + j + "].next_event");
                }
            }
            return bad;
        }

        // 解析一遍（带日志）+ 统计无法解析的项数
        private static int CheckBranches(List<object> raw, string owner)
        {
            if (raw == null || raw.Count == 0) return 0;

            int parsed = EventDef.ParseBranches(raw, owner).Count;
            int bad = raw.Count - parsed;
            if (bad > 0)
                Debug.LogWarning("[MoreFood/Event][Story] " + owner + " 有 " + bad
                               + " 项无法解析（见上）→ 这些分支不会生效");
            return bad;
        }

    }
}
