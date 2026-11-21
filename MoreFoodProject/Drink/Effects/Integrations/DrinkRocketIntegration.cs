using System.Collections.Generic;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class DrinkRocketIntegration
    {
        // 朗姆酒效果的注册 id
        public const string RumEffect = "DrinkEffect_Rum";
        // 慕尼黑啤酒效果的注册 id
        public const string MunichEffect = "DrinkEffect_MunichBeer";

        // 判断火箭乘员里是否有人带着指定效果（Rum 加速 / Munich 弹药用）
        public static bool RocketHasCrewWithEffect(Clustercraft craft, string effectId)
        {
            if (craft == null || craft.ModuleInterface == null || string.IsNullOrEmpty(effectId)) return false;

            WorldContainer world;
            try { world = craft.ModuleInterface.GetInteriorWorld(); }
            catch { return false; }
            if (world == null) return false;

            List<MinionIdentity> minions;
            try { minions = Components.LiveMinionIdentities.GetWorldItems(world.id, false); }
            catch { return false; }
            if (minions == null) return false;

            for (int i = 0; i < minions.Count; i++)
            {
                MinionIdentity m = minions[i];
                if (m == null) continue;
                Effects fx = m.GetComponent<Effects>();
                if (fx != null && fx.HasEffect(effectId)) return true;
            }
            return false;
        }

        // 火箭速度倍率（乘员有 Rum 效果时 ×1.2）
        public static float RocketSpeedMultiplier(Clustercraft craft)
        {
            return RocketHasCrewWithEffect(craft, RumEffect) ? 1.2f : 1f;
        }

        // 火箭燃料消耗倍率（乘员有 Rum 效果时 ×1.1）
        public static float RocketFuelMultiplier(Clustercraft craft)
        {
            return RocketHasCrewWithEffect(craft, RumEffect) ? 1.1f : 1f;
        }

        // 火箭弹药伤害倍率（乘员有 MunichBeer 效果时 ×1.1）
        public static float RocketAmmoDamageMultiplier(Clustercraft craft)
        {
            return RocketHasCrewWithEffect(craft, MunichEffect) ? 1.1f : 1f;
        }
    }
}
