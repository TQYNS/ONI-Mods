namespace MoreFood_KFC
{
    public static class BlueberrySparklingWineEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_BlueberrySparklingWine";
        // 该效果授予的原版特质 id
        private const string TraitId = "RadiationEater";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.RemoveAddedTraits(go, Id));
            def.Special("trait", 0.33f,
                        go => DrinkEffectHelpers.AddTemporaryTrait(go, Id, TraitId),
                        summary: "暂时获得「辐射食客」");
        }
    }
}
