using UnityEngine;

namespace MoreFood_KFC
{
    public static class BoneSoupEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_BoneSoup";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach, null);
        }

        private static void Attach(BuffDef def)
        {
            // 特殊效果：100% 触发，但仍受冷静期管制（冷却内不续抗寒，只保留运动 +2）
            def.Special("cold", 1f, go => ApplyColdWet(go), summary: "抗寒：免疫严寒与双脚湿透");
        }

        private static bool ApplyColdWet(GameObject go)
        {
            if (go == null) return false;
            DrinkEffectHelpers.EnsureColdWetImmunity(go);
            return true;
        }
    }
}
