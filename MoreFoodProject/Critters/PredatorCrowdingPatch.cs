// PredatorCrowdingPatch.cs — 捕食者判定拥挤时排除同域活体猎物的占用格数（选项分类「补丁」默认开启）
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(OvercrowdingMonitor.RegionAnalysis), "get_OccupiedCellCount")]
    internal static class RegionAnalysis_OccupiedCellCount_Patch
    {
        private static readonly FieldInfo SmiField =
            AccessTools.Field(typeof(OvercrowdingMonitor.RegionAnalysis), "smi");

        private static readonly MethodInfo SpaceMethod =
            AccessTools.Method(typeof(OvercrowdingMonitor), "FetchCreaturePersonalSpace", new[] { typeof(KPrefabID) });

        private static readonly Dictionary<Tag, int> SpaceCache = new Dictionary<Tag, int>();

        // 补丁：Postfix（__result = 原版占用格数）——只减当前捕食者自己看不到的猎物占用，不碰共享 Occupancy
        public static void Postfix(OvercrowdingMonitor.RegionAnalysis __instance, ref int __result)
        {
            try
            {
                if (!ModOptions.Current.PredatorIgnoresPreyCrowding || __result <= 0 || SmiField == null || SpaceMethod == null)
                {
                    return;
                }
                OvercrowdingMonitor.Instance smi = SmiField.GetValue(__instance) as OvercrowdingMonitor.Instance;
                if (smi == null || smi.kpid == null || smi.def.spaceRequiredPerCreature == 0)
                {
                    return;
                }
                DietManager manager = DietManager.Instance;
                Diet diet = manager == null ? null : manager.GetPrefabDiet(smi.kpid.gameObject);
                if (!IsPredator(diet))
                {
                    return;
                }
                List<KPrefabID> peers = smi.IsFish
                    ? (smi.pond == null ? null : smi.pond.fishes)
                    : (smi.cavity == null ? null : smi.cavity.creatures);
                if (peers == null)
                {
                    return;
                }
                int excluded = 0;
                for (int i = 0; i < peers.Count; i++)
                {
                    KPrefabID peer = peers[i];
                    if (peer == null || peer == smi.kpid || peer.HasTag(GameTags.Egg) || !IsLivePrey(peer, diet))
                    {
                        continue;
                    }
                    int space;
                    if (!SpaceCache.TryGetValue(peer.PrefabTag, out space))
                    {
                        space = (int)SpaceMethod.Invoke(null, new object[] { peer });
                        if (space > 0)
                        {
                            SpaceCache[peer.PrefabTag] = space;
                        }
                    }
                    excluded += space;
                }
                if (excluded > 0)
                {
                    __result = Mathf.Max(0, __result - excluded);
                }
            }
            catch (Exception e) { MFLog.HotFail("patch:PredatorCrowding", e); }
        }

        private static bool IsPredator(Diet diet)
        {
            Diet.Info[] infos = diet == null ? null : diet.preyInfos;
            if (infos == null)
            {
                return false;
            }
            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i].foodType == Diet.Info.FoodType.EatPrey && infos[i].consumedTags.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsLivePrey(KPrefabID peer, Diet diet)
        {
            Diet.Info[] infos = diet.preyInfos;
            for (int i = 0; i < infos.Length; i++)
            {
                Diet.Info info = infos[i];
                if (info.foodType != Diet.Info.FoodType.EatPrey)
                {
                    continue;
                }
                foreach (Tag tag in info.consumedTags)
                {
                    if (peer.HasTag(tag))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
