namespace MoreFood_KFC
{
    public static class XiMeiSodaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_XiMeiSoda";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.UngrantTemporarySkills(go, Id));
            def.Special("skill",
                        go => DrinkEffectHelpers.GrantTemporarySkill(go, Id, "Suits1"),
                        summary: "临时获得「宇航服 1」技能");
        }
    }
}
