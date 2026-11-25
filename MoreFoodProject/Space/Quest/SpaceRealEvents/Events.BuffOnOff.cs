// 用途：周期性增益开关：重力/沃土/孢子/磁暴/旱季/宇宙射线

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
        // 持续 buff 策略包：挑谁/要不要/改什么都集中于此，Hub 只拿委托
        private static readonly BuffActions GravityActions = new BuffActions
        {
            apply = delegate (RealBuff b, GameObject go)
            {
                return SpaceRealEventRuntime.AddAttrMod(b, go, Db.Get().Attributes.Athletics.Id, b.value, false);
            }
        };

        private static readonly BuffActions FertileActions = new BuffActions
        {
            apply = delegate (RealBuff b, GameObject go)
            {
                return SpaceRealEventRuntime.AddAttrMod(b, go,
                    Db.Get().Amounts.Maturity.deltaAttribute.Id, b.value, true);
            }
        };

        private static readonly BuffActions DrySeasonActions = new BuffActions
        {
            filter = delegate (GameObject go) { return HasIrrigation(go); },
            apply = delegate (RealBuff b, GameObject go)
            {
                if (!SpaceRealEventRuntime.AddAttrMod(b, go,
                        Db.Get().PlantAttributes.FertilizerUsageMod.Id, b.value, false))
                {
                    return false;
                }
                RefreshIrrigation(go);
                return true;
            }
        };

        // 孢子云只作用于毛刺花/同伴芽/孢子兰，且走 DiseaseDropper
        private static readonly string[] SporePrefabs = new string[] { "PrickleFlower", "BulbPlant", "EvilFlower" };

        internal static bool IsSporePrefab(GameObject go)
        {
            if (go == null)
            {
                return false;
            }
            KPrefabID pid = go.GetComponent<KPrefabID>();
            if (pid == null)
            {
                return false;
            }
            string tag = pid.PrefabTag.Name;
            for (int i = 0; i < SporePrefabs.Length; i++)
            {
                if (tag == SporePrefabs[i])
                {
                    return true;
                }
            }
            return false;
        }

        private static List<GameObject> SporePlants()
        {
            List<GameObject> list = new List<GameObject>();
            try
            {
                List<GameObject> all = AllPlantsSnapshot();
                for (int i = 0; i < all.Count; i++)
                {
                    if (IsSporePrefab(all[i]))
                    {
                        list.Add(all[i]);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 取产病菌植物失败");
            }
            return list;
        }

        // 放大病菌植物喷发量并记录原值；DiseaseDropper.Def 可能共享，按引用去重
        private static bool BoostDropper(RealBuff b, GameObject go)
        {
            if (b == null || go == null)
            {
                return false;
            }
            try
            {
                DiseaseDropper.Def def = go.GetDef<DiseaseDropper.Def>();
                if (def == null)
                {
                    return false;
                }
                for (int i = 0; i < b.droppers.Count; i++)
                {
                    if (b.droppers[i] != null && b.droppers[i].def == def)
                    {
                        return true;   // 该 def 已在本次 buff 中放大过
                    }
                }
                RealDropperBinding bind = new RealDropperBinding();
                bind.def = def;
                bind.singleEmitQuantity = def.singleEmitQuantity;
                bind.averageEmitPerSecond = def.averageEmitPerSecond;
                b.droppers.Add(bind);
                if (def.singleEmitQuantity > 0)
                {
                    def.singleEmitQuantity = Mathf.Max(1, def.singleEmitQuantity * 2);
                }
                if (def.averageEmitPerSecond > 0)
                {
                    def.averageEmitPerSecond = Mathf.Max(1, def.averageEmitPerSecond * 2);
                }
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 病菌喷发放大失败 @" + go.name);
                return false;
            }
        }

        private static readonly BuffActions SporeActions = new BuffActions
        {
            apply = delegate (RealBuff b, GameObject go) { return BoostDropper(b, go); }
        };

        private static void GravityAnomalyOn(IEventContext inst)
        {
            try
            {
                if (SpaceRealEventRuntime.HasBuff(GravityKey))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 重力异常已在持续中，本次不覆盖旧效果");
                    return;
                }
                float seconds = SpaceRealEventRuntime.BuffSeconds(inst, 600f, 1800f);
                float value;
                if (UnityEngine.Random.value < 0.6f)
                {
                    value = Mathf.Round(UnityEngine.Random.Range(3f, 8f));
                }
                else
                {
                    value = -Mathf.Round(UnityEngine.Random.Range(1f, 3f));
                }
                RealBuff b = SpaceRealEventRuntime.BeginBuff(GravityKey, "重力异常", "normal", seconds, value);
                b.owner = inst != null ? inst.InstanceId : null;
                List<GameObject> dupes = LiveDupes(false);
                string attrId = Db.Get().Attributes.Athletics.Id;
                b.attrId = attrId;
                b.attrMultiplier = false;
                SpaceRealEventBuffHub.Watch(b, GravityActions, BuffChannel.Dupes);
                int hit = SpaceRealEventBuffHub.AttachRange(b, dupes, GravityActions);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 重力异常：全体复制人运动 " + value.ToString("+0;-0")
                    + "（属性 " + attrId + "），命中 " + hit + " 人，持续 " + Mathf.Round(seconds)
                    + " 秒（" + (seconds / 600f).ToString("0.0") + " 周期）；期间新加入的复制人将自动补挂");
                // 只给数值，句式由 yaml tooltip 决定
                inst.SetNoticeArg("delta", value.ToString("+0;-0"));
                inst.SetNoticeArg("total_cycles", Mathf.Max(0f, seconds / 600f).ToString("0.0"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 重力异常执行失败");
            }
        }

        private static void GravityAnomalyOff(IEventContext inst)
        {
            SpaceRealEventRuntime.EndBuffFor(GravityKey, inst);
        }

        internal static List<GameObject> GrowingPlants()
        {
            List<GameObject> list = new List<GameObject>();
            try
            {
                ClusterManager cm = ClusterManager.Instance;
                if (cm == null)
                {
                    return list;
                }
                AddPlotPlants(SpaceRealEventRuntime.MainWorldId(), list);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 取植物列表失败");
            }
            return list;
        }

        private static void AddPlotPlants(int worldId, List<GameObject> into)
        {
            List<PlantablePlot> plots = Components.PlantablePlots.GetItems(worldId);
            if (plots == null)
            {
                return;
            }
            for (int i = 0; i < plots.Count; i++)
            {
                PlantablePlot plot = plots[i];
                if (plot == null)
                {
                    continue;
                }
                KPrefabID plant = plot.plant;
                if (plant == null)
                {
                    continue;
                }
                GameObject go = plant.gameObject;
                if (go == null || go.GetComponent<Growing>() == null)
                {
                    continue;
                }
                into.Add(go);
            }
        }

        private static List<GameObject> AllPlantsSnapshot()
        {
            List<GameObject> list = GrowingPlants();
            try
            {
                List<Growing> all = SpaceRealEventRuntime.AllSceneComponents<Growing>();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == null)
                    {
                        continue;
                    }
                    GameObject go = all[i].gameObject;
                    if (go == null || !SpaceRealEventRuntime.IsInActiveWorld(go) || list.Contains(go))
                    {
                        continue;
                    }
                    list.Add(go);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 取野生植物失败");
            }
            return list;
        }

        private static void FertileSeasonOn(IEventContext inst)
        {
            try
            {
                if (SpaceRealEventRuntime.HasBuff(FertileKey))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 丰饶季已在持续中，本次不覆盖旧效果");
                    return;
                }
                float seconds = SpaceRealEventRuntime.BuffSeconds(inst, 1200f, 4800f);
                float pct = UnityEngine.Random.Range(0.2f, 0.5f);
                RealBuff b = SpaceRealEventRuntime.BeginBuff(FertileKey, "丰饶季", "normal", seconds, pct);
                b.owner = inst != null ? inst.InstanceId : null;
                List<GameObject> plants = AllPlantsSnapshot();
                string attrId = Db.Get().Amounts.Maturity.deltaAttribute.Id;
                b.attrId = attrId;
                b.attrMultiplier = true;
                SpaceRealEventBuffHub.Watch(b, FertileActions, BuffChannel.Plants);
                int hit = SpaceRealEventBuffHub.AttachRange(b, plants, FertileActions);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 丰饶季：植物生长速度 +" + Mathf.Round(pct * 100f) + "%（属性 " + attrId
                    + " 乘算），命中 " + hit + "/" + plants.Count + " 株，持续 " + Mathf.Round(seconds)
                    + " 秒（" + (seconds / 600f).ToString("0.0") + " 周期）");
                // 只给数值，句式由 yaml tooltip 决定
                inst.SetNoticeArg("pct", "+" + Mathf.Round(pct * 100f) + "%");
                inst.SetNoticeArg("total_cycles", Mathf.Max(0f, seconds / 600f).ToString("0.0"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 丰饶季执行失败");
            }
        }

        private static void FertileSeasonOff(IEventContext inst)
        {
            SpaceRealEventRuntime.EndBuffFor(FertileKey, inst);
        }

        private static void SporeCloudOn(IEventContext inst)
        {
            try
            {
                if (SpaceRealEventRuntime.HasBuff(SporeKey))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 孢子云已在持续中，本次不覆盖旧效果");
                    return;
                }
                float seconds = SpaceRealEventRuntime.BuffSeconds(inst, 600f, 1800f);
                RealBuff b = SpaceRealEventRuntime.BeginBuff(SporeKey, "孢子云", "spore", seconds, 2f);
                b.owner = inst != null ? inst.InstanceId : null;
                Disease pollen = Db.Get().Diseases.PollenGerms;
                Disease zombie = Db.Get().Diseases.ZombieSpores;
                List<GameObject> plants = SporePlants();
                SpaceRealEventBuffHub.Watch(b, SporeActions, BuffChannel.Droppers);
                int emitters = SpaceRealEventBuffHub.AttachRange(b, plants, SporeActions);
                BackupGermInfo(b, pollen);
                BackupGermInfo(b, zombie);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 孢子云：产病菌作物 " + emitters + " 株喷发量 ×2（毛刺花/同伴芽/孢子兰，"
                    + "DiseaseDropper 配置）；病菌半衰期改为倍增期（"
                    + pollen.id + " / " + zombie.id + " 全元素）；持续 " + Mathf.Round(seconds) + " 秒");
                // 只给数值，句式由 yaml tooltip 决定
                inst.SetNoticeArg("total_cycles", Mathf.Max(0f, seconds / 600f).ToString("0.0"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 孢子云执行失败");
            }
        }

        // 幂等：同一 buff 对同种病菌只备份一次（恢复路径可能执行两遍）
        private static void BackupGermInfoOnce(RealBuff b, Disease d)
        {
            if (b == null || d == null)
            {
                return;
            }
            for (int i = 0; i < b.germs.Count; i++)
            {
                if (b.germs[i] != null && b.germs[i].disease == d)
                {
                    return;
                }
            }
            BackupGermInfo(b, d);
        }

        private static void BackupGermInfo(RealBuff b, Disease d)
        {
            if (b == null || d == null || d.elemGrowthInfo == null)
            {
                return;
            }
            try
            {
                ElemGrowthInfo[] copy = new ElemGrowthInfo[d.elemGrowthInfo.Length];
                Array.Copy(d.elemGrowthInfo, copy, d.elemGrowthInfo.Length);
                RealGermBackup backup = new RealGermBackup();
                backup.disease = d;
                backup.info = copy;
                b.germs.Add(backup);
                for (int i = 0; i < d.elemGrowthInfo.Length; i++)
                {
                    float hl = d.elemGrowthInfo[i].populationHalfLife;
                    if (hl > 0f && !float.IsInfinity(hl))
                    {
                        d.elemGrowthInfo[i].populationHalfLife = -hl;
                    }
                    float ohl = d.elemGrowthInfo[i].overPopulationHalfLife;
                    if (ohl > 0f && !float.IsInfinity(ohl))
                    {
                        d.elemGrowthInfo[i].overPopulationHalfLife = -ohl;
                    }
                }
                PushDiseaseTable();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 病菌半衰期改写失败（" + d.id + "）");
            }
        }

        private static void SporeCloudOff(IEventContext inst)
        {
            SpaceRealEventRuntime.EndBuffFor(SporeKey, inst);
        }

        private static void MagneticStormOn(IEventContext inst)
        {
            try
            {
                if (SpaceRealEventRuntime.HasBuff(MagneticKey))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 全球磁暴已在持续中，本次不覆盖旧效果");
                    return;
                }
                float seconds = SpaceRealEventRuntime.BuffSeconds(inst, 300f, 1800f);
                RealBuff b = SpaceRealEventRuntime.BeginBuff(MagneticKey, "全球磁暴", "magnetic", seconds, 0.7f);
                b.owner = inst != null ? inst.InstanceId : null;
                SpaceRealEventRuntime.MagneticStormMul = 0.7f;
                MFLog.Verbose(LogCat.Event,"[Space] ★ 全球磁暴：太阳能发电 -30%（WorldContainer.currentSunlightIntensity ×0.7，"
                    + "SolarPanel 读 Grid.LightIntensity），持续 " + Mathf.Round(seconds) + " 秒");
                // 只给数值，句式由 yaml tooltip 决定
                inst.SetNoticeArg("total_cycles", Mathf.Max(0f, seconds / 600f).ToString("0.0"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 全球磁暴执行失败");
            }
        }

        private static void MagneticStormOff(IEventContext inst)
        {
            SpaceRealEventRuntime.EndBuffFor(MagneticKey, inst);
        }

        // IrrigationMonitor.Instance 反射结果缓存（HasIrrigation 在每株作物循环中调用）
        private static Type s_irrigationSmiType;
        private static bool s_irrigationSmiLookedUp;

        private static Type IrrigationSmiType()
        {
            if (!s_irrigationSmiLookedUp)
            {
                s_irrigationSmiLookedUp = true;
                s_irrigationSmiType = AccessTools.Inner(typeof(IrrigationMonitor), "Instance");
            }
            return s_irrigationSmiType;
        }

        internal static bool HasIrrigation(GameObject go)
        {
            StateMachineController ctrl = go.GetComponent<StateMachineController>();
            if (ctrl == null)
            {
                return false;
            }
            Type t = IrrigationSmiType();
            if (t == null)
            {
                return false;
            }
            return ctrl.GetSMI(t) != null;
        }

        internal static void RefreshIrrigation(GameObject go)
        {
            if (go == null)
            {
                return;
            }
            try
            {
                StateMachineController ctrl = go.GetComponent<StateMachineController>();
                if (ctrl == null)
                {
                    return;
                }
                Type t = IrrigationSmiType();
                if (t == null)
                {
                    return;
                }
                StateMachine.Instance smi = ctrl.GetSMI(t);
                if (smi == null)
                {
                    return;
                }
                MethodInfo m = AccessTools.Method(t, "UpdateAbsorbing", new Type[] { typeof(bool) }, null);
                if (m == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 灌溉吸收器刷新：找不到 IrrigationMonitor.UpdateAbsorbing(bool)");
                    return;
                }
                m.Invoke(smi, new object[] { false });
                m.Invoke(smi, new object[] { true });
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 灌溉吸收器刷新失败 @" + go.name);
            }
        }

        private static void DrySeasonOn(IEventContext inst)
        {
            try
            {
                if (SpaceRealEventRuntime.HasBuff(DrySeasonKey))
                {
                    // 前置条件已保证未在持续；此处仅防御性返回
                    MFLog.Verbose(LogCat.Event,"[Space] 旱季已在持续中，本次不覆盖旧效果");
                    return;
                }
                float seconds = SpaceRealEventRuntime.BuffSeconds(inst, 900f, 4800f);
                float pct = UnityEngine.Random.Range(0.1f, 0.2f);
                RealBuff b = SpaceRealEventRuntime.BeginBuff(DrySeasonKey, "旱季", "dry", seconds, pct);
                b.owner = inst != null ? inst.InstanceId : null;
                List<GameObject> plants = GrowingPlants();
                string attrId = Db.Get().PlantAttributes.FertilizerUsageMod.Id;
                b.attrId = attrId;
                b.attrMultiplier = false;
                SpaceRealEventBuffHub.Watch(b, DrySeasonActions, BuffChannel.Plants);
                int hit = SpaceRealEventBuffHub.AttachRange(b, plants, DrySeasonActions);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 旱季：带液体需求的作物耗液 +" + Mathf.Round(pct * 100f) + "%（属性 "
                    + attrId + " 加算，命中 " + hit + "/" + plants.Count + " 株），持续 "
                    + Mathf.Round(seconds) + " 秒；事件期间新种下的作物由 OnPlantPlanted 自动补挂");
                // 只给数值，句式由 yaml tooltip 决定
                inst.SetNoticeArg("pct", "+" + Mathf.Round(pct * 100f) + "%");
                inst.SetNoticeArg("total_cycles", Mathf.Max(0f, seconds / 600f).ToString("0.0"));
                inst.SetNoticeArg("count", hit.ToString());
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 旱季执行失败");
            }
        }

        private static void DrySeasonOff(IEventContext inst)
        {
            SpaceRealEventRuntime.EndBuffFor(DrySeasonKey, inst);
        }

        private static void CosmicRayOn(IEventContext inst)
        {
            try
            {
                if (SpaceRealEventRuntime.HasBuff(CosmicKey))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 宇宙射线已在持续中，本次不覆盖旧效果");
                    return;
                }
                float seconds = SpaceRealEventRuntime.BuffSeconds(inst, 900f, 1800f);
                float mul = UnityEngine.Random.Range(2f, 4f);
                RealBuff b = SpaceRealEventRuntime.BeginBuff(CosmicKey, "宇宙射线", "cosmic", seconds, mul);
                b.owner = inst != null ? inst.InstanceId : null;
                SpaceRealEventRuntime.CosmicRadMul = mul;
                SpaceRealEventRuntime.MutationMul = 3f;
                MFLog.Verbose(LogCat.Event,"[Space] ★ 宇宙射线：太空辐射 ×" + mul.ToString("0.00") + "（+"
                    + Mathf.Round((mul - 1f) * 100f) + "%，WorldContainer.currentCosmicIntensity），"
                    + "植物变异概率 ×3（SeedProducer.RollForMutation 三连掷），持续 "
                    + Mathf.Round(seconds) + " 秒");
                // 只给数值，句式由 yaml tooltip 决定
                inst.SetNoticeArg("pct", "+" + Mathf.Round((mul - 1f) * 100f) + "%");
                inst.SetNoticeArg("total_cycles", Mathf.Max(0f, seconds / 600f).ToString("0.0"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 宇宙射线执行失败");
            }
        }

        private static void CosmicRayOff(IEventContext inst)
        {
            SpaceRealEventRuntime.MutationMul = 1f;
            SpaceRealEventRuntime.EndBuffFor(CosmicKey, inst);
        }
    }
}
