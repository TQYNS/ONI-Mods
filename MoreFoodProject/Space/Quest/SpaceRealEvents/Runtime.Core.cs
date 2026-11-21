// 用途：事件运行时核心：全局倍率、节拍、重置与增益生命周期

using System;
using EventFramework.Distribution.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSerialization;
using Klei.AI;
using Klei.AI.DiseaseGrowthRules;
using STRINGS;
using TUNING;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC
{
    internal static partial class SpaceRealEventRuntime
    {
        internal static float MagneticStormMul = 1f;
        internal static float CosmicRadMul = 1f;
        internal static float MutationMul = 1f;
        internal static bool GeneShuffleBonus;

        private static readonly List<RealBuff> buffs = new List<RealBuff>();
        private static readonly List<RealFreighterRun> runs = new List<RealFreighterRun>();
        private static readonly List<GameObject> joyTracked = new List<GameObject>();
        private static SpaceRealEventTicker ticker;
        private static bool tempSwept;
        private static int lastProbeSecond = -1;

        internal static float Now()
        {
            GameClock clock = GameClock.Instance;
            return clock != null ? clock.GetTime() : 0f;
        }

        internal static void EnsureTicker()
        {
            try
            {
                if (ticker != null)
                {
                    return;
                }
                GameObject anchor = null;
                if (Game.Instance != null)
                {
                    anchor = Game.Instance.gameObject;
                }
                else if (SaveGame.Instance != null)
                {
                    anchor = SaveGame.Instance.gameObject;
                }
                else if (ClusterManager.Instance != null)
                {
                    anchor = ClusterManager.Instance.gameObject;
                }
                if (anchor == null)
                {
                    return;
                }
                ticker = anchor.AddOrGet<SpaceRealEventTicker>();
                MFLog.Verbose(LogCat.Event,"[Space] 真实事件运行时已挂上节拍器（宿主 " + anchor.name + "）");
            }
            catch (Exception e)
            {
                MFLog.HotFail("space:EnsureTicker", e);
            }
        }

        internal static void Tick()
        {
            try
            {
                if (Game.Instance == null)
                {
                    return;
                }
                EnsureTicker();
                SweepOrphanTempStations();
                ExpireBuffs();
                TickJoyCleanup();
                TickFreighters();
                ProbeImpactorBySecond();
            }
            catch (Exception e)
            {
                MFLog.HotFail("tick:SpaceRealEvents", e);
            }
        }

        internal static void ResetAll(string why)
        {
            suppressPersist = true;
            try
            {
                for (int i = buffs.Count - 1; i >= 0; i--)
                {
                    if (i < buffs.Count)
                    {
                        EndBuff(buffs[i], "外部复位（" + why + "）", false);
                    }
                }
                buffs.Clear();
                SpaceRealEventBuffHub.ResetAll();
                for (int i = runs.Count - 1; i >= 0; i--)
                {
                    if (i < runs.Count)
                    {
                        KillRun(runs[i], "外部复位（" + why + "）");
                    }
                }
                runs.Clear();
                MagneticStormMul = 1f;
                CosmicRadMul = 1f;
                MutationMul = 1f;
                GeneShuffleBonus = false;
                tempSwept = false;
                ticker = null;
                lastProbeSecond = -1;
                joyTracked.Clear();
                MFLog.Verbose(LogCat.Event,"[Space] 真实事件运行时复位（" + why + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 真实事件复位异常");
            }
            finally
            {
                // ★ 外部复位期间禁止落盘，否则会清空存档里未用的 buff 快照
                suppressPersist = false;
            }
        }

        private static void ExpireBuffs()
        {
            float now = Now();
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (i >= buffs.Count)
                {
                    continue;
                }
                RealBuff b = buffs[i];
                if (b == null)
                {
                    buffs.RemoveAt(i);
                    continue;
                }
                if (now < b.endsAt)
                {
                    continue;
                }
                EndBuff(b, "到期", true);
                buffs.RemoveAt(i);
            }
        }

        internal static RealBuff FindBuff(string id)
        {
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i] != null && buffs[i].id == id)
                {
                    return buffs[i];
                }
            }
            return null;
        }

        internal static bool HasBuff(string id)
        {
            RealBuff b = FindBuff(id);
            return b != null && Now() < b.endsAt;
        }

        internal static float BuffSeconds(IEventContext inst, float lo, float hi)
        {
            if (inst != null && inst.HasDuration)
            {
                float d = inst.DurationRemaining;
                if (d > 0f)
                {
                    return d;
                }
            }
            return UnityEngine.Random.Range(lo, hi);
        }

        internal static void EndBuffFor(string id, IEventContext inst)
        {
            RealBuff b = FindBuff(id);
            if (b == null)
            {
                return;
            }
            if (inst != null && !string.IsNullOrEmpty(b.owner) && b.owner != inst.InstanceId)
            {
                MFLog.Verbose(LogCat.Event,"[Space] " + b.name + " 的结束回调来自另一个实例（"
                    + inst.InstanceId + "），保留原有效果");
                return;
            }
            EndBuffById(id, "事件结束", true);
        }

        internal static void TrackJoy(GameObject go)
        {
            if (go != null && !joyTracked.Contains(go))
            {
                joyTracked.Add(go);
            }
        }

        private static void TickJoyCleanup()
        {
            try
            {
            if (joyTracked.Count == 0)
            {
                return;
            }
            for (int i = joyTracked.Count - 1; i >= 0; i--)
            {
                GameObject go = joyTracked[i];
                if (go == null)
                {
                    joyTracked.RemoveAt(i);
                    continue;
                }
                JoyBehaviourMonitor.Instance mon = go.GetSMI<JoyBehaviourMonitor.Instance>();
                if (mon != null && mon.IsInsideState(mon.sm.overjoyed))
                {
                    continue;
                }
                CleanupJoy(go);
                joyTracked.RemoveAt(i);
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:SpaceRealEvents", e); }
        }

        private static void CleanupJoy(GameObject go)
        {
            try
            {
                KPrefabID kpid = go.GetComponent<KPrefabID>();
                if (kpid == null)
                {
                    return;
                }
                bool had = kpid.HasTag(GameTags.Overjoyed) || kpid.HasTag(GameTags.PleasantConversation);
                kpid.RemoveTag(GameTags.Overjoyed);
                kpid.RemoveTag(GameTags.PleasantConversation);
                if (had)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 心情愉悦：喜悦反应结束后已清理残留状态 " + go.GetProperName());
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("space:CleanupJoy", e);
            }
        }

        internal static bool HasAttrBinding(RealBuff b, GameObject go)
        {
            for (int i = 0; i < b.attrs.Count; i++)
            {
                RealAttrBinding bind = b.attrs[i];
                if (bind != null && bind.go == go && bind.mod != null)
                {
                    return true;
                }
            }
            return false;
        }

        internal static RealBuff BeginBuff(string id, string name, string kind, float seconds, float value)
        {
            RealBuff old = FindBuff(id);
            if (old != null)
            {
                MFLog.Warn(LogCat.Event,"[Space] " + name + " 已在持续中（剩余 "
                    + Mathf.Max(0f, old.endsAt - Now()).ToString("0") + " 秒），本次不覆盖");
                return old;
            }
            RealBuff b = new RealBuff();
            b.id = id;
            b.name = name;
            b.kind = kind;
            b.endsAt = Now() + Mathf.Max(1f, seconds);
            b.value = value;
            buffs.Add(b);
            PersistBuffs();
            return b;
        }
    }
}
