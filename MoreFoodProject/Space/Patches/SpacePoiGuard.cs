using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(HarvestablePOIConfigurator), "CreateRandomInstance")]
    internal static class HarvestablePoiGuardPatch
    {
        private static bool Prefix(ref HarvestablePOIConfigurator.HarvestablePOIInstanceConfiguration __result,
            HashedString typeId, float min, float max)
        {
            try
            {
                if (SaveLoader.Instance != null && SaveLoader.Instance.clusterDetailSave != null)
                {
                    return true;
                }
                __result = new HarvestablePOIConfigurator.HarvestablePOIInstanceConfiguration
                {
                    typeId = typeId,
                    capacityRoll = min,
                    rechargeRoll = min
                };
                Debug.LogWarning("[Space] 原版 POI 在世界存档就绪前取配置（clusterDetailSave 为空）→ 已跳过，"
                    + "避免 NRE 级联把游戏弹回主菜单");
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] POI 保护补丁异常");
                return true;
            }
        }
    }
}
