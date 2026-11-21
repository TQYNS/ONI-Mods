using UnityEngine;

namespace MoreFood_KFC
{
    public static class OrangeSodaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_OrangeSoda";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach, OnDrink);
        }

        private static void Attach(BuffDef def)
        {
            def.OnTick((go, d, dt) => OnTick(go));
            def.Special("cure", go => DrinkEffectHelpers.CureAllergies(go),
                        summary: "治愈过敏");
        }

        // 必定部分：每次饮用续一次抗过敏压制
        private static void OnDrink(GameObject go)
        {
            DrinkEffectHelpers.EnsureAllergySuppression(go);
        }

        private static void OnTick(GameObject go)
        {
            DrinkEffectHelpers.EnsureAllergySuppression(go);
        }
    }
}
