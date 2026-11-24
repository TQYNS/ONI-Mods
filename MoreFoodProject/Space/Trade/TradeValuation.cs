using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 静态定价表：交易层 买 1.40 / 卖 0.60，声望只加在卖侧（TPI）
    internal static class TradeValuation
    {
        internal const int RarityCount = 6;
        internal const float BuyBase = 1.40f;
        internal const float SellBase = 0.60f;
        internal const float CaloriesPerMark = 1000f * 1000f;

        internal static readonly Dictionary<string, TagDef> Tags =
            new Dictionary<string, TagDef>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, float> ElementPrice =
            new Dictionary<string, float>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, float> TpiTable =
            new Dictionary<string, float>(StringComparer.Ordinal);
        internal static readonly HashSet<string> Blacklist = new HashSet<string>(StringComparer.Ordinal);
        internal static readonly HashSet<string> InvalidTags = new HashSet<string>(StringComparer.Ordinal);

        private static readonly float[] Bounds = new float[] { 3f, 20f, 100f, 400f, 1288f };
        private static readonly float[] Weights = new float[] { 3f, 10f, 68f, 9f, 8f, 2f };
        private static readonly float[] Tpi = new float[] { 0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.10f, 0.12f };
        private static readonly float[] TpiDefault = new float[] { 0f, 0.02f, 0.04f, 0.06f, 0.08f, 0.10f, 0.12f };
        private static readonly string[] RankNames = new string[] { "F", "E", "D", "C", "B", "A", "S" };

        internal static bool inited;
        internal static int version = -1;

        internal static void Init()
        {
            if (inited && version == GlobalCacheVersion.TRADE_VERSION)
            {
                return;
            }
            inited = true;
            version = GlobalCacheVersion.TRADE_VERSION;

            Tags.Clear();
            ElementPrice.Clear();
            TpiTable.Clear();
            Blacklist.Clear();
            InvalidTags.Clear();
            for (int i = 0; i < Tpi.Length; i++)
            {
                Tpi[i] = TpiDefault[i];
            }

            ValuationFileDef v = TradeDb.Valuation;
            if (v != null)
            {
                if (v.tags != null)
                {
                    foreach (KeyValuePair<string, TagDef> kv in v.tags)
                    {
                        if (kv.Value == null || string.IsNullOrEmpty(kv.Key))
                        {
                            continue;
                        }
                        if (kv.Value.price < 0f)
                        {
                            InvalidTags.Add(kv.Key);
                            MFLog.Warn(LogCat.Trade, "[Space] Tag " + kv.Key + " 的 price < 0，已忽略");
                            continue;
                        }
                        if (kv.Value.stock == null || kv.Value.stock.Length < 2)
                        {
                            InvalidTags.Add(kv.Key);
                            MFLog.Warn(LogCat.Trade, "[Space] Tag " + kv.Key + " 缺少 stock 区间，已忽略");
                            continue;
                        }
                        if (kv.Value.stock[1] < kv.Value.stock[0])
                        {
                            float t = kv.Value.stock[0];
                            kv.Value.stock[0] = kv.Value.stock[1];
                            kv.Value.stock[1] = t;
                            MFLog.Warn(LogCat.Trade, "[Space] Tag " + kv.Key + " 的 stock 区间反转，已交换");
                        }
                        kv.Value.stock[0] = TradeNumber.Units(kv.Value.stock[0]);
                        kv.Value.stock[1] = Mathf.Max(kv.Value.stock[0], TradeNumber.Units(kv.Value.stock[1]));
                        if (kv.Value.x <= 0f)
                        {
                            kv.Value.x = 0.12f;
                        }
                        Tags[kv.Key] = kv.Value;
                    }
                }
                if (v.element_prices != null)
                {
                    foreach (KeyValuePair<string, float> kv in v.element_prices)
                    {
                        if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0f)
                        {
                            ElementPrice[kv.Key] = kv.Value;
                        }
                    }
                }
                if (v.tpi != null)
                {
                    foreach (KeyValuePair<string, float> kv in v.tpi)
                    {
                        if (kv.Value >= 0f)
                        {
                            TpiTable[kv.Key] = kv.Value;
                        }
                    }
                }
                if (v.blacklist_tags != null)
                {
                    for (int i = 0; i < v.blacklist_tags.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(v.blacklist_tags[i]))
                        {
                            Blacklist.Add(v.blacklist_tags[i]);
                        }
                    }
                }
                ApplyBounds(v.rarity_bounds);
                ApplyWeights(v.rarity_weight);
                ApplyTpi();
            }
            MFLog.Info(LogCat.Space, "[Space] 贸易估值表：有效 Tag " + Tags.Count + " / 元素价 " + ElementPrice.Count
                + " / 无效 " + InvalidTags.Count + " / 黑名单 " + Blacklist.Count
                + " / 分段 " + string.Join("|", Array.ConvertAll(Bounds, delegate(float f) { return f.ToString("0.##"); })));
        }

        private static void ApplyBounds(Dictionary<string, float> src)
        {
            if (src == null || src.Count == 0)
            {
                return;
            }
            float b;
            if (src.TryGetValue("bad", out b) && b > 0f) Bounds[0] = b;
            if (src.TryGetValue("common", out b) && b > 0f) Bounds[1] = b;
            if (src.TryGetValue("good", out b) && b > 0f) Bounds[2] = b;
            if (src.TryGetValue("master", out b) && b > 0f) Bounds[3] = b;
            if (src.TryGetValue("legendary", out b) && b > 0f) Bounds[4] = b;
            for (int i = 1; i < Bounds.Length; i++)
            {
                if (Bounds[i] <= Bounds[i - 1])
                {
                    MFLog.Warn(LogCat.Trade, "[Space] rarity_bounds 分段不递增，用内置默认兜底");
                    Bounds[0] = 3f;
                    Bounds[1] = 20f;
                    Bounds[2] = 100f;
                    Bounds[3] = 400f;
                    Bounds[4] = 1288f;
                    return;
                }
            }
        }

        private static void ApplyWeights(Dictionary<string, float> src)
        {
            if (src == null || src.Count == 0)
            {
                return;
            }
            float sum = 0f;
            float[] got = new float[RarityCount];
            bool any = false;
            for (int i = 0; i < RarityCount; i++)
            {
                float w;
                if (src.TryGetValue(WeightKey((TradeRarity)i), out w) && w >= 0f)
                {
                    got[i] = w;
                    sum += w;
                    any = true;
                }
                else
                {
                    got[i] = -1f;
                }
            }
            if (!any || sum <= 0f)
            {
                MFLog.Warn(LogCat.Trade, "[Space] rarity_weight 全为 0，用内置默认权重兜底");
                return;
            }
            for (int i = 0; i < RarityCount; i++)
            {
                Weights[i] = got[i] < 0f ? 0f : got[i];
            }
        }

        private static void ApplyTpi()
        {
            if (TpiTable.Count == 0)
            {
                return;
            }
            for (int i = 0; i < RankNames.Length; i++)
            {
                float v;
                if (TpiTable.TryGetValue(RankNames[i], out v))
                {
                    Tpi[i] = Mathf.Clamp(v, 0f, 0.5f);
                }
            }
        }

        private static string WeightKey(TradeRarity r)
        {
            switch (r)
            {
                case TradeRarity.Poor: return "poor";
                case TradeRarity.Bad: return "bad";
                case TradeRarity.Good: return "good";
                case TradeRarity.Master: return "master";
                case TradeRarity.Legendary: return "legendary";
                default: return "common";
            }
        }

        internal static float WeightOf(TradeRarity r)
        {
            int i = (int)r;
            return i < 0 || i >= Weights.Length ? 0f : Weights[i];
        }

        internal static float TpiOf(int rankIndex)
        {
            return rankIndex < 0 || rankIndex >= Tpi.Length ? 0f : Tpi[rankIndex];
        }

        internal static float TotalWeight()
        {
            float sum = 0f;
            for (int i = 0; i < RarityCount; i++)
            {
                sum += Weights[i];
            }
            return sum;
        }

        // 稀有度只看「站点加成前的价值」：不含 k、不含 TPI
        internal static TradeRarity TierOf(float price)
        {
            if (price < Bounds[0]) return TradeRarity.Poor;
            if (price < Bounds[1]) return TradeRarity.Bad;
            if (price < Bounds[2]) return TradeRarity.Common;
            if (price < Bounds[3]) return TradeRarity.Good;
            if (price < Bounds[4]) return TradeRarity.Master;
            return TradeRarity.Legendary;
        }

        internal static bool TryTag(string tag, out TagDef def)
        {
            def = null;
            return !string.IsNullOrEmpty(tag) && Tags.TryGetValue(tag, out def) && def != null;
        }

        internal static bool TryElementPrice(string id, out float price)
        {
            price = 0f;
            return !string.IsNullOrEmpty(id) && ElementPrice.TryGetValue(id, out price) && price > 0f;
        }

        // 分类标签卡上用的中文名（配置里还是英文 Tag；这里只为好读，认不出就原样返回）
        internal static string TagLabel(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return "其它";
            }
            switch (tag)
            {
                case "Edible": return "食物";
                case "CookingIngredient": return "烹饪原料";
                case "Dehydrated": return "脱水食物";
                case "Medicine": return "药品";
                case "MedicalSupplies": return "医疗物资";
                case "Seed": return "种子";
                case "Egg": return "蛋";
                case "Clothes": return "衣物";
                case "BionicUpgrade": return "仿生升级块";
                case "ChargedPortableBattery": return "便携电池";
                case "RefinedMetal": return "精炼金属";
                case "ManufacturedMaterial": return "人造材料";
                case "Insulator": return "绝缘体";
                case "IndustrialProduct": return "工业产品";
                case "ConsumableOre": return "消耗性矿石";
                case "RareMaterials": return "稀有材料";
                case "Artifact": return "遗物";
                case "BuildableRaw": return "建筑原料";
                case "BuildableProcessed": return "加工建材";
                case "Organics": return "有机物";
                case "Agriculture": return "农业材料";
                case "Farmable": return "可种植";
                case "Filter": return "过滤材料";
                case "ammo": return "弹药";
                case "TechComponents": return "科技组件";
                case "SpaceScroll": return "卷轴";
                case "RecipeScroll": return "卷轴";
                case "ModuleItem": return "战斗模块";
                case "IndustrialIngredient": return "工业原料";
                case "MoreFoodDrinkItem": return "饮品";
                default: return tag;
            }
        }

        // 热量公式：K = (卡路里 / 标卡) × (1 + x×(士气-2))
        internal static float EdibleMultiplier(float caloriesPerUnit, int quality, float x)
        {
            float ratio = caloriesPerUnit / CaloriesPerMark;
            if (ratio <= 0f)
            {
                return 0f;
            }
            float morale = 1f + x * (quality - 2);
            if (morale < 0.05f)
            {
                morale = 0.05f;
            }
            return ratio * morale;
        }

        internal static float[] TagStockRange(TagDef def)
        {
            return def != null && def.stock != null && def.stock.Length >= 2 ? def.stock : null;
        }
    }
}
