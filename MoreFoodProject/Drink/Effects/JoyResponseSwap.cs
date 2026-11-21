using System;
using System.Collections.Generic;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    // 临时"借"一次别人的喜悦反应（不动复制人自己的喜悦特质）。
    public static class JoyResponseSwap
    {
        // 本类日志前缀
        private const string LogTag = "[MoreFood/Drink][Joy]";
        // 项目分隔符
        private const string Sep = "|";
        // 气球艺术家特质 id
        private const string BalloonArtistTrait = "BalloonArtist";

        private static void AddBalloonSensor(GameObject go)
        {
            try
            {
                Sensors sensors = go.GetComponent<Sensors>();
                if (sensors == null || HasBalloonSensor(sensors)) return;
                sensors.Add(new BalloonStandCellSensor(sensors));
                MFLog.Info(LogCat.Buff,LogTag + " 已补 BalloonStandCellSensor（原版只在出生时带气球艺术家特质才加）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 补 BalloonStandCellSensor 失败");
            }
        }

        private static void RemoveBalloonSensor(GameObject go)
        {
            try
            {
                Sensors sensors = go.GetComponent<Sensors>();
                if (sensors == null) return;
                for (int i = sensors.sensors.Count - 1; i >= 0; i--)
                {
                    if (sensors.sensors[i] is BalloonStandCellSensor)
                    {
                        sensors.sensors.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 移除 BalloonStandCellSensor 失败");
            }
        }

        private static bool HasBalloonSensor(Sensors sensors)
        {
            for (int i = 0; i < sensors.sensors.Count; i++)
            {
                if (sensors.sensors[i] is BalloonStandCellSensor) return true;
            }
            return false;
        }

        // 保存的原版前置条件（还原用）
        private static ChorePreconditions patchedPreconditions;

        private static void InstallSchedulePrecondition()
        {
            try
            {
                ChorePreconditions cp = ChorePreconditions.instance;
                if (cp == null || patchedPreconditions == cp) return;
                patchedPreconditions = cp;

                Chore.PreconditionFn orig = cp.IsScheduledTime.fn;
                Chore.Precondition cond = cp.IsScheduledTime;
                cond.fn = new Chore.PreconditionFn(delegate(ref Chore.Precondition.Context ctx, object data)
                {
                    try
                    {
                        Chore c = ctx.chore;
                        if (c != null && c.gameObject != null && HasBorrow(c.gameObject))
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        // expected: 借出状态判定失败时按非借出处理，交回原版排班时段条件
                    }
                    return orig != null ? orig(ref ctx, data) : true;
                });
                cp.IsScheduledTime = cond;
                MFLog.Info(LogCat.Buff,LogTag + " 已挂 IsScheduledTime 豁免（只对借出反应的小人生效）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 挂 IsScheduledTime 豁免失败");
            }
        }

        // 该目标当前是否处于「借出喜悦反应」状态
        internal static bool HasBorrow(GameObject go)
        {
            if (go == null) return false;
            DrinkEffectState st = go.GetComponent<DrinkEffectState>();
            return st != null && st.joySwaps != null && st.joySwaps.Count > 0;
        }

        private static void ScheduleDiag(GameObject go)
        {
            try
            {
                GameScheduler.Instance.Schedule("MoreFoodJoyDiag", 3f, delegate(object data)
                {
                    DumpJoyStates(data as GameObject);
                }, go);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 诊断安排失败");
            }
        }

        private static void DumpJoyStates(GameObject go)
        {
            try
            {
                if (go == null) return;
                StateMachineController ctrl = go.GetComponent<StateMachineController>();
                if (ctrl == null) return;
                List<string> parts = new List<string>();
                foreach (StateMachine.Instance smi in ctrl)
                {
                    if (smi == null) continue;
                    bool mon = IsMonitor(smi);
                    Type owner = JoyOwnerOf(smi);
                    if (owner == null && !mon) continue;
                    StateMachine.BaseState st = smi.GetCurrentState();
                    parts.Add((mon ? "Monitor" : owner.Name) + "=" + (st != null ? st.name : "无"));
                }
                MFLog.Info(LogCat.Buff,LogTag + " 3 秒后喜悦状态机：" + string.Join("、", parts.ToArray())
                    + "；摊位位=" + StallCellInfo(go) + "；当前任务=" + ChoreInfo(go));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 喜悦状态机诊断失败");
            }
        }

        private static string StallCellInfo(GameObject go)
        {
            try
            {
                int walk;
                int stand;
                if (JoyStallSpot.TryPick(go, out walk, out stand))
                {
                    return "walk=" + walk + " stand=" + stand;
                }
                return "无可用空地";
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Buff, e, LogTag + " 摊位选点异常");
                return "异常:" + e.GetType().Name;
            }
        }

        private static string ChoreInfo(GameObject go)
        {
            try
            {
                ChoreDriver driver = go.GetComponent<ChoreDriver>();
                if (driver == null) return "无ChoreDriver";
                Chore c = driver.GetCurrentChore();
                if (c == null) return "空闲";
                return c.GetType().Name + "/" + c.choreType.Id;
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Buff, e, LogTag + " 当前任务读取异常");
                return "异常:" + e.GetType().Name;
            }
        }

        // 借出一次喜悦反应：把特质临时换成 targetTrait，并记在 buffId 名下；返回 true = 这次真的借出/触发了
        public static bool Trigger(GameObject go, string buffId, string targetTrait)
        {
            if (go == null || string.IsNullOrEmpty(targetTrait)) return false;
            try
            {
                InstallSchedulePrecondition();
                DrinkEffectState state = go.AddOrGet<DrinkEffectState>();
                EndAll(go, "重新借出");

                Trait target = GetTrait(targetTrait);
                if (target == null || target.OnAddTrait == null)
                {
                    Debug.LogWarning(LogTag + " 特质 " + targetTrait + " 没有 OnAddTrait，无法临时借出喜悦反应（按原生反应触发）");
                    DrinkEffectHelpers.TriggerJoy(go);
                    return true;
                }

                string native = NativeJoyOwner(go);
                if (native == targetTrait)
                {
                    MFLog.Info(LogCat.Buff,LogTag + " 本人就是 " + targetTrait + "，无需借出，直接触发");
                    DrinkEffectHelpers.TriggerJoy(go);
                    return true;
                }

                List<JoyBehaviourMonitor.Instance> before = CollectMonitors(go);
                int stopped = StopJoySmis(go, null);
                if (targetTrait == BalloonArtistTrait) AddBalloonSensor(go);
                target.OnAddTrait.Invoke(go);
                state.AddUnique(state.joySwaps, buffId, targetTrait + Sep + (native ?? ""));
                JoyBehaviourMonitor.Instance mon = NewestMonitor(go, before);
                if (mon != null)
                {
                    mon.GoToOverjoyed();
                }
                else
                {
                    DrinkEffectHelpers.TriggerJoy(go);
                }
                MFLog.Info(LogCat.Buff,LogTag + " 临时借出喜悦反应：" + (string.IsNullOrEmpty(native) ? "（无原生）" : native)
                    + " → " + targetTrait + "（停用原生状态机 " + stopped + " 个，特质表未改动"
                    + (mon != null ? "，动画监视器已换成目标" : "，动画监视器未找到") + "）");
                ScheduleDiag(go);
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 借出喜悦反应失败");
                return false;
            }
        }

        // 结束借出：还原目标原有的喜悦特质
        public static void End(GameObject go, string buffId)
        {
            if (go == null) return;
            try
            {
                DrinkEffectState state = go.GetComponent<DrinkEffectState>();
                if (state == null || state.joySwaps == null) return;
                List<string> entries = state.TakeByPrefix(state.joySwaps, buffId);
                for (int i = 0; i < entries.Count; i++)
                {
                    RestoreOne(go, DrinkEffectState.ValueOf(entries[i]));
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, LogTag + " 归还喜悦反应失败");
            }
        }

        private static void EndAll(GameObject go, string why)
        {
            DrinkEffectState state = go.GetComponent<DrinkEffectState>();
            if (state == null || state.joySwaps == null || state.joySwaps.Count == 0) return;
            List<string> entries = new List<string>(state.joySwaps);
            state.joySwaps.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                RestoreOne(go, DrinkEffectState.ValueOf(entries[i]));
            }
            MFLog.Info(LogCat.Buff,LogTag + " 旧借出记录已归还（" + why + "，共 " + entries.Count + " 条）");
        }

        private static void RestoreOne(GameObject go, string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            int cut = payload.IndexOf(Sep, StringComparison.Ordinal);
            string target = cut >= 0 ? payload.Substring(0, cut) : payload;
            string native = cut >= 0 && cut + 1 < payload.Length ? payload.Substring(cut + 1) : "";
            StopJoySmis(go, null);
            if (target == BalloonArtistTrait) RemoveBalloonSensor(go);
            List<JoyBehaviourMonitor.Instance> before = CollectMonitors(go);
            if (!string.IsNullOrEmpty(native))
            {
                Trait back = GetTrait(native);
                if (back != null && back.OnAddTrait != null)
                {
                    back.OnAddTrait.Invoke(go);
                }
            }
            JoyBehaviourMonitor.Instance mon = NewestMonitor(go, before);
            if (mon != null && go.HasTag(GameTags.Overjoyed))
            {
                mon.GoToOverjoyed();
            }
            MFLog.Info(LogCat.Buff,LogTag + " 归还喜悦反应：" + target + " → "
                + (string.IsNullOrEmpty(native) ? "（无原生）" : native));
        }

        private static List<JoyBehaviourMonitor.Instance> CollectMonitors(GameObject go)
        {
            List<JoyBehaviourMonitor.Instance> list = new List<JoyBehaviourMonitor.Instance>();
            StateMachineController ctrl = go.GetComponent<StateMachineController>();
            if (ctrl == null) return list;
            foreach (StateMachine.Instance smi in ctrl)
            {
                JoyBehaviourMonitor.Instance mon = smi as JoyBehaviourMonitor.Instance;
                if (mon != null) list.Add(mon);
            }
            return list;
        }

        private static JoyBehaviourMonitor.Instance NewestMonitor(GameObject go,
            List<JoyBehaviourMonitor.Instance> before)
        {
            StateMachineController ctrl = go.GetComponent<StateMachineController>();
            if (ctrl == null) return null;
            foreach (StateMachine.Instance smi in ctrl)
            {
                JoyBehaviourMonitor.Instance mon = smi as JoyBehaviourMonitor.Instance;
                if (mon == null) continue;
                if (before != null && before.Contains(mon)) continue;
                return mon;
            }
            return null;
        }

        private static Trait GetTrait(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || Db.Get() == null || Db.Get().traits == null) return null;
                return Db.Get().traits.TryGet(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NativeJoyOwner(GameObject go)
        {
            StateMachineController ctrl = go.GetComponent<StateMachineController>();
            if (ctrl == null) return null;
            foreach (StateMachine.Instance smi in ctrl)
            {
                Type owner = JoyOwnerOf(smi);
                if (owner != null) return owner.Name;
            }
            return null;
        }

        private static Type JoyOwnerOf(StateMachine.Instance smi)
        {
            if (smi == null) return null;
            if (IsMonitor(smi)) return null;
            Type owner = smi.GetType().DeclaringType;
            if (owner == null) return null;
            return owner.GetNestedType("OverjoyedStates") != null ? owner : null;
        }

        private static bool IsMonitor(StateMachine.Instance smi)
        {
            if (smi == null) return false;
            Type t = smi.GetType();
            if (t.Name != "Instance" || t.DeclaringType == null) return false;
            return t.DeclaringType.Name == "JoyBehaviourMonitor";
        }

        private static int StopJoySmis(GameObject go, string keepOwner)
        {
            StateMachineController ctrl = go.GetComponent<StateMachineController>();
            if (ctrl == null) return 0;
            List<StateMachine.Instance> victims = new List<StateMachine.Instance>();
            List<string> names = new List<string>();
            foreach (StateMachine.Instance smi in ctrl)
            {
                if (smi == null) continue;
                if (IsMonitor(smi))
                {
                    victims.Add(smi);
                    names.Add("JoyBehaviourMonitor");
                    continue;
                }
                Type owner = JoyOwnerOf(smi);
                if (owner == null) continue;
                if (!string.IsNullOrEmpty(keepOwner) && owner.Name == keepOwner) continue;
                victims.Add(smi);
                names.Add(owner.Name);
            }
            for (int i = 0; i < victims.Count; i++)
            {
                try
                {
                    victims[i].StopSM("MoreFoodJoySwap");
                    ctrl.RemoveStateMachineInstance(victims[i]);
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Buff, e, LogTag + " 停用喜悦状态机失败");
                }
            }
            if (victims.Count > 0)
            {
                MFLog.Info(LogCat.Buff,LogTag + " 已停用喜悦状态机：" + string.Join("、", names.ToArray()));
            }
            return victims.Count;
        }
    }
}
