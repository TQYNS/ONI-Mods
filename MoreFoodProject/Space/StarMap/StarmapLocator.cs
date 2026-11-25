using System;
using EventFramework.Distribution.Contracts;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoreFood_KFC
{
    // 星图定位器：把任务生成的小行星带注册为可跳转目标
    internal static class StarmapLocator
    {
        private static readonly Dictionary<string, AxialI> cells = new Dictionary<string, AxialI>();

        internal static void Register(string id, AxialI cell)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            cells[id] = cell;
        }

        internal static void Drop(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                cells.Remove(id);
            }
        }

        internal static void Clear()
        {
            cells.Clear();
        }

        internal static bool Has(string id)
        {
            AxialI ignored;
            return !string.IsNullOrEmpty(id) && cells.TryGetValue(id, out ignored);
        }

        internal static bool Jump(string id)
        {
            AxialI cell;
            if (string.IsNullOrEmpty(id) || !cells.TryGetValue(id, out cell))
            {
                return false;
            }
            // 目标已消失（采空销毁）则不跳转，并摘掉注册
            if (!HasBeltAt(cell))
            {
                cells.Remove(id);
                MFLog.Warn(LogCat.Space, "[Space] 星图跳转：目标小行星带已不存在，已摘掉该跳转点");
                return false;
            }
            return JumpToCell(cell);
        }

        // 该格里是否还有我们的小行星带。
        private static bool HasBeltAt(AxialI cell)
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
                if (list[i] is SpaceLootField)
                {
                    return true;
                }
            }
            return false;
        }

        // 打开星图 → 选中该格实体（星图整体自适应缩放下等同视野聚焦）。
        internal static bool JumpToCell(AxialI cell)
        {
            try
            {
                ClusterGrid grid = ClusterGrid.Instance;
                if (grid == null || !grid.IsValidCell(cell))
                {
                    return false;
                }
ClusterMapScreen screen = ClusterMapScreen.Instance;
                if (screen == null)
                {
                    return false;
                }
                // 走原版状态机打开（直接 Show / 靠选中工具隐式打开会让 ManagementMenu.activeScreen 不同步 ⇒ ESC 无响应、星图按钮要点两次）
                ManagementMenu mm = ManagementMenu.Instance;
                if (mm != null)
                {
                    mm.OpenClusterMap();
                }
                if (!screen.IsActive())
                {
                    screen.Show(true);
                }
                ClusterGridEntity e = grid.GetVisibleEntityOfLayerAtCell(cell, EntityLayer.POI);
                if (e == null)
                {
                    e = grid.GetVisibleEntityOfLayerAtCell(cell, EntityLayer.Asteroid);
                }
                if (e != null)
                {
                    SelectEntity(screen, e);
                }
                // 聚焦走原版入口（不自己改 mapScrollRect.content：原版 ScreenUpdate 每帧都在写它，且紧跟 FloatyAsteroidAnimation）
                screen.SetTargetFocusPosition(cell, 0.35f);
                StarMapVisuals.YieldZoom();
                StarMapVisuals.RefreshNow();
                MFLog.Info(LogCat.Space, "[Space] ★ 星图跳转：" + SpaceCfg.Pos(cell));
                return true;
            }
            catch (Exception ex)
            {
                // 同上：星图跳转链路的兜底异常不得走 Debug.LogError（LogCatcher 判致命）。
                MFLog.Warn(LogCat.Space, "[Space] 星图跳转失败 | " + ex);
                return false;
            }
        }

        // SetSelectedEntity 在原版是私有方法 → 反射调用；参数按运行时签名适配（默认值不会由反射自动补齐）。
        private static void SelectEntity(ClusterMapScreen screen, ClusterGridEntity e)
        {
            try
            {
                MethodInfo m = typeof(ClusterMapScreen).GetMethod("SetSelectedEntity",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null)
                {
                    MFLog.Warn(LogCat.Space, "[Space] 星图选中跳过：当前版本无 SetSelectedEntity");
                    return;
                }
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length == 0)
                {
                    MFLog.Warn(LogCat.Space, "[Space] 星图选中跳过：SetSelectedEntity 无实体参数");
                    return;
                }
                // 当前版本签名为 (ClusterGridEntity, bool frameDelay = false)；按实际参数个数补默认值，
                // 兼容旧版 1 参与未来多参版本，避免 TargetParameterCountException。
                object[] args = new object[ps.Length];
                args[0] = e;
                for (int i = 1; i < ps.Length; i++)
                {
                    Type t = ps[i].ParameterType;
                    args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
                }
                m.Invoke(screen, args);
            }
            catch (Exception ex)
            {
                // 反射边界属版本差异高发处：禁止 LogError（LogCatcher 会判致命），但仍带完整堆栈留痕。
                MFLog.Warn(LogCat.Space, "[Space] 星图选中失败（已跳过选中，跳转继续）| " + ex);
            }
        }
    }
}