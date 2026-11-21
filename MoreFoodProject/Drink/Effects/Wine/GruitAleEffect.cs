namespace MoreFood_KFC
{
    public static class GruitAleEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_GruitAle";
        // 喜悦反应对应的原版特质 id
        private const string JoyTrait = "HappySinger";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => JoyResponseSwap.End(go, Id));
            def.Special("joy", 0.33f,
                        go => JoyResponseSwap.Trigger(go, Id, JoyTrait),
                        summary: "借出「快乐歌手」喜悦反应");
        }
    }
}
