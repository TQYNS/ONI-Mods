// DrinkDesireMonitor.cs — 饮水系统 · 触发层（含性能埋点）

using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    public class DrinkDesireMonitor : KMonoBehaviour, ISim1000ms
    {
        // 水位低于此值 ⇒ 挂「脱水」debuff
        public const float DEHYDRATED_AT = 10f;
        // chore 下发后 5 秒内没被驱动 ⇒ 取消（防卡死）
        public const float UNDRIVEN_TIMEOUT = 5f;
        // chore 存活超过 300 秒 ⇒ 强制取消并打诊断（看门狗）
        public const float WATCHDOG_TIMEOUT = 300f;

        // 本小人当前下发的饮水 chore（null = 无）
        private Chore _chore;
        // chore 是否已进入收尾（防重复收尾）
        private bool _choreEnded;
        // 货源轮转序号：每次选目标自增，避免总挑同一货源
        private int _rotation;
        // 已预约的目标实体（收尾时要把预约释放回去）
        private GameObject _reservedTarget;
        // 当前挂着的惩罚 buff id（脱水），变化时先摘旧的
        private string _appliedPenalty;
        // 是否已注册进 Sim1000ms（防重复注册）
        private bool _simRegistered;
        // 正在卸载/清理中，回调据此跳过后续逻辑
        private bool _isCleaningUp;
        // chore 下发时刻（Time.time），看门狗用它算存活时长
        private float _choreStartTime;
        // 未被驱动的累计秒数（到 UNDRIVEN_TIMEOUT 就取消）
        private float _undrivenSecs;
        // 是否曾被驱动过（驱动过就不再累计 undriven）
        private bool _wasDriven;

        // Unity 回调：prefab 初始化
        protected override void OnPrefabInit()
        {
            try
            {
            base.OnPrefabInit();
            this.autoRegisterSimRender = false;
            }
            catch (System.Exception e) { MFLog.HotFail("prefab:DrinkDesireMonitor", e); }
        }

        // Unity 回调：实例生成
        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            if (_simRegistered) return;
            _simRegistered = true;
            SimAndRenderScheduler.instance.Add(this, false);
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:DrinkDesireMonitor", e); }
        }

        // Unity 回调：清理
        protected override void OnCleanUp()
        {
            try
            {
            _isCleaningUp = true;

            if (this._chore != null && !this._chore.isComplete)
            {
                try { this._chore.Cancel("monitor cleanup"); }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] Cancel 异常（已忽略）");
                }
            }
            this._chore = null;
            this._choreEnded = false;
            this._choreStartTime = 0f;
            this._undrivenSecs = 0f;
            this._wasDriven = false;
            this.ReleaseReservation();

            if (_simRegistered)
            {
                SimAndRenderScheduler.instance.Remove(this);
                _simRegistered = false;
            }
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:DrinkDesireMonitor", e); }
        }

        // 每秒驱动：按当前水位决定挂「脱水」buff，并推进饮水欲望状态
        public void Sim1000ms(float dt)
        {
            // ★ 性能修复：移除每复制人每秒 2× GC.GetTotalMemory 性能探针（正式版无必要）
            try
            {
                this.Sim1000ms_Internal(dt);
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] Sim1000ms 顶层异常（已吞掉，monitor 保活）: " + e);
            }
        }

        private void Sim1000ms_Internal(float dt)
        {
            if (!DrinkDatabase.Ready) return;
            if (DrinkDatabase.IsBionic(this.gameObject)) return;

            var inst = DrinkDatabase.Hydration.Lookup(this.gameObject);
            if (inst == null) return;

            // 惰性清理排除表（时间到自动失效）；失败只报一次，避免每秒刷屏
            try { DrinkExclusionRegistry.Cleanup(); }
            catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.ExclusionCleanup", e); }

            try { this.RefreshPenalty(inst.value); }
            catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.RefreshPenalty", e); }

            if (this._choreEnded)
            {
                try { this.ReleaseReservation(); }
                catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.ReleaseReservation", e); }
                this.ResetChoreState();
            }

            if (this._chore != null && !this._chore.isComplete)
            {
                bool valid;
                try { valid = IsChoreTargetValid(this._chore); }
                catch (Exception e)
                {
                    MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.IsChoreTargetValid", e);
                    valid = false;
                }

                if (!valid)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] " + this.gameObject.name
                                     + " 的 chore 目标已失效（拆除/销毁？），主动 Cancel");
                    this.CancelCurrentChore("chore 目标失效");
                }
            }

            if (this._chore != null && !this._chore.isComplete)
            {
                bool hasDriver = false;
                try { hasDriver = this._chore.driver != null; }
                catch { /* expected: chore 已失效时 driver 读取失败，按未驱动继续走超时重建 */ }

                if (hasDriver)
                {
                    this._wasDriven = true;
                    this._undrivenSecs = 0f;
                }
                else if (!this._wasDriven)
                {
                    this._undrivenSecs += dt;
                    if (this._undrivenSecs >= UNDRIVEN_TIMEOUT)
                    {
                        MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] " + this.gameObject.name
                                         + " 的 chore 建了 " + this._undrivenSecs.ToString("F1")
                                         + "s 仍未被 driver 选中（前置条件不过？），Cancel 重建");
                        this.CancelCurrentChore("未驱动超时 " + UNDRIVEN_TIMEOUT + "s");
                        this._rotation++;
                    }
                }
            }

            if (this._chore != null && this._choreStartTime > 0f)
            {
                float aliveSecs = Time.time - this._choreStartTime;
                if (aliveSecs > WATCHDOG_TIMEOUT)
                {
                    try { DumpChoreDiag(this._chore, aliveSecs); }
                    catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.DumpChoreDiag", e); }

                    this.CancelCurrentChore("watchdog: 存活超过 " + WATCHDOG_TIMEOUT + "s");
                }
            }

            if (inst.value >= DrinkSystem.TRIGGER_AT)
            {
                this.CancelCurrentChore("水位已回升，取消饮水");
                return;
            }

            if (this._chore != null && !this._chore.isComplete && this._reservedTarget != null)
            {
                try
                {
                    var kp = this._reservedTarget.GetComponent<KPrefabID>();
                    if (kp != null && Drinks.FindItem(kp.PrefabTag) != null
                        && !DrinkRestriction.IsUsableBy(this.gameObject, kp.PrefabTag.ToString()))
                    {
                        MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][Restrict] " + this.gameObject.name
                                  + " 的饮水目标 '" + kp.PrefabTag + "' 已被禁 → 取消 chore（回落到其它货源）");
                        this.CancelCurrentChore("目标已被饮品管制禁止");
                    }
                }
                catch (Exception e)
                {
                    MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.RestrictionRecheck", e);
                }
            }

            if (this._chore == null)
            {
                try { this.TryDispatch(inst); }
                catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.Sim1000ms.TryDispatch", e); }
            }
        }

        // 目标可达性查询
        private static bool IsApproachTargetReachable(GameObject duplicant, DrinkTarget target)
        {
            if (duplicant == null || target == null) return true;
            if (target.approachTarget == null) return true;

            var approachable = target.approachTarget as IApproachable;
            if (approachable == null) return true;

            var navigator = duplicant.GetComponent<Navigator>();
            if (navigator == null) return true;

            try
            {
                return navigator.GetNavigationCost(approachable) != -1;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] IsApproachTargetReachable 异常（放行）");
                return true;
            }
        }

        private void TryDispatch(AmountInstance inst)
        {
            float t0 = Time.realtimeSinceStartup;
            DrinkTarget target = DrinkSourceRegistry.FindBest(this.gameObject, this._rotation);
            float t1 = Time.realtimeSinceStartup;
            if (target == null) return;

            float t2 = t1, t3 = t1;
            if (target is ItemDrinkTarget idt && idt.entry != null)
            {
                if (!DrinkWorldRegistry.TryReserve(idt.go, this.gameObject))
                {
                    this._rotation++;
                    return;
                }
                this._reservedTarget = idt.go;
                t2 = Time.realtimeSinceStartup;
            }

            DrinkChore chore = DrinkSystem.MakeChore(this.gameObject, target);
            t3 = Time.realtimeSinceStartup;
            if (chore == null)
            {
                this.ReleaseReservation();
                return;
            }

            // 构造后立刻查目标是否可达
            if (!IsApproachTargetReachable(this.gameObject, target))
            {
                string targetName = (target.go != null) ? target.go.name : "<null>";
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink] " + this.gameObject.name
                                 + " 的目标 " + targetName + " 当前不可达（货源="
                                 + target.sourceName + "）→ 立刻 Cancel 并降级");
                DrinkExclusionRegistry.ExcludeFor(target.go,
                    DrinkExclusionRegistry.DEFAULT_BACKOFF_SECONDS);

                try { chore.Cancel("target unreachable at dispatch"); }
                catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] Cancel 异常"); }
                try { chore.ReleaseSlot(); }
                catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] ReleaseSlot 异常"); }

                this.ReleaseReservation();
                this.ResetChoreState();
                this._rotation++;
                return;
            }

            chore.onExit = (Action<Chore>)Delegate.Combine(
                chore.onExit,
                new Action<Chore>(this.OnMyChoreExit));

            this._chore = chore;
            this._choreStartTime = Time.time;
            this._undrivenSecs = 0f;
            this._wasDriven = false;

            float totalMs = (Time.realtimeSinceStartup - t0) * 1000f;
            float findMs = (t1 - t0) * 1000f;
            float reserveMs = (t2 - t1) * 1000f;
            float makeMs = (t3 - t2) * 1000f;

            if (totalMs > 3f)
            {
                MFLog.Verbose(LogCat.Drink,string.Format(
                    "[MoreFood/Perf] TryDispatch: total={0:F2}ms find={1:F2} reserve={2:F2} make={3:F2}",
                    totalMs, findMs, reserveMs, makeMs));
            }

            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] " + this.gameObject.name + " 想喝水（水位 "
                      + inst.value.ToString("F1") + "），货源=" + target.sourceName);
        }

        private void OnMyChoreExit(Chore chore)
        {
            if (this._isCleaningUp) return;

            this._choreEnded = true;

            var drinkChore = chore as DrinkChore;
            if (drinkChore == null) return;

            var smi = drinkChore.smi;
            if (smi == null || !smi.IsRunning()) return;

            try
            {
                smi.StopSM("DrinkDesireMonitor.OnChoreExit");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] SMI StopSM 异常（已忽略）");
            }
        }

        // 统一收口：Cancel 后立刻清状态，不能依赖 onExit（未 Begin 的 chore 的 End 在 driver==null 提前返回，onExit 不触发）。
        private void CancelCurrentChore(string reason)
        {
            if (this._chore != null && !this._chore.isComplete)
            {
                var dc = this._chore as DrinkChore;
                if (dc != null)
                {
                    try { dc.ReleaseSlot(); }
                    catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.ReleaseSlot", e); }
                }
                try { this._chore.Cancel(reason); }
                catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.CancelCurrentChore", e); }
            }
            try { this.ReleaseReservation(); }
            catch (Exception e) { MFLog.HotFail("drink:DrinkDesireMonitor.ReleaseReservation", e); }
            this.ResetChoreState();
        }

        private void ResetChoreState()
        {
            this._chore = null;
            this._choreStartTime = 0f;
            this._undrivenSecs = 0f;
            this._wasDriven = false;
            this._choreEnded = false;
        }

        private void ReleaseReservation()
        {
            if (this._reservedTarget != null)
            {
                DrinkWorldRegistry.Release(this._reservedTarget, this.gameObject);
                this._reservedTarget = null;
            }
        }

        private bool IsChoreTargetValid(Chore chore)
        {
            var dc = chore as DrinkChore;
            if (dc == null) return true;

            var target = dc.drinkTarget;
            if (target == null) return false;
            if (target.go == null) return false;

            var bdt = target as BuildingDrinkTarget;
            if (bdt != null && bdt.cooler == null) return false;


            return true;
        }

        private void DumpChoreDiag(Chore chore, float aliveSecs)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("[MoreFood/Drink/Watchdog] chore 存活超过 ")
                  .Append(WATCHDOG_TIMEOUT).Append("s（实际 ")
                  .Append(aliveSecs.ToString("F1")).Append("s），强制清理\n");
                sb.Append("  minion=").Append(SafeName(this.gameObject)).Append("\n");

                try { sb.Append("  chore.isComplete=").Append(chore.isComplete).Append("\n"); } catch { /* expected: 诊断字段读取失败时跳过该项 */ }

                string driverName = "<读取失败>";
                try
                {
                    var drv = chore.driver;
                    if (drv == null) driverName = "<null>";
                    else driverName = SafeName(drv.gameObject);
                }
                catch { /* expected: driver 读取失败保留“<读取失败>”占位 */ }
                sb.Append("  chore.driver=").Append(driverName).Append("\n");

                try
                {
                    sb.Append("  chore.provider=")
                      .Append(chore.provider != null ? "非空" : "<null>")
                      .Append("\n");
                }
                catch { /* expected: 诊断字段读取失败时跳过该项 */ }

                try { sb.Append("  chore.gameObject=").Append(SafeName(chore.gameObject)).Append("\n"); }
                catch { /* expected: 诊断字段读取失败时跳过该项 */ }

                var dc = chore as DrinkChore;
                if (dc != null)
                {
                    sb.Append("  isBuilding=").Append(dc.isBuildingSource).Append("\n");
                    sb.Append("  assignedSlotIndex=").Append(dc.assignedSlotIndex).Append("\n");

                    var t = dc.drinkTarget;
                    if (t != null)
                    {
                        sb.Append("  target.sourceName=").Append(t.sourceName).Append("\n");
                        sb.Append("  target.go=").Append(SafeName(t.go)).Append("\n");

                        var bdt = t as BuildingDrinkTarget;
                        if (bdt != null)
                        {
                            sb.Append("  bdt.cooler=")
                              .Append(SafeName(bdt.cooler != null ? bdt.cooler.gameObject : null))
                              .Append("\n");
                            sb.Append("  bdt.wantedKg=").Append(bdt.wantedKg.ToString("F3")).Append("\n");
                        }
                    }

                    sb.Append("  smi.currentState=").Append(SafeGetSmiState(dc)).Append("\n");
                }

                try
                {
                    var inst = DrinkDatabase.Hydration.Lookup(this.gameObject);
                    if (inst != null)
                        sb.Append("  hydration=").Append(inst.value.ToString("F1")).Append("\n");
                }
                catch { /* expected: 补水数据不可得时跳过该诊断项 */ }

                MFLog.Warn(LogCat.Drink,sb.ToString());
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink/Watchdog] DumpChoreDiag 异常: " + e);
            }
        }

        private static string SafeName(GameObject go)
        {
            try
            {
                if (go == null) return "<null>";
                return go.name;
            }
            catch { return "<读取失败>"; }
        }

        private static string SafeGetSmiState(DrinkChore dc)
        {
            try
            {
                var smi = dc.smi;
                if (smi == null) return "<null>";

                bool running = false;
                try { running = smi.IsRunning(); }
                catch { return "<IsRunning 读取失败>"; }
                if (!running) return "<已停止>";

                const BindingFlags F = BindingFlags.Instance
                                     | BindingFlags.Public
                                     | BindingFlags.NonPublic;

                object cur = null;
                var t = smi.GetType();

                string[] fieldNames = { "currentState", "CurrentState", "sm" };
                foreach (var fn in fieldNames)
                {
                    try
                    {
                        var f = t.GetField(fn, F);
                        if (f != null) { cur = f.GetValue(smi); if (cur != null) break; }
                    }
                    catch { /* expected: 反射探测失败继续尝试下一候选字段 */ }
                }
                if (cur == null)
                {
                    string[] propNames = { "currentState", "CurrentState" };
                    foreach (var pn in propNames)
                    {
                        try
                        {
                            var p = t.GetProperty(pn, F);
                            if (p != null && p.CanRead) { cur = p.GetValue(smi); if (cur != null) break; }
                        }
                        catch { /* expected: 反射探测失败继续尝试下一候选属性 */ }
                    }
                }
                if (cur == null) return "<字段/属性均未命中>";

                try
                {
                    var nt = cur.GetType();
                    var nf = nt.GetField("name", F);
                    if (nf != null)
                    {
                        var v = nf.GetValue(cur);
                        return v != null ? v.ToString() : "<unnamed>";
                    }
                }
                catch { /* expected: 反射取 name 失败时回退到 ToString */ }

                return cur.ToString();
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Drink, e, "[MoreFood/Drink] SafeGetSmiState 反射异常");
                return "<反射异常:" + e.GetType().Name + ">";
            }
        }

        private void RefreshPenalty(float value)
        {
            string want = value < DEHYDRATED_AT ? Buffs.DehydratedId : null;

            if (want == this._appliedPenalty) return;

            var effects = this.GetComponent<Effects>();
            if (effects == null) return;

            if (!string.IsNullOrEmpty(this._appliedPenalty) && effects.HasEffect(this._appliedPenalty))
                effects.Remove(this._appliedPenalty);

            this._appliedPenalty = want;

            if (string.IsNullOrEmpty(want)) return;
            if (Db.Get() == null || Db.Get().effects == null || !Db.Get().effects.Exists(want)) return;
            if (effects.HasEffect(want)) return;

            effects.Add(want, false);
            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink] " + this.gameObject.name + " 进入「"
                      + BuffConfig.Get(want).name + "」状态（水位 " + value.ToString("F1") + "）");
        }
    }

    [HarmonyPatch(typeof(MinionIdentity), "OnSpawn")]
    public static class DrinkDesireMonitor_Mount_Patch
    {
        // 补丁：小人生成后挂 DrinkDesireMonitor（仿生人另走 DetachFrom 饮水）
        public static void Postfix(MinionIdentity __instance)
        {
            try
            {
            if (__instance == null) return;
            if (DrinkDatabase.IsBionic(__instance.gameObject)) return;
            __instance.gameObject.AddOrGet<DrinkDesireMonitor>();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkDesireMonitor", e); }
        }
    }
}
