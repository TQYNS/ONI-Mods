using System;
using UnityEngine;

namespace MoreFood_KFC
{
    // 装配唯一入口（v1.7）

    internal static class CombatAssembly
    {
        internal static bool CanEquip(Clustercraft craft, string moduleId, out string reason)
        {
            RocketLoadout lo = RocketLoadout.Ensure(craft);
            if (lo == null)
            {
                reason = "火箭装配组件缺失";
                return false;
            }
            return lo.CanEquip(craft, moduleId, out reason);
        }

        internal static bool RequestEquip(Clustercraft craft, string moduleId)
        {
            string reason = null;
            RocketLoadout lo = RocketLoadout.Ensure(craft);
            if (lo == null || !lo.CanEquip(craft, moduleId, out reason))
            {
                TradeUi.Notify("装配失败", reason == null ? "未知原因" : reason);
                return false;
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            if (def == null)
            {
                TradeUi.Notify("装配失败", "未知模块：" + moduleId);
                return false;
            }

            // 1) 已送达池
            int at = lo.virtualStorage.IndexOf(moduleId);
            if (at >= 0)
            {
                lo.virtualStorage.RemoveAt(at);
                if (lo.Equip(craft, moduleId, out reason))
                {
                    return true;
                }
                lo.virtualStorage.Insert(at, moduleId);
                TradeUi.Notify("装配失败", reason == null ? "未知原因" : reason);
                return false;
            }

            // 2) 火箭货舱/内舱地面已有实物：收进控制台实物仓并装备
            ControlStationModuleReceiver receiver = FindReceiver(craft);
            if (!string.IsNullOrEmpty(def.prefabId)
                && RocketInventory.Consume(craft, new Tag(def.prefabId), 1f) >= 0.999f)
            {
                if (receiver != null)
                {
                    receiver.StoreModuleItem(def.prefabId);
                }
                if (lo.equippedModules.Contains(moduleId))
                {
                    return true;
                }
                lo.virtualStorage.Add(moduleId);
                if (lo.Equip(craft, moduleId, out reason))
                {
                    lo.virtualStorage.Remove(moduleId);
                    return true;
                }
                lo.virtualStorage.Remove(moduleId);
                TradeUi.Notify("装配失败", reason == null ? "未知原因" : reason);
                return false;
            }

            // 3) 不在船上：说清楚，不再发跨世界的搬运任务
            if (receiver == null)
            {
                TradeUi.Notify("装配失败", "改装锁定：需要火箭控制台");
                return false;
            }
            Debug.LogWarning("[Space] 装配失败（船上没有）：" + moduleId + " prefab=" + def.prefabId
                + " 货舱可用=" + RocketInventory.Available(craft, new Tag(def.prefabId)));
            TradeUi.Notify("装配失败", def.name + " 不在火箭货舱里，先买或让它送进火箭");
            return false;
        }

        internal static bool Unequip(Clustercraft craft, string moduleId)
        {
            string reason = null;
            RocketLoadout lo = RocketLoadout.Ensure(craft);
            if (lo == null || !lo.CanUnequip(craft, moduleId, out reason))
            {
                TradeUi.Notify("卸下失败", reason == null ? "未知原因" : reason);
                return false;
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            if (def == null)
            {
                return false;
            }
            ControlStationModuleReceiver receiver = FindReceiver(craft);
            if (receiver == null)
            {
                TradeUi.Notify("卸下失败", "改装锁定：需要火箭控制台");
                return false;
            }
            if (!lo.Unequip(craft, moduleId, out reason))
            {
                TradeUi.Notify("卸下失败", reason == null ? "未知原因" : reason);
                return false;
            }
            bool hadItem = receiver.RemoveModuleItem(moduleId);
            if (hadItem)
            {
                SpawnModuleItem(receiver.transform.GetPosition(), def);
            }
            return true;
        }

        internal static bool CanRemoveComponent(Clustercraft craft, string componentPrefabId, out string reason)
        {
            RocketLoadout lo = RocketLoadout.Get(craft);
            if (lo == null)
            {
                reason = "火箭装配组件缺失";
                return false;
            }
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st != null && st.inCombat)
            {
                reason = "战斗中禁止拆除组件";
                return false;
            }
            if (!lo.HasControlStation(craft))
            {
                reason = "改装锁定：需要火箭控制台";
                return false;
            }
            return lo.CanRemoveComponent(craft, componentPrefabId, out reason);
        }

        internal static void RecalcStats(Clustercraft craft)
        {
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st != null)
            {
                st.Recalc();
            }
        }

        internal static ControlStationModuleReceiver FindReceiver(Clustercraft craft)
        {
            WorldContainer w = RocketInventory.GetInterior(craft);
            if (w == null)
            {
                return null;
            }
            foreach (RocketControlStation s in Components.RocketControlStations)
            {
                if (s == null || ClusterUtil.GetMyWorldId(s) != w.id)
                {
                    continue;
                }
                ControlStationModuleReceiver r = s.GetComponent<ControlStationModuleReceiver>();
                if (r != null)
                {
                    return r;
                }
            }
            return null;
        }

        private static void SpawnModuleItem(Vector3 pos, ModuleDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.prefabId))
            {
                return;
            }
            GameObject prefab = Assets.TryGetPrefab(new Tag(def.prefabId));
            if (prefab == null)
            {
                Debug.LogWarning("[Space] 模块物品 prefab 缺失：" + def.prefabId);
                return;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                return;
            }
            go.transform.SetPosition(pos);
            go.SetActive(true);
        }
    }
}
