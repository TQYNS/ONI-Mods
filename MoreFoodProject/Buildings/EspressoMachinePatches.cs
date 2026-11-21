// EspressoMachinePatches.cs — 咖啡机（EspressoMachine）改造

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TUNING;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(EspressoMachineConfig), "ConfigureBuildingTemplate")]
    public static class EspressoMachine_ConfigureBuildingTemplate_Patch
    {
        // Harmony 补丁：Prefix（参数 go：目标对象、prefab_tag：Tag）
        public static bool Prefix(GameObject go, Tag prefab_tag)
        {
            try
            {
                var kp = go.GetComponent<KPrefabID>();

                // 1) 拆掉原版的娱乐/手动部件
                DestroyIfExists<EspressoMachineWorkable>(go);
                DestroyIfExists<EspressoMachine>(go);
                DestroyIfExists<ManualDeliveryKG>(go);
                DestroyIfExists<RoomTracker>(go);

                // 原版在方法末尾挂了 prefabInitFn += this.OnInit，
                if (kp != null)
                {
                    var field = typeof(KPrefabID).GetField("prefabInitFn",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    field?.SetValue(kp, null);

                    // 2) 剥离娱乐建筑标签
                    kp.RemoveTag(RoomConstraints.ConstraintTags.RecBuilding);
                }

                // 3) 改造成配方机
                var fabricator = go.AddOrGet<ComplexFabricator>();
                fabricator.duplicantOperated = true;
                fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
                fabricator.keepExcessLiquids = true;
                fabricator.allowManualFluidDelivery = true;

                var workable = go.AddOrGet<ComplexFabricatorWorkable>();
                var espressoAnim = Assets.GetAnim("anim_interacts_espresso_machine_kanim");
                if (espressoAnim != null)
                    workable.overrideAnims = new KAnimFile[] { espressoAnim };
                go.AddOrGet<FabricatorIngredientStatusManager>();
                go.AddOrGet<CopyBuildingSettings>();

                BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);
                fabricator.inStorage.capacityKg = 100f;

                // 4) 液体口：沿用原版 def 的 UtilityInputOffset = (1,2)
                var liquidConsumer = go.AddOrGet<ConduitConsumer>();
                liquidConsumer.conduitType = ConduitType.Liquid;
                liquidConsumer.capacityTag = ElementLoader.FindElementByHash(SimHashes.Water).tag;
                liquidConsumer.storage = fabricator.inStorage;   // 关键：接到配方机的入口仓
                liquidConsumer.capacityKG = 50f;
                liquidConsumer.alwaysConsume = true;
                liquidConsumer.forceAlwaysSatisfied = true;
                liquidConsumer.ignoreMinMassCheck = true;
                liquidConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Store;

                go.AddOrGetDef<PoweredActiveStoppableController.Def>();

                MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 咖啡机已改造为配方机（剥离娱乐属性，液体口 (1,2)）");
                return false;   // 跳过原版实现
            }
            catch (System.Exception e) { MFLog.HotFail("patch:EspressoMachine_ConfigureBuildingTemplate_Patch", e); return true; }
        }

        private static void DestroyIfExists<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp != null) UnityEngine.Object.Destroy(comp);
        }
    }

    // 技能属性：照抄 SodaFountainPatches 里 SodaFountain_DoPostConfigureComplete_Patch 的写法
    [HarmonyPatch(typeof(EspressoMachineConfig), "DoPostConfigureComplete")]
    public static class EspressoMachine_DoPostConfigureComplete_Patch
    {
        // Harmony 补丁：Postfix（参数 go：目标对象）
        public static void Postfix(GameObject go)
        {
            try
            {
            var kp = go.GetComponent<KPrefabID>();
            if (kp == null) return;

            kp.prefabSpawnFn += (gameObj) =>
            {
                var workable = gameObj.GetComponent<ComplexFabricatorWorkable>();
                if (workable != null)
                {
                    workable.AttributeConverter = Db.Get().AttributeConverters.CookingSpeed;
                    workable.AttributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.PART_DAY_EXPERIENCE;
                    workable.SkillExperienceSkillGroup = Db.Get().SkillGroups.Cooking.Id;
                    workable.SkillExperienceMultiplier = SKILLS.PART_DAY_EXPERIENCE;
                }
            };
            }
            catch (System.Exception e) { MFLog.HotFail("patch:EspressoMachine_DoPostConfigureComplete_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(RequireInputs), "OnSpawn")]
    public static class EspressoMachine_RequireInputs_Patch
    {
        // 补丁：咖啡机 RequireInputs 之后 —— 按 prefab tag 判定本机，关掉输入接口的质量要求
        public static void Postfix(RequireInputs __instance)
        {
            try
            {
            var kp = __instance.GetComponent<KPrefabID>();
            if (kp == null) return;
            Tag tag = kp.PrefabTag;
            if (tag == new Tag("EspressoMachine") || tag == new Tag("EspressoMachineComplete"))
                __instance.requireConduitHasMass = false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:EspressoMachinePatches", e); }
        }
    }
}
