using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class ToolTipLocFix
    {
        private static readonly Dictionary<string, string> Replacements =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Standard Duplicant", "标准复制人" },
            { "Bionic Duplicant", "仿生复制人" },
        };

        private static readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        // 反射缓存的 MultiStringTooltip 字段
        private static FieldInfo _multi;

        // 修正原版 ToolTip 列表里的中文缺失（仅中文环境生效）
        public static void FixList(ToolTip tip)
        {
            if (!ModOptions.Current.TranslationFix) return;
            if (tip == null) return;
            if (!IsChinese()) return;

            try
            {
                if (_multi == null)
                    _multi = typeof(ToolTip).GetField("multiStringToolTips",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                if (_multi == null) return;

                var list = _multi.GetValue(tip) as List<string>;
                if (list == null || list.Count == 0) return;

                for (int i = 0; i < list.Count; i++)
                {
                    string s = list[i];
                    if (string.IsNullOrEmpty(s)) continue;

                    string fixedText = s;
                    foreach (var kv in Replacements)
                    {
                        if (fixedText.IndexOf(kv.Key, StringComparison.Ordinal) < 0) continue;
                        fixedText = fixedText.Replace(kv.Key, kv.Value);

                        if (_reported.Contains(kv.Key)) continue;
                        _reported.Add(kv.Key);
                        Debug.LogWarning("[MoreFood/Loc] 工具提示里发现 '" + kv.Key
                                         + "' → 已替换；调用栈：\n" + new System.Diagnostics.StackTrace(true));
                    }
                    if (!ReferenceEquals(fixedText, s)) list[i] = fixedText;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, "[MoreFood/Loc] 工具提示修复失败（忽略）");
            }
        }

        private static bool IsChinese()
        {
            try
            {
                string code = Localization.GetCurrentLanguageCode();
                return !string.IsNullOrEmpty(code) && code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    [HarmonyPatch(typeof(ToolTip), "SetSimpleTooltip")]
    public static class ToolTipLocFix_Simple_Patch
    {
        public static void Postfix(ToolTip __instance)
        {
            try
            {
                ToolTipLocFix.FixList(__instance);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ToolTipLocFix_Simple_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(ToolTip), "AddMultiStringTooltip")]
    public static class ToolTipLocFix_Multi_Patch
    {
        public static void Postfix(ToolTip __instance)
        {
            try
            {
                ToolTipLocFix.FixList(__instance);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ToolTipLocFix_Multi_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(ToolTip), "RebuildDynamicTooltip")]
    public static class ToolTipLocFix_Rebuild_Patch
    {
        public static void Postfix(ToolTip __instance)
        {
            try
            {
                ToolTipLocFix.FixList(__instance);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ToolTipLocFix_Rebuild_Patch", e); }
        }
    }
}
