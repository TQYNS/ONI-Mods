using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class WarpRuntime
    {
        internal const string EngineModuleId = "module_speed_3";
        internal const float CooldownCycles = 30f;

        internal static bool HasEngine(Clustercraft craft)
        {
            if (craft == null)
            {
                return false;
            }
            RocketLoadout lo = RocketLoadout.Get(craft);
            return lo != null && lo.equippedModules != null && lo.equippedModules.Contains(EngineModuleId);
        }

        internal static bool SourceIsMainMap(Clustercraft craft)
        {
            if (craft == null)
            {
                return false;
            }
            SpaceZones z = StarMapVisuals.Zones;
            return z != null && z.valid && !z.IsSub(craft.Location);
        }

        internal static int PilotCount(Clustercraft craft)
        {
            try
            {
                WorldContainer w = RocketInventory.GetInterior(craft);
                if (w == null)
                {
                    return 0;
                }
                List<MinionIdentity> minions = Components.LiveMinionIdentities.GetWorldItems(w.id, false);
                if (minions == null)
                {
                    return 0;
                }
                int n = 0;
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i] != null)
                    {
                        n++;
                    }
                }
                return n;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        internal static float CooldownRemainingSeconds(Clustercraft craft)
        {
            RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
            if (st == null)
            {
                return 0f;
            }
            return Mathf.Max(0f, st.warpCooldownUntil - TradeRegistry.Now());
        }

        internal static bool CanUse(Clustercraft craft)
        {
            return craft != null
                && HasEngine(craft)
                && SourceIsMainMap(craft)
                && CooldownRemainingSeconds(craft) <= 0f
                && PilotCount(craft) > 0;
        }

        internal static string StatusText(Clustercraft craft)
        {
            if (craft == null)
            {
                return "跃迁：无火箭";
            }
            if (!SourceIsMainMap(craft))
            {
                return "跃迁：仅允许主星图内 → 主星图内";
            }
            if (!HasEngine(craft))
            {
                return "跃迁：未装备：曲率引擎模块";
            }
            float remain = CooldownRemainingSeconds(craft);
            if (remain > 0f)
            {
                return "跃迁：冷却中，剩余 " + Mathf.CeilToInt(remain / 600f) + " 周期";
            }
            if (PilotCount(craft) <= 0)
            {
                return "跃迁：缺少驾驶员";
            }
            return "跃迁：就绪";
        }

        internal static bool TryWarp(Clustercraft craft, AxialI cell)
        {
            if (craft == null || craft.gameObject == null)
            {
                TradeUi.Notify("跃迁地点无效", "火箭不存在");
                return false;
            }
            SpaceZones z = StarMapVisuals.Zones;
            if (z == null || !z.valid)
            {
                TradeUi.Notify("跃迁地点无效", "星图分区尚未就绪");
                return false;
            }
            if (z.IsSub(craft.Location) || z.IsSub(cell))
            {
                TradeUi.Notify("跃迁地点无效", "只允许主星图内 → 主星图内，不能跨子星图");
                return false;
            }
            if (!HasEngine(craft))
            {
                TradeUi.Notify("跃迁地点无效", "未装备：曲率引擎模块");
                return false;
            }
            if (CooldownRemainingSeconds(craft) > 0f)
            {
                TradeUi.Notify("跃迁地点无效", "跃迁冷却中，剩余 "
                    + Mathf.CeilToInt(CooldownRemainingSeconds(craft) / 600f) + " 周期");
                return false;
            }
            if (PilotCount(craft) <= 0)
            {
                TradeUi.Notify("跃迁地点无效", "必须存在驾驶员");
                return false;
            }
            if (cell == craft.Location)
            {
                TradeUi.Notify("跃迁地点无效", "目标就是当前所在格");
                return false;
            }
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || !grid.IsValidCell(cell) || z.IsVoid(cell))
            {
                TradeUi.Notify("跃迁地点无效", "目标格不在合法主星图范围内");
                return false;
            }
            List<ClusterGridEntity> contents;
            if (grid.cellContents.TryGetValue(cell, out contents) && contents != null && contents.Count > 0
                && !IsWarpFriendlyCell(contents))
            {
                TradeUi.Notify("跃迁地点无效", "目标格已被占用");
                return false;
            }
            try
            {
                AxialI from = craft.Location;
                RocketCombatState st = craft.GetComponent<RocketCombatState>();
                if (st == null)
                {
                    st = craft.gameObject.AddOrGet<RocketCombatState>();
                }
                st.warpCooldownUntil = TradeRegistry.Now() + CooldownCycles * 600f;
                st.orderedTargetUid = 0;
                FinishWarp(craft, from, cell);
                CombatUi.RefreshBar();
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 跃迁执行失败");
                TradeUi.Notify("跃迁地点无效", "跃迁执行异常，已取消");
                return false;
            }
        }

        // 原版就可达的关键格（裂口/我们的带）可作为跃迁落点。
        private static bool IsWarpFriendlyCell(List<ClusterGridEntity> contents)
        {
            bool friendly = false;
            for (int i = 0; i < contents.Count; i++)
            {
                ClusterGridEntity e = contents[i];
                if (e == null)
                {
                    continue;
                }
                if (e is TemporalTear || e is SpaceLootField)
                {
                    friendly = true;
                    continue;
                }
                return false;
            }
            return friendly;
        }

        private static void BeginWarpVisual(Clustercraft craft, AxialI from, AxialI to)
        {
            ClusterMapVisualizer vis = null;
            try
            {
                if (ClusterMapScreen.Instance != null)
                {
                    vis = ClusterMapScreen.Instance.GetEntityVisAnim(craft);
                }
            }
            catch (Exception)
            {
                // expected: 取不到火箭视觉体时直接 FinishWarp，跳过起飞动画立即结算落地
            }
            if (vis == null || GameScheduler.Instance == null)
            {
                FinishWarp(craft, from, to);
                return;
            }
            ClusterMapRocketAnimator.StatesInstance smi = null;
            try
            {
                smi = vis.GetSMI<ClusterMapRocketAnimator.StatesInstance>();
                if (smi != null)
                {
                    smi.StopSM("warp");
                }
                vis.PlayAnim("launching", KAnim.PlayMode.Loop);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 跃迁起飞动画失败（继续执行）");
            }
            ClusterMapRocketAnimator.StatesInstance stopped = smi;
            GameScheduler.Instance.Schedule("SpaceWarpArrive", 0.35f,
                delegate(object o)
                {
                    FinishWarp(craft, from, to);
                    try
                    {
                        if (vis != null)
                        {
                            vis.PlayAnim("idle_loop", KAnim.PlayMode.Loop);
                        }
                        if (stopped != null)
                        {
                            stopped.StartSM();
                        }
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Space, e, "[Space] 跃迁到达动画失败");
                    }
                }, null, null);
        }

        private static void FinishWarp(Clustercraft craft, AxialI from, AxialI to)
        {
            if (craft == null || craft.gameObject == null)
            {
                return;
            }
            ApplyInstantRelocate(craft, from, to);
            MFLog.Info(LogCat.Space, "[Space] ★ 火箭跃迁：" + craft.Name + " " + SpaceCfg.Pos(from)
                + " → " + SpaceCfg.Pos(to) + "，冷却 " + CooldownCycles + " 周期");
            TradeUi.Notify("跃迁", craft.Name + " 已跃迁至 " + SpaceCfg.Pos(to)
                + "，冷却 " + CooldownCycles + " 周期");
            CombatUi.RefreshBar();
        }

        // 裂口进出用：直接瞬移到配对格，不播飞行动画。
        internal static void InstantJump(Clustercraft craft, AxialI to)
        {
            if (craft == null || craft.gameObject == null)
            {
                return;
            }
            AxialI from = craft.Location;
            ApplyInstantRelocate(craft, from, to);
            if (CombatRuntime.RegionOf(from) != CombatRuntime.RegionOf(to))
            {
                CombatRuntime.OnCraftLeftRegion(craft);   // 跨星图：立刻通知怪物脱战（子↔主视作距离无穷）
            }
            MFLog.Info(LogCat.Space, "[Space] ★ 瞬移：" + craft.Name + " " + SpaceCfg.Pos(from)
                + " → " + SpaceCfg.Pos(to));
        }

        // 跃迁/瞬移共用：落点 + 停飞行 SM + 强制视觉定位 + 两端爆闪。
        private static void ApplyInstantRelocate(Clustercraft craft, AxialI from, AxialI to)
        {
            try
            {
                Teleport(craft, to);
                KMonoBehaviour.PlaySound(CombatDb.Shared.warpSound);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 瞬移落点执行失败");
            }
            try
            {
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                if (screen != null)
                {
                    ClusterMapVisualizer vis = screen.GetEntityVisAnim(craft);
                    if (vis != null)
                    {
                        ClusterMapTravelAnimator.StatesInstance travel =
                            vis.GetSMI<ClusterMapTravelAnimator.StatesInstance>();
                        if (travel != null)
                        {
                            travel.StopSM("warp");
                        }
                    }
                    MethodInfo update = typeof(ClusterMapScreen).GetMethod("UpdateVis",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (update != null)
                    {
                        update.Invoke(screen, new object[] { true });
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 瞬移视觉强制定位失败");
            }
            try
            {
                CombatFx.PlayBurst(from, new Color(0.55f, 0.75f, 1f, 1f), 2f);
                CombatFx.PlayBurst(to, new Color(0.55f, 0.75f, 1f, 1f), 2f);
            }
            catch (Exception)
            {
                // expected: 光团特效播不出时只少个视觉提示，传送本身已完成
            }
        }

        internal static void Teleport(Clustercraft craft, AxialI target)
        {
            ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
            if (selector != null)
            {
                selector.SetDestination(target);
            }
            craft.Location = target;
            if (selector != null)
            {
                selector.SetDestination(target);
            }
            ClusterTraveler traveler = craft.GetComponent<ClusterTraveler>();
            if (traveler != null)
            {
                MethodInfo m = typeof(ClusterTraveler).GetMethod("MarkPathDirty",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null)
                {
                    m.Invoke(traveler, null);
                }
                SetField(traveler, "m_cachedPath", null);
                SetField(traveler, "m_cachedPathDestination", target);
                SetField(traveler, "m_movePotential", 0f);
            }
        }

        private static void SetField(object target, string field, object value)
        {
            try
            {
                FieldInfo f = target.GetType().GetField(field,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    f.SetValue(target, value);
                }
            }
            catch (Exception)
            {
                // expected: 字段改名/缺失时跳过该项缓存重置，路径已由 MarkPathDirty 作废
            }
        }
    }
}
