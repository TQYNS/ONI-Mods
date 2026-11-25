// 用途：着陆 / 停飞后清理星图上残留的飞行路线与箭头
// 背景：星图路线是每个实体自己的 ClusterMapVisualizer.mapPath 画的（ClusterMapPath 独立 GameObject），
//       只在"目标变更 / 显式刷新"时重绘；火箭落地后 mapPath 没有被销毁，就留下
//       「由原所在格指向星球」的箭头。这里在状态落到 Grounded 的瞬间强制该实体重绘一次，
//       让原版逻辑自己把 mapPath（含箭头）销毁，不碰任何私有字段。

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class StarMapPathCleanup
    {
        // 星图路径的容器（ClusterMapPathDrawer.pathContainer）—— 所有路线/箭头都是它的子物体。
        // 只取一次字段引用，之后无反射开销（低频调用）。
        private static FieldInfo drawerField;
        private static bool drawerFieldSearched;

        // 让指定火箭的星图路径按当前状态重画：不在飞行途中时，原版实现会销毁 mapPath。
        internal static void ClearFor(Clustercraft craft)
        {
            if (craft == null || craft.gameObject == null)
            {
                return;
            }
            try
            {
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                if (screen == null)
                {
                    return;   // 星图没开过，不存在可视对象，无需处理
                }
                ClusterMapVisualizer vis = screen.GetEntityVisAnim(craft);
                if (vis == null)
                {
                    return;
                }
                vis.RefreshPathDrawing();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 清理星图路径失败");
            }
        }

        //
        // 着陆后统一清理。原版只在"目的地变化"时重绘路径，落地这一刻没人触发，
        // 于是 mapPath（含箭头）留在原地。这里做三件事，且完全不动 selector 目的地
        // （玩家落地后要自己选新目的地）：
        // ① 把路径容器里的残留对象全部销毁；② 仍在下行的火箭重画自己的路线；
        // ③ 本机再单独重绘一次（不存在飞行路径时原版会清空自己持有的引用）。
        //
        internal static void OnLanded(Clustercraft craft)
        {
            // ① 落地后 selector 若仍停在旧目的地（原版 IsTraveling 就是 !IsAtDestination），
            //    路径会被判定为"仍在飞行"而反复重画 —— 先把目的地收回当前格，路径才真正结束。
            //    只在"刚落地"这一刻做一次，不影响玩家之后选新目的地。
            try
            {
                if (craft != null)
                {
                    ClusterTraveler tr = craft.GetComponent<ClusterTraveler>();
                    ClusterDestinationSelector sel = craft.GetComponent<ClusterDestinationSelector>();
                    if (sel != null && tr != null && tr.IsTraveling())
                    {
                        sel.SetDestination(craft.Location);
                        MFLog.Info(LogCat.Space, "[Space] 落地收尾：目的地收回当前格 → " + SpaceCfg.Pos(craft.Location));
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 落地收尾失败");
            }
            // ② 销毁容器里所有残留路线/箭头（含 pathStart 箭头），③ 只给"仍在星图中飞行"的火箭重画
            ResetAllPaths();
            RedrawTravelling();
            ClearFor(craft);
        }

        // 销毁路径容器下的全部路线/箭头对象（含原版预览路径），由后续重绘按需重建。
        internal static void ResetAllPaths()
        {
            Transform container = PathContainer();
            if (container == null)
            {
                return;
            }
            try
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    Transform child = container.GetChild(i);
                    if (child != null && child.gameObject != null)
                    {
                        Util.KDestroyGameObject(child.gameObject);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 清空星图路径容器失败");
            }
        }

        // 让仍在飞行途中的火箭重新画出路线（容器刚被清空）。
        private static void RedrawTravelling()
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || grid.cellContents == null)
            {
                return;
            }
            try
            {
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
                {
                    List<ClusterGridEntity> list = kv.Value;
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        Clustercraft c = list[i] as Clustercraft;
                        if (c == null)
                        {
                            continue;
                        }
                        // 只重画"仍在星图中 + 仍在飞行"的火箭：已落地/发射中/着陆中的一律不画，
                        // 否则等于把刚清掉的箭头又画回来
                        if (c.Status != Clustercraft.CraftStatus.InFlight)
                        {
                            continue;
                        }
                        ClusterTraveler tr = c.GetComponent<ClusterTraveler>();
                        if (tr != null && tr.IsTraveling())
                        {
                            ClearFor(c);   // RefreshPathDrawing：飞行中 ⇒ 重建路线
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 重画飞行路线失败");
            }
        }

        private static Transform PathContainer()
        {
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null)
            {
                return null;
            }
            try
            {
                if (!drawerFieldSearched)
                {
                    drawerFieldSearched = true;
                    drawerField = typeof(ClusterMapScreen).GetField("pathDrawer",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                if (drawerField == null)
                {
                    return null;
                }
                ClusterMapPathDrawer drawer = drawerField.GetValue(screen) as ClusterMapPathDrawer;
                return drawer != null ? drawer.pathContainer : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        //
        // 兜底扫描：把所有「已不在飞行途中」的火箭路径重绘一遍。
        // 只在着陆/停飞这类低频时机调用一次，不进帧循环。
        //
        internal static void ClearAllStopped()
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || grid.cellContents == null)
            {
                return;
            }
            try
            {
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
                {
                    List<ClusterGridEntity> list = kv.Value;
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        Clustercraft c = list[i] as Clustercraft;
                        if (c == null)
                        {
                            continue;
                        }
                        ClusterTraveler tr = c.GetComponent<ClusterTraveler>();
                        if (tr != null && tr.IsTraveling())
                        {
                            continue;   // 还在飞的保留路线
                        }
                        ClearFor(c);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星图残留路径批量清理失败");
            }
        }
    }
}
