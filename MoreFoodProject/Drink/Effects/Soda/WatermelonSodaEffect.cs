using UnityEngine;

namespace MoreFood_KFC
{
    public static class WatermelonSodaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_WatermelonSoda";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach, OnDrink);
        }

        private static void Attach(BuffDef def)
        {
            def.OnTick((go, d, dt) => OnTick(go));
        }

        // 纯属性/续期：不挂特殊效果，不受冷却管制
        private static void OnDrink(GameObject go)
        {
            DrinkEffectHelpers.EnsureHeatImmunity(go);
            DrinkEffectHelpers.EnsureColdWetImmunity(go);
        }

        private static void OnTick(GameObject go)
        {
            DrinkEffectHelpers.EnsureHeatImmunity(go);
            DrinkEffectHelpers.EnsureColdWetImmunity(go);
        }
    }
}
