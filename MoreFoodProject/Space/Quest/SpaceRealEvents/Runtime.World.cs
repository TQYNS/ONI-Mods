// 用途：世界与格位：主世界判定、空位搜索与步进

using System;
using EventFramework.Distribution.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSerialization;
using Klei.AI;
using Klei.AI.DiseaseGrowthRules;
using STRINGS;
using TUNING;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC
{
    internal static partial class SpaceRealEventRuntime
    {
        internal static bool IsMainWorld(WorldContainer w)
        {
            if (w == null || w.IsModuleInterior || w.GetComponent<SpaceQuestSite>() != null)
            {
                return false;
            }
            if (w.ParentWorldId != w.id)
            {
                return false;
            }
            if (w.WorldSize.x <= 0 || w.WorldSize.y <= 0)
            {
                return false;
            }
            return true;
        }

        internal static WorldContainer MainWorld()
        {
            ClusterManager cm = ClusterManager.Instance;
            if (cm == null)
            {
                return null;
            }
            WorldContainer active = cm.GetWorld(cm.activeWorldId);
            if (IsMainWorld(active))
            {
                return active;
            }
            WorldContainer start = cm.GetStartWorld();
            if (IsMainWorld(start))
            {
                return start;
            }
            for (int i = 0; i < cm.WorldContainers.Count; i++)
            {
                WorldContainer w = cm.WorldContainers[i];
                if (IsMainWorld(w))
                {
                    return w;
                }
            }
            return null;
        }

        internal static int MainWorldId()
        {
            WorldContainer w = MainWorld();
            return w != null ? w.id : -1;
        }

        // 取场景实际存在的组件：含非活跃对象，用 scene.IsValid 剔除 prefab
        internal static List<T> AllSceneComponents<T>() where T : Component
        {
            List<T> list = new List<T>();
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(typeof(T));
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    T c = all[i] as T;
                    if (c == null) continue;
                    GameObject go = c.gameObject;
                    if (go == null || !go.scene.IsValid()) continue;
                    // 排除未激活场景对象：其内部字段为 null，调用实例方法会 NRE
                    if (!go.activeInHierarchy) continue;
                    list.Add(c);
                }
            }
            return list;
        }

        internal static bool IsInActiveWorld(GameObject go)
        {
            if (go == null || ClusterManager.Instance == null)
            {
                return false;
            }
            int cell = Grid.PosToCell(go);
            if (!Grid.IsValidCell(cell))
            {
                return false;
            }
            int worldId = MainWorldId();
            return worldId >= 0 && Grid.WorldIdx[cell] == worldId;
        }

        internal static AxialI InvalidCell()
        {
            return SpaceCfg.Mk(int.MinValue, int.MinValue);
        }

        internal static bool CellOk(AxialI c)
        {
            return c.Q != int.MinValue;
        }

        internal static bool CellFree(AxialI c)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || !grid.IsValidCell(c))
            {
                return false;
            }
            List<ClusterGridEntity> list;
            if (grid.cellContents.TryGetValue(c, out list) && list != null && list.Count > 0)
            {
                return false;
            }
            return true;
        }

        internal static AxialI HomePlanetCell()
        {
            ClusterManager cm = ClusterManager.Instance;
            if (cm == null)
            {
                return InvalidCell();
            }
            int active = cm.activeWorldId;
            AxialI fallback = InvalidCell();
            foreach (WorldContainer wc in cm.WorldContainers)
            {
                if (wc == null || wc.IsModuleInterior)
                {
                    continue;
                }
                ClusterGridEntity e = wc.GetComponent<ClusterGridEntity>();
                if (e == null || e.Layer != EntityLayer.Asteroid)
                {
                    continue;
                }
                if (wc.id == active)
                {
                    return e.Location;
                }
                if (!CellOk(fallback))
                {
                    fallback = e.Location;
                }
            }
            return fallback;
        }

        internal static AxialI FindFreeCellNear(AxialI center, int minD, int maxD)
        {
            if (!CellOk(center))
            {
                return InvalidCell();
            }
            for (int i = 0; i < 500; i++)
            {
                int q = center.Q + UnityEngine.Random.Range(-maxD, maxD + 1);
                int r = center.R + UnityEngine.Random.Range(-maxD, maxD + 1);
                AxialI c = SpaceCfg.Mk(q, r);
                int d = SpaceCfg.Distance(c, center);
                if (d < minD || d > maxD)
                {
                    continue;
                }
                if (CellFree(c))
                {
                    return c;
                }
            }
            return InvalidCell();
        }

        internal static AxialI FindEdgeCell()
        {
            SpaceZones z = StarMapVisuals.Zones;
            if (z == null || !z.valid || z.mainEdgeCells.Count == 0)
            {
                return InvalidCell();
            }
            return z.mainEdgeCells[UnityEngine.Random.Range(0, z.mainEdgeCells.Count)];
        }

        internal static int StepToward(AxialI from, AxialI to)
        {
            int bestDir = -1;
            int bestDist = SpaceCfg.Distance(from, to);
            for (int i = 0; i < 6; i++)
            {
                AxialI n = SpaceCfg.Neighbor(from, i);
                int d = SpaceCfg.Distance(n, to);
                if (d >= bestDist)
                {
                    continue;
                }
                if (!CellFree(n))
                {
                    continue;
                }
                bestDist = d;
                bestDir = i;
            }
            return bestDir;
        }
    }
}
