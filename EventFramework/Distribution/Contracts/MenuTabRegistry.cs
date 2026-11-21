// MenuTabRegistry.cs — 共享契约层 · 【往右侧管理面板 ManagementMenu 加 tab】的反射工具
//
// 为什么在 Contracts：这是纯游戏 UI 反射、不含任何框架状态；内容 Mod 要自挂自己的 tab
// （MoreFood 的「饮品」「火箭」）就必须能用它，否则只能复制一份 380 行反射代码，迟早漂移。

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace EventFramework.Distribution.Contracts
{
    public static class MenuTabRegistry
    {
        public class TabSpec
        {
            // tab 显示文字（会变成 GameObject 名 "Toggle:<text>"）
            public string text;
            // 图标 sprite 名。必须用【原版已有的】名字，否则 fgImage.sprite = null
            public string icon;
            // 悬浮提示文案
            public string tooltip;
            // 点击后显示的屏幕（KScreen）
            public KScreen screen;
        }

        private const BindingFlags BF = BindingFlags.Instance
                                      | BindingFlags.Public
                                      | BindingFlags.NonPublic;

        public static int AddedCount { get; private set; }

        // 把一批 tab 挂到管理菜单上（幂等：同名 tab 已存在则跳过）。
        public static int AddTabs(ManagementMenu menu, TabSpec[] specs, string afterText = null)
        {
            if (menu == null || specs == null || specs.Length == 0) return 0;

            try
            {
                var menuType = typeof(ManagementMenu);

                var infoType = menuType.GetNestedType("ManagementMenuToggleInfo",
                                                      BindingFlags.NonPublic | BindingFlags.Public);
                var dataType = menuType.GetNestedType("ScreenData",
                                                      BindingFlags.NonPublic | BindingFlags.Public);
                if (infoType == null || dataType == null)
                {
                    Debug.LogWarning("[EventFramework][Tab] 找不到嵌套类型 "
                                     + "ManagementMenuToggleInfo / ScreenData，跳过加 tab");
                    return 0;
                }

                var simField = menuType.GetField("ScreenInfoMatch", BF);
                var tiField  = menuType.GetField("toggleInfo", BF);
                if (simField == null || tiField == null)
                {
                    Debug.LogWarning("[EventFramework][Tab] 找不到 ScreenInfoMatch / toggleInfo 字段，跳过");
                    return 0;
                }

                var sim = simField.GetValue(menu) as IDictionary;
                var ti  = tiField.GetValue(menu) as IList;
                if (sim == null || ti == null)
                {
                    Debug.LogWarning("[EventFramework][Tab] ScreenInfoMatch/toggleInfo 值异常"
                                     + "（sim=" + (sim != null) + " ti=" + (ti != null) + "），跳过");
                    return 0;
                }

                var ctor = infoType.GetConstructors(BF);
                if (ctor == null || ctor.Length == 0)
                {
                    Debug.LogWarning("[EventFramework][Tab] ManagementMenuToggleInfo 没有可用的构造函数");
                    return 0;
                }
                var infoCtor = ctor[0];

                var screenField  = dataType.GetField("screen", BF);
                var tabIdxField  = dataType.GetField("tabIdx", BF);
                var toggleField  = dataType.GetField("toggleInfo", BF);
                var cancelField  = dataType.GetField("cancelHandler", BF);
                var hotKeyField  = FindField(infoType, "hotKey");

                // ★★ 关键：hotKey 必须是一个【在 GameInputBindings 里有绑定的】枚举值
                var ps = infoCtor.GetParameters();
                Type actionType = (ps.Length > 3 && ps[3].ParameterType.IsEnum)
                                  ? ps[3].ParameterType : null;

                object borrowedHotKey = BorrowHotKeyFromVanilla(menu, menuType, actionType);

                // ★ 插入位置：原版「饮食」(consumablesInfo) 的【下一个】
                int insertAt = ti.Count;   // 兜底：末尾
                try
                {
                    var cf = menuType.GetField("consumablesInfo", BF);
                    if (cf != null)
                    {
                        var consumables = cf.GetValue(menu);
                        if (consumables != null)
                        {
                            for (int k = 0; k < ti.Count; k++)
                            {
                                if (ReferenceEquals(ti[k], consumables)) { insertAt = k + 1; break; }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[EventFramework][Tab] 定位 consumablesInfo 失败: " + e);
                }

                // ★ 可选：插到指定文字 tab 之后（用于「事件」右侧）
                if (!string.IsNullOrEmpty(afterText))
                {
                    int idx = FindTextIndex(ti, afterText);
                    if (idx >= 0)
                    {
                        insertAt = idx + 1;
                    }
                    else
                    {
                        Debug.Log("[EventFramework][Tab] 没找到 tab '" + afterText
                            + "'，回退到「饮食」之后");
                    }
                }

                int insertedSoFar = 0;
                int added = 0;
                for (int i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (spec == null || string.IsNullOrEmpty(spec.text)) continue;
                    if (spec.screen == null)
                    {
                        Debug.LogWarning("[EventFramework][Tab] '" + spec.text + "' 的 screen 为 null，跳过");
                        continue;
                    }

                    // 幂等：同名 tab 已在 toggleInfo 里就不再加
                    if (ContainsText(ti, spec.text))
                    {
                        Debug.Log("[EventFramework][Tab] tab '" + spec.text + "' 已存在，跳过（幂等）");
                        continue;
                    }

                    // ---- ① 构造 ManagementMenuToggleInfo ----
                    object info = TryConstructInfo(infoCtor, spec, borrowedHotKey, actionType,
                                                   out string failReason);
                    if (info == null)
                    {
                        Debug.LogWarning("[EventFramework][Tab] '" + spec.text
                                         + "' 的 info 构造失败: " + failReason);
                        continue;
                    }

                    // 构造后把 hotKey 置 null —— 不绑定任何原版快捷键
                    if (hotKeyField != null)
                    {
                        try { hotKeyField.SetValue(info, null); }
                        catch (Exception e) { DistributionLog.ExcOnce("tab:hotkey", "[Tab] 置空 hotKey 失败 → 该页签仍绑原版快捷键", e); }
                    }

                    // ---- ② 构造 ScreenData ----
                    var data = Activator.CreateInstance(dataType);
                    if (screenField != null) screenField.SetValue(data, spec.screen);
                    if (tabIdxField != null) tabIdxField.SetValue(data, 0);
                    if (toggleField != null) toggleField.SetValue(data, info);
                    if (cancelField != null) cancelField.SetValue(data, null);

                    // ---- ③ 登记（★ 插入到「饮食」之后，不是追加到末尾）----
                    sim[info] = data;
                    ti.Insert(Math.Min(insertAt + insertedSoFar, ti.Count), info);
                    insertedSoFar++;
                    added++;
                }

                if (added == 0) return 0;

                // ---- ④ 让原版用它自己的模板重建全部按钮（幂等）----
                var rb = menuType.GetMethod("RefreshButtons", BF);
                if (rb != null)
                {
                    rb.Invoke(menu, null);
                }
                else
                {
                    Debug.LogWarning("[EventFramework][Tab] 找不到 RefreshButtons，tab 可能不显示");
                }

                ResetTabIndicators(menu, menuType);

                // ---- ⑤ 把原版的「配置」(PauseMenuButton) 按回最后 ----
                try
                {
                    var pmbField = menuType.GetField("PauseMenuButton", BF);
                    if (pmbField != null)
                    {
                        var pmb = pmbField.GetValue(menu) as Component;
                        if (pmb != null) pmb.transform.SetAsLastSibling();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[EventFramework][Tab] 恢复 PauseMenuButton 位置失败: " + e);
                }

                // ---- ⑥ 补一次布局（原版 OnPrefabInit 末尾也会调）----
                try
                {
                    var orc = menuType.GetMethod("OnResolutionChanged", BF);
                    if (orc != null) orc.Invoke(menu, new object[] { null });
                }
                catch (Exception e)
                {
                    DistributionLog.ExcOnce("tab:resolution", "[Tab] 通知原版菜单分辨率变化失败 → 页签可能错位一次", e);
                }

                AddedCount += added;
                Debug.Log("[EventFramework][Tab] ★ 已向管理菜单加入 " + added
                          + " 个 tab（总数 " + ti.Count + "），已调 RefreshButtons 重建");
                return added;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EventFramework][Tab] AddTabs 失败（不影响原版菜单）: " + e);
                return 0;
            }
        }

        // 从原版已经建好的 ManagementMenuToggleInfo 里借一个【有效】的 hotKey 值。
        private static object BorrowHotKeyFromVanilla(ManagementMenu menu, Type menuType, Type actionType)
        {
            try
            {
                // 原版 OnPrefabInit 里建过的 info 字段（任取其一即可）
                string[] donors = { "reportsInfo", "codexInfo", "consumablesInfo",
                                    "vitalsInfo", "jobsInfo", "scheduleInfo" };
                for (int i = 0; i < donors.Length; i++)
                {
                    var f = menuType.GetField(donors[i], BF);
                    if (f == null) continue;
                    var donor = f.GetValue(menu);
                    if (donor == null) continue;

                    var hf = FindField(donor.GetType(), "hotKey");
                    if (hf == null) continue;

                    var v = hf.GetValue(donor);
                    if (v != null) return v;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EventFramework][Tab] 借 hotKey 失败: " + e);
            }
            return null;
        }

        // 构造 info：先试借来的有效值；失败则枚举 Action 的所有值逐个试，直到成功。
        private static object TryConstructInfo(ConstructorInfo ctor, TabSpec spec,
                                               object borrowedHotKey, Type actionType,
                                               out string failReason)
        {
            failReason = null;

            if (borrowedHotKey != null)
            {
                var r = TryInvoke(ctor, spec, borrowedHotKey, ref failReason);
                if (r != null) return r;
            }

            if (actionType != null)
            {
                try
                {
                    foreach (var v in Enum.GetValues(actionType))
                    {
                        var r = TryInvoke(ctor, spec, v, ref failReason);
                        if (r != null) return r;
                    }
                }
                catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("tab:BorrowHotKeyFromVanilla", "[Tab] BorrowHotKeyFromVanilla 失败 → 已按兜底处理", e); failReason = e.ToString(); }
            }

            return null;
        }

        private static object TryInvoke(ConstructorInfo ctor, TabSpec spec,
                                        object hotKey, ref string failReason)
        {
            try
            {
                return ctor.Invoke(new object[]
                {
                    spec.text,
                    spec.icon ?? "",
                    null,
                    hotKey,
                    spec.tooltip ?? "",
                    "",
                });
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("tab:BorrowHotKeyFromVanilla#2", "[Tab] BorrowHotKeyFromVanilla 失败 → 已按兜底处理", e);
                failReason = (e.InnerException ?? e).ToString();
                return null;
            }
        }

        private static int FindTextIndex(IList toggleInfo, string text)
        {
            try
            {
                for (int i = 0; i < toggleInfo.Count; i++)
                {
                    var o = toggleInfo[i];
                    if (o == null) continue;
                    var f = FindField(o.GetType(), "text");
                    if (f != null && (f.GetValue(o) as string) == text) return i;
                }
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("tab:findtext:" + text, "[Tab] 按文本查页签下标失败 → 按未找到处理", e);
            }
            return -1;
        }

        private static bool ContainsText(IList toggleInfo, string text)
        {
            try
            {
                for (int i = 0; i < toggleInfo.Count; i++)
                {
                    var o = toggleInfo[i];
                    if (o == null) continue;
                    var f = FindField(o.GetType(), "text");
                    if (f != null && (f.GetValue(o) as string) == text) return true;
                }
            }
            catch (Exception e)
            {
                DistributionLog.ExcOnce("tab:hastext:" + text, "[Tab] 按文本查页签失败 → 按未找到处理", e);
            }
            return false;
        }

        private static FieldInfo FindField(Type t, string name)
        {
            while (t != null)
            {
                var f = t.GetField(name, BF);
                if (f != null) return f;
                t = t.BaseType;
            }
            return null;
        }

        private static void ResetTabIndicators(ManagementMenu menu, Type menuType)
        {
            if (menu == null) return;
            try
            {
                var toggles = menu.GetComponentsInChildren<KToggle>(true);
                int alertOff = 0;
                for (int i = 0; i < toggles.Length; i++)
                {
                    var kt = toggles[i];
                    if (kt == null) continue;
                    var alert = kt.transform.Find("AlertIcon");
                    if (alert != null && alert.gameObject.activeSelf)
                    {
                        alert.gameObject.SetActive(false);
                        alertOff++;
                    }
                    var glow = kt.transform.Find("OuterGlow");
                    if (glow != null)
                    {
                        bool want = false;
                        try { want = kt.isOn; }
                        catch (Exception e) { DistributionLog.ExcOnce("tab:ison", "[Tab] 读页签选中态失败 → 按未选中处理", e); }
                        if (glow.gameObject.activeSelf != want) glow.gameObject.SetActive(want);
                    }
                }
                var nm = menuType.GetMethod("OnNotificationsChanged", BF);
                if (nm != null) nm.Invoke(menu, null);
                Debug.Log("[EventFramework][Tab] 页签指示器已复位（关闭 AlertIcon " + alertOff
                          + " 个，OuterGlow 按选中态校正）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EventFramework][Tab] 页签指示器复位失败: " + e);
            }
        }
    }
}
