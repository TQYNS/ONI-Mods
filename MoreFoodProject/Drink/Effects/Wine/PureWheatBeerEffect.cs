using UnityEngine;

namespace MoreFood_KFC
{
    public static class PureWheatBeerEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_PureWheatBeer";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach, OnDrink);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.RestoreSuppressedTraits(go, Id));
            def.Special("suppress", 0.60f,
                        go => DrinkEffectHelpers.SuppressTrait(go, Id, "CalorieBurner"),
                        summary: "暂时压制「卡路里燃烧者」");
        }

        // 必定部分：每次饮用都加 200kcal
        private static void OnDrink(GameObject go)
        {
            DrinkEffectHelpers.AddCalories(go, 200f);
        }
    }
}
