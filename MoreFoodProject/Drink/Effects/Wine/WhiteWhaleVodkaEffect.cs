namespace MoreFood_KFC
{
    public static class WhiteWhaleVodkaEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_WhiteWhaleVodka";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => DrinkEffectHelpers.UngrantTemporarySkills(go, Id));
            def.Special("skill", 0.40f,
                        go => DrinkEffectHelpers.GrantTemporarySkill(go, Id, "Swimming2"),
                        summary: "临时获得「游泳 2」技能");
        }
    }
}
