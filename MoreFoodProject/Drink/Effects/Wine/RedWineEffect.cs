namespace MoreFood_KFC
{
    public static class RedWineEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_RedWine";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.RemoveAddedTraits(go, Id));
            def.Special("trait", 0.60f,
                        go => DrinkEffectHelpers.AddTemporaryTrait(go, Id, "SimpleTastes"),
                        summary: "暂时获得「简单口味」");
            def.Special("cure", 0.02f,
                        go => DrinkEffectHelpers.CureTrait(go, Id, "WeakImmuneSystem"),
                        summary: "治愈「免疫系统薄弱」");
        }
    }
}
