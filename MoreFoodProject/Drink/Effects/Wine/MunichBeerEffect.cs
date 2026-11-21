using UnityEngine;

namespace MoreFood_KFC
{
    public static class MunichBeerEffect
    {
        // 效果 ID：注册进饮品效果行为表时用的键
        private const string Id = "DrinkEffect_MunichBeer";
        // 喜悦反应对应的原版特质 id
        private const string JoyTrait = "BalloonArtist";

        public static void Register()
        {
            DrinkEffectBehaviorRegistry.Register(Id, Attach, OnDrink);
        }

        private static void Attach(BuffDef def)
        {
            def.OnExpire((go, d) => JoyResponseSwap.End(go, Id));
            def.Special("joy", 0.33f,
                        go => JoyResponseSwap.Trigger(go, Id, JoyTrait),
                        summary: "借出「气球艺术家」喜悦反应");
        }

        // 必定部分：每次饮用都回 10 生命
        private static void OnDrink(GameObject go)
        {
            DrinkEffectHelpers.AddHitPoints(go, 10f);
        }
    }
}
