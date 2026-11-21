using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(Hit), "DeliverHit")]
    public static class DrinkAnimalDamagePatch
    {
        private static readonly AccessTools.FieldRef<Hit, AttackProperties> Props =
            AccessTools.FieldRefAccess<Hit, AttackProperties>("properties");
        private static readonly AccessTools.FieldRef<Hit, GameObject> Target =
            AccessTools.FieldRefAccess<Hit, GameObject>("target");

        // 补丁：动物伤害命中前 —— 攻击者喝过「闪避」类效果则取消这次伤害
        public static bool Prefix(Hit __instance)
        {
            try
            {
                AttackProperties props = Props(__instance);
                GameObject target = Target(__instance);
                if (props == null || target == null || props.attacker == null) return true;
                if (!DrinkEffectHelpers.HasEffect(target, "DrinkEffect_SoyMilk")) return true;

                KPrefabID kp = props.attacker.GetComponent<KPrefabID>();
                if (kp != null && kp.HasTag(GameTags.Creature)) return false;

                FactionAlignment alignment = props.attacker.GetComponent<FactionAlignment>();
                if (alignment != null && alignment.Alignment == FactionManager.FactionID.Predator) return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Drink][Effect] 动物伤害免疫判定异常");
            }
            return true;
        }
    }
}
