using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class SpaceCfg
    {
        // 网格总环数（同 trade/config.yaml 的 star_map_rings）：主图 0~14、间隔、子图 16~26
        internal const int NumRings = 27;

        // 原版主图环数（扩径前；构造 ClusterGrid 时实测捕获，默认 12）。
        internal static int VanillaRings = 12;

        internal const int SubRadius = 5;
        internal const int SubGapCells = 2;
        internal const int VisionRadius = 2;

        internal const int DangerRingSpan = 2;

        internal const int MaxStations = 6;

        // 贸易站占位动画。★ 星图上的实体动画必须用"星图专用"的 kanim：
        internal const string StationAnim = "gravitas_space_poi_kanim";
        internal const string StationAnimState = "station_1";

        internal const string MainRiftPrefabId = "SpaceMainRift";
        internal const string FakeRiftPrefabId = "SpaceFakeRift";
        internal const string StationPrefabId = "SpaceTradeStation";
        internal const float FreighterSpeed = 4f;
        internal const string QuestSitePrefabId = "SpaceQuestSite";
        // 档位单一来源：卷轴 prefab 与星图任务事件共用同一组 id
        internal static readonly string[] ScrollTiers = MoreFood_KFC.ScrollItems.Tiers;

        internal static string ScrollPrefabId(string tier)
        {
            return MoreFood_KFC.ScrollItems.PrefabId(tier);
        }

        internal static string ScrollDisplayName(string tier)
        {
            return MoreFood_KFC.ScrollItems.DisplayName(tier);
        }

        // ★ 怪物动画兜底（2026-11-12 起：20 只怪各用自己的 `<id>_kanim`，这里只在 yaml 没写 / 资源缺失时兜底）
        internal const string MonsterAnim = "longrange_missile_clustermap_kanim";
        internal const string MonsterAnimState = "idle_loop";   // 兜底 kanim 的状态；专属贴图没有它时自动退回 "ui"
        //   任务地点：复用原版星图 POI 动画（与贸易站同族，避免尺寸错位）
        internal const string QuestSiteAnim = "gravitas_space_poi_kanim";
        internal const string QuestSiteAnimState = "station_2";

        // 前锥 → 额外攻击距离（D-11：基础射程=相邻，前锥按级别 +1/+2/+3）
        internal const string NoseconeSmall = "HabitatModuleSmall";
        internal const string NoseconeBasic = "NoseconeBasic";
        internal const string NoseconeHarvest = "NoseconeHarvest";

        internal static int NoseconeRangeBonus(string prefabId)
        {
            if (prefabId == NoseconeSmall)
            {
                return 1;
            }
            if (prefabId == NoseconeBasic)
            {
                return 2;
            }
            if (prefabId == NoseconeHarvest)
            {
                return 3;
            }
            return 0;
        }

        /// <summary>★ 问题12：前锥提供的【基础护甲】——钻头 800 / 基础 400 / 太空员 200（依次递减）。</summary>
        internal static float NoseconeArmor(string prefabId)
        {
            if (prefabId == NoseconeHarvest) return 800f;   // 钻头前锥（最高）
            if (prefabId == NoseconeBasic) return 400f;     // 基础前锥
            if (prefabId == NoseconeSmall) return 200f;     // 太空员前锥（最低）
            return 0f;
        }

        /// <summary>★ 问题12：前锥提供的【基础防御】——依次相当于 F/E/D 档怪物防御（0/2/5）。</summary>
        internal static float NoseconeDefense(string prefabId)
        {
            if (prefabId == NoseconeHarvest) return 5f;     // D 档
            if (prefabId == NoseconeBasic) return 2f;       // E 档
            if (prefabId == NoseconeSmall) return 0f;       // F 档
            return 0f;
        }


        internal static readonly Color DangerColor = new Color(0.72f, 0.18f, 0.14f, 0.52f);

        internal static readonly Color[] MosaicPalette = new Color[]
        {
            new Color(0.13f, 0.15f, 0.24f, 1f),
            new Color(0.19f, 0.17f, 0.31f, 1f),
            new Color(0.10f, 0.12f, 0.19f, 1f),
            new Color(0.23f, 0.20f, 0.35f, 1f)
        };

        // ★ AxialI 的构造函数签名是 AxialI(int a, int b) → r = a, q = b（反的！）
        internal static AxialI Mk(int q, int r)
        {
            AxialI a = default(AxialI);
            a.Q = q;
            a.R = r;
            return a;
        }

        internal static string Pos(AxialI c)
        {
            return "(" + c.Q + "," + c.R + ")";
        }

        internal static int Abs(int v)
        {
            return v < 0 ? -v : v;
        }

        internal static int Ring(AxialI c)
        {
            return (Abs(c.Q) + Abs(c.R) + Abs(c.Q + c.R)) / 2;
        }

        internal static int Distance(AxialI a, AxialI b)
        {
            return Ring(Mk(a.Q - b.Q, a.R - b.R));
        }

        internal static int Sector(AxialI c)
        {
            int q = c.Q;
            int r = c.R;
            int s = -q - r;
            int aq = Abs(q);
            int ar = Abs(r);
            int asv = Abs(s);
            if (aq >= ar && aq >= asv)
            {
                return q >= 0 ? 0 : 3;
            }
            if (ar >= aq && ar >= asv)
            {
                return r >= 0 ? 1 : 4;
            }
            return s >= 0 ? 2 : 5;
        }

        internal static AxialI Neighbor(AxialI c, int dir)
        {
            AxialI d = AxialI.DIRECTIONS[((dir % 6) + 6) % 6];
            return Mk(c.Q + d.Q, c.R + d.R);
        }

        internal static int StableHash(AxialI c, int salt)
        {
            return Abs(c.Q * 73856093 ^ c.R * 19349663 ^ salt * 83492791);
        }

        internal static Color MosaicColor(AxialI c)
        {
            return MosaicPalette[StableHash(c, 7) % MosaicPalette.Length];
        }
    }

    internal sealed class StationSlot
    {
        internal AxialI orbit;
        internal AxialI planet;
        internal int worldId;
        internal string planetType;
    }

    internal sealed class SpaceZones
    {
        private readonly HashSet<AxialI> dangerLookup = new HashSet<AxialI>();
        private readonly HashSet<AxialI> subSet = new HashSet<AxialI>();

        internal bool valid;
        internal int mainMaxRing;
        internal int visibleEdgeRing;
        internal int dangerSector;
        internal int dangerRingLo;
        internal int dangerRingHi;
        internal AxialI subCenter;
        internal int subRadius;
        internal int subCenterDistance;
        internal AxialI subRiftCell;

        internal readonly List<AxialI> dangerCells = new List<AxialI>();
        internal readonly List<AxialI> subCells = new List<AxialI>();
        internal readonly List<StationSlot> stationSlots = new List<StationSlot>();

        // 战斗（K6）：区域边缘格索引缓存 —— 刷怪/离场只从这里随机取，不做全图扫描
        internal readonly List<AxialI> mainEdgeCells = new List<AxialI>();
        internal readonly List<AxialI> subEdgeCells = new List<AxialI>();

        internal bool IsSub(AxialI c)
        {
            return subSet.Contains(c);
        }

        internal bool IsDanger(AxialI c)
        {
            return dangerLookup.Contains(c);
        }

        internal bool IsVoid(AxialI c)
        {
            return SpaceCfg.Ring(c) > visibleEdgeRing && !IsSub(c);
        }

        internal static SpaceZones Build(ClusterGrid grid, bool hasFakeHint, AxialI fakeHint)
        {
            SpaceZones z = new SpaceZones();
            ClusterManager cm = ClusterManager.Instance;
            if (grid == null || cm == null)
            {
                return z;
            }

            List<AxialI> planets = new List<AxialI>();
            List<int> worldIds = new List<int>();
            List<string> planetTypes = new List<string>();
            foreach (WorldContainer wc in cm.WorldContainers)
            {
                // ★ 排除火箭舱室；任务地图世界（SpaceQuestSite 宿主）同样不当星球 ——
                if (wc == null || wc.IsModuleInterior || wc.GetComponent<SpaceQuestSite>() != null)
                {
                    continue;
                }
                ClusterGridEntity e = wc.GetComponent<ClusterGridEntity>();
                if (e == null || e.Layer != EntityLayer.Asteroid || !grid.IsValidCell(e.Location))
                {
                    continue;
                }
                planets.Add(e.Location);
                worldIds.Add(wc.id);
                planetTypes.Add(e.GetType().Name);
            }
            if (planets.Count == 0)
            {
                Debug.LogWarning("[Space] 还没读到星球（大星球），本轮暂缓分区与生成");
                return z;
            }

            int max = 0;
            for (int i = 0; i < planets.Count; i++)
            {
                int r = SpaceCfg.Ring(planets[i]);
                if (r > max)
                {
                    max = r;
                }
            }
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null || kv.Value.Count == 0 || IsOnlyOurs(kv.Value))
                {
                    continue;
                }
                int r = SpaceCfg.Ring(kv.Key);
                if (r > max)
                {
                    max = r;
                }
            }

            // ★ 主图恒按原版环数裁：扩展环里的原版内容（读档后才注册的 POI 等）
            int vanillaMax = SpaceCfg.VanillaRings - 1;
            if (vanillaMax > 0 && max != vanillaMax)
            {
                if (max > vanillaMax)
                {
                    Debug.LogWarning("[Space] 原版内容最大环=" + max + " 超出原版主图 " + vanillaMax
                        + " 环，按原版主图裁切（扩展环内的原版内容只隐藏，不计入分区）");
                }
                max = vanillaMax;
            }

            z.mainMaxRing = max;
            // ★ 危险区放在"内容之外"：(M+1)~(M+1+span) 环，这些环按定义没有星球 → 不与星球重叠
            z.dangerRingLo = max + 1;
            z.dangerRingHi = max + 1 + SpaceCfg.DangerRingSpan;
            z.visibleEdgeRing = z.dangerRingHi;

            z.subRadius = SpaceCfg.SubRadius;
            z.subCenterDistance = z.visibleEdgeRing + SpaceCfg.SubGapCells + z.subRadius;
            int cap = SpaceCfg.NumRings - 1 - z.subRadius;
            if (z.subCenterDistance > cap)
            {
                Debug.LogWarning("[Space] 环数不够：子图想放到 " + z.subCenterDistance + " 环，最多 " + cap + " 环，已收窄");
                z.subCenterDistance = cap;
            }
            z.subCenter = SpaceCfg.Mk(z.subCenterDistance, 0);
            BuildSub(z, grid);

            z.subRiftCell = hasFakeHint && z.IsSub(fakeHint) ? fakeHint : PickRandomSubCell(z, grid);
            z.dangerSector = PickDangerSector(grid, z);
            BuildDanger(z, grid);
            BuildStations(z, grid, cm, planets, worldIds, planetTypes);
            BuildEdgeCaches(z, grid);

            z.valid = true;
            MFLog.Info(LogCat.Space, "[Space] 原版内容最大环=" + max + " 可见外沿=" + z.visibleEdgeRing
                + " 危险区=" + z.dangerRingLo + "~" + z.dangerRingHi + "环"
                + " 子图中心=" + SpaceCfg.Pos(z.subCenter) + " 子裂缝=" + SpaceCfg.Pos(z.subRiftCell)
                + " 星球(大星球) " + planets.Count + " 颗：" + DescribePlanets(planets, worldIds, planetTypes));
            return z;
        }

        private static string DescribePlanets(List<AxialI> planets, List<int> ids, List<string> types)
        {
            string s = "";
            for (int i = 0; i < planets.Count && i < 8; i++)
            {
                s += (i > 0 ? " | " : "") + types[i] + "#" + ids[i] + "@" + SpaceCfg.Pos(planets[i]);
            }
            return s;
        }

        private static void BuildSub(SpaceZones z, ClusterGrid grid)
        {
            List<AxialI> all = AxialUtil.GetAllPointsWithinRadius(z.subCenter, z.subRadius);
            if (all == null)
            {
                return;
            }
            int skipped = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (!grid.IsValidCell(all[i]))
                {
                    continue;
                }
                // ★ 子图只允许落在"可见外沿之外"：分区若在星球注册前就算出来（P-10），
                //   子图中心会挤回主星系，遮罩会把主图（含已揭示格）一起盖成半揭示。
                //   这里直接剔除主图范围内的格，保证遮罩永远碰不到主图。
                if (SpaceCfg.Ring(all[i]) <= z.visibleEdgeRing)
                {
                    skipped++;
                    continue;
                }
                z.subCells.Add(all[i]);
                z.subSet.Add(all[i]);
            }
            if (skipped > 0)
            {
                Debug.LogWarning("[Space] 子图范围压到主图（可见外沿 " + z.visibleEdgeRing + " 环）内 "
                    + skipped + " 格，已剔除；分区可能在星球注册前算早了（P-10）");
            }
        }

        private static AxialI PickRandomSubCell(SpaceZones z, ClusterGrid grid)
        {
            List<AxialI> free = new List<AxialI>();
            for (int i = 0; i < z.subCells.Count; i++)
            {
                if (!IsOccupied(grid, z.subCells[i]))
                {
                    free.Add(z.subCells[i]);
                }
            }
            if (free.Count == 0)
            {
                return z.subCenter;
            }
            return free[Random.Range(0, free.Count)];
        }

        private static int PickDangerSector(ClusterGrid grid, SpaceZones z)
        {
            AxialI offset = SpaceCfg.Mk(z.subRiftCell.Q - z.subCenter.Q, z.subRiftCell.R - z.subCenter.R);
            int start = SpaceCfg.StableHash(offset, 31) % 6;
            int bestSector = start;
            int bestOccupied = int.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                int s = (start + i) % 6;
                int occupied = 0;
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
                {
                    int r = SpaceCfg.Ring(kv.Key);
                    if (r < z.dangerRingLo || r > z.dangerRingHi || SpaceCfg.Sector(kv.Key) != s)
                    {
                        continue;
                    }
                    if (IsOccupied(grid, kv.Key))
                    {
                        occupied++;
                    }
                }
                if (occupied == 0)
                {
                    return s;
                }
                if (occupied < bestOccupied)
                {
                    bestOccupied = occupied;
                    bestSector = s;
                }
            }
            Debug.LogWarning("[Space] 危险环上仍有星球，落在占用最少的扇区 " + bestSector + "（" + bestOccupied + " 格被占）");
            return bestSector;
        }

        private static void BuildDanger(SpaceZones z, ClusterGrid grid)
        {
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                AxialI cell = kv.Key;
                int r = SpaceCfg.Ring(cell);
                if (r < z.dangerRingLo || r > z.dangerRingHi || SpaceCfg.Sector(cell) != z.dangerSector)
                {
                    continue;
                }
                if (IsOccupied(grid, cell))
                {
                    continue;
                }
                z.dangerCells.Add(cell);
                z.dangerLookup.Add(cell);
            }
        }

        private static void BuildEdgeCaches(SpaceZones z, ClusterGrid grid)
        {
            z.mainEdgeCells.Clear();
            z.subEdgeCells.Clear();
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                AxialI c = kv.Key;
                if (z.IsSub(c))
                {
                    continue;
                }
                if (SpaceCfg.Ring(c) == z.visibleEdgeRing)
                {
                    z.mainEdgeCells.Add(c);
                }
            }
            for (int i = 0; i < z.subCells.Count; i++)
            {
                AxialI c = z.subCells[i];
                AxialI rel = SpaceCfg.Mk(c.Q - z.subCenter.Q, c.R - z.subCenter.R);
                if (SpaceCfg.Ring(rel) == z.subRadius)
                {
                    z.subEdgeCells.Add(c);
                }
            }
        }

        private static void BuildStations(SpaceZones z, ClusterGrid grid, ClusterManager cm,
            List<AxialI> planets, List<int> ids, List<string> planetTypes)
        {
            WorldContainer start = cm.GetStartWorld();
            int startId = start != null ? start.id : -1;
            ClusterGridEntity startEntity = start != null ? start.GetComponent<ClusterGridEntity>() : null;
            AxialI startCell = startEntity != null ? startEntity.Location : SpaceCfg.Mk(0, 0);

            List<int> dists = new List<int>();
            for (int i = 0; i < planets.Count; i++)
            {
                dists.Add(SpaceCfg.Distance(planets[i], startCell));
            }

            int secondaryId = -1;
            int bestD = int.MaxValue;
            for (int i = 0; i < planets.Count; i++)
            {
                if (ids[i] == startId)
                {
                    continue;
                }
                if (dists[i] < bestD)
                {
                    bestD = dists[i];
                    secondaryId = ids[i];
                }
            }

            List<int> order = new List<int>();
            for (int i = 0; i < planets.Count; i++)
            {
                if (ids[i] != startId && ids[i] != secondaryId)
                {
                    order.Add(i);
                }
            }
            order.Sort(delegate(int a, int b) { return dists[a].CompareTo(dists[b]); });

            HashSet<AxialI> taken = new HashSet<AxialI>();
            int count = 0;
            for (int k = 0; k < order.Count && count < SpaceCfg.MaxStations; k++)
            {
                int i = order[k];
                AxialI planet = planets[i];

                AxialI orbit = FindOrbitWithStation(grid, planet);
                if (orbit.Q == int.MinValue)
                {
                    orbit = DeterministicOrbit(grid, planet, taken);
                }
                if (orbit.Q == int.MinValue)
                {
                    Debug.LogWarning("[Space] 星球 " + SpaceCfg.Pos(planet) + " 的 6 个轨道格都被占了，跳过");
                    continue;
                }
                int d = SpaceCfg.Distance(orbit, planet);
                if (d != 1)
                {
                    Debug.LogWarning("[Space] 自检失败：轨道格 " + SpaceCfg.Pos(orbit) + " 到星球 "
                        + SpaceCfg.Pos(planet) + " 距离=" + d);
                    continue;
                }
                taken.Add(orbit);
                StationSlot slot = new StationSlot();
                slot.planet = planet;
                slot.worldId = ids[i];
                slot.planetType = planetTypes[i];
                slot.orbit = orbit;
                z.stationSlots.Add(slot);
                count++;
            }
            MFLog.Info(LogCat.Space, "[Space] 规划站点 " + count + " 座（主星球#" + startId + " 与副星球#" + secondaryId + " 不设站）");
        }

        private static AxialI FindOrbitWithStation(ClusterGrid grid, AxialI planet)
        {
            for (int d = 0; d < 6; d++)
            {
                AxialI cell = SpaceCfg.Neighbor(planet, d);
                if (grid.IsValidCell(cell) && HasStation(grid, cell))
                {
                    return cell;
                }
            }
            return SpaceCfg.Mk(int.MinValue, int.MinValue);
        }

        private static AxialI DeterministicOrbit(ClusterGrid grid, AxialI planet, HashSet<AxialI> taken)
        {
            int start = SpaceCfg.StableHash(planet, 17) % 6;
            for (int i = 0; i < 6; i++)
            {
                AxialI cell = SpaceCfg.Neighbor(planet, (start + i) % 6);
                if (!grid.IsValidCell(cell) || taken.Contains(cell) || IsOccupied(grid, cell))
                {
                    continue;
                }
                if (SpaceCfg.Distance(cell, planet) != 1)
                {
                    continue;
                }
                return cell;
            }
            return SpaceCfg.Mk(int.MinValue, int.MinValue);
        }

        private static bool HasStation(ClusterGrid grid, AxialI cell)
        {
            List<ClusterGridEntity> list;
            if (!grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is SpaceStation)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsOccupied(ClusterGrid grid, AxialI cell)
        {
            List<ClusterGridEntity> list;
            if (grid == null || !grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is SpaceRift) && !(list[i] is SpaceStation))
                {
                    return true;
                }
            }
            return false;
        }

        // ★ 实锤：这里只认原版实体 —— 我们自己的星图实体一个都不能漏。
        private static bool IsOurs(ClusterGridEntity e)
        {
            return e is SpaceRift || e is SpaceStation || e is SpaceQuestSite
                || e is SpaceMonster || e is SpaceLootField;
        }

        private static bool IsOnlyOurs(List<ClusterGridEntity> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!IsOurs(list[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    // 时间裂口所在格：原版唯一入口，闸门/隐藏都要让路
    internal static class SpaceTearCell
    {
        internal static bool Hosts(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return false;
            }
            List<ClusterGridEntity> list;
            if (!grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is TemporalTear)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
