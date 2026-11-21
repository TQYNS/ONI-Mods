using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 危险驾驶：Rum buff 的乘员在火箭内 → 速度 ×1.2、燃料 ×1.1（不叠加）
    [HarmonyPatch(typeof(Clustercraft), "get_Speed")]
    public static class DrinkRocket_Speed_Patch
    {
        // 补丁：火箭速度算完后乘上乘员效果倍率
        public static void Postfix(Clustercraft __instance, ref float __result)
        {
            try
            {
                if (__instance == null || __result <= 0f) return;
                __result *= DrinkRocketIntegration.RocketSpeedMultiplier(__instance);
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:DrinkRocket_Speed_Patch", e);
            }
        }
    }

    [HarmonyPatch(typeof(Clustercraft), "BurnFromTank")]
    public static class DrinkRocket_Fuel_Patch
    {
        // 补丁：火箭燃料消耗前乘上乘员效果倍率
        public static void Prefix(Clustercraft __instance, ref float attemptTravelAmount)
        {
            try
            {
                if (__instance == null || attemptTravelAmount <= 0f) return;
                attemptTravelAmount *= DrinkRocketIntegration.RocketFuelMultiplier(__instance);
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:DrinkRocket_Fuel_Patch", e);
            }
        }
    }

    // 让 UI / 航程校验与实际油耗保持一致
    [HarmonyPatch(typeof(Clustercraft), "get_FuelPerDistance")]
    public static class DrinkRocket_FuelPerDistance_Patch
    {
        // 补丁：另一条燃料取数路径同样乘倍率
        public static void Postfix(Clustercraft __instance, ref float __result)
        {
            try
            {
                if (__instance == null || __result <= 0f) return;
                __result *= DrinkRocketIntegration.RocketFuelMultiplier(__instance);
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:DrinkRocket_FuelPerDistance_Patch", e);
            }
        }
    }

    [HarmonyPatch(typeof(CraftModuleInterface), "get_FuelPerHex")]
    public static class DrinkRocket_FuelPerHex_Patch
    {
        // 补丁：模块弹药伤害乘倍率（CraftModuleInterface 路径）
        public static void Postfix(CraftModuleInterface __instance, ref float __result)
        {
            try
            {
                if (__instance == null || __result <= 0f) return;
                Clustercraft craft = __instance.GetComponent<Clustercraft>();
                if (craft == null) return;
                __result *= DrinkRocketIntegration.RocketFuelMultiplier(craft);
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:DrinkRocket_FuelPerHex_Patch", e);
            }
        }
    }

    [HarmonyPatch(typeof(CraftModuleInterface), "get_Range")]
    public static class DrinkRocket_Range_Patch
    {
        // 补丁：同上，另一处取数路径
        public static void Postfix(CraftModuleInterface __instance, ref float __result)
        {
            try
            {
                if (__instance == null || __result <= 0f) return;
                Clustercraft craft = __instance.GetComponent<Clustercraft>();
                if (craft == null) return;
                __result /= DrinkRocketIntegration.RocketFuelMultiplier(craft);
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:DrinkRocket_Range_Patch", e);
            }
        }
    }

    [HarmonyPatch(typeof(CraftModuleInterface), "get_MaxRange")]
    public static class DrinkRocket_MaxRange_Patch
    {
        // 补丁：模块弹药数量 / 伤害取整后按倍率放大
        public static void Postfix(CraftModuleInterface __instance, ref int __result)
        {
            try
            {
                if (__instance == null || __result <= 0) return;
                Clustercraft craft = __instance.GetComponent<Clustercraft>();
                if (craft == null) return;
                __result = Mathf.Max(0, Mathf.RoundToInt(__result / DrinkRocketIntegration.RocketFuelMultiplier(craft)));
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:DrinkRocket_MaxRange_Patch", e);
            }
        }
    }
}
