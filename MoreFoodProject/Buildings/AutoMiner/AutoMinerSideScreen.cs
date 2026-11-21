// Buildings/AutoMiner/AutoMinerSideScreen.cs — 在自动采矿机 Config 侧屏注入模式切换 Toggle（克隆原生 TurboModeSideScreen 的 Contents）

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class AutoMinerSideScreen
    {
        private const string TAG = "[MoreFood/AutoMiner/UI]";
        private const string NodeName = "MoreFood_AutoMinerModeRow";
        private const string LabelName = "MoreFood_AutoMinerModeLabel";
        private const string ToggleName = "MoreFood_AutoMinerModeToggle";

        private static AutoMiner current;
        private static GameObject rowRoot;
        private static MultiToggle toggle;
        private static LocText modeLabel;
        private static ToolTip modeTooltip;

        [HarmonyPatch(typeof(DetailsScreen), "Refresh")]
        internal static class RefreshPatch
        {
            private static void Postfix(DetailsScreen __instance, GameObject go)
            {
                try
                {
                    if (go == null)
                    {
                        current = null;
                        SetRowVisible(false);
                        return;
                    }
                    AutoMiner miner = go.GetComponent<AutoMiner>();
                    if (miner == null)
                    {
                        current = null;
                        SetRowVisible(false);
                        return;
                    }
                    Transform host = FindDeep(__instance.transform, "Config Tab - body instance");
                    if (host == null) return;
                    EnsureBuilt(host, __instance);
                    current = miner;
                    if (toggle == null) return;
                    var mode = miner.GetComponent<AutoMinerHarvestMode>();
                    bool harvest = mode != null && mode.harvestMode;
                    toggle.ChangeState(harvest ? 1 : 0);
                    ApplyLabel(harvest);   // 文案随模式：挖矿→「挖矿模式」，采集→「采集模式」
                    SetRowVisible(true);
                }
                catch (Exception e) { MFLog.HotFail("patch:AutoMiner_SideScreen", e); }
            }
        }

        // Config body 是所有建筑共享的容器：切走时必须隐藏整行，否则 Label/BG 会残留在其他建筑上
        private static void SetRowVisible(bool visible)
        {
            try
            {
                if (rowRoot != null)
                {
                    if (rowRoot.activeSelf != visible) rowRoot.SetActive(visible);
                    return;
                }
                if (toggle != null && toggle.transform.parent != null)
                    toggle.transform.parent.gameObject.SetActive(visible);
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 切换整行可见性失败"); }
        }

        // 清空克隆体原生本地化键，避免覆盖 SetText；文案随模式切换（详见生态.md §6.2）
        private static void ApplyLabel(bool harvest)
        {
            if (modeLabel == null) return;
            try
            {
                modeLabel.key = "";
                modeLabel.SetText(harvest ? AutoMinerText.ModeHarvest : AutoMinerText.ModeMining);
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 设置标签文案失败"); }
        }

        // 替换克隆体原 SpaceHeater 的 ToolTip；setter 清空重建，直接赋值即整体替换
        private static void ApplyTooltip()
        {
            if (modeTooltip == null) return;
            try
            {
                modeTooltip.UseFixedStringKey = false;   // 防止 OnPrefabInit 用 FixedStringKey 重取原生文案
                modeTooltip.toolTip = AutoMinerText.ModeTooltip;
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 设置 ToolTip 失败"); }
        }

        private static void EnsureBuilt(Transform host, DetailsScreen ds)
        {
            if (toggle != null && toggle.gameObject != null) return;
            Transform exist = FindDeep(host, NodeName);
            if (exist != null)
            {
                rowRoot = exist.gameObject;
                toggle = exist.GetComponentInChildren<MultiToggle>(true);
                modeLabel = FindLocText(exist);
                modeTooltip = exist.GetComponentInChildren<ToolTip>(true);
                if (toggle != null)
                {
                    toggle.onClick = null;
                    toggle.onClick += OnToggle;
                    ApplyTooltip();
                    return;   // 文案随模式由 Refresh 随后的 ApplyLabel(harvest) 设置
                }
                rowRoot = null;
                UnityEngine.Object.Destroy(exist.gameObject);
            }

            // 主路径：克隆原生 TurboModeSideScreen/Contents —— states/toggle_image/颜色全继承
            Transform src = FindTurboContents(ds);
            if (src != null)
            {
                GameObject row = Util.KInstantiateUI(src.gameObject, host.gameObject, true);
                if (row != null)
                {
                    row.name = NodeName;
                    rowRoot = row;
                    row.SetActive(true);   // prefab 的 Contents 可能 inactive；克隆体会继承，必须显式激活整行

                    // 尺寸沿用原生 Contents：高 106 才装得下 Toggle，宽 280 避免居中元素右偏（详见生态.md §6.2）
                    LayoutElement le = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
                    le.minHeight = 106f;
                    le.preferredHeight = 106f;
                    le.flexibleHeight = 0f;
                    le.minWidth = 280f;
                    le.preferredWidth = 280f;

                    Transform labelT = row.transform.Find("Label");
                    if (labelT != null)
                    {
                        labelT.name = LabelName;
                        modeLabel = labelT.GetComponent<LocText>();
                    }

                    Transform toggleT = row.transform.Find("Toggle");
                    if (toggleT != null)
                    {
                        toggleT.name = ToggleName;
                        toggle = toggleT.GetComponent<MultiToggle>();
                        modeTooltip = toggleT.GetComponent<ToolTip>();
                        ApplyTooltip();
                        if (toggle != null)
                        {
                            // 清空运行时委托，防止误用 screenInstance 时残留原生 OnClick 导致 NRE
                            toggle.onClick = null;
                            toggle.onClick += OnToggle;
                            return;
                        }
                    }
                }
            }

            // 降级：原生 prefab 取不到时，用表格屏 Checkbox 模板兜底（至少能点，外观不同）
            GameObject fallback = MakeFallbackRow(host);
            if (fallback != null)
            {
                rowRoot = fallback;
                toggle = fallback.GetComponentInChildren<MultiToggle>(true);
                modeLabel = FindLocText(fallback.transform);
                modeTooltip = fallback.GetComponentInChildren<ToolTip>(true);
                ApplyTooltip();
            }
        }

        // 从 DetailsScreen.sideScreens 里找 TurboModeSideScreen 的 prefab，取其 Contents 子树
        private static Transform FindTurboContents(DetailsScreen ds)
        {
            try
            {
                if (ds == null) return null;
                FieldInfo field = typeof(DetailsScreen).GetField("sideScreens",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return null;
                var list = field.GetValue(ds) as List<DetailsScreen.SideScreenRef>;
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    var r = list[i];
                    if (r == null || r.screenPrefab == null) continue;
                    if (r.screenPrefab is TurboModeSideScreen)
                        return r.screenPrefab.transform.Find("Contents");
                }
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 定位 TurboModeSideScreen prefab 失败"); }
            return null;
        }

        private static LocText FindLocText(Transform root)
        {
            try
            {
                var all = root.GetComponentsInChildren<LocText>(true);
                return all != null && all.Length > 0 ? all[0] : null;
            }
            catch { return null; }
        }

        private static GameObject MakeFallbackRow(Transform host)
        {
            try
            {
                GameObject row = EventUiKit.MakeNode(NodeName, host);
                LayoutElement le = row.AddComponent<LayoutElement>();
                le.minHeight = 40f;
                le.preferredHeight = 40f;
                le.flexibleHeight = 0f;

                LocText label = EventUiKit.MakeLabel(LabelName, row.transform, AutoMinerText.ModeLabel);
                EventUiKit.Place(label.gameObject, 8f, 6f, 160f, 28f);

                GameObject prefab = EventUiKit.GetWidget("Checkbox");
                GameObject toggleGo = prefab != null
                    ? Util.KInstantiateUI(prefab, row.gameObject, true)
                    : null;
                if (toggleGo == null)
                {
                    toggleGo = EventUiKit.MakeNode(ToggleName, row.transform);
                    toggleGo.AddComponent<MultiToggle>();
                }
                toggleGo.name = ToggleName;
                EventUiKit.Place(toggleGo, 176f, 6f, 80f, 28f);
                return row;
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 降级行构建失败"); }
            return null;
        }

        private static void OnToggle()
        {
            try
            {
                if (current == null) return;
                var mode = current.GetComponent<AutoMinerHarvestMode>();
                // 以组件真实状态取反：降级 Checkbox 的 CurrentState 恒为 0，不可读（详见生态.md §6.2）
                bool next = mode == null || !mode.harvestMode;
                AutoMinerModeSwitch.Set(current, next);
                if (toggle != null) toggle.ChangeState(next ? 1 : 0);
                ApplyLabel(next);   // 点击后立即更新标签：挖矿↔采集
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 切换模式失败"); }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = FindDeep(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
