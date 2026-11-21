using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal sealed class PlannedStation
    {
        internal string id;
        internal string name;
        internal string kind;
        internal AxialI cell;
        internal bool sub;
    }

    internal static class TradeStationManager
    {
        internal const int SubMinSpacing = 3;

        internal static readonly List<PlannedStation> Plan = new List<PlannedStation>();

        private static readonly Dictionary<string, SpaceStation> Live = new Dictionary<string, SpaceStation>();

        private static void PruneLive()
        {
            List<string> dead = null;
            foreach (KeyValuePair<string, SpaceStation> kv in Live)
            {
                if (kv.Value == null)
                {
                    if (dead == null)
                    {
                        dead = new List<string>();
                    }
                    dead.Add(kv.Key);
                }
            }
            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++)
                {
                    Live.Remove(dead[i]);
                }
            }
        }

        internal static void BuildPlan(ClusterGrid grid, SpaceZones zones)
        {
            Plan.Clear();
            if (grid == null || zones == null || !zones.valid)
            {
                return;
            }
            TradeRegistry.Init();

            int orbitIdx = 0;
            int subIdx = 0;
            int dangerUsed = 0;
            for (int i = 0; i < TradeDb.Stations.Count; i++)
            {
                StationDef def = TradeDb.Stations[i];
                PlannedStation p = new PlannedStation();
                p.id = def.id;
                p.name = string.IsNullOrEmpty(def.name) ? def.id : def.name;
                p.kind = string.IsNullOrEmpty(def.kind) ? "main" : def.kind;

                if (p.kind == "blackmarket")
                {
                    if (dangerUsed > 0)
                    {
                        continue;
                    }
                    AxialI cell = PickDangerCell(zones);
                    if (cell.Q == int.MinValue)
                    {
                        Debug.LogWarning("[Space] 危险区里找不到空位，黑市 " + def.id + " 暂缓");
                        continue;
                    }
                    p.cell = cell;
                    dangerUsed++;
                    Plan.Add(p);
                    continue;
                }

                if (p.kind == "sub")
                {
                    if (subIdx >= TradeDb.Config.max_stations_sub)
                    {
                        continue;
                    }
                    Plan.Add(p);
                    subIdx++;
                    continue;
                }

                if (orbitIdx >= zones.stationSlots.Count || orbitIdx >= TradeDb.Config.max_stations_main)
                {
                    continue;
                }
                p.cell = zones.stationSlots[orbitIdx].orbit;
                Plan.Add(p);
                orbitIdx++;
            }

            List<PlannedStation> subs = new List<PlannedStation>();
            for (int i = 0; i < Plan.Count; i++)
            {
                if (Plan[i].kind == "sub")
                {
                    subs.Add(Plan[i]);
                }
            }
            AssignSubCells(zones, grid, subs);

            int mainCount = 0;
            int subCount = 0;
            for (int i = 0; i < Plan.Count; i++)
            {
                if (Plan[i].kind == "sub")
                {
                    subCount++;
                }
                else
                {
                    mainCount++;
                }
            }
            MFLog.Info(LogCat.Trade,"[Space] 站点规划：主图 " + mainCount + " 座（含危险区黑市 " + dangerUsed
                + "）+ 子图 " + subCount + " 座 = " + Plan.Count + " 座");
        }

        private static AxialI PickDangerCell(SpaceZones zones)
        {
            if (zones.dangerCells.Count == 0)
            {
                return SpaceCfg.Mk(int.MinValue, int.MinValue);
            }
            int start = SpaceCfg.StableHash(zones.subRiftCell, 53) % zones.dangerCells.Count;
            for (int i = 0; i < zones.dangerCells.Count; i++)
            {
                AxialI c = zones.dangerCells[(start + i) % zones.dangerCells.Count];
                if (!OccupiedByOurs(c))
                {
                    return c;
                }
            }
            return SpaceCfg.Mk(int.MinValue, int.MinValue);
        }

        private static void AssignSubCells(SpaceZones zones, ClusterGrid grid, List<PlannedStation> subs)
        {
            if (subs.Count == 0)
            {
                return;
            }
            HashSet<AxialI> taken = new HashSet<AxialI>();
            for (int i = 0; i < subs.Count; i++)
            {
                AxialI cell = PickSubCell(zones, taken, SubMinSpacing, subs[i].id);
                if (cell.Q == int.MinValue)
                {
                    cell = PickSubCell(zones, taken, 2, subs[i].id);
                }
                if (cell.Q == int.MinValue)
                {
                    Debug.LogWarning("[Space] 子图里放不下更多站点，" + subs[i].id + " 暂缓");
                    continue;
                }
                taken.Add(cell);
                subs[i].cell = cell;
                subs[i].sub = true;
            }
        }

        private static AxialI PickSubCell(SpaceZones zones, HashSet<AxialI> taken, int minSpacing, string seed)
        {
            List<AxialI> free = new List<AxialI>();
            for (int i = 0; i < zones.subCells.Count; i++)
            {
                AxialI c = zones.subCells[i];
                if (OccupiedByOurs(c) || c == zones.subRiftCell)
                {
                    continue;
                }
                bool tooClose = false;
                foreach (AxialI t in taken)
                {
                    if (SpaceCfg.Distance(c, t) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    free.Add(c);
                }
            }
            if (free.Count == 0)
            {
                return SpaceCfg.Mk(int.MinValue, int.MinValue);
            }
            return free[StableStringHash(seed) % free.Count];
        }

        private static int StableStringHash(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }
            unchecked
            {
                int h = 23;
                for (int i = 0; i < s.Length; i++)
                {
                    h = h * 31 + s[i];
                }
                return h == int.MinValue ? 0 : Math.Abs(h);
            }
        }

        private static bool OccupiedByOurs(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            List<ClusterGridEntity> list;
            if (grid == null || !grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is SpaceRift) && !(list[i] is SpaceStation) && !(list[i] is SpaceQuestSite))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Reconcile(ClusterGrid grid, SpaceZones zones, GameObject prefab, List<SpaceStation> live)
        {
            BuildPlan(grid, zones);
            if (Plan.Count == 0)
            {
                return;
            }

            int adoptedLegacy = 0;
            Dictionary<string, SpaceStation> existing = new Dictionary<string, SpaceStation>();
            PruneLive();
            foreach (KeyValuePair<string, SpaceStation> kv in Live)
            {
                if (kv.Value != null)
                {
                    existing[kv.Key] = kv.Value;
                }
            }
            List<SpaceStation> all = new List<SpaceStation>();
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    SpaceStation st = kv.Value[i] as SpaceStation;
                    if (st != null)
                    {
                        all.Add(st);
                    }
                }
            }

            for (int i = 0; i < all.Count; i++)
            {
                SpaceStation st = all[i];
                string id = st.stationId;
                if (string.IsNullOrEmpty(id))
                {
                    id = "legacy@" + SpaceCfg.Pos(st.Location);
                    existing[id] = st;
                    continue;
                }
                if (existing.ContainsKey(id))
                {
                    MFLog.Info(LogCat.Trade,"[Space] 发现同 id 站点（保留，不删除） " + id + " @" + SpaceCfg.Pos(st.Location));
                    continue;
                }
                existing[id] = st;
            }
            foreach (KeyValuePair<string, SpaceStation> kv in Live)
            {
                if (kv.Value != null && !existing.ContainsKey(kv.Key))
                {
                    existing[kv.Key] = kv.Value;
                }
            }

            HashSet<string> wanted = new HashSet<string>();
            for (int i = 0; i < Plan.Count; i++)
            {
                PlannedStation p = Plan[i];
                if (p.cell.Q == int.MinValue)
                {
                    continue;
                }
                wanted.Add(p.id);
                SpaceStation have;
                if (existing.TryGetValue(p.id, out have) && have != null)
                {
                    if (have.Location != p.cell)
                    {
                        AxialI wrong = have.Location;
                        have.Location = p.cell;
                        Debug.LogWarning("[Space] 站点 " + p.id + " 位置自愈 " + SpaceCfg.Pos(wrong)
                            + " → " + SpaceCfg.Pos(p.cell));
                    }
                    continue;
                }
                SpaceStation liveHave;
                if (Live.TryGetValue(p.id, out liveHave) && liveHave != null)
                {
                    continue;
                }
                SpaceStation legacy = FindLegacyAt(all, p.cell);
                if (legacy != null)
                {
                    legacy.stationId = p.id;
                    legacy.displayName = p.name;
                    legacy.kind = p.kind;
                    legacy.subMap = p.sub;
                    legacy.ApplyIdentity();
                    Live[p.id] = legacy;
                    MFLog.Info(LogCat.Trade,"[Space] 收养旧档站点 " + p.id + "（" + p.name + "）@" + SpaceCfg.Pos(p.cell)
                        + "（旧档站位与规划一致，不再删了重建）");
                    continue;
                }
                if (HasStationEntityAt(grid, p.cell) || HasLiveAt(live, p.cell))
                {
                    continue;
                }
                GameObject go = SpawnStation(prefab, p);
                if (go == null)
                {
                    continue;
                }
                SpaceStation spawned = go.GetComponent<SpaceStation>();
                if (spawned != null)
                {
                    live.Add(spawned);
                    Live[p.id] = spawned;
                }
            }

            for (int i = 0; i < all.Count; i++)
            {
                SpaceStation st = all[i];
                if (st == null)
                {
                    continue;
                }
                string id = st.stationId;
                if (string.IsNullOrEmpty(id))
                {
                    st.stationId = "legacy_" + (++adoptedLegacy);
                    if (string.IsNullOrEmpty(st.displayName))
                    {
                        st.displayName = "贸易站·旧档";
                    }
                    st.ApplyIdentity();
                    Live[st.stationId] = st;
                    Debug.LogWarning("[Space] 旧档遗留站点已保留并编号 " + st.stationId
                        + " @" + SpaceCfg.Pos(st.Location));
                }
                else if (!wanted.Contains(id))
                {
                    MFLog.Info(LogCat.Trade,"[Space] 站点 " + id + " 不在当前规划里，保留 @" + SpaceCfg.Pos(st.Location));
                }
            }
        }

        private static SpaceStation FindLegacyAt(List<SpaceStation> all, AxialI cell)
        {
            for (int i = 0; i < all.Count; i++)
            {
                SpaceStation st = all[i];
                if (st != null && string.IsNullOrEmpty(st.stationId) && st.Location == cell)
                {
                    return st;
                }
            }
            return null;
        }

        private static bool HasLiveAt(List<SpaceStation> live, AxialI cell)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i] == null)
                {
                    live.RemoveAt(i);
                    continue;
                }
                if (live[i].Location == cell)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasStationEntityAt(ClusterGrid grid, AxialI cell)
        {
            List<ClusterGridEntity> list;
            if (!grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                SpaceStation s = list[i] as SpaceStation;
                if (s != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static GameObject SpawnStation(GameObject prefab, PlannedStation p)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[Space] 站点 prefab 未注册");
                return null;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                return null;
            }
            SpaceStation st = go.GetComponent<SpaceStation>();
            if (st != null)
            {
                st.stationId = p.id;
                st.displayName = p.name;
                st.kind = p.kind;
                st.subMap = p.sub;
            }
            ClusterGridEntity entity = go.GetComponent<ClusterGridEntity>();
            if (entity == null)
            {
                Debug.LogWarning("[Space] 站点缺少 ClusterGridEntity");
                return null;
            }
            entity.Location = p.cell;
            go.SetActive(true);
            if (st != null)
            {
                st.ApplyIdentity();
            }
            MFLog.Info(LogCat.Trade,"[Space] 生成站点 " + p.id + "（" + p.name + "）@" + SpaceCfg.Pos(p.cell)
                + (p.sub ? " [子图·信号屏蔽区]" : (p.kind == "blackmarket" ? " [危险区]" : " [行星轨道]")));
            return go;
        }
    }

    internal static class SpaceReveal
    {
        internal static readonly HashSet<AxialI> Revealed = new HashSet<AxialI>();
        private static readonly List<AxialI> PendingReveal = new List<AxialI>();

        internal static bool IsRevealed(AxialI cell)
        {
            return Revealed.Contains(cell);
        }

        /// <summary>撤销主动揭示（小行星带采空销毁时调用，避免留下常亮的空格）。</summary>
        internal static void Forget(AxialI cell)
        {
            Revealed.Remove(cell);
            PendingReveal.Remove(cell);
        }

        internal static void Reveal(AxialI cell)
        {
            bool isNew = Revealed.Add(cell);
            if (TryRevealFow(cell))
            {
                if (isNew)
                {
                    MFLog.Info(LogCat.Trade,"[Space] 星图格已揭示：" + SpaceCfg.Pos(cell) + "（视野已点亮，常亮显示）");
                }
                return;
            }
            if (!PendingReveal.Contains(cell))
            {
                PendingReveal.Add(cell);
                Debug.LogWarning("[Space] 星图格揭示暂未生效（迷雾系统未就绪），已排队重试："
                    + SpaceCfg.Pos(cell));
            }
        }

        // 只点亮主图内的格：危险区/空白带/子图一律不点亮，避免实体生成顺手把外层刷亮（见 星图.md P-32）
        internal static void RevealIfMain(AxialI cell)
        {
            SpaceZones z = StarMapVisuals.Zones;
            if (z != null && z.valid && (z.IsSub(cell) || SpaceCfg.Ring(cell) > z.mainMaxRing))
            {
                Forget(cell);
                return;
            }
            Reveal(cell);
        }

        internal static void RetryPending()
        {
            if (PendingReveal.Count == 0)
            {
                return;
            }
            for (int i = PendingReveal.Count - 1; i >= 0; i--)
            {
                AxialI c = PendingReveal[i];
                if (TryRevealFow(c))
                {
                    PendingReveal.RemoveAt(i);
                    MFLog.Info(LogCat.Trade,"[Space] 星图格已揭示（补揭示）：" + SpaceCfg.Pos(c));
                }
            }
        }

        private static bool TryRevealFow(AxialI cell)
        {
            StarMapVisuals.RevealCell(cell, 0);
            return StarMapVisuals.IsCellRevealed(cell);
        }

        internal static bool RevealRandomSubCell()
        {
            SpaceZones z = StarMapVisuals.Zones;
            if (z == null || !z.valid || z.subCells.Count == 0)
            {
                Debug.LogWarning("[Space] 分区未就绪，星图数据暂时解不开（保留购买记录）");
                return false;
            }
            List<AxialI> pool = new List<AxialI>();
            for (int i = 0; i < z.subCells.Count; i++)
            {
                AxialI c = z.subCells[i];
                if (c == z.subRiftCell || Revealed.Contains(c))
                {
                    continue;
                }
                pool.Add(c);
            }
            if (pool.Count == 0)
            {
                Debug.LogWarning("[Space] 子图的格子都已解锁，不需要再买星图数据了");
                TradeUi.Notify("星图数据", "子图坐标已全部解锁");
                return false;
            }
            AxialI pick = pool[UnityEngine.Random.Range(0, pool.Count)];
            Reveal(pick);
            MFLog.Info(LogCat.Trade,"[Space] ★ 购买星图数据 → 随机解锁子图一格 " + SpaceCfg.Pos(pick)
                + "（已解锁 " + Revealed.Count + " 格，子图共 " + z.subCells.Count + " 格）");
            TradeUi.Notify("星图数据", "星图数据已解析，子图有一处星域被揭示（剩余未解锁 "
                + (pool.Count - 1) + " 格）。");
            StarMapVisuals.RefreshNow();
            return true;
        }

        internal static void Capture(List<int> qs, List<int> rs)
        {
            qs.Clear();
            rs.Clear();
            foreach (AxialI c in Revealed)
            {
                qs.Add(c.Q);
                rs.Add(c.R);
            }
        }

        internal static int Restore(List<int> qs, List<int> rs)
        {
            Revealed.Clear();
            if (qs == null || rs == null)
            {
                return 0;
            }
            int n = Mathf.Min(qs.Count, rs.Count);
            for (int i = 0; i < n; i++)
            {
                Revealed.Add(SpaceCfg.Mk(qs[i], rs[i]));
            }
            if (Revealed.Count > 0)
            {
                MFLog.Info(LogCat.Trade,"[Space] 恢复已解锁的子图坐标 " + Revealed.Count + " 格");
            }
            return Revealed.Count;
        }
    }
}
