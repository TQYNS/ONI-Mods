namespace MoreFood_KFC
{
    public static class RiceWineEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_RiceWine";
        // 该效果授予的原版特质 id
        private const string TraitId = "IronGut";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.RemoveAddedTraits(go, Id));
            def.Special("trait", 0.40f,
                        go => DrinkEffectHelpers.AddTemporaryTrait(go, Id, TraitId),
                        summary: "暂时获得「铁胃」");
        }
    }
}
