namespace MoreFood_KFC
{
    public static class BlackcurrantSodaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_BlackcurrantSoda";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.Special("cure", 0.012f,
                        go => DrinkEffectHelpers.CureTrait(go, Id, "NightLight"),
                        summary: "治愈「夜光」");
        }
    }
}
