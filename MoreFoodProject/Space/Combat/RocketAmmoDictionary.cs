using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 火箭弹药字典（§12.5 + K6：每 Tick 建一次，规模小开销可忽略）

    internal static class RocketAmmoDictionary
    {
        internal static Dictionary<string, float> Build(Clustercraft craft)
        {
            Dictionary<string, float> dict = new Dictionary<string, float>();
            if (craft == null)
            {
                return dict;
            }
            List<RocketHold> holds = RocketInventory.GetHolds(craft);
            for (int i = 0; i < holds.Count; i++)
            {
                RocketHold hold = holds[i];
                if (hold == null || hold.storage == null)
                {
                    continue;
                }
                List<GameObject> items = null;
                try
                {
                    items = hold.storage.GetItems();
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] 枚举货舱物品失败");
                }
                AddItems(dict, items);
            }
            // 除标准货舱外，火箭各模块自带的 Storage（引擎/罐/接收器等）也扫一遍。
            try
            {
                IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface != null
                    ? craft.ModuleInterface.ClusterModules : null;
                if (mods != null)
                {
                    for (int i = 0; i < mods.Count; i++)
                    {
                        RocketModuleCluster m = mods[i].Get();
                        if (m == null)
                        {
                            continue;
                        }
                        Storage[] sts = m.GetComponents<Storage>();
                        if (sts == null)
                        {
                            continue;
                        }
                        for (int s = 0; s < sts.Length; s++)
                        {
                            if (sts[s] == null)
                            {
                                continue;
                            }
                            List<GameObject> items = null;
                            try { items = sts[s].GetItems(); }
                            catch (Exception) { continue; }
                            AddItems(dict, items);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 扫描模块 Storage 弹药失败");
            }
            WorldContainer w = RocketInventory.GetInterior(craft);
            if (w != null)
            {
                List<Pickupable> floor = Components.Pickupables.GetWorldItems(w.id, false);
                if (floor != null)
                {
                    for (int i = 0; i < floor.Count; i++)
                    {
                        Pickupable p = floor[i];
                        if (p == null || p.gameObject == null)
                        {
                            continue;
                        }
                        AddItem(dict, p.gameObject);
                    }
                }
            }
            return dict;
        }

        private static void AddItems(Dictionary<string, float> dict, List<GameObject> items)
        {
            if (items == null)
            {
                return;
            }
            for (int k = 0; k < items.Count; k++)
            {
                AddItem(dict, items[k]);
            }
        }

        private static void AddItem(Dictionary<string, float> dict, GameObject item)
        {
            if (item == null)
            {
                return;
            }
            PrimaryElement pe = item.GetComponent<PrimaryElement>();
            KPrefabID pid = item.GetComponent<KPrefabID>();
            if (pe == null || pid == null || pe.Mass <= 0f)
            {
                return;
            }
            string key = pid.PrefabTag.Name;
            if (CombatDb.FindAmmoByPrefab(key) == null)
            {
                return;
            }
            float acc;
            dict.TryGetValue(key, out acc);
            dict[key] = acc + pe.Mass;
        }

        internal static bool IsAmmoSource(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return false;
            }
            string[] list = CombatDb.Shared.ammoSourcePrefabs;
            if (list == null || list.Length == 0)
            {
                return true;   // 没配白名单 = 所有货舱都算弹药源
            }
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == prefabId)
                {
                    return true;
                }
            }
            return false;
        }

        internal static float AvailableKg(Clustercraft craft, string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return 0f;
            }
            Dictionary<string, float> dict = Build(craft);
            float kg;
            return dict.TryGetValue(prefabId, out kg) ? kg : 0f;
        }
    }
}
