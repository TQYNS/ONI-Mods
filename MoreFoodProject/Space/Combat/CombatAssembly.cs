using System;
using System.Collections.Generic;
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

        internal static bool CanRemoveComponent(Clustercraft craft, RocketModuleCluster target, out string reason)
        {
            reason = null;
            if (target == null)
            {
                reason = "找不到要拆除的舱段";
                return false;
            }
            // 原版照常拆、材料原地掉；槽位不足不拦，装不下的模块在拆完后原地掉落（C-45）
            RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
            if (st != null && st.inCombat)
            {
                reason = "战斗中禁止拆除组件";
                return false;
            }
            return true;
        }

        // 舱段拆掉后槽位缩水：装不下的模块原地掉落，保留集按「容量最大且优先留大」挑（C-45）
        internal static void EvictOverflow(Clustercraft craft, Vector3 pos)
        {
            try
            {
                RocketLoadout lo = RocketLoadout.Get(craft);
                if (lo == null)
                {
                    return;
                }
                List<string> gone = lo.EvictToFit(craft);
                if (gone == null || gone.Count == 0)
                {
                    return;
                }
                ControlStationModuleReceiver receiver = FindReceiver(craft);
                string names = "";
                for (int i = 0; i < gone.Count; i++)
                {
                    ModuleDef def = CombatDb.FindModule(gone[i]);
                    try
                    {
                        bool hadItem = receiver != null && receiver.RemoveModuleItem(gone[i]);
                        // 与手动卸下同口径：拿到实物才掉；控制台已没了（实物随之销毁）则补一个
                        if (hadItem || receiver == null)
                        {
                            SpawnModuleItem(pos, def);
                        }
                    }
                    catch (Exception e)
                    {
                        // 单件失败不拖累整批：其余模块照常掉
                        MFLog.Exc(LogCat.Combat, e, "[Space] 掉落模块失败：" + gone[i]);
                    }
                    names += (i > 0 ? "、" : "") + (def != null ? def.name : gone[i]);
                }
                TradeUi.NotifyInWorld(ClusterUtil.GetMyWorldId(craft), "舱段拆除：模块掉落",
                    "槽位不足，" + gone.Count + " 个模块已掉落在原地：" + names);
                MFLog.Info(LogCat.Combat, "[Space] 拆除后槽位不足，原地掉落 " + gone.Count + " 个模块：" + names);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 掉落超出槽位的模块失败");
            }
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
