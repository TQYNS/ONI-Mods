// WaterCoolerPatches.cs — 饮水机改造（含性能埋点）
using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(WaterCoolerConfig), "ConfigureBuildingTemplate")]
    public static class WaterCooler_ConfigureBuildingTemplate_Patch
    {
        // Harmony 补丁：Postfix（参数 go：目标对象、prefab_tag：Tag）
        public static void Postfix(GameObject go, Tag prefab_tag)
        {
            try
            {
            var kp = go.GetComponent<KPrefabID>();
            if (kp != null)
                kp.RemoveTag(RoomConstraints.ConstraintTags.RecBuilding);

            var roomTracker = go.GetComponent<RoomTracker>();
            if (roomTracker != null)
            {
                try { roomTracker.requirement = RoomTracker.Requirement.TrackingOnly; }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.General, e, "[MoreFood/Drink] 设置 RoomTracker.Requirement 失败");
                }
            }
            

            var storage = go.GetComponent<Storage>();
            if (storage != null)
            {
                storage.capacityKg = 200f;
                MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 饮水机 Storage 容量改为 200kg");
            }

            var manualDeliveryKG = go.GetComponent<ManualDeliveryKG>();
            if (manualDeliveryKG != null)
            {
                manualDeliveryKG.capacity = 200f;
                manualDeliveryKG.refillMass = 20f;
                MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 饮水机 ManualDeliveryKG 容量改为 200kg");
            }

            MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 饮水机已剥离娱乐属性（保留 Water/Milk/Ink 选项）");
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WaterCoolerPatches", e); }
        }
    }

    [HarmonyPatch(typeof(WaterCooler), "UpdateDrinkChores")]
    public static class WaterCooler_UpdateDrinkChores_Patch
    {
        public static bool Prefix()
        {
            try
            {
                return false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WaterCoolerPatches", e); return true; }
        }
    }

    [HarmonyPatch(typeof(WaterCooler), "OnSpawn")]
    public static class WaterCooler_OnSpawn_RegisterSource_Patch
    {
        // 补丁：饮水机生成后登记进饮水建筑表（供"饮品建筑来源"查找）
        public static void Postfix(WaterCooler __instance)
        {
            try
            {
            if (__instance == null) return;
            DrinkBuildingRegistry.Register(__instance.gameObject);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WaterCoolerPatches", e); }
        }
    }

    // 带埋点的 OnCleanUp
    [HarmonyPatch(typeof(WaterCooler), "OnCleanUp")]
    public static class WaterCooler_OnCleanUp_Patch
    {
        // 补丁：饮水机生成后做一次性初始化（含内存埋点，排查卡顿用）
        public static void Postfix(WaterCooler __instance)
        {
            try
            {
            if (__instance == null) return;

            long mem0 = GC.GetTotalMemory(false);
            float t0 = Time.realtimeSinceStartup;

            try { WaterCoolerReservation.CancelAllChores(__instance, "饮水机被拆除"); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[MoreFood/Drink] WaterCoolerReservation.CancelAllChores 异常");
            }

            long mem1 = GC.GetTotalMemory(false);
            float t1 = Time.realtimeSinceStartup;

            try { DrinkBuildingRegistry.Unregister(__instance.gameObject); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[MoreFood/Drink] DrinkBuildingRegistry.Unregister 异常");
            }

            long mem2 = GC.GetTotalMemory(false);
            float t2 = Time.realtimeSinceStartup;

            try { WaterCoolerReservation.ForceRemove(__instance); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[MoreFood/Drink] WaterCoolerReservation.ForceRemove 异常");
            }

            long mem3 = GC.GetTotalMemory(false);
            float t3 = Time.realtimeSinceStartup;

            MFLog.Info(LogCat.General, string.Format(
                "[MoreFood/Perf] OnCleanUp 分步: cancel={0:F2}ms/{1}KB, unregister={2:F2}ms/{3}KB, force={4:F2}ms/{5}KB",
                (t1 - t0) * 1000f, (mem1 - mem0) / 1024,
                (t2 - t1) * 1000f, (mem2 - mem1) / 1024,
                (t3 - t2) * 1000f, (mem3 - mem2) / 1024));

            MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 饮水机已拆除，chore + slot 已全部清理");
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WaterCoolerPatches", e); }
        }
    }

    [HarmonyPatch(typeof(WaterCooler.StatesInstance), "Drink")]
    public static class WaterCooler_Drink_SkipDoubleConsume_Patch
    {
        // 补丁：拦截 Drink(worker, false) 的二次扣水（triggerOnDrinkCallback=false 时原版会重复扣）
        public static bool Prefix(GameObject druplicant, bool triggerOnDrinkCallback)
        {
            try
            {
                if (triggerOnDrinkCallback) return true;

                MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink] 跳过 Drink(worker, false) 的二次扣水"
                          + "（抗寒效果已在 ApplyImmunityEffect 里加过）");
                return false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WaterCoolerPatches", e); return true; }
        }
    }
}
