namespace MoreFood_KFC
{
    public static class RumEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_Rum";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.RestoreSuppressedTraits(go, Id));
            // 两条独立判定、独立冷却：治愈没中也不影响压制，反之亦然
            def.Special("cure", 0.02f,
                        go => DrinkEffectHelpers.CureTrait(go, Id, "NightLight"),
                        summary: "治愈「夜光」");
            def.Special("suppress", 0.40f,
                        go => DrinkEffectHelpers.SuppressTrait(go, Id, "NightLight"),
                        summary: "暂时压制「夜光」");
        }
    }
}
