// 用途：钩子与恢复：增益回读、光照缩放与灾星补丁

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
    internal static partial class SpaceRealEvents
    {
        // 读档恢复：buff 快照已归宿主自管 —— 挂载在 EventSaveMountPatch、补写在 EventSaveCapturePatch、
        // 读回在 SaveLoader.Load 后缀（幂等）。这里只兜底"已读档但后缀已错过"的场景。
        private static bool buffRestoreHooked;

        private static void HookBuffRestore()
        {
            try
            {
                if (buffRestoreHooked)
                {
                    return;
                }
                buffRestoreHooked = true;

                if (SaveLoader.Instance != null && SaveLoader.Instance.loadedFromSave)
                {
                    RestoreBuffsAfterLoad();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 读档恢复兜底失败");
            }
        }

        // 可选补丁手动挂载：attribute PatchAll 解析不到会禁用整个 mod
        private static bool optionalPatchesInstalled;

        internal static void InstallOptionalPatches(Harmony harmony)
        {
            if (optionalPatchesInstalled || harmony == null)
            {
                return;
            }
            optionalPatchesInstalled = true;
            TryPatch(harmony, typeof(DiseaseDropper.Instance), "StartSM",
                typeof(SpaceRealEventsDropperStartPatch), "Postfix");
        }

        private static void TryPatch(Harmony harmony, Type target, string methodName,
            Type patchClass, string patchMethod)
        {
            try
            {
                MethodInfo mi = AccessTools.Method(target, methodName);
                if (mi == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 可选补丁跳过（找不到目标方法）: "
                        + target.FullName + "." + methodName);
                    return;
                }
                MethodInfo post = AccessTools.Method(patchClass, patchMethod);
                if (post == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 可选补丁跳过（找不到补丁方法）: "
                        + patchClass.Name + "." + patchMethod);
                    return;
                }
                harmony.Patch(mi, postfix: new HarmonyMethod(post));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 可选补丁挂载失败（不影响其它功能）: "
                    + target.Name + "." + methodName);
            }
        }

        internal static void RestoreBuffsAfterLoad()
        {
            try
            {
                EventSaveData d = EventSaveData.Attach();
                if (d == null || d.buffId == null || d.buffId.Count == 0)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 读档恢复检查：没有 buff 快照（Data="
                        + (d == null ? "null" : "ok") + "，条数="
                        + ((d == null || d.buffId == null) ? -1 : d.buffId.Count) + "）");
                    return;
                }
                // 恢复前先拷局部副本再遍历：BeginBuff 会触发 PersistBuffs 改写同一集合
                List<string> ids = new List<string>(d.buffId);
                List<float> vals = new List<float>(d.buffValue);
                List<float> rems = new List<float>(d.buffRemain);
                int total = ids.Count;

                int restored = 0;
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    float value = i < vals.Count ? vals[i] : 0f;
                    float remain = i < rems.Count ? rems[i] : 0f;
                    if (string.IsNullOrEmpty(id) || remain <= 0f)
                    {
                        continue;
                    }
                    if (RestoreOneBuff(id, value, remain))
                    {
                        restored++;
                    }
                }
                if (restored > 0)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 读档恢复持续 buff " + restored + "/" + total + " 项（数值与剩余时间原样还原）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 读档恢复持续 buff 失败");
            }
        }

        // 幂等：同名 buff 已存在（另一挂载点先跑）则复用，只补齐目标
        private static RealBuff EnsureBuff(string id, string name, string kind, float remain, float value)
        {
            RealBuff b = SpaceRealEventRuntime.FindBuff(id);
            if (b != null)
            {
                return b;
            }
            return SpaceRealEventRuntime.BeginBuff(id, name, kind, remain, value);
        }

        // 推导 buff id 归属（real_xxx -> xxx），不维护硬编码表
        private static IEventContext FindOwnerInstance(string buffId)
        {
            if (string.IsNullOrEmpty(buffId))
            {
                return null;
            }
            try
            {
                IEventContext[] list = EventFrameworkApi.ActiveContexts();
                if (list == null)
                {
                    return null;
                }
                for (int i = 0; i < list.Length; i++)
                {
                    IEventContext a = list[i];
                    if (a == null || string.IsNullOrEmpty(a.TemplateId))
                    {
                        continue;
                    }
                    if (string.Equals(buffId, "real_" + a.TemplateId, StringComparison.Ordinal)
                        || string.Equals(buffId, a.TemplateId, StringComparison.Ordinal))
                    {
                        return a;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 查找 buff 归属实例失败");
            }
            return null;
        }

        // 存盘数值 -> tooltip 占位符 pct/delta/total_cycles，与首发同格式
        private static void WriteBuffNoticeArgs(string id, float value, float remain, IEventContext inst)
        {
            if (inst == null)
            {
                return;
            }
            float cycles = Mathf.Max(0f, remain / 600f);
            if (id == GravityKey)
            {
                inst.SetNoticeArg("delta", value.ToString("+0;-0"));
            }
            else if (id == FertileKey || id == DrySeasonKey)
            {
                inst.SetNoticeArg("pct", "+" + Mathf.Round(value * 100f) + "%");
            }
            else if (id == CosmicKey)
            {
                inst.SetNoticeArg("pct", "+" + Mathf.Round((value - 1f) * 100f) + "%");
            }
            inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
        }

        private static bool RestoreOneBuff(string id, float value, float remain)
        {
            // 先补 tooltip 占位符再判幂等，否则短路后永远补不上
            IEventContext inst = FindOwnerInstance(id);
            WriteBuffNoticeArgs(id, value, remain, inst);

            if (SpaceRealEventRuntime.HasBuff(id))
            {
                return false;
            }
            float cycles = Mathf.Max(0f, remain / 600f);
            if (id == GravityKey)
            {
                RealBuff b = EnsureBuff(GravityKey, "重力异常", "normal", remain, value);
                b.attrId = Db.Get().Attributes.Athletics.Id;
                b.attrMultiplier = false;
                SpaceRealEventBuffHub.Watch(b, GravityActions, BuffChannel.Dupes);
                int hit = SpaceRealEventBuffHub.AttachRange(b, LiveDupes(false), GravityActions);
                if (inst != null)
                {
                    inst.SetNoticeArg("delta", value.ToString("+0;-0"));
                    inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
                }
                MFLog.Verbose(LogCat.Event,"[Space] 读档恢复 重力异常：运动 " + value.ToString("+0;-0")
                    + "，剩余 " + Mathf.Round(remain) + " 秒，已挂 " + hit + " 人");
                return true;
            }
            if (id == FertileKey)
            {
                RealBuff b = EnsureBuff(FertileKey, "丰饶季", "normal", remain, value);
                b.attrId = Db.Get().Amounts.Maturity.deltaAttribute.Id;
                b.attrMultiplier = true;
                SpaceRealEventBuffHub.Watch(b, FertileActions, BuffChannel.Plants);
                int hit = SpaceRealEventBuffHub.AttachRange(b, AllPlantsSnapshot(), FertileActions);
                if (inst != null)
                {
                    inst.SetNoticeArg("pct", "+" + Mathf.Round(value * 100f) + "%");
                    inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
                }
                MFLog.Verbose(LogCat.Event,"[Space] 读档恢复 丰饶季：生长 +" + Mathf.Round(value * 100f)
                    + "%，剩余 " + Mathf.Round(remain) + " 秒，已挂 " + hit + " 株");
                return true;
            }
            if (id == DrySeasonKey)
            {
                RealBuff b = EnsureBuff(DrySeasonKey, "旱季", "dry", remain, value);
                b.attrId = Db.Get().PlantAttributes.FertilizerUsageMod.Id;
                b.attrMultiplier = false;
                SpaceRealEventBuffHub.Watch(b, DrySeasonActions, BuffChannel.Plants);
                int hit = SpaceRealEventBuffHub.AttachRange(b, GrowingPlants(), DrySeasonActions);
                if (inst != null)
                {
                    inst.SetNoticeArg("pct", "+" + Mathf.Round(value * 100f) + "%");
                    inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
                }
                MFLog.Verbose(LogCat.Event,"[Space] 读档恢复 旱季：耗液 +" + Mathf.Round(value * 100f)
                    + "%，剩余 " + Mathf.Round(remain) + " 秒，已挂 " + hit + " 株");
                return true;
            }
            if (id == SporeKey)
            {
                RealBuff b = EnsureBuff(SporeKey, "孢子云", "spore", remain, value);
                List<GameObject> plants = SporePlants();
                SpaceRealEventBuffHub.Watch(b, SporeActions, BuffChannel.Droppers);
                int hit = SpaceRealEventBuffHub.AttachRange(b, plants, SporeActions);
                // 病菌半衰期改写是全局 Disease 表，读档后同样要重做（ResetAll 已还原过一次）
                BackupGermInfoOnce(b, Db.Get().Diseases.PollenGerms);
                BackupGermInfoOnce(b, Db.Get().Diseases.ZombieSpores);
                if (inst != null)
                {
                    inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
                }
                MFLog.Verbose(LogCat.Event,"[Space] 读档恢复 孢子云：剩余 " + Mathf.Round(remain)
                    + " 秒，已挂 " + hit + " 株产病菌作物");
                return true;
            }
            if (id == MagneticKey)
            {
                EnsureBuff(MagneticKey, "全球磁暴", "magnetic", remain, value);
                SpaceRealEventRuntime.MagneticStormMul = value;
                if (inst != null)
                {
                    inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
                }
                MFLog.Verbose(LogCat.Event,"[Space] 读档恢复 全球磁暴：太阳能 ×" + value.ToString("0.00")
                    + "，剩余 " + Mathf.Round(remain) + " 秒");
                return true;
            }
            if (id == CosmicKey)
            {
                EnsureBuff(CosmicKey, "宇宙射线", "cosmic", remain, value);
                SpaceRealEventRuntime.CosmicRadMul = value;
                SpaceRealEventRuntime.MutationMul = 3f;
                if (inst != null)
                {
                    inst.SetNoticeArg("pct", "+" + Mathf.Round((value - 1f) * 100f) + "%");
                    inst.SetNoticeArg("total_cycles", cycles.ToString("0.0"));
                }
                MFLog.Verbose(LogCat.Event,"[Space] 读档恢复 宇宙射线：辐射 ×" + value.ToString("0.00")
                    + "，剩余 " + Mathf.Round(remain) + " 秒");
                return true;
            }
            return false;
        }

        internal static void ScaleWorldLight()
        {
            try
            {
                float cos = SpaceRealEventRuntime.CosmicRadMul;
                if (cos == 1f)
                {
                    return;
                }
                if (ClusterManager.Instance == null)
                {
                    return;
                }
                foreach (WorldContainer w in ClusterManager.Instance.WorldContainers)
                {
                    if (w == null)
                    {
                        continue;
                    }
                    w.currentCosmicIntensity *= cos;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 光照/辐射系数下发失败");
            }
        }

        internal static float ClampSolarOutput(float value, float min, float max)
        {
            float clamped = Mathf.Clamp(value, min, max);
            float mul = SpaceRealEventRuntime.MagneticStormMul;
            if (mul < 1f)
            {
                return clamped * mul;
            }
            return clamped;
        }

        internal static bool MutationBoostActive()
        {
            return SpaceRealEventRuntime.MutationMul > 1f;
        }

        internal static int MutationRolls()
        {
            return Mathf.Max(1, Mathf.RoundToInt(SpaceRealEventRuntime.MutationMul));
        }

        internal static void ProbeImpactor()
        {
            if (!HasPendingImpactor)
            {
                return;
            }
            if (TryConfigureImpactor())
            {
                return;
            }
            float now = SpaceRealEventRuntime.Now();
            if (now - PendingImpactorStartedAt < 10f)
            {
                return;
            }
            HasPendingImpactor = false;
            MFLog.Warn(LogCat.Event,"[Space] 大灭绝：等待 10 秒仍未生成灾星实体，本次仅保留原版事件流程（不再补流星雨）");
        }

        internal static void ImpactorStartSMPrefix(object instance)
        {
            if (!HasPendingImpactor)
            {
                return;
            }
            try
            {
                object def = ReadMember(instance, "def");
                if (def == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 灾星倒计时注入：取不到 def");
                    return;
                }
                if (!WriteMember(def, "arrivalTime", PendingImpactorArrival))
                {
                    MFLog.Warn(LogCat.Event,"[Space] 灾星倒计时注入：arrivalTime 不可写");
                    return;
                }
                MFLog.Verbose(LogCat.Event,"[Space] 灾星倒计时已注入 ClusterMapLargeImpactor："
                    + Mathf.Round(PendingImpactorArrival) + " 秒游戏时间（约 "
                    + Mathf.Round(PendingImpactorArrival / 600f) + " 周期）");
                HasPendingImpactor = false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 灾星倒计时注入异常");
            }
        }

        private static object ReadMember(object obj, string name)
        {
            Type t = obj.GetType();
            while (t != null)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null)
                {
                    return f.GetValue(obj);
                }
                PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead)
                {
                    return p.GetValue(obj, null);
                }
                t = t.BaseType;
            }
            return null;
        }

        private static bool WriteMember(object obj, string name, float value)
        {
            Type t = obj.GetType();
            while (t != null)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (f != null && f.FieldType == typeof(float))
                {
                    f.SetValue(obj, value);
                    return true;
                }
                t = t.BaseType;
            }
            return false;
        }

        private static void InstallImpactorPatch()
        {
            if (impactorPatchTried)
            {
                return;
            }
            impactorPatchTried = true;
            try
            {
                Type inner = AccessTools.Inner(typeof(ClusterMapLargeImpactor), "Instance");
                if (inner == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 灾星倒计时注入：找不到 ClusterMapLargeImpactor.Instance（倒计时走原版）");
                    return;
                }
                MethodInfo target = AccessTools.Method(inner, "StartSM", null, null);
                if (target == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 灾星倒计时注入：找不到 StartSM（倒计时走原版）");
                    return;
                }
                MethodInfo prefix = typeof(SpaceRealEvents).GetMethod("ImpactorStartSMPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 灾星倒计时注入：找不到前缀方法");
                    return;
                }
                Harmony h = new Harmony("space.realevents.impactor");
                h.Patch(target, new HarmonyMethod(prefix), null, null);
                MFLog.Verbose(LogCat.Event,"[Space] 灾星倒计时注入补丁已安装（ClusterMapLargeImpactor.Instance.StartSM）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 灾星倒计时注入补丁安装失败（倒计时走原版）");
            }
        }
    }
}
