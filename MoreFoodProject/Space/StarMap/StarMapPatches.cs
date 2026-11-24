using System;
using EventFramework.Distribution.Contracts;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 1. 星图扩径：ClusterGrid(int numRings) 构造前缀，强制 config.star_map_rings（网格总环数，主图可见 0~14）
    [HarmonyPatch(typeof(ClusterGrid), MethodType.Constructor, new Type[] { typeof(int) })]
    internal static class ClusterGridCtorPatch
    {
        private static void Prefix(ref int numRings)
        {
            try
            {
                SpaceLoadTrace.Set("星图网格构建（原版 " + numRings + " 环）");
                int want = TradeDb.Config.star_map_rings;
                if (want <= 0)
                {
                    MFLog.Info(LogCat.Space,"[Space] 星图环数保持原版 " + numRings + "（config.star_map_rings=0）");
                    return;
                }
                if (numRings != want)
                {
                    // ★ 记下扩径前的原版环数：分区只认原版主图范围，
                    if (numRings > 0)
                    {
                        SpaceCfg.VanillaRings = numRings;
                    }
                    MFLog.Info(LogCat.Space,"[Space] 星图环数 " + numRings + " -> " + want
                        + "（原版主图 " + SpaceCfg.VanillaRings + " 环；改 config.star_map_rings 可为 0 以关闭）");
                    numRings = want;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 扩径失败");
            }
        }

        private static void Postfix(ClusterGrid __instance)
        {
            try
            {
                SpaceLoadTrace.Set("星图网格就绪");
                SpaceTimers.OnGridReadyAfterLoad();
                SpaceTestRuntime.LogGridStats(__instance);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星图网格就绪处理失败");
            }
        }
    }

    // 2. 世界就绪（新档/读档都走这里）
    [HarmonyPatch(typeof(ClusterManager), "OnWorldGenComplete")]
    internal static class ClusterManagerWorldGenPatch
    {
        private static void Prefix()
        {
            try
            {
                SpaceLoadTrace.Set("世界生成完成回调（OnWorldGenComplete）");
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:ClusterManagerWorldGenPatch", e);
            }
        }

        private static void Postfix()
        {
            try
            {
                SpaceTestRuntime.OnWorldReady();
                SpaceLoadTrace.Set("星球内容补齐完成");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星球内容补齐处理失败");
            }
        }
    }

    // 3. 星图打开：染色 + 申请自适应缩放
    [HarmonyPatch(typeof(ClusterMapScreen), "OnShow")]
    internal static class ClusterMapShowPatch
    {
        private static void Postfix(ClusterMapScreen __instance, bool show)
        {
            if (!show)
            {
                try
                {
                    TradeUi.CloseWindow();
                    TradeUi.CloseRemote();
                    TradeUi.CloseRefuel();
                    TradeUi.HideBar();
                    CombatUi.OnMapClosed();   // 战斗按钮排 + 攻击选目标模式收尾（C-19）
                    CombatBars.OnMapClosed(); // 迷你条全部销毁（打开时再建）
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Space, e, "[Space] 关闭星图时清理交易界面失败");
                }
                return;
            }
            try
            {
                CloseOurPanels();          // ★ 修复2：打开星图先清理我们自己的所有面板
                SpaceTestRuntime.EnsureTestContent();
                StarMapVisuals.OnScreenShown(__instance);
                CombatBars.OnMapShown();   // 迷你条：给现存怪物/交战火箭补建（之后全靠事件推送）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星图打开处理失败");
            }
        }

        /// <summary>
        private static void CloseOurPanels()
        {
            try { EventFrameworkApi.ClosePanels("打开星图"); } catch (Exception) { /* expected: 框架面板可能已关闭，失败不阻断后续清理 */ }
            try { RocketPanelScreen.ClosePanel(); } catch (Exception) { /* expected: 火箭面板可能已关闭，失败不阻断后续清理 */ }
            try { TradeUi.CloseWindow(); } catch (Exception) { /* expected: 交易窗可能已关闭，失败不阻断后续清理 */ }
            try { TradeUi.CloseRemote(); } catch (Exception) { /* expected: 远程交易可能已关闭，失败不阻断后续清理 */ }
            try { TradeUi.CloseRefuel(); } catch (Exception) { /* expected: 加油窗可能已关闭，失败不阻断后续清理 */ }
            try { TradeUi.HideBar(); } catch (Exception) { /* expected: 交易条可能已隐藏，失败不阻断后续清理 */ }
            try { UiModalBlocker.Close(); } catch (Exception) { /* expected: 遮罩可能已关闭，失败不阻断后续清理 */ }
        }
    }

    // 4. 每帧刷新时把"自适应缩放"落实（等 uGUI 布局稳定后再算）
    [HarmonyPatch(typeof(ClusterMapScreen), "ScreenUpdate")]
    internal static class ClusterMapScreenUpdatePatch
    {
        private static void Postfix(ClusterMapScreen __instance)
        {
            try
            {
                StarMapVisuals.OnScreenUpdate(__instance);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星图每帧刷新处理失败");
            }
        }
    }

    // 5. 滚轮缩放：原版硬钳 50~150(常量，改不了)，这里用自己的下限与步进
    [HarmonyPatch(typeof(ClusterMapScreen), "OnKeyDown")]
    internal static class ClusterMapKeyDownPatch
    {
        private static void Postfix(ClusterMapScreen __instance, KButtonEvent __0)
        {
            try
            {
                StarMapVisuals.OnZoomKey(__instance, __0);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 星图缩放按键处理失败");
            }
        }
    }

    // 6. 星外区闸门：任何"跨越子图边界"的寻路一律拒绝
    [HarmonyPatch]
    internal static class GetPathGatePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ClusterGrid), "GetPath", new Type[]
            {
                typeof(AxialI), typeof(AxialI), typeof(ClusterDestinationSelector),
                typeof(string).MakeByRefType(), typeof(bool)
            });
        }

        private static bool IsFlightSelector(object[] args)
        {
            if (args == null || args.Length < 3) return false;
            ClusterDestinationSelector sel = args[2] as ClusterDestinationSelector;
            return sel != null && sel.GetComponent<ClusterMapMeteorShowerVisualizer>() != null;
        }

        private static void Postfix(ref List<AxialI> __result, object[] __args)
        {
            try
            {
            if (__result == null || __result.Count == 0)
            {
                return;
            }
            SpaceZones zones = StarMapVisuals.Zones;
            if (zones == null || !zones.valid)
            {
                return;
            }
            AxialI from = __result[0];
            AxialI to = __result[__result.Count - 1];
            bool startIn = zones.IsSub(from);
            bool endIn = zones.IsSub(to);
            if (startIn == endIn)
            {
                return;
            }
            // 端点含裂口则放行（裂口是原版唯一入口）
            if (SpaceTearCell.Hosts(from) || SpaceTearCell.Hosts(to))
            {
                MFLog.Info(LogCat.Space,"[Space] 闸门放行：端点含时间裂口 " + SpaceCfg.Pos(to));
                return;
            }
            // 流星与灾星共用同一 visualizer，飞行路径不能置 null
            if (IsFlightSelector(__args))
            {
                MFLog.Info(LogCat.Space,"[Space] 闸门放行：流星/灾星飞行路径 " + SpaceCfg.Pos(from) + " -> " + SpaceCfg.Pos(to));
                return;
            }
            __result = null;
            MFLog.Info(LogCat.Space,"[Space] 闸门拦截：" + SpaceCfg.Pos(from) + " -> " + SpaceCfg.Pos(to)
                + "（星外区只能靠裂缝双向往返，禁止普通寻路跨越边界）");
            }
            catch (System.Exception e) { MFLog.HotFail("patch:GetPathGatePatch", e); }
        }
    }

    // 7. 星外区遮光：整片信号屏蔽区不接受原版任何"全额揭示"
    [HarmonyPatch]
    internal static class FowMaskPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ClusterFogOfWarManager.Instance), "RevealCellIfValid",
                new Type[] { typeof(AxialI), typeof(int) });
        }

        private static int fowFrame = -1;
        private static int fowCalls;
        private static bool fowWarned;

        private static bool Prefix(AxialI __0)
        {
            try
            {
            SpaceZones zones = StarMapVisuals.Zones;
            if (zones == null || !zones.valid || !zones.IsSub(__0))
            {
                return true;
            }
            if (Time.frameCount != fowFrame)
            {
                fowFrame = Time.frameCount;
                fowCalls = 0;
            }
            fowCalls++;
            if (fowCalls > 8192)
            {
                if (!fowWarned)
                {
                    fowWarned = true;
                    MFLog.Warn(LogCat.Space,"[Space] 同一帧内揭示请求超过 8192 次（疑似有 Mod 在逐格揭示全图），"
                        + "本帧不再拦截，避免把对方拖成死循环");
                }
                return true;
            }
            return false;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:FowMaskPatch", e); return true; }
        }
    }

    // 11. 全图揭示调试下原版会给整张星图补 0.01 窥视点（随档保存且无法自愈），此状态下跳过（见 星图.md P-34）
    [HarmonyPatch]
    internal static class FowPeekSprayPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ClusterFogOfWarManager.Instance), "EnsureRevealedTilesHavePeek", Type.EmptyTypes);
        }

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        private static bool Prefix()
        {
            try
            {
                return !DebugHandler.RevealFogOfWar;
            }
            catch (Exception e) { MFLog.HotFail("patch:FowPeekSpray", e); return true; }
        }
    }

    // 10. 尽早注册 prefab：EntityTemplates.CreateTemplates 跑完才能建实体，
    [HarmonyPatch(typeof(EntityTemplates), "CreateTemplates")]
    internal static class EntityTemplatesReadyPatch
    {
        private static void Postfix()
        {
            try
            {
                MFLog.Info(LogCat.Space,"[Space] CreateTemplates 完成（此刻不注册自己的 prefab："
                    + "会在原版注册建筑的同时改动 prefab 列表，导致后续建筑 NRE）");
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:EntityTemplatesReadyPatch", e);
            }
        }
    }

    // 9. 时间裂口上加"前往"面板：裂口本体也能点出进入选项（原版只有"选中火箭"那一条路）
    [HarmonyPatch(typeof(TemporalTear), "OnSpawn")]
    internal static class TemporalTearSpawnPatch
    {
        private static void Postfix(TemporalTear __instance)
        {
            try
            {
                __instance.gameObject.AddOrGet<TearEnterButton>();
                // 裂口落点/环/分区归属诊断
                try
                {
                    SpaceZones z = StarMapVisuals.Zones;
                    MFLog.Info(LogCat.Space,"[Space] 时间裂口 @" + SpaceCfg.Pos(__instance.Location)
                        + " 环=" + SpaceCfg.Ring(__instance.Location)
                        + (z != null && z.valid
                            ? " 子图=" + z.IsSub(__instance.Location) + " 空白带=" + z.IsVoid(__instance.Location)
                            : " （分区未就绪）"));
                }
                catch (Exception) { /* expected: 分区诊断打印失败不影响裂口面板挂载 */ }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 裂口面板挂载失败");
            }
        }
    }

    // 8. 接管原版时间裂口：不销毁火箭/舱内复制人，改为送到子图时空裂隙
    [HarmonyPatch(typeof(TemporalTear), "ConsumeCraft")]
    internal static class TemporalTearTakeoverPatch
    {
        private static bool Prefix(TemporalTear __instance, Clustercraft craft)
        {
            try
            {
                if (SpaceTestRuntime.TryTearTakeover(__instance, craft))
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 裂口接管失败，交回原版");
            }
            return true;
        }
    }

    // 9. 星图漂浮动画兜底：世界小行星视觉取不到动画控制器时，原版会每帧 NRE
    [HarmonyPatch(typeof(ClusterMapScreen), "FloatyAsteroidAnimation")]
    internal static class ClusterMapFloatyGuardPatch
    {
        private static FieldInfo fGridEntityVis;

        // 原版取 GetFirstAnimController 后写 Offset，视觉器缺失会每帧 NRE；有坏条目则跳过本帧漂浮动画
        private static bool Prefix(ClusterMapScreen __instance)
        {
            try
            {
                if (fGridEntityVis == null)
                {
                    fGridEntityVis = typeof(ClusterMapScreen).GetField("m_gridEntityVis",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (fGridEntityVis == null)
                {
                    return true;
                }
                System.Collections.IDictionary dict = fGridEntityVis.GetValue(__instance) as System.Collections.IDictionary;
                if (dict == null)
                {
                    return true;
                }
                List<WorldContainer> worlds = ClusterManager.Instance != null
                    ? ClusterManager.Instance.WorldContainers : null;
                if (worlds == null)
                {
                    return true;
                }
                for (int i = 0; i < worlds.Count; i++)
                {
                    WorldContainer wc = worlds[i];
                    if (wc == null) continue;
                    AsteroidGridEntity comp = wc.GetComponent<AsteroidGridEntity>();
                    if (comp == null || !dict.Contains(comp)) continue;

                    ClusterMapVisualizer vis = dict[comp] as ClusterMapVisualizer;
                    if (vis == null || vis.GetFirstAnimController() == null)
                    {
                        return false;   // 本帧跳过（否则原版 set_Offset(null) 每帧刷 NRE）
                    }
                }
            }
            catch (Exception)
            {
                // expected: 反射探测失败时放行原版视觉更新
            }
            return true;
        }
    }
}
