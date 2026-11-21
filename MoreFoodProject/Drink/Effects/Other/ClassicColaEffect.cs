using UnityEngine;

namespace MoreFood_KFC
{
    public static class ClassicColaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_ClassicCola";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach, OnDrink);
        }

        private static void Attach(BuffDef def)
        {
            def.OnTick((go, d, dt) => OnTick(go));
            def.Special("insight", 0.025f,
                        go => DrinkEffectHelpers.TryInsight(go),
                        summary: "顿悟：随机一项属性 +1");
        }

        // 必定部分：每次饮用续一次耐寒/防湿足
        private static void OnDrink(GameObject go)
        {
            DrinkEffectHelpers.EnsureColdWetImmunity(go);
        }

        private static void OnTick(GameObject go)
        {
            DrinkEffectHelpers.EnsureColdWetImmunity(go);
        }
    }
}
