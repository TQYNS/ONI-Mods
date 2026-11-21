using System;

namespace MoreFood_KFC
{
    // 饮品 buff 的 tooltip：原生属性行由 CountedBuffBuilder 自动生成，这里只追加自定义并列行。
    public static class DrinkEffectDisplays
    {
        // 增益行配色（金）
        private const string AccentColor = "#FFD700";
        // 减益行配色（红）
        private const string DebuffColor = "#FF3B30";
        // 红色富文本标记
        private const string RedMarker = "[red]";

        private static readonly SpanStyle AccentStyle = new SpanStyle().Color(AccentColor).Bold();
        private static readonly SpanStyle DebuffStyle = new SpanStyle().Color(DebuffColor).Bold();

// 注册全部饮品效果的显示条目（幂等）
        public static void RegisterAll()
        {
            Register("DrinkEffect_WhiteWhaleVodka",           ModStrings.BUFFS.ROWS.WHITEWHALEVODKA);
            Register("DrinkEffect_RedWine",                  ModStrings.BUFFS.ROWS.REDWINE);
            Register("DrinkEffect_Brandy",                   ModStrings.BUFFS.ROWS.BRANDY);
            Register("DrinkEffect_PureWheatBeer",            ModStrings.BUFFS.ROWS.PUREWHEATBEER);
            Register("DrinkEffect_GruitAle",                 ModStrings.BUFFS.ROWS.GRUITALE);
            Register("DrinkEffect_Rum",                      ModStrings.BUFFS.ROWS.RUM);
            Register("DrinkEffect_Moutai",                   ModStrings.BUFFS.ROWS.MOUTAI);
            Register("DrinkEffect_RiceWine",                 ModStrings.BUFFS.ROWS.RICEWINE);
            Register("DrinkEffect_MunichBeer",               ModStrings.BUFFS.ROWS.MUNICHBEER);
            Register("DrinkEffect_LimeCitrusWine",           ModStrings.BUFFS.ROWS.LIMECITRUSWINE);
            Register("DrinkEffect_BlueberrySparklingWine",   ModStrings.BUFFS.ROWS.BLUEBERRYSPARKLINGWINE);

            Register("DrinkEffect_IcedCoffee",               ModStrings.BUFFS.ROWS.ICEDCOFFEE);
            Register("DrinkEffect_Espresso",                 ModStrings.BUFFS.ROWS.ESPRESSO);
            Register("DrinkEffect_RetroEspresso",            ModStrings.BUFFS.ROWS.RETROESPRESSO);
            Register("DrinkEffect_CoffeeDoubleSkinJelly",    ModStrings.BUFFS.ROWS.COFFEEDOUBLESKINJELLY);
            Register("DrinkEffect_Mocha",                    ModStrings.BUFFS.ROWS.MOCHA);

            Register("DrinkEffect_PineappleBeer",            ModStrings.BUFFS.ROWS.PINEAPPLEBEER);
            Register("DrinkEffect_OrangeSoda",               ModStrings.BUFFS.ROWS.ORANGESODA);
            Register("DrinkEffect_BlackcurrantSoda",         ModStrings.BUFFS.ROWS.BLACKCURRANTSODA);
            Register("DrinkEffect_BlueberrySoda",            ModStrings.BUFFS.ROWS.BLUEBERRYSODA);
            Register("DrinkEffect_WatermelonSoda",           ModStrings.BUFFS.ROWS.WATERMELONSODA);
            Register("DrinkEffect_XiMeiSoda",                ModStrings.BUFFS.ROWS.XIMEISODA);
            Register("DrinkEffect_MangoSoda",                ModStrings.BUFFS.ROWS.MANGOSODA);

            Register("DrinkEffect_ClassicCola",              ModStrings.BUFFS.ROWS.CLASSICCOLA);
            Register("DrinkEffect_ZeroSugarCola",            ModStrings.BUFFS.ROWS.ZEROSUGARCOLA);
            Register("DrinkEffect_SoyMilk",                  ModStrings.BUFFS.ROWS.SOYMILK);
            Register("DrinkEffect_BoneSoup",                 ModStrings.BUFFS.ROWS.BONESOUP);

            RegisterDebuffBuff(DrinkEffectHelpers.HangoverId, ModStrings.BUFFS.ROWS.HANGOVER);
        }

        private static void Register(string effectId, params string[] labelValuePairs)
        {
            if (string.IsNullOrEmpty(effectId) || labelValuePairs == null) return;
            CountedBuffDisplay.Register(effectId, b => AddRows(b, labelValuePairs));
        }

        private static void RegisterDebuffBuff(string effectId, params string[] labelValuePairs)
        {
            if (string.IsNullOrEmpty(effectId) || labelValuePairs == null) return;
            CountedBuffDisplay.Register(effectId, b =>
            {
                b.titleColor = DebuffColor;
                AddRows(b, labelValuePairs);
            });
        }

        private static void AddRows(CountedBuffBuilder b, string[] labelValuePairs)
        {
            // StressDelta 的原生格式会显示成 "+0"，统一改用下面的「压力 -5%/周期」自定义行
            b.HideAttribute("StressDelta");

            for (int i = 0; i + 1 < labelValuePairs.Length; i += 2)
            {
                string label = labelValuePairs[i];
                string value = labelValuePairs[i + 1];
                bool red = label != null && label.StartsWith(RedMarker, StringComparison.Ordinal);
                if (red) label = label.Substring(RedMarker.Length);

                string rowLabel = label;
                string rowValue = value;
                SpanStyle style = red ? DebuffStyle : AccentStyle;
                // 只给 name 上色，数值保持普通文本（红/金规则一致）
                b.Row(r => r.Label(rowLabel, style).Span(rowValue));
            }
        }
    }
}
