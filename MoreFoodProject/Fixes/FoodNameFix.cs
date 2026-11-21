// FoodNameFix.cs — 修复「食物统计显示 MISSING.STRINGS.ITEMS.FOOD.XXX.NAME」：Db.Initialize 后扫 s_allFoodMap 补缓存值

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class FoodNameFix
    {
        // .po key 前缀（与 LanguageHelper.cs 用的是同一套）
        private const string PoPrefix = "MoreFood_KFC.ModStrings.ITEMS.FOOD.";

        // 警告只打一次
        private static bool _warned;

        // 扫一遍所有已注册的食物，补上 MISSING 的名称/描述。幂等。
        public static void FixAll()
        {
            if (!ModOptions.Current.TranslationFix) return;
            // ⚠⚠ 必须用 GetAllLoadedFoodTypes()，【不能用 GetAllFoodTypes()】：
            try { EdiblesManager.ClearSaveFoodCache(); }   // 清掉可能已被污染的缓存
            catch (Exception) { /* expected: 老版本 EdiblesManager 无 ClearSaveFoodCache，跳过清缓存直接用现有缓存扫描 */ }

            List<EdiblesManager.FoodInfo> all = EdiblesManager.GetAllLoadedFoodTypes();
            if (all == null)
            {
                Debug.LogWarning("[MoreFood/Drink] GetAllLoadedFoodTypes() 为 null，食物名修复跳过");
                return;
            }

            int fixedNames = 0, fixedDescs = 0, scanned = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var info = all[i];
                if (info == null) continue;

                string id = info.Id;
                if (string.IsNullOrEmpty(id)) continue;
                scanned++;

                string upper = id.ToUpperInvariant();

                info.Name = ResolveBetter(info.Name, upper, "NAME", ref fixedNames);
                info.Description = ResolveBetter(info.Description, upper, "DESC", ref fixedDescs);
            }

            // 打上扫描条数，方便下次一眼看出"是不是又扫了个空表"
            MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 食物名检查：扫描 " + scanned + " 条，修复 NAME "
                      + fixedNames + " / DESC " + fixedDescs);
        }

        private static bool IsMissing(string s)
        {
            return string.IsNullOrEmpty(s) || s.StartsWith("MISSING");
        }

        // 返回"更好的"文本，按两步：
        private static string ResolveBetter(string current, string upperId, string suffix, ref int changed)
        {
            string live = Strings.Get("STRINGS.ITEMS.FOOD." + upperId + "." + suffix);
            if (!IsMissing(live))
            {
                if (live != current) { changed++; return live; }
                return current;
            }

            string po = LookupPo(upperId, suffix);
            if (po != null && po != current) { changed++; return po; }
            if (po == null && IsMissing(current)) WarnOnce(upperId, suffix);
            return current;
        }

        // 走 .po 那条已验证可用的取值路径
        private static string LookupPo(string upperId, string suffix)
        {
            string s = Strings.Get(PoPrefix + upperId + "." + suffix);
            return IsMissing(s) ? null : s;
        }

        private static void WarnOnce(string id, string suffix)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[MoreFood/Drink] 取不到 .po 文本：" + PoPrefix + id + "." + suffix
                             + " —— 检查 translations/*.po 是否缺该条目");
        }
    }

    // 触发点：Db.Initialize 晚于 OnLoad（ModFoods.Init 在 OnLoad 里跑）
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class FoodNameFix_DbInitialize_Patch
    {
        public static void Postfix()
        {
            try
            {
                FoodNameFix.FixAll();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:FoodNameFix_DbInitialize_Patch", e); }
        }
    }
}
