using HarmonyLib;
using Klei.AI;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(AttributeLevels), "AddExperience")]
    public static class DrinkLearningEfficiencyPatch
    {
        // 补丁：学习时间结算前按「专注」效果放大学习效率
        public static void Prefix(AttributeLevels __instance, ref float time_spent)
        {
            try
            {
                if (__instance == null) return;
                float mult = DrinkEffectHelpers.GetLearningMultiplier(__instance.gameObject);
                if (!UnityEngine.Mathf.Approximately(mult, 1f)) time_spent *= mult;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkLearningEfficiencyPatch", e); }
        }
    }
}
