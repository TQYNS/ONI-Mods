// EventSystem.cs — 事件框架 · 【统一入口 + 运行态】

using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public static class EventSystem
    {
        // 运行态
        private static readonly List<EventInstance> _active = new List<EventInstance>();

        // 读档恢复完成回调：实例已恢复（EventSaveBridge.Restore）、闹钟已重排之后触发。
        // 宿主（如持续 buff）借此重建纯运行时效果——框架本身不重跑 handler。
        // ★ 用回调而不是让宿主 Harmony 硬挂框架方法：框架方法签名变动不会拖垮宿主 mod 加载。
        public static System.Action OnRestored;

 // eventId → 上次触发时刻（游戏秒；负数 = 从未触发）单位由周期改为秒
        private static readonly Dictionary<string, float> _lastTriggerTime = new Dictionary<string, float>();

        // ★ 每个事件的冷却结束时刻（游戏秒）。成功执行时 roll 一次 [min,max] 并固定；
        //   冷却期间不重掷，下次成功执行再重掷。随存档保存。
        private static readonly Dictionary<string, float> _cooldownUntil = new Dictionary<string, float>();

        // 本帧已触发过的 eventId（单帧去重）
        private static readonly HashSet<string> _firedThisFrame = new HashSet<string>();
        // 上次做单帧去重时的帧号
        private static int _firedFrame = -1;

        // 待触发的链式
        public static readonly List<ScheduledBranch> Branches = new List<ScheduledBranch>();

        // 事件历史（「事件」tab 两个页面卡的数据源）
        public class HistoryEntry
        {
            // 事件模板 id
            public string id;
            // 事件显示名
            public string name;
            // 发生周期
            public int cycle;
            public string outcome;        // 触发（reason）/ 选择：X / 结束：reason
            // 实例 id（点回顾时反查进行中实例）
            public string instanceId;
            public int choiceIndex = -1;   // 玩家选了哪个选项（-1 = 未选）
            public bool isKey;              // def.key_event
            // ★ 与通知同一套解析渲染好的正文（变体 + 占位符 + 富文本）；老档为空 ⇒ 回退显示名称
            public string tip;
        }

        public const int EVENT_KEEP = 50;     // 「事件」页底层保留
        public const int KEY_KEEP = 100;    // 「历史」页关键记录上限
        public const int PAGE_SIZE = 10;     // 两个页卡统一每页条数

        private static readonly List<HistoryEntry> _events = new List<HistoryEntry>();
        private static readonly List<HistoryEntry> _keys = new List<HistoryEntry>();
        // ★ 第二十二轮-3：被"清空"标记过的实例（instanceId）—— 其结束时不再写入「事件」页（防清完立刻回填）
        private static readonly HashSet<string> _suppressHistory = new HashSet<string>(StringComparer.Ordinal);

        public static IList<HistoryEntry> History { get { return _events; } }
        public static IList<HistoryEntry> KeyHistory { get { return _keys; } }
        public static int KeyCount { get { return _keys.Count; } }

        // instId：标识 id
        public static HistoryEntry FindHistory(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return null;
            for (int i = 0; i < _events.Count; i++)
                if (_events[i] != null && _events[i].instanceId == instId) return _events[i];
            return null;
        }

        // ★ 第二十二轮-3：在「历史」页（关键节点）里按 instanceId 查
        private static HistoryEntry FindKeyHistory(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return null;
            for (int i = 0; i < _keys.Count; i++)
                if (_keys[i] != null && _keys[i].instanceId == instId) return _keys[i];
            return null;
        }

        public static void AddHistory(string id, string name, int cycle, string outcome,
                                      string instId, int choiceIndex = -1, bool isKey = false)
        {
            try
            {
                var e = FindHistory(instId);
                bool suppressed = !string.IsNullOrEmpty(instId) && _suppressHistory.Contains(instId);
                if (e == null && suppressed)
                {
                    e = FindKeyHistory(instId);   // ★ 清空后仍复用「历史」页里已有的关键节点条目
                }
                if (e != null)
                {
                    e.outcome = outcome;
                    if (choiceIndex >= 0) e.choiceIndex = choiceIndex;
                    e.isKey = e.isKey || isKey;
                    if (isKey && FindKeyHistory(instId) == null)
                    {
                        _keys.Insert(0, e);
                        while (_keys.Count > KEY_KEEP) _keys.RemoveAt(_keys.Count - 1);
                    }
                    return;
                }

                e = new HistoryEntry
                {
                    id = id,
                    name = name,
                    cycle = cycle,
                    outcome = outcome,
                    instanceId = instId,
                    choiceIndex = choiceIndex,
                    isKey = isKey,
                };
                // ★ 第二十二轮-3：被"清空"标记过的实例不再写入「事件」页（关键节点仍照常记入「历史」页）
                if (!suppressed)
                {
                    _events.Insert(0, e);
                    while (_events.Count > EVENT_KEEP) _events.RemoveAt(_events.Count - 1);
                }
                if (isKey)
                {
                    _keys.Insert(0, e);
                    while (_keys.Count > KEY_KEEP) _keys.RemoveAt(_keys.Count - 1);
                }
            }
            catch (Exception e2) { Debug.LogWarning("[MoreFood/Event] AddHistory 异常: " + e2); }
        }

        // 清空「事件」页滚动摘要。
        public static void ClearHistory()
        {
            _events.Clear();
            // _keys（关键节点）刻意不清 —— 用户口径：清理不清"历史中的关键节点事件"
            try
            {
                for (int i = 0; i < _active.Count; i++)
                {
                    EventInstance a = _active[i];
                    if (a != null && a.state != EventInstanceState.Ended && !string.IsNullOrEmpty(a.instanceId))
                    {
                        _suppressHistory.Add(a.instanceId);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ClearHistory 标记进行中实例失败: " + e);
            }
        }

        // 是否已初始化
        private static bool _inited;
        // 运行序号（拼 instanceId 用）
        private static int _runCounter;

        public static bool Inited { get { return _inited; } }
        public static IList<EventInstance> ActiveInstances { get { return _active; } }

        // ★ 该实例是否仍「进行中」——判据与「事件」屏上方"进行中 N 个事件"严格同源：
        public static bool IsOngoing(string instId)
        {
            if (string.IsNullOrEmpty(instId)) return false;
            for (int i = 0; i < _active.Count; i++)
            {
                EventInstance it = _active[i];
                if (it != null && it.state == EventInstanceState.Active && it.instanceId == instId)
                {
                    return true;
                }
            }
            return false;
        }

        // ★ 抽取频率硬下限（游戏秒）：叙事者/槽/事件自己怎么配都不得低于它。
        public const float MinDrawIntervalSeconds = 5f;

        // 是否有事件在跑（互斥检查用）
        public static bool HasActive
        {
            get
            {
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i] != null && _active[i].state != EventInstanceState.Ended) return true;
                return false;
            }
        }

        public static EventInstance CurrentActive
        {
            get
            {
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i] != null && _active[i].state != EventInstanceState.Ended) return _active[i];
                return null;
            }
        }

        // 待兑现的分支容器（表态时决定、事件收尾时兑现）
        public class ScheduledBranch
        {
            // 要接的下一个事件 id
            public string nextEventId;
            public float triggerAtTime;      // ★v2：绝对游戏秒（不再是周期）
            public List<string> visited = new List<string>();
 // ：正在排队等互斥窗口释放（存盘）
            public bool waitingForMutex;
            // 运行时：已排的闹钟句柄（不存盘）
            [NonSerialized] public SchedulerHandle handle;
            // 运行时：排队超时闹钟句柄（不存盘）
            [NonSerialized] public SchedulerHandle timeoutHandle;
        }

        // 初始化
        public static void Init()
        {
            try
            {
 // 记录主线程 id（用于 Trigger 的跨线程检测）
                _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

                // 重入清理（本局残留）
                EventRegistry.Clear();
                CancelAllInstanceAlarms();      // 先清闹钟（要在 _active 清空之前）
                _active.Clear();
                _lastTriggerTime.Clear();
                _cooldownUntil.Clear();
                _presetPromoted.Clear();
                Branches.Clear();
                _firedThisFrame.Clear();
                EventAudioTrigger.Shutdown();
                EventNarratorSelection.ResetForSession();
                EventMediaPlayer.Clear();
                EventNotifier.Clear();        // ★v3.5：清左侧通知栏残留（换局 / 二次读档时旧通知不残留）
                AudioSwitch.Clear();          // ★ 音频开关是运行态（不存盘）：换局/读档一律归零
                EventHistory.Clear();
                _suppressHistory.Clear();     // ★ 第二十二轮-3：换局/读档清掉"清空标记"
                // 0) 奖励池必须先于事件加载：事件 yaml 的 grant_rewards 可用 [[池id]] 引用
                RewardBoxLoader.LoadAll();

                // 1) 加载 yaml（扫全 Mod 目录）
                int n = EventYamlLoader.LoadAll();
                Debug.Log("[MoreFood/Event] YAML 加载完成：" + EventRegistry.Describe()
                          + "，解析失败文件=" + EventYamlLoader.FailedFiles);

                // 2) 静态链式闭环检测
                EventChainChecker.DetectAndBreakCycles();

                // 3) 校验 handler 绑定 + yaml 前置条件键
                ValidateHandlers();
                EventConditionRegistry.Validate(EventRegistry.All);
                EventHookRegistry.Validate(EventRegistry.All);   // ★v2 start_fn / end_fn 是否都注册了
                ValidateBranchOnly();                            // ★v3.2 branch_only 孤儿检查
                ReportMutexCoverage();                           // ★v3.2 未声明互斥的事件数（Info）

 // 3d) 叙事状态层：条件编译 + 引用扫描 + 双向自检（必须在存档恢复之前）
                EventHistoryScanner.RunAll(EventRegistry.All);

                // 3b) 音频模板（音效库 yaml → 内置件幂等注册 + 音源可解析性自检 + yaml 引用自检）
                EventAudioLibrary.LoadAll();
                EventAudioFactory.EnsureBuiltins();
                EventMediaPlayer.ValidateAll();
                EventAudioLibrary.ValidateVariants();
                EventMediaPlayer.ValidateYamlRefs(EventRegistry.All);
                EventAudioTrigger.EnsureBuilt();   // ★音效库的 fire_on / flag 绑定（信号与开关）

                // 3c) 全局 ID 命名空间提醒（不阻断）
                EventNamingConvention.Check();

                // 4) 存档载体
                EventSaveBridge.Install();

                // 5) 读档恢复（若有数据）
                EventSaveBridge.Restore();

                // 5b) 读档后重排闹钟（duration / resident / 链式到点全部靠闹钟，不再有 1 秒 tick）
                ScheduleRestoredAlarms();

                // 5d) 读档恢复完成 ⇒ 通知宿主重建纯运行时效果（持续 buff 等）。
                //     放在闹钟重排之后，宿主拿到的剩余时间已对齐；异常吞掉，绝不影响 Init 本身。
                try { if (OnRestored != null) OnRestored(); }
                catch (System.Exception e) { Debug.LogWarning("[MoreFood/Event] OnRestored 回调异常: " + e); }

                // 5c) 读档后按开关当前状态对齐音频（开着的补播、关着的停掉）
                EventAudioTrigger.RefreshFlags();

                // 6) 排第一个闹钟：读档走 Resume（恢复配置 + 续订），新局走 StartFresh
                string wanted = EventNarratorSelection.ResolveForSession();
                bool fromFrontEnd = EventNarratorSelection.IsPendingFromFrontEnd;
                bool started = fromFrontEnd
                    ? EventNarratorDirector.StartFresh(wanted)
                    : EventNarratorDirector.Resume();
                if (fromFrontEnd) EventNarratorSelection.TakePending();

                if (started)
                {
                    var running = EventNarratorDirector.Running;
                    Debug.Log("[MoreFood/Narr] 本局叙事者 = "
                              + (running != null ? running.Id : (wanted ?? "默认"))
                              + (fromFrontEnd ? "（开局选择）" : "（来自存档 / 默认）"));
                    if (fromFrontEnd) EventSaveBridge.Capture();
                }
                else
                {
                    Debug.LogWarning("[MoreFood/Narr] 叙事者启动失败（" + (wanted ?? "默认")
                                     + "）→ 重新启动默认");
                    EventNarratorDirector.RestartRunning();
                }

                _inited = true;
                Debug.Log("[MoreFood/Event] 初始化完成（事件 " + EventRegistry.Count + " 个）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] Init 异常: " + e);
            }
        }

        private static void ValidateHandlers()
        {
            int missing = 0;
            foreach (var def in EventRegistry.All)
            {
                if (string.IsNullOrEmpty(def.handler)) { missing++; continue; }
                if (!EventHandlerRegistry.Has(def.handler))
                {
                    missing++;
                    Debug.LogWarning("[MoreFood/Event] 事件 '" + def.id
                                     + "' 的 handler '" + def.handler
                                     + "' 未注册（代码端漏了 Register？），该事件不会被触发");
                }
            }
            if (missing > 0)
                Debug.LogWarning("[MoreFood/Event] 共 " + missing + " 个事件的 handler 未绑定");
        }

 // Info：并发默认开启 ⇒ 提示有多少事件"没有声明任何互斥"（可同时进行）
        private static void ReportMutexCoverage()
        {
            try
            {
                int free = 0, total = 0;
                foreach (var d in EventRegistry.All)
                {
                    if (d == null || d.trigger_mode != EventTriggerMode.scheduled) continue;
                    total++;
                    var mc = d.condition;
                    bool hasMutex = (mc != null && mc.mutex_running != null && mc.mutex_running.Count > 0)
                                    || (mc != null && mc.mutex_groups != null && mc.mutex_groups.Count > 0);
                    if (!hasMutex) free++;
                }

                if (free > 0)
                    Debug.Log("[MoreFood/Event][互斥] " + free + "/" + total
                              + " 个可抽取事件未声明任何互斥 → 它们可与其他事件同时进行"
                              + "（如需串行：给它们加 mutex_groups，或把当前叙事者的 PickMode 设为 single）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][互斥] 覆盖率统计异常（已跳过）: " + e);
            }
        }

 // 自检：branch_only 事件必须被某个 next_event 引用，否则永远不会触发
        private static int ValidateBranchOnly()
        {
            int orphan = 0;
            try
            {
                var referenced = new HashSet<string>(StringComparer.Ordinal);

                foreach (var d in EventRegistry.All)
                {
                    if (d == null) continue;
                    CollectBranchRefs(d.next_event, referenced);
                    if (d.options == null) continue;
                    for (int i = 0; i < d.options.Count; i++)
                    {
                        var o = d.options[i];
                        if (o == null) continue;
                        CollectBranchRefs(o.next_event, referenced);
                    }
                }

                foreach (var d in EventRegistry.All)
                {
                    if (d == null || d.trigger_mode != EventTriggerMode.branch_only) continue;
                    if (referenced.Contains(d.id)) continue;

                    orphan++;
                    Debug.LogWarning("[MoreFood/Event][校验] branch_only 事件 '" + d.id
                                     + "' 没有任何 next_event 引用它 → 永远不会被触发（孤儿事件）");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][校验] branch_only 检查异常（已跳过）: " + e);
            }
            return orphan;
        }

        private static void CollectBranchRefs(List<object> raw, HashSet<string> into)
        {
            if (raw == null) return;
            var bs = EventDef.ParseBranches(raw, "自检");
            for (int i = 0; i < bs.Count; i++) into.Add(bs[i].next);
        }

        // 统一入口
        public static bool Trigger(string eventId, string reason = null,
                                   System.Collections.Generic.IEnumerable<string> inheritedVisited = null)
        {
            var def = EventRegistry.Get(eventId);
            if (def == null)
            {
                Debug.LogWarning("[MoreFood/Event] Trigger('" + eventId + "') 找不到模板（来自 " + reason + "）");
                return false;
            }
            return Trigger(def, reason, inheritedVisited);
        }

        public static bool Trigger(EventDef def, string reason = null,
                                   System.Collections.Generic.IEnumerable<string> inheritedVisited = null)
        {
            if (def == null) return false;

            try
            {
 // ---- 主线程检测：跨线程调用直接拒绝（本框架不做跨线程同步）----
                if (!IsMainThread())
                {
                    Debug.LogWarning("[MoreFood/Event] Trigger 被【非主线程】调用（'" + def.id
                                   + "'）→ 已拒绝。请把调用派发回主线程");
                    return false;
                }

 // ---- 手动来源闸门（M3 / B11）----
                // 调度器抽取（scheduler）与链式（next_event）之外，凡手动 / 调试 / 界面来源，
                // 必须先有一个可用叙事者。玩家把三位叙事者全部停用后，触发应被拒绝，
                // 而不是绕开叙事者自己跑起来。
                if (IsManualSource(reason) && EventNarratorDirector.ResolveDefault() == null)
                {
                    Debug.LogWarning("[MoreFood/Event] 手动触发被拒：当前没有可用的叙事者"
                                   + "（三位是否全部停用？）→ '" + def.id + "'（来源=" + (reason ?? "null") + "）");
                    return false;
                }

                // ---- 单帧去重 ----
                if (Time.frameCount != _firedFrame) { _firedFrame = Time.frameCount; _firedThisFrame.Clear(); }
                if (_firedThisFrame.Contains(def.id))
                {
                    Debug.LogWarning("[MoreFood/Event] 单帧重复触发被拒: " + def.id);
                    return false;
                }

 // ---- 同 id 重入：按 on_duplicate 策略处理（§17.4）----
                var dup = FindInWindow(def.id);
                if (dup != null)
                {
                    if (!def.allow_self_concurrent)
                    {
                        Debug.Log("[MoreFood/Event] '" + def.id
                                  + "' 仍在持续中，默认禁止自身并发 → 本次触发被拒（reason="
                                  + (reason ?? "-") + "）");
                        return false;
                    }
                    if (ApplyDuplicatePolicy(def, dup, reason) == DupResult.Rejected) return false;
                    // Continue ⇒ 新建一份
                }

 // ---- one_shot 统一拦（抽取 / 分支 / 手动都走这里）----
                if (IsOneShotUsed(def))
                {
                    Debug.LogWarning("[MoreFood/Event] one_shot 事件 '" + def.id
                                     + "' 已触发过 → 拒绝（reason=" + (reason ?? "-") + "）");
                    return false;
                }

 // 承诺优先：Trigger 里【不再做互斥拒绝】

                // ---- 建实例 ----
                var cycle = GetCycle();
                var inst = EventInstance.Create(def, cycle, GetGameTime());

 // visited：继承父链 + 加自己（去重 + 上限 64）
                if (inheritedVisited != null)
                {
                    foreach (var v in inheritedVisited) inst.AddVisited(v);
                }
                inst.AddVisited(def.id);

                var handler = EventHandlerRegistry.Get(def.handler);

 // ---- 声明式条件门（9 字段；全部 AND）----
                if (!CheckCondition(def))
                {
                    Debug.Log("[MoreFood/Event] '" + def.id + "' 声明式条件不通过（require_/forbid_）");
                    return false;
                }

                // ---- 前置条件（唯一入口：yaml 的 condition.function → EventConditionRegistry）----
                if (!EventConditionRegistry.Evaluate(def, inst))
                {
                    Debug.Log("[MoreFood/Event] '" + def.id + "' 前置条件不通过（"
                              + (def.condition != null ? def.condition.function : "?") + "）");
                    return false;
                }

                // ---- 登记 ----
                _active.Add(inst);
                _firedThisFrame.Add(def.id);
                _lastTriggerTime[def.id] = GetGameTime();
                // ★ 成功执行 ⇒ 进入本次冷却（roll 一次区间值）
                ArmCooldown(def);
                _runCounter++;

 // 先写痕迹，再执行 action / 回调（action 崩了痕迹仍在）
                EventHistory.NotifyTriggered(def.id);

                inst.state = EventInstanceState.Active;

 // ：根级效果【触发即生效】；选择轴按有无选项分流
                BeginEffect(inst, false, "触发");

                int optCount = (def.options != null) ? def.options.Count : 0;
                bool allowPanel = def.notification == null || def.notification.show_panel;

                if (optCount == 0)
                {
                    // 无选项 ⇒ 决策立即完成（不排 resident、不弹面板）
                    Debug.Log("[MoreFood/Event] 无选项 → 决策立即完成（效果由根级承担）");
                    inst.choicePhase = EventChoicePhase.Chosen;
                    inst.chosenIndex = -1;
                    inst.chosenByDefault = true;
                    DecideBranch(inst, def, GetGameTime());
                    TryFinalize(inst);
                }
                else if (!allowPanel)
                {
                    // 只通知不弹面板 ⇒ 立即按兜底算法替玩家决定（§16.7）
                    Debug.Log("[MoreFood/Event] show_panel=false → 立即按兜底算法自动决策");
                    int autoIdx = PickDefaultOption(def);
                    if (autoIdx >= 0) ApplyChoice(inst, def, autoIdx, true);
                    else
                    {
                        inst.choicePhase = EventChoicePhase.Chosen;
                        inst.chosenIndex = -1;
                        inst.chosenByDefault = true;
                        DecideBranch(inst, def, GetGameTime());
                        EventNotifier.RemoveEvent(inst);
                        TryFinalize(inst);
                    }
                }
                else
                {
                    ScheduleResidentAlarm(inst);
                }

                Debug.Log("[MoreFood/Event] ★ 触发事件 '" + def.id + "'（" + inst.instanceId + "）"
                          + " cycle=" + cycle + " reason=" + (reason ?? "-"));

                // ★ 标题取实例冻结的变体名（未配 variants 时回落根级 name）：与通知/面板同一口径
                AddHistory(def.id, inst.GetShownName(), cycle,
                           "触发（" + (reason ?? "-") + "）", inst.instanceId,
                           -1, def.key_event);

                // ---- 回调 ----
                EventHandlerRegistry.SafeCallTrigger(handler, inst, def.id);
                EventHandlerRegistry.SafeCallStart(handler, inst, def.id);

                // ★ 历史正文必须在 handler 之后补写：无时长事件会在 handler 之前就 TryFinalize，
                //   那时 NoticeArgs 还空（占位符解析不出来），历史条目也可能尚未建立。
                RefreshHistoryTip(inst, def);

 // ---- AI 桥接（空壳）----
                EventAiBridge.OnEventTriggered(inst, def);

                // ---- 音频（先起音，再推通知；通知携带暂停标记时一并处理）----
                EventMediaPlayer.PlayOnTrigger(def, inst);

                // ---- 通知（原版通知系统，Doc-V §T14）----
                if (def.notification == null || def.notification.notify)
                    EventNotifier.Push(def, inst);

                EventSaveBridge.Capture();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] Trigger('" + def.id + "') 异常: " + e);
                return false;
            }
        }

        // 某池里全部 scheduled 事件的 id（调试面板按 id 触发用；纯只读，不产生任何调度）
        public static string[] EventIds(string pool)
        {
            string filter = string.IsNullOrEmpty(pool) ? "normal" : pool;
            List<string> ids = new List<string>();
            try
            {
                foreach (EventDef def in EventRegistry.All)
                {
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    if (def.trigger_mode != EventTriggerMode.scheduled) continue;
                    string p = string.IsNullOrEmpty(def.pool) ? "normal" : def.pool;
                    if (p != filter) continue;
                    ids.Add(def.id);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 取池事件清单失败 " + pool + ": " + e);
            }
            return ids.ToArray();
        }

        // 触发并立即代玩家选定某个选项：与面板点击走同一条 ApplyChoice 路径，
        // 只是不等 resident / 不弹面板。供「阅读即生效」的主动凭证类入口（任务卷轴）使用。
        public static bool TriggerOption(string eventId, string optionId, string reason = null)
        {
            EventDef def = EventRegistry.Get(eventId);
            if (def == null || def.options == null || def.options.Count == 0)
            {
                Debug.LogWarning("[MoreFood/Event] TriggerOption('" + eventId + "') 找不到模板或没有选项");
                return false;
            }

            int idx = -1;
            for (int i = 0; i < def.options.Count; i++)
            {
                if (def.options[i] != null && def.options[i].id == optionId)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0)
            {
                Debug.LogWarning("[MoreFood/Event] TriggerOption('" + eventId + "') 没有选项 '" + optionId + "'");
                return false;
            }

            int before = _active.Count;
            if (!Trigger(def, reason, null))
            {
                return false;
            }

            // Trigger 成功即把实例追加到 _active 末尾（同帧内不会插入别的实例）
            EventInstance inst = _active.Count > before ? _active[_active.Count - 1] : null;
            if (inst == null || inst.templateId != def.id)
            {
                Debug.LogWarning("[MoreFood/Event] TriggerOption('" + eventId
                                 + "') 取不到刚创建的实例 → 放弃代选（事件仍在等待面板表态）");
                return false;
            }

            Debug.Log("[MoreFood/Event] TriggerOption：'" + eventId + "' 触发后代选选项 '" + optionId + "'");
            return ApplyChoice(inst, def, idx, true);
        }

        // 结束
        public static string NormalizeOutcome(string outcome)
        {
            if (string.IsNullOrEmpty(outcome)) return outcome;
            // 展示文案：只把"结束…"归一成【已结束】，其余（触发 / 选择…）原样保留。
            if (outcome.StartsWith("结束")) return "已结束";
            return outcome;
        }

        // 强制结束（玩家点「结束事件」/ 外部需要立刻收尾）。
        public static void End(EventInstance inst, string reason = null)
        {
            if (inst == null) return;
            try
            {
                if (inst.state == EventInstanceState.Ended) return;

                Debug.Log("[MoreFood/Event] 强制结束 '" + inst.templateId + "'（"
                          + inst.instanceId + "）reason=" + (reason ?? "-"));

                inst.choicePhase = EventChoicePhase.Chosen;   // 强制结束视为"已表态"
                FinalizeEnd(inst);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] End 异常: " + e);
            }
        }

        // 结束当前唯一活跃事件
        public static void EndCurrent(string reason = null)
        {
            End(CurrentActive, reason);
        }

        // 每帧/每秒 Tick（由叙事者 EventNarrator 的循环闹钟驱动）
        private static void ScheduleInstanceAlarms(EventInstance inst)
        {
            if (inst == null) return;
            CancelInstanceAlarms(inst);

            ScheduleResidentAlarm(inst);

            if (inst.hasDuration && !inst.triggerEffectDone) ScheduleEffectAlarm(inst, false);
            if (inst.hasChoiceEffect && !inst.choiceEffectDone) ScheduleEffectAlarm(inst, true);
        }

        // 选择轴：resident 到点 ⇒ 强制弹窗
        private static void ScheduleResidentAlarm(EventInstance inst)
        {
            if (inst == null || !inst.hasResident || inst.ChoiceForced || inst.ChoiceChosen) return;

            var gs = GameScheduler.Instance;
            if (gs == null) return;

            try
            {
                float secs = inst.GetResidentRemaining();
                if (secs < 0.01f) secs = 0.01f;
                inst.residentEndsAt = GetGameTime() + secs;

                var self = inst;
                inst.residentHandle = gs.Schedule("MoreFoodEvent_Resident", secs,
                    new System.Action<object>(delegate (object data) { ForcePopup(self); }), null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 排 resident 闹钟异常: " + e);
            }
        }

        // 效果轴：给某个效果槽排闹钟（choiceSlot=false 根级槽 / true 决策槽）
        private static void ScheduleEffectAlarm(EventInstance inst, bool choiceSlot)
        {
            if (inst == null) return;

            var gs = GameScheduler.Instance;
            if (gs == null)
            {
                Debug.LogWarning("[MoreFood/Event][Sched] GameScheduler 不可用 → 效果闹钟未排（读档重排会补）: "
                                 + inst.templateId);
                return;
            }

            try
            {
                float secs = choiceSlot ? inst.GetChoiceEffectRemaining() : inst.GetDurationRemaining();
                if (secs < 0.01f) secs = 0.01f;

                inst.effectAnyStarted = true;
                var self = inst;

                if (choiceSlot)
                {
                    inst.choiceEffectEndsAt = GetGameTime() + secs;
                    inst.choiceEffectHandle = gs.Schedule("MoreFoodEvent_ChoiceEffect", secs,
                        new System.Action<object>(delegate (object data) { ExpireEffect(self, true); }), null, null);
                }
                else
                {
                    inst.triggerEffectEndsAt = GetGameTime() + secs;
                    inst.triggerEffectHandle = gs.Schedule("MoreFoodEvent_TriggerEffect", secs,
                        new System.Action<object>(delegate (object data) { ExpireEffect(self, false); }), null, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 排效果闹钟异常: " + e);
            }
        }

        // 本次决策选中的选项（没选 / 越界 ⇒ null）
        private static EventOption ChosenOption(EventInstance inst, EventDef def)
        {
            if (inst == null || def == null || def.options == null) return null;
            int i = inst.chosenIndex;
            if (i < 0 || i >= def.options.Count) return null;
            return def.options[i];
        }

 // 新增助手
        internal static bool CheckCondition(EventDef def)
        {
            string ignored;
            return CheckCondition(def, out ignored);
        }

        // 同上，但给出【不通过的原因】（抽取追踪用）
        internal static bool CheckCondition(EventDef def, out string reason)
        {
            reason = null;
            if (def == null) return true;

            var c = def.compiled;
            if (c == null || c.IsEmpty) return true;

            try { return c.Check(out reason); }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] CheckCondition 异常（按不通过处理）: " + e);
                reason = "条件求值异常：" + e.GetType().Name;
                return false;
            }
        }

 // 选项可用性：该选项自身条件 + （可选）其分支目标当前能否触发
        internal static bool IsOptionAvailable(EventDef def, EventOption opt)
        {
            if (opt == null) return true;

            try
            {
                if (opt.compiled != null && !opt.compiled.IsEmpty && !opt.compiled.Check()) return false;

                if (def != null && def.check_branch_availability && opt.next_event != null)
                {
                    var branches = EventDef.ParseBranches(opt.next_event, def.id + ".options.avail", true);
                    if (branches.Count > 0)
                    {
                        bool any = false;
                        for (int i = 0; i < branches.Count; i++)
                        {
                            var target = EventRegistry.Get(branches[i].next);
                            if (target == null) continue;
                            if (IsBlockedByWindow(target)) continue;      // 与正在跑的事件互斥
                            if (!CheckCondition(target)) continue;        // 目标条件不满足
                            if (IsOneShotUsed(target)) continue;          // one_shot 已用尽
                            any = true; break;
                        }
                        if (!any) return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] IsOptionAvailable 异常（按可用处理）: " + e);
                return true;
            }
        }

        // 主线程 id（在 Init 里记录；-1 = 还没记录）
        private static int _mainThreadId = -1;

        // 是否在主线程（与 Init 时记录的主线程 id 比较，零反射零依赖）
        // 手动来源：null（直接调）/ 调试 / 界面。调度器与链式不算。
        private static bool IsManualSource(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return true;
            return reason == "debug" || reason == "debug_all"
                || reason == "manual" || reason == "scroll";
        }

        private static bool IsMainThread()
        {
            if (_mainThreadId < 0) return true;    // 还没记录（Init 之前）⇒ 不拦
            return System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

 // 窗口互斥判定（遍历窗口内全部实例，双向 + 组；不维护冗余集合）
        internal static bool IsBlockedByWindow(EventDef cand)
        {
            return MutexBlocker(cand) != null;
        }

        // 被哪个正在跑的事件挡住（没被挡返回 null）—— 抽取追踪用
        internal static string MutexBlocker(EventDef cand)
        {
            if (cand == null) return null;

            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a == null || a.state != EventInstanceState.Active) continue;   // 只看窗口内

                var ad = a.def ?? EventRegistry.Get(a.templateId);
                if (ad == null) continue;

                if (ad.id == cand.id && !cand.allow_self_concurrent)
                {
                    return ad.id + "，同 id 持续中（默认禁止自身并发）";
                }

                string how = null;
                var adm = ad.condition != null ? ad.condition.mutex_running : null;
                var cdm = cand.condition != null ? cand.condition.mutex_running : null;
                var adg = ad.condition != null ? ad.condition.mutex_groups : null;
                var cdg = cand.condition != null ? cand.condition.mutex_groups : null;
                if (adm != null && adm.Contains(cand.id)) how = "对方 mutex_running 含我";
                else if (cdm != null && cdm.Contains(ad.id)) how = "我 mutex_running 含对方";
                else if (GroupsIntersect(adg, cdg)) how = "mutex_groups 相交";

                if (how != null) return ad.id + "，" + how;
            }
            return null;
        }

        private static bool GroupsIntersect(List<string> a, List<string> b)
        {
            if (a == null || b == null) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (string.IsNullOrEmpty(a[i])) continue;
                for (int j = 0; j < b.Count; j++)
                    if (a[i] == b[j]) return true;
            }
            return false;
        }

 // 唤醒排队中的分支（在【互斥窗口关闭时】调用：FinalizeEnd 提交后 / 读档后）
        internal static void WakeWaitingBranches()
        {
            if (Branches.Count == 0) return;

            try
            {
                float now = GetGameTime();
                for (int i = Branches.Count - 1; i >= 0; i--)
                {
                    var sb = Branches[i];
                    if (sb == null) { Branches.RemoveAt(i); continue; }
                    if (!sb.waitingForMutex) continue;
                    if (sb.triggerAtTime > now) continue;                  // 时间还没到，继续等
                    if (IsBlockedByWindow(EventRegistry.Get(sb.nextEventId))) continue;   // 还被挡

                    Branches.RemoveAt(i);
                    Debug.Log("[MoreFood/Event] 队列唤醒 → 触发分支 " + sb.nextEventId);
                    Trigger(sb.nextEventId, "branch", sb.visited);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] WakeWaitingBranches 异常: " + e);
            }
        }

 // 排队时排一个超时闹钟（零轮询；<=0 = 无限等）
        private static void ScheduleBranchTimeoutAlarm(ScheduledBranch sb)
        {
            float wait = EventNarratorDirector.BranchWaitTimeoutSeconds;
            if (wait <= 0f || sb == null) return;

            var gs = GameScheduler.Instance;
            if (gs == null) return;

            try
            {
                var self = sb;
                sb.timeoutHandle = gs.Schedule("MoreFoodEvent_BranchTimeout", wait,
                    new System.Action<object>(delegate (object data)
                    {
                        if (self.waitingForMutex && Branches.Contains(self)) AbandonWaitingBranch(self);
                    }), null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 排分支超时闹钟异常: " + e);
            }
        }

 // 排队超时：放弃该分支 + 写一条历史（玩家可见）
        private static void AbandonWaitingBranch(ScheduledBranch sb)
        {
            if (sb == null) return;
            Branches.Remove(sb);

            var def = EventRegistry.Get(sb.nextEventId);
            AddHistory(sb.nextEventId, def != null ? def.GetNameText() : sb.nextEventId,
                       GetCycle(), "因世界繁忙未生效", "branch_wait:" + sb.nextEventId, -1, false);

            Debug.LogWarning("[MoreFood/Event] 分支 " + sb.nextEventId + " 等待互斥窗口超时（"
                             + EventNarratorDirector.BranchWaitTimeoutSeconds.ToString("F0")
                             + "s）→ 放弃。它被某个互斥事件长期占用");
            EventSaveBridge.Capture();
        }

        // 窗口内（Active）是否有同 id 实例
        internal static EventInstance FindInWindow(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a != null && a.state == EventInstanceState.Active && a.templateId == eventId) return a;
            }
            return null;
        }

 // one_shot 是否已用尽（零新状态：复用上次触发时刻）
        internal static bool IsOneShotUsed(EventDef def)
        {
            if (def == null || def.repeat == null || !def.repeat.one_shot) return false;
            return GetLastTriggerTime(def.id) >= 0f;
        }

        private enum DupResult { Continue, Rejected }

        // 同 id 重入策略：reject 丢弃（默认） / allow 允许并存（新建一份）
        private static DupResult ApplyDuplicatePolicy(EventDef def, EventInstance dup, string reason)
        {
            try
            {
                if (def.on_duplicate == EventDuplicatePolicy.allow)
                {
                    Debug.Log("[MoreFood/Event] '" + def.id + "' 允许并存（on_duplicate=allow）→ 新建一份");
                    return DupResult.Continue;
                }

                Debug.Log("[MoreFood/Event] '" + def.id + "' 已在窗口内，on_duplicate=reject → 丢弃（"
                          + (reason ?? "-") + "）");
                return DupResult.Rejected;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ApplyDuplicatePolicy 异常（降级为新建一份）: " + e);
                return DupResult.Continue;
            }
        }

        // 选中项配的决策效果时长（秒）；未配 ⇒ 0 = 该选项没有决策效果
        private static float GetChosenOptionDuration(EventInstance inst, EventDef def)
        {
            var o = ChosenOption(inst, def);
            if (o == null || o.duration <= 0f) return 0f;
            return o.duration;
        }

 // 唯一的效果启动入口（详见 §16.4）
        internal static void BeginEffect(EventInstance inst, bool choiceSlot, string source)
        {
            if (inst == null) return;
            var def = inst.def ?? EventRegistry.Get(inst.templateId);

            try
            {
                if (choiceSlot)
                {
                    float secs = GetChosenOptionDuration(inst, def);
                    inst.hasChoiceEffect = secs > 0f;
                    inst.choiceEffectRemaining = secs > 0f ? secs : 0f;
                    inst.choiceEffectEndsAt = 0f;
                    inst.choiceEffectDone = !inst.hasChoiceEffect;

                    var opt = ChosenOption(inst, def);
                    EventHookRegistry.Invoke(opt != null ? opt.start_fn : null, inst, inst.templateId,
                                             "option.start_fn");

                    Debug.Log("[MoreFood/Event] ▶ 决策效果开始（来源=" + source + "，时长="
                              + (secs > 0f ? secs.ToString("F0") + "s" : "无") + "）");

 // 选项级通知：选项被触发那一刻推一条（notice_name 非空才推）

 if (opt != null && !string.IsNullOrEmpty(opt.notice_name))

 { EventNotifier.PushOptionNotice(def, inst, opt); }


 // 选项级状态行（opt.status = true 才推；效果结束自动撤）
                    if (inst.hasDuration && inst.hasChoiceEffect && opt != null && opt.status
                        && def != null && def.notification != null)
                    {
                        EventNotifier.PushStatus(def, inst, true, opt.status_text, opt.status_tooltip);
                    }
                }
                else
                {
                    if (!inst.hasDuration) { inst.triggerEffectDone = true; }

                    // §16.9：开始钩子 yaml 先、handler 后（handler.onStart 由 Trigger 在这之后调）
                    EventHookRegistry.Invoke(def != null ? def.start_fn : null, inst, inst.templateId,
                                             "start_fn");

                    Debug.Log("[MoreFood/Event] ▶ 根级效果开始（来源=" + source + "，时长="
                              + (inst.hasDuration ? inst.GetDurationRemaining().ToString("F0") + "s" : "无")
                              + "）");

 // notification.status = true 才推状态行（默认不推）
                    if (inst.hasDuration && def != null && def.notification != null
                        && def.notification.status)
                    {
                        EventNotifier.PushStatus(def, inst, false, def.notification.status_text,
                                                     def.notification.status_tooltip);
                    }
                }

                inst.effectAnyStarted = true;

                if (choiceSlot ? inst.hasChoiceEffect : inst.hasDuration)
                    ScheduleEffectAlarm(inst, choiceSlot);

                TryFinalize(inst);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] BeginEffect 异常: " + e);
            }
        }

 // 效果槽到点（或收尾时补调）：各自调一次自己的结束回调
// 效果结束：两阶段提交（§17.2）
        internal static void ExpireEffect(EventInstance inst, bool choiceSlot)
        {
            if (inst == null) return;
            var def = inst.def ?? EventRegistry.Get(inst.templateId);

            // ① 重入守卫
            if (choiceSlot)
            {
                if (inst.choiceEffectDone || inst.choiceEffectExpiring) return;
                inst.choiceEffectExpiring = true;
            }
            else
            {
                if (inst.triggerEffectDone || inst.triggerEffectExpiring) return;
                inst.triggerEffectExpiring = true;
            }

            try
            {
                // ② 临界区：结束回调
                if (choiceSlot)
                {
                    CancelChoiceEffectAlarm(inst);
                    var opt = ChosenOption(inst, def);
                    EventHookRegistry.Invoke(opt != null ? opt.end_fn : null, inst, inst.templateId,
                                             "option.end_fn");

                    Debug.Log("[MoreFood/Event] ⏱ 决策效果结束 | " + AxisText(inst));
                    EventNotifier.RemoveStatus(inst, true);   // ★v3.8 选项级状态行随决策效果结束而撤
                }
                else
                {
                    CancelTriggerEffectAlarm(inst);

                    Debug.Log("[MoreFood/Event] ⏱ 根级效果结束（duration 到期）| " + AxisText(inst));

                    var handler = EventHandlerRegistry.Get(def != null ? def.handler : null);
                    EventHandlerRegistry.SafeCallEnd(handler, inst, inst.templateId);

                    // §16.9：结束钩子 handler 先、yaml 后
                    EventHookRegistry.Invoke(def != null ? def.end_fn : null, inst, inst.templateId, "end_fn");
                    EventMediaPlayer.OnEffectEnd(def, inst);
                    EventNotifier.RemoveStatus(inst);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ExpireEffect 结束回调异常（仍会提交）: " + e);
            }
            finally
            {
                // ③ 提交点：此后才算"效果结束"，互斥窗口由此关闭
                if (choiceSlot) { inst.choiceEffectExpiring = false; inst.choiceEffectDone = true; }
                else { inst.triggerEffectExpiring = false; inst.triggerEffectDone = true; }

                try { TryFinalize(inst); }
                catch (Exception e) { Debug.LogWarning("[MoreFood/Event] TryFinalize 异常: " + e); }
            }
        }

 // 收尾判定（纯函数）：两轴都到头才收尾
        internal static void TryFinalize(EventInstance inst)
        {
            if (inst == null) return;
            if (!inst.ChoiceChosen) return;
            if (!inst.EffectAxisDone()) return;
            FinalizeEnd(inst);
        }

        // 两条轴的当前状态（日志用）
        internal static string AxisText(EventInstance inst)
        {
            if (inst == null) return "-";
            string eff = inst.GetEffectPhase().ToString()
                         + (inst.hasDuration && !inst.triggerEffectDone
                            ? "(根级剩 " + inst.GetDurationRemaining().ToString("F0") + "s)" : "")
                         + (inst.hasChoiceEffect && !inst.choiceEffectDone
                            ? "(决策剩 " + inst.GetChoiceEffectRemaining().ToString("F0") + "s)" : "");
            string cho = inst.GetChoicePhaseText();
            return "轴①=" + eff + " · 轴②=" + cho;
        }

        private static void CancelResidentAlarm(EventInstance inst)
        {
            if (inst == null) return;
            try { if (inst.residentHandle.IsValid) inst.residentHandle.ClearScheduler(); }
            catch (Exception e) { DistributionLog.ExcOnce("ev:clr-resident", "[MoreFood/Event] 清 resident 闹钟失败", e); }
            inst.residentHandle = default(SchedulerHandle);
        }

        private static void CancelTriggerEffectAlarm(EventInstance inst)
        {
            if (inst == null) return;
            try { if (inst.triggerEffectHandle.IsValid) inst.triggerEffectHandle.ClearScheduler(); }
            catch (Exception e) { DistributionLog.ExcOnce("ev:clr-trigger", "[MoreFood/Event] 清触发效果闹钟失败", e); }
            inst.triggerEffectHandle = default(SchedulerHandle);
        }

        private static void CancelChoiceEffectAlarm(EventInstance inst)
        {
            if (inst == null) return;
            try { if (inst.choiceEffectHandle.IsValid) inst.choiceEffectHandle.ClearScheduler(); }
            catch (Exception e) { DistributionLog.ExcOnce("ev:clr-choice", "[MoreFood/Event] 清选择效果闹钟失败", e); }
            inst.choiceEffectHandle = default(SchedulerHandle);
        }

        private static void CancelInstanceAlarms(EventInstance inst)
        {
            if (inst == null) return;
            CancelTriggerEffectAlarm(inst);
            CancelChoiceEffectAlarm(inst);
            CancelResidentAlarm(inst);
        }

        private static void CancelAllInstanceAlarms()
        {
            for (int i = 0; i < _active.Count; i++) CancelInstanceAlarms(_active[i]);
        }

        // 读档 / 重排：给恢复出来的实例与链式补闹钟
        private static void ScheduleRestoredAlarms()
        {
            int n = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] == null) continue;
                ScheduleInstanceAlarms(_active[i]);
                n++;
            }
            for (int i = 0; i < Branches.Count; i++)
            {
                var sb = Branches[i];
                if (sb == null) continue;
                if (sb.waitingForMutex) ScheduleBranchTimeoutAlarm(sb);   // ★v3.2 恢复排队超时
                else ScheduleBranchAlarm(sb);
            }
            WakeWaitingBranches();                                        // ★v3.2 读档后尝试唤醒

            if (n > 0 || Branches.Count > 0)
                Debug.Log("[MoreFood/Event][Sched] 读档后重排闹钟：实例 " + n + " 个，链式 "
                          + Branches.Count + " 条");
        }

        // 驻留时间到期 → 强制弹窗 + 暂停
        private static void ForcePopup(EventInstance inst)
        {
            if (inst == null || inst.ChoiceForced || inst.ChoiceChosen) return;
            inst.choicePhase = EventChoicePhase.ForcedPanel;
            CancelResidentAlarm(inst);       // 已经弹了，驻留闹钟使命结束（duration 闹钟还得留着）

            try
            {
                var def = inst.def ?? EventRegistry.Get(inst.templateId);
                int optCount = (def != null && def.options != null) ? def.options.Count : 0;
                bool allowPop = def == null || def.notification == null || def.notification.show_panel;
                bool residentPop = def != null && def.notification != null && def.notification.resident_popup;

                // 没选项 / 策划关了弹窗 / 未开启"驻留到期强制弹窗" → 直接走保底，不打扰玩家
                if (optCount == 0 || !allowPop || !residentPop)
                {
                    Debug.Log("[MoreFood/Event] ⏰ 驻留到期（"
                              + (optCount == 0 ? "无选项"
                                 : (!allowPop ? "show_panel=false" : "resident_popup=false"))
                              + "）→ 直接走保底默认选项");
                    ResolveByDefault(inst);
                    return;
                }

                Debug.Log("[MoreFood/Event] ⏰ 驻留时间到期仍未选择 → 强制弹出面板并暂停: "
                          + inst.instanceId);

                AcquireForcePause(inst);

                var panel = EventPanel.ShowFor(inst, true);
                if (panel == null && !EventPanel.IsQueued(inst))
                {
                    // 面板开不出来（找不到 UI 父节点）⇒ 绝不能把游戏卡在暂停里
                    Debug.LogWarning("[MoreFood/Event] 强制弹窗失败 → 直接走保底默认选项");
                    ResolveByDefault(inst);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ForcePopup 异常（强制走保底）: " + e);
                try { ResolveByDefault(inst); }
                catch (Exception e2) { DistributionLog.ExcOnce("ev:force-default", "[MoreFood/Event] 强制保底再次失败", e2); }
            }
        }

        // 玩家把关掉的强制弹窗（或弹不出来）→ 按配置抽保底选项
        public static void ResolveByDefault(EventInstance inst)
        {
            if (inst == null || inst.ChoiceChosen) return;
            try
            {
                var def = inst.def ?? EventRegistry.Get(inst.templateId);
                int idx = PickDefaultOption(def);

                if (idx < 0)
                {
                    Debug.Log("[MoreFood/Event] 保底默认：无可用选项 → 不执行任何 action");
                    inst.choicePhase = EventChoicePhase.Chosen;
                    EventNotifier.RemoveEvent(inst);
                    ReleaseForcePause(inst);
                    if (inst.EffectAxisDone()) FinalizeEnd(inst);
                    return;
                }
                ApplyChoice(inst, def, idx, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ResolveByDefault 异常: " + e);
                inst.choicePhase = EventChoicePhase.Chosen;
                ReleaseForcePause(inst);
            }
        }

        // 保底选项抽取
        public static int PickDefaultOption(EventDef def)
        // 单选是特例：只有一个选项时直接命中（保持旧行为）
        {
            if (def == null || def.options == null || def.options.Count == 0) return -1;
            if (def.options.Count == 1) return 0;

            var idxs = new List<int>();
            var ws = new List<float>();

            // ① 默认项
            for (int i = 0; i < def.options.Count; i++)
            {
                var o = def.options[i];
                if (o == null || !o.is_default) continue;

                float w = o.weight >= 0f ? o.weight : 1f;
                if (w <= 0f) { WarnDefault(def, "默认项 options[" + i + "] 权重 <= 0，已跳过"); continue; }
                idxs.Add(i); ws.Add(w);
            }
            if (idxs.Count == 1) return idxs[0];
            if (idxs.Count > 1)
            {
                Debug.LogWarning("[MoreFood/Event] '" + def.id + "' 配了 " + idxs.Count
                                 + " 个 is_default 项 → 在它们之间按权重随机");
                return WeightedPick(idxs, ws);
            }

            // ② 没有默认项 ⇒ weight > 0 的项（未配 = 1；退出项默认不参与）
            for (int i = 0; i < def.options.Count; i++)
            {
                var o = def.options[i];
                if (o == null) continue;

                bool exitOption = string.IsNullOrEmpty(o.action);
                float w = o.weight >= 0f ? o.weight : (exitOption ? 0f : 1f);
                if (w <= 0f) continue;
                idxs.Add(i); ws.Add(w);
            }
            if (idxs.Count == 0)
            {
                Debug.LogWarning("[MoreFood/Event] '" + def.id
                                 + "' 没有任何可用兜底项（全 weight<=0 / 全是退出项）→ 不选任何项");
                return -1;
            }
            return WeightedPick(idxs, ws);
        }

        private static int WeightedPick(List<int> idxs, List<float> ws)
        {
            float total = 0f;
            for (int i = 0; i < ws.Count; i++) total += ws[i];
            if (total <= 0f) return idxs[0];

            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < ws.Count; i++)
            {
                r -= ws[i];
                if (r <= 0f) return idxs[i];
            }
            return idxs[idxs.Count - 1];
        }

        private static int FindOptionIndex(EventDef def, string id)
        {
            if (def == null || def.options == null) return -1;
            for (int i = 0; i < def.options.Count; i++)
            {
                var o = def.options[i];
                if (o != null && o.id == id) return i;
            }
            return -1;
        }

        private static void WarnDefault(EventDef def, string why)
        {
            Debug.LogWarning("[MoreFood/Event] 兜底选项数据问题（'" + (def != null ? def.id : "?")
                             + "'）：" + why);
        }

        // 执行一次选择（玩家点的 / 超时保底的）—— 面板与保底路径共用
        public static bool ApplyChoice(EventInstance inst, EventDef def, int idx, bool isDefault)
        {
            if (inst == null || def == null) return false;
            try
            {
                var opt = (def.options != null && idx >= 0 && idx < def.options.Count)
                          ? def.options[idx] : null;

 // ：启动时资产索引尚未就绪（Game.OnSpawn 时点太早）
                if (opt != null)
                {
                    string preOwner = def.id + ".options[" + idx + "]";
                    ItemStackParser.EnsureValidated(opt.submit_compiled, preOwner + ".submit_materials", opt.Warning);
                    ItemStackParser.EnsureValidated(opt.reward_compiled, preOwner + ".grant_rewards", opt.Warning);
                }

                // ═══════════════════════════════════════════════════════
                if (!isDefault
                    && opt != null
                    && opt.submit_compiled != null
                    && !opt.submit_compiled.IsEmpty)
                {
                    string itemOwner = def.id + ".options[" + idx + "]";
                    bool warnEnabled = opt.Warning;

                    if (!ItemStackExecutor.CanSubmit(
                            opt.submit_compiled, itemOwner + ".submit_materials", warnEnabled))
                    {
                        Debug.LogWarning("[MoreFood/Event] 选项 " + idx
                                         + " 提交材料不足 → 拒绝本次选择（面板保持打开）");
                        return false;   // ← 关键：告知调用方"没成功"
                    }
                }

                // ================= 以下为原有流程 =================

                inst.playerChoices.Add(idx);
                inst.chosenIndex = idx;
                inst.chosenByDefault = isDefault;
                DecideBranch(inst, def, GetGameTime());   // ★v2：表态那一刻就决定走向

                string picked = EventDef.ResolveText(opt != null ? opt.label : null, "选项 " + idx);
                Debug.Log("[MoreFood/Event] " + (isDefault ? "⏰ 超时默认" : "玩家选择")
                          + " '" + def.id + "' 选项 " + idx + "（" + picked + "）");

                EventNotifier.RemoveEvent(inst);

                AddHistory(def.id, inst.GetShownName(), GetCycle(),
                           (isDefault ? "超时默认：" : "选择：") + picked,
                           inst.instanceId, idx, def.key_event);

 // 先写痕迹，再执行 action
                if (opt != null && !string.IsNullOrEmpty(opt.id))
                    EventHistory.NotifyChosen(def.id, opt.id);

 // 提交 / 奖励（执行阶段）：
                if (opt != null && opt.HasItemEffect)
                {
                    string itemOwner = def.id + ".options[" + idx + "]";
                    bool warnEnabled = opt.Warning;

                    var sr = ItemStackExecutor.TrySubmit(
                        opt.submit_compiled, itemOwner + ".submit_materials", warnEnabled);

                    // 只有非 Failed 才发奖励（Skipped / Success 都发）
                    if (sr != ItemStackExecutor.SubmitResult.Failed
                        && opt.reward_compiled != null && !opt.reward_compiled.IsEmpty)
                    {
                        ItemStackExecutor.Grant(
                            opt.reward_compiled, itemOwner + ".grant_rewards", warnEnabled);
                    }
                }

                var handler = EventHandlerRegistry.Get(def.handler);
                if (opt != null && !string.IsNullOrEmpty(opt.action))
                    EventHandlerRegistry.SafeCallOption(handler, opt.action, inst, def.id);

                EventMediaPlayer.PlayOnChoice(def, inst);
                EventAiBridge.OnPlayerChoice(inst, idx);

                inst.choicePhase = EventChoicePhase.Chosen;
                ReleaseForcePause(inst);
                CancelResidentAlarm(inst);

                BeginEffect(inst, true, isDefault ? "保底替选" : "玩家选择");

                EventSaveBridge.Capture();
                TryFinalize(inst);

                return true;   // ★v3.5：成功
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ApplyChoice 异常: " + e);
                inst.choicePhase = EventChoicePhase.Chosen;
                ReleaseForcePause(inst);
                return false;
            }
        }

        // 强制弹窗持有的暂停（与 EventNotifier 的 pause_game 那份各自独立配对）
        private static void AcquireForcePause(EventInstance inst)
        {
            try
            {
                if (inst == null || inst.forcePauseHeld) return;
                var sc = SpeedControlScreen.Instance;
                if (sc == null) return;
                sc.Pause(false, false);
                inst.forcePauseHeld = true;
                Debug.Log("[MoreFood/Event] 已暂停（强制弹窗持有）");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] 暂停失败: " + e); }
        }

        private static void ReleaseForcePause(EventInstance inst)
        {
            try
            {
                if (inst == null || !inst.forcePauseHeld) return;
                inst.forcePauseHeld = false;
                var sc = SpeedControlScreen.Instance;
                if (sc != null) sc.Unpause(false);
                Debug.Log("[MoreFood/Event] 已解除暂停（强制弹窗释放）");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] 恢复失败: " + e); }
        }

        // duration 到期：只停事件效果，事件实体继续存活等玩家处理（由闹钟调）
        private static void ExpireDuration(EventInstance inst)
        {
            if (inst == null || inst.triggerEffectDone) return;
            inst.triggerEffectDone = true;
            try
            {
                var def = inst.def ?? EventRegistry.Get(inst.templateId);
                var handler = EventHandlerRegistry.Get(def != null ? def.handler : null);

                Debug.Log("[MoreFood/Event] ⏱ duration 到期 → 停效果（事件仍在等玩家选择）: "
                          + inst.instanceId);

                EventHandlerRegistry.SafeCallEnd(handler, inst, inst.templateId);
                EventMediaPlayer.OnEffectEnd(def, inst);

                // 两个计时器都完事 → 收尾（原来在 Tick 里判，现在这里判）
                if (inst.ChoiceChosen) FinalizeEnd(inst);
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] ExpireDuration 异常: " + e); }
        }

        // 真正结束（两个计时器都完事 / 被强制结束）
// 收尾：提交点移到【末尾】（§17.2）——
        private static void FinalizeEnd(EventInstance inst)
        {
            if (inst == null) return;
            if (inst.state == EventInstanceState.Ended || inst.finalizing) return;
            inst.finalizing = true;

            var def = inst.def ?? EventRegistry.Get(inst.templateId);
            try
            {
                Debug.Log("[MoreFood/Event] ● 结束事件 '" + inst.templateId + "'（"
                          + inst.instanceId + "）");

                // 还没到点就被收尾的槽 → 各自补一次结束（配对：每个已启动的槽恰好调一次）
                if (!inst.RootEffectDone()) ExpireEffect(inst, false);
                if (inst.hasChoiceEffect && !inst.choiceEffectDone) ExpireEffect(inst, true);

                AddHistory(inst.templateId, inst.GetShownName(),
                           GetCycle(), "已结束", inst.instanceId,
                           -1, def != null && def.key_event);

                // ★ 历史页正文：与通知走同一套解析（变体 → 模板 → 占位符 → 富文本），在结束时定稿
                if (def != null)
                {
                    try
                    {
                        var hist = FindHistory(inst.instanceId);
                        if (hist != null) hist.tip = EventNotifier.RenderNoticeTooltipNow(def, inst);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MoreFood/Event] 历史正文渲染失败（已忽略）: " + e);
                    }
                }

                CancelInstanceAlarms(inst);
                EventAiBridge.OnEventEnded(inst, def);
                ReleaseForcePause(inst);

                // 撤掉左侧通知（Doc-V §T14.7 的配对）
                EventNotifier.Remove(inst);

                // 音频兜底清理（含 on_end 起的循环 / 曲目 / 快照）
                EventMediaPlayer.Cleanup(inst);

 // 分支兑现：表态时已决定；没决定（强制结束/从未表态）⇒ 按"没人拍板"用根级默认
                if (def != null)
                {
                    if (!inst.pendingBranchDecided) DecideBranch(inst, def, GetGameTime());
                    SchedulePendingBranch(inst);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] FinalizeEnd 异常（仍会提交）: " + e);
            }
            finally
            {
 // 提交点：窗口在这里才关（state=Ended）+ 出活跃表
                inst.finalizing = false;
                inst.state = EventInstanceState.Ended;
                try { _active.Remove(inst); }
                catch (Exception e) { DistributionLog.ExcOnce("ev:deactive", "[MoreFood/Event] 出活跃表失败", e); }

                // 窗口刚关 ⇒ 唤醒所有排队等窗口的分支（零轮询）
                try { WakeWaitingBranches(); }
                catch (Exception e) { DistributionLog.ExcOnce("ev:wake", "[MoreFood/Event] 唤醒排队分支失败", e); }
                try { EventSaveBridge.Capture(); }
                catch (Exception e) { DistributionLog.ExcOnce("ev:capture", "[MoreFood/Event] 收尾存盘快照失败", e); }
            }
        }

        // 链式
        private static void ScheduleBranchAlarm(ScheduledBranch sc)
        {
            if (sc == null) return;

            var gs = GameScheduler.Instance;
            if (gs == null)
            {
                Debug.LogWarning("[MoreFood/Event][Sched] GameScheduler 不可用 → 链式闹钟未排: "
                                 + sc.nextEventId);
                return;
            }

            float secs = sc.triggerAtTime - GetGameTime();   // ★v2：已经是秒
            if (secs < 0.01f) secs = 0.01f;

            try
            {
                var self = sc;
                sc.handle = gs.Schedule("MoreFoodEvent_Branch", secs,
                    new System.Action<object>(delegate (object data) { FireDueBranch(self); }), null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 排链式闹钟异常: " + e);
            }
        }

 // 分支（§16.8）：表态/无选项那一刻【决定】，事件收尾时【兑现】
        private class BranchSource
        {
            // 分支候选（含权重）
            public List<EventBranch> branches;
            // 触发延迟范围（秒）
            public float delayMin, delayMax;
            public bool allowRepeat;      // ★v3.2：本来源的分支是否允许回到已访问节点
            // 来源描述文本（日志用）
            public string text;
        }

        private static BranchSource ResolveBranchSource(EventInstance inst, EventDef def)
        {
            var opt = (inst != null && def != null && def.options != null
                       && inst.chosenIndex >= 0 && inst.chosenIndex < def.options.Count)
                      ? def.options[inst.chosenIndex] : null;

            bool playerChose = inst != null && inst.ChoiceChosen && !inst.chosenByDefault;
            string optTag = def != null ? def.id + ".options[" + inst.chosenIndex + "].next_event" : "next_event";

            if (opt != null && opt.next_event != null)
            {
                return new BranchSource
                {
                    branches = EventDef.ParseBranches(opt.next_event, optTag),
                    delayMin = opt.next_delay_min,
                    delayMax = opt.next_delay_max,
                    allowRepeat = opt.next_allow_repeat,
                    text = "选项 " + inst.chosenIndex,
                };
            }

            if (opt != null && playerChose)
                return new BranchSource { branches = null, text = "选项 " + inst.chosenIndex + "（未配，玩家亲选 ⇒ 无后续）" };

            if (def != null && def.next_event != null)
            {
                return new BranchSource
                {
                    branches = EventDef.ParseBranches(def.next_event, def.id + ".next_event"),
                    delayMin = def.next_delay_min,
                    delayMax = def.next_delay_max,
                    allowRepeat = def.next_allow_repeat,
                    text = opt != null ? "根级默认（选项 " + inst.chosenIndex + " 没配）" : "根级默认",
                };
            }

            return new BranchSource { branches = null, text = "无（没配任何 next_event）" };
        }

        // 决定分支（幂等：一个实例只决定一次）
        private static void DecideBranch(EventInstance inst, EventDef def, float decidedAtTime)
        {
            if (inst == null || inst.pendingBranchDecided) return;
            inst.pendingBranchDecided = true;
            inst.pendingBranchNext = "";

            try
            {
                var src = ResolveBranchSource(inst, def);
                if (src == null || src.branches == null || src.branches.Count == 0)
                {
                    Debug.Log("[MoreFood/Event] 分支：无后续（" + (src != null ? src.text : "?") + "）");
                    return;
                }

 // 目标过滤：① 已访问（除非 allow_repeat）② one_shot 已用尽
                var pool = new List<EventBranch>();
                for (int i = 0; i < src.branches.Count; i++)
                {
                    var b = src.branches[i];

                    if (!src.allowRepeat && inst.participantIds.Contains(b.next))
                    {
                        EventChainChecker.NoteCycleReject();
                        Debug.LogWarning("[MoreFood/Event] 分支 '" + b.next + "' 被防环拒绝（本链已走过："
                                         + inst.VisitedText() + "）");
                        continue;
                    }

                    if (IsOneShotUsed(EventRegistry.Get(b.next)))
                    {
                        Debug.LogWarning("[MoreFood/Event] 分支目标 '" + b.next
                                         + "' 是 one_shot 且已触发过 → 跳过该目标");
                        continue;
                    }

                    pool.Add(b);
                }
                if (pool.Count == 0) { Debug.Log("[MoreFood/Event] 分支：目标都被过滤掉 → 无后续"); return; }

                var picked = WeightedPickBranch(pool);

                float lo = Mathf.Min(src.delayMin, src.delayMax);
                float hi = Mathf.Max(src.delayMin, src.delayMax);
                float delay = (hi > lo) ? UnityEngine.Random.Range(lo, hi) : lo;
                if (delay < 0f) delay = 0f;

                inst.pendingBranchNext = picked.next;
                inst.pendingBranchAtTime = decidedAtTime + delay;

                Debug.Log("[MoreFood/Event] 分支已决定（" + src.text + "）：→ " + picked.next
                          + "，延迟 " + delay.ToString("F0") + "s（收尾时兑现）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] DecideBranch 异常: " + e);
            }
        }

        private static EventBranch WeightedPickBranch(List<EventBranch> pool)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++) total += pool[i].weight;
            if (total <= 0f) return pool[0];

            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                r -= pool[i].weight;
                if (r <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        // 收尾时兑现分支（未到点则排闹钟等；已过点则立刻）
        private static void SchedulePendingBranch(EventInstance inst)
        {
            if (inst == null || !inst.pendingBranchDecided) return;

            string next = inst.pendingBranchNext;
            if (string.IsNullOrEmpty(next)) return;

            if (!EventRegistry.Has(next))
            {
                Debug.LogWarning("[MoreFood/Event] 分支目标已失效，丢弃: " + next);
                return;
            }

 // 分支兑现前再过一次条件门（状态在"表态→兑现"之间会变）
            if (!CheckCondition(EventRegistry.Get(next)))
            {
                Debug.LogWarning("[MoreFood/Event] 分支目标 '" + next
                                 + "' 声明式条件不满足 → 本次不排（作废，不重试）");
                return;
            }

            float now = GetGameTime();
            float at = inst.pendingBranchAtTime > now ? inst.pendingBranchAtTime : now;

            var sb = new ScheduledBranch
            {
                nextEventId = next,
                triggerAtTime = at,
                visited = new List<string>(inst.participantIds),   // ★v3.2 只带父链；目标实例会自己 AddVisited
            };
            Branches.Add(sb);
            ScheduleBranchAlarm(sb);

            Debug.Log("[MoreFood/Event] 分支兑现 → " + next + "（" + Mathf.Max(0f, at - now).ToString("F0") + "s 后）");
        }

        // 由闹钟调用：某条分支到点了
        public static void FireDueBranch(ScheduledBranch sc)
        {
            try
            {
                if (sc == null || !Branches.Contains(sc)) return;   // 已被清掉 / 重复回调

                if (!EventRegistry.Has(sc.nextEventId))
                {
                    Debug.LogWarning("[MoreFood/Event] 分支目标已失效，丢弃: " + sc.nextEventId);
                    Branches.Remove(sc);
                    return;
                }

 // 承诺优先：遇互斥【不拒绝】⇒ 排队等窗口释放（§17.3 / §17.7）
                if (IsBlockedByWindow(EventRegistry.Get(sc.nextEventId)))
                {
                    if (!sc.waitingForMutex)
                    {
                        sc.waitingForMutex = true;
                        Debug.Log("[MoreFood/Event] 分支 " + sc.nextEventId
                                  + " 被互斥挡住 → 排队等窗口释放（超时 "
                                  + EventNarratorDirector.BranchWaitTimeoutSeconds.ToString("F0") + "s）");
                        ScheduleBranchTimeoutAlarm(sc);
                        EventSaveBridge.Capture();
                    }
                    return;   // 留在 Branches 里，等 WakeWaitingBranches 唤醒
                }

                Debug.Log("[MoreFood/Event] 分支到点触发 → " + sc.nextEventId);

 // ：先触发、再移除（保持原有顺序语义，visited 显式传递）
                Trigger(sc.nextEventId, "branch", sc.visited);
                Branches.Remove(sc);
                EventSaveBridge.Capture();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] FireDueBranch 异常: " + e);
            }
        }

        // 判断 def 是否是 cur 的链式后继（链式豁免互斥，Doc-V §6.3）
        private static bool IsChainedFrom(EventDef def, EventInstance cur)
        {
            if (cur == null) return false;
            for (int i = 0; i < Branches.Count; i++)
                if (Branches[i] != null && Branches[i].nextEventId == def.id) return true;
            return false;
        }

        // ── 预制池：已降级到常规池的事件（不可逆） ──
        private static readonly HashSet<string> _presetPromoted = new HashSet<string>(StringComparer.Ordinal);

        internal static bool IsPresetPromoted(string id)
        {
            return !string.IsNullOrEmpty(id) && _presetPromoted.Contains(id);
        }

        // 降级：改 pool 标记（运行时）+ 记账（随存档）；重复调用幂等
        internal static void PromotePreset(EventDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            def.pool = "normal";
            _presetPromoted.Add(def.id);
        }

        // 读档后重放：yaml 每次都重建 EventDef，pool 标记必须按存档再打一次
        internal static void ApplyPresetPromotions()
        {
            foreach (var id in _presetPromoted)
            {
                var def = EventRegistry.Get(id);
                if (def != null) def.pool = "normal";
            }
        }

        internal static int PresetPromotedCount { get { return _presetPromoted.Count; } }

        // 上次触发时刻（游戏秒）；负数 = 从未触发过
        public static float GetLastTriggerTime(string id)
        {
            float v; return _lastTriggerTime.TryGetValue(id, out v) ? v : -1f;
        }

        // 作废一次性事件的触发资格：语义等同已触发过，抽取阶段即不再进池（幂等）
        public static bool MarkOneShotUsed(string id, string why)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (GetLastTriggerTime(id) >= 0f) return false;

            _lastTriggerTime[id] = GetGameTime();
            Debug.Log("[MoreFood/Event] '" + id + "' 一次性资格作废（" + (why ?? "-")
                      + "）→ 此后不再进抽取池");
            return true;
        }

        // ★ 进入冷却：仅在事件【成功执行】后调用。随机 roll 一次 [min_interval, max_interval]，
        //   本次冷却固定为该值（不随查询重掷）；下次成功执行时再重掷。
        private static void ArmCooldown(EventDef def)
        {
            try
            {
                if (def == null || def.repeat == null) return;
                float lo = Mathf.Max(0f, def.repeat.min_interval);
                float hi = Mathf.Max(lo, def.repeat.max_interval);
                float cd = (hi > lo) ? UnityEngine.Random.Range(lo, hi) : lo;
                if (cd <= 0f) { _cooldownUntil.Remove(def.id); return; }
                _cooldownUntil[def.id] = GetGameTime() + cd;
                Debug.Log("[MoreFood/Event] 冷却开始：'" + def.id + "' " + cd.ToString("F0") + "s");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ArmCooldown 异常: " + e);
            }
        }

        // 查询冷却结束时刻；false = 无记录（老档 / 从未触发）
        public static bool TryGetCooldown(string id, out float until)
        {
            until = 0f;
            if (string.IsNullOrEmpty(id)) return false;
            return _cooldownUntil.TryGetValue(id, out until);
        }

        // 存 / 读
        public static void WriteToSave(IEventSaveData d)
        {
            if (d == null) return;
            d.ResetAll();

            var narrator = EventNarratorDirector.Running;
            d.narratorId = (narrator != null) ? narrator.Id : "";
            // 当局叙事者：下次闹钟绝对游戏秒 + 三项可调参数（开局无运行实例时全 0 ⇒ 读档走默认）
            d.narrNextDueAt = (narrator != null) ? narrator.NextDueAt : 0f;
            d.narrI0 = (narrator != null) ? narrator.I0 : 0f;
            d.narrW = (narrator != null) ? narrator.W : 0f;
            d.narrR = (narrator != null) ? narrator.R : 0f;
            // 调度器槽闹钟：随叙事者一并落盘（读档续订剩余时间，避免读档把槽节奏重置为满间隔）
            if (narrator != null)
            {
                var slots = narrator.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    if (s == null) continue;
                    d.slotName.Add(s.Name ?? "");
                    d.slotNextDueAt.Add(s.NextDueAt);
                }
            }

            d.presetPromoted.Clear();
            foreach (var id in _presetPromoted) d.presetPromoted.Add(id);

            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i]; if (a == null) continue;
                d.instId.Add(a.instanceId ?? "");
                d.instTemplate.Add(a.templateId ?? "");
                d.instStartCycle.Add(a.startCycle);
                d.instRemaining.Add(a.GetDurationRemaining());
                d.instState.Add((int)a.state);

                // ★ 驻留计时 + 两个计时器的完成标志
                d.instResident.Add(a.GetResidentRemaining());
                d.instChoiceEffect.Add(a.GetChoiceEffectRemaining());
                d.instChoiceEffectDone.Add(a.choiceEffectDone);

 // 决策结果 + 已决定的分支
                d.instChosenIndex.Add(a.chosenIndex);
                d.instChosenByDefault.Add(a.chosenByDefault);
                d.instBranchDecided.Add(a.pendingBranchDecided);
                d.instBranchNext.Add(a.pendingBranchNext ?? "");
                d.instBranchAt.Add(a.pendingBranchAtTime);
                d.instVisited.Add(a.participantIds != null ? string.Join("|", a.participantIds.ToArray()) : "");
                d.instNoticeArgs.Add(DumpNoticeArgs(a));
                d.instShownName.Add(a.shownName ?? "");
                d.instShownDesc.Add(a.shownDescription ?? "");
                d.instChoicePhase.Add((int)a.choicePhase);
                d.instTriggerEffectDone.Add(a.RootEffectDone());
            }

            foreach (var kv in _lastTriggerTime)
            { d.lastTriggerId.Add(kv.Key); d.lastTriggerTime.Add(kv.Value); }

            foreach (var kv in _cooldownUntil)
            { d.cooldownId.Add(kv.Key); d.cooldownAt.Add(kv.Value); }

            for (int i = 0; i < Branches.Count; i++)
            {
                var c = Branches[i]; if (c == null) continue;
                d.branchNextId.Add(c.nextEventId ?? "");
                d.branchAtTime.Add(c.triggerAtTime);
                d.branchVisited.Add(c.visited != null ? string.Join("|", c.visited.ToArray()) : "");
                d.branchWaiting.Add(c.waitingForMutex);
            }

 // 事件历史（事件次数表 + 选项组合表）
            EventHistory.ExportTo(d);

            for (int i = 0; i < _events.Count; i++) DumpHistoryEntry(d, _events[i]);
            for (int i = 0; i < _keys.Count; i++)
            {
                var h = _keys[i];
                if (h == null || FindInList(_events, h.instanceId)) continue;
                DumpHistoryEntry(d, h);
            }
        }

        // ── 通知占位参数（NoticeArgs）的存盘编码 ──
        // 每实例一条 "键=值;键=值"；分隔符与 % 全部转义，值里出现 ; = | % 也不会截断。
        private static string EscapeArgPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("%", "%25").Replace(";", "%3B").Replace("=", "%3D").Replace("|", "%7C");
        }

        private static string UnescapeArgPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("%3B", ";").Replace("%3D", "=").Replace("%7C", "|").Replace("%25", "%");
        }

        private static string DumpNoticeArgs(EventInstance a)
        {
            if (a == null || a.NoticeArgs == null || a.NoticeArgs.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in a.NoticeArgs)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(EscapeArgPart(kv.Key)).Append('=').Append(EscapeArgPart(kv.Value));
            }
            return sb.ToString();
        }

        private static void LoadNoticeArgs(EventInstance inst, string raw)
        {
            if (inst == null || string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (string.IsNullOrEmpty(p)) continue;
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;
                string k = UnescapeArgPart(p.Substring(0, eq));
                string v = UnescapeArgPart(p.Substring(eq + 1));
                if (!string.IsNullOrEmpty(k)) inst.SetNoticeArg(k, v);
            }
        }

        // 用当前 NoticeArgs 重新渲染历史条目正文（与通知同一条解析链）。        // 调用时机：handler 执行之后 —— 否则占位符还没写入，渲染出来的是裸 {xxx}。
        private static void RefreshHistoryTip(EventInstance inst, EventDef def)
        {
            try
            {
                if (inst == null || def == null) return;
                var h = FindHistory(inst.instanceId);
                if (h == null) return;
                string tip = EventNotifier.RenderNoticeTooltipNow(def, inst);
                if (!string.IsNullOrEmpty(tip)) h.tip = tip;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 历史正文刷新失败（已忽略）: " + e);
            }
        }

        private static void DumpHistoryEntry(IEventSaveData d, HistoryEntry h)
        {
            if (d == null || h == null) return;
            d.histInstId.Add(h.instanceId ?? "");
            d.histTemplate.Add(h.id ?? "");
            d.histName.Add(h.name ?? "");
            d.histCycle.Add(h.cycle);
            d.histOutcome.Add(h.outcome ?? "");
            d.histChoice.Add(h.choiceIndex);
            d.histKey.Add(h.isKey);
            d.histTip.Add(h.tip ?? "");
        }

        private static bool FindInList(List<HistoryEntry> list, string instId)
        {
            if (string.IsNullOrEmpty(instId)) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].instanceId == instId) return true;
            return false;
        }

// 存档一致性检查 —— 只报告，不拒绝
        private static void ReportSaveConsistency(IEventSaveData d)
        {
            if (d == null) return;

            var sb = new System.Text.StringBuilder();
            int problems = 0;

            // 以 instId.Count 为基准
            int n = d.instId.Count;
            CheckListLen(sb, ref problems, "instTemplate", d.instTemplate.Count, n);
            CheckListLen(sb, ref problems, "instStartCycle", d.instStartCycle.Count, n);
            CheckListLen(sb, ref problems, "instRemaining", d.instRemaining.Count, n);
            CheckListLen(sb, ref problems, "instState", d.instState.Count, n);
            CheckListLen(sb, ref problems, "instResident", d.instResident.Count, n);
            CheckListLen(sb, ref problems, "instChoicePhase", d.instChoicePhase.Count, n);
            CheckListLen(sb, ref problems, "instTriggerEffectDone", d.instTriggerEffectDone.Count, n);
            CheckListLen(sb, ref problems, "instChoiceEffect", d.instChoiceEffect.Count, n);
            CheckListLen(sb, ref problems, "instChoiceEffectDone", d.instChoiceEffectDone.Count, n);
            CheckListLen(sb, ref problems, "instChosenIndex", d.instChosenIndex.Count, n);
            CheckListLen(sb, ref problems, "instChosenByDefault", d.instChosenByDefault.Count, n);
            CheckListLen(sb, ref problems, "instBranchDecided", d.instBranchDecided.Count, n);
            CheckListLen(sb, ref problems, "instBranchNext", d.instBranchNext.Count, n);
            CheckListLen(sb, ref problems, "instBranchAt", d.instBranchAt.Count, n);
            CheckListLen(sb, ref problems, "instVisited", d.instVisited.Count, n);
            CheckListLen(sb, ref problems, "instNoticeArgs", d.instNoticeArgs.Count, n);
            CheckListLen(sb, ref problems, "instShownName", d.instShownName.Count, n);
            CheckListLen(sb, ref problems, "instShownDesc", d.instShownDesc.Count, n);

            // 键值对并行表：以 Keys 为基准
            CheckListLen(sb, ref problems, "histTriggeredCounts", d.histTriggeredCounts.Count, d.histTriggeredKeys.Count);
            CheckListLen(sb, ref problems, "lastTriggerTime", d.lastTriggerTime.Count, d.lastTriggerId.Count);
            CheckListLen(sb, ref problems, "cooldownAt", d.cooldownAt.Count, d.cooldownId.Count);
            CheckListLen(sb, ref problems, "branchAtTime", d.branchAtTime.Count, d.branchNextId.Count);
            CheckListLen(sb, ref problems, "branchVisited", d.branchVisited.Count, d.branchNextId.Count);
            CheckListLen(sb, ref problems, "branchWaiting", d.branchWaiting.Count, d.branchNextId.Count);
            CheckListLen(sb, ref problems, "histTemplate", d.histTemplate.Count, d.histInstId.Count);
            CheckListLen(sb, ref problems, "histName", d.histName.Count, d.histInstId.Count);
            CheckListLen(sb, ref problems, "histCycle", d.histCycle.Count, d.histInstId.Count);
            CheckListLen(sb, ref problems, "histOutcome", d.histOutcome.Count, d.histInstId.Count);
            CheckListLen(sb, ref problems, "histChoice", d.histChoice.Count, d.histInstId.Count);
            CheckListLen(sb, ref problems, "histKey", d.histKey.Count, d.histInstId.Count);
            CheckListLen(sb, ref problems, "slotNextDueAt", d.slotNextDueAt.Count, d.slotName.Count);

            if (problems == 0)
            {
                Debug.Log("[MoreFood/Event][存档] 一致性校验通过：实例 " + n + " 个");
                return;
            }

            // ★ 关键：只是警告，不阻止恢复
            Debug.LogWarning("[MoreFood/Event][存档] 有 " + problems
                             + " 处并行表长度不一致 ⇒ 缺失的位置按默认值处理，恢复继续。\n"
                             + sb.ToString());
        }

        private static void CheckListLen(System.Text.StringBuilder sb, ref int problems,
                                         string name, int actual, int expected)
        {
            if (actual == expected) return;
            problems++;
            sb.Append("    · ").Append(name).Append("：实际 ").Append(actual)
              .Append("，期望 ").Append(expected)
              .Append(actual < expected ? "（缺 " : "（多 ")
              .Append(System.Math.Abs(expected - actual)).Append(" 项）\n");
        }

 // 安全索引：越界 / 列表为 null 时返回默认值。
        private static T GetAt<T>(System.Collections.Generic.List<T> list, int idx, T dflt)
        {
            if (list == null || idx < 0 || idx >= list.Count) return dflt;
            return list[idx];
        }

        // 读档 —— 从"严格拒绝"改为"尽力兼容"
        // 当前活跃的事件（一次一个，Doc-V §2.2）
        public static void ReadFromSave(IEventSaveData d)
        {
            if (d == null) return;

 // ：只报告不拒绝
            ReportSaveConsistency(d);

            _presetPromoted.Clear();
            for (int i = 0; i < d.presetPromoted.Count; i++)
            {
                string pid = GetAt(d.presetPromoted, i, null);
                if (!string.IsNullOrEmpty(pid)) _presetPromoted.Add(pid);
            }
            ApplyPresetPromotions();   // 把降级标记重新打到刚加载的 EventDef 上

            EventNarratorSelection.SetFromSave(d.narratorId);
            // 只记 pending；激活与闹钟续订等 Game.OnSpawn 之后由 Director.Resume 消费
            EventNarratorDirector.RestoreFrom(d.narratorId, d.narrNextDueAt,
                                              d.narrI0, d.narrW, d.narrR,
                                              d.slotName, d.slotNextDueAt);

            int dropped = 0;

            // 1) 活跃实例（以 instId 为主键，主键缺就真的没法恢复）
            for (int i = 0; i < d.instId.Count; i++)
            {
                string iid = GetAt(d.instId, i, null);
                string tid = GetAt(d.instTemplate, i, null);

                if (string.IsNullOrEmpty(iid))
                {
                    dropped++;
                    Debug.Log("[MoreFood/Event][读档兼容] 第 " + i + " 项 instId 为空 → 跳过");
                    continue;
                }

                var def = EventRegistry.Get(tid);
                if (def == null)
                {
                    dropped++;
                    Debug.Log("[MoreFood/Event][读档兼容] 丢弃实例 '" + (tid ?? "<null>")
                              + "'（模板不存在：Mod 卸载或 yaml 改名）");
                    continue;
                }

                var inst = new EventInstance
                {
                    instanceId = iid,
                    templateId = tid,
                    startCycle = GetAt(d.instStartCycle, i, 0),
                    remainingDuration = GetAt(d.instRemaining, i, 0f),
                    state = EventInstanceState.Pending,
                    def = def,
                };

                // 枚举字段：安全转换，越界或非法值 → 默认值
                int stateRaw = GetAt(d.instState, i, (int)EventInstanceState.Pending);
                inst.state = System.Enum.IsDefined(typeof(EventInstanceState), stateRaw)
                             ? (EventInstanceState)stateRaw
                             : EventInstanceState.Pending;

                inst.hasDuration = def.duration != null && def.duration.max > 0f;

                // 驻留计时
                float residentSecs = GetAt(d.instResident, i, def.GetResidentSeconds());
                inst.hasResident = true;
                inst.residentRemaining = residentSecs;

                float nowSec = GetGameTime();
                inst.triggerEffectEndsAt = nowSec + (inst.remainingDuration > 0f ? inst.remainingDuration : 0f);
                inst.residentEndsAt = nowSec + (residentSecs > 0f ? residentSecs : 0f);

                // 决策槽
                inst.choiceEffectRemaining = GetAt(d.instChoiceEffect, i, 0f);
                inst.choiceEffectEndsAt = nowSec + (inst.choiceEffectRemaining > 0f ? inst.choiceEffectRemaining : 0f);
                inst.hasChoiceEffect = inst.choiceEffectRemaining > 0f;
                inst.choiceEffectDone = GetAt(d.instChoiceEffectDone, i, true);
                inst.effectAnyStarted = inst.hasDuration || inst.hasChoiceEffect;

                // 决策结果 + 分支
                inst.chosenIndex = GetAt(d.instChosenIndex, i, -1);
                inst.chosenByDefault = GetAt(d.instChosenByDefault, i, false);
                inst.pendingBranchDecided = GetAt(d.instBranchDecided, i, false);
                inst.pendingBranchNext = GetAt(d.instBranchNext, i, "") ?? "";
                inst.pendingBranchAtTime = GetAt(d.instBranchAt, i, 0f);

                if (inst.participantIds == null) inst.participantIds = new System.Collections.Generic.List<string>();
                inst.participantIds.Clear();
                string visitedRaw = GetAt(d.instVisited, i, null);
                if (!string.IsNullOrEmpty(visitedRaw))
                    inst.participantIds.AddRange(visitedRaw.Split('|'));

                // 通知占位参数：必须在下面重推状态行之前恢复，否则 tooltip 里的 {pct} 等内容会丢
                LoadNoticeArgs(inst, GetAt(d.instNoticeArgs, i, null));

                // 冻结文案：读档后必须还是存档时那一组（不重抽），否则同一任务读到一半会改名
                inst.shownName = GetAt(d.instShownName, i, null);
                inst.shownDescription = GetAt(d.instShownDesc, i, null);
                inst.hunt = def.hunt;

                // 选择轴 phase
                int phaseRaw = GetAt(d.instChoicePhase, i, (int)EventChoicePhase.Waiting);
                inst.choicePhase = System.Enum.IsDefined(typeof(EventChoicePhase), phaseRaw)
                                   ? (EventChoicePhase)phaseRaw
                                   : EventChoicePhase.Waiting;

                // 根级效果槽是否已结束
                inst.triggerEffectDone = GetAt(d.instTriggerEffectDone, i, false);

                if (inst.state == EventInstanceState.Ended)
                {
                    dropped++;
                    continue;
                }

                _active.Add(inst);

 // 修复：读档恢复后重推左侧通知 —— 否则 UI 上一片空白。
                try
                {
                    bool notifyOn = def == null || def.notification == null || def.notification.notify;
                    bool statusOn = def != null && def.notification != null && def.notification.status;

                    if (statusOn && inst.hasDuration && !inst.triggerEffectDone)
                        EventNotifier.PushStatus(def, inst, false, def.notification.status_text,
                                                     def.notification.status_tooltip);

                    if (inst.choicePhase != EventChoicePhase.Chosen)
                    {
                        if (notifyOn) EventNotifier.Push(def, inst);
                    }
                    else if (statusOn && inst.hasDuration && inst.hasChoiceEffect && !inst.choiceEffectDone)
                    {
 // ：读档时选项级状态行也要按同一语义重建
                        var chosen = ChosenOption(inst, def);
                        if (chosen != null && chosen.status)
                            EventNotifier.PushStatus(def, inst, true, chosen.status_text,
                                                     chosen.status_tooltip);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][读档兼容] 重推通知失败（不影响事件本体）: " + e);
                }

                // ★ 读档重接：事件还在跑，但它起的【持续类】音频（循环 / 曲目 / 快照）不会自己回来
                try
                {
                    int again = 0;
                    if (inst.hasDuration && !inst.triggerEffectDone)
                        again += EventMediaPlayer.ReplayPersistent(def, inst);

                    if (inst.choicePhase == EventChoicePhase.Chosen
                        && inst.hasChoiceEffect && !inst.choiceEffectDone)
                        again += EventMediaPlayer.ReplayPersistentChoice(def, inst);

                    if (again > 0)
                        Debug.Log("[MoreFood/Event][读档兼容] 重接持续类音频 " + again + " 条："
                                  + inst.instanceId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][读档兼容] 重接音频失败（不影响事件本体）: " + e);
                }

                Debug.Log("[MoreFood/Event][读档兼容] 恢复实例 '" + inst.instanceId
                          + "' 剩余=" + inst.GetDurationRemaining().ToString("F1") + "s");
            }

            // 2) 上次触发时刻
            for (int i = 0; i < d.lastTriggerId.Count; i++)
            {
                string id = GetAt(d.lastTriggerId, i, null);
                if (string.IsNullOrEmpty(id) || !EventRegistry.Has(id)) { dropped++; continue; }
                _lastTriggerTime[id] = GetAt(d.lastTriggerTime, i, -1f);
            }

            // 2b) 冷却结束时刻
            for (int i = 0; i < d.cooldownId.Count; i++)
            {
                string id = GetAt(d.cooldownId, i, null);
                if (string.IsNullOrEmpty(id)) continue;
                _cooldownUntil[id] = GetAt(d.cooldownAt, i, 0f);
            }

            // 3) 待兑现的分支
            for (int i = 0; i < d.branchNextId.Count; i++)
            {
                string nid = GetAt(d.branchNextId, i, null);
                if (string.IsNullOrEmpty(nid) || !EventRegistry.Has(nid))
                {
                    dropped++;
                    Debug.Log("[MoreFood/Event][读档兼容] 丢弃分支 → '" + (nid ?? "<null>")
                              + "'（目标不存在）");
                    continue;
                }

                var sb = new ScheduledBranch
                {
                    nextEventId = nid,
                    triggerAtTime = GetAt(d.branchAtTime, i, 0f),
                    visited = new System.Collections.Generic.List<string>(),
                    waitingForMutex = GetAt(d.branchWaiting, i, false),
                };

                string vraw = GetAt(d.branchVisited, i, null);
                if (!string.IsNullOrEmpty(vraw))
                    sb.visited.AddRange(vraw.Split('|'));

                Branches.Add(sb);
            }

            // 4) 事件历史摘要
            _events.Clear();
            _keys.Clear();
            for (int i = 0; i < d.histInstId.Count; i++)
            {
                string iid = GetAt(d.histInstId, i, null);
                if (string.IsNullOrEmpty(iid)) continue;

                var h = new HistoryEntry
                {
                    instanceId = iid,
                    id = GetAt(d.histTemplate, i, null) ?? "",
                    name = GetAt(d.histName, i, null) ?? "",
                    cycle = GetAt(d.histCycle, i, 0),
                    outcome = GetAt(d.histOutcome, i, null) ?? "",
                    choiceIndex = GetAt(d.histChoice, i, -1),
                    isKey = GetAt(d.histKey, i, false),
                    tip = GetAt(d.histTip, i, null) ?? "",
                };
                _events.Add(h);
                if (h.isKey) _keys.Add(h);
            }
            while (_events.Count > EVENT_KEEP) _events.RemoveAt(_events.Count - 1);
            while (_keys.Count > KEY_KEEP) _keys.RemoveAt(_keys.Count - 1);

            // 6) 事件历史（自身已做长度检查，直接调）
            EventHistory.ImportFrom(d);

            Debug.Log("[MoreFood/Event][读档兼容] 恢复完成：活跃=" + _active.Count
                      + " 链式=" + Branches.Count + " 丢弃=" + dropped
                      + " 历史=" + _events.Count + "（关键 " + _keys.Count + "）"
                      + " → 闹钟由 EventNarrator 重排");
        }

        // 小工具
        public static int GetCycle()
        {
            try { return GameClock.Instance != null ? GameClock.Instance.GetCycle() : 0; }
            catch (Exception e) { DistributionLog.ExcOnce("ev:clock-cycle", "[MoreFood/Event] 取周期失败 → 按 0", e); return 0; }
        }

        public static float GetGameTime()
        {
            try { return GameClock.Instance != null ? GameClock.Instance.GetTime() : 0f; }
            catch (Exception e) { DistributionLog.ExcOnce("ev:clock-time", "[MoreFood/Event] 取游戏时间失败 → 按 0", e); return 0f; }
        }

        public static float GetCycleFloat()
        {
            try { return GameClock.Instance != null ? GameClock.Instance.GetTimeInCycles() : 0f; }
            catch (Exception e) { DistributionLog.ExcOnce("ev:clock-cycles", "[MoreFood/Event] 取周期小数失败 → 按 0", e); return 0f; }
        }

        public static void Reset()
        {
            _inited = false;
            _active.Clear();
            _lastTriggerTime.Clear();
            _cooldownUntil.Clear();
            _presetPromoted.Clear();
            CancelAllInstanceAlarms();
            Branches.Clear();
            _firedThisFrame.Clear();
            EventAudioTrigger.Shutdown();
            EventMediaPlayer.Clear();
        }
    }
}
