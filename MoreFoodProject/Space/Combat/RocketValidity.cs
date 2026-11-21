// 用途：过滤存档里缺失引擎或前锥的无效火箭，只影响面板显示，不改原版数据

using System;
using System.Collections.Generic;

namespace MoreFood_KFC
{
    internal static class RocketValidity
    {
        private static readonly HashSet<string> EngineIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "CO2Engine", "SugarEngine", "SteamEngine", "KeroseneEngineClusterSmall",
            "KeroseneEngineCluster", "BiodieselEngineCluster", "HEPEngine", "HydrogenEngineCluster"
        };

        private static readonly HashSet<string> NoseconeIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "HabitatModuleSmall", "NoseconeBasic", "NoseconeHarvest"
        };

        internal static bool IsValidCraft(Clustercraft craft)
        {
            if (craft == null) return false;
            if (craft.Status != Clustercraft.CraftStatus.Grounded) return true;
            try
            {
                List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
                bool hasEngine = false;
                bool hasNosecone = false;
                for (int i = 0; i < mods.Count; i++)
                {
                    string id = mods[i].prefabId;
                    if (EngineIds.Contains(id)) hasEngine = true;
                    else if (NoseconeIds.Contains(id)) hasNosecone = true;
                    if (hasEngine && hasNosecone) return true;
                }
                return false;
            }
            catch (Exception e) { MFLog.HotFail("rocket:validity", e); return true; }
        }
    }
}
