using System;
using HarmonyLib;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(Edible), "StopConsuming")]
    public static class DrinkBeanDishPatch
    {
        private static readonly string[] BeanDishes =
        {
            "DeepFriedNosh", "Edamame", "Tofu", "Curry", "SpicyTofu", "SoyMilk"
        };

        // 补丁：吃完豆类菜肴后追加「豆腐西施」效果（额外 +4 士气、全属性+2）
        public static void Postfix(Edible __instance, WorkerBase worker)
        {
            try
            {
                if (__instance == null || worker == null) return;
                if (!DrinkEffectHelpers.HasEffect(worker.gameObject, "DrinkEffect_BlueberrySoda")) return;

                string id = __instance.FoodID;
                if (string.IsNullOrEmpty(id) || Array.IndexOf(BeanDishes, id) < 0) return;
                DrinkEffectHelpers.ApplyBeanMorale(worker.gameObject);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkBeanDishPatch", e); }
        }
    }

    [HarmonyPatch(typeof(Edible), "StopConsuming")]
    public static class PuddingImprovArtworkPatch
    {
        // 吃完香草奶油布丁后获得「崩溃艺术家」，走完整增益生命周期。
        public static void Postfix(Edible __instance, WorkerBase worker)
        {
            try
            {
                if (__instance == null || worker == null) return;
                if (__instance.FoodID != "Pudding") return;
                BuffManager.Apply(worker.gameObject, Buffs.ImprovArtworkId);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:PuddingImprovArtworkPatch", e); }
        }
    }
}
