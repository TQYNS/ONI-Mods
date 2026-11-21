using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(OxidizerTank), "get_TotalOxidizerPower")]
    internal static class OxidizerTankInvalidItemPatch
    {
        private static readonly HashSet<string> warned = new HashSet<string>();

        private static bool Prefix(OxidizerTank __instance, ref float __result)
        {
            try
            {
                Storage storage = __instance != null ? __instance.GetComponent<Storage>() : null;
                if (storage == null || storage.items == null)
                {
                    __result = 0f;
                    return false;
                }
                Dictionary<Tag, float> table = DlcManager.FeatureClusterSpaceEnabled()
                    ? Clustercraft.dlc1OxidizerEfficiencies
                    : global::RocketStats.oxidizerEfficiencies;
                float power = 0f;
                for (int i = 0; i < storage.items.Count; i++)
                {
                    GameObject item = storage.items[i];
                    if (item == null)
                    {
                        continue;
                    }
                    PrimaryElement pe = item.GetComponent<PrimaryElement>();
                    if (pe == null)
                    {
                        continue;
                    }
                    Tag tag = pe.ElementID.CreateTag();
                    float efficiency;
                    if (table == null || !table.TryGetValue(tag, out efficiency))
                    {
                        string key = tag.Name;
                        if (warned.Add(key))
                        {
                            Debug.LogWarning("[Space] 氧化剂罐里有非氧化剂物品 " + key
                                + "（已从氧化剂能量计算中跳过，避免原版字典 KeyNotFound）");
                        }
                        continue;
                    }
                    power += pe.Mass * efficiency;
                }
                __result = power;
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 氧化剂能量计算保护失败（交回原版）");
                return true;
            }
        }
    }
}
