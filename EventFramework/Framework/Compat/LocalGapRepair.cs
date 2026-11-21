// LocalGapRepair.cs — 词条缺口通用修复：把原版 STRINGS 中空/Missing 的词条用其默认文本回填

using System;
using System.Reflection;
using EventFramework.Distribution.Contracts;
using HarmonyLib;
using UnityEngine;

namespace EventFramework
{
    // 通用防御：任意来源（第三方 mod 遍历重写 STRINGS 等）造成的词条缺口都在此收口
    public static class LocalGapRepair
    {
        private const string TAG = "[MoreFood/Event][Compat]";

        private static bool _installed;
        private static bool _disabled;      // 熔断：修复抛异常后彻底回退默认行为
        private static FieldInfo[] _fields; // 反射缓存

        // 手动装配（目标缺失时不会拖垮 PatchAll；由宿主或框架使用方调用）
        public static void Install(Harmony harmony)
        {
            if (_installed) return;
            _installed = true;

            try
            {
                var t = AccessTools.TypeByName("Localization");
                if (t == null) { Debug.LogWarning(TAG + " 找不到 Localization，跳过词条缺口修复"); return; }

                var m = AccessTools.Method(t, "Initialize");
                if (m == null) { Debug.LogWarning(TAG + " 找不到 Localization.Initialize，跳过词条缺口修复"); return; }

                var pi = AccessTools.Method(typeof(LocalGapRepair), nameof(Postfix));
                if (pi == null) { Debug.LogWarning(TAG + " 找不到自身 Postfix，跳过词条缺口修复"); return; }

                harmony.Patch(m, postfix: new HarmonyMethod(pi));
                Debug.Log(TAG + " 词条缺口修复已挂：Localization.Initialize");
            }
            catch (Exception e)
            {
                Debug.LogWarning(TAG + " 词条缺口修复安装失败（已放弃，等同无补丁）: " + e);
            }
        }

        // 在所有本地化补丁之后执行（Priority.Last）
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (_disabled) return;

            try { RepairNow(); }
            catch (Exception e)
            {
                _disabled = true;
                Debug.LogWarning(TAG + " 词条缺口修复异常，已熔断（回退默认行为）: " + e);
            }
        }

        // 立即修复，返回回填条数（幂等：只处理空/Missing 的词条，不覆盖已有值）
        public static int RepairNow()
        {
            var stringsType = AccessTools.TypeByName("STRINGS");
            if (stringsType == null) return 0;

            int fixedCount = 0;
            RepairType(stringsType, "", ref fixedCount);
            return fixedCount;
        }

        private static void RepairType(Type type, string parent, ref int fixedCount)
        {
            if (type == null) return;

            string prefix = parent + type.Name + ".";

            CacheFields(type);
            for (int i = 0; i < _fields.Length; i++)
            {
                var f = _fields[i];
                if (f.FieldType != typeof(LocString)) continue;

                try
                {
                    var ls = f.GetValue(null) as LocString;
                    if (ls == null) continue;

                    string key = prefix + f.Name;
                    string cur = Strings.Get(key);
                    if (!string.IsNullOrEmpty(cur) && !cur.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase)) continue;

                    string text = ls.text;
                    if (string.IsNullOrEmpty(text)) continue;

                    Strings.Add(key, text);
                    fixedCount++;
                }
                catch (Exception e)
                {
                    DistributionLog.ExcOnce("gap:" + type.FullName, "[GapRepair] 回填 " + type.FullName + " 失败 → 跳过该类型", e);
                }
            }

            foreach (var n in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)) RepairType(n, prefix, ref fixedCount);
        }

        private static void CacheFields(Type type)
        {
            _fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        }
    }
}