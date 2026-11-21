// 用途：食物吃完后按实际质量结算饮水值，并附加档案里的效果 buff。
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(Edible), "StopConsuming")]
    public static class HydratingFoodPatch
    {
        // unitsConsumed 是私有字段，且在 StopConsuming 内被重置为 NaN，必须在 Prefix 抓取
        private static readonly AccessTools.FieldRef<Edible, float> UnitsConsumed =
            AccessTools.FieldRefAccess<Edible, float>("unitsConsumed");

        public static void Prefix(Edible __instance, out float __state)
        {
            __state = 0f;
            try
            {
                if (__instance == null || !__instance.isBeingConsumed) return;

                float units = UnitsConsumed(__instance);
                if (float.IsNaN(units) || units <= 0f) return;

                __state = units * __instance.MassPerUnit;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:HydratingFoodPatch.Prefix", e); }
        }

        public static void Postfix(Edible __instance, WorkerBase worker, float __state)
        {
            try
            {
                if (__instance == null || worker == null || __state <= 0f) return;

                KPrefabID kpid = __instance.GetComponent<KPrefabID>();
                Tag tag = kpid != null ? kpid.PrefabTag : new Tag(__instance.FoodID);
                ConsumeProfile profile = ConsumeRegistry.Find(tag);
                if (profile == null) return;

                HydrationSettlement.Apply(worker.gameObject, profile.hydrationPerKg, __state);

                // 食物效果仍走 Buff 通道；只对复制人生效，动物不挂
                if (!string.IsNullOrEmpty(profile.effectId)
                    && worker.GetComponent<MinionIdentity>() != null
                    && Db.Get() != null && Db.Get().effects != null
                    && Db.Get().effects.Exists(profile.effectId))
                {
                    BuffManager.Apply(worker.gameObject, profile.effectId);
                }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:HydratingFoodPatch.Postfix", e); }
        }
    }
}
