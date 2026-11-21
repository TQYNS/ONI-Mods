using HarmonyLib;
using TUNING;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(Workable), "GetEfficiencyMultiplier")]
    public static class DrinkLitWorkspacePatch
    {
        // 补丁：工作速率算完后按「光照工作加成」效果放大（无需光照也生效）
        public static void Postfix(Workable __instance, WorkerBase worker, ref float __result)
        {
            try
            {
            if (__instance == null || worker == null) return;
            if (!DrinkEffectHelpers.HasEffect(worker.gameObject, "DrinkEffect_BlackcurrantSoda")) return;

            try
            {
                var field = AccessTools.Field(typeof(Workable), "lightEfficiencyBonus");
                if (field == null || !(bool)field.GetValue(__instance)) return;
                if (__instance.currentlyLit) return;
                __result += DUPLICANTSTATS.STANDARD.Light.LIGHT_WORK_EFFICIENCY_BONUS;
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Drink][Effect] 光照工作加成异常");
            }
            }
            catch (System.Exception ex) { MFLog.HotFail("patch:DrinkLitWorkspacePatch", ex); }
        }
    }
}
