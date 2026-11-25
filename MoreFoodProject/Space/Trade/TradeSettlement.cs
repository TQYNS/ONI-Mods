using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal sealed class RocketHold
    {
        internal RocketModuleCluster module;
        internal Storage storage;
        internal string prefabId;
        internal CargoBay.CargoType? cargoType;
    }

    internal sealed class RocketModuleInfo
    {
        internal RocketModuleCluster module;
        internal string prefabId;
    }

    internal static class RocketInventory
    {
        internal const float MinMass = 0.0001f;

        internal static List<RocketHold> GetHolds(Clustercraft craft)
        {
            List<RocketHold> holds = new List<RocketHold>();
            if (craft == null)
            {
                return holds;
            }
            CraftModuleInterface mi = craft.ModuleInterface;
            if (mi == null)
            {
                return holds;
            }
            IList<Ref<RocketModuleCluster>> mods = mi.ClusterModules;
            if (mods == null)
            {
                return holds;
            }
            for (int i = 0; i < mods.Count; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null)
                {
                    continue;
                }
                Storage s = null;
                CargoBay.CargoType? type = null;
                CargoBayCluster bay = m.GetComponent<CargoBayCluster>();
                if (bay != null && bay.storage != null)
                {
                    s = bay.storage;
                    type = bay.storageType;
                }
                else
                {
                    CargoBay cargo = m.GetComponent<CargoBay>();
                    if (cargo != null && cargo.storage != null)
                    {
                        s = cargo.storage;
                        type = cargo.storageType;
                    }
                }
                if (s == null)
                {
                    continue;
                }
                RocketHold h = new RocketHold();
                h.module = m;
                h.storage = s;
                h.cargoType = type;
                KPrefabID pid = m.GetComponent<KPrefabID>();
                h.prefabId = pid != null ? pid.PrefabTag.Name : m.name;
                holds.Add(h);
            }
            return holds;
        }

        internal static List<RocketModuleInfo> GetModules(Clustercraft craft)
        {
            List<RocketModuleInfo> all = new List<RocketModuleInfo>();
            if (craft == null || craft.ModuleInterface == null)
            {
                return all;
            }
            IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
            if (mods == null)
            {
                return all;
            }
            for (int i = 0; i < mods.Count; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null)
                {
                    continue;
                }
                RocketModuleInfo info = new RocketModuleInfo();
                info.module = m;
                KPrefabID pid = m.GetComponent<KPrefabID>();
                info.prefabId = pid != null ? pid.PrefabTag.Name : m.name;
                all.Add(info);
            }
            return all;
        }

        internal static WorldContainer GetInterior(Clustercraft craft)
        {
            if (craft == null || craft.ModuleInterface == null)
            {
                return null;
            }
            try
            {
                return craft.ModuleInterface.GetInteriorWorld();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 取火箭内舱世界失败");
                return null;
            }
        }

        /// <summary>
        /// ★ 计量口径（本轮核心修复）：每「份」的质量。
        /// 批量物资（食物、材料）按 kg 计 ⇒ 1kg = 1 份；单件物品（工艺品/模块/弹药）按 prefab 自身的
        /// 单位质量计 ⇒ 工艺品 25kg = 1 份。旧实现全链路把 PrimaryElement.Mass 当份数用，
        /// 于是 2 件工艺品（2×25kg）被显示成「持有 50」，卖出 20 时还会把一件工艺品的质量切掉 20kg。
        /// </summary>
        internal static float UnitMass(Tag tag)
        {
            GameObject prefab = Assets.TryGetPrefab(tag);
            PrimaryElement pe = prefab != null ? prefab.GetComponent<PrimaryElement>() : null;
            float m = pe != null ? pe.Mass : 0f;
            return m > MinMass ? m : 1f;
        }

        /// <summary>该 Tag 是否以「份」而非 kg 计量（工艺品 / 模块 / 弹药）。</summary>
        internal static bool CountsByPiece(Tag tag)
        {
            return IsArtifact(tag) || IsDiscretePrefab(tag);
        }

        /// <summary>
        /// 是否必须「整件」扣取/发放：单件物品且单位质量 ≠ 1kg（如工艺品 25kg/件）。
        /// 1kg/件 的模块与弹药数值上与 kg 等价，保持原有按 kg 逻辑，避免波及战斗侧扣弹口径。
        /// </summary>
        internal static bool TakesWholePieces(Tag tag)
        {
            return CountsByPiece(tag) && UnitMass(tag) > 1.001f;
        }

        /// <summary>质量 → 份数（整份，向下取整，避免半件被凑成一件）；批量物资原样返回 kg。</summary>
        internal static float PieceCount(Tag tag, float mass)
        {
            if (!CountsByPiece(tag))
            {
                return mass;
            }
            float um = UnitMass(tag);
            return um > MinMass ? Mathf.Floor(mass / um + 0.01f) : mass;
        }

        /// <summary>份数 → 质量（结算/退款时换算回 kg）。</summary>
        internal static float PieceMass(Tag tag, float pieces)
        {
            if (!CountsByPiece(tag))
            {
                return pieces;
            }
            return pieces * UnitMass(tag);
        }

        internal static float AvailableInHolds(Clustercraft craft, Tag tag)
        {
            float sum = 0f;
            List<RocketHold> holds = GetHolds(craft);
            // ★ 计量口径：单件物品按「件」数，不用 GetAmountAvailable 的 Units（其定义随 MassPerUnit 浮动）
            bool byPiece = CountsByPiece(tag);
            for (int i = 0; i < holds.Count; i++)
            {
                Storage st = holds[i].storage;
                if (st == null)
                {
                    continue;
                }
                if (!byPiece)
                {
                    sum += st.GetAmountAvailable(tag);
                    continue;
                }
                List<GameObject> items = null;
                try
                {
                    items = st.GetItems();
                }
                catch (Exception)
                {
                    continue;
                }
                if (items == null)
                {
                    continue;
                }
                for (int k = 0; k < items.Count; k++)
                {
                    GameObject it = items[k];
                    if (it != null && it.HasTag(tag))
                    {
                        sum += 1f;
                    }
                }
            }
            return sum;
        }

        /// <summary>
        internal static int InteriorDropCell(Clustercraft craft)
        {
            WorldContainer w = GetInterior(craft);
            if (w == null)
            {
                return Grid.InvalidCell;
            }
            // 1) 控制台所在格
            try
            {
                RocketControlStation station = FindControlStation(craft);
                if (station != null && station.gameObject != null)
                {
                    int c = Grid.PosToCell(station.gameObject);
                    if (Grid.IsValidCell(c) && Grid.WorldIdx[c] == w.id)
                    {
                        return c;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 控制站探测失败，继续尝试复制人站位
            }
            // 2) 内舱里复制人/仿生人的站立格
            try
            {
                List<MinionIdentity> ids = Components.LiveMinionIdentities.GetWorldItems(w.id, false);
                if (ids != null)
                {
                    for (int i = 0; i < ids.Count; i++)
                    {
                        MinionIdentity mi = ids[i];
                        if (mi == null || mi.gameObject == null)
                        {
                            continue;
                        }
                        int c = Grid.PosToCell(mi.gameObject);
                        if (Grid.IsValidCell(c) && Grid.WorldIdx[c] == w.id)
                        {
                            return c;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // expected: 复制人站位探测失败，回退到世界安全格
            }
            // 3) 兜底
            return w.GetSafeCell();
        }

        private static RocketControlStation FindControlStation(Clustercraft craft)
        {
            try
            {
                if (craft != null && craft.ModuleInterface != null)
                {
                    IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
                    if (mods != null)
                    {
                        for (int i = 0; i < mods.Count; i++)
                        {
                            RocketModuleCluster m = mods[i].Get();
                            if (m == null)
                            {
                                continue;
                            }
                            RocketControlStation cs = m.GetComponent<RocketControlStation>();
                            if (cs != null)
                            {
                                return cs;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // expected: 模块接口探测失败，回退到全局控制站查找
            }
            try
            {
                WorldContainer w = GetInterior(craft);
                if (w == null)
                {
                    return null;
                }
                foreach (RocketControlStation s in Components.RocketControlStations)
                {
                    if (s == null || s.gameObject == null)
                    {
                        continue;
                    }
                    int c = Grid.PosToCell(s.gameObject);
                    if (Grid.IsValidCell(c) && Grid.WorldIdx[c] == w.id)
                    {
                        return s;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 全局控制站扫描失败按找不到处理
            }
            return null;
        }

        /// <summary>把一份/一堆物品直接落到火箭内部（落点见 InteriorDropCell）。</summary>
        internal static bool SpawnInInterior(Clustercraft craft, string label, GameObject prefab,
            float mass, float temperature)
        {
            int cell = InteriorDropCell(craft);
            if (!Grid.IsValidCell(cell))
            {
                MFLog.Warn(LogCat.Trade, "[Space] 火箭内舱没有可用落点，" + label + " 入库失败");
                return false;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                return false;
            }
            PrimaryElement pe = go.GetComponent<PrimaryElement>();
            if (pe != null)
            {
                pe.Mass = mass;
                pe.Temperature = temperature;
            }
            go.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore));
            go.SetActive(true);
            return true;
        }

        /// <summary>
        internal static bool DepositForTrade(Clustercraft craft, Tag tag, float amount,
            float temperature, out string reason)
        {
            reason = null;
            GameObject prefab = Assets.TryGetPrefab(tag);
            if (prefab == null)
            {
                reason = "找不到物品 " + tag.Name;
                return false;
            }
            // ★ 计量口径：工艺品按「件」入舱，每件都是完整单位质量（25kg），不按 kg 切
            if (IsArtifact(tag))
            {
                float um = UnitMass(tag);
                int pieces = Mathf.Max(1, Mathf.FloorToInt(amount + 0.001f));
                int done = 0;
                for (int n = 0; n < pieces; n++)
                {
                    if (DepositArtifact(craft, tag, temperature)
                        || SpawnInInterior(craft, tag.Name, prefab, um, temperature))
                    {
                        done++;
                        continue;
                    }
                    // 放不下：把本轮已入舱的整件回滚，避免"扣了钱只拿到一部分"
                    if (done > 0)
                    {
                        ConsumePieces(craft, tag, done);
                    }
                    reason = "火箭里放不下这件工艺品（无运输仓且内舱没有落脚点）";
                    return false;
                }
                return true;
            }
            if (IsDiscretePrefab(tag))
            {
                int count = Mathf.FloorToInt(amount + 0.001f);
                if (count <= 0)
                {
                    reason = TradeUtil.DisplayName(tag.Name) + " 不足 1 个，无法入库";
                    return false;
                }
                float unit = UnitMass(tag);
                for (int n = 0; n < count; n++)
                {
                    if (!SpawnInInterior(craft, tag.Name, prefab, unit, temperature))
                    {
                        reason = "火箭内舱放不下 " + TradeUtil.DisplayName(tag.Name);
                        return false;
                    }
                }
                return true;
            }
            if (amount <= 0f)
            {
                reason = "数量为 0";
                return false;
            }
            if (!SpawnInInterior(craft, tag.Name, prefab, amount, temperature))
            {
                reason = "火箭内舱放不下 " + TradeUtil.DisplayName(tag.Name);
                return false;
            }
            return true;
        }

        internal static float AvailableOnFloor(Clustercraft craft, Tag tag)
        {
            WorldContainer w = GetInterior(craft);
            if (w == null)
            {
                return 0f;
            }
            float sum = 0f;
            List<Pickupable> items = Components.Pickupables.GetWorldItems(w.id, false);
            if (items == null)
            {
                return 0f;
            }
            for (int i = 0; i < items.Count; i++)
            {
                Pickupable p = items[i];
                if (p == null)
                {
                    continue;
                }
                // ★ 修订（持有量虚高 25/50）：只要物品在任意 Storage 里（陈列架、储物箱、冰箱、
                //   模块自带仓库……）就不是"可搬运的散落物"，不能计入可交易持有量。
                //   旧实现遍历内舱全部 Pickupable，把陈列架上的陈列品也算成货，清空内舱后数字才回落。
                if (p.storage != null)
                {
                    continue;
                }
                if (p.gameObject == null || !p.gameObject.activeInHierarchy)
                {
                    continue;
                }
                KPrefabID pid = p.GetComponent<KPrefabID>();
                // 双保险：部分"已被收进建筑"的物品 storage 字段可能滞后，但会带 Stored 标记
                if (pid != null && pid.HasTag(GameTags.Stored))
                {
                    continue;
                }
                if (pid == null || !pid.HasTag(tag))
                {
                    continue;
                }
                PrimaryElement pe = p.GetComponent<PrimaryElement>();
                if (pe != null && pe.Mass > 0f)
                {
                    // ★ 计量口径：单件物品按整件计（25kg 工艺品 = 1 份），批量物资仍按 kg（= 份）
                    sum += PieceCount(tag, pe.Mass);
                }
            }
            return sum;
        }

        /// <summary>
        /// ★ 模块自带 Storage（工艺品运输仓等）里的件数。货舱已由 GetHolds 覆盖，这里跳过货舱模块，
        /// 避免同一份物品被算两次。仅单件物品需要（工艺品买回来是进运输仓的，旧实现看不见 ⇒ 持有 0 卖不掉）。
        /// </summary>
        internal static float AvailableInExtraStorage(Clustercraft craft, Tag tag)
        {
            if (craft == null || craft.ModuleInterface == null)
            {
                return 0f;
            }
            IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
            if (mods == null)
            {
                return 0f;
            }
            float sum = 0f;
            for (int i = 0; i < mods.Count; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null || m.GetComponent<CargoBayCluster>() != null || m.GetComponent<CargoBay>() != null)
                {
                    continue;
                }
                Storage[] sts = m.GetComponents<Storage>();
                if (sts == null)
                {
                    continue;
                }
                for (int k = 0; k < sts.Length; k++)
                {
                    Storage st = sts[k];
                    if (st == null)
                    {
                        continue;
                    }
                    List<GameObject> items = null;
                    try { items = st.GetItems(); }
                    catch (Exception) { continue; }
                    if (items == null)
                    {
                        continue;
                    }
                    for (int n = 0; n < items.Count; n++)
                    {
                        GameObject it = items[n];
                        if (it != null && it.HasTag(tag))
                        {
                            sum += 1f;
                        }
                    }
                }
            }
            return sum;
        }

        internal static float Available(Clustercraft craft, Tag tag)
        {
            float sum = AvailableInHolds(craft, tag) + AvailableOnFloor(craft, tag);
            // ★ 单件物品：运输仓里的也算可交易（买入即入仓，必须能再卖出）
            //   仅限整件扣取的物品（单位质量 > 1kg），与 ConsumePieces 的扫描范围严格一致
            if (TakesWholePieces(tag))
            {
                sum += AvailableInExtraStorage(craft, tag);
            }
            return sum;
        }

        /// <summary>★ 第十八轮-5：模块实物的"全仓"可用量 —— 货舱 + 内舱地面 + 各模块自带 Storage。
        internal static float AvailableAnywhere(Clustercraft craft, Tag tag)
        {
            float sum = Available(craft, tag);
            try
            {
                System.Collections.Generic.IList<Ref<RocketModuleCluster>> mods =
                    craft != null && craft.ModuleInterface != null
                        ? craft.ModuleInterface.ClusterModules : null;
                if (mods == null)
                {
                    return sum;
                }
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
                        if (items == null)
                        {
                            continue;
                        }
                        for (int k = 0; k < items.Count; k++)
                        {
                            GameObject it = items[k];
                            if (it == null)
                            {
                                continue;
                            }
                            KPrefabID pid = it.GetComponent<KPrefabID>();
                            if (pid == null || !pid.HasTag(tag))
                            {
                                continue;
                            }
                            PrimaryElement pe = it.GetComponent<PrimaryElement>();
                            // ★ 计量口径：与 AvailableInHolds 一致 —— 单件物品按件数，其余按份(=kg)
                            sum += PieceCount(tag, pe != null ? pe.Mass : 1f);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 模块 Storage 实物统计失败");
            }
            return sum;
        }

        // 火箭现有物品清单（货舱 + 内舱地面）：prefabId → 总质量。
        internal static Dictionary<string, float> Snapshot(Clustercraft craft)
        {
            Dictionary<string, float> map = new Dictionary<string, float>(StringComparer.Ordinal);
            if (craft == null)
            {
                return map;
            }
            List<RocketHold> holds = GetHolds(craft);
            for (int i = 0; i < holds.Count; i++)
            {
                Storage s = holds[i].storage;
                if (s == null)
                {
                    continue;
                }
                List<GameObject> items = null;
                try
                {
                    items = s.GetItems();
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Trade, e, "[Space] 枚举货舱物品失败");
                }
                if (items == null)
                {
                    continue;
                }
                for (int k = 0; k < items.Count; k++)
                {
                    GameObject go = items[k];
                    if (go == null)
                    {
                        continue;
                    }
                    KPrefabID pid = go.GetComponent<KPrefabID>();
                    if (pid == null)
                    {
                        continue;
                    }
                    PrimaryElement pe = go.GetComponent<PrimaryElement>();
                    float mass = pe != null ? pe.Mass : 0f;
                    if (mass <= MinMass)
                    {
                        continue;
                    }
                    string id = pid.PrefabTag.Name;
                    // ★ 计量口径：快照同样以「份」为单位（工艺品按件、其余按 kg），调用方无需再换算
                    float qty = PieceCount(pid.PrefabTag, mass);
                    if (qty <= MinMass)
                    {
                        continue;
                    }
                    float old;
                    map[id] = map.TryGetValue(id, out old) ? old + qty : qty;
                }
            }
            // ★ 单件物品：模块自带 Storage（工艺品运输仓等）里的也要进快照，
            //   否则买回来的工艺品在交易列表里看不到、卖不回去。只收单件物品，批量物资不受影响。
            if (craft.ModuleInterface != null)
            {
                IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
                if (mods != null)
                {
                    for (int i = 0; i < mods.Count; i++)
                    {
                        RocketModuleCluster m = mods[i].Get();
                        if (m == null || m.GetComponent<CargoBayCluster>() != null || m.GetComponent<CargoBay>() != null)
                        {
                            continue;
                        }
                        Storage[] sts = m.GetComponents<Storage>();
                        if (sts == null)
                        {
                            continue;
                        }
                        for (int k = 0; k < sts.Length; k++)
                        {
                            Storage st = sts[k];
                            if (st == null)
                            {
                                continue;
                            }
                            List<GameObject> items = null;
                            try { items = st.GetItems(); }
                            catch (Exception) { continue; }
                            if (items == null)
                            {
                                continue;
                            }
                            for (int n = 0; n < items.Count; n++)
                            {
                                GameObject it = items[n];
                                if (it == null)
                                {
                                    continue;
                                }
                                KPrefabID pid = it.GetComponent<KPrefabID>();
                                if (pid == null || !TakesWholePieces(pid.PrefabTag))
                                {
                                    continue;
                                }
                                string id = pid.PrefabTag.Name;
                                float old;
                                map[id] = map.TryGetValue(id, out old) ? old + 1f : 1f;
                            }
                        }
                    }
                }
            }
            WorldContainer w = GetInterior(craft);
            if (w != null)
            {
                List<Pickupable> floor = Components.Pickupables.GetWorldItems(w.id, false);
                if (floor != null)
                {
                    for (int i = 0; i < floor.Count; i++)
                    {
                        Pickupable p = floor[i];
                        if (p == null)
                        {
                            continue;
                        }
                        // ★ 修订：与 AvailableOnFloor / Consume 同口径 —— 只统计"散落可搬运"的物品，
                        //   陈列架/储物箱里的不算，否则会凭空多出可卖条目。
                        if (p.storage != null || p.gameObject == null || !p.gameObject.activeInHierarchy)
                        {
                            continue;
                        }
                        KPrefabID pid = p.GetComponent<KPrefabID>();
                        if (pid == null || pid.HasTag(GameTags.Stored))
                        {
                            continue;
                        }
                        PrimaryElement pe = p.GetComponent<PrimaryElement>();
                        if (pid == null || pe == null || pe.Mass <= MinMass)
                        {
                            continue;
                        }
                        string id = pid.PrefabTag.Name;
                        float qty = PieceCount(pid.PrefabTag, pe.Mass);
                        if (qty <= MinMass)
                        {
                            continue;
                        }
                        float old;
                        map[id] = map.TryGetValue(id, out old) ? old + qty : qty;
                    }
                }
            }
            return map;
        }

        internal static float Consume(Clustercraft craft, Tag tag, float amount)
        {
            // ★ 计量口径：单件物品按「件」整件扣，绝不切分单件质量（1kg/件 的弹药/模块仍按 kg，数值等价）
            if (TakesWholePieces(tag))
            {
                return ConsumePieces(craft, tag, amount);
            }
            float left = amount;
            List<RocketHold> holds = GetHolds(craft);
            for (int i = 0; i < holds.Count && left > MinMass; i++)
            {
                float have = holds[i].storage.GetAmountAvailable(tag);
                if (have <= MinMass)
                {
                    continue;
                }
                float take = Mathf.Min(have, left);
                holds[i].storage.ConsumeIgnoringDisease(tag, take);
                left -= take;
            }
            if (left <= MinMass)
            {
                return amount - left;
            }
            WorldContainer w = GetInterior(craft);
            if (w == null)
            {
                return amount - left;
            }
            List<Pickupable> items = Components.Pickupables.GetWorldItems(w.id, false);
            if (items == null)
            {
                return amount - left;
            }
            for (int i = 0; i < items.Count && left > MinMass; i++)
            {
                Pickupable p = items[i];
                if (p == null)
                {
                    continue;
                }
                // ★ 修订：与 AvailableOnFloor 同口径 —— 只扣"散落在地上"的搬运物，
                //   陈列架/储物箱里的东西一律不碰（否则卖出会拆掉玩家的陈列）。
                if (p.storage != null || p.gameObject == null || !p.gameObject.activeInHierarchy)
                {
                    continue;
                }
                KPrefabID pid = p.GetComponent<KPrefabID>();
                if (pid == null || pid.HasTag(GameTags.Stored))
                {
                    continue;
                }
                if (pid == null || !pid.HasTag(tag))
                {
                    continue;
                }
                PrimaryElement pe = p.GetComponent<PrimaryElement>();
                if (pe == null)
                {
                    continue;
                }
                // ★ 修订：质量为 0/负数的残体不参与扣减，避免 take=0 造成的空转与"扣了 0 却算成交"
                if (pe.Mass <= MinMass)
                {
                    continue;
                }
                float take = Mathf.Min(pe.Mass, left);
                if (take <= MinMass)
                {
                    continue;
                }
                left -= take;
                if (take >= pe.Mass - MinMass)
                {
                    Util.KDestroyGameObject(p.gameObject);
                }
                else
                {
                    pe.Mass = pe.Mass - take;
                }
            }
            return amount - left;
        }

        /// <summary>
        /// ★ 计量口径：按「件」扣减 —— 货舱与内舱地面逐件销毁，绝不修改单件物品的质量。
        /// 返回实际扣到的件数（整数）。
        /// </summary>
        private static float ConsumePieces(Clustercraft craft, Tag tag, float pieces)
        {
            float left = pieces;
            List<RocketHold> holds = GetHolds(craft);
            for (int i = 0; i < holds.Count && left > 0.5f; i++)
            {
                Storage s = holds[i].storage;
                if (s == null)
                {
                    continue;
                }
                List<GameObject> items = null;
                try
                {
                    items = s.GetItems();
                }
                catch (Exception)
                {
                    continue;
                }
                if (items == null)
                {
                    continue;
                }
                for (int k = items.Count - 1; k >= 0 && left > 0.5f; k--)
                {
                    GameObject it = items[k];
                    if (it == null || !it.HasTag(tag))
                    {
                        continue;
                    }
                    s.Remove(it, true);
                    Util.KDestroyGameObject(it);
                    left -= 1f;
                }
            }
            if (left > 0.5f && craft != null && craft.ModuleInterface != null)
            {
                IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
                if (mods != null)
                {
                    for (int i = 0; i < mods.Count && left > 0.5f; i++)
                    {
                        RocketModuleCluster m = mods[i].Get();
                        if (m == null || m.GetComponent<CargoBayCluster>() != null || m.GetComponent<CargoBay>() != null)
                        {
                            continue;
                        }
                        Storage[] sts = m.GetComponents<Storage>();
                        if (sts == null)
                        {
                            continue;
                        }
                        for (int k = 0; k < sts.Length && left > 0.5f; k++)
                        {
                            Storage st = sts[k];
                            if (st == null)
                            {
                                continue;
                            }
                            List<GameObject> items = null;
                            try { items = st.GetItems(); }
                            catch (Exception) { continue; }
                            if (items == null)
                            {
                                continue;
                            }
                            for (int n = items.Count - 1; n >= 0 && left > 0.5f; n--)
                            {
                                GameObject it = items[n];
                                if (it == null || !it.HasTag(tag))
                                {
                                    continue;
                                }
                                st.Remove(it, true);
                                Util.KDestroyGameObject(it);
                                left -= 1f;
                            }
                        }
                    }
                }
            }
            if (left > 0.5f)
            {
                WorldContainer w = GetInterior(craft);
                List<Pickupable> items = w != null
                    ? Components.Pickupables.GetWorldItems(w.id, false) : null;
                if (items != null)
                {
                    for (int i = items.Count - 1; i >= 0 && left > 0.5f; i--)
                    {
                        Pickupable p = items[i];
                        if (p == null || p.storage != null)
                        {
                            continue;   // 陈列架/储物箱里的不碰
                        }
                        if (p.gameObject == null || !p.gameObject.activeInHierarchy)
                        {
                            continue;
                        }
                        KPrefabID pid = p.GetComponent<KPrefabID>();
                        if (pid == null || pid.HasTag(GameTags.Stored) || !pid.HasTag(tag))
                        {
                            continue;
                        }
                        Util.KDestroyGameObject(p.gameObject);
                        left -= 1f;
                    }
                }
            }
            return pieces - left;
        }

        internal static bool Grant(Clustercraft craft, Tag tag, float mass, float temperature)
        {
            GameObject prefab = Assets.TryGetPrefab(tag);
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 找不到物品 prefab：" + tag.Name + "（发放失败）");
                return false;
            }
            // ★ 计量口径：单件物品按整件发放（每件都是 prefab 的单位质量），不产生半件/1kg 残件
            if (TakesWholePieces(tag))
            {
                float um = UnitMass(tag);
                int pieces = Mathf.FloorToInt(mass / um + 0.01f);
                if (pieces <= 0 && mass > MinMass)
                {
                    pieces = 1;   // 调用方按「1 单位」请求时仍给 1 整件
                }
                if (pieces <= 0)
                {
                    return false;
                }
                for (int n = 0; n < pieces; n++)
                {
                    if (!GrantOne(craft, tag.Name, prefab, um, temperature, false))
                    {
                        return false;
                    }
                }
                return true;
            }
            // 弹药/模块是 1kg/个的独立物品：按 kg 拆成多个物品，不能合成一个大块
            bool split = IsDiscretePrefab(tag);
            // ★ 边界：不足一份（<1kg）不再入库 —— 用 Floor 而非 Round，避免把 0.5kg 凑成 1 个整份
            int count = split ? Mathf.FloorToInt(mass + 0.001f) : 1;
            if (split && count <= 0)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 不足一份（" + mass.ToString("0.###") + "kg），不入库：" + tag.Name);
                return false;
            }
            float unitMass = split ? 1f : mass;
            for (int n = 0; n < count; n++)
            {
                if (!GrantOne(craft, tag.Name, prefab, unitMass, temperature, !split))
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool GrantUnits(Clustercraft craft, Tag tag, float units, float temperature)
        {
            GameObject prefab = Assets.TryGetPrefab(tag);
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 找不到物品 prefab：" + tag.Name + "（发放失败）");
                return false;
            }
            if (units <= 0.001f)
            {
                return true;
            }
            // ★ 边界：不足一份（<1）不再入库（Floor，不四舍五入凑整），防止整数物品被错误分割
            int count = Mathf.FloorToInt(units + 0.001f);
            if (count <= 0)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 不足一份（" + units.ToString("0.###") + "），不入库：" + tag.Name);
                return false;
            }
            // ★ 计量口径：单件物品每件都是 prefab 的单位质量（工艺品 25kg/件），其余 1 份 = 1kg
            float unitMass = CountsByPiece(tag) ? UnitMass(tag) : 1f;
            for (int n = 0; n < count; n++)
            {
                if (!GrantUnitsOne(craft, tag.Name, prefab, unitMass, temperature))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool GrantUnitsOne(Clustercraft craft, string label, GameObject prefab,
            float unitMass, float temperature)
        {
            List<RocketHold> holds = GetHolds(craft);
            for (int i = 0; i < holds.Count; i++)
            {
                Storage s = holds[i].storage;
                if (s == null || !CargoAccepts(holds[i].cargoType, prefab))
                {
                    continue;
                }
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    continue;
                }
                PrimaryElement pe = go.GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    // ★ 计量口径：一件 = 一个完整单位质量（Mass 会同时重算 Units）
                    pe.Mass = unitMass;
                    pe.Temperature = temperature;
                }
                float mass = pe != null ? pe.Mass : unitMass;
                if (s.MassStored() + mass > s.capacityKg)
                {
                    UnityEngine.Object.Destroy(go);
                    continue;
                }
                go.SetActive(true);
                s.Store(go, true, false, true, false);
                return true;
            }
            // ★ 货舱放不下 → 卸到火箭内部（控制台位 → 人员站位）
            int cell = InteriorDropCell(craft);
            if (Grid.IsValidCell(cell))
            {
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go != null)
                {
                    PrimaryElement pe = go.GetComponent<PrimaryElement>();
                    if (pe != null)
                    {
                        pe.Mass = unitMass;
                        pe.Temperature = temperature;
                    }
                    go.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore));
                    go.SetActive(true);
                    MFLog.Info(LogCat.Trade, "[Space] 货舱放不下，" + label + " 1 份已卸到火箭内部");
                    return true;
                }
            }
            MFLog.Warn(LogCat.Trade, "[Space] 火箭里放不下 " + label + "（货舱满且没有内舱地板）");
            return false;
        }

        internal static bool IsDiscretePrefab(Tag tag)
        {
            GameObject prefab = Assets.TryGetPrefab(tag);
            if (prefab == null)
            {
                return false;
            }
            KPrefabID pid = prefab.GetComponent<KPrefabID>();
            return pid != null
                && (pid.HasTag(new Tag("ModuleItem")) || pid.HasTag(new Tag("ammo")));
        }

        internal static bool IsArtifact(Tag tag)
        {
            GameObject prefab = Assets.TryGetPrefab(tag);
            return prefab != null && prefab.HasTag(GameTags.Artifact);
        }

        internal static bool DepositArtifact(Clustercraft craft, Tag tag, float temperature)
        {
            GameObject prefab = Assets.TryGetPrefab(tag);
            if (prefab == null)
            {
                return false;
            }
            // ★ 1) 优先：工艺品运输仓（ArtifactModule）
            try
            {
                if (craft != null && craft.ModuleInterface != null)
                {
                    IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
                    if (mods != null)
                    {
                        for (int i = 0; i < mods.Count; i++)
                        {
                            RocketModuleCluster m = mods[i].Get();
                            if (m == null)
                            {
                                continue;
                            }
                            ArtifactModule art = m.GetComponent<ArtifactModule>();
                            if (art == null || art.GetMassStored() >= art.GetCapacity())
                            {
                                continue;
                            }
                            GameObject go = Util.KInstantiate(prefab, null, null);
                            if (go == null)
                            {
                                continue;
                            }
                            PrimaryElement pe = go.GetComponent<PrimaryElement>();
                            if (pe != null)
                            {
                                // ★ 计量口径：一件工艺品 = 一个完整单位质量（25kg），不能留 1kg 残件
                                pe.Mass = UnitMass(tag);
                                pe.Temperature = temperature;
                            }
                            go.SetActive(true);
                            art.ForceDeposit(go);
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 工艺品运输仓入库异常");
            }
            // ★ 2) 没有运输仓（或已满）→ 落到火箭内部，确保交易/采集不被卡住
            return SpawnInInterior(craft, tag.Name, prefab, UnitMass(tag), temperature);
        }

        private static bool CargoAccepts(CargoBay.CargoType? type, GameObject prefab)
        {
            if (!type.HasValue || type.Value == CargoBay.CargoType.Entities || prefab == null)
            {
                return true;
            }
            PrimaryElement pe = prefab.GetComponent<PrimaryElement>();
            if (pe == null)
            {
                return true;
            }
            Element element = ElementLoader.FindElementByHash(pe.ElementID);
            if (element == null)
            {
                return true;
            }
            switch (type.Value)
            {
                case CargoBay.CargoType.Solids: return element.IsSolid;
                case CargoBay.CargoType.Liquids: return element.IsLiquid;
                case CargoBay.CargoType.Gasses: return element.IsGas;
                default: return true;
            }
        }

        private static bool GrantOne(Clustercraft craft, string label, GameObject prefab,
            float mass, float temperature, bool allowSplit)
        {
            List<RocketHold> holds = GetHolds(craft);
            float remaining = mass;
            for (int i = 0; i < holds.Count && remaining > 0.001f; i++)
            {
                Storage s = holds[i].storage;
                if (s == null)
                {
                    continue;
                }
                if (!CargoAccepts(holds[i].cargoType, prefab))
                {
                    continue;
                }
                float room = s.capacityKg - s.MassStored();
                if (room <= 0.001f)
                {
                    continue;
                }
                float put = allowSplit ? Mathf.Min(room, remaining) : remaining;
                if (put > room + 0.001f)
                {
                    continue;
                }
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    continue;
                }
                PrimaryElement pe = go.GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    pe.Mass = put;
                    pe.Temperature = temperature;
                }
                go.SetActive(true);
                s.Store(go, true, false, true, false);
                remaining -= put;
            }
            if (remaining <= 0.001f)
            {
                return true;
            }
            // ★ 货舱放不下 → 卸到火箭内部（控制台位 → 人员站位，避免落点被占导致堵塞）
            int cell = InteriorDropCell(craft);
            if (Grid.IsValidCell(cell))
            {
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go != null)
                {
                    PrimaryElement pe = go.GetComponent<PrimaryElement>();
                    if (pe != null)
                    {
                        pe.Mass = remaining;
                        pe.Temperature = temperature;
                    }
                    go.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore));
                    go.SetActive(true);
                    MFLog.Info(LogCat.Trade, "[Space] 货舱放不下，" + label + " " + remaining + "kg 已卸到火箭内部");
                    return true;
                }
            }
            MFLog.Warn(LogCat.Trade, "[Space] 火箭里放不下 " + label + " " + mass + "kg（货舱满且内舱没有落点）");
            return false;
        }
    }

    internal static class TradeExecutor
    {
        // 回收不设上限：星币是唯一闸门，可收物品恒可卖
        internal static float RecycleLeft(TradeStationState st, TradeItemValue v)
        {
            return st == null || v == null ? 0f : float.MaxValue;
        }

        internal static bool TrySell(Clustercraft craft, StationDef def, TradeStationState st,
            string itemId, float amount, out float gained, out string reason, bool moveMoney, float credit)
        {
            gained = 0f;
            reason = null;
            if (craft == null || def == null || st == null)
            {
                reason = "火箭或站点未就绪";
                return false;
            }
            amount = TradeNumber.Units(amount);
            if (amount <= 0f)
            {
                reason = "数量为 0";
                return false;
            }
            StationCatalog cat = TradeCatalog.Catalog(def);
            TradeItemValue v;
            if (cat == null || !cat.accept.TryGetValue(itemId, out v) || v == null)
            {
                reason = TradeUtil.DisplayName(itemId) + " 不在本站收购范围";
                return false;
            }
            Tag tag = new Tag(itemId);
            float have = TradeNumber.Units(RocketInventory.Available(craft, tag));
            if (have < amount)
            {
                reason = TradeUtil.DisplayName(itemId) + " 火箭里只有 " + Mathf.Round(have) + "，不足 " + Mathf.Round(amount);
                return false;
            }
            float unit = TradeRegistry.SellUnit(def, v);
            float pay = TradeNumber.Money(unit * amount);
            if (st.currency + credit + 0.0001f < pay)
            {
                reason = "本店剩余星币 " + st.currency.ToString("0.00") + "，本次回收应付 "
                    + pay.ToString("0.00") + "，超出 " + (pay - st.currency - credit).ToString("0.00");
                return false;
            }
            float taken = TradeNumber.Units(RocketInventory.Consume(craft, tag, amount));
            if (taken < amount)
            {
                // ★ 修订：扣物不足时必须把已扣部分退回。旧实现扣了就返回 false，物品直接消失，
                //   且下一轮判定与显示不一致（表现为"持有量对不上/越卖越多"）。
                if (taken > 0f)
                {
                    // ★ 计量口径：taken 是「份」，退回时换算回质量（单件物品 = 份 × 单位质量）
                    RocketInventory.Grant(craft, tag, RocketInventory.PieceMass(tag, taken), 293.15f);
                }
                reason = TradeUtil.DisplayName(itemId) + " 实际只扣到 " + taken + "（物品可能被占用），交易取消";
                return false;
            }
            gained = pay;
            if (moveMoney)
            {
                st.currency = TradeNumber.Money(Mathf.Max(0f, st.currency - pay));
                TradeRegistry.AddCurrency(pay);
            }
            TradeRegistry.OnFoodDelivered(itemId);

            // 卖出去了就是对方的：站点库存增加，下次刷新前可以重新买回（按正常买价）
            TradeStockEntry e = st.Find(itemId);
            if (e == null)
            {
                e = new TradeStockEntry();
                e.id = itemId;
                e.value = v;
                st.shelf.Add(e);
            }
            e.stock = TradeNumber.Units(e.stock + amount);
            if (moveMoney)
            {
                MFLog.Info(LogCat.Trade, "[Space] 卖出 " + itemId + " " + amount + " × " + unit.ToString("0.00")
                    + " = +" + pay.ToString("0.00") + " 星币（站点 " + def.id + " 星币余 "
                    + st.currency.ToString("0.00") + "，玩家余额 " + TradeRegistry.Currency.ToString("0.00") + "）");
            }
            else
            {
                MFLog.Info(LogCat.Trade, "[Space] 卖出 " + itemId + " " + amount + " × " + unit.ToString("0.00")
                    + " = +" + pay.ToString("0.00") + " 星币（并入本次净额结算）");
            }
            return true;
        }

        internal static bool TryBuy(Clustercraft craft, StationDef def, TradeStationState st,
            string itemId, float amount, out float cost, out string reason, bool moveMoney, float credit)
        {
            cost = 0f;
            reason = null;
            if (craft == null || def == null || st == null)
            {
                reason = "火箭或站点未就绪";
                return false;
            }
            TradeStockEntry entry = st.Find(itemId);
            if (entry == null)
            {
                reason = TradeUtil.DisplayName(itemId) + " 不在本站售卖清单";
                return false;
            }
            amount = TradeNumber.Units(amount);
            if (amount <= 0f)
            {
                reason = "数量为 0";
                return false;
            }
            entry.stock = TradeNumber.Units(entry.stock);
            if (entry.stock < amount)
            {
                reason = TradeUtil.DisplayName(itemId) + " 本站库存只剩 " + Mathf.Round(entry.stock);
                return false;
            }
            // 读档后货架条目的估值引用会丢（不落盘），这里按需重建，否则会按兜底价 0.01 卖给你
            TradeItemValue v = entry.value;
            if (v == null)
            {
                StationCatalog cat = TradeCatalog.Catalog(def);
                if (cat != null)
                {
                    cat.sell.TryGetValue(itemId, out v);
                }
                if (v == null)
                {
                    reason = TradeUtil.DisplayName(itemId) + " 的估值缺失（配置里已找不到这个物品）";
                    return false;
                }
                entry.value = v;
            }
            float unit = TradeRegistry.BuyUnit(def, v);
            cost = TradeNumber.Money(unit * amount);
            if (TradeRegistry.Currency + credit + 0.0001f < cost)
            {
                reason = "星币不足：需要 " + cost.ToString("0.00") + "，只有 "
                    + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00")
                    + (credit > 0.0001f ? "（含卖出 " + TradeNumber.Money(credit).ToString("0.00") + "）" : "");
                return false;
            }
            // ★ 交易默认入库位置 = 火箭内部（控制台位 → 人员站位）；工艺品优先运输仓、其次内舱
            string depositReason;
            if (!RocketInventory.DepositForTrade(craft, new Tag(itemId), amount, 300f, out depositReason))
            {
                reason = depositReason ?? (TradeUtil.DisplayName(itemId) + " 装不进火箭");
                return false;
            }
            entry.stock = Mathf.Max(0f, TradeNumber.Units(entry.stock - amount));
            if (moveMoney)
            {
                st.currency = TradeNumber.Money(st.currency + cost);
                TradeRegistry.AddCurrency(-cost);
            }
            if (entry.stock <= 0f)
            {
                st.shelf.Remove(entry);
            }
            if (moveMoney)
            {
                MFLog.Info(LogCat.Trade, "[Space] 买入 " + itemId + " " + amount + " = -" + cost.ToString("0.00")
                    + " 星币（站点 " + def.id + " 星币余 " + st.currency.ToString("0.00")
                    + "，玩家余额 " + TradeRegistry.Currency.ToString("0.00") + "）");
            }
            else
            {
                MFLog.Info(LogCat.Trade, "[Space] 买入 " + itemId + " " + amount + " = -" + cost.ToString("0.00")
                    + " 星币（并入本次净额结算）");
            }
            return true;
        }

        internal static bool TryRefuel(Clustercraft craft, StationDef def, TradeStationState st,
            float wantKg, out float cost, out string reason, out float storedKg)
        {
            cost = 0f;
            reason = null;
            storedKg = 0f;
            if (craft == null || def == null || !def.fuel_refill)
            {
                reason = "该站不支持燃料补充";
                return false;
            }
            CraftModuleInterface mi = craft.ModuleInterface;
            if (mi == null)
            {
                reason = "火箭模块接口未就绪";
                return false;
            }
            RocketEngineCluster engine = mi.GetEngine();
            if (engine == null)
            {
                reason = "火箭没有引擎";
                return false;
            }
            wantKg = TradeNumber.FuelKg(wantKg);
            float room = TradeNumber.FuelKg(Mathf.Max(0f, mi.FuelCapacity - mi.FuelRemaining));
            if (room <= 0f)
            {
                reason = "燃料仓已满（容量 " + Mathf.Round(mi.FuelCapacity) + "kg）";
                return false;
            }
            float amount = TradeNumber.FuelKg(Mathf.Min(wantKg, room));
            float pricePerKg = TradeNumber.Money(TradeDb.FuelPricePerKg(engine.fuelTag.Name));
            cost = TradeNumber.Money(pricePerKg * amount);
            if (TradeRegistry.Currency + 0.0001f < cost)
            {
                reason = "星币不足：补 " + amount.ToString("0.0") + "kg 需要 " + cost.ToString("0.00");
                return false;
            }
            float stored = TradeNumber.FuelKg(StoreFuel(craft, engine.fuelTag, amount));
            if (stored <= 0f)
            {
                reason = "燃料没存进燃料仓（燃料仓类型不匹配？）";
                return false;
            }
            // 只按真正入库的质量收费（同氧化剂口径），避免装不满时按请求量多扣
            cost = TradeNumber.Money(pricePerKg * stored);
            if (TradeRegistry.Currency + 0.0001f < cost)
            {
                reason = "星币不足：补 " + stored.ToString("0.0") + "kg 需要 " + cost.ToString("0.00");
                return false;
            }
            storedKg = stored;
            TradeRegistry.AddCurrency(-cost);
            if (st != null)
            {
                st.currency = TradeNumber.Money(st.currency + cost);   // 与买入同口径：玩家付的星币进本店账上
            }
            MFLog.Info(LogCat.Trade, "[Space] 补充燃料 " + engine.fuelTag.Name + " " + stored.ToString("0.0") + "kg × "
                + pricePerKg.ToString("0.00") + " = -" + cost.ToString("0.00") + " 星币"
                + "（燃料 " + Mathf.Round(mi.FuelRemaining) + "/" + Mathf.Round(mi.FuelCapacity) + "kg）");
            return true;
        }

        // 氧化剂舱仓储：窗口打开时抓一次列表，之后拖动滑条只读 MassStored，不再每次 GetComponent
        internal static List<Storage> OxidizerStorages(Clustercraft craft)
        {
            List<Storage> list = new List<Storage>();
            CraftModuleInterface mi = craft != null ? craft.ModuleInterface : null;
            if (mi == null || mi.ClusterModules == null)
            {
                return list;
            }
            IList<Ref<RocketModuleCluster>> mods = mi.ClusterModules;
            for (int i = 0; i < mods.Count; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null || m.GetComponent<OxidizerTank>() == null)
                {
                    continue;
                }
                Storage storage = m.GetComponent<Storage>();
                if (storage != null)
                {
                    list.Add(storage);
                }
            }
            return list;
        }

        internal static float OxidizerCapacityOf(List<Storage> storages)
        {
            float sum = 0f;
            if (storages == null)
            {
                return sum;
            }
            for (int i = 0; i < storages.Count; i++)
            {
                if (storages[i] != null)
                {
                    sum += storages[i].Capacity();
                }
            }
            return sum;
        }

        internal static float OxidizerRemainingOf(List<Storage> storages)
        {
            float sum = 0f;
            if (storages == null)
            {
                return sum;
            }
            for (int i = 0; i < storages.Count; i++)
            {
                if (storages[i] != null)
                {
                    sum += storages[i].MassStored();
                }
            }
            return sum;
        }

        // 氧化剂舱的可用种类只从该舱真实过滤器读出，绝不使用硬编码清单（固体舱 tagOptions / 液体舱 storageFilters）
        internal static List<Tag> OxidizerTagsOf(Clustercraft craft)
        {
            List<Tag> result = new List<Tag>();
            CraftModuleInterface mi = craft != null ? craft.ModuleInterface : null;
            if (mi == null || mi.ClusterModules == null)
            {
                return result;
            }
            IList<Ref<RocketModuleCluster>> mods = mi.ClusterModules;
            for (int i = 0; i < mods.Count; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null || m.GetComponent<OxidizerTank>() == null)
                {
                    continue;
                }
                Storage storage = m.GetComponent<Storage>();
                FlatTagFilterable filter = m.GetComponent<FlatTagFilterable>();
                List<Tag> pool = AcceptorPool(storage, filter);
                if (pool == null)
                {
                    continue;
                }
                for (int j = 0; j < pool.Count; j++)
                {
                    if (!result.Contains(pool[j]))
                    {
                        result.Add(pool[j]);
                    }
                }
            }
            return result;
        }

        internal static float OxidizerCapacityOf(Clustercraft craft)
        {
            return OxidizerCapacityOf(OxidizerStorages(craft));
        }

        internal static float OxidizerRemainingOf(Clustercraft craft)
        {
            return OxidizerRemainingOf(OxidizerStorages(craft));
        }

        internal static bool TryRefuelOxidizer(Clustercraft craft, StationDef def, TradeStationState st,
            Tag tag, float wantKg, out float cost, out string reason, out float storedKg)
        {
            cost = 0f;
            reason = null;
            storedKg = 0f;
            if (craft == null || def == null || !def.fuel_refill)
            {
                reason = "该站不支持燃料补充";
                return false;
            }
            List<Tag> accepted = OxidizerTagsOf(craft);
            if (!accepted.Contains(tag))
            {
                reason = "火箭氧化剂舱不接受「" + tag.Name + "」";
                return false;
            }
            wantKg = TradeNumber.FuelKg(wantKg);
            float capacity = OxidizerCapacityOf(craft);
            float room = TradeNumber.FuelKg(Mathf.Max(0f, capacity - OxidizerRemainingOf(craft)));
            if (room <= 0f)
            {
                reason = "氧化剂舱已满（容量 " + Mathf.Round(capacity) + "kg）";
                return false;
            }
            float amount = TradeNumber.FuelKg(Mathf.Min(wantKg, room));
            float pricePerKg = TradeNumber.Money(TradeDb.OxidizerPricePerKg(tag.Name));
            cost = TradeNumber.Money(pricePerKg * amount);
            if (TradeRegistry.Currency + 0.0001f < cost)
            {
                reason = "星币不足：补 " + amount.ToString("0.0") + "kg 需要 " + cost.ToString("0.00");
                return false;
            }
            float stored = TradeNumber.FuelKg(StoreOxidizer(craft, tag, amount));
            if (stored <= 0f)
            {
                reason = "氧化剂没存进舱（舱类型不匹配？）";
                return false;
            }
            cost = TradeNumber.Money(pricePerKg * stored);   // 只按真正入库的质量收费，避免部分入库时多扣
            storedKg = stored;   // 提示与日志复用真正入库的质量，不由金额反推
            TradeRegistry.AddCurrency(-cost);
            if (st != null)
            {
                st.currency = TradeNumber.Money(st.currency + cost);
            }
            MFLog.Info(LogCat.Trade, "[Space] 补充氧化剂 " + tag.Name + " " + stored.ToString("0.0") + "kg × "
                + pricePerKg.ToString("0.00") + " = -" + cost.ToString("0.00") + " 星币");
            return true;
        }

        // 单舱的可用氧化剂：优先 FlatTagFilterable.tagOptions（为空再退回 storage.storageFilters）
        private static List<Tag> AcceptorPool(Storage storage, FlatTagFilterable filter)
        {
            if (filter != null && filter.tagOptions != null && filter.tagOptions.Count > 0)
            {
                return filter.tagOptions;
            }
            return storage != null ? storage.storageFilters : null;
        }

        // 返回真正入库的质量（kg）；按舱逐个装，满了自动换下一个舱
        private static float StoreOxidizer(Clustercraft craft, Tag tag, float amount)
        {
            Element element = ElementLoader.GetElement(tag);
            if (element == null)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 找不到氧化剂元素：" + tag.Name);
                return 0f;
            }
            IList<Ref<RocketModuleCluster>> mods = craft.ModuleInterface.ClusterModules;
            float stored = 0f;
            for (int i = 0; i < mods.Count && amount > 0.0001f; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null || m.GetComponent<OxidizerTank>() == null)
                {
                    continue;
                }
                Storage concrete = m.GetComponent<Storage>();
                if (concrete == null)
                {
                    continue;
                }
                FlatTagFilterable filter = m.GetComponent<FlatTagFilterable>();
                List<Tag> pool = AcceptorPool(concrete, filter);
                if (pool == null || !pool.Contains(tag))
                {
                    continue;
                }
                if (filter != null && filter.selectedTags != null && !filter.selectedTags.Contains(tag))
                {
                    filter.selectedTags.Add(tag);   // 与原版 DEBUG_FillTank 同口径：先允许再入库，避免被过滤器挤掉
                }
                float room = concrete.Capacity() - concrete.MassStored();
                if (room <= 0.0001f)
                {
                    continue;
                }
                float put = Mathf.Min(room, amount);
                float before = concrete.MassStored();
                GameObject spawned = null;
                if (element.IsLiquid)
                {
                    concrete.AddLiquid(element.id, put, element.defaultValues.temperature, 0, 0, false, true);
                }
                else
                {
                    GameObject prefab = Assets.TryGetPrefab(tag);
                    if (prefab == null)
                    {
                        MFLog.Warn(LogCat.Trade, "[Space] 找不到氧化剂 prefab：" + tag.Name);
                        continue;
                    }
                    spawned = Util.KInstantiate(prefab, null, null);
                    if (spawned == null)
                    {
                        continue;
                    }
                    PrimaryElement pe = spawned.GetComponent<PrimaryElement>();
                    if (pe != null)
                    {
                        pe.Mass = put;
                    }
                    spawned.SetActive(true);
                    concrete.Store(spawned, true, false, true, false);
                }
                float actually = Mathf.Max(0f, concrete.MassStored() - before);   // 按真正入库的质量记账
                if (actually <= 0.0001f)
                {
                    if (spawned != null)
                    {
                        Util.KDestroyGameObject(spawned);   // 没进去就别留孤儿物体
                    }
                    continue;
                }
                amount -= actually;
                stored += actually;
            }
            return stored;
        }

        // 逐舱加注，返回真正入库的质量（kg）—— 按质量收费，不能按请求量收费
        private static float StoreFuel(Clustercraft craft, Tag fuelTag, float amount)
        {
            GameObject prefab = Assets.TryGetPrefab(fuelTag);
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Trade, "[Space] 找不到燃料 prefab：" + fuelTag.Name);
                return 0f;
            }
            CraftModuleInterface mi = craft.ModuleInterface;
            IList<Ref<RocketModuleCluster>> mods = mi.ClusterModules;
            float stored = 0f;
            for (int i = 0; i < mods.Count && amount > 0.0001f; i++)
            {
                RocketModuleCluster m = mods[i].Get();
                if (m == null)
                {
                    continue;
                }
                IFuelTank tank = m.GetComponent<IFuelTank>();
                if (tank == null || tank.Storage == null)
                {
                    continue;
                }
                Storage concrete = m.GetComponent<Storage>();
                if (concrete == null)
                {
                    continue;
                }
                float room = concrete.Capacity() - concrete.MassStored();
                if (room <= 0.0001f)
                {
                    continue;
                }
                float put = Mathf.Min(room, amount);
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    continue;
                }
                PrimaryElement pe = go.GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    pe.Mass = put;
                }
                go.SetActive(true);
                float before = concrete.MassStored();
                concrete.Store(go, true, false, true, false);
                float actually = Mathf.Max(0f, concrete.MassStored() - before);
                if (actually <= 0.0001f)
                {
                    Util.KDestroyGameObject(go);   // 没进去就别留孤儿物体
                    continue;
                }
                amount -= actually;
                stored += actually;
            }
            return stored;
        }
    }

    internal static class RocketStats
    {
        internal static float MaxHp(Clustercraft craft)
        {
            float sum = 0f;
            SpaceLoadTrace.Set("MaxHp：枚举模块");
            List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
            SpaceLoadTrace.Set("MaxHp：模块数 " + mods.Count);
            for (int i = 0; i < mods.Count; i++)
            {
                BuildingHP hp = mods[i].module.GetComponent<BuildingHP>();
                if (hp != null)
                {
                    sum += hp.MaxHitPoints;
                }
            }
            if (sum <= 0f)
            {
                sum = 100f;
            }
            return sum;
        }

        internal static float Hp(Clustercraft craft)
        {
            float sum = 0f;
            bool any = false;
            List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
            for (int i = 0; i < mods.Count; i++)
            {
                BuildingHP hp = mods[i].module.GetComponent<BuildingHP>();
                if (hp != null)
                {
                    sum += hp.HitPoints;
                    any = true;
                }
            }
            if (!any)
            {
                sum = 100f;
            }
            return sum;
        }

        internal static float HpRatio(Clustercraft craft)
        {
            float max = MaxHp(craft);
            if (max <= 0f)
            {
                return 1f;
            }
            return Mathf.Clamp01(Hp(craft) / max);
        }

        internal static float HpSpeedFactor(Clustercraft craft)
        {
            // ★ 性能：直接读 RocketCombatState 的缓存（受击/落地/回血/模块变更时更新），
            try
            {
                RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
                if (st != null)
                {
                    return st.hpSpeedFactorCache;
                }
            }
            catch (Exception)
            {
                // expected: 缓存读取失败，回退到按血量比例实时计算
            }
            return Mathf.Lerp(0.6f, 1f, HpRatio(craft));
        }

        internal static int AttackRange(Clustercraft craft)
        {
            return Mathf.Max(1, Mathf.RoundToInt(CombatRuntime.AttackRangeOf(craft)));
        }

        internal static float Damage(Clustercraft craft)
        {
            return 20f;
        }

        internal static void ApplyDamage(Clustercraft craft, float damage)
        {
            List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
            if (mods.Count == 0)
            {
                return;
            }
            float per = damage / mods.Count;
            for (int i = 0; i < mods.Count; i++)
            {
                BuildingHP hp = mods[i].module.GetComponent<BuildingHP>();
                if (hp == null || hp.invincible)
                {
                    continue;
                }
                int want = Mathf.Max(0, Mathf.RoundToInt(per));
                int allow = Mathf.Max(0, hp.HitPoints - 1);
                int deal = Mathf.Min(want, allow);
                if (deal > 0)
                {
                    hp.DoDamage(deal);
                }
            }
            MFLog.Info(LogCat.Trade, "[Space] 火箭受击 " + Mathf.Round(damage) + " → HP "
                + Mathf.Round(Hp(craft)) + "/" + Mathf.Round(MaxHp(craft))
                + "（速度系数 " + HpSpeedFactor(craft).ToString("0.00") + "）");
        }

        internal static void DamageCrew(Clustercraft craft, float damage)
        {
            WorldContainer w = RocketInventory.GetInterior(craft);
            if (w == null)
            {
                return;
            }
            List<MinionIdentity> minions = Components.LiveMinionIdentities.GetWorldItems(w.id, false);
            if (minions == null || minions.Count == 0)
            {
                return;
            }
            float each = damage / minions.Count;
            for (int i = 0; i < minions.Count; i++)
            {
                MinionIdentity m = minions[i];
                if (m == null)
                {
                    continue;
                }
                Health h = m.GetComponent<Health>();
                if (h == null)
                {
                    continue;
                }
                float allow = Mathf.Max(0f, h.hitPoints - 1f);
                float deal = Mathf.Min(each, allow);
                if (deal > 0f)
                {
                    h.Damage(deal);
                }
            }
            MFLog.Info(LogCat.Trade, "[Space] 剧烈震动：舱内 " + minions.Count + " 名乘员各受 " + Mathf.Round(each)
                + " 点伤害（不致死）");
        }
    }
}
