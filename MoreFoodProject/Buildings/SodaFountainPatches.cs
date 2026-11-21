using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TUNING;
using STRINGS;

namespace MoreFood_KFC
{
    // 1. 建筑定义：气体输入口
    [HarmonyPatch(typeof(SodaFountainConfig), "CreateBuildingDef")]
    public static class SodaFountain_CreateBuildingDef_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(ref BuildingDef __result)
        {
            try
            {
            __result.InputConduitType = ConduitType.Gas;
            __result.UtilityInputOffset = new CellOffset(1, 0);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); }
        }
    }

    // 2. 接管 ConfigureBuildingTemplate
    [HarmonyPatch(typeof(SodaFountainConfig), "ConfigureBuildingTemplate")]
    public static class SodaFountain_ConfigureBuildingTemplate_Patch
    {
        // 是否已屏蔽 Ronivan 的补丁（兼容处理）
        private static bool _ronivansBlocked = false;

        // Harmony 补丁：Prefix（参数 go：目标对象、prefab_tag：Tag）
        public static bool Prefix(GameObject go, Tag prefab_tag)
        {
            try
            {
            if (!_ronivansBlocked)
            {
                _ronivansBlocked = true;
                BlockRonivansSodaFountainExtension();
            }

            DestroyIfExists<SodaFountainWorkable>(go);
            DestroyIfExists<SodaFountain>(go);
            DestroyIfExists<ManualDeliveryKG>(go);
            DestroyIfExists<RoomTracker>(go);

            var kp = go.GetComponent<KPrefabID>();
            var field = typeof(KPrefabID).GetField("prefabInitFn",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(kp, null);
            kp.RemoveTag(RoomConstraints.ConstraintTags.RecBuilding);

            var fabricator = go.AddOrGet<ComplexFabricator>();
            fabricator.duplicantOperated = true;
            fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
            fabricator.keepAdditionalTag = SimHashes.CarbonDioxide.CreateTag();
            fabricator.keepExcessLiquids = true;
            fabricator.allowManualFluidDelivery = true;

            var workable = go.AddOrGet<ComplexFabricatorWorkable>();
            go.AddOrGet<FabricatorIngredientStatusManager>();
            go.AddOrGet<CopyBuildingSettings>();

            BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);
            fabricator.inStorage.capacityKg = 200f;

            // 气体口 (1,0)
            var gasConsumer = go.AddOrGet<ConduitConsumer>();
            gasConsumer.conduitType = ConduitType.Gas;
            gasConsumer.capacityTag = SimHashes.CarbonDioxide.CreateTag();
            gasConsumer.storage = fabricator.inStorage;
            gasConsumer.capacityKG = 50f;
            gasConsumer.alwaysConsume = true;
            gasConsumer.forceAlwaysSatisfied = true;
            gasConsumer.ignoreMinMassCheck = true;
            gasConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Store;

            // 液体口 (1,1)
            go.AddOrGet<SodaFountainLiquidInput>();
            go.AddOrGet<SodaFountainLiquidSink>();

            var liquidConsumer = go.AddComponent<ConduitConsumer>();
            liquidConsumer.conduitType = ConduitType.Liquid;
            liquidConsumer.useSecondaryInput = true;
            liquidConsumer.capacityTag = GameTags.Liquid;
            liquidConsumer.storage = fabricator.inStorage;
            liquidConsumer.capacityKG = 50f;
            liquidConsumer.alwaysConsume = true;
            liquidConsumer.forceAlwaysSatisfied = true;
            liquidConsumer.ignoreMinMassCheck = true;
            liquidConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Store;

            go.AddOrGetDef<PoweredActiveStoppableController.Def>();

            var animFile = Assets.GetAnim("anim_interacts_cookstation_gourtmet_kanim");
            if (animFile != null)
            {
                workable.overrideAnims = new KAnimFile[] { animFile };
            }

            return false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); return true; }
        }

        // 给 RL-IR 的 CustomSodaFountain.ConfigureBuilding 打一个返回 false 的 Prefix
        private static void BlockRonivansSodaFountainExtension()
        {
            try
            {
                var type = AccessTools.TypeByName(
                    "RonivansLegacy_ChemicalProcessing.Content.ModDb.CustomSodaFountain");
                if (type == null)
                {
                    MFLog.Info(LogCat.General, "[MoreFood] RL-IR 未安装或 CustomSodaFountain 不存在，跳过");
                    return;
                }

                var method = AccessTools.Method(type, "ConfigureBuilding");
                if (method == null)
                {
                    Debug.LogWarning("[MoreFood] RL-IR 的 ConfigureBuilding 方法未找到");
                    return;
                }

                var harmony = new Harmony("MoreFood_KFC.BlockRonivansSodaFountain");
                harmony.Patch(method,
                    prefix: new HarmonyMethod(
                        typeof(RonivansSodaFountainBlocker), nameof(RonivansSodaFountainBlocker.Prefix)));

                MFLog.Info(LogCat.General, "[MoreFood] 已拦截 RL-IR 的 CustomSodaFountain.ConfigureBuilding");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[MoreFood] 拦截 RL-IR 扩展失败");
            }
        }

        private static void DestroyIfExists<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp != null) UnityEngine.Object.Destroy(comp);
        }
    }

    // 拦截器：让 RL-IR 的 ConfigureBuilding 直接返回
    public static class RonivansSodaFountainBlocker
    {
        public static bool Prefix()
        {
            try
            {
            return false;  // 跳过 RL-IR 的方法体
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); return true; }
        }
    }

    // 3-4. ConduitConsumer storage 自愈
    [HarmonyPatch(typeof(ConduitConsumer), "Consume")]
    public static class ConduitConsumer_Consume_ForceStorage_Patch
    {
        // 补丁：汽水机 ConduitConsumer 取输入前，把消费源指向 ComplexFabricator 的输入库
        public static void Prefix(ConduitConsumer __instance)
        {
            try
            {
            var fabricator = __instance.GetComponent<ComplexFabricator>();
            if (fabricator == null || fabricator.inStorage == null) return;
            if (__instance.storage != fabricator.inStorage)
                __instance.storage = fabricator.inStorage;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); }
        }
    }

    [HarmonyPatch(typeof(ConduitConsumer), "OnSpawn")]
    public static class ConduitConsumer_OnSpawn_ForceStorage_Patch
    {
        // 补丁：同上（后置）—— 若 fabricator 有输入库就把 storage 重新指回它，防原版改回
        public static void Postfix(ConduitConsumer __instance)
        {
            try
            {
            var fabricator = __instance.GetComponent<ComplexFabricator>();
            if (fabricator != null && fabricator.inStorage != null)
                __instance.storage = fabricator.inStorage;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); }
        }
    }

    // 5. RequireInputs
    [HarmonyPatch(typeof(RequireInputs), "OnSpawn")]
    public static class RequireInputs_OnSpawn_Patch
    {
        // 补丁：汽水机 RequireInputs 之后关掉「必须通入液体」要求（SodaFountain 专属）
        public static void Postfix(RequireInputs __instance)
        {
            try
            {
            var kp = __instance.GetComponent<KPrefabID>();
            if (kp == null || kp.PrefabTag != new Tag("SodaFountain")) return;
            __instance.requireConduitHasMass = false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); }
        }
    }

    // 6. 技能属性
    [HarmonyPatch(typeof(SodaFountainConfig), "DoPostConfigureComplete")]
    public static class SodaFountain_DoPostConfigureComplete_Patch
    {
        // Harmony 补丁：Postfix（参数 go：目标对象）
        public static void Postfix(GameObject go)
        {
            try
            {
            go.GetComponent<KPrefabID>().prefabSpawnFn += (gameObj) =>
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
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); }
        }
    }

    // 7. HasIngredients 调试
    [HarmonyPatch(typeof(ComplexFabricator), "HasIngredients")]
    public static class ComplexFabricator_HasIngredients_Debug
    {
        // 补丁：ComplexFabricator 判定能否开产时，对汽水机放宽输入要求
        public static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, Storage storage, ref bool __result)
        {
            try
            {
            var kp = __instance.GetComponent<KPrefabID>();
            if (kp == null || kp.PrefabTag != new Tag("SodaFountain")) return;
            if (storage == null) return;
            foreach (var ing in recipe.ingredients)
            {
                float avail = storage.GetAmountAvailable(ing.material);
                MFLog.Verbose(LogCat.General, $"[SodaFountain Debug] ingredient={ing.material}, need={ing.amount}, available={avail}, result={__result}");
            }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); }
        }
    }

    // 8. 配方侧屏读建筑缓存
    [HarmonyPatch(typeof(SelectedRecipeQueueScreen), "GetIngredientDescription")]
    public static class SelectedRecipeQueueScreen_GetIngredientDescription_Patch
    {
        public static bool Prefix(
            SelectedRecipeQueueScreen __instance,
            ComplexRecipe.RecipeElement ingredient,
            out bool hasEnoughMaterial,
            ref string __result,
            ComplexFabricator ___target)
        {
            try
            {
            hasEnoughMaterial = false;
            __result = "";
            if (___target == null || ___target.inStorage == null || ___target.buildStorage == null)
                return true;

            float amount =
                ___target.inStorage.GetAmountAvailable(ingredient.material)
              + ___target.buildStorage.GetAmountAvailable(ingredient.material)
              + ___target.GetMyWorld().worldInventory.GetAmount(ingredient.material, true);

            hasEnoughMaterial = amount >= ingredient.amount;

            GameObject prefab = Assets.GetPrefab(ingredient.material);
            if (prefab == null) return true;

            string need = GameUtil.GetFormattedByTag(ingredient.material, ingredient.amount, GameUtil.TimeSlice.None);
            string have = GameUtil.GetFormattedByTag(ingredient.material, amount, GameUtil.TimeSlice.None);

            string text = GameUtil.SafeStringFormat(
                UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_REQUIREMENT,
                prefab.GetProperName(), need);
            text += "\n";
            if (hasEnoughMaterial)
                text += "<size=12>" + GameUtil.SafeStringFormat(
                    UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_AVAILABLE, have) + "</size>";
            else
                text += "<size=12><color=#E68280>" + GameUtil.SafeStringFormat(
                    UI.UISIDESCREENS.FABRICATORSIDESCREEN.RECIPE_AVAILABLE, have) + "</color></size>";

            __result = text;
            return false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); hasEnoughMaterial = false; __result = ""; return true; }
        }
    }

    // 9. ISecondaryInput：液体口 (1,1)
    public class SodaFountainLiquidInput : KMonoBehaviour, ISecondaryInput
    {
        // 汽水机次接口只接液体（告诉原版这是液体输入口）
        public bool HasSecondaryConduitType(ConduitType type) => type == ConduitType.Liquid;
        // 汽水机次接口的格子偏移（硬编码 (1,1)，绕开原版自动分配）
        public CellOffset GetSecondaryConduitOffset(ConduitType type) => new CellOffset(1, 1);
    }

    // 10. GetInputCell 硬编码液体口 (1,1)
    [HarmonyPatch(typeof(ConduitConsumer), "GetInputCell")]
    public static class ConduitConsumer_GetInputCell_Patch
    {
        // 补丁：取输入格时对汽水机的液体次接口直接返回 (1,1)（原版算错）
        public static bool Prefix(ConduitConsumer __instance, ConduitType inputConduitType, ref int __result)
        {
            try
            {
            if (!__instance.useSecondaryInput) return true;
            if (inputConduitType != ConduitType.Liquid) return true;

            var kp = __instance.GetComponent<KPrefabID>();
            if (kp == null) return true;
            var tag = kp.PrefabTag;
            if (tag != new Tag("SodaFountain") && tag != new Tag("SodaFountainComplete")) return true;

            var building = __instance.GetComponent<Building>();
            if (building == null) return true;

            __result = Grid.OffsetCell(building.NaturalBuildingCell(), new CellOffset(1, 1));
            return false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainPatches", e); return true; }
        }
    }

    // 11. 液体口 (1,1) 注册为 FlowUtilityNetwork 的 sink
    public class SodaFountainLiquidSink : KMonoBehaviour, FlowUtilityNetwork.IItem
    {
        // 所在格子
        private int _cell;
        // 接入的液体管网
        private FlowUtilityNetwork _network;

        // 所在格子（接口属性）
        public int Cell => _cell;
        public FlowUtilityNetwork Network { set { _network = value; } }
        // 端点类型：Sink（进液口）
        public Endpoint EndpointType => Endpoint.Sink;
        // 管网类型：液体
        public ConduitType ConduitType => ConduitType.Liquid;
        // 自身 GameObject
        public GameObject GameObject => gameObject;

        // Unity 回调：实例生成
        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            var building = GetComponent<Building>();
            if (building == null) return;
            _cell = Grid.OffsetCell(building.NaturalBuildingCell(), new CellOffset(1, 1));
            Game.Instance.liquidConduitSystem.AddToNetworks(_cell, this, true);
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:SodaFountainPatches", e); }
        }

        // Unity 回调：清理
        protected override void OnCleanUp()
        {
            try
            {
            if (_cell != 0 && Grid.IsValidCell(_cell))
                Game.Instance.liquidConduitSystem.RemoveFromNetworks(_cell, this, true);
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:SodaFountainPatches", e); }
        }
    }
}
