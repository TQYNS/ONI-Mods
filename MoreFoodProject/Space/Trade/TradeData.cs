using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    // v5.8：稀有度 6 档（极差/很差/一般/精良/大师/传奇），由价格走 rarity_bounds 分段，ID 清单可覆盖
    internal enum TradeRarity
    {
        Poor = 0,       // 极差
        Bad = 1,        // 很差
        Common = 2,     // 一般
        Good = 3,       // 精良
        Master = 4,     // 大师
        Legendary = 5   // 传奇
    }

    internal sealed class TradeCfgFile
    {
        public int currency_start { get; set; }
        public float refresh_seconds { get; set; }
        public int max_stations_main { get; set; }
        public int max_stations_sub { get; set; }
        public int hunter_gift { get; set; }
        public string hunter_station_id { get; set; }
        public int star_map_rings { get; set; }

        public void FillDefaults()
        {
            if (refresh_seconds <= 0f) refresh_seconds = 4200f;
            if (max_stations_main <= 0) max_stations_main = 6;
            if (max_stations_sub <= 0) max_stations_sub = 7;
            if (hunter_gift <= 0) hunter_gift = 800;
            if (star_map_rings == 0) star_map_rings = 27;
        }
    }

    // valuation.yaml 单表：每条 Tag 提供身份价 / 数量区间（全为属性）
    internal sealed class TagDef
    {
        public float price { get; set; }
        public float x { get; set; }
        public float[] stock { get; set; }
    }

    internal sealed class ValuationFileDef
    {
        public Dictionary<string, TagDef> tags { get; set; }
        public Dictionary<string, float> element_prices { get; set; }
        public Dictionary<string, float> tpi { get; set; }
        public Dictionary<string, float> rarity_bounds { get; set; }
        public Dictionary<string, float> rarity_weight { get; set; }
        public string[] blacklist_tags { get; set; }
    }

    // items.yaml / 站点子清单
    internal sealed class ItemListEntryDef
    {
        public float? price { get; set; }
        public float[] stock { get; set; }        // [下限, 上限]
        public string tier { get; set; }
        public Dictionary<string, ItemListEntryDef> ids { get; set; }   // 有 Group 即为组
    }

    // ban.yaml
    internal sealed class BanFileDef
    {
        public string[] ids { get; set; }
    }

    // donation.yaml（捐款 / 荣誉点）
    internal sealed class DonationChannelDef
    {
        public string key { get; set; }
        public string name { get; set; }
        public string icon { get; set; }             // 渠道图标（物品 id 或纯图标条目 id，如 HonorPoint）
        public string kind { get; set; }             // money | food
        public float credit_per_point { get; set; }  // money：多少星币 = 1 荣誉点
        public float points_per_unit { get; set; }   // food：每份荣誉点
        public float? min_quality { get; set; }      // food：品质下限
        public float? max_quality { get; set; }      // food：品质上限
        public int cap { get; set; }                 // 本渠道累计荣誉点上限
        public int per_item_limit { get; set; }      // 单 ID 每次最多几份（0 = 不限）
    }

    internal sealed class DonationStationDef
    {
        public List<DonationChannelDef> channels { get; set; }
    }

    internal sealed class DonationFileDef
    {
        public Dictionary<string, DonationStationDef> stations { get; set; }
        // 站点 → (物品 id → 份数上限)，覆盖渠道里的 per_item_limit
        public Dictionary<string, Dictionary<string, int>> item_limits { get; set; }
    }

    // station/*.yaml
    internal sealed class StationDef
    {
        public string id { get; set; }
        public string name { get; set; }
        public string kind { get; set; }              // main / blackmarket / sub
        public float k { get; set; }
        public bool fuel_refill { get; set; } = true;   // 所有空间交易站默认支持加油；要关某站就在 yaml 里写 false
        public bool pirate { get; set; }
        public float close_chance { get; set; }
        public float close_seconds { get; set; }

        // 动画：单个 state（字符串）或一组 state（数组，随机抽一个）；不写 → 按 kind 从全局组抽
        public object anim { get; set; }

        // 卫星贴图组覆盖：不写 = 海盗站走 pirate、其余走 normal（见 trade/anim/*.yaml）
        public string anim_group { get; set; }

        // 模块抽取上限（不写按站点类型默认：普通 传奇1/大师3；黑市/走私/海盗 传奇2/大师4）
        public int? module_legendary_max { get; set; }
        public int? module_master_max { get; set; }

        // v5.8 站点字段
        public float[] currency_stock { get; set; }   // [下限, 上限]，每次刷新重掷
        public float refresh_days { get; set; }
        public object entries { get; set; }           // 单值 或 [下限, 上限]
        public Dictionary<string, int> extra_picks { get; set; }

        public string[] sell_tags { get; set; }
        public string[] sell_ban_tags { get; set; }
        public string[] accept_tags { get; set; }
        public string[] accept_ban_tags { get; set; }
        public string[] sell_ids { get; set; }
        public string[] sell_ban_ids { get; set; }
        public string[] accept_ids { get; set; }
        public string[] accept_ban_ids { get; set; }

        public Dictionary<string, ItemListEntryDef> items { get; set; }   // 站点子清单：覆盖全局基类 / 新增

        internal string[] AnimStates()
        {
            return YamlValue.ToStrings(anim);
        }

        internal int[] EntryRange()
        {
            return YamlValue.ToInts(entries);
        }

        internal bool IsPirate
        {
            get { return pirate || (kind != null && kind == "pirate"); }
        }

        internal string AnimGroupName
        {
            get
            {
                if (!string.IsNullOrEmpty(anim_group))
                {
                    return anim_group;
                }
                return IsPirate ? "pirate" : "normal";
            }
        }

        private bool IsLuxury
        {
            get
            {
                return (kind != null && kind == "blackmarket")
                    || (id != null && (id.StartsWith("blackmarket", StringComparison.Ordinal)
                        || id.StartsWith("smuggler", StringComparison.Ordinal)
                        || id.StartsWith("pirate", StringComparison.Ordinal)));
            }
        }

        internal int ModuleLegendaryMax
        {
            get { return module_legendary_max ?? (IsLuxury ? 2 : 1); }
        }

        internal int ModuleMasterMax
        {
            get { return module_master_max ?? (IsLuxury ? 4 : 3); }
        }
    }

    internal sealed class StationFileDef
    {
        public List<StationDef> stations { get; set; }
    }

    // trade/anim/*.yaml（卫星贴图候选清单）
    internal sealed class SatelliteAnimGroupDef
    {
        public string group { get; set; }        // normal / pirate
        public string anim_state { get; set; }   // 候选文件的动画状态名（这批资产统一 object）
        public string[] anims { get; set; }      // 候选动画文件（xxx_kanim）
    }

    // 其它（沿用）
    internal sealed class FuelEntryDef
    {
        public string engine { get; set; }
        public string fuel_tag { get; set; }
        public string name { get; set; }          // 界面显示的中文名；留空则按元素名回退
        public float price_per_kg { get; set; }
    }

    internal sealed class FuelFileDef
    {
        public List<FuelEntryDef> fuels { get; set; }
    }

    // 氧化剂价目：fuel_tag = 元素 tag（OxyRock/Fertilizer/LiquidOxygen 等），实际可用种类由火箭氧化剂舱的过滤器决定
    internal sealed class OxidizerEntryDef
    {
        public string fuel_tag { get; set; }
        public string name { get; set; }
        public float price_per_kg { get; set; }
    }

    internal sealed class OxidizerFileDef
    {
        public List<OxidizerEntryDef> oxidizers { get; set; }
    }

    // 任务不再有本地表：委托数据全部来自事件 yaml（hunt: 规格 + variants 文案）

    // YAML 单值/数组两写法归一化（Klei.YamlIO 底层是 YamlDotNet：标量喂给数组会抛异常，
    internal static class YamlValue
    {
        internal static string[] ToStrings(object v)
        {
            if (v == null)
            {
                return null;
            }
            string s = v as string;
            if (s != null)
            {
                return new string[] { s };
            }
            System.Collections.IEnumerable e = v as System.Collections.IEnumerable;
            if (e == null)
            {
                return null;
            }
            List<string> list = new List<string>();
            foreach (object o in e)
            {
                if (o != null)
                {
                    list.Add(o.ToString());
                }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        internal static int[] ToInts(object v)
        {
            if (v == null)
            {
                return null;
            }
            if (v is string)
            {
                int one;
                if (int.TryParse((string)v, out one))
                {
                    return new int[] { one };
                }
                return null;
            }
            System.Collections.IEnumerable e = v as System.Collections.IEnumerable;
            if (e == null)
            {
                return null;
            }
            List<int> list = new List<int>();
            foreach (object o in e)
            {
                if (o == null)
                {
                    continue;
                }
                int n;
                if (o is int)
                {
                    list.Add((int)o);
                }
                else if (int.TryParse(o.ToString(), out n))
                {
                    list.Add(n);
                }
            }
            return list.Count > 0 ? list.ToArray() : null;
        }
    }

    internal sealed class TradeDb
    {
        internal static TradeCfgFile Config = new TradeCfgFile();
        internal static readonly List<StationDef> Stations = new List<StationDef>();
        internal static readonly List<FuelEntryDef> Fuels = new List<FuelEntryDef>();
        internal static readonly List<OxidizerEntryDef> Oxidizers = new List<OxidizerEntryDef>();

        // v5.8：valuation.yaml / items.yaml / ban.yaml
        internal static ValuationFileDef Valuation = new ValuationFileDef();
        internal static readonly Dictionary<string, ItemListEntryDef> GlobalItems =
            new Dictionary<string, ItemListEntryDef>(StringComparer.Ordinal);
        internal static readonly HashSet<string> BanIds = new HashSet<string>(StringComparer.Ordinal);
        internal static DonationFileDef Donation = new DonationFileDef();

        // 卫星贴图组：group → 候选 kanim 文件清单（trade/anim/*.yaml）
        internal static readonly Dictionary<string, SatelliteAnimGroupDef> SatelliteAnims =
            new Dictionary<string, SatelliteAnimGroupDef>(StringComparer.Ordinal);

        internal static bool loaded;

        internal static string RootDir
        {
            get
            {
                try
                {
                    return Path.Combine(Path.GetDirectoryName(typeof(TradeDb).Assembly.Location), "trade");
                }
                catch (Exception)
                {
                    return "trade";
                }
            }
        }

        internal static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            ContentDiag.Begin("trade", LogCat.Trade);
            try
            {
                LoadInternal();
            }
            catch (Exception e)
            {
                // 以前这里没有兜底 —— 解析中途抛一次就整块配置半死（ban/items 全废），
                loaded = false;
                MFLog.Exc(LogCat.Trade, e, "[Space] 贸易配置加载中断（下次会重试）");
            }
            finally
            {
                ContentDiag.End("trade");
            }
        }

        private static void LoadInternal()
        {
            string root = RootDir;
            TradeCfgFile cfg = LoadOne<TradeCfgFile>(Path.Combine(root, "config.yaml"));
            if (cfg != null)
            {
                Config = cfg;
            }
            Config.FillDefaults();

            // 先换成一个全新的空表再合并：以前把已装载的 Valuation 同时当 dst 和 src，
            Valuation = new ValuationFileDef();
            string valPath = Path.Combine(root, "valuation.yaml");
            ValuationFileDef val = LoadOne<ValuationFileDef>(valPath);
            if (val != null)
            {
                ApplyValuation(val, "本地", valPath);
            }

            string banPath = Path.Combine(root, "ban.yaml");
            BanFileDef ban = LoadOne<BanFileDef>(banPath);
            if (ban != null)
            {
                ApplyBan(ban, "本地", banPath);
            }

            // 捐款/荣誉点独立成文件（渠道的每份荣誉点、每渠道上限、单 ID 份数限制）
            Donation = new DonationFileDef();
            string donPath = Path.Combine(root, "donation.yaml");
            DonationFileDef don = LoadOne<DonationFileDef>(donPath);
            if (don != null)
            {
                Donation = don;
            }
            MFLog.Info(LogCat.Trade,"[Space] 捐款配置：站点 " + DonationStationCount() + " 个 / 渠道 "
                + DonationChannelCount() + " 条");

            string itemsPath = Path.Combine(root, "items.yaml");
            Dictionary<string, ItemListEntryDef> items =
                LoadOne<Dictionary<string, ItemListEntryDef>>(itemsPath);
            if (items != null)
            {
                ApplyItems(items, "本地", itemsPath);
            }

            int fuelFiles = LoadDir<FuelFileDef>(Path.Combine(root, "fuel"),
                delegate(FuelFileDef f, string file) { ApplyFuels(f, "本地", file); },
                delegate(string file) { return !file.EndsWith("oxidizers.yaml", StringComparison.OrdinalIgnoreCase); });
            string oxPath = Path.Combine(root, "fuel", "oxidizers.yaml");
            OxidizerFileDef oxFile = LoadOne<OxidizerFileDef>(oxPath);
            if (oxFile != null)
            {
                ApplyOxidizers(oxFile, "本地", oxPath);
            }
            int stationFiles = LoadDir<StationFileDef>(Path.Combine(root, "station"),
                delegate(StationFileDef f, string file) { ApplyStations(f, "本地", file); });
            int animGroupFiles = LoadDir<SatelliteAnimGroupDef>(Path.Combine(root, "anim"),
                delegate(SatelliteAnimGroupDef f, string file) { ApplySatelliteAnimGroup(f, "本地", file); });

            MFLog.Info(LogCat.Trade,"[Space] 贸易配置：valuation(Tag " + TagCount()
                + " 个 / 分段 " + BoundCount() + ") items(" + GlobalItems.Count
                + ") ban(" + BanIds.Count
                + ") fuel " + fuelFiles + "（" + Fuels.Count
                + "）oxidizer " + Oxidizers.Count
                + " station " + stationFiles + "（" + Stations.Count
                + "）satellite-anim " + animGroupFiles + "（" + SatelliteAnims.Count + " 组）");
            if (Stations.Count == 0)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 没读到站点配置（trade/station/*.yaml）→ 启用内置兜底站点（6 个），"
                    + "保证星图上仍有站点可测");
                ContentDiag.File("trade", "trade/station/", "没有读到任何站点条目",
                    "补 station yaml；本次已用内置 6 个兜底站点，自定义站点不会出现");
                AddFallbackStations();
            }
        }

        // 路径归一化：Mono 的 Assembly.Location / Mod.ContentPath 可能一个用 '/' 一个用 '\'
        internal static string NormDir(string p)
        {
            if (string.IsNullOrEmpty(p))
            {
                return "";
            }
            return p.Replace('\\', '/').TrimEnd('/');
        }

        private static int TagCount()
        {
            return Valuation.tags == null ? 0 : Valuation.tags.Count;
        }

        private static int BoundCount()
        {
            return Valuation.rarity_bounds == null ? 0 : Valuation.rarity_bounds.Count;
        }

        private static void AddFallbackStations()
        {
            string[][] defs = new string[][]
            {
                new string[] { "hunter_assoc", "美食猎人协会", "main" },
                new string[] { "trade_general_food", "星际贸易站·食材", "main" },
                new string[] { "trade_general_ore", "星际贸易站·矿产", "main" },
                new string[] { "blackmarket_main", "黑市港", "blackmarket" },
                new string[] { "smuggler_port", "走私港", "sub" },
                new string[] { "pirate_station", "海盗站", "sub" }
            };
            for (int i = 0; i < defs.Length; i++)
            {
                StationDef s = new StationDef();
                s.id = defs[i][0];
                s.name = defs[i][1];
                s.kind = defs[i][2];
                s.k = 0.25f;
                s.fuel_refill = true;
                s.currency_stock = new float[] { 800f, 1500f };
                s.refresh_days = 7f;
                s.entries = new int[] { 12, 18 };
                Stations.Add(s);
            }
        }

        // accept 用来排除「同目录但结构不同」的文件（如 fuel/oxidizers.yaml），避免被当成 FuelFileDef 报未知字段
        private static int LoadDir<T>(string dir, Action<T, string> apply, Func<string, bool> accept = null) where T : class
        {
            if (!Directory.Exists(dir))
            {
                MFLog.Verbose(LogCat.Trade, "[Space] 目录不存在，跳过：" + dir);
                return 0;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.yaml");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 枚举目录失败 {0}", dir);
                ContentDiag.File("trade", dir, "目录枚举失败：" + e.Message, "检查目录权限或是否被其它程序占用");
                return 0;
            }
            int ok = 0;
            for (int i = 0; i < files.Length; i++)
            {
                if (accept != null && !accept(files[i]))
                {
                    continue;
                }
                T one = LoadOne<T>(files[i]);
                if (one == null)
                {
                    continue;
                }
                try
                {
                    apply(one, files[i]);
                    ok++;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Trade, e, "[Space] {0} 解析后处理失败", Path.GetFileName(files[i]));
                    ContentDiag.File("trade", files[i], "内容应用阶段抛异常：" + e.Message, "该文件已跳过，前面几条可能已生效");
                }
            }
            return ok;
        }

        private static T LoadOne<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 读取失败 {0}", path);
                ContentDiag.File("trade", path, "读取失败：" + e.Message, "检查文件是否存在、是否被占用、编码是否为 UTF-8");
                return null;
            }
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                ContentDiag.File("trade", path, "空文件或只有空白", "删掉该文件，或按文档补齐内容");
                return null;
            }
            return ParseOne<T>(text, path);
        }

        // 铁规：配置错误一律走 ContentDiag（Warn），绝不 LogError（LogCatcher 会把 LogError 变崩溃）。
        private static T ParseOne<T>(string text, string path) where T : class
        {
            return YamlLoad.Parse<T>(text, path, "trade");
        }

        private static int IndexOfStations(string id)
        {
            for (int i = 0; i < Stations.Count; i++)
            {
                if (Stations[i].id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int IndexOfFuels(string fuelTag)
        {
            for (int i = 0; i < Fuels.Count; i++)
            {
                if (Fuels[i].fuel_tag == fuelTag)
                {
                    return i;
                }
            }
            return -1;
        }

        internal static void ApplyValuation(ValuationFileDef f, string src, string file)
        {
            if (f == null)
            {
                return;
            }
            Valuation.tags = MergeTags(Valuation.tags, f.tags, src, "tags", file);
            Valuation.element_prices = MergeFloats(Valuation.element_prices, f.element_prices);
            Valuation.tpi = MergeFloats(Valuation.tpi, f.tpi);
            Valuation.rarity_bounds = MergeFloats(Valuation.rarity_bounds, f.rarity_bounds);
            Valuation.rarity_weight = MergeFloats(Valuation.rarity_weight, f.rarity_weight);
            if (f.blacklist_tags != null && f.blacklist_tags.Length > 0)
            {
                Valuation.blacklist_tags = f.blacklist_tags;
            }
        }

        private static Dictionary<string, T> MergeTags<T>(Dictionary<string, T> dst,
            Dictionary<string, T> src, string label, string what, string file) where T : class
        {
            if (src == null || src.Count == 0)
            {
                return dst;
            }
            if (dst == null || ReferenceEquals(dst, src))
            {
                // 防御：dst 与 src 是同一个对象时不能边枚举边写（Mono 会抛 Collection was modified）
                dst = new Dictionary<string, T>(StringComparer.Ordinal);
            }
            foreach (KeyValuePair<string, T> kv in src)
            {
                if (kv.Value == null)
                {
                    ContentDiag.Skip("trade", file, "「" + kv.Key + "」", what + " 条目只有键没有值", "补上该标签的数值，或删掉该键");
                    continue;
                }
                if (dst.ContainsKey(kv.Key))
                {
                    MFLog.Info(LogCat.Trade,"[Space] valuation 覆盖 " + what + "：" + kv.Key + " ← " + label);
                }
                dst[kv.Key] = kv.Value;
            }
            return dst;
        }

        private static Dictionary<string, float> MergeFloats(Dictionary<string, float> dst,
            Dictionary<string, float> src)
        {
            if (src == null || src.Count == 0)
            {
                return dst;
            }
            if (dst == null)
            {
                dst = new Dictionary<string, float>(StringComparer.Ordinal);
            }
            foreach (KeyValuePair<string, float> kv in src)
            {
                dst[kv.Key] = kv.Value;
            }
            return dst;
        }

        internal static void ApplyBan(BanFileDef f, string src, string file)
        {
            if (f == null || f.ids == null)
            {
                return;
            }
            for (int i = 0; i < f.ids.Length; i++)
            {
                if (string.IsNullOrEmpty(f.ids[i]))
                {
                    ContentDiag.Skip("trade", file, "#" + (i + 1), "ban 清单里有空值", "删掉该行，或补上要禁用的 tag");
                    continue;
                }
                if (BanIds.Add(f.ids[i]))
                {
                    MFLog.Info(LogCat.Trade,"[Space] ban 清单加入：" + f.ids[i] + " ← " + src);
                }
            }
        }

        internal static void ApplyItems(Dictionary<string, ItemListEntryDef> f, string src, string file)
        {
            if (f == null)
            {
                return;
            }
            foreach (KeyValuePair<string, ItemListEntryDef> kv in f)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                {
                    ContentDiag.Skip("trade", file, "「" + (string.IsNullOrEmpty(kv.Key) ? "(空键)" : kv.Key) + "」",
                        "条目没有 id 或值为空", "把物品 id 写在键上，值填条目字段");
                    continue;
                }
                if (GlobalItems.ContainsKey(kv.Key))
                {
                    MFLog.Info(LogCat.Trade,"[Space] items 覆盖：" + kv.Key + " ← " + src);
                }
                GlobalItems[kv.Key] = kv.Value;
            }
        }

        private static void ApplyFuels(FuelFileDef f, string src, string file)
        {
            if (f == null || f.fuels == null)
            {
                return;
            }
            for (int i = 0; i < f.fuels.Count; i++)
            {
                FuelEntryDef it = f.fuels[i];
                if (it == null || string.IsNullOrEmpty(it.fuel_tag))
                {
                    ContentDiag.Skip("trade", file, "#" + (i + 1), "缺少 fuel_tag", "补 fuel_tag: <燃料 tag>（如 Solid）");
                    continue;
                }
                if (it.price_per_kg <= 0f)
                {
                    ContentDiag.Fix("trade", file, "「" + it.fuel_tag + "」", "price_per_kg=" + it.price_per_kg + "（必须 > 0）", "1");
                    it.price_per_kg = 1f;
                }
                int at = IndexOfFuels(it.fuel_tag);
                if (at >= 0)
                {
                    Fuels[at] = it;
                    MFLog.Info(LogCat.Trade,"[Space] fuel 覆盖：" + it.fuel_tag + " ← " + src);
                }
                else
                {
                    Fuels.Add(it);
                }
            }
        }

        private static void ApplyOxidizers(OxidizerFileDef f, string src, string file)
        {
            if (f == null || f.oxidizers == null)
            {
                return;
            }
            for (int i = 0; i < f.oxidizers.Count; i++)
            {
                OxidizerEntryDef it = f.oxidizers[i];
                if (it == null || string.IsNullOrEmpty(it.fuel_tag))
                {
                    ContentDiag.Skip("trade", file, "#" + (i + 1), "缺少 fuel_tag", "补 fuel_tag: <氧化剂 tag>（如 OxyRock）");
                    continue;
                }
                if (it.price_per_kg <= 0f)
                {
                    ContentDiag.Fix("trade", file, "「" + it.fuel_tag + "」", "price_per_kg=" + it.price_per_kg + "（必须 > 0）", "1");
                    it.price_per_kg = 1f;
                }
                int at = IndexOfOxidizer(it.fuel_tag);
                if (at >= 0)
                {
                    Oxidizers[at] = it;
                    MFLog.Info(LogCat.Trade, "[Space] oxidizer 覆盖：" + it.fuel_tag + " ← " + src);
                }
                else
                {
                    Oxidizers.Add(it);
                }
            }
        }

        private static int IndexOfOxidizer(string tag)
        {
            for (int i = 0; i < Oxidizers.Count; i++)
            {
                if (Oxidizers[i].fuel_tag == tag)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void ApplyStations(StationFileDef f, string src, string file)
        {
            if (f == null || f.stations == null)
            {
                return;
            }
            for (int i = 0; i < f.stations.Count; i++)
            {
                StationDef s = f.stations[i];
                if (s == null || string.IsNullOrEmpty(s.id))
                {
                    ContentDiag.Skip("trade", file, "#" + (i + 1), "缺少 id", "补 id: <站点唯一短名>（与其它站点不重名）");
                    continue;
                }
                if (!string.IsNullOrEmpty(s.kind) && s.kind != "main" && s.kind != "sub"
                    && s.kind != "blackmarket" && s.kind != "pirate")
                {
                    ContentDiag.Suspect("trade", file, "「" + s.id + "」", "kind='" + s.kind + "' 不认识",
                        "改成 main / sub / blackmarket / pirate（不写按 main）");
                }
                int at = IndexOfStations(s.id);
                if (at >= 0)
                {
                    Stations[at] = s;
                    MFLog.Info(LogCat.Trade,"[Space] station 覆盖：" + s.id + " ← " + src);
                }
                else
                {
                    Stations.Add(s);
                }
            }
        }

        private static void ApplySatelliteAnimGroup(SatelliteAnimGroupDef f, string src, string file)
        {
            if (f == null || string.IsNullOrEmpty(f.group) || f.anims == null || f.anims.Length == 0)
            {
                ContentDiag.Skip("trade", file, string.IsNullOrEmpty(f.group) || f == null ? "" : "「" + f.group + "」",
                    "缺少 group 或 anims（至少要 1 个动画名）", "补 group: <组名> 与 anims: [动画, ...]");
                return;
            }
            SatelliteAnimGroupDef dst;
            if (!SatelliteAnims.TryGetValue(f.group, out dst) || dst == null)
            {
                dst = new SatelliteAnimGroupDef();
                dst.group = f.group;
                SatelliteAnims[f.group] = dst;
            }
            if (string.IsNullOrEmpty(dst.anim_state))
            {
                dst.anim_state = f.anim_state;
            }
            List<string> list = new List<string>();
            if (dst.anims != null)
            {
                list.AddRange(dst.anims);
            }
            for (int i = 0; i < f.anims.Length; i++)
            {
                string name = f.anims[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                name = name.Trim();
                if (name.Length == 0)
                {
                    continue;
                }
                if (!name.EndsWith("_kanim", StringComparison.OrdinalIgnoreCase))
                {
                    name += "_kanim";
                }
                if (!list.Contains(name))
                {
                    list.Add(name);
                }
            }
            dst.anims = list.ToArray();
            MFLog.Info(LogCat.Trade,"[Space] 卫星贴图组 " + dst.group + "：" + dst.anims.Length + " 张候选（← " + src + "）");
        }

        internal static bool overlaid;

        internal static void OverlayMods()
        {
            if (overlaid || !loaded)
            {
                return;
            }
            overlaid = true;
            string ownRoot = "";
            try
            {
                ownRoot = NormDir(Path.GetDirectoryName(typeof(TradeDb).Assembly.Location));
            }
            catch (Exception e)
            {
                MFLog.Exc("trade:ownroot", LogCat.Trade, e,
                    "[Space] 取自身程序集目录失败 → 可能把本 Mod 的 trade/ 当其它 Mod 重复合并");
            }
            List<ModYamlFile> files;
            ContentDiag.Begin("trade-覆盖", LogCat.Trade);
            try
            {
                files = ModYamlScanner.Scan(new string[] { "trade" }, "trade", "trade-覆盖");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 多 Mod 贸易扫描失败（只用本地 trade/）");
                ContentDiag.End("trade-覆盖");
                return;
            }
            int applied = 0;
            for (int i = 0; i < files.Count; i++)
            {
                ModYamlFile file = files[i];
                if (file == null || string.IsNullOrEmpty(file.text) || string.IsNullOrEmpty(file.filePath)
                    || string.IsNullOrEmpty(file.modRoot))
                {
                    continue;
                }
                // 以前两侧都只做 TrimEnd，一旦一边用 '/' 一边用 '\'，
                string fileMod = NormDir(file.modRoot);
                if (ownRoot.Length > 0 && string.Equals(fileMod, ownRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string filePath = (file.filePath ?? "").Replace('\\', '/');
                if (ownRoot.Length > 0
                    && filePath.StartsWith(ownRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string rel;
                try
                {
                    rel = filePath.StartsWith(fileMod + "/", StringComparison.OrdinalIgnoreCase)
                        ? filePath.Substring(fileMod.Length + 1)
                        : filePath.Substring(file.modRoot.Length).TrimStart('\\', '/');
                }
                catch (Exception)
                {
                    continue;
                }
                string[] parts = rel.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }
                // 顶层文件（trade/valuation.yaml）段名要去扩展名；子目录（trade/station/xxx.yaml）段名就是目录名
                string second = parts[1].ToLowerInvariant();
                string section = parts.Length >= 3 ? second : Path.GetFileNameWithoutExtension(second);
                string src = file.modTitle + "/" + rel;
                if (section == "config")
                {
                    ContentDiag.Skip("trade-覆盖", src, "", "系统配置只认本地一份，其它 Mod 的同名文件被忽略",
                        "要改全局参数请直接改本 Mod 的 trade/config.yaml");
                    continue;
                }
                try
                {
                    if (section == "valuation")
                    {
                        ValuationFileDef one = ParseOne<ValuationFileDef>(file.text, src);
                        if (one != null)
                        {
                            ApplyValuation(one, src, src);
                            applied++;
                        }
                    }
                    else if (section == "items")
                    {
                        Dictionary<string, ItemListEntryDef> one =
                            ParseOne<Dictionary<string, ItemListEntryDef>>(file.text, src);
                        if (one != null)
                        {
                            ApplyItems(one, src, src);
                            applied++;
                        }
                    }
                    else if (section == "ban")
                    {
                        BanFileDef one = ParseOne<BanFileDef>(file.text, src);
                        if (one != null)
                        {
                            ApplyBan(one, src, src);
                            applied++;
                        }
                    }
                    else if (section == "fuel")
                    {
                        FuelFileDef one = ParseOne<FuelFileDef>(file.text, src);
                        if (one != null)
                        {
                            ApplyFuels(one, src, src);
                            applied++;
                        }
                    }
                    else if (section == "station")
                    {
                        StationFileDef one = ParseOne<StationFileDef>(file.text, src);
                        if (one != null)
                        {
                            ApplyStations(one, src, src);
                            applied++;
                        }
                    }
                    else
                    {
                        ContentDiag.Skip("trade-覆盖", src, "",
                            "未识别的 trade 子目录 '" + section + "'，整份未加载",
                            "只认 valuation / items / ban / fuel / station / anim / config");
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Trade, e, "[Space] 覆盖文件处理失败 {0}", src);
                    ContentDiag.File("trade-覆盖", src, "应用阶段抛异常：" + e.Message, "该文件后续条目未生效");
                }
            }
            MFLog.Info(LogCat.Trade,"[Space] 多 Mod 贸易扫描完成：其它 Mod 覆盖文件 " + applied + " 个"
                + "（本 Mod 自有 trade/ 已在基础加载并入）");
            ContentDiag.End("trade-覆盖");
        }

        private static int DonationStationCount()
        {
            return Donation == null || Donation.stations == null ? 0 : Donation.stations.Count;
        }

        private static int DonationChannelCount()
        {
            if (Donation == null || Donation.stations == null)
            {
                return 0;
            }
            int n = 0;
            foreach (KeyValuePair<string, DonationStationDef> kv in Donation.stations)
            {
                if (kv.Value != null && kv.Value.channels != null)
                {
                    n += kv.Value.channels.Count;
                }
            }
            return n;
        }

        // 该站点的捐款渠道（没有 = 该站不支持捐款）。
        internal static List<DonationChannelDef> DonationChannels(string stationId)
        {
            if (string.IsNullOrEmpty(stationId) || Donation == null || Donation.stations == null)
            {
                return null;
            }
            DonationStationDef def;
            if (!Donation.stations.TryGetValue(stationId, out def) || def == null || def.channels == null)
            {
                return null;
            }
            return def.channels.Count > 0 ? def.channels : null;
        }

        // 单个 ID 一次最多捐几份（0 = 不限）。站点覆盖 > 渠道默认。
        internal static int DonationItemLimit(string stationId, DonationChannelDef ch, string itemId)
        {
            if (Donation != null && Donation.item_limits != null && !string.IsNullOrEmpty(stationId))
            {
                Dictionary<string, int> per;
                if (Donation.item_limits.TryGetValue(stationId, out per) && per != null)
                {
                    int v;
                    if (!string.IsNullOrEmpty(itemId) && per.TryGetValue(itemId, out v))
                    {
                        return v;
                    }
                }
            }
            return ch != null ? ch.per_item_limit : 0;
        }

        internal static SatelliteAnimGroupDef SatelliteAnimGroup(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            SatelliteAnimGroupDef def;
            return SatelliteAnims.TryGetValue(name, out def) ? def : null;
        }

        internal static StationDef FindStation(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            for (int i = 0; i < Stations.Count; i++)
            {
                if (Stations[i].id == id)
                {
                    return Stations[i];
                }
            }
            int cut = id.LastIndexOf('_');
            if (cut > 0)
            {
                string baseId = id.Substring(0, cut);
                for (int i = 0; i < Stations.Count; i++)
                {
                    if (Stations[i].id == baseId)
                    {
                        return Stations[i];
                    }
                }
            }
            return null;
        }

        internal static TradeRarity ParseRarity(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return TradeRarity.Common;
            }
            switch (s.Trim().ToLowerInvariant())
            {
                case "poor":
                    return TradeRarity.Poor;
                case "bad":
                    return TradeRarity.Bad;
                case "common":
                    return TradeRarity.Common;
                case "good":
                    return TradeRarity.Good;
                case "master":
                    return TradeRarity.Master;
                case "legendary":
                    return TradeRarity.Legendary;
                default:
                    return TradeRarity.Common;
            }
        }

        internal static string RarityName(TradeRarity r)
        {
            switch (r)
            {
                case TradeRarity.Poor:
                    return "极差";
                case TradeRarity.Bad:
                    return "很差";
                case TradeRarity.Good:
                    return "精良";
                case TradeRarity.Master:
                    return "大师";
                case TradeRarity.Legendary:
                    return "传奇";
                default:
                    return "一般";
            }
        }

        internal static FuelEntryDef FindFuelByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }
            for (int i = 0; i < Fuels.Count; i++)
            {
                if (Fuels[i].fuel_tag == tag)
                {
                    return Fuels[i];
                }
            }
            return null;
        }

        internal static OxidizerEntryDef FindOxidizerByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }
            for (int i = 0; i < Oxidizers.Count; i++)
            {
                if (Oxidizers[i].fuel_tag == tag)
                {
                    return Oxidizers[i];
                }
            }
            return null;
        }

        internal static float OxidizerPricePerKg(string tag)
        {
            OxidizerEntryDef fd = FindOxidizerByTag(tag);
            return fd != null && fd.price_per_kg > 0f ? TradeNumber.Money(fd.price_per_kg) : 1f;
        }

        internal static string OxidizerDisplayName(string tag)
        {
            OxidizerEntryDef fd = FindOxidizerByTag(tag);
            if (fd != null && !string.IsNullOrEmpty(fd.name))
            {
                return fd.name;
            }
            return TradeUtil.DisplayName(tag);
        }

        // 加油面板与结算共用同一套单价/名称口径，避免各处各写一份回退
        internal static float FuelPricePerKg(string tag)
        {
            FuelEntryDef fd = FindFuelByTag(tag);
            return fd != null && fd.price_per_kg > 0f ? TradeNumber.Money(fd.price_per_kg) : 1f;
        }

        internal static string FuelDisplayName(string tag)
        {
            FuelEntryDef fd = FindFuelByTag(tag);
            if (fd != null && !string.IsNullOrEmpty(fd.name))
            {
                return fd.name;
            }
            if (!string.IsNullOrEmpty(tag))
            {
                try
                {
                    Element e = ElementLoader.GetElement(new Tag(tag));
                    if (e != null && !string.IsNullOrEmpty(e.name))
                    {
                        return e.name;
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Trade, e, "[Space] 燃料元素名解析失败 " + tag);
                }
                string n = TradeUtil.DisplayName(tag);
                if (!string.IsNullOrEmpty(n) && n != tag)
                {
                    return n;
                }
            }
            return "燃料";
        }

        internal static FuelEntryDef FindFuelByEngine(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return null;
            }
            for (int i = 0; i < Fuels.Count; i++)
            {
                if (Fuels[i].engine == prefabId)
                {
                    return Fuels[i];
                }
            }
            return null;
        }
    }
}
