namespace MoreFood_KFC
{
    public static class MoutaiEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_Moutai";
        // 喜悦反应对应的原版特质 id
        private const string JoyTrait = "SuperProductive";
        // 本 buff 携带者的「瞬间完成工作」概率（原版为 10%）
        internal const float InstantSuccessChance = 30f;

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) =>
            {
                DrinkEffectState state = go.GetComponent<DrinkEffectState>();
                if (state != null) state.moutaiSuperBoost = false;
                JoyResponseSwap.End(go, Id);
            });
            def.Special("joy", 0.60f, go =>
            {
                bool borrowed = JoyResponseSwap.Trigger(go, Id, JoyTrait);
                // 借出成功才标记 30%（原生就是超级生产力的小人同样享受）
                if (borrowed)
                {
                    DrinkEffectState state = go.AddOrGet<DrinkEffectState>();
                    if (state != null) state.moutaiSuperBoost = true;
                }
                return borrowed;
            }, summary: "借出「超级生产力」喜悦反应（瞬完率 30%）");
        }
    }
}
