using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(Garnish), "Activate")]
    public static class Garnish_Activate_DrinkHydration_Patch
    {
        // 补丁：上桌调料被吃掉后补饮水（按 consumeRate）并触发对应 buff 行为
        public static void Postfix(Garnish __instance, GameObject diner)
        {
            try
            {
            if (__instance == null || diner == null) return;

            DrinkData data = Drinks.FindItem(__instance.itemTag);
            if (data == null) return;   // 原版盐 / 鱼子酱不处理

            HydrationSettlement.Apply(diner, data.hydrationPerKg, __instance.consumeRate);

            if (!string.IsNullOrEmpty(data.effectId)
                && Db.Get() != null && Db.Get().effects != null
                && Db.Get().effects.Exists(data.effectId))
            {
                BuffManager.Apply(diner, data.effectId);
                DrinkEffectBehaviorRegistry.OnDrink(diner, data.effectId);
            }

            DrinkHangoverEffect.TryApply(diner, data);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:Garnish_Activate_DrinkHydration_Patch", e); }
        }
    }
}
