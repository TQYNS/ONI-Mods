using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class StarMapVisuals
    {
        private const string OverlayName = "SpaceZoneBg";

        private static readonly FieldInfo fScrollRect = typeof(ClusterMapScreen).GetField("mapScrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo fCellVis = typeof(ClusterMapScreen).GetField("m_cellVisByLocation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo fCurZoom = typeof(ClusterMapScreen).GetField("m_currentZoomScale", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo fTgtZoom = typeof(ClusterMapScreen).GetField("m_targetZoomScale", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo fRevealPoints = typeof(ClusterFogOfWarManager.Instance).GetField("m_revealPointsByCell", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo mRevealCell = typeof(ClusterFogOfWarManager.Instance).GetMethod("RevealCellIfValid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(AxialI), typeof(int) }, null);

        // 怪物死亡/离场即时同步图标：原版 UpdateVis 只在事件驱动时跑，不每帧跑
        private static MethodInfo mRemoveDeletedEntities;
        private static MethodInfo mUpdateVis;

        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        private static SpaceZones zones;
        private static int framesLeft;
        private static int settleTicks;
        private static bool zonesApplied;
        private static bool fitDone;
        private static bool userZoomed;
        private static float fitScale = 100f;
        private static float zoomTarget = 100f;

        // 迷你条被动重排——记录上次通知过的地图缩放值
        private static float lastNotifiedScale = -1f;

        // 问题9：抑制本次刷新的全图 Trigger（RefreshNow 用）+ 节流（OnScreenUpdate 用）
        private static bool suppressFullRefresh;
        private static float lastFullRefreshAt;

        // 性能修复：每帧路径对象缓存（引用变化即失效；ResetForLoad 清空）
        private static ClusterMapScreen cachedScreen;                          // 当前缓存的屏幕实例
        private static Dictionary<AxialI, ClusterMapVisualizer> cachedCells;   // == screen.m_cellVisByLocation
        private static ScrollRect cachedScroll;                                // == screen.mapScrollRect
        private static ClusterFogOfWarManager.Instance cachedFow;              // 迷雾管理器单例
        private static Dictionary<AxialI, float> cachedReveal;                 // == fow.m_revealPointsByCell
        private static Dictionary<AxialI, ClusterMapVisualizer> hexCacheSrc;   // hexCache 对应的 cells 引用
        private static readonly Dictionary<AxialI, ClusterMapHex> hexCache =
            new Dictionary<AxialI, ClusterMapHex>();        // 格 → ClusterMapHex（消除每帧 GetComponent）
        private static readonly Dictionary<ClusterMapHex, Image> overlayCache =
            new Dictionary<ClusterMapHex, Image>();         // 格 → 底色 Image（消除每帧 transform.Find）
        private static readonly Dictionary<AxialI, Color> lastMaskColor =
            new Dictionary<AxialI, Color>();                // 上次写入的遮罩色（去抖）
        private static readonly List<CraftVision> craftVisionBuffer = new List<CraftVision>();  // 复用缓冲
        private static readonly HashSet<AxialI> nearVisibleCells = new HashSet<AxialI>();     // 合法半揭示邻域（见 星图.md P-34）
        private static readonly List<AxialI> junkPeekBuffer = new List<AxialI>();             // 脏半揭示复用缓冲
        private static bool baselineLogged;                                                   // 兜底补揭示只记一次日志

        internal static SpaceZones Zones
        {
            get { return zones; }
        }

        internal static void SetZones(SpaceZones z)
        {
            zones = z;
            RepairJunkPeek();   // 建区即修一次脏半揭示（见 星图.md P-34）
        }

        internal static void ResetForLoad()
        {
            zones = null;
            framesLeft = 0;
            settleTicks = 0;
            zonesApplied = false;
            fitDone = false;
            userZoomed = false;
            lastNotifiedScale = -1f;
            suppressFullRefresh = false;
            lastFullRefreshAt = 0f;
            // 性能修复：清空每帧缓存（屏幕重建/读档后旧对象全部失效）
            cachedScreen = null;
            cachedCells = null;
            cachedScroll = null;
            cachedFow = null;
            cachedReveal = null;
            hexCache.Clear();
            hexCacheSrc = null;
            overlayCache.Clear();
            lastMaskColor.Clear();
            craftVisionBuffer.Clear();
            nearVisibleCells.Clear();
            junkPeekBuffer.Clear();
            baselineLogged = false;
        }

        // 只把传入的这一格写成全额揭示；第二参是窥视半径（邻格只是微亮），与揭示范围无关
        internal static void RevealCell(AxialI cell, int peekRadius)
        {
            try
            {
                ClusterFogOfWarManager.Instance fow = GetFow();
                if (fow == null || mRevealCell == null)
                {
                    return;
                }
                mRevealCell.Invoke(fow, new object[] { cell, peekRadius });   // 内部方法，走反射（与 FowMaskPatch 同款取法）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 揭示星图格失败");
            }
        }

        // 合法半揭示例：原版补光围绕可见格 2 环、望远镜扫描目标在可见星球 3 环内（见 星图.md P-34）
        private const int LegitPeekRadius = 3;

        // 只清「远离任何可见格」的半揭示：正常游戏里不存在这种格子，它是全图揭示调试留下的脏数据
        internal static int RepairJunkPeek()
        {
            try
            {
                ClusterFogOfWarManager.Instance fow = GetFow();
                if (fow == null)
                {
                    return 0;
                }
                Dictionary<AxialI, float> reveal = GetRevealPoints(fow);
                if (reveal == null || reveal.Count == 0)
                {
                    return 0;
                }
                float threshold = VisiblePoints;
                int visible = 0;
                int peek = 0;
                foreach (KeyValuePair<AxialI, float> kv in reveal)
                {
                    if (kv.Value >= threshold)
                    {
                        visible++;
                    }
                    else if (kv.Value > 0f)
                    {
                        peek++;
                    }
                }
                // 正常态半揭示只可能贴着可见格，数量不可能超过可见格的数倍：不做邻域展开，零开销跳过
                if (peek <= visible * 2 + 256)
                {
                    return 0;
                }
                nearVisibleCells.Clear();
                foreach (KeyValuePair<AxialI, float> kv in reveal)
                {
                    if (kv.Value < threshold)
                    {
                        continue;
                    }
                    List<AxialI> ring = AxialUtil.GetAllPointsWithinRadius(kv.Key, LegitPeekRadius);
                    if (ring == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < ring.Count; i++)
                    {
                        nearVisibleCells.Add(ring[i]);
                    }
                }
                junkPeekBuffer.Clear();
                foreach (KeyValuePair<AxialI, float> kv in reveal)
                {
                    if (kv.Value <= 0f || kv.Value >= threshold)
                    {
                        continue;
                    }
                    if (nearVisibleCells.Contains(kv.Key))
                    {
                        continue;
                    }
                    junkPeekBuffer.Add(kv.Key);
                }
                for (int i = 0; i < junkPeekBuffer.Count; i++)
                {
                    reveal[junkPeekBuffer[i]] = 0f;
                }
                if (junkPeekBuffer.Count > 0)
                {
                    MFLog.Info(LogCat.Space, "[Space] ★ 清理脏半揭示 " + junkPeekBuffer.Count + " 格 → 全黑（可见格 "
                        + visible + " 格，半揭示 " + peek + " 格；见 星图.md P-34）");
                }
                return junkPeekBuffer.Count;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 清理脏半揭示失败");
                return 0;
            }
        }

        // 一个可见格都没有时补回已发现星球：全图揭示调试期间原版不写揭示点，关掉后会是整片全黑
        internal static void EnsureWorldBaseline()
        {
            try
            {
                ClusterFogOfWarManager.Instance fow = GetFow();
                if (fow == null)
                {
                    return;
                }
                Dictionary<AxialI, float> reveal = GetRevealPoints(fow);
                if (reveal != null)
                {
                    float threshold = VisiblePoints;
                    foreach (KeyValuePair<AxialI, float> kv in reveal)
                    {
                        if (kv.Value >= threshold)
                        {
                            return;
                        }
                    }
                }
                fow.UpdateRevealedCellsFromDiscoveredWorlds();
                if (!baselineLogged)
                {
                    baselineLogged = true;
                    MFLog.Info(LogCat.Space, "[Space] 星图无任何可见格 → 按已发现星球补回基础揭示（见 星图.md P-34）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 补回星球基础揭示失败");
            }
        }

        internal static void VerifyReflection()
        {
            MFLog.Info(LogCat.Space, "[Space] 反射自检 mapScrollRect=" + (fScrollRect != null)
                + " m_cellVisByLocation=" + (fCellVis != null)
                + " m_currentZoomScale=" + (fCurZoom != null)
                + " m_targetZoomScale=" + (fTgtZoom != null)
                + " m_revealPointsByCell=" + (fRevealPoints != null));
        }

        internal static void OnScreenShown(ClusterMapScreen screen)
        {
            SpaceTestRuntime.EnsureTestContent();
            int repaired = RepairJunkPeek();   // 开图再兜一次脏半揭示（见 星图.md P-34）
            EnsureWorldBaseline();             // 一个可见格都没有时补回已发现星球（见 星图.md P-34）
            if (repaired > 0)
            {
                RefreshEntityVisuals();        // 直接改字典不会触发原版刷新，只有 UpdateVis 会调 SetRevealed
            }
            zonesApplied = false;
            fitDone = false;
            userZoomed = false;
            framesLeft = 12;
            settleTicks = 3;
            lastNotifiedScale = -1f;   // 重开星图，先复位（下一帧第一次比较一定会触发一次通知）
        }

        internal static void OnScreenUpdate(ClusterMapScreen screen)
        {
            // 迷你条被动重排——每帧只做一次 float 比较，
            try
            {
                float scale = GetMapScale(screen);
                if (scale > 0.0001f && Mathf.Abs(scale - lastNotifiedScale) > 0.0001f)
                {
                    lastNotifiedScale = scale;
                    CombatBars.OnMapScaleChanged();
                }
            }
            catch (Exception)
            {
                // expected: 缩放读取失败时本帧不通知迷你条重排
            }
            // hover 检测（唯一的每帧调用点；纯 float 距离判断，不重排、不建对象）
            try
            {
                CombatBars.UpdateHover();
            }
            catch (Exception)
            {
                // expected: 悬停检测失败时本帧跳过，下一帧重试
            }

            if (!zonesApplied || !fitDone)
            {
                if (framesLeft > 0)
                {
                    framesLeft--;
                    if (!zonesApplied)
                    {
                        if (!ApplyZones(screen))
                        {
                            return;
                        }
                        zonesApplied = true;
                    }
                    if (!fitDone)
                    {
                        if (!TryFit(screen))
                        {
                            return;
                        }
                        fitDone = true;
                    }
                }
            }
            if (zonesApplied)
            {
                UpdateSubMapMask(screen);
                UpdateZoneTint(screen);
            }
            if (fitDone && !userZoomed)
            {
                ReassertZoom(screen);
            }
            if (zonesApplied && settleTicks > 0)
            {
                settleTicks--;
                if (settleTicks > 0)
                {
                    SpaceTestRuntime.EnsureTestContent();
                }
            }
        }

        internal static void OnZoomKey(ClusterMapScreen screen, KButtonEvent e)
        {
            if (e == null || screen == null || !fitDone)
            {
                return;
            }
            if (UiModalBlocker.Active)
            {
                return;   // 模态窗（交易/远程/历史）打开时不吃滚轮，其余情况保持原有缩放行为
            }
            bool zoomIn = e.IsAction(global::Action.ZoomIn);
            bool zoomOut = e.IsAction(global::Action.ZoomOut);
            if (!zoomIn && !zoomOut)
            {
                return;
            }
            userZoomed = true;
            float floor = Mathf.Min(50f, fitScale);
            zoomTarget = Mathf.Clamp(zoomTarget + (zoomIn ? 25f : -25f), floor, 150f);
            SetTargetZoom(screen, zoomTarget);
        }

        // 让出缩放控制权：停自适应回正，否则原版聚焦会被 ReassertZoom 拽回 fitScale
        internal static void YieldZoom()
        {
            userZoomed = true;
        }

        internal static bool IsCellRevealed(AxialI cell)
        {
            try
            {
                ClusterFogOfWarManager.Instance fow = GetFow();
                return fow != null && fow.GetCellRevealLevel(cell) == ClusterRevealLevel.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void RefreshNow()
        {
            SpaceReveal.RetryPending();
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen != null)
            {
                // 问题9：采集/落地等主动刷新不触发全图 Trigger（会整屏重绘→短黑屏）；
                suppressFullRefresh = true;
                try
                {
                    ApplyZones(screen);
                    UpdateSubMapMask(screen);
                }
                finally
                {
                    suppressFullRefresh = false;
                }
            }
        }

        // 怪物死亡/离场：立刻摘掉该实体的星图 visualizer（原版 UpdateVis 事件驱动，不每帧）
        internal static void DropEntityVisual(ClusterGridEntity entity)
        {
            if (entity == null) return;
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null) return;   // 星图没开：没有 visualizer；开图时按 IsVisible=false 也不会重建
            try
            {
                if (mRemoveDeletedEntities == null)
                {
                    mRemoveDeletedEntities = typeof(ClusterMapScreen).GetMethod("RemoveDeletedEntities",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (mRemoveDeletedEntities != null)
                {
                    mRemoveDeletedEntities.Invoke(screen, new object[] { entity.gameObject });
                    return;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 摘除怪物星图图标失败（降级为隐藏）");
            }
            // 降级：反射不到原版方法时，至少让图标当帧不可见
            try
            {
                ClusterMapVisualizer vis = screen.GetEntityVisAnim(entity);
                if (vis != null && vis.gameObject != null) vis.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 隐藏怪物星图图标失败");
            }
        }

        // 让原版重建一次实体 visualizer：残骸带等新实体即时出现
        internal static void RefreshEntityVisuals()
        {
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null) return;
            try
            {
                if (mUpdateVis == null)
                {
                    mUpdateVis = typeof(ClusterMapScreen).GetMethod("UpdateVis",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (mUpdateVis != null) mUpdateVis.Invoke(screen, new object[] { true });
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星图实体视觉刷新失败（不影响战斗结算）");
            }
        }

        // 有真实实体的格子不能隐藏视觉器：原版 FloatyAsteroidAnimation 取不到动画控制器会每帧 NRE
        private static bool CellHasEntity(AxialI cell)
        {
            try
            {
                ClusterGrid grid = ClusterGrid.Instance;
                List<ClusterGridEntity> list;
                if (grid != null && grid.cellContents.TryGetValue(cell, out list) && list != null && list.Count > 0)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // expected: 格子内容探测失败按无实体处理
            }
            return false;
        }

        private static bool ApplyZones(ClusterMapScreen screen)
        {
            Dictionary<AxialI, ClusterMapVisualizer> cells = GetCellVis(screen);
            if (cells == null || cells.Count == 0 || zones == null || !zones.valid)
            {
                return false;
            }

            int danger = 0;
            int sub = 0;
            int hidden = 0;
            int visibleCells = 0;
            int peekedCells = 0;
            int fogCells = 0;
            ClusterFogOfWarManager.Instance fow = GetFow();

            foreach (KeyValuePair<AxialI, ClusterMapVisualizer> kv in cells)
            {
                ClusterMapVisualizer vis = kv.Value;
                if (vis == null)
                {
                    continue;
                }
                ClusterMapHex hex = vis.GetComponent<ClusterMapHex>();
                if (hex == null)
                {
                    continue;
                }

                AxialI cell = kv.Key;
                if (zones.IsVoid(cell) && !SpaceTearCell.Hosts(cell) && !CellHasEntity(cell))
                {
                    vis.gameObject.SetActive(false);
                    hidden++;
                    continue;
                }
                if (!vis.gameObject.activeSelf)
                {
                    vis.gameObject.SetActive(true);
                }

                if (fow != null)
                {
                    // 开图一次性统计（仅本次遍历，不新增扫描）：确认揭示数据是 Visible / Peeked / Hidden 各多少格。
                    switch (fow.GetCellRevealLevel(cell))
                    {
                        case ClusterRevealLevel.Visible: visibleCells++; break;
                        case ClusterRevealLevel.Peeked: peekedCells++; break;
                        default: fogCells++; break;
                    }
                }

                if (zones.IsSub(cell))
                {
                    ClearOverlay(hex);
                    sub++;
                }
                else if (zones.IsDanger(cell))
                {
                    UpdateDangerTint(hex, cell, fow);
                    danger++;
                }
                else
                {
                    ClearOverlay(hex);
                }
            }

            MFLog.Info(LogCat.Space, "[Space] 染色 危险区=扇区" + zones.dangerSector + "(" + danger + "格)"
                + " 子图(" + sub + "格) 隐藏空白=" + hidden
                + " 揭示(可见=" + visibleCells + " 半=" + peekedCells + " 未=" + fogCells + ")");
            return true;
        }

        internal static void UpdateZoneTint(ClusterMapScreen screen)
        {
            SpaceZones z = zones;
            if (z == null || !z.valid || z.dangerCells.Count == 0)
            {
                return;
            }
            Dictionary<AxialI, ClusterMapVisualizer> cells = GetCellVis(screen);
            ClusterFogOfWarManager.Instance fow = GetFow();
            if (cells == null || fow == null)
            {
                return;
            }
            for (int i = 0; i < z.dangerCells.Count; i++)
            {
                AxialI cell = z.dangerCells[i];
                ClusterMapVisualizer vis;
                if (!cells.TryGetValue(cell, out vis) || vis == null)
                {
                    continue;
                }
                ClusterMapHex hex = GetHexCached(cell, vis);   // 缓存，消除每帧 GetComponent
                if (hex != null)
                {
                    UpdateDangerTint(hex, cell, fow);
                }
            }
        }

        // 原版迷雾贴图是半透明的：只要在下面垫任何颜色，未揭示格都会透出"影子"，
        private static void UpdateDangerTint(ClusterMapHex hex, AxialI cell, ClusterFogOfWarManager.Instance fow)
        {
            bool revealed = fow != null && fow.GetCellRevealLevel(cell) == ClusterRevealLevel.Visible;
            if (!revealed)
            {
                ClearOverlay(hex);
                return;
            }
            SetOverlay(hex, SpaceCfg.DangerColor);
        }

        // 原版「完全揭示」的点数门槛：GetRevealCompleteFraction = min(揭示点数 / 此值, 1)，
        //   即 Visible ⇔ 点数 >= 此值（TUNING.ROCKETRY.CLUSTER_FOW.POINTS_TO_REVEAL，当前 100f）。
        // 必须读常量，不能按「1f = 点亮」写点：1f 只有 0.01 揭示分数 ⇒ 判成 Peeked（黑幕 + 问号），
        //     而且挡光判定若也按 1f 算，格子会被当成「已点亮」不再修正，错误状态还会随档保存。
        private static float VisiblePoints
        {
            get { return TUNING.ROCKETRY.CLUSTER_FOW.POINTS_TO_REVEAL; }
        }

        internal static void UpdateSubMapMask(ClusterMapScreen screen)
        {
            SpaceZones z = zones;
            if (z == null || !z.valid || fRevealPoints == null || z.subCells.Count == 0)
            {
                return;
            }
            ClusterFogOfWarManager.Instance fow = GetFow();
            if (fow == null)
            {
                return;
            }
            Dictionary<AxialI, float> reveal = GetRevealPoints(fow);
            if (reveal == null)
            {
                return;
            }
            Dictionary<AxialI, ClusterMapVisualizer> cells = GetCellVis(screen);

            List<CraftVision> craftCells = GetCraftCellsInSub(z);
            bool changed = false;

            for (int i = 0; i < z.subCells.Count; i++)
            {
                AxialI cell = z.subCells[i];
                bool inVision = false;
                for (int c = 0; c < craftCells.Count; c++)
                {
                    if (AxialUtil.GetDistance(cell, craftCells[c].cell) <= craftCells[c].radius)
                    {
                        inVision = true;
                        break;
                    }
                }

                if (SpaceReveal.IsRevealed(cell))
                {
                    inVision = true;
                }
                // 点亮 = 写满原版门槛点数（100f），不是 1f：残留的 1 分格在这里会被判成未点亮，
                //   再按 inVision 重写成 100 / 0 ⇒ 旧档里被写成 1 分的格子可自愈。
                float threshold = VisiblePoints;
                float cur;
                reveal.TryGetValue(cell, out cur);
                bool curVisible = cur >= threshold;
                if (curVisible != inVision)
                {
                    reveal[cell] = inVision ? threshold : 0f;
                    changed = true;
                }

                if (cells == null)
                {
                    continue;
                }
                ClusterMapVisualizer vis;
                if (!cells.TryGetValue(cell, out vis) || vis == null)
                {
                    continue;
                }
                ClusterMapHex hex = GetHexCached(cell, vis);   // 缓存，消除每帧 GetComponent
                if (hex == null)
                {
                    continue;
                }
                hex.SetRevealed(inVision ? ClusterRevealLevel.Visible : ClusterRevealLevel.Hidden);
                if (!inVision && hex.fogOfWar != null)
                {
                    // 颜色为纯函数（SpaceCfg.MosaicColor），仅变化时写，避免每帧重新着色/重绘
                    Color want = SpaceCfg.MosaicColor(cell);
                    Color prev;
                    if (!lastMaskColor.TryGetValue(cell, out prev) || prev != want)
                    {
                        hex.fogOfWar.color = want;
                        lastMaskColor[cell] = want;
                    }
                }
            }

            // 问题9：整图刷新 Trigger 会整屏重绘 → 短黑屏。
            if (changed && !suppressFullRefresh && Game.Instance != null
                && Time.unscaledTime - lastFullRefreshAt >= 2f)
            {
                lastFullRefreshAt = Time.unscaledTime;
                Game.Instance.Trigger(1980521255, null);
            }
        }

        // 性能修复：改 struct + 复用缓冲
        private struct CraftVision
        {
            internal AxialI cell;
            internal int radius;
        }

        // 性能修复：填充并返回静态复用缓冲（调用点仅 UpdateSubMapMask，每帧一次）
        private static List<CraftVision> GetCraftCellsInSub(SpaceZones z)
        {
            craftVisionBuffer.Clear();
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return craftVisionBuffer;
            }
            for (int i = 0; i < z.subCells.Count; i++)
            {
                List<ClusterGridEntity> list;
                if (!grid.cellContents.TryGetValue(z.subCells[i], out list) || list == null)
                {
                    continue;
                }
                for (int j = 0; j < list.Count; j++)
                {
                    Clustercraft craft = list[j] as Clustercraft;
                    if (craft == null)
                    {
                        continue;
                    }
                    RocketCombatState st = craft.GetComponent<RocketCombatState>();
                    int bonus = st != null ? st.scoutingBonusCache : 0;
                    CraftVision v;
                    v.cell = z.subCells[i];
                    v.radius = SpaceCfg.VisionRadius + Mathf.Max(0, bonus);
                    craftVisionBuffer.Add(v);
                    break;
                }
            }
            return craftVisionBuffer;
        }

        private static ClusterFogOfWarManager.Instance GetFow()
        {
            if (SaveGame.Instance == null)
            {
                return null;
            }
            return SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
        }

        private static void SetOverlay(ClusterMapHex hex, Color color)
        {
            Image img = GetOverlay(hex);
            if (img != null)
            {
                if (!img.enabled) img.enabled = true;      // 仅在变化时设置
                if (img.color != color) img.color = color;
            }
        }

        private static void ClearOverlay(ClusterMapHex hex)
        {
            // 性能修复：走缓存。从未建过底色 ⇒ 无事可做（原实现是每帧 Find+enabled=false）
            Image img;
            if (hex != null && overlayCache.TryGetValue(hex, out img) && img != null)
            {
                if (img.enabled) img.enabled = false;
            }
        }

        private static Image GetOverlay(ClusterMapHex hex)
        {
            if (hex == null)
            {
                return null;
            }
            Image cached;
            if (overlayCache.TryGetValue(hex, out cached))
            {
                if (cached != null)
                {
                    return cached;
                }
                overlayCache.Remove(hex);   // 对象已被销毁（屏幕重建等）→ 重新解析
            }
            // 先看是否已存在（历史遗留 / 别处创建）
            Transform t = hex.transform.Find(OverlayName);
            Image img = t != null ? t.GetComponent<Image>() : null;
            if (img == null)
            {
                Sprite sprite = hex.toggle_image != null ? hex.toggle_image.sprite : null;
                if (sprite == null)
                {
                    return null;
                }
                // 底色要垫在"迷雾"下面：未揭示的格子应当整格是迷雾，不能透出区域色。
                Transform parent = hex.fogOfWar != null ? hex.fogOfWar.transform.parent : hex.transform;
                GameObject go = new GameObject(OverlayName, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                img = go.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.type = Image.Type.Simple;
                RectTransform rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                if (hex.fogOfWar != null)
                {
                    go.transform.SetSiblingIndex(hex.fogOfWar.transform.GetSiblingIndex());
                }
                else
                {
                    go.transform.SetAsLastSibling();
                }
            }
            overlayCache[hex] = img;
            return img;
        }

        private static bool TryFit(ClusterMapScreen screen)
        {
            ScrollRect scroll = GetScrollRect(screen);
            if (scroll == null || scroll.content == null)
            {
                return false;
            }
            RectTransform content = scroll.content;
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : content.parent as RectTransform;
            if (viewport == null)
            {
                return false;
            }
            Dictionary<AxialI, ClusterMapVisualizer> cells = GetCellVis(screen);
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            bool first = true;
            Bounds bounds = default(Bounds);
            foreach (KeyValuePair<AxialI, ClusterMapVisualizer> kv in cells)
            {
                ClusterMapVisualizer vis = kv.Value;
                if (vis == null || !vis.gameObject.activeSelf)
                {
                    continue;
                }
                RectTransform rt = vis.GetComponent<RectTransform>();
                if (rt == null)
                {
                    continue;
                }
                rt.GetWorldCorners(cornerBuffer);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = content.InverseTransformPoint(cornerBuffer[i]);
                    if (first)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }
            if (first)
            {
                return false;
            }

            float vw = viewport.rect.width;
            float vh = viewport.rect.height;
            if (vw <= 1f || vh <= 1f || bounds.size.x <= 0.01f || bounds.size.y <= 0.01f)
            {
                return false;
            }

            float scale = Mathf.Clamp(Mathf.Min(vw / bounds.size.x, vh / bounds.size.y) * 0.94f, 3f, 150f);

            fitScale = scale;
            zoomTarget = scale;
            if (fCurZoom != null)
            {
                fCurZoom.SetValue(screen, scale);
            }
            SetTargetZoom(screen, scale);
            content.localScale = new Vector3(scale, scale, 1f);
            content.localPosition = new Vector3(-bounds.center.x * scale, -bounds.center.y * scale, content.localPosition.z);

            MFLog.Verbose(LogCat.Space, "[Space] 自适应缩放 scale=" + scale.ToString("0.00")
                + " 图幅=" + bounds.size.x.ToString("0.0") + "x" + bounds.size.y.ToString("0.0")
                + " 视口=" + vw.ToString("0") + "x" + vh.ToString("0")
                + " 主图最大环=" + zones.mainMaxRing + " 子图中心=" + zones.subCenter);
            return true;
        }

        private static void ReassertZoom(ClusterMapScreen screen)
        {
            if (fCurZoom != null)
            {
                fCurZoom.SetValue(screen, fitScale);
            }
            SetTargetZoom(screen, fitScale);
            ScrollRect scroll = GetScrollRect(screen);
            if (scroll != null && scroll.content != null)
            {
                scroll.content.localScale = new Vector3(fitScale, fitScale, 1f);
            }
        }

        private static void SetTargetZoom(ClusterMapScreen screen, float value)
        {
            if (fTgtZoom != null)
            {
                fTgtZoom.SetValue(screen, value);
            }
        }

        // 性能修复：屏幕实例变化才刷新缓存（m_cellVisByLocation 仅 OnSpawn 建立，存活期稳定）
        private static void EnsureScreenCached(ClusterMapScreen screen)
        {
            if (screen == cachedScreen)
            {
                return;
            }
            cachedScreen = screen;
            cachedCells = null;
            cachedScroll = null;
            hexCache.Clear();
            hexCacheSrc = null;
            overlayCache.Clear();
            lastMaskColor.Clear();
        }

        private static ScrollRect GetScrollRect(ClusterMapScreen screen)
        {
            if (fScrollRect == null || screen == null)
            {
                return null;
            }
            EnsureScreenCached(screen);
            if (cachedScroll == null)
            {
                cachedScroll = fScrollRect.GetValue(screen) as ScrollRect;
            }
            return cachedScroll;
        }

        internal static bool TryCellWorld(ClusterMapScreen screen, AxialI cell, out Vector3 world, out Transform root)
        {
            world = Vector3.zero;
            root = null;
            if (screen == null)
            {
                return false;
            }
            Dictionary<AxialI, ClusterMapVisualizer> cells = GetCellVis(screen);
            if (cells == null)
            {
                return false;
            }
            ClusterMapVisualizer vis;
            if (!cells.TryGetValue(cell, out vis) || vis == null)
            {
                return false;
            }
            root = vis.transform.parent;
            if (root == null)
            {
                return false;
            }
            world = vis.transform.position;
            return true;
        }

        private static Dictionary<AxialI, ClusterMapVisualizer> GetCellVis(ClusterMapScreen screen)
        {
            if (fCellVis == null || screen == null)
            {
                return null;
            }
            EnsureScreenCached(screen);
            if (cachedCells == null)
            {
                cachedCells = fCellVis.GetValue(screen) as Dictionary<AxialI, ClusterMapVisualizer>;
            }
            return cachedCells;
        }

        // 性能修复：迷雾点字典缓存（fow 单例，引用变才反射）
        private static Dictionary<AxialI, float> GetRevealPoints(ClusterFogOfWarManager.Instance fow)
        {
            if (fRevealPoints == null || fow == null)
            {
                return null;
            }
            if (fow != cachedFow)
            {
                cachedFow = fow;
                cachedReveal = null;
            }
            if (cachedReveal == null)
            {
                cachedReveal = fRevealPoints.GetValue(fow) as Dictionary<AxialI, float>;
            }
            return cachedReveal;
        }

        // 性能修复：格子 → ClusterMapHex 缓存（消除每帧 GetComponent；cells 引用变即失效）
        private static ClusterMapHex GetHexCached(AxialI cell, ClusterMapVisualizer vis)
        {
            if (vis == null)
            {
                return null;
            }
            if (hexCacheSrc != cachedCells)
            {
                hexCache.Clear();
                hexCacheSrc = cachedCells;
            }
            ClusterMapHex hex;
            if (hexCache.TryGetValue(cell, out hex) && hex != null)
            {
                return hex;
            }
            hex = vis.GetComponent<ClusterMapHex>();
            hexCache[cell] = hex;
            return hex;
        }

        // 战斗特效尺寸工具（CombatFx 用；K6：不新增反射，复用本类现有缓存）

        // 星图内容层缩放（特效挂在缩放内容下，像素钳制要除以它）。
        internal static float GetMapScale(ClusterMapScreen screen)
        {
            try
            {
                ScrollRect scroll = GetScrollRect(screen);
                if (scroll != null && scroll.content != null)
                {
                    float s = scroll.content.localScale.x;
                    if (s > 0.0001f)
                    {
                        return s;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 内容层缩放读取失败按 1 倍处理
            }
            return 1f;
        }

        // 格子宽度（公开方法 GetClusterMapHexAtLocation，已实证）。
        internal static float GetHexWidth(ClusterMapScreen screen, AxialI cell)
        {
            try
            {
                if (screen == null)
                {
                    return 0f;
                }
                ClusterMapHex hex = screen.GetClusterMapHexAtLocation(cell);
                if (hex != null)
                {
                    RectTransform rt = hex.GetComponent<RectTransform>();   // rectTransform 字段是私有的，走组件取
                    if (rt != null)
                    {
                        return rt.rect.width;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 格子宽度读取失败返回 0，由调用方跳过该格
            }
            return 0f;
        }

        // 实体图标宽度（公开方法 GetEntityVisAnim，已实证）。
        internal static float GetEntityWidth(ClusterMapScreen screen, ClusterGridEntity entity)
        {
            try
            {
                if (screen == null || entity == null)
                {
                    return 0f;
                }
                ClusterMapVisualizer vis = screen.GetEntityVisAnim(entity);
                if (vis != null)
                {
                    RectTransform rt = vis.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        return rt.rect.width;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 实体宽度读取失败返回 0，由调用方跳过该实体
            }
            return 0f;
        }
    }
}
