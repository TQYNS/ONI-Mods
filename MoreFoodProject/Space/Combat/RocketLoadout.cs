using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    // 火箭装配（导弹+模块 v1.7）

    internal sealed class SlotSource
    {
        internal string prefabId;
        internal int count;
        internal int capacity;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    internal sealed class RocketLoadout : KMonoBehaviour
    {
        [Serialize]
        internal string rocketUid;
        [Serialize]
        internal List<string> equippedModules = new List<string>();
        [Serialize]
        internal List<string> virtualStorage = new List<string>();
        [Serialize]
        internal List<string> weaponModuleIds = new List<string>();
        [Serialize]
        internal List<string> weaponPrimaryAmmoIds = new List<string>();
        [Serialize]
        internal List<string> weaponBackupAmmoIds = new List<string>();
        [Serialize]
        internal List<float> weaponAttackUntil = new List<float>();

        private bool synced;
        private bool slotsDirty = true;
        private readonly Dictionary<string, SlotEntry> slotCache =
            new Dictionary<string, SlotEntry>(StringComparer.Ordinal);

        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            SpaceLoadTrace.Set("RocketLoadout.OnSpawn 开始");
            synced = false;
            slotsDirty = true;
            Clustercraft craft = GetComponent<Clustercraft>();
            if (craft != null)
            {
                craft.Subscribe((int)GameHashes.RocketModuleChanged, OnModuleChanged);
            }
            if (CombatDb.built)
            {
                EnsureUid();
                Sanitize();
                RebuildWeapons();
                synced = true;
            }
            SpaceLoadTrace.Set("RocketLoadout.OnSpawn 完成");
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:RocketLoadout", e); }
        }

        protected override void OnCleanUp()
        {
            try
            {
            try
            {
                Clustercraft craft = GetComponent<Clustercraft>();
                if (craft != null)
                {
                    craft.Unsubscribe((int)GameHashes.RocketModuleChanged, OnModuleChanged);
                }
            }
            catch (Exception)
            {
                // expected: 销毁时退订模块变更事件失败可忽略，craft 与组件同时销毁不会留回调
            }
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:RocketLoadout", e); }
        }

        private void OnModuleChanged(object data)
        {
            slotsDirty = true;
        }

        internal void SyncIfNeeded()
        {
            if (synced || !CombatDb.built)
            {
                return;
            }
            EnsureUid();
            Sanitize();
            RebuildWeapons();
            synced = true;
        }

        internal static RocketLoadout Get(Clustercraft craft)
        {
            return craft != null ? craft.GetComponent<RocketLoadout>() : null;
        }

        internal static RocketLoadout Ensure(Clustercraft craft)
        {
            if (craft == null)
            {
                return null;
            }
            RocketLoadout lo = craft.GetComponent<RocketLoadout>();
            if (lo == null)
            {
                lo = craft.gameObject.AddOrGet<RocketLoadout>();
                Debug.LogWarning("[Space] 火箭 " + craft.Name
                    + " 缺 RocketLoadout 组件（prefab 模板注入未生效？已运行时补挂，字段会丢一次）");
            }
            return lo;
        }

        private void EnsureUid()
        {
            if (string.IsNullOrEmpty(rocketUid))
            {
                rocketUid = "rocket_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            }
        }

        /// <summary>清掉新设计里不存在的模块 ID（开发期切换；旧档不迁移）。</summary>
        private void Sanitize()
        {
            int removed = 0;
            for (int i = equippedModules.Count - 1; i >= 0; i--)
            {
                if (CombatDb.FindModule(equippedModules[i]) == null)
                {
                    equippedModules.RemoveAt(i);
                    removed++;
                }
            }
            for (int i = virtualStorage.Count - 1; i >= 0; i--)
            {
                if (CombatDb.FindModule(virtualStorage[i]) == null)
                {
                    virtualStorage.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
            {
                Debug.LogWarning("[Space] 火箭 " + rocketUid + " 清理了 " + removed
                    + " 个旧设计模块 ID（新档/新设计，不迁移）");
            }
        }

        // 槽位

        internal SlotEntry GetSlots(Clustercraft craft, string type)
        {
            if (!string.IsNullOrEmpty(type) && !slotsDirty)
            {
                SlotEntry cached;
                if (slotCache.TryGetValue(type, out cached))
                {
                    return cached;
                }
            }
            SlotEntry total = new SlotEntry { type = type, count = 0, capacity = 0 };
            List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
            for (int i = 0; i < mods.Count; i++)
            {
                RocketComponentSlots comp = CombatDb.FindComponent(mods[i].prefabId);
                if (comp == null)
                {
                    continue;
                }
                SlotEntry e;
                if (comp.slots.TryGetValue(type, out e))
                {
                    total.count += e.count;
                    total.capacity += e.capacity;
                }
            }
            if (!string.IsNullOrEmpty(type))
            {
                slotCache[type] = total;
            }
            return total;
        }

        internal List<SlotSource> GetSlotSources(Clustercraft craft, string type)
        {
            List<SlotSource> list = new List<SlotSource>();
            List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
            for (int i = 0; i < mods.Count; i++)
            {
                RocketComponentSlots comp = CombatDb.FindComponent(mods[i].prefabId);
                if (comp == null)
                {
                    continue;
                }
                SlotEntry e;
                if (!comp.slots.TryGetValue(type, out e))
                {
                    continue;
                }
                SlotSource s = new SlotSource();
                s.prefabId = mods[i].prefabId;
                s.count = e.count;
                s.capacity = e.capacity;
                list.Add(s);
            }
            return list;
        }

        internal void GetSlotUsed(string type, out int usedCount, out int usedCost)
        {
            usedCount = 0;
            usedCost = 0;
            for (int i = 0; i < equippedModules.Count; i++)
            {
                ModuleDef def = CombatDb.FindModule(equippedModules[i]);
                if (def == null || def.slotType != type)
                {
                    continue;
                }
                usedCount++;
                usedCost += def.slotCost;
            }
        }

        internal void InvalidateSlots()
        {
            slotsDirty = true;
            slotCache.Clear();
        }

        // 装 / 卸

        internal bool CanEquip(Clustercraft craft, string moduleId, out string reason)
        {
            reason = null;
            if (!CombatDb.built)
            {
                reason = "战斗配置未就绪";
                return false;
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            if (def == null)
            {
                reason = "未知模块：" + moduleId;
                return false;
            }
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st != null && st.inCombat)
            {
                reason = "战斗中禁止改装";
                return false;
            }
            if (!HasControlStation(craft))
            {
                reason = "改装锁定：需要火箭控制台";
                return false;
            }
            SlotEntry slot = GetSlots(craft, def.slotType);
            if (slot.count <= 0)
            {
                reason = SlotName(def.slotType) + "没有挂点（count=0，仅提供容量）";
                return false;
            }
            int usedCount, usedCost;
            GetSlotUsed(def.slotType, out usedCount, out usedCost);
            if (usedCount + 1 > slot.count)
            {
                reason = SlotName(def.slotType) + "槽已满（" + usedCount + "/" + slot.count + "）";
                return false;
            }
            if (usedCost + def.slotCost > slot.capacity)
            {
                reason = SlotName(def.slotType) + "容量不足：需要 " + def.slotCost
                    + "，剩余 " + (slot.capacity - usedCost) + "/" + slot.capacity;
                return false;
            }
            return true;
        }

        internal bool CanUnequip(Clustercraft craft, string moduleId, out string reason)
        {
            reason = null;
            if (equippedModules.IndexOf(moduleId) < 0)
            {
                reason = "没装这个模块";
                return false;
            }
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st != null && st.inCombat)
            {
                reason = "战斗中禁止改装";
                return false;
            }
            if (!HasControlStation(craft))
            {
                reason = "改装锁定：需要火箭控制台";
                return false;
            }
            return true;
        }

        // 槽位缩水后把装不下的模块卸下（保留集按容量最大 + 优先留大），返回被卸下的模块 ID（C-45）
        internal List<string> EvictToFit(Clustercraft craft)
        {
            List<string> gone = new List<string>();
            if (!CombatDb.built)
            {
                return gone;
            }
            InvalidateSlots();
            string[] types = { "weapon", "defense", "utility", "sensor" };
            for (int i = 0; i < types.Length; i++)
            {
                EvictType(craft, types[i], gone);
            }
            if (gone.Count == 0)
            {
                return gone;
            }
            // 掉落清单已定，状态重算出错也不能影响调用方取件（C-45）
            try
            {
                RebuildWeapons();
                InvalidateSlots();
                RocketCombatState st = RocketCombatState.Ensure(craft);
                if (st != null)
                {
                    st.Recalc();
                    CombatBars.PushCraft(craft, st);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 拆舱后重算战斗状态失败（掉落照常）");
            }
            return gone;
        }

        private void EvictType(Clustercraft craft, string type, List<string> gone)
        {
            SlotEntry slot = GetSlots(craft, type);
            int cap = Mathf.Max(0, slot.capacity);
            int limit = Mathf.Max(0, slot.count);
            List<int> index = new List<int>();
            List<int> cost = new List<int>();
            int sumCost = 0;
            for (int i = 0; i < equippedModules.Count; i++)
            {
                ModuleDef def = CombatDb.FindModule(equippedModules[i]);
                if (def == null || def.slotType != type)
                {
                    continue;
                }
                int c = Mathf.Max(1, def.slotCost);
                index.Add(i);
                cost.Add(c);
                sumCost += c;
            }
            int n = index.Count;
            if (n == 0 || (cap >= sumCost && n <= limit))
            {
                return;
            }
            // 从小到大排：回溯从后往前挑，等价于优先保留大的
            List<int> order = new List<int>();
            for (int i = 0; i < n; i++)
            {
                order.Add(i);
            }
            order.Sort(delegate (int a, int b)
            {
                int d = cost[a] - cost[b];
                return d != 0 ? d : index[a] - index[b];
            });
            int width = Mathf.Min(cap, sumCost);
            int[,] f = new int[n + 1, width + 1];
            for (int i = 1; i <= n; i++)
            {
                int c = cost[order[i - 1]];
                for (int w = 0; w <= width; w++)
                {
                    f[i, w] = f[i - 1, w];
                    if (c <= w && f[i - 1, w - c] + c > f[i, w])
                    {
                        f[i, w] = f[i - 1, w - c] + c;
                    }
                }
            }
            bool[] keep = new bool[n];
            int room = width;
            for (int i = n; i >= 1; i--)
            {
                int c = cost[order[i - 1]];
                if (room >= c && f[i, room] == f[i - 1, room - c] + c)
                {
                    keep[order[i - 1]] = true;
                    room -= c;
                }
            }
            // 再压挂点数量：留下的里面从最小的开始丢
            List<int> kept = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (keep[i])
                {
                    kept.Add(i);
                }
            }
            kept.Sort(delegate (int a, int b)
            {
                int d = cost[a] - cost[b];
                return d != 0 ? d : index[a] - index[b];
            });
            while (kept.Count > limit)
            {
                keep[kept[0]] = false;
                kept.RemoveAt(0);
            }
            List<int> drop = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!keep[i])
                {
                    drop.Add(index[i]);
                }
            }
            drop.Sort();
            for (int i = drop.Count - 1; i >= 0; i--)
            {
                gone.Add(equippedModules[drop[i]]);
                equippedModules.RemoveAt(drop[i]);
            }
        }

        internal bool Equip(Clustercraft craft, string moduleId, out string reason)
        {
            if (!CanEquip(craft, moduleId, out reason))
            {
                return false;
            }
            equippedModules.Add(moduleId);
            RebuildWeapons();
            InvalidateSlots();
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st != null)
            {
                st.Recalc();
                CombatBars.PushCraft(craft, st);
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            MFLog.Info(LogCat.Space, "[Space] 火箭 " + (craft != null ? craft.Name : "?") + " 装上模块 "
                + (def != null ? def.name : moduleId) + "（" + moduleId + "）");
            slotsDirty = true;
            return true;
        }

        internal bool Unequip(Clustercraft craft, string moduleId, out string reason)
        {
            if (!CanUnequip(craft, moduleId, out reason))
            {
                return false;
            }
            equippedModules.Remove(moduleId);
            RebuildWeapons();
            InvalidateSlots();
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st != null)
            {
                st.Recalc();
                CombatBars.PushCraft(craft, st);
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            MFLog.Info(LogCat.Space, "[Space] 火箭 " + (craft != null ? craft.Name : "?") + " 卸下模块 "
                + (def != null ? def.name : moduleId) + "（" + moduleId + "）");
            return true;
        }

        internal bool HasControlStation(Clustercraft craft)
        {
            WorldContainer w = RocketInventory.GetInterior(craft);
            if (w == null)
            {
                return false;
            }
            foreach (RocketControlStation s in Components.RocketControlStations)
            {
                if (s != null && ClusterUtil.GetMyWorldId(s) == w.id)
                {
                    return true;
                }
            }
            return false;
        }

        internal static string SlotName(string type)
        {
            switch (type)
            {
                case "weapon": return "武器";
                case "defense": return "防御";
                case "utility": return "辅助";
                case "sensor": return "感知";
                default: return type;
            }
        }

        // 武器实例

        internal void RebuildWeapons()
        {
            if (!CombatDb.built)
            {
                return;
            }
            Dictionary<string, string> oldPrimary = new Dictionary<string, string>();
            Dictionary<string, string> oldBackup = new Dictionary<string, string>();
            Dictionary<string, float> oldUntil = new Dictionary<string, float>();
            Dictionary<string, int> occ = new Dictionary<string, int>();
            for (int i = 0; i < weaponModuleIds.Count; i++)
            {
                string id = weaponModuleIds[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                int n;
                occ.TryGetValue(id, out n);
                occ[id] = n + 1;
                string key = id + "#" + n;
                oldPrimary[key] = i < weaponPrimaryAmmoIds.Count ? weaponPrimaryAmmoIds[i] : null;
                oldBackup[key] = i < weaponBackupAmmoIds.Count ? weaponBackupAmmoIds[i] : null;
                oldUntil[key] = i < weaponAttackUntil.Count ? weaponAttackUntil[i] : 0f;
            }

            List<string> ids = new List<string>();
            List<string> primary = new List<string>();
            List<string> backup = new List<string>();
            List<float> until = new List<float>();
            occ.Clear();
            for (int i = 0; i < equippedModules.Count; i++)
            {
                string id = equippedModules[i];
                ModuleDef def = CombatDb.FindModule(id);
                if (def == null || def.slotType != "weapon")
                {
                    continue;
                }
                int n;
                occ.TryGetValue(id, out n);
                occ[id] = n + 1;
                string key = id + "#" + n;
                string p, b;
                float u;
                oldPrimary.TryGetValue(key, out p);
                oldBackup.TryGetValue(key, out b);
                oldUntil.TryGetValue(key, out u);
                ids.Add(id);
                primary.Add(string.IsNullOrEmpty(p) ? "auto" : p);
                backup.Add(b);
                until.Add(u);
            }
            weaponModuleIds = ids;
            weaponPrimaryAmmoIds = primary;
            weaponBackupAmmoIds = backup;
            weaponAttackUntil = until;
        }

        internal void SetWeaponAmmo(int weaponIndex, string primary, string backup)
        {
            if (weaponIndex < 0 || weaponIndex >= weaponPrimaryAmmoIds.Count)
            {
                return;
            }
            weaponPrimaryAmmoIds[weaponIndex] = string.IsNullOrEmpty(primary) ? "auto" : primary;
            weaponBackupAmmoIds[weaponIndex] = string.IsNullOrEmpty(backup) ? null : backup;
            if (weaponPrimaryAmmoIds[weaponIndex] != "auto"
                && weaponPrimaryAmmoIds[weaponIndex] == weaponBackupAmmoIds[weaponIndex])
            {
                weaponBackupAmmoIds[weaponIndex] = null;
            }
        }

        /// <summary>全火箭聚合：穿甲/速度/雷达/再生；武器/防御按槽位分别取。</summary>
        internal void SumStats(out float armorPenAdd, out float armorDamageMul, out float speedMul,
            out int radarRangeBonus, out int scoutingBonus,
            out float hpRegenPct, out float shieldRegenPct)
        {
            armorPenAdd = 0f;
            armorDamageMul = 1f;
            speedMul = 1f;
            radarRangeBonus = 0;
            scoutingBonus = 0;
            hpRegenPct = 0f;
            shieldRegenPct = 0f;
            for (int i = 0; i < equippedModules.Count; i++)
            {
                ModuleDef def = CombatDb.FindModule(equippedModules[i]);
                if (def == null)
                {
                    continue;
                }
                if (def.slotType == "utility")
                {
                    armorPenAdd += def.stats.armorPenAdd;
                    armorDamageMul *= def.stats.armorDamageMul;
                    speedMul *= def.stats.speedMul;
                }
                else if (def.slotType == "sensor")
                {
                    radarRangeBonus += def.stats.rangeBonus;
                    scoutingBonus += def.stats.scoutingBonus;
                }
                else if (def.slotType == "defense")
                {
                    hpRegenPct += def.stats.hpRegenPctPerTick;
                    shieldRegenPct += def.stats.shieldRegenPctPerTick;
                }
            }
        }
    }
}
