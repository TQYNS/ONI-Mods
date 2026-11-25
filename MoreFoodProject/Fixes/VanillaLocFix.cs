using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class VanillaLocFix
    {
        private static readonly Dictionary<string, string> Table =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "STRINGS.CODEX.CREATURE_DESCRIPTORS.CONFINED_AQUATIC",
              "当房间小于<b>{0}格</b>时会感到<link=\"CREATURES::GUIDE::MOOD\">封闭</link>。" },

            { "STRINGS.CODEX.CREATURE_DESCRIPTORS.OVERCROWDING_AQUATIC",
              "当房间空间对每只小动物平均少于<b>{0}格</b>时会感到<link=\"CREATURES::GUIDE::MOOD\">拥挤</link>。" },

            { "STRINGS.CODEX.PACU.BODY.CONTAINER2",
              "给帕库鱼喂它最喜欢的<link=\"PACUTREAT\">帕库鱼食</link>，有助于驯化它并让它保持满足。" },

            { "STRINGS.CODEX.STORY_TRAITS.CRITTER_MANIPULATOR.SPECIES_ENTRIES.BUTTERFLY",
              "这只小动物在扫描过程中把翅膀紧紧收拢在身体周围。查看数据以获得更多信息。" },

            { "STRINGS.CODEX.STORY_TRAITS.CRITTER_MANIPULATOR.SPECIES_ENTRIES_EXPANDED.BUTTERFLY",
              "这只小动物在扫描过程中把翅膀紧紧收拢在身体周围，并用触角把自己的身体在舱室里滚来滚去。\\n\\n这些感觉器官的外表虽然不起眼，但肌肉组织似乎比同类物种更发达。尤其值得注意的是：一旦离开这个封闭的环境，它就拒绝做出这种动作。" },
        };

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

                int n = 0;
                foreach (var kv in Table)
                {
                    if (ApplyOne(kv.Key, kv.Value)) n++;
                }

                n += SyncTableFromFields();
                n += SyncCaptured();

                if (n == _lastFixed) return;
                _lastFixed = n;

                if (n > 0)
                    MFLog.Info(LogCat.UI, "[MoreFood/Loc] 补原版缺失词条：" + n + " 条（语言=" + code + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, "[MoreFood/Loc] 补原版词条失败（已忽略，不影响游戏）");
            }
        }

        private static bool ApplyOne(string key, string zh)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(zh)) return false;

            var f = FindField(key);
            if (f == null || f.FieldType != typeof(LocString))
            {
                WarnOnce("field:" + key, "[MoreFood/Loc] 找不到词条字段（跳过）: " + key);
                return false;
            }

            var cur = f.GetValue(null) as LocString;
            if (cur != null && cur.text == zh) return false;

            f.SetValue(null, new LocString(zh, key));
            Strings.Add(key, zh);
            return true;
        }

        private static int SyncCaptured()
        {
            int n = 0;

            n += SyncTag("Minion", "STRINGS.DUPLICANTS.MODEL.STANDARD.NAME");
            n += SyncTag("BionicMinion", "STRINGS.DUPLICANTS.MODEL.BIONIC.NAME");

            n += SyncField("MinionConfig", "NAME", "STRINGS.DUPLICANTS.MODEL.STANDARD.NAME");
            n += SyncField("MinionConfigBionic", "NAME", "STRINGS.DUPLICANTS.MODEL.BIONIC.NAME");

            return n;
        }

        private static int SyncTag(string tagName, string key)
        {
            string zh = FieldText(key);
            if (string.IsNullOrEmpty(zh)) return 0;

            try
            {
                string cur = TagManager.GetProperName(TagManager.Create(tagName), false);
                if (string.Equals(cur, zh, StringComparison.Ordinal)) return 0;

                TagManager.Create(tagName, zh);
                return 1;
            }
            catch
            {
                WarnOnce("tag:" + tagName, "[MoreFood/Loc] 改写 Tag 显示名失败（跳过）: " + tagName);
                return 0;
            }
        }

        private static int SyncField(string typeName, string fieldName, string key)
        {
            string zh = FieldText(key);
            if (string.IsNullOrEmpty(zh)) return 0;

            Type t = null;
            try { t = typeof(Localization).Assembly.GetType(typeName, false); }
            catch { /* expected: 类型名解析不到时跳过该字段同步，其余本地化条目继续 */ }
            if (t == null) return 0;

            var f = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null || f.FieldType != typeof(LocString)) return 0;

            var cur = f.GetValue(null) as LocString;
            if (cur != null && cur.text == zh) return 0;

            try
            {
                f.SetValue(null, new LocString(zh, key));
                return 1;
            }
            catch { return 0; }
        }

        private static string FieldText(string key)
        {
            var f = FindField(key);
            if (f == null || f.FieldType != typeof(LocString)) return null;

            var ls = f.GetValue(null) as LocString;
            if (ls == null || string.IsNullOrEmpty(ls.text)) return null;
            if (ls.text.StartsWith("MISSING", StringComparison.Ordinal)) return null;
            return ls.text;
        }

        private static int SyncTableFromFields()
        {
            int n = 0;

            Type[] types;
            try { types = typeof(Localization).Assembly.GetTypes(); }
            catch { return 0; }

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null) continue;
                if (!string.Equals(t.Namespace, "STRINGS", StringComparison.Ordinal)) continue;

                FieldInfo[] fields;
                try { fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); }
                catch { continue; }

                for (int k = 0; k < fields.Length; k++)
                {
                    var f = fields[k];
                    if (f.FieldType != typeof(LocString)) continue;

                    var ls = f.GetValue(null) as LocString;
                    if (ls == null || string.IsNullOrEmpty(ls.text)) continue;

                    // 绝不把 MISSING 占位串写进 Strings 表：一旦烤进去，
                    //   反向修复（ManagementMenuLocGuard）连可用的文本源都会失去。
                    // 根因修复：此前这里只判了空，没判 MISSING。
                    if (ls.text.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase)) continue;

                    string key = null;
                    try { key = ls.key.String; }
                    catch { /* expected: LocString 缺 key 时取不到键名，跳过该字段不写 Strings 表 */ }
                    if (string.IsNullOrEmpty(key)) continue;

                    string cur;
                    try { cur = Strings.Get(key); }
                    catch { continue; }
                    if (string.Equals(cur, ls.text, StringComparison.Ordinal)) continue;

                    try
                    {
                        Strings.Add(key, ls.text);
                        n++;
                    }
                    catch { /* expected: 该键写入 Strings 失败时跳过这一条，其余键继续同步 */ }
                }
            }
            return n;
        }

        private static readonly System.Collections.Generic.HashSet<string> _warned =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        private static void WarnOnce(string tag, string msg)
        {
            if (_warned.Contains(tag)) return;
            _warned.Add(tag);
            Debug.LogWarning(msg);
        }

        private static FieldInfo FindField(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            string[] parts = key.Split('.');
            if (parts.Length < 2) return null;

            var asm = typeof(Localization).Assembly;

            Type t = null;
            int used = 0;

            for (int i = parts.Length - 1; i >= 1; i--)
            {
                Type cand;
                try { cand = asm.GetType(string.Join(".", parts, 0, i), false); }
                catch { continue; }
                if (cand == null) continue;

                t = cand;
                used = i;
                break;
            }

            if (t == null) return null;

            for (int i = used; i < parts.Length; i++)
            {
                Type nested = t.GetNestedType(parts[i], BindingFlags.Public | BindingFlags.NonPublic);
                if (nested != null) { t = nested; continue; }

                return t.GetField(parts[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }
            return null;
        }

    }

    [HarmonyPatch(typeof(Localization), "Initialize")]
    public static class VanillaLocFix_Initialize_Patch
    {
        public static void Postfix()
        {
            try
            {
                VanillaLocFix.Apply();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:VanillaLocFix_Initialize_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(Localization), "LoadPreinstalledTranslation")]
    public static class VanillaLocFix_Preinstalled_Patch
    {
        public static void Postfix()
        {
            try
            {
                VanillaLocFix.Apply();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:VanillaLocFix_Preinstalled_Patch", e); }
        }
    }
}
