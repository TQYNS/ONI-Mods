using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class SpaceTestRuntime
    {
        private static GameObject legacyRiftPrefab;
        private static GameObject fakeRiftPrefab;
        private static GameObject stationPrefab;

        // 本会话刚生成的实体：cellContents 的注册要下一帧才生效，
        private static readonly List<SpaceRift> liveFakes = new List<SpaceRift>();
        private static readonly List<SpaceStation> liveStations = new List<SpaceStation>();

        internal static void ResetForLoad()
        {
            liveFakes.Clear();
            liveStations.Clear();
            fakeRiftPrefab = null;
            stationPrefab = null;
            contentBuilt = false;   // 问题9：读档后允许重建一次
        }

        internal static void LogConfig()
        {
            MFLog.Verbose(LogCat.Space,"[Space] 配置 环数=" + SpaceCfg.NumRings
                + " 子图半径=" + SpaceCfg.SubRadius
                + " 子图间隔格=" + SpaceCfg.SubGapCells
                + " 信号屏蔽半径=" + SpaceCfg.VisionRadius
                + " 危险区环数=" + (SpaceCfg.DangerRingSpan + 1)
                + " 站点上限=" + SpaceCfg.MaxStations);
        }

        internal static void RegisterPrefabs()
        {
            if (legacyRiftPrefab == null)
            {
                legacyRiftPrefab = SpaceRift.CreatePrefab(SpaceCfg.MainRiftPrefabId, "时空裂缝", false);
                MFLog.Verbose(LogCat.Space,"[Space] 注册 prefab " + SpaceCfg.MainRiftPrefabId + "（仅为兼容旧档，不再生成）");
            }
            if (fakeRiftPrefab == null)
            {
                fakeRiftPrefab = SpaceRift.CreatePrefab(SpaceCfg.FakeRiftPrefabId, "时空裂隙", true);
                MFLog.Verbose(LogCat.Space,"[Space] 注册 prefab " + SpaceCfg.FakeRiftPrefabId);
            }
            if (stationPrefab == null)
            {
                stationPrefab = SpaceStation.CreatePrefab(SpaceCfg.StationPrefabId, "贸易站");
                MFLog.Verbose(LogCat.Space,"[Space] 注册 prefab " + SpaceCfg.StationPrefabId);
            }
            CombatRuntime.RegisterPrefabs();
        }

        internal static void LogGridStats(ClusterGrid grid)
        {
            if (grid == null)
            {
                return;
            }
            int occupied = 0;
            int maxEntityRing = 0;
            int empty = 0;
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                {
                    empty++;
                    continue;
                }
                occupied++;
                int ring = SpaceCfg.Ring(kv.Key);
                if (ring > maxEntityRing)
                {
                    maxEntityRing = ring;
                }
            }
            MFLog.Verbose(LogCat.Space,"[Space] 星图 rings=" + grid.numRings
                + " 总格数=" + grid.cellContents.Count
                + " 有实体格=" + occupied
                + " 空格=" + empty
                + " 原版内容最大环=" + maxEntityRing);
        }

        internal static void OnWorldReady()
        {
            LogGridStats(ClusterGrid.Instance);
            RegisterPrefabs();
            TradeRegistry.Init();
            // 读档恢复【不在这里】——OnWorldReady 会在"载入途中"被触发（实测 17:52:33.293，
            EnsureTestContent();
            MFLog.Verbose(LogCat.Space,"[Space] 星球生成完毕 → 已检查并补齐裂缝、贸易站、任务地点");
        }

        internal static void RebuildZones()
        {
            ClusterGrid grid = ClusterGrid.Instance;
            bool hasFake = false;
            AxialI fakeHint = AxialI.ZERO;
            SpaceRift fake = FindFakeRift(grid);
            if (fake != null)
            {
                hasFake = true;
                fakeHint = fake.Location;
            }
            StarMapVisuals.SetZones(SpaceZones.Build(grid, hasFake, fakeHint));
        }

        private static bool contentBuilt;   // 问题9：一次会话内容已建成标记（去重高频重建）

        internal static void EnsureTestContent()
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return;
            }
            // 问题9：内容已建成且分区有效 → 直接返回，不再全量重建。
            if (contentBuilt && StarMapVisuals.Zones != null && StarMapVisuals.Zones.valid)
            {
                return;
            }
            RebuildZones();
            SpaceLoadTrace.Set("分区已算出");
            SpaceZones zones = StarMapVisuals.Zones;
            if (zones == null || !zones.valid)
            {
                return;
            }
            ReconcileRifts(grid);
            SpaceLoadTrace.Set("裂缝核对完成");
            EnsureFakeRift(grid, zones);
            SpaceLoadTrace.Set("时空裂隙就绪");
            TradeStationManager.Reconcile(grid, zones, stationPrefab, liveStations);
            SpaceLoadTrace.Set("贸易站核对完成");
            contentBuilt = true;
            LogZones(zones);
        }

        internal static SpaceRift FindFakeRift(ClusterGrid grid)
        {
            if (grid == null)
            {
                return null;
            }
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    SpaceRift rift = kv.Value[i] as SpaceRift;
                    if (rift != null && rift.IsFake)
                    {
                        return rift;
                    }
                }
            }
            return null;
        }

        private static void ReconcileRifts(ClusterGrid grid)
        {
            List<SpaceRift> fakes = new List<SpaceRift>();
            List<SpaceRift> strays = new List<SpaceRift>();
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    SpaceRift rift = kv.Value[i] as SpaceRift;
                    if (rift == null)
                    {
                        continue;
                    }
                    if (!rift.IsFake)
                    {
                        strays.Add(rift);
                    }
                    else
                    {
                        fakes.Add(rift);
                    }
                }
            }

            for (int i = 0; i < strays.Count; i++)
            {
                MFLog.Verbose(LogCat.Space,"[Space] 清理旧版时空裂缝 " + SpaceCfg.Pos(strays[i].Location) + "（入口改用原版时间裂口）");
                Util.KDestroyGameObject(strays[i].gameObject);
            }
            for (int i = 1; i < fakes.Count; i++)
            {
                MFLog.Verbose(LogCat.Space,"[Space] 清理多余时空裂隙 " + fakes[i].Location);
                Util.KDestroyGameObject(fakes[i].gameObject);
            }
        }

        private static void EnsureFakeRift(ClusterGrid grid, SpaceZones zones)
        {
            SpaceRift existing = FindFakeRift(grid);
            if (existing != null)
            {
                if (!zones.IsSub(existing.Location))
                {
                    AxialI wrong = existing.Location;
                    existing.Location = zones.subRiftCell;
                    MFLog.Warn(LogCat.Space,"[Space] 时空裂隙原本落在 " + SpaceCfg.Pos(wrong) + "（不在子图内，疑似过早生成）→ 已搬到 " + SpaceCfg.Pos(zones.subRiftCell));
                }
                return;
            }
            if (HasLiveFake())
            {
                return;
            }
            AxialI cell = zones.subRiftCell;
            if (!grid.IsValidCell(cell))
            {
                MFLog.Warn(LogCat.Space,"[Space] 时空裂隙目标格无效 " + SpaceCfg.Pos(cell));
                return;
            }
            GameObject go = SpawnPrefab(fakeRiftPrefab, cell);
            if (go == null)
            {
                return;
            }
            SpaceRift spawned = go.GetComponent<SpaceRift>();
            if (spawned != null)
            {
                liveFakes.Add(spawned);
            }
            MFLog.Verbose(LogCat.Space,"[Space] 生成 时空裂隙 " + SpaceCfg.Pos(cell) + " 环=" + SpaceCfg.Ring(cell));
        }

        private static bool HasLiveFake()
        {
            for (int i = liveFakes.Count - 1; i >= 0; i--)
            {
                if (liveFakes[i] == null)
                {
                    liveFakes.RemoveAt(i);
                }
            }
            return liveFakes.Count > 0;
        }

        private static void LogZones(SpaceZones zones)
        {
            MFLog.Verbose(LogCat.Space,"[Space] 分区 主图最大环=" + zones.mainMaxRing
                + " 可见外沿=" + zones.visibleEdgeRing
                + " 危险区=扇区" + zones.dangerSector + " 环" + zones.dangerRingLo + "~" + zones.dangerRingHi
                + "(" + zones.dangerCells.Count + "格)"
                + " 子图 中心=" + SpaceCfg.Pos(zones.subCenter) + " 半径=" + zones.subRadius
                + "(" + zones.subCells.Count + "格) 时空裂隙=" + SpaceCfg.Pos(zones.subRiftCell)
                + " 规划站点=" + zones.stationSlots.Count);
        }

        internal static TemporalTear GetTear()
        {
            ClusterManager cm = ClusterManager.Instance;
            if (cm == null)
            {
                return null;
            }
            ClusterPOIManager poi = cm.GetComponent<ClusterPOIManager>();
            return poi != null ? poi.GetTemporalTear() : null;
        }

        internal static void EnterTear(TemporalTear tear)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (tear == null || grid == null)
            {
                return;
            }
            if (!tear.IsOpen())
            {
                MFLog.Warn(LogCat.Space,"[Space] 时间裂口还没打开，先让裂口开启");
                return;
            }
            Clustercraft craft = FindCraftAt(grid, tear.Location);
            if (craft == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 裂口上没有火箭：先把火箭飞到裂口所在格再点这个按钮");
                return;
            }
            if (!TryTearTakeover(tear, craft))
            {
                MFLog.Warn(LogCat.Space,"[Space] 接管条件不满足（火箭是否在飞行中？）");
            }
        }

        internal static bool TryTearTakeover(TemporalTear tear, Clustercraft craft)
        {
            if (tear == null || craft == null)
            {
                return false;
            }
            if (!tear.IsOpen() || craft.Location != tear.Location || craft.IsFlightInProgress())
            {
                return false;
            }

            EnsureTestContent();
            SpaceZones zones = StarMapVisuals.Zones;
            if (zones == null || !zones.valid)
            {
                return false;
            }

            AxialI to = zones.subRiftCell;
            AxialI from = craft.Location;
            WarpRuntime.InstantJump(craft, to);
            SetConsumedCraft(tear);

            StarMapVisuals.RefreshNow();
            SpaceAchievements.Unlock(SpaceAchievements.TearTakeoverId, "穿过裂口的人");
            MFLog.Verbose(LogCat.Space,"[Space] 接管时间裂口：不销毁火箭与舱内复制人，" + craft.Name + " "
                + SpaceCfg.Pos(from) + " -> " + SpaceCfg.Pos(to)
                + "（结算动画/成就照常触发）");
            return true;
        }

        private static void SetConsumedCraft(TemporalTear tear)
        {
            FieldInfo f = typeof(TemporalTear).GetField("m_hasConsumedCraft", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null)
            {
                f.SetValue(tear, true);
            }
            else
            {
                MFLog.Warn(LogCat.Space,"[Space] 没找到 m_hasConsumedCraft，成就可能不会触发");
            }
        }

        internal static void RiftJump(SpaceRift rift)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            SpaceZones zones = StarMapVisuals.Zones;
            if (grid == null || zones == null || !zones.valid || rift == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 裂缝/星图未就绪");
                return;
            }

            TemporalTear tear = GetTear();
            AxialI to;
            if (tear != null)
            {
                to = tear.Location;
            }
            else
            {
                WorldContainer start = ClusterManager.Instance != null ? ClusterManager.Instance.GetStartWorld() : null;
                to = start != null ? FindWorldCell(grid, start.id) : SpaceCfg.Mk(2, 0);
            }

            Clustercraft craft = FindCraftAt(grid, rift.Location);
            if (craft == null)
            {
                craft = FindCraftAt(grid, zones.subRiftCell);
            }
            if (craft == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 裂缝上没有火箭");
                return;
            }

            AxialI from = craft.Location;
            WarpRuntime.InstantJump(craft, to);

            StarMapVisuals.RefreshNow();
            MFLog.Verbose(LogCat.Space,"[Space] 时空裂隙返程 " + craft.Name + " " + SpaceCfg.Pos(from) + " -> " + SpaceCfg.Pos(to)
                + "（回到时间裂口处，可再次进入）");
        }

        private static AxialI FindWorldCell(ClusterGrid grid, int worldId)
        {
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    ClusterGridEntity e = kv.Value[i];
                    if (e == null)
                    {
                        continue;
                    }
                    WorldContainer wc = e.GetComponent<WorldContainer>();
                    if (wc != null && wc.id == worldId)
                    {
                        return kv.Key;
                    }
                }
            }
            return SpaceCfg.Mk(2, 0);
        }

        internal static void PathCheckTest()
        {
            ClusterGrid grid = ClusterGrid.Instance;
            SpaceZones zones = StarMapVisuals.Zones;
            if (grid == null || zones == null || !zones.valid)
            {
                MFLog.Warn(LogCat.Space,"[Space] 星图/分区未就绪");
                return;
            }
            Clustercraft craft = FindAnyRocket(grid);
            if (craft == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 需要一艘火箭（先建造并发射）");
                return;
            }
            ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
            if (selector == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 火箭缺少 ClusterDestinationSelector");
                return;
            }

            bool savedFog = selector.canNavigateFogOfWar;
            AxialI from = craft.Location;
            AxialI to = zones.subRiftCell;
            string reason;

            selector.canNavigateFogOfWar = false;
            List<AxialI> p1 = grid.GetPath(from, to, selector, out reason, false);
            MFLog.Verbose(LogCat.Space,"[Space] ① 普通火箭 " + SpaceCfg.Pos(from) + " -> " + SpaceCfg.Pos(to) + " : "
                + (p1 == null ? "不可达（" + reason + "）" : "可达 steps=" + p1.Count));

            selector.canNavigateFogOfWar = true;
            List<AxialI> p2 = grid.GetPath(from, to, selector, out reason, false);
            MFLog.Verbose(LogCat.Space,"[Space] ② 可穿雾火箭 : "
                + (p2 == null ? "不可达（" + reason + "）" : "可达 steps=" + p2.Count));

            selector.canNavigateFogOfWar = savedFog;
        }

        private static GameObject SpawnPrefab(GameObject prefab, AxialI cell)
        {
            RegisterPrefabs();
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] prefab 未注册");
                return null;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 实例化失败");
                return null;
            }
            ClusterGridEntity entity = go.GetComponent<ClusterGridEntity>();
            if (entity == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 缺少 ClusterGridEntity");
                return null;
            }
            entity.Location = cell;
            go.SetActive(true);
            return go;
        }

        private static Clustercraft FindCraftAt(ClusterGrid grid, AxialI cell)
        {
            List<ClusterGridEntity> list;
            if (!grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return null;
            }
            for (int i = 0; i < list.Count; i++)
            {
                Clustercraft craft = list[i] as Clustercraft;
                if (craft != null)
                {
                    return craft;
                }
            }
            return null;
        }

        private static Clustercraft FindAnyRocket(ClusterGrid grid)
        {
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kvp in grid.cellContents)
            {
                foreach (ClusterGridEntity entity in kvp.Value)
                {
                    Clustercraft craft = entity as Clustercraft;
                    if (craft != null)
                    {
                        return craft;
                    }
                }
            }
            return null;
        }

        private static ClusterFogOfWarManager.Instance GetFow()
        {
            if (SaveGame.Instance == null)
            {
                return null;
            }
            return SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        }
    }
}
