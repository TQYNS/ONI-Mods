using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 一个物品在「某站点」下的最终估值（逐字段短路后的结果）
    internal sealed class TradeItemValue
    {
        internal string id;
        internal float price;          // 价值 M（买价/卖价的基准，不含站点 k 与 TPI）
        internal TradeRarity rarity;
        internal bool hasStock;
        internal float stockMin;
        internal float stockMax;
        internal string primaryTag;    // 归类用：命中里价格最高的那条 Tag
    }

    internal sealed class StationCatalog
    {
        internal string stationId;
        internal readonly Dictionary<string, TradeItemValue> sell =
            new Dictionary<string, TradeItemValue>(StringComparer.Ordinal);
        internal readonly Dictionary<string, TradeItemValue> accept =
            new Dictionary<string, TradeItemValue>(StringComparer.Ordinal);
        internal readonly List<TradeItemValue> sellList = new List<TradeItemValue>();
        internal readonly List<TradeItemValue> acceptList = new List<TradeItemValue>();
        internal readonly Dictionary<TradeRarity, List<TradeItemValue>> sellByRarity =
            new Dictionary<TradeRarity, List<TradeItemValue>>();
    }

    // v5.8 第二/三层：预处理 ID 列表 + 有价无价分流 + ID 清单编译 + 站点池编译
    internal static class TradeCatalog
    {
        internal static readonly List<string> Prepared = new List<string>();
        internal static readonly Dictionary<string, KPrefabID> PrefabOf =
            new Dictionary<string, KPrefabID>(StringComparer.Ordinal);

        // 全局基类清单的 Group 展开索引（items.yaml）
        internal static readonly Dictionary<string, ItemListEntryDef> GlobalGroupOf =
            new Dictionary<string, ItemListEntryDef>(StringComparer.Ordinal);
        internal static readonly Dictionary<string, ItemListEntryDef> GlobalChildOf =
            new Dictionary<string, ItemListEntryDef>(StringComparer.Ordinal);

        private static readonly Dictionary<string, StationCatalog> Catalogs =
            new Dictionary<string, StationCatalog>(StringComparer.Ordinal);

        private static readonly HashSet<string> WarnedMissing = new HashSet<string>(StringComparer.Ordinal);

        internal static bool built;
        internal static int builtVersion = -1;

        internal static void Invalidate()
        {
            built = false;
            builtVersion = -1;
            Prepared.Clear();
            PrefabOf.Clear();
            GlobalGroupOf.Clear();
            GlobalChildOf.Clear();
            Catalogs.Clear();
        }

        internal static void EnsureBuilt()
        {
            if (built && builtVersion == GlobalCacheVersion.TRADE_VERSION)
            {
                return;
            }
            built = true;
            builtVersion = GlobalCacheVersion.TRADE_VERSION;
            Prepared.Clear();
            PrefabOf.Clear();
            Catalogs.Clear();
            WarnedMissing.Clear();

            TradeDb.Load();
            TradeValuation.Init();
            BuildGlobalIndex();
            ScanPrefabs();

            MFLog.Info(LogCat.Space, "[Space] 贸易准入：预处理 ID 列表 " + Prepared.Count + " 条"
                + "（可拾取 ∩ 有效 Tag；ban " + TradeDb.BanIds.Count
                + "，黑名单 Tag " + TradeValuation.Blacklist.Count + "）");
        }

        private static void BuildGlobalIndex()
        {
            GlobalGroupOf.Clear();
            GlobalChildOf.Clear();
            foreach (KeyValuePair<string, ItemListEntryDef> kv in TradeDb.GlobalItems)
            {
                ItemListEntryDef def = kv.Value;
                if (def == null || def.ids == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, ItemListEntryDef> sub in def.ids)
                {
                    if (string.IsNullOrEmpty(sub.Key))
                    {
                        continue;
                    }
                    GlobalGroupOf[sub.Key] = def;
                    if (sub.Value != null)
                    {
                        GlobalChildOf[sub.Key] = sub.Value;
                    }
                }
            }
        }

        // 准入 = 有 Pickupable 组件 AND 命中单表 tags 至少一条有效 Tag
        private static void ScanPrefabs()
        {
            List<GameObject> all;
            try
            {
                all = Assets.GetPrefabsWithComponent<Pickupable>();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 枚举可拾取 prefab 失败（贸易准入表为空，商店不会有货）");
                return;
            }
            if (all == null)
            {
                return;
            }
            List<string> gasFilteredNow = null;
            for (int i = 0; i < all.Count; i++)
            {
                GameObject go = all[i];
                if (go == null)
                {
                    continue;
                }
                KPrefabID pid = go.GetComponent<KPrefabID>();
                if (pid == null)
                {
                    continue;
                }
                string id = pid.PrefabTag.Name;
                if (string.IsNullOrEmpty(id) || TradeDb.BanIds.Contains(id))
                {
                    continue;
                }
                if (HasAnyTag(pid, TradeValuation.Blacklist))
                {
                    continue;
                }
                // 剔除"元素类"的气态 / 液态（含熔融）物质 —— 用户实测交易列表里混进了
                if (IsGasOrLiquid(pid) && !pid.HasTag(new Tag("MoreFoodDrinkItem")))
                {
                    if (GasFiltered.Add(id))
                    {
                        if (gasFilteredNow == null) gasFilteredNow = new List<string>();
                        gasFilteredNow.Add(id);      // 汇总成一行再打，避免每条刷屏（原实现一次刷 80+ 行）
                    }
                    continue;
                }
                if (!HitsValidTag(pid))
                {
                    continue;
                }
                if (PrefabOf.ContainsKey(id))
                {
                    continue;
                }
                PrefabOf[id] = pid;
                Prepared.Add(id);
            }
            Prepared.Sort(StringComparer.Ordinal);

            if (gasFilteredNow != null && gasFilteredNow.Count > 0)
            {
                int show = gasFilteredNow.Count > 6 ? 6 : gasFilteredNow.Count;
                string head = string.Join(" / ", gasFilteredNow.GetRange(0, show).ToArray());
                MFLog.Info(LogCat.Space, "[Space] 贸易准入：剔除元素类气/液物质 " + gasFilteredNow.Count + " 种（"
                    + head + (gasFilteredNow.Count > show ? " …" : "") + "）");
            }
        }

        private static readonly HashSet<string> GasFiltered = new HashSet<string>(StringComparer.Ordinal);

        // 元素是气/液（含熔融金属）的 prefab —— 贸易一律不收（饮品除外，调用点判）
        private static bool IsGasOrLiquid(KPrefabID pid)
        {
            try
            {
                PrimaryElement pe = pid.GetComponent<PrimaryElement>();
                if (pe == null || pe.Element == null)
                {
                    return false;
                }
                return pe.Element.IsGas || pe.Element.IsLiquid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasAnyTag(KPrefabID pid, HashSet<string> tags)
        {
            if (tags == null || tags.Count == 0)
            {
                return false;
            }
            foreach (string t in tags)
            {
                if (!string.IsNullOrEmpty(t) && pid.HasTag(new Tag(t)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HitsValidTag(KPrefabID pid)
        {
            foreach (KeyValuePair<string, TagDef> kv in TradeValuation.Tags)
            {
                if (pid.HasTag(new Tag(kv.Key)))
                {
                    return true;
                }
            }
            return false;
        }

        internal static StationCatalog Catalog(StationDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
            {
                return null;
            }
            EnsureBuilt();
            StationCatalog c;
            if (Catalogs.TryGetValue(def.id, out c) && c != null)
            {
                return c;
            }
            c = Compile(def);
            Catalogs[def.id] = c;
            return c;
        }

        private static StationCatalog Compile(StationDef def)
        {
            StationCatalog c = new StationCatalog();
            c.stationId = def.id;

            List<string> sellIds = FilterPool(def, def.sell_tags, def.sell_ban_tags,
                def.sell_ids, def.sell_ban_ids, "售卖");
            List<string> acceptIds = FilterPool(def, def.accept_tags, def.accept_ban_tags,
                def.accept_ids, def.accept_ban_ids, "回收");

            for (int i = 0; i < sellIds.Count; i++)
            {
                TradeItemValue v = Resolve(sellIds[i], def);
                if (v == null || !v.hasStock)
                {
                    continue;
                }
                c.sell[v.id] = v;
                c.sellList.Add(v);
            }
            // 回收不设上限：凡解析出价格的收购项都进清单，星币是唯一闸门
            for (int i = 0; i < acceptIds.Count; i++)
            {
                TradeItemValue v = Resolve(acceptIds[i], def);
                if (v == null)
                {
                    continue;
                }
                c.accept[v.id] = v;
                c.acceptList.Add(v);
            }

            for (int i = 0; i < c.sellList.Count; i++)
            {
                TradeItemValue v = c.sellList[i];
                List<TradeItemValue> bucket;
                if (!c.sellByRarity.TryGetValue(v.rarity, out bucket) || bucket == null)
                {
                    bucket = new List<TradeItemValue>();
                    c.sellByRarity[v.rarity] = bucket;
                }
                bucket.Add(v);
            }
            MFLog.Info(LogCat.Space, "[Space] 站点池编译 " + def.id + "：可卖 " + c.sellList.Count
                + " / 可收 " + c.acceptList.Count + "（回收不设上限）");
            return c;
        }

        // 并集语义（Q3 裁决）：池 = (预处理 ∩ Tag白名单 − Tag黑名单) ∪ ID白名单 − ID黑名单
        private static List<string> FilterPool(StationDef def, string[] allowTags, string[] banTags,
            string[] addIds, string[] banIds, string label)
        {
            HashSet<string> banIdSet = ToSet(banIds);
            HashSet<string> pool = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Prepared.Count; i++)
            {
                string id = Prepared[i];
                if (banIdSet.Contains(id))
                {
                    continue;
                }
                if (!TagPass(id, allowTags, banTags))
                {
                    continue;
                }
                pool.Add(id);
            }
            if (addIds != null)
            {
                for (int i = 0; i < addIds.Length; i++)
                {
                    string id = addIds[i];
                    if (string.IsNullOrEmpty(id) || banIdSet.Contains(id))
                    {
                        continue;
                    }
                    if (Assets.TryGetPrefab(id) == null)
                    {
                        if (WarnedMissing.Add("id:" + id))
                        {
                            Debug.LogWarning("[Space] " + label + "清单里的 id 在游戏里不存在，已跳过：" + id);
                        }
                        continue;
                    }
                    pool.Add(id);
                }
            }
            List<string> list = new List<string>(pool);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        private static HashSet<string> ToSet(string[] arr)
        {
            HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    if (!string.IsNullOrEmpty(arr[i]))
                    {
                        set.Add(arr[i]);
                    }
                }
            }
            return set;
        }

        private static bool TagPass(string id, string[] allowTags, string[] banTags)
        {
            KPrefabID pid;
            if (!PrefabOf.TryGetValue(id, out pid) || pid == null)
            {
                return false;
            }
            if (banTags != null)
            {
                for (int i = 0; i < banTags.Length; i++)
                {
                    if (!string.IsNullOrEmpty(banTags[i]) && pid.HasTag(new Tag(banTags[i])))
                    {
                        return false;   // ban 压倒 allow
                    }
                }
            }
            if (allowTags == null || allowTags.Length == 0)
            {
                return true;            // 白名单空 = ALL
            }
            for (int i = 0; i < allowTags.Length; i++)
            {
                if (!string.IsNullOrEmpty(allowTags[i]) && pid.HasTag(new Tag(allowTags[i])))
                {
                    return true;
                }
            }
            return false;
        }

        // 逐字段短路定价：yaml 显式 → 元素价 → 热量公式 → Tag 最高价 → 无身份价不进表
        internal static TradeItemValue Resolve(string id, StationDef def)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            KPrefabID pid;
            if (!PrefabOf.TryGetValue(id, out pid) || pid == null)
            {
                GameObject go = Assets.TryGetPrefab(id);
                if (go == null)
                {
                    return null;
                }
                pid = go.GetComponent<KPrefabID>();
                if (pid == null)
                {
                    return null;
                }
                PrefabOf[id] = pid;
            }

            ItemListEntryDef stDirect = null;
            ItemListEntryDef stChild = null;
            ItemListEntryDef stGroup = null;
            if (def != null && def.items != null)
            {
                def.items.TryGetValue(id, out stDirect);
                foreach (KeyValuePair<string, ItemListEntryDef> kv in def.items)
                {
                    if (kv.Value == null || kv.Value.ids == null)
                    {
                        continue;
                    }
                    ItemListEntryDef child;
                    if (kv.Value.ids.TryGetValue(id, out child))
                    {
                        stGroup = kv.Value;
                        stChild = child;
                        break;
                    }
                }
            }
            ItemListEntryDef glDirect;
            TradeDb.GlobalItems.TryGetValue(id, out glDirect);
            ItemListEntryDef glGroup;
            GlobalGroupOf.TryGetValue(id, out glGroup);
            ItemListEntryDef glChild;
            GlobalChildOf.TryGetValue(id, out glChild);

            ItemListEntryDef[] chain = new ItemListEntryDef[]
            {
                stDirect, stChild, stGroup, glDirect, glChild, glGroup
            };

            TradeItemValue v = new TradeItemValue();
            v.id = id;

            // 单表一次遍历：同时取最高身份价、最高库存；Edible 单独记住
            float bestTagPrice = 0f;
            float[] bestStock = null;
            TagDef edibleHit = null;
            foreach (KeyValuePair<string, TagDef> kv in TradeValuation.Tags)
            {
                if (!pid.HasTag(new Tag(kv.Key)))
                {
                    continue;
                }
                TagDef t = kv.Value;
                if (t.price > bestTagPrice)
                {
                    bestTagPrice = t.price;
                    v.primaryTag = kv.Key;
                }
                if (kv.Key == "Edible")
                {
                    edibleHit = t;
                }
                if (GreaterRange(t.stock, bestStock))
                {
                    bestStock = t.stock;
                }
            }

            // 价格：命中链 → 元素价 → 热量公式 → Tag 最高价 → 无身份价不进表
            float? chainPrice = FirstPrice(chain);
            float elementVal;
            bool hasElement = TradeValuation.TryElementPrice(pid.PrefabTag.Name, out elementVal);
            if (chainPrice.HasValue)
            {
                v.price = chainPrice.Value;
            }
            else if (hasElement)
            {
                v.price = elementVal;
            }
            else if (edibleHit != null)
            {
                float kcal;
                int quality;
                if (TryEdible(pid, out kcal, out quality))
                {
                    float k = TradeValuation.EdibleMultiplier(kcal, quality, edibleHit.x);
                    v.price = k > 0f ? edibleHit.price * k : edibleHit.price;
                }
                else
                {
                    v.price = edibleHit.price;
                }
            }
            else if (bestTagPrice > 0f)
            {
                v.price = bestTagPrice;
            }
            else
            {
                return null;
            }
            v.price = Mathf.Max(0f, TradeNumber.Money(v.price));
            if (v.price <= 0f)
            {
                return null;
            }

            // 库存（站点卖给你的量）：命中链优先，否则取命中 Tag 的最高区间
            float[] chainStock = FirstRange(chain);
            float[] stock = chainStock != null ? chainStock : bestStock;
            if (stock != null)
            {
                v.stockMin = TradeNumber.Units(stock[0]);
                v.stockMax = Mathf.Max(v.stockMin, TradeNumber.Units(stock.Length > 1 ? stock[1] : stock[0]));
                v.hasStock = v.stockMax > 0f;
            }

            // 回收不设上限：不读配额，星币是唯一闸门。

            // 稀有度（ID 显式 tier 覆盖价格分段）
            string tier = FirstTier(chain);
            v.rarity = string.IsNullOrEmpty(tier) ? TradeValuation.TierOf(v.price) : TradeDb.ParseRarity(tier);

            return v;
        }

        // 区间比较：先比上限，再比下限；整段取走，不拆开拼接
        private static bool GreaterRange(float[] a, float[] b)
        {
            if (a == null || a.Length < 2)
            {
                return false;
            }
            if (b == null || b.Length < 2)
            {
                return true;
            }
            if (a[1] != b[1])
            {
                return a[1] > b[1];
            }
            return a[0] > b[0];
        }

        private static bool TryEdible(KPrefabID pid, out float calories, out int quality)
        {
            calories = 0f;
            quality = 0;
            try
            {
                Edible ed = pid.GetComponent<Edible>();
                if (ed == null || ed.FoodInfo == null)
                {
                    return false;
                }
                calories = ed.FoodInfo.CaloriesPerUnit;
                quality = ed.FoodInfo.Quality;
                return calories > 0f;
            }
            catch (Exception e)
            {
                MFLog.Exc("trade:foodinfo:" + pid.PrefabTag.Name, LogCat.Trade, e,
                    "[Space] 读 FoodInfo 失败（{0}，该物品不会出现在贸易清单）", pid.PrefabTag.Name);
                return false;
            }
        }

        private static float? FirstPrice(ItemListEntryDef[] chain)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] != null && chain[i].price.HasValue)
                {
                    return chain[i].price.Value;
                }
            }
            return null;
        }

        private static float[] FirstRange(ItemListEntryDef[] chain)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] == null)
                {
                    continue;
                }
                float[] r = chain[i].stock;
                if (r != null && r.Length >= 1)
                {
                    return r;
                }
            }
            return null;
        }

        private static string FirstTier(ItemListEntryDef[] chain)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] != null && !string.IsNullOrEmpty(chain[i].tier))
                {
                    return chain[i].tier;
                }
            }
            return null;
        }
    }
}
