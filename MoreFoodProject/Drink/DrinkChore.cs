// DrinkChore.cs — 饮水系统 · 执行层（含性能埋点）

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Klei;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{

    // 【已废弃，仅记录，不删除】手压泵（LiquidPumpingStation）链路已移除，实现与踩坑记录见 文档整理/饮品.md §5.10.1/§6.3/§12.4/§16。

    public static class WaterCoolerReservation
    {
        // 饮水机同时最多服务 2 个小人（对应两格站位）
        public const int MAX_SLOTS = 2;

        private static readonly Dictionary<WaterCooler, bool[]> _slots
            = new Dictionary<WaterCooler, bool[]>();

        private static readonly Dictionary<WaterCooler, List<DrinkChore>> _chores
            = new Dictionary<WaterCooler, List<DrinkChore>>();

        // 给饮水机找一个空闲站位并占用；成功时给出 slotIndex，全满返回 false
        public static bool TryOccupy(WaterCooler cooler, out int slotIndex)
        {
            slotIndex = -1;
            if (cooler == null) return false;
            try
            {
                bool[] slots;
                if (!_slots.TryGetValue(cooler, out slots))
                {
                    slots = new bool[MAX_SLOTS];
                    _slots[cooler] = slots;
                }
                for (int i = 0; i < slots.Length; i++)
                    if (!slots[i]) { slots[i] = true; slotIndex = i; return true; }
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] TryOccupy 异常");
                return false;
            }
        }

        // 释放饮水机的某个站位
        public static void Release(WaterCooler cooler, int slotIndex)
        {
            if (cooler == null || slotIndex < 0) return;
            try
            {
                bool[] slots;
                if (!_slots.TryGetValue(cooler, out slots)) return;
                if (slotIndex < slots.Length) slots[slotIndex] = false;
            }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] Release 异常"); }
        }

        // 清空饮水机的全部站位占用（拆建筑 / 读档自愈用）
        public static void ForceRemove(WaterCooler cooler)
        {
            if (cooler == null) return;
            try { _slots.Remove(cooler); } catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] ForceRemove(_slots) 异常"); }
            try { _chores.Remove(cooler); } catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] ForceRemove(_chores) 异常"); }
        }

        public static void Clear()
        {
            try { _slots.Clear(); } catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] Clear(_slots) 异常"); }
            try { _chores.Clear(); } catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] Clear(_chores) 异常"); }
        }

        // 登记饮水机正在跑的 chore（拆建筑时要能一并取消）
        public static void TrackChore(WaterCooler cooler, DrinkChore chore)
        {
            if (cooler == null || chore == null) return;
            try
            {
                List<DrinkChore> list;
                if (!_chores.TryGetValue(cooler, out list))
                {
                    list = new List<DrinkChore>();
                    _chores[cooler] = list;
                }
                if (!list.Contains(chore)) list.Add(chore);
            }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] TrackChore 异常"); }
        }

        // 注销饮水机的 chore 登记（chore 正常结束时调）
        public static void UntrackChore(WaterCooler cooler, DrinkChore chore)
        {
            if (cooler == null || chore == null) return;
            try
            {
                List<DrinkChore> list;
                if (!_chores.TryGetValue(cooler, out list)) return;
                list.Remove(chore);
                if (list.Count == 0) _chores.Remove(cooler);
            }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] UntrackChore 异常"); }
        }

        // 取消该饮水机上登记的全部 chore，并记下原因（日志用）
        public static void CancelAllChores(WaterCooler cooler, string reason)
        {
            if (cooler == null) return;
            try
            {
                List<DrinkChore> list;
                if (!_chores.TryGetValue(cooler, out list)) return;

                var snapshot = new List<DrinkChore>(list);
                _chores.Remove(cooler);

                float t_enter = Time.realtimeSinceStartup;
                long m_enter = GC.GetTotalMemory(false);

                int cancelled = 0;
                for (int i = 0; i < snapshot.Count; i++)
                {
                    var chore = snapshot[i];
                    if (chore == null || chore.isComplete) continue;

                    float t_c0 = Time.realtimeSinceStartup;
                    long m_c0 = GC.GetTotalMemory(false);

                    try
                    {
                        chore.Cancel(reason);
                        cancelled++;
                    }
                    catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] CancelAllChores.Cancel 异常"); }

                    float t_c1 = Time.realtimeSinceStartup;
                    long m_c1 = GC.GetTotalMemory(false);

                    MFLog.Verbose(LogCat.Drink,string.Format(
                        "[MoreFood/Perf]   chore[{0}] Cancel: {1:F2}ms, {2}KB",
                        i, (t_c1 - t_c0) * 1000f, (m_c1 - m_c0) / 1024));
                }

                float t_exit = Time.realtimeSinceStartup;
                long m_exit = GC.GetTotalMemory(false);

                MFLog.Verbose(LogCat.Drink,string.Format(
                    "[MoreFood/Perf] CancelAllChores 总计: {0:F2}ms, {1}KB, {2} 个 chore（{3}）",
                    (t_exit - t_enter) * 1000f, (m_exit - m_enter) / 1024, cancelled, reason));
            }
            catch (Exception e) { MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] CancelAllChores 异常: " + e); }
        }
    }

    public static class DrinkSystem
    {
        // chore 类型 id（注册进 ChoreTypes 的键名）
        public const string CHORE_TYPE_ID = "DrinkDesire";
        // 水位低于 50 才发起主动饮水谋划
        public const float TRIGGER_AT = 50f;
        // 1kg 饮水换算的补水值
        public const float HYDRATION_PER_KG = 50f;
        // 抢断优先级：压倒常规家务去喝水
        public const int INTERRUPT_PRIORITY = 97500;

        // 本系统的 chore 类型（Init 时注册）
        public static ChoreType ChoreType;
        // 前置条件：水位偏低（够格发起饮水）
        public static Chore.Precondition HydrationIsLow;

        public static void Init()
        {
            RegisterChoreType();
            RegisterPrecondition();
        }

        private static void RegisterChoreType()
        {
            if (Db.Get() == null || Db.Get().ChoreTypes == null) return;
            if (Db.Get().ChoreTypes.Exists(CHORE_TYPE_ID))
            {
                ChoreType = Db.Get().ChoreTypes.Get(CHORE_TYPE_ID);
                return;
            }
            string[] groups = new string[0];
            var ct = new ChoreType(
                CHORE_TYPE_ID, Db.Get().ChoreTypes, groups,
                null, "饮水", "饮水", "该喝点水了",
                new Tag[0], 7500, 7500);
            SetInterruptPriority(ct, INTERRUPT_PRIORITY);
            if (!Db.Get().ChoreTypes.Exists(CHORE_TYPE_ID)) Db.Get().ChoreTypes.Add(ct);
            ChoreType = ct;
            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] ChoreType 'DrinkDesire' 已注册（interruptPriority=97500）");
        }

        private static void SetInterruptPriority(ChoreType ct, int value)
        {
            try
            {
                var prop = typeof(ChoreType).GetProperty("interruptPriority",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite) { prop.SetValue(ct, value); return; }
                var field = typeof(ChoreType).GetField("<interruptPriority>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) { field.SetValue(ct, value); return; }
            }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] 覆写 interruptPriority 失败"); }
        }

        private static void RegisterPrecondition()
        {
            Chore.Precondition p = default(Chore.Precondition);
            p.id = "HydrationIsLow";
            p.description = "需要饮水";
            p.sortOrder = 0;
            p.canExecuteOnAnyThread = false;
            p.fn = delegate (ref Chore.Precondition.Context context, object data)
            {
                if (!DrinkDatabase.Ready) return false;
                if (context.consumerState == null) return false;
                var inst = DrinkDatabase.Hydration.Lookup(context.consumerState.gameObject);
                return inst != null && inst.value < TRIGGER_AT;
            };
            HydrationIsLow = p;
        }

        // 为指定饮水目标创建 DrinkChore 实例（绑定复制人与目标）
        public static DrinkChore MakeChore(GameObject duplicant, DrinkTarget target)
        {
            if (duplicant == null || target == null) return null;

            // 建筑路径
            if (target is BuildingDrinkTarget bdt)
            {
                var cooler = bdt.cooler;
                if (cooler == null) return null;
                if (target.go == null) return null;

                var smc = target.go.GetComponent<StateMachineComponent>();
                if (smc == null) return null;

                int slotIndex;
                if (!WaterCoolerReservation.TryOccupy(cooler, out slotIndex)) return null;

                DrinkChore chore;
                float t0 = Time.realtimeSinceStartup;
                long m0 = GC.GetTotalMemory(false);
                try
                {
                    chore = new DrinkChore(smc, target, true);
                    chore.assignedSlotIndex = slotIndex;
                    WaterCoolerReservation.TrackChore(cooler, chore);
                }
                catch (Exception e)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] 创建 DrinkChore 失败: " + e);
                    WaterCoolerReservation.Release(cooler, slotIndex);
                    return null;
                }
                float t1 = Time.realtimeSinceStartup;
                long m1 = GC.GetTotalMemory(false);
                MFLog.Verbose(LogCat.Drink,string.Format(
                    "[MoreFood/Perf] MakeChore(building): {0:F2}ms, {1}KB (slot={2})",
                    (t1 - t0) * 1000f, (m1 - m0) / 1024, slotIndex));

                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] 饮水机 slot " + slotIndex + " 占用 —— " + duplicant.name);
                return chore;
            }

            // 物品路径
            var minion = duplicant.GetComponent<MinionIdentity>();
            if (minion == null) return null;

            DrinkChore itemChore;
            float ti0 = Time.realtimeSinceStartup;
            long mi0 = GC.GetTotalMemory(false);
            try
            {
                itemChore = new DrinkChore(minion, target, false);
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] 创建 DrinkChore 失败: " + e);
                return null;
            }
            float ti1 = Time.realtimeSinceStartup;
            long mi1 = GC.GetTotalMemory(false);
            MFLog.Verbose(LogCat.Drink,string.Format(
                "[MoreFood/Perf] MakeChore(item): {0:F2}ms, {1}KB",
                (ti1 - ti0) * 1000f, (mi1 - mi0) / 1024));
            return itemChore;
        }
    }

    public class DrinkChore : Chore<DrinkChore.StatesInstance>
    {
        // 本次要喝的目标（物品 / 建筑）
        public DrinkTarget drinkTarget;
        // 目标是否为建筑货源（饮水机之类）
        public bool isBuildingSource;

        // 在饮水机上占的站位号（-1 = 未占位）
        public int assignedSlotIndex = -1;
        // 站位是否已释放（Cancel 与 onExit 可能双触发，必须幂等）
        private bool _slotReleased;

        // 基类第 4 个 bool 实为 runUntilComplete；chore_provider 为 null 时原版自动挂 GlobalChoreProvider。
        public DrinkChore(IStateMachineTarget master, DrinkTarget target, bool runUntilComplete)
            : base(DrinkSystem.ChoreType, master,
                   master.GetComponent<ChoreProvider>(),
                   runUntilComplete, null, null, null,
                   PriorityScreen.PriorityClass.personalNeeds, 4,
                   false, true, 0, false,
                   ReportManager.ReportType.PersonalTime)
        {
            this.drinkTarget = target;
            this.isBuildingSource = target is BuildingDrinkTarget;

            base.smi = new DrinkChore.StatesInstance(this);
            base.smi.sm.drinkTarget.Set(target.go, base.smi);

            this.AddPrecondition(ChorePreconditions.instance.IsNotRedAlert, null);
            if (target.approachTarget != null)
                this.AddPrecondition(ChorePreconditions.instance.CanMoveTo, target.approachTarget);
            this.AddPrecondition(DrinkSystem.HydrationIsLow, null);

            this.onExit = (Action<Chore>)Delegate.Combine(
                this.onExit, new Action<Chore>(DrinkChore.OnChoreExitCleanup));

            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink/Dbg] DrinkChore 构造: targetClass=" + target.GetType().Name
                      + " isBuilding=" + this.isBuildingSource
                      + " targetGo=" + (target.go != null ? target.go.name : "null"));
        }

        public IReadOnlyList<CellOffset> GetAssignedOffsets()
        {
            if (assignedSlotIndex < 0) return null;
            var bdt = drinkTarget as BuildingDrinkTarget;
            if (bdt == null || bdt.cooler == null) return null;

            var all = bdt.cooler.GetOffsets();
            if (all == null || assignedSlotIndex >= all.Count) return null;

            return new CellOffset[] { all[assignedSlotIndex] };
        }

        // 幂等释放站位：未 Begin 的 chore 被 Cancel 时原版 End 在 driver==null 提前返回，onExit 不触发，slot 会泄漏。
        public void ReleaseSlot()
        {
            if (this._slotReleased) return;
            this._slotReleased = true;
            if (!this.isBuildingSource) return;

            var bdt = this.drinkTarget as BuildingDrinkTarget;
            if (bdt == null || bdt.cooler == null) return;
            WaterCoolerReservation.UntrackChore(bdt.cooler, this);
            WaterCoolerReservation.Release(bdt.cooler, this.assignedSlotIndex);
            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] 饮水机 slot " + this.assignedSlotIndex + " 已释放");
        }

        private static void OnChoreExitCleanup(Chore chore)
        {
            var dc = chore as DrinkChore;
            if (dc != null) dc.ReleaseSlot();
        }

        // 前置条件执行体：开始时把喝水的人写进状态机，并按当前水位/需求算出这次要喝多少 kg
        public override void Begin(Chore.Precondition.Context context)
        {
            var eater = context.consumerState.gameObject;
            base.smi.sm.drinker.Set(eater, base.smi, false);

            float neededKg = 1f;
            try
            {
                var inst = DrinkDatabase.Hydration.Lookup(eater);
                if (inst != null)
                {
                    float deficit = inst.GetMax() - inst.value;
                    float perKg = 50f;
                    if (drinkTarget.data != null && drinkTarget.data.hydrationPerKg > 0f)
                        perKg = drinkTarget.data.hydrationPerKg;
                    neededKg = deficit / perKg;
                    float maxServe = (drinkTarget.data != null && drinkTarget.data.maxServeMass > 0f)
                                     ? drinkTarget.data.maxServeMass : 2f;
                    neededKg = Mathf.Clamp(neededKg, 0.1f, maxServe);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] 计算 neededKg 异常");
                neededKg = 1f;
            }

            if (isBuildingSource)
            {
                var bdt = drinkTarget as BuildingDrinkTarget;
                if (bdt != null) bdt.wantedKg = neededKg;
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink/Dbg] Begin 建筑路径：wantedKg = "
                          + neededKg.ToString("F3") + " slot=" + assignedSlotIndex);
            }
            else
            {
                base.smi.sm.drinkItemsource.Set(this.drinkTarget.go, base.smi, false);
                base.smi.sm.requestedunits.Set(neededKg, base.smi, false);
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink/Dbg] Begin 非建筑路径：requestedunits = "
                          + neededKg.ToString("F3"));
            }

            base.Begin(context);
        }

        public class States : GameStateMachine<DrinkChore.States, DrinkChore.StatesInstance, DrinkChore>
        {
            // 喝水的那个小人
            public TargetParameter drinker;
            // 本次饮水目标
            public TargetParameter drinkTarget;
            // 物品货源（瓶 / 罐）
            public TargetParameter drinkItemsource;
            // 正在搬运的物品块
            public TargetParameter drinkItemchunk;
            // 计划取用的量
            public FloatParameter requestedunits;
            // 实际取到的量
            public FloatParameter actualunits;

            // 入口：按目标类型选一条子流程
            public State chooseaction;
            // 物品货源：取货子状态机
            public FetchSubState fetch;
            // 在地上直接喝
            public DrinkOnFloorState drinkonfloor;
            // 在建筑（饮水机）处喝
            public DrinkAtBuildingState drinkatbuilding;
            // 成功出口（已结算补水）
            public State success;
            // 失败出口：导航失败 / 目标被抢占必须 ReturnFailure，否则 Monitor 当作喝过了并立刻重建同一目标
            public State failed;

            private static void HideChunk(StatesInstance smi)
            {
                try
                {
                    var chunk = smi.sm.drinkItemchunk.Get(smi);
                    if (chunk == null) return;
                    chunk.SetActive(false);
                }
                catch (Exception e) { MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] HideChunk 异常: " + e); }
            }

            private static void MoveToCooler(StatesInstance smi)
            {
                try
                {
                    var drinkerGo = smi.sm.drinker.Get(smi);
                    if (drinkerGo == null) { smi.GoTo(smi.sm.failed); return; }
                    var navigator = drinkerGo.GetComponent<Navigator>();
                    if (navigator == null) { smi.GoTo(smi.sm.failed); return; }

                    var bdt = smi.master.drinkTarget as BuildingDrinkTarget;
                    if (bdt == null || bdt.cooler == null)
                    {
                        MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] MoveToCooler: cooler 已失效 → failed 兜底");
                        smi.GoTo(smi.sm.failed);
                        return;
                    }

                    var offsets = smi.master.GetAssignedOffsets();
                    if (offsets == null || offsets.Count == 0)
                    {
                        MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] MoveToCooler: 无可用 offsets → failed 兜底");
                        smi.GoTo(smi.sm.failed);
                        return;
                    }

                    // 分配站位不可达时回退到全部站位：CanMoveTo 用多格取最小 cost，硬分配单格会误判失败
                    int coolerCell = Grid.PosToCell(bdt.cooler);
                    if (navigator.GetNavigationCost(coolerCell, offsets) == -1)
                    {
                        var all = bdt.cooler.GetOffsets();
                        if (all != null && all.Count > 0 && navigator.GetNavigationCost(coolerCell, all) != -1)
                        {
                            offsets = all;
                            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] MoveToCooler: 分配站位不可达，回退全部站位");
                        }
                        else
                        {
                            MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] MoveToCooler: 全部站位不可达 → failed 兜底");
                            smi.GoTo(smi.sm.failed);
                            return;
                        }
                    }

                    navigator.GoTo(bdt.cooler, offsets, NavigationTactics.ReduceTravelDistance);
                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink/Dbg] MoveToCooler: slot=" + smi.master.assignedSlotIndex
                              + " offset=(" + offsets[0].x + "," + offsets[0].y + ")");
                }
                catch (Exception e)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] MoveToCooler 异常 → failed 兜底: " + e);
                    try { smi.GoTo(smi.sm.failed); } catch { /* expected: 兜底跳转再失败只能放弃，异常已记录 */ }
                }
            }

            // 修复原版 FetchSubState 内部硬编码的 approach.NavigationFailed → null。
            private static void FixupFetchSubStateApproachFailure(FetchSubState fetch, State failureState)
            {
                if (fetch == null || failureState == null) return;

                var approach = fetch.approach;
                if (approach == null || approach.transitions == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] FixupFetchSubStateApproachFailure: approach / transitions 为 null");
                    return;
                }

                int fixedCount = 0;
                for (int i = 0; i < approach.transitions.Count; i++)
                {
                    var t = approach.transitions[i];
                    if (t == null) continue;
                    // BaseTransition.name 由 EventTransitionData 构造函数传入，就是 evt.ToString()
                    if (t.name == "NavigationFailed" && t.targetState == null)
                    {
                        t.targetState = failureState;
                        fixedCount++;
                    }
                }

                if (fixedCount > 0)
                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] 已修复 FetchSubState.approach.NavigationFailed 目标 → failed（"
                              + fixedCount + " 条）");
                else
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] FixupFetchSubStateApproachFailure: 未找到 "
                                     + "NavigationFailed(targetState==null) 的 transition —— 原版结构可能变了");
            }

            // 定义 chore 状态机：取水 → 走到站位 → 喝下 → 清理离开
            public override void InitializeStates(out BaseState default_state)
            {
                default_state = this.chooseaction;
                base.Target(this.drinker);

                this.chooseaction
                    .Enter("DbgChooseEnter", smi => MFLog.Verbose(LogCat.Drink,
                        "[MoreFood/Drink/Dbg] >>> 进入 chooseaction: isBuilding=" + smi.master.isBuildingSource))
                    .EnterTransition(this.drinkatbuilding, smi => smi.master.isBuildingSource)
                    // 目标在排队期间被其它 fetch（例如餐桌配送）预订完 → 失败终态，避免 ToggleReserve 打 LogError
                    .EnterTransition(this.failed, smi => ItemTargetUnavailable(smi))
                    .EnterTransition(this.fetch, smi => true);

                // fetch 内部把 failure_state 硬编码为 null：先按原样初始化，再由下面的 Fixup 改指向 failed
                this.fetch.InitializeStates(
                    this.drinker, this.drinkItemsource, this.drinkItemchunk,
                    this.requestedunits, this.actualunits,
                    this.drinkonfloor, null);

                // approach 里的 NavigationFailed 同样改指 failed，不能让基类当成喝完了
                FixupFetchSubStateApproachFailure(this.fetch, this.failed);

                this.drinkonfloor.DefaultState(this.drinkonfloor.drink);
                this.drinkonfloor.drink
                    .Enter("DbgDrinkOnFloorEnter", smi => MFLog.Verbose(LogCat.Drink,
                        "[MoreFood/Drink/Dbg] >>> 进入 drinkonfloor.drink"))
                    .Enter("HideChunk", smi => HideChunk(smi))
                    .ToggleAnims(new Func<StatesInstance, HashedString>(GetItemDrinkAnim))
                    .Face(this.drinkItemchunk, 0.5f)
                    .PlayAnim("working_loop")
                    .OnAnimQueueComplete(this.drinkonfloor.post);
                this.drinkonfloor.post
                    .Enter("DrinkItem", smi => TriggerDrink(smi, true))
                    .EnterGoTo(this.success);

                this.drinkatbuilding.DefaultState(this.drinkatbuilding.moveto);
                this.drinkatbuilding.OnTargetLost(this.drinkTarget, this.failed);

                this.drinkatbuilding.moveto
                    .Enter("DbgMoveToEnter", smi => MFLog.Verbose(LogCat.Drink,
                        "[MoreFood/Drink/Dbg] >>> 进入 drinkatbuilding.moveto: slot="
                        + smi.master.assignedSlotIndex))
                    .Enter("MoveToCooler", smi => MoveToCooler(smi))
                    .Exit("StopMoving", smi =>
                    {
                        try
                        {
                            var go = smi.sm.drinker.Get(smi);
                            if (go == null) return;
                            var nav = go.GetComponent<Navigator>();
                            if (nav != null) nav.Stop();
                        }
                        catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] moveto.Exit 异常"); }
                    })
                    .EventTransition(GameHashes.DestinationReached, this.drinkatbuilding.drink)
                    .EventTransition(GameHashes.NavigationFailed, this.failed);

                this.drinkatbuilding.drink.DefaultState(this.drinkatbuilding.drink.pre);
                this.drinkatbuilding.drink.pre
                    .Enter("DbgDrinkPreEnter", smi => MFLog.Verbose(LogCat.Drink,
                        "[MoreFood/Drink/Dbg] >>> 进入 drinkatbuilding.drink.pre"))
                    .ToggleAnims(new Func<StatesInstance, HashedString>(GetBuildingDrinkAnim))
                    .Face(this.drinkTarget, 0.5f)
                    .PlayAnim("working_pre")
                    .QueueAnim("working_loop", false)
                    .OnAnimQueueComplete(this.drinkatbuilding.drink.post);
                this.drinkatbuilding.drink.post
                    .Enter("DrinkBuilding", smi => TriggerDrink(smi, false))
                    .PlayAnim("working_pst")
                    .OnAnimQueueComplete(this.success);

                this.success
                    .Enter("DbgSuccessEnter", smi => MFLog.Verbose(LogCat.Drink,
                        "[MoreFood/Drink/Dbg] >>> 进入 success"))
                    .ReturnSuccess();

                this.failed
                    .Enter("DbgFailedEnter", smi => MFLog.Verbose(LogCat.Drink,
                        "[MoreFood/Drink/Dbg] >>> 进入 failed"))
                    .ReturnFailure();
            }

            private static bool ItemTargetUnavailable(StatesInstance smi)
            {
                DrinkChore chore = smi.master;
                if (chore == null || chore.isBuildingSource) return false;

                DrinkTarget target = chore.drinkTarget;
                if (target == null || target.go == null) return true;

                Pickupable pickupable = target.go.GetComponent<Pickupable>();
                return pickupable != null && pickupable.UnreservedFetchAmount <= 0f;
            }

            private static HashedString GetItemDrinkAnim(StatesInstance smi)
            {
                var workerGo = smi.sm.drinker.Get(smi);
                if (workerGo != null)
                {
                    var id = workerGo.GetComponent<MinionIdentity>();
                    if (id != null && id.model == BionicMinionConfig.MODEL)
                        return "anim_bionic_interacts_sodamaker_kanim";
                }
                return "anim_interacts_sodamaker_kanim";
            }
            private static HashedString GetBuildingDrinkAnim(StatesInstance smi) => "anim_interacts_watercooler_kanim";
            private static void TriggerDrink(StatesInstance smi, bool isItem)
            {
                var workerGo = smi.sm.drinker.Get(smi);
                if (workerGo == null) return;
                var target = smi.master != null ? smi.master.drinkTarget : null;
                if (target == null) return;

                if (isItem)
                {
                    // chunk 丢失时不能凭空补 1kg；Settle 对 mass<=0 直接返回，避免免费补水
                    float mass = 0f;
                    var chunk = smi.sm.drinkItemchunk.Get(smi);
                    if (chunk != null)
                    {
                        var p = chunk.GetComponent<Pickupable>();
                        if (p != null) mass = p.TotalAmount;

                        var storage = workerGo.GetComponent<Storage>();
                        if (storage != null) storage.Remove(chunk, true);

                        chunk.SetActive(true);
                        Util.KDestroyGameObject(chunk);
                    }
                    if (target is ItemDrinkTarget idt) idt.SettleFromMass(workerGo, mass);
                }
                else
                {
                    if (target is BuildingDrinkTarget bdt) bdt.Consume(workerGo);
                }
            }

            public class DrinkOnFloorState : State
            {
                // 喝（地上）
                public State drink;
                // 收尾
                public State post;
            }

            public class DrinkAtBuildingState : State
            {
                // 走到饮水机
                public State moveto;
                // 喝（工作态）
                public DrinkWorkingState drink;
                public class DrinkWorkingState : State
                {
                    // 起手
                    public State pre;
                    // 收尾
                    public State post;
                }
            }
        }

        public class StatesInstance
            : GameStateMachine<DrinkChore.States, DrinkChore.StatesInstance, DrinkChore, object>.GameInstance
        {
            public StatesInstance(DrinkChore master) : base(master) { }
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class DrinkChore_DbInitialize_Patch
    {
        public static void Postfix()
        {
            try
            {
                WaterCoolerReservation.Clear();
                DrinkSystem.Init();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkChore_DbInitialize_Patch", e); }
        }
    }
}
