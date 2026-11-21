using UnityEngine;

namespace MoreFood_KFC
{
    public static class RetroEspressoEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_RetroEspresso";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, null, OnDrink);
        }

        private static void OnDrink(GameObject go)
        {
            DrinkEffectHelpers.RestoreStamina(go, 0.75f);
        }
    }
}
