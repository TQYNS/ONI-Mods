using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class DrinkEffectBehaviorRegistry
    {
        // 是否已初始化
        private static bool _initialized;

        private static readonly Dictionary<string, Action<BuffDef>> _attach =
            new Dictionary<string, Action<BuffDef>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Action<GameObject>> _onDrink =
            new Dictionary<string, Action<GameObject>>(StringComparer.Ordinal);

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            RegisterHelperBuffs();

            WhiteWhaleVodkaEffect.Register();
            RedWineEffect.Register();
            BrandyEffect.Register();
            PureWheatBeerEffect.Register();
            GruitAleEffect.Register();
            RumEffect.Register();
            MoutaiEffect.Register();
            RiceWineEffect.Register();
            MunichBeerEffect.Register();
            LimeCitrusWineEffect.Register();
            BlueberrySparklingWineEffect.Register();

            IcedCoffeeEffect.Register();
            EspressoEffect.Register();
            RetroEspressoEffect.Register();
            CoffeeDoubleSkinJellyEffect.Register();
            MochaEffect.Register();

            PineappleBeerEffect.Register();
            OrangeSodaEffect.Register();
            BlackcurrantSodaEffect.Register();
            BlueberrySodaEffect.Register();
            WatermelonSodaEffect.Register();
            XiMeiSodaEffect.Register();
            MangoSodaEffect.Register();

            ClassicColaEffect.Register();
            ZeroSugarColaEffect.Register();
            SoyMilkEffect.Register();
            BoneSoupEffect.Register();

            DrinkEffectDisplays.RegisterAll();
        }

        public static void Register(string effectId,
                                    Action<BuffDef> attach,
                                    Action<GameObject> onDrink = null)
        {
            if (string.IsNullOrEmpty(effectId)) return;
            if (attach != null) _attach[effectId] = attach;
            if (onDrink != null) _onDrink[effectId] = onDrink;
        }

        // 把效果实现挂到 buff 定义上（注册特殊效果 Special / OnTick / OnExpire 等）
        public static void Attach(BuffDef def)
        {
            EnsureInitialized();
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            Action<BuffDef> action;
            if (_attach.TryGetValue(def.id, out action) && action != null)
                action(def);
        }

        // 回调：OnDrink（参数 go：目标对象、effectId：效果 id）——先跑必定部分，再判定特殊效果
        public static void OnDrink(GameObject go, string effectId)
        {
            EnsureInitialized();
            Action<GameObject> action;
            if (!string.IsNullOrEmpty(effectId) && _onDrink.TryGetValue(effectId, out action) && action != null)
            {
                try { action(go); }
                catch (Exception e) { MFLog.Exc(LogCat.Buff, e, "[MoreFood/Drink][Effect] OnDrink 异常 " + effectId); }
            }
            BuffManager.TriggerSpecials(go, effectId, SpecialTrigger.Drink);
        }

        private static void RegisterHelperBuffs()
        {
            if (!BuffConfig.Exists(DrinkEffectHelpers.HangoverId))
            {
                BuffDef hangover = BuffConfig.Add(
                    DrinkEffectHelpers.HangoverId, ModStrings.BUFFS.HANGOVER_NAME,
                    ModStrings.BUFFS.HANGOVER_DESC, true);
                hangover.duration = 300f;
            }

            if (!BuffConfig.Exists(DrinkEffectHelpers.BeanMoraleId))
            {
                BuffDef bean = BuffConfig.Add(
                    DrinkEffectHelpers.BeanMoraleId, ModStrings.BUFFS.BEANMORALE_NAME,
                    ModStrings.BUFFS.BEANMORALE_DESC, false);
                bean.duration = 600f;
                bean.Add(StatIds.Morale, 4f);
                // 全属性 +2
                bean.Add(StatIds.Construction, 2f);
                bean.Add(StatIds.Digging, 2f);
                bean.Add(StatIds.Machinery, 2f);
                bean.Add(StatIds.Athletics, 2f);
                bean.Add(StatIds.Learning, 2f);
                bean.Add(StatIds.Cooking, 2f);
                bean.Add(StatIds.Caring, 2f);
                bean.Add(StatIds.Strength, 2f);
                bean.Add(StatIds.Art, 2f);
                bean.Add(StatIds.Ranching, 2f);
                bean.Add(StatIds.SpaceNavigation, 2f);
            }

            if (!BuffConfig.Exists(DrinkEffectHelpers.MangoCarryId))
            {
                BuffDef mango = BuffConfig.Add(
                    DrinkEffectHelpers.MangoCarryId, ModStrings.BUFFS.MANGOCARRY_NAME,
                    ModStrings.BUFFS.MANGOCARRY_DESC, false);
                mango.duration = 600f;
                mango.Add(StatIds.CarryAmount, 600f);
            }

            if (!BuffConfig.Exists(DrinkEffectHelpers.ThermalHeatId))
            {
                BuffDef heat = BuffConfig.Add(
                    DrinkEffectHelpers.ThermalHeatId, ModStrings.BUFFS.THERMALHEAT_NAME,
                    ModStrings.BUFFS.THERMALHEAT_DESC, false);
                heat.duration = 600f;
                heat.Add(StatIds.ScaldingThreshold, 100000f);
            }

            if (!BuffConfig.Exists(DrinkEffectHelpers.ThermalColdId))
            {
                BuffDef cold = BuffConfig.Add(
                    DrinkEffectHelpers.ThermalColdId, ModStrings.BUFFS.THERMALCOLD_NAME,
                    ModStrings.BUFFS.THERMALCOLD_DESC, false);
                cold.duration = 600f;
                cold.Add(StatIds.ScoldingThreshold, -100000f);
            }
        }
    }
}
