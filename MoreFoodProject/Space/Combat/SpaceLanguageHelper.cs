// SpaceLanguageHelper.cs — 显示名多语言（po 覆盖不到静态化的显示名 → patch 查表）

using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class SpaceNameHelper
    {
        // prefabId → po key（命名空间.树路径）映射；未登记返回 null。
        internal static string GetDisplayName(string prefabId)
        {
            string key;
            switch (prefabId)
            {
                // 弹药
                case "AmmoHEBasic": key = "Space.ModStrings.ITEMS.AMMO.AMMOHEBASIC.NAME"; break;
                case "AmmoCruise": key = "Space.ModStrings.ITEMS.AMMO.AMMOCRUISE.NAME"; break;
                case "AmmoCluster": key = "Space.ModStrings.ITEMS.AMMO.AMMOCLUSTER.NAME"; break;
                case "AmmoThermobaric": key = "Space.ModStrings.ITEMS.AMMO.AMMOTHERMOBARIC.NAME"; break;
                case "AmmoLaserGuided": key = "Space.ModStrings.ITEMS.AMMO.AMMOLASERGUIDED.NAME"; break;
                case "AmmoPenetrator": key = "Space.ModStrings.ITEMS.AMMO.AMMOPENETRATOR.NAME"; break;
                case "AmmoICBM": key = "Space.ModStrings.ITEMS.AMMO.AMMOICBM.NAME"; break;

                // 模块
                case "ModuleItem_weapon_small": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMWEAPONSMALL.NAME"; break;
                case "ModuleItem_weapon_medium": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMWEAPONMEDIUM.NAME"; break;
                case "ModuleItem_weapon_large": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMWEAPONLARGE.NAME"; break;
                case "ModuleItem_module_penetrator_1": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR1.NAME"; break;
                case "ModuleItem_module_penetrator_2": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR2.NAME"; break;
                case "ModuleItem_module_penetrator_3": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULEPENETRATOR3.NAME"; break;
                case "ModuleItem_module_speed_1": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED1.NAME"; break;
                case "ModuleItem_module_speed_2": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED2.NAME"; break;
                case "ModuleItem_module_speed_3": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULESPEED3.NAME"; break;
                case "ModuleItem_module_radar_1": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR1.NAME"; break;
                case "ModuleItem_module_radar_2": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR2.NAME"; break;
                case "ModuleItem_module_radar_3": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULERADAR3.NAME"; break;
                case "ModuleItem_module_repair_1": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR1.NAME"; break;
                case "ModuleItem_module_repair_2": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR2.NAME"; break;
                case "ModuleItem_module_repair_3": key = "Space.ModStrings.ITEMS.MODULE.MODULEITEMMODULEREPAIR3.NAME"; break;

                default: return null;
            }

            string s = Strings.Get(key);
            if (s == null || s.StartsWith("MISSING")) return null;
            return s;
        }
    }

    // 选中信息卡 / 头顶显示名
    [HarmonyPatch(typeof(KSelectable), "GetName")]
    public static class SpaceDisplayNamePatch
    {
        static void Postfix(KSelectable __instance, ref string __result)
        {
            try
            {
                var kp = __instance != null ? __instance.GetComponent<KPrefabID>() : null;
                if (kp == null) return;
                string name = SpaceNameHelper.GetDisplayName(kp.PrefabTag.Name);
                if (name != null) __result = name;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SpaceDisplayNamePatch", e); }
        }
    }

    // 标签 / 提示（TagManager.ProperNames → 回退 tag.Name）
    [HarmonyPatch(typeof(TagManager), "GetProperName")]
    public static class SpaceProperNamePatch
    {
        static void Postfix(Tag tag, ref string __result)
        {
            try
            {
                string name = SpaceNameHelper.GetDisplayName(tag.Name);
                if (name != null) __result = name;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SpaceProperNamePatch", e); }
        }
    }
}
