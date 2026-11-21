// 用途：宿主补丁：地表/旅行/光照/太阳能/种植/存档等

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
    [HarmonyPatch(typeof(LargeImpactorEvent), "GetImpactTime")]
    internal static class SpaceRealEventsImpactTimePatch
    {
        private static bool Prefix(ref float __result)
        {
            try
            {
                if (!SpaceRealEvents.HasPendingImpactor)
                {
                    return true;
                }
                __result = SpaceRealEvents.PendingImpactorDelay;
                return false;
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsImpactTimePatch", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ClusterManager), "GetRandomSurfaceCell")]
    internal static class SpaceRealEventsSurfaceWorldGuardPatch
    {
        private static void Prefix(ref int worldID)
        {
            try
            {
                ClusterManager cm = ClusterManager.Instance;
                if (cm == null)
                {
                    return;
                }
                WorldContainer w = cm.GetWorld(worldID);
                if (w == null || w.WorldSize.x <= 0 || w.WorldSize.y <= 0)
                {
                    WorldContainer start = cm.GetStartWorld();
                    if (start != null)
                    {
                        worldID = start.id;
                    }
                    return;
                }
                int cell = Grid.XYToCell(w.WorldOffset.x, w.WorldOffset.y);
                if (!Grid.IsValidCell(cell))
                {
                    WorldContainer start = cm.GetStartWorld();
                    if (start != null)
                    {
                        worldID = start.id;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 随机地表格世界校验失败");
            }
        }
    }

    [HarmonyPatch(typeof(ClusterManager), "ValidSurfaceCell")]
    internal static class SpaceRealEventsSurfaceCellGuardPatch
    {
        private static bool Prefix(int x, int y, ref bool __result)
        {
            try
            {
                int cell = Grid.XYToCell(x, y - 1);
                if (!Grid.IsValidCell(cell))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsSurfaceCellGuardPatch", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ClusterTraveler), "Sim200ms")]
    internal static class SpaceRealEventsStationTravelerGuardPatch
    {
        private static bool Prefix(ClusterTraveler __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return false;
                }
                SpaceStation station = __instance.GetComponent<SpaceStation>();
                if (station == null)
                {
                    return true;
                }
                if (!station.isShip)
                {
                    return false;
                }
                return __instance.GetComponent<ClusterDestinationSelector>() != null;
            }
            catch (Exception e)
            {
                // 守卫判定失败时保持阻断（与原正常分支语义一致），但必须留痕
                MFLog.Exc("guard:ClusterTraveler.Sim200ms", LogCat.Space, e,
                    "[Space] ClusterTraveler.Sim200ms 守卫异常，本帧已阻断");
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(ClusterUtil), "GetAmountFromRelatedWorlds")]
    internal static class SpaceRealEventsStaleWorldGuardPatch
    {
        private static bool Prefix(WorldInventory worldInventory, Tag element, ref float __result)
        {
            try
            {
                WorldContainer own = worldInventory != null ? worldInventory.WorldContainer : null;
                __result = 0f;
                if (own == null)
                {
                    return false;
                }
                ClusterManager cm = ClusterManager.Instance;
                if (cm == null || cm.WorldContainers == null)
                {
                    return false;
                }
                int parentWorldId = own.ParentWorldId;
                for (int i = 0; i < cm.WorldContainers.Count; i++)
                {
                    WorldContainer w = cm.WorldContainers[i];
                    if (w == null)
                    {
                        continue;
                    }
                    if (w.GetComponent<WorldContainer>() == null)
                    {
                        continue;
                    }
                    WorldInventory inv = w.worldInventory;
                    if (inv == null)
                    {
                        continue;
                    }
                    if (w.ParentWorldId != parentWorldId)
                    {
                        continue;
                    }
                    __result += inv.GetAmount(element, false);
                }
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 世界库存汇总已跳过异常条目");
                __result = 0f;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(JoyBehaviourMonitor.Instance), "GoToOverjoyed")]
    internal static class SpaceRealEventsJoyGuardPatch
    {
        private static bool Prefix(JoyBehaviourMonitor.Instance __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return true;
                }
                if (__instance.IsInsideState(__instance.sm.overjoyed))
                {
                    return false;
                }
                KPrefabID kpid = __instance.GetComponent<KPrefabID>();
                if (kpid != null)
                {
                    kpid.RemoveTag(GameTags.Overjoyed);
                    kpid.RemoveTag(GameTags.PleasantConversation);
                }
                return true;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ClusterMapMeteorShowerVisualizer), "RefreshVisuals")]
    internal static class SpaceRealEventsMeteorVisualGuardPatch
    {
        private static bool Prefix()
        {
            try
            {
                return ClusterMapScreen.Instance != null;
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsMeteorVisualGuardPatch", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(TimeOfDay), "UpdateSunlightIntensity")]
    internal static class SpaceRealEventsLightPatch
    {
        private static void Postfix()
        {
            try
            {
                SpaceRealEvents.ScaleWorldLight();
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsLightPatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(SolarPanel), "EnergySim200ms")]
    internal static class SpaceRealEventsSolarPanelStormPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ci in instructions)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo mi
                    && mi.DeclaringType == typeof(Mathf) && mi.Name == "Clamp")
                {
                    ci.operand = AccessTools.Method(typeof(SpaceRealEvents), "ClampSolarOutput");
                }
                yield return ci;
            }
        }
    }

    [HarmonyPatch(typeof(ModuleSolarPanel), "EnergySim200ms")]
    internal static class SpaceRealEventsModuleSolarPanelStormPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ci in instructions)
            {
                if (ci.opcode == OpCodes.Call && ci.operand is MethodInfo mi
                    && mi.DeclaringType == typeof(Mathf) && mi.Name == "Clamp")
                {
                    ci.operand = AccessTools.Method(typeof(SpaceRealEvents), "ClampSolarOutput");
                }
                yield return ci;
            }
        }
    }

    [HarmonyPatch(typeof(PlantablePlot), "ConfigureOccupyingObject", new Type[] { typeof(GameObject) })]
    internal static class SpaceRealEventsPlantPlantedPatch
    {
        private static void Postfix(GameObject newPlant)
        {
            try
            {
                SpaceRealEvents.OnPlantPlanted(newPlant);
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsPlantPlantedPatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(SeedProducer), "RollForMutation")]
    internal static class SpaceRealEventsMutationPatch
    {
        private static bool inRoll;

        private static bool Prefix(SeedProducer __instance, ref bool __result)
        {
            if (inRoll || !SpaceRealEvents.MutationBoostActive())
            {
                return true;
            }
            try
            {
                inRoll = true;
                int n = SpaceRealEvents.MutationRolls();
                __result = false;
                for (int i = 0; i < n; i++)
                {
                    if (__instance.RollForMutation())
                    {
                        __result = true;
                        break;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 植物变异三连掷失败");
                __result = false;
                return false;
            }
            finally
            {
                inRoll = false;
            }
        }
    }

    [HarmonyPatch(typeof(TradeUtil), "FindCraftAt")]
    internal static class SpaceRealEventsFreighterTradePatch
    {
        private static void Postfix(AxialI cell, ref Clustercraft __result)
        {
            if (__result != null)
            {
                return;
            }
            try
            {
                if (!SpaceRealEventRuntime.IsFreighterCell(cell))
                {
                    return;
                }
                __result = TradeUtil.NearestCraft(cell, 1);
                if (__result != null)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 太空货船相邻交易：借用 " + __result.Name + " 与货船交易");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 太空货船相邻交易判定失败");
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoader), "Load", new Type[] { typeof(IReader) })]
    internal static class SpaceRealEventsLoadSweepPatch
    {
        private static void Postfix()
        {
            try
            {
                SpaceRealEventRuntime.SweepTempStationsNow();
                // 持续 buff：按存档快照重建（与 EventSystem.Init 钩子互为兜底，幂等）
                SpaceRealEvents.RestoreBuffsAfterLoad();
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsLoadSweepPatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoader), "Load", new Type[] { typeof(IReader) })]
    internal static class SpaceRealEventsLoadResetPatch
    {
        private static void Prefix()
        {
            try
            {
                SpaceRealEventRuntime.ResetAll("读档");
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsLoadResetPatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoader), "LoadFromWorldGen")]
    internal static class SpaceRealEventsNewGameResetPatch
    {
        private static void Prefix()
        {
            try
            {
                SpaceRealEventRuntime.ResetAll("新档");
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceRealEventsNewGameResetPatch", e);
            }
        }
    }
}
