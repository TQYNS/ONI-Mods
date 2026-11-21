
namespace MoreFood_KFC
{
    public static class SoyMilkEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_SoyMilk";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, null);
        }
    }
}
