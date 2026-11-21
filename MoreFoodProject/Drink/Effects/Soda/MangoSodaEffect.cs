namespace MoreFood_KFC
{
    public static class MangoSodaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_MangoSoda";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.Special("buff", 0.40f,
                        go => DrinkEffectHelpers.EnsureEffect(go, DrinkEffectHelpers.MangoCarryId),
                        summary: "获得「芒果搬运」加成");
        }
    }
}
