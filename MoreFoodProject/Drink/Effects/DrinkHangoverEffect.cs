using UnityEngine;

namespace MoreFood_KFC
{
    public static class DrinkHangoverEffect
    {
        // 按饮品数据判定是否触发宿醉（酒类命中则挂宿醉效果）
        public static void TryApply(GameObject go, DrinkData data)
        {
            if (go == null || data == null || data.hangoverChance <= 0f) return;
            if (!DrinkEffectHelpers.Roll(data.hangoverChance)) return;
            DrinkEffectHelpers.EnsureEffect(go, DrinkEffectHelpers.HangoverId);
        }
    }
}
