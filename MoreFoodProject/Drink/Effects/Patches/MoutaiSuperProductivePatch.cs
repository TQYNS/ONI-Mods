using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 茅台「超级生产力」携带者：瞬间完成工作的概率改为 30%（原版 10%）
    [HarmonyPatch(typeof(SuperProductive.Instance), "ShouldSkipWork")]
    internal static class MoutaiSuperProductiveChancePatch
    {
        private static bool Prefix(SuperProductive.Instance __instance, ref bool __result)
        {
            try
            {
                if (!IsMoutaiCarrier(__instance.gameObject)) return true;
                __result = UnityEngine.Random.Range(0f, 100f) <= MoutaiEffect.InstantSuccessChance;
                return false;
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:MoutaiSuperProductiveChance", e);
                return true;
            }
        }

        // 只认「茅台 buff 生效 + 本次饮用 60% 已命中」的小人
        private static bool IsMoutaiCarrier(GameObject go)
        {
            if (go == null || !DrinkEffectHelpers.HasEffect(go, "DrinkEffect_Moutai")) return false;
            DrinkEffectState state = go.GetComponent<DrinkEffectState>();
            return state != null && state.moutaiSuperBoost;
        }
    }
}
