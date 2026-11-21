namespace MoreFood_KFC
{
    public static class BrandyEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_Brandy";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.RemoveAddedTraits(go, Id));
            def.Special("trait", 0.33f,
                        go => DrinkEffectHelpers.AddTemporaryTrait(go, Id, "FastLearner"),
                        summary: "暂时获得「快速学习者」");
            def.Special("cure", 0.02f,
                        go => DrinkEffectHelpers.CureTrait(go, Id, "SlowLearner"),
                        summary: "治愈「学习迟缓」");
        }
    }
}
