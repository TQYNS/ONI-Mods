using System;
using System.Reflection;
using HarmonyLib;
using STRINGS;
using UnityEngine;

namespace MoreFood_KFC
{
    // 战斗 Harmony 补丁（全部收在本文件；K1 修复后每个补丁只装一次）

    // 存档挂载：SaveGame.OnPrefabInit（反序列化前就位，与 TradeSaveData 同款模式）
    [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
    internal static class CombatSaveMountPatch
    {
        private static void Postfix(SaveGame __instance)
        {
            try
            {
                // ★【探针定案】：不再挂 SaveGame —— SPACEDBG 探针实证 SaveGame
                SpaceSaveHost.RegisterPrefab();
                MFLog.Info(LogCat.Combat,"[Space] 存档宿主 prefab 已在 SaveGame.OnPrefabInit 注册（实体承载，随档保存）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 战斗存档载体挂载失败");
            }
        }
    }

    // 存档前捕获：SaveLoader.Save(string,bool,bool) 前缀（签名已实证）
    [HarmonyPatch(typeof(SaveLoader), "Save", new Type[] { typeof(string), typeof(bool), typeof(bool) })]
    internal static class CombatSaveCapturePatch
    {
        private static void Prefix()
        {
            try
            {
                MFLog.Info(LogCat.Combat,"[SAVE-TRACE] S1 存档前缀(外层) 触发");
                CombatSaveData save = CombatSaveData.Attach();
                if (save == null)
                {
                    MFLog.Info(LogCat.Combat,"[SAVE-TRACE] S1 外层：Attach()=null ⇒ 本次没写战斗数据");
                }
                else
                {
                    save.Capture();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 战斗存档捕获失败（照常保存）");
            }
        }
    }

    // 存档前捕获（二道保险）：内层 Save(BinaryWriter) 也捕一次（幂等，先清后写）
    [HarmonyPatch(typeof(SaveLoader), "Save", new Type[] { typeof(System.IO.BinaryWriter) })]
    internal static class CombatSaveCaptureInnerPatch
    {
        private static void Prefix()
        {
            try
            {
                MFLog.Info(LogCat.Combat,"[SAVE-TRACE] S1 存档前缀(内层) 触发");
                CombatSaveData save = CombatSaveData.Attach();
                if (save != null)
                {
                    save.Capture();
                }
                TradeSaveData tsd = TradeSaveData.Attach();
                if (tsd != null)
                {
                    tsd.Capture();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 内层存档捕获失败（照常保存）");
            }
        }
    }

    // Clustercraft prefab 模板注入（D1：组件必须在模板上，读档反序列化才找得到）
    [HarmonyPatch(typeof(ClustercraftConfig), "CreatePrefab")]
    internal static class CombatPrefabInjectPatch
    {
        private static void Postfix(ref GameObject __result)
        {
            try
            {
                if (__result == null)
                {
                    return;
                }
                __result.AddOrGet<RocketCombatState>();
                __result.AddOrGet<RocketLoadout>();
                MFLog.Info(LogCat.Combat,"[Space] 火箭战斗组件已注入 Clustercraft prefab 模板（RocketCombatState + RocketLoadout）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] Clustercraft prefab 注入失败");
            }
        }
    }

    // 兜底：万一 CreatePrefab 注入点没打上（版本差异），Db 完成后补一次（AddOrGet 幂等）
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class CombatPrefabInjectFallbackPatch
    {
        private static void Postfix()
        {
            try
            {
                GameObject prefab = Assets.TryGetPrefab(new Tag("Clustercraft"));
                if (prefab != null && prefab.GetComponent<RocketCombatState>() == null)
                {
                    prefab.AddOrGet<RocketCombatState>();
                    prefab.AddOrGet<RocketLoadout>();
                    MFLog.Info(LogCat.Combat,"[Space] 火箭战斗组件经兜底路径注入 Clustercraft prefab 模板");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] Clustercraft prefab 兜底注入失败");
            }
        }
    }

    // 控制台模块接收器（v1.7 模块物流）
    [HarmonyPatch(typeof(RocketControlStationConfig), "DoPostConfigureComplete")]
    internal static class ControlStationReceiverPatch
    {
        private static void Postfix(GameObject go)
        {
            try
            {
                if (go != null)
                {
                    go.AddOrGet<ControlStationModuleReceiver>();
                    go.AddOrGet<RocketAssemblyButton>();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 控制台接收器注入失败");
            }
        }
    }

    // 火箭舱块：槽位说明 + 拆除保护（v1.7）；U60 起描述符改为 CollectDescriptors 收集式
    [HarmonyPatch(typeof(Building), "CollectDescriptors")]
    internal static class RocketComponentDescriptorPatch
    {
        private static void Postfix(Building __instance, System.Collections.Generic.List<Descriptor> __1)
        {
            try
            {
                if (__instance == null || __1 == null
                    || __instance.GetComponent<RocketModuleCluster>() == null)
                {
                    return;
                }
                KPrefabID pid = __instance.GetComponent<KPrefabID>();
                RocketComponentSlots comp = pid != null
                    ? CombatDb.FindComponent(pid.PrefabTag.Name) : null;
                if (comp == null || comp.slots.Count == 0)
                {
                    return;
                }
                string text = RocketSlotText.Build(pid.PrefabTag.Name);
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                __1.Add(new Descriptor(text, text, Descriptor.DescriptorType.Effect, false));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 组件槽位说明失败");
            }
        }
    }

    [HarmonyPatch(typeof(SelectModuleSideScreen), "SetupBuildingTooltip")]
    internal static class RocketComponentTooltipPatch
    {
        private static void Postfix(ToolTip tooltip, BuildingDef def)
        {
            try
            {
                if (tooltip == null || def == null)
                {
                    return;
                }
                string text = RocketSlotText.Build(def.PrefabID);
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                PlanScreen plan = PlanScreen.Instance;
                if (plan == null)
                {
                    return;
                }
                tooltip.AddMultiStringTooltip(text, plan.buildingToolTipSettings.BuildButtonDescription);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 组件槽位 Tooltip 注入失败");
            }
        }
    }

    internal static class RocketSlotText
    {
        private static readonly string[] Order = { "weapon", "defense", "utility", "sensor" };

        internal static string Build(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return null;
            }
            RocketComponentSlots comp = CombatDb.FindComponent(prefabId);
            if (comp == null || comp.slots.Count == 0)
            {
                return null;
            }
            string text = "提供槽位";
            int lines = 0;
            for (int i = 0; i < Order.Length; i++)
            {
                SlotEntry e;
                if (!comp.slots.TryGetValue(Order[i], out e) || e == null)
                {
                    continue;
                }
                if (e.count <= 0 && e.capacity <= 0)
                {
                    continue;
                }
                string slotName = RocketLoadout.SlotName(Order[i]);
                text += "\n  " + slotName + "槽  ×" + e.count + "   容量 " + e.capacity;
                if (e.count <= 0)
                {
                    text += "   （不提供槽位，仅提供" + slotName + "容量）";
                }
                lines++;
            }
            if (lines == 0)
            {
                return null;
            }
            return text;
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "QueueDeconstruction", new Type[] { typeof(bool) })]
    internal static class RocketComponentDeconstructGuardPatch
    {
        private static bool Prefix(Deconstructable __instance)
        {
            try
            {
                if (__instance == null || DebugHandler.InstantBuildMode)
                {
                    return true;
                }
                RocketModuleCluster module = __instance.GetComponent<RocketModuleCluster>();
                if (module == null || module.CraftInterface == null)
                {
                    return true;
                }
                Clustercraft craft = module.CraftInterface.GetComponent<Clustercraft>();
                KPrefabID pid = __instance.GetComponent<KPrefabID>();
                if (craft == null || pid == null)
                {
                    return true;
                }
                string reason;
                if (!CombatAssembly.CanRemoveComponent(craft, pid.PrefabTag.Name, out reason))
                {
                    TradeUi.Notify("无法拆除组件", reason == null ? "槽位不足" : reason);
                    return false;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 组件拆除保护异常（放行）");
            }
            return true;
        }
    }

    // 攻击指针：选目标模式下点击格子的拦截（K10/K12）
    [HarmonyPatch(typeof(ClusterMapScreen), "SelectHex")]
    internal static class CombatAttackSelectHexPatch
    {
        private static bool Prefix(ClusterMapScreen __instance, ClusterMapHex newSelectionHex)
        {
            try
            {
                return CombatTargeting.HandleSelectHex(__instance, newSelectionHex);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 攻击选目标处理失败（交回原版）");
                return true;
            }
        }
    }

    // 攻击模式收尾：离开 SelectDestination 模式即清状态（Esc/关图/火箭没了都走这里）
    [HarmonyPatch(typeof(ClusterMapScreen), "SetMode")]
    internal static class CombatModeExitPatch
    {
        private static void Postfix(ClusterMapScreen __instance)
        {
            try
            {
                if (!CombatTargeting.Targeting)
                {
                    return;
                }
                object mode = __instance.GetMode();
                if (mode == null || mode.ToString() != "SelectDestination")
                {
                    CombatTargeting.ExitTargeting("模式退出");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 攻击模式收尾失败");
            }
        }
    }

    // Bug2：星图格 Tooltip —— 把原版"行程：X/Y星图格"改为"射程：X/Y星图格"，超出射程红字
    [HarmonyPatch(typeof(ClusterMapHex), "SetDestinationStatus",
        new Type[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
    internal static class CombatHexRangeTooltipPatch
    {
        private static FieldInfo tooltipField;
        private static MethodInfo hoverColorsMethod;
        private static string lastReason;

        private static void Prepare()
        {
            try
            {
            if (tooltipField == null)
            {
                tooltipField = AccessTools.Field(typeof(ClusterMapHex), "m_tooltip");
            }
            if (hoverColorsMethod == null)
            {
                hoverColorsMethod = AccessTools.Method(typeof(ClusterMapHex), "UpdateHoverColors",
                    new Type[] { typeof(bool) });
            }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:CombatHexRangeTooltipPatch", e); }
        }

        private static bool Prefix(ClusterMapHex __instance, string fail_reason, int pathLength,
            int rocketRange, bool repeat)
        {
            try
            {
                if (__instance == null)
                {
                    return true;
                }
                if (pathLength <= 0)
                {
                    // 目的地不可达时打出原版失败原因（去重）
                    if (!string.IsNullOrEmpty(fail_reason) && fail_reason != lastReason)
                    {
                        lastReason = fail_reason;
                        MFLog.Info(LogCat.Combat,"[Space] 目标格 " + SpaceCfg.Pos(__instance.location)
                            + " 不可达：" + fail_reason + "（行程=0 火箭航程=" + rocketRange + "）");
                    }
                    return true;   // 无路径信息 → 交回原版（仍会处理 fail_reason 与高亮色）
                }
                Prepare();
                ToolTip tip = tooltipField != null ? tooltipField.GetValue(__instance) as ToolTip : null;
                if (tip == null)
                {
                    return true;
                }
                // 分母取"火箭的攻击距离"（原版 rocketRange 是航程/燃料可达格数，不是射程）。
                int rangeForTip = rocketRange;
                Clustercraft targetingCraft = CombatTargeting.TargetingCraft;
                if (targetingCraft != null)
                {
                    rangeForTip = Mathf.Max(1, Mathf.RoundToInt(CombatRuntime.AttackRangeOf(targetingCraft)));
                }
                string tmpl = repeat
                    ? STRINGS.UI.CLUSTERMAP.TOOLTIP_PATH_LENGTH_RETURN
                    : STRINGS.UI.CLUSTERMAP.TOOLTIP_PATH_LENGTH;
                int shown = repeat ? pathLength * 2 : pathLength;
                string text = string.Format(tmpl, shown, GameUtil.GetFormattedRocketRange(rangeForTip, true));
                if (!string.IsNullOrEmpty(text))
                {
                    text = text.Replace("行程", "射程");   // ★ 文案：行程 → 射程（数值 = 到该格距离 / 攻击距离）
                }
                bool overRange = pathLength > rangeForTip;   // ★ 超出射程 → 红字
                tip.ClearMultiStringTooltip();
                tip.AddMultiStringTooltip(text,
                    overRange ? __instance.invalidDestinationTooltipStyle : __instance.informationTooltipStyle);
                if (!string.IsNullOrEmpty(fail_reason))
                {
                    tip.AddMultiStringTooltip(fail_reason, __instance.invalidDestinationTooltipStyle);
                }
                if (hoverColorsMethod != null)
                {
                    hoverColorsMethod.Invoke(__instance, new object[] { string.IsNullOrEmpty(fail_reason) });
                }
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 星图格射程 Tooltip 处理失败（交回原版）");
                return true;
            }
        }
    }
}
