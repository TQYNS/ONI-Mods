namespace MoreFood_KFC
{
    public static class LimeCitrusWineEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_LimeCitrusWine";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            // 两个特质各自 3% 独立判定、独立冷却
            def.Special("cureNoodleArms", 0.03f,
                        go => DrinkEffectHelpers.CureTrait(go, Id, "NoodleArms"),
                        summary: "治愈「面条手臂」");
            def.Special("cureAnemic", 0.03f,
                        go => DrinkEffectHelpers.CureTrait(go, Id, "Anemic"),
                        summary: "治愈「贫血」");
        }
    }
}
