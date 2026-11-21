using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Klei.CustomSettings;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class SettingLocFix
    {
        // 设置项文案的 STRINGS 前缀
        private const string PREFIX = "STRINGS.UI.FRONTEND.CUSTOMGAMESETTINGSSCREEN.SETTINGS.";
        // STRINGS.UI 公共前缀
        private const string UI_PREFIX = "STRINGS.UI.";

        private static readonly Dictionary<string, string> SegmentOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ClusterLayout", "CLUSTER_CHOICE" },
            { "BionicWattage", "BIONICPOWERUSE" },
            { "GuranteeMixing", "GUARANTEE_MIXING" },
        };

        private static readonly Dictionary<string, string> MixingLevelPrefix =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "DlcMixingSettingConfig", "DLC_MIXING" },
            { "WorldMixingSettingConfig", "WORLD_MIXING" },
            { "SubworldMixingSettingConfig", "SUBWORLD_MIXING" },
        };

        // 反射缓存的设置项 Label 字段
        private static FieldInfo _cfgLabel;
        // 反射缓存的设置项 tooltip 字段
        private static FieldInfo _cfgTooltip;
        // 反射缓存的档位 Label 字段
        private static FieldInfo _levelLabel;
        // 反射缓存的档位 tooltip 字段
        private static FieldInfo _levelTooltip;
        // 上次修复的条数（-1 = 没跑过）
        private static int _lastFixed = -1;

        public static void Apply()
        {
            if (!ModOptions.Current.TranslationFix) return;
            try
            {
                string code = Localization.GetCurrentLanguageCode();
                if (string.IsNullOrEmpty(code)) return;
                if (!code.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return;

                var cgs = CustomGameSettings.Instance;
                if (cgs == null || cgs.QualitySettings == null) return;

                int fixedCount = 0;
                foreach (var kv in cgs.QualitySettings)
                {
                    var cfg = kv.Value;
                    if (cfg == null || string.IsNullOrEmpty(kv.Key)) continue;

                    string up = ResolveSegment(kv.Key);
                    fixedCount += FixConfig(cfg, PREFIX + up + ".");

                    var levels = cfg.GetLevels();
                    if (levels == null) continue;

                    for (int i = 0; i < levels.Count; i++)
                    {
                        var lv = levels[i];
                        if (lv == null || string.IsNullOrEmpty(lv.id)) continue;
                        fixedCount += FixLevel(lv, PREFIX + up + ".LEVELS." + ResolveSegment(lv.id) + ".");
                    }
                }

                fixedCount += FixMixing(cgs);

                if (fixedCount == _lastFixed) return;
                _lastFixed = fixedCount;

                MFLog.Info(LogCat.UI, "[MoreFood/Loc] 设置面板词条已按中文表重写：" + fixedCount + " 处（语言=" + code + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, "[MoreFood/Loc] 设置面板词条修复失败（已忽略，不影响游戏）");
            }
        }

        private static int FixMixing(CustomGameSettings cgs)
        {
            if (cgs.MixingSettings == null) return 0;

            int n = 0;
            foreach (var kv in cgs.MixingSettings)
            {
                var cfg = kv.Value;
                if (cfg == null) continue;

                string typeName = cfg.GetType().Name;
                string levelSeg;
                if (MixingLevelPrefix.TryGetValue(typeName, out levelSeg))
                {
                    string levelPrefix = PREFIX + levelSeg + ".LEVELS.";
                    var levels = cfg.GetLevels();
                    if (levels != null)
                    {
                        for (int i = 0; i < levels.Count; i++)
                        {
                            var lv = levels[i];
                            if (lv == null || string.IsNullOrEmpty(lv.id)) continue;
                            n += FixLevel(lv, levelPrefix + ResolveSegment(lv.id) + ".");
                        }
                    }
                }

                if (string.Equals(typeName, "DlcMixingSettingConfig", StringComparison.Ordinal))
                {
                    string id = kv.Key;
                    if (!string.IsNullOrEmpty(id))
                    {
                        string dlc = id.EndsWith("_ID", StringComparison.Ordinal)
                                     ? id.Substring(0, id.Length - 3) : id;

                        string label = Lookup(UI_PREFIX + dlc + ".NAME");
                        if (label != null && cfg.label != label && _cfgLabel != null)
                        {
                            _cfgLabel.SetValue(cfg, label);
                            n++;
                        }

                        string tip = Lookup(UI_PREFIX + dlc + ".MIXING_TOOLTIP");
                        if (tip != null && cfg.tooltip != tip && _cfgTooltip != null)
                        {
                            _cfgTooltip.SetValue(cfg, tip);
                            n++;
                        }
                    }
                }
            }
            return n;
        }

        private static int FixConfig(SettingConfig cfg, string prefix)
        {
            EnsureFields();
            int n = 0;

            string label = Lookup(prefix + "NAME");
            if (label != null && cfg.label != label && _cfgLabel != null)
            {
                _cfgLabel.SetValue(cfg, label);
                n++;
            }

            string tip = Lookup(prefix + "TOOLTIP");
            if (tip != null && cfg.tooltip != tip && _cfgTooltip != null)
            {
                _cfgTooltip.SetValue(cfg, tip);
                n++;
            }

            return n;
        }

        private static int FixLevel(SettingLevel lv, string prefix)
        {
            EnsureFields();
            int n = 0;

            string label = Lookup(prefix + "NAME");
            if (label != null && lv.label != label && _levelLabel != null)
            {
                _levelLabel.SetValue(lv, label);
                n++;
            }

            string tip = Lookup(prefix + "TOOLTIP");
            if (tip != null && lv.tooltip != tip && _levelTooltip != null)
            {
                _levelTooltip.SetValue(lv, tip);
                n++;
            }

            return n;
        }

        private static void EnsureFields()
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;

            if (_cfgLabel == null) _cfgLabel = typeof(SettingConfig).GetField("<label>k__BackingField", F);
            if (_cfgTooltip == null) _cfgTooltip = typeof(SettingConfig).GetField("<tooltip>k__BackingField", F);
            if (_levelLabel == null) _levelLabel = typeof(SettingLevel).GetField("<label>k__BackingField", F);
            if (_levelTooltip == null) _levelTooltip = typeof(SettingLevel).GetField("<tooltip>k__BackingField", F);
        }

        private static string Lookup(string key)
        {
            try
            {
                string s = Strings.Get(key);
                if (string.IsNullOrEmpty(s)) return null;
                if (s.StartsWith("MISSING", StringComparison.Ordinal)) return null;
                return s;
            }
            catch { return null; }
        }

        private static string ResolveSegment(string id)
        {
            string over;
            if (SegmentOverrides.TryGetValue(id, out over)) return over;

            string plain = id.ToUpperInvariant();
            if (Lookup(PREFIX + plain + ".NAME") != null) return plain;

            string snake = ToSnakeUpper(id);
            if (snake != plain && Lookup(PREFIX + snake + ".NAME") != null) return snake;

            return plain;
        }

        private static string ToSnakeUpper(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;

            var sb = new System.Text.StringBuilder(id.Length + 4);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (i > 0 && c >= 'A' && c <= 'Z') sb.Append('_');
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }

    [HarmonyPatch(typeof(CustomGameSettings), "OnPrefabInit")]
    public static class SettingLocFix_OnPrefabInit_Patch
    {
        public static void Postfix()
        {
            try { SettingLocFix.Apply(); }
            catch (System.Exception e) { MFLog.HotFail("patch:SettingLocFix", e); }
        }
    }

    [HarmonyPatch(typeof(Localization), "Initialize")]
    public static class SettingLocFix_Initialize_Patch
    {
        public static void Postfix()
        {
            try
            {
                SettingLocFix.Apply();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SettingLocFix_Initialize_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(Localization), "LoadPreinstalledTranslation")]
    public static class SettingLocFix_Preinstalled_Patch
    {
        public static void Postfix()
        {
            try
            {
                SettingLocFix.Apply();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SettingLocFix_Preinstalled_Patch", e); }
        }
    }
}
