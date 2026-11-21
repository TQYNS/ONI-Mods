using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    // 小行星带物资池：专池专用

    internal sealed class BeltPoolSrcRaw
    {
        public List<string> tags { get; set; }
        public List<string> ids { get; set; }
    }

    internal sealed class BeltTierRaw
    {
        public List<float> range { get; set; }
        public float? weight { get; set; }
    }

    internal sealed class BeltGroupRaw
    {
        public string group { get; set; }
        public bool? only_ours { get; set; }
        public List<int> picks { get; set; }
        public BeltPoolSrcRaw pool { get; set; }
        public List<BeltTierRaw> tiers { get; set; }
        public List<string> exclude_groups { get; set; }
        public bool? allow_liquid { get; set; }
        // ★ 只给怪物掉落用：true = 子图物资带散布时跳过该组（否则加弹药组会让子图带也掉弹药）
        public bool? monster_only { get; set; }
    }

    internal sealed class BeltSpawnRaw
    {
        public List<int> count { get; set; }
        public bool? avoid_adjacent { get; set; }
        public bool? once_per_save { get; set; }
        public string label { get; set; }
        public string style { get; set; }
    }

    internal sealed class AsteroidBeltRaw
    {
        public BeltSpawnRaw sub_spawn { get; set; }
        public List<BeltGroupRaw> composition { get; set; }
    }

    internal sealed class AsteroidBeltFileDef
    {
        public AsteroidBeltRaw asteroid_belt { get; set; }
    }

    internal static class AsteroidBeltPool
    {
        private static AsteroidBeltRaw cfg;
        private static bool loaded;

        /// <summary>加载期解析（CombatDb.Load 调用），运行时解析会污染原版本地化。</summary>
        internal static void Load(string root)
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            try
            {
                string path = Path.Combine(Path.Combine(root, "loot"), "asteroid_belt.yaml");
                if (!File.Exists(path))
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带配置缺失：" + path + "（子图不会散布物资带）");
                    ContentDiag.File("combat", path, "文件不存在", "要生成小行星带就补 combat/loot/asteroid_belt.yaml");
                    return;
                }
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带配置读取失败 {0}", path);
                    ContentDiag.File("combat", path, "读取失败：" + e.Message, "检查文件是否被占用、编码是否为 UTF-8");
                    return;
                }
                AsteroidBeltFileDef file = YamlLoad.Parse<AsteroidBeltFileDef>(text, path, "combat");
                cfg = file != null ? file.asteroid_belt : null;
                if (cfg == null)
                {
                    ContentDiag.File("combat", path, "顶层缺少 asteroid_belt 段", "顶层键写 asteroid_belt: 并在下面写 sub_spawn 与 composition");
                    return;
                }
                ValidateComposition(cfg, path);
                BuildGroupIndex(cfg);
                // ★ 怪物掉落按组名展开：注入构建器（静态字段，命中路径零分配）
                SpaceLootPools.groupBuilder = BuildGroupCandidates;
                int subGroups = 0;
                int monsterGroups = 0;
                if (cfg.composition != null)
                {
                    for (int i = 0; i < cfg.composition.Count; i++)
                    {
                        BeltGroupRaw g = cfg.composition[i];
                        if (g != null && g.monster_only == true) monsterGroups++;
                        else subGroups++;
                    }
                }
                MFLog.Info(LogCat.Combat,"[Space] 小行星带配置：子图散布 "
                    + Range(cfg.sub_spawn != null ? cfg.sub_spawn.count : null, 7, 12)
                    + " 座，子图组 " + subGroups + " + 怪物组 " + monsterGroups);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带配置加载失败（子图不会散布物资带）");
                cfg = null;
            }
        }

        // 加载期一次性检查组成组：抽不出东西 / 物品找不到都提前说清楚
        private static void ValidateComposition(AsteroidBeltRaw c, string path)
        {
            try
            {
                if (c == null || c.composition == null)
                {
                    return;
                }
                for (int g = 0; g < c.composition.Count; g++)
                {
                    BeltGroupRaw grp = c.composition[g];
                    if (grp == null)
                    {
                        ContentDiag.Skip("combat", path, "#" + (g + 1), "组成组为空", "删掉这个空的 '- ' 项");
                        continue;
                    }
                    string name = string.IsNullOrEmpty(grp.group) ? "#" + (g + 1) : "「" + grp.group + "」";
                    if (string.IsNullOrEmpty(grp.group))
                    {
                        ContentDiag.Skip("combat", path, name, "组成组缺少 group", "补 group: <组名>（exclude_groups 也按它引用）");
                    }
                    bool hasIds = grp.pool != null && grp.pool.ids != null && grp.pool.ids.Count > 0;
                    bool hasTags = grp.pool != null && grp.pool.tags != null && grp.pool.tags.Count > 0;
                    bool hasTiers = grp.tiers != null && grp.tiers.Count > 0;
                    bool onlyOurs = grp.only_ours.HasValue && grp.only_ours.Value;
                    if (!hasIds && !hasTags && !hasTiers && !onlyOurs)
                    {
                        ContentDiag.Skip("combat", path, name,
                            "既没有 pool（ids/tags）也没有 tiers，抽不出任何物品",
                            "补 pool 或 tiers，或删掉该组");
                        continue;
                    }
                    if (hasIds)
                    {
                        for (int i = 0; i < grp.pool.ids.Count; i++)
                        {
                            string id = grp.pool.ids[i];
                            if (string.IsNullOrEmpty(id))
                            {
                                continue;
                            }
                            if (Assets.TryGetPrefab(new Tag(id)) == null)
                            {
                                ContentDiag.Suspect("combat", path, name,
                                    "物品 '" + id + "' 找不到对应 prefab（抽取时会被跳过）",
                                    "确认 tag 拼写，或该物品来自未启用的 DLC/Mod");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带组成校验失败（配置仍会照常加载）");
            }
        }

        /// <summary>运行时只读缓存，不解析 YAML。</summary>
        private static AsteroidBeltRaw Cfg()
        {
            if (!loaded)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 小行星带配置未在加载期读取，本次跳过生成");
            }
            return cfg;
        }

        // 组名 → 组定义（加载期建一次；忽略大小写）。怪物掉落按组名查，避免每次击杀遍历 composition。
        private static readonly Dictionary<string, BeltGroupRaw> groupMap =
            new Dictionary<string, BeltGroupRaw>(StringComparer.OrdinalIgnoreCase);

        private static void BuildGroupIndex(AsteroidBeltRaw c)
        {
            groupMap.Clear();
            if (c == null || c.composition == null)
            {
                return;
            }
            for (int i = 0; i < c.composition.Count; i++)
            {
                BeltGroupRaw g = c.composition[i];
                if (g == null || string.IsNullOrEmpty(g.group))
                {
                    continue;
                }
                string key = g.group.Trim();
                if (groupMap.ContainsKey(key))
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带组名重复：" + key + "（后一个被忽略）");
                    continue;
                }
                groupMap[key] = g;
            }
        }

        /// <summary>按组名取组定义（找不到返回 null）。</summary>
        internal static BeltGroupRaw FindGroup(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            BeltGroupRaw g;
            return groupMap.TryGetValue(name.Trim(), out g) ? g : null;
        }

        /// <summary>子星图散布生成（每档一次；读档不重生）。</summary>
        internal static void EnsureSubSpawn(ClusterGrid grid, SpaceZones zones)
        {
            try
            {
                if (grid == null)
                {
                    grid = ClusterGrid.Instance;
                }
                AsteroidBeltRaw c = Cfg();
                if (c == null || c.sub_spawn == null || c.composition == null || c.composition.Count == 0)
                {
                    return;
                }
                // 分区未就绪 → 先补齐（此刻已是装载完成点，重建安全；EnsureTestContent 自带去重）
                if (zones == null || !zones.valid || zones.subCells.Count == 0)
                {
                    SpaceTestRuntime.EnsureTestContent();
                    zones = StarMapVisuals.Zones;
                }
                if (grid == null || zones == null || !zones.valid || zones.subCells.Count == 0)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 子图未就绪，本次跳过小行星带散布");
                    return;
                }
                CombatSaveData sd = CombatSaveData.Attach();
                bool once = !c.sub_spawn.once_per_save.HasValue || c.sub_spawn.once_per_save.Value;
                if (once && sd != null && sd.beltSpawned)
                {
                    return;
                }
                // 子图已有本档的带 → 判定已生成过（熔断）
                if (once && HasSubBelt(zones))
                {
                    if (sd != null)
                    {
                        sd.beltSpawned = true;
                    }
                    MFLog.Info(LogCat.Combat,"[Space] 子图已有小行星带，判定本档已生成过 → 跳过（并补记标记）");
                    return;
                }

                List<AxialI> free = new List<AxialI>();
                for (int i = 0; i < zones.subCells.Count; i++)
                {
                    AxialI cell = zones.subCells[i];
                    if (cell == zones.subRiftCell)
                    {
                        continue;
                    }
                    if (SpaceRealEventRuntime.CellFree(cell))
                    {
                        free.Add(cell);
                    }
                }
                if (free.Count == 0)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 子图没有空格，小行星带不生成");
                    return;
                }

                int want = Range(c.sub_spawn.count, 7, 12);
                if (want > free.Count)
                {
                    want = free.Count;
                }
                bool avoidAdjacent = c.sub_spawn.avoid_adjacent.HasValue && c.sub_spawn.avoid_adjacent.Value;
                string wantStyle = c.sub_spawn.style == null ? "" : c.sub_spawn.style.Trim();
                string label = string.IsNullOrEmpty(c.sub_spawn.label) ? "星际物资带" : c.sub_spawn.label;

                int made = 0;
                int guard = free.Count * 2;
                for (int n = 0; n < want && free.Count > 0 && guard-- > 0; n++)
                {
                    int pick = UnityEngine.Random.Range(0, free.Count);
                    AxialI cell = free[pick];
                    free.RemoveAt(pick);

                    List<string> ids;
                    List<float> masses;
                    List<int> counts;
                    if (!Compose(c.composition, out ids, out masses, out counts))
                    {
                        continue;
                    }
                    BeltStyle style = ResolveStyle(wantStyle);
                    SpaceAsteroidBelt belt = AsteroidBeltFactory.Spawn(label, style, ids, masses, counts, cell);
                    if (belt == null)
                    {
                        continue;
                    }
                    made++;
                    if (avoidAdjacent)
                    {
                        for (int d = 0; d < 6; d++)
                        {
                            free.Remove(SpaceCfg.Neighbor(cell, d));
                        }
                    }
                }
                if (sd != null)
                {
                    sd.beltSpawned = true;
                }
                MFLog.Info(LogCat.Combat,"[Space] ★ 子星图小行星带：" + made + "/" + want
                    + " 座（外观随机，剩空格 " + free.Count + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc("belt:spawn", LogCat.Combat, e, "[Space] 子星图小行星带生成失败（本次不生成）");
            }
        }

        /// <summary>未配置 / random / 解析失败 → 全随机外观。</summary>
        private static BeltStyle ResolveStyle(string want)
        {
            BeltStyle style;
            if (!string.IsNullOrEmpty(want)
                && !string.Equals(want, "random", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<BeltStyle>(want, true, out style))
            {
                return style;
            }
            return AsteroidBeltFactory.RollAnyStyle();
        }

        /// <summary>子图内是否已有小行星带（熔断用）。</summary>
        private static bool HasSubBelt(SpaceZones zones)
        {
            if (zones == null || !zones.valid)
            {
                return false;
            }
            List<SpaceLootField> list = CombatRuntime.LootFields;
            if (list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                SpaceLootField lf = list[i];
                if (lf == null || lf.Location == AxialI.INVALID)
                {
                    continue;
                }
                if (zones.IsSub(lf.Location))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Compose(List<BeltGroupRaw> groups, out List<string> ids,
            out List<float> masses, out List<int> counts)
        {
            ids = new List<string>();
            masses = new List<float>();
            counts = new List<int>();
            Dictionary<string, List<string>> picked = new Dictionary<string, List<string>>();
            for (int g = 0; g < groups.Count; g++)
            {
                BeltGroupRaw grp = groups[g];
                if (grp == null)
                {
                    continue;
                }
                if (grp.monster_only == true)
                {
                    // ★ 只给怪物掉落的组（弹药/模块/传奇…），子图物资带不参与
                    continue;
                }
                // ★ GroupCandidates 返回的是缓存 List（只读）→ 这里要抽签删除，必须先复制
                List<string> pool = new List<string>(GroupCandidates(grp));
                HashSet<string> banned = new HashSet<string>();
                if (grp.exclude_groups != null)
                {
                    for (int k = 0; k < grp.exclude_groups.Count; k++)
                    {
                        List<string> other;
                        if (picked.TryGetValue(grp.exclude_groups[k], out other) && other != null)
                        {
                            for (int q = 0; q < other.Count; q++)
                            {
                                banned.Add(other[q]);
                            }
                        }
                    }
                }
                if (banned.Count > 0)
                {
                    for (int i = pool.Count - 1; i >= 0; i--)
                    {
                        if (banned.Contains(pool[i]))
                        {
                            pool.RemoveAt(i);
                        }
                    }
                }
                int want = Range(grp.picks, 1, 1);
                if (want > pool.Count)
                {
                    want = pool.Count;
                }
                List<string> took = new List<string>();
                for (int n = 0; n < want; n++)
                {
                    int pick = UnityEngine.Random.Range(0, pool.Count);
                    string id = pool[pick];
                    pool.RemoveAt(pick);
                    took.Add(id);
                    float mass = Tiers(grp.tiers);
                    if (mass <= 0f)
                    {
                        mass = 1f;
                    }
                    if (!ids.Contains(id))
                    {
                        ids.Add(id);
                        masses.Add(mass);
                        counts.Add(0);
                    }
                }
                if (!string.IsNullOrEmpty(grp.group))
                {
                    picked[grp.group] = took;
                }
                if (want <= 0 && (grp.picks == null || grp.picks.Count == 0))
                {
                    continue;
                }
            }
            return ids.Count > 0;
        }

        /// <summary>怪物掉落按组展开：抽 N 种（不重复）每种一个 kg，异常吞掉不冒到击杀流程。</summary>
        internal static bool ComposeGroup(string group, int picksLo, int picksHi,
            float kgLo, float kgHi, bool hasRange, out List<string> ids, out List<float> kgs)
        {
            ids = null;
            kgs = null;
            try
            {
                BeltGroupRaw grp = FindGroup(group);
                if (grp == null)
                {
                    WarnGroupOnce(group);
                    return false;
                }
                List<string> pool = GroupCandidates(grp);   // 缓存本体，只读
                if (pool == null || pool.Count == 0)
                {
                    WarnGroupOnce(group);
                    return false;
                }
                int lo = picksLo > 0 ? picksLo : Range(grp.picks, 1, 1);
                int hi = picksHi > 0 ? picksHi : lo;
                if (hi < lo)
                {
                    int t = lo;
                    lo = hi;
                    hi = t;
                }
                int want = UnityEngine.Random.Range(lo, hi + 1);
                if (want > pool.Count)
                {
                    want = pool.Count;
                }
                if (want <= 0)
                {
                    return false;
                }
                if (hasRange && kgHi < kgLo)
                {
                    float tk = kgLo;
                    kgLo = kgHi;
                    kgHi = tk;
                }
                int[] chosen = new int[want];
                ids = new List<string>(want);
                kgs = new List<float>(want);
                for (int n = 0; n < want; n++)
                {
                    int idx = UnityEngine.Random.Range(0, pool.Count);
                    int guard = 0;
                    while (ContainsIndex(chosen, n, idx) && guard++ < pool.Count)
                    {
                        idx = UnityEngine.Random.Range(0, pool.Count);
                    }
                    chosen[n] = idx;
                    ids.Add(pool[idx]);
                    float kg = hasRange ? UnityEngine.Random.Range(kgLo, kgHi) : Tiers(grp.tiers);
                    if (kg <= 0f)
                    {
                        kg = 1f;
                    }
                    kgs.Add(kg);
                }
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 展开掉落组失败（组 {0}）", group);
                ids = null;
                kgs = null;
                return false;
            }
        }

        private static bool ContainsIndex(int[] arr, int count, int value)
        {
            for (int i = 0; i < count; i++)
            {
                if (arr[i] == value)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>取组内第一个候选 id（图鉴图标用；只读缓存，不触发构建）。</summary>
        internal static string FirstCandidate(string group)
        {
            try
            {
                BeltGroupRaw grp = FindGroup(group);
                if (grp == null)
                {
                    return null;
                }
                List<string> pool = SpaceLootPools.TryGetCached(GroupCacheKey(grp));
                return pool != null && pool.Count > 0 ? pool[0] : null;
            }
            catch (Exception)
            {
                // expected: 图鉴取图标失败 → 该行退化成纯文本
                return null;
            }
        }

        /// <summary>组默认抽几种（yaml 没写 picks 时图鉴用）。</summary>
        internal static void GroupPicks(string group, out int lo, out int hi)
        {
            lo = 0;
            hi = 0;
            BeltGroupRaw grp = FindGroup(group);
            if (grp == null || grp.picks == null || grp.picks.Count == 0)
            {
                return;
            }
            lo = grp.picks[0];
            hi = grp.picks.Count > 1 ? grp.picks[1] : lo;
        }

        // 组候选（带缓存）：内存命中 → 存档命中 → 重建。返回的 List 是缓存本体，调用方只读，不得原地删除。
        private static List<string> GroupCandidates(BeltGroupRaw grp)
        {
            if (grp == null)
            {
                return new List<string>();
            }
            string key = GroupCacheKey(grp);
            if (string.IsNullOrEmpty(key) || SpaceLootPools.groupBuilder == null)
            {
                return BuildGroupCandidates(grp);   // 匿名组 / 委托未注入（加载异常）→ 直接构建
            }
            return SpaceLootPools.GetOrBuild(key, grp.group);
        }

        // 缓存 key = 组名 + 依赖指纹（注册表变长 ⇒ key 变 ⇒ 自动重建；不用 roots 是因为同一 List 引用恒等）
        private static string GroupCacheKey(BeltGroupRaw grp)
        {
            string name = grp == null || string.IsNullOrEmpty(grp.group)
                ? null : grp.group.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            switch (name)
            {
                case "food": return "g:food#" + SafeCount(MoreFood_KFC.ModFoods.CollectFoodIds());
                case "drink": return "g:drink#" + (MoreFood_KFC.Drinks.CreatedDrinkIds != null
                    ? MoreFood_KFC.Drinks.CreatedDrinkIds.Count : 0);
                case "ammo": return "g:ammo#" + (CombatDb.Ammo != null ? CombatDb.Ammo.Count : 0);
                case "module": return "g:module#" + (CombatDb.Modules != null ? CombatDb.Modules.Count : 0);
                case "recipe": return "g:recipe#" + RecipeScrollCount();
                default: return "g:" + name;
            }
        }

        private static int SafeCount(List<string> list)
        {
            return list != null ? list.Count : 0;
        }

        private static int RecipeScrollCount()
        {
            try
            {
                List<MoreFood_KFC.RecipeScrollInfo> all = MoreFood_KFC.RecipeScrollRegistry.All;
                return all != null ? all.Count : 0;
            }
            catch (Exception)
            {
                // expected: 配方卷轴尚未注册完 → 计数 0，等下次指纹变化重建
                return 0;
            }
        }

        /// <summary>供 SpaceLootPools 的构建委托调用（按组名）。异常在此吞掉，不能让它冒到开火/击杀流程。</summary>
        internal static List<string> BuildGroupCandidates(string group)
        {
            try
            {
                BeltGroupRaw grp = FindGroup(group);
                if (grp == null)
                {
                    WarnGroupOnce(group);
                    return new List<string>();
                }
                return BuildGroupCandidates(grp);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 构建掉落组候选失败（组 {0}，本次为空）", group);
                return new List<string>();
            }
        }

        private static readonly HashSet<string> groupWarned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static void WarnGroupOnce(string group)
        {
            string key = group != null ? group.Trim() : "";
            if (!groupWarned.Add(key))
            {
                return;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 掉落组不存在：" + key + "（怪物 loot 引用该组，本次掉落为空）");
            ContentDiag.Suspect("combat", "loot/asteroid_belt.yaml", "组「" + key + "」",
                "怪物 loot 引用了不存在的组", "在 asteroid_belt.yaml 的 composition 里补该组，或修正怪物 yaml 的 group 名");
        }

        private static List<string> BuildGroupCandidates(BeltGroupRaw grp)
        {
            // ★ 开关：only_ours=true → 只从本 Mod 自己登记的总表抽，忽略 pool（清单按组名推断）
            if (grp.only_ours.HasValue && grp.only_ours.Value)
            {
                return OursCandidates(grp.group);
            }
            List<string> result = new List<string>();
            if (grp.pool == null)
            {
                return result;
            }
            if (grp.pool.ids != null)
            {
                for (int i = 0; i < grp.pool.ids.Count; i++)
                {
                    string id = grp.pool.ids[i];
                    if (!string.IsNullOrEmpty(id) && Assets.TryGetPrefab(new Tag(id)) != null
                        && !result.Contains(id))
                    {
                        result.Add(id);
                    }
                }
            }
            if (grp.pool.tags != null && grp.pool.tags.Count > 0)
            {
                string[] tags = grp.pool.tags.ToArray();
                string key = SpaceLootPools.KeyOf(tags);
                bool liquid = grp.allow_liquid.HasValue && grp.allow_liquid.Value;
                List<string> scanned = liquid
                    ? SpaceLootPools.GetAny(key, tags)
                    : SpaceLootPools.Get(key, tags);
                if (scanned != null)
                {
                    for (int i = 0; i < scanned.Count; i++)
                    {
                        if (!result.Contains(scanned[i]))
                        {
                            result.Add(scanned[i]);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>按组名取本 Mod 自建总表（food=食物表 / drink=饮品表）。</summary>
        private static List<string> OursCandidates(string group)
        {
            List<string> ids = new List<string>();
            string key = (group ?? "").Trim().ToLowerInvariant();
            try
            {
                if (key == "food")
                {
                    List<string> foods = MoreFood_KFC.ModFoods.CollectFoodIds();
                    if (foods != null)
                    {
                        for (int i = 0; i < foods.Count; i++)
                        {
                            ids.Add(foods[i]);
                        }
                    }
                }
                else if (key == "drink")
                {
                    List<Tag> drinks = MoreFood_KFC.Drinks.CreatedDrinkIds;
                    if (drinks != null)
                    {
                        for (int i = 0; i < drinks.Count; i++)
                        {
                            ids.Add(drinks[i].Name);
                        }
                    }
                }
                else if (key == "ammo")
                {
                    List<AmmoDef> all = CombatDb.Ammo;
                    if (all != null)
                    {
                        for (int i = 0; i < all.Count; i++)
                        {
                            AmmoDef d = all[i];
                            if (d == null || string.IsNullOrEmpty(d.prefabId) || ids.Contains(d.prefabId))
                            {
                                continue;
                            }
                            ids.Add(d.prefabId);
                        }
                    }
                }
                else if (key == "module")
                {
                    List<ModuleDef> all = CombatDb.Modules;
                    if (all != null)
                    {
                        for (int i = 0; i < all.Count; i++)
                        {
                            ModuleDef d = all[i];
                            if (d == null || string.IsNullOrEmpty(d.prefabId) || ids.Contains(d.prefabId))
                            {
                                continue;
                            }
                            ids.Add(d.prefabId);
                        }
                    }
                }
                else if (key == "recipe")
                {
                    List<MoreFood_KFC.RecipeScrollInfo> all = MoreFood_KFC.RecipeScrollRegistry.All;
                    if (all != null)
                    {
                        for (int i = 0; i < all.Count; i++)
                        {
                            MoreFood_KFC.RecipeScrollInfo info = all[i];
                            if (info == null || string.IsNullOrEmpty(info.itemId) || ids.Contains(info.itemId))
                            {
                                continue;
                            }
                            ids.Add(info.itemId);
                        }
                    }
                }
                else
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带 only_ours 组名未识别：" + group
                        + "（自动清单支持 food / drink / ammo / module / recipe）");
                    return new List<string>();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc("belt:ours:" + group, LogCat.Combat, e,
                    "[Space] 小行星带取自建清单失败（组 {0}，该组本次抽空）", group);
                return new List<string>();
            }

            // 弹药/模块/卷轴不是可拾取固体 → 这三组只校验「prefab 存在且非建筑」（同 Events.Loot 的 AddLoot）
            bool strict = key != "ammo" && key != "module" && key != "recipe";
            List<string> result = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id) || result.Contains(id))
                {
                    continue;
                }
                GameObject prefab = Assets.TryGetPrefab(new Tag(id));
                if (prefab == null)
                {
                    continue;
                }
                if (prefab.GetComponent<Building>() != null)
                {
                    continue;
                }
                if (strict && prefab.GetComponent<Pickupable>() == null
                    && ElementLoader.GetElement(new Tag(id)) == null)
                {
                    continue;
                }
                result.Add(id);
            }
            MFLog.Info(LogCat.Combat,"[Space] 小行星带 only_ours 组 " + key + " 候选 " + result.Count + " 种");
            return result;
        }

        private static float Tiers(List<BeltTierRaw> tiers)
        {
            if (tiers == null || tiers.Count == 0)
            {
                return 1f;
            }
            float total = 0f;
            for (int i = 0; i < tiers.Count; i++)
            {
                total += Weight(tiers[i]);
            }
            if (total <= 0f)
            {
                return 1f;
            }
            float roll = UnityEngine.Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < tiers.Count; i++)
            {
                acc += Weight(tiers[i]);
                if (roll < acc)
                {
                    return RollRange(tiers[i]);
                }
            }
            return RollRange(tiers[tiers.Count - 1]);
        }

        private static float Weight(BeltTierRaw t)
        {
            if (t == null)
            {
                return 0f;
            }
            float w = t.weight.HasValue ? t.weight.Value : 1f;
            return w <= 0f ? 0f : w;
        }

        private static float RollRange(BeltTierRaw t)
        {
            if (t == null || t.range == null || t.range.Count < 2)
            {
                return 1f;
            }
            float lo = t.range[0];
            float hi = t.range[1];
            if (hi < lo)
            {
                float tmp = lo;
                lo = hi;
                hi = tmp;
            }
            return UnityEngine.Random.Range(lo, hi);
        }

        private static int Range(List<int> v, int defLo, int defHi)
        {
            if (v == null || v.Count == 0)
            {
                return UnityEngine.Random.Range(defLo, defHi + 1);
            }
            int lo = v[0];
            int hi = v.Count > 1 ? v[1] : v[0];
            if (hi < lo)
            {
                int tmp = lo;
                lo = hi;
                hi = tmp;
            }
            return UnityEngine.Random.Range(lo, hi + 1);
        }
    }
}
