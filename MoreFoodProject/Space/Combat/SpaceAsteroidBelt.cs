using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    public enum BeltStyle
    {
        Metallic,
        Gilded,
        Rocky,
        Oxidized,
        Ice,
        Cloud
    }

    public class SpaceAsteroidBelt : SpaceLootField, IGameObjectEffectDescriptor
    {
        [Serialize]
        public string beltStyle = "metallic_asteroid_field";

        internal int createdEpoch;
        internal bool fromRestore;

        private StarmapHexCellInventory linkedStore;

        public void CollectDescriptors(GameObject go, List<Descriptor> result)
        {
            if (result == null)
            {
                return;
            }
            try
            {
                int kinds = 0;
                float total = 0f;
                string detail = "";
                for (int i = 0; i < lootItemIds.Count; i++)
                {
                    string itemId = lootItemIds[i];
                    float mass = i < lootMasses.Count ? lootMasses[i] : 0f;
                    int count = i < lootCounts.Count ? lootCounts[i] : 0;
                    if (mass <= 0f && count <= 0)
                    {
                        continue;
                    }
                    string display = itemId;
                    try
                    {
                        display = TradeUtil.DisplayName(itemId);
                    }
                    catch (Exception)
                    {
                        // expected: 显示名取不到时回退物资 id
                    }
                    kinds++;
                    total += mass;
                    if (detail.Length > 0)
                    {
                        detail += "、";
                    }
                    detail += count > 1
                        ? display + " ×" + count
                        : display + " " + Mathf.Round(mass) + "kg";
                }
                if (kinds == 0)
                {
                    result.Add(new Descriptor("已采空", "没有可打捞物资", Descriptor.DescriptorType.Effect, false));
                }
                else
                {
                    result.Add(new Descriptor("可打捞物 " + kinds + " 项（合计 " + Mathf.Round(total) + "kg）",
                        detail, Descriptor.DescriptorType.Effect, false));
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带内容描述失败");
            }
        }

        internal StarmapHexCellInventory Store
        {
            get { return gameObject.AddOrGet<StarmapHexCellInventory>(); }
        }

        private bool healthLogged;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            try
            {
                KSelectable selectable = GetComponent<KSelectable>();
                bool selectableOk = selectable != null && selectable.IsSelectable;
                string cellInfo = "";
                int cellCount = -1;
                ClusterGrid grid = ClusterGrid.Instance;
                List<ClusterGridEntity> list = null;
                if (grid != null && grid.cellContents.TryGetValue(Location, out list) && list != null)
                {
                    cellCount = list.Count;
                    for (int i = 0; i < list.Count; i++)
                    {
                        ClusterGridEntity e = list[i];
                        if (e == null)
                        {
                            cellInfo += (i > 0 ? " | " : "") + "[null]";
                            continue;
                        }
                        KSelectable es = e.GetComponent<KSelectable>();
                        cellInfo += (i > 0 ? " | " : "") + e.gameObject.name
                            + "(sel=" + (es != null && es.IsSelectable) + ")";
                    }
                }
                MFLog.Info(LogCat.Combat,"[Space] 打捞点出生检查：@" + SpaceCfg.Pos(Location) + " 可选中=" + selectableOk
                    + " 格内=" + cellCount + " [" + cellInfo + "]");
                if (selectable != null && !selectableOk)
                {
                    selectable.IsSelectable = true;
                    MFLog.Warn(LogCat.Combat,"[Space] 打捞点出生时不可选中，已强制恢复：@" + SpaceCfg.Pos(Location));
                }
                StartCoroutine(MFLog.SafeRoutine(DelayedHealthCheck(), "routine:SpaceAsteroidBelt"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 打捞点出生检查异常");
            }
        }

        private System.Collections.IEnumerator DelayedHealthCheck()
        {
            yield return null;
            yield return null;
            HealthCheck();
            yield return new WaitForSecondsRealtime(2f);
            HealthCheck();
        }

        internal void HealthCheck()
        {
            try
            {
                KSelectable selectable = GetComponent<KSelectable>();
                bool selectableOk = selectable != null && selectable.IsSelectable;
                bool inCell = false;
                int cellCount = 0;
                ClusterGrid grid = ClusterGrid.Instance;
                List<ClusterGridEntity> list = null;
                if (grid != null && grid.cellContents.TryGetValue(Location, out list) && list != null)
                {
                    cellCount = list.Count;
                    inCell = list.Contains(this);
                }
                if (!healthLogged)
                {
                    healthLogged = true;
                    MFLog.Info(LogCat.Combat,"[Space] 打捞点自检：@" + SpaceCfg.Pos(Location)
                        + " 可选中=" + selectableOk + " 在格=" + inCell + " 格内实体=" + cellCount
                        + " 已揭示=" + StarMapVisuals.IsCellRevealed(Location));
                }
                if (selectable == null)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 打捞点缺少 KSelectable，无法交互：@" + SpaceCfg.Pos(Location));
                    return;
                }
                if (!selectableOk)
                {
                    selectable.IsSelectable = true;
                    MFLog.Warn(LogCat.Combat,"[Space] 打捞点不可选中，已强制恢复：@" + SpaceCfg.Pos(Location));
                }
                if (!inCell && grid != null)
                {
                    grid.RegisterEntity(this);
                    MFLog.Warn(LogCat.Combat,"[Space] 打捞点不在格子内容里，已补注册：@" + SpaceCfg.Pos(Location));
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 打捞点自检异常");
            }
        }

        internal void BindStore(StarmapHexCellInventory store)
        {
            try
            {
                StarmapHexCellInventory mine = Store;
                if (mine == null)
                {
                    return;
                }
                linkedStore = store;
                if (store != null && store.Items != null)
                {
                    mine.Items = store.Items;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带绑定原版打捞清单失败");
            }
        }

        private void NotifyStoreChanged()
        {
            Trigger(-1697596308, null);
            if (linkedStore != null)
            {
                linkedStore.Trigger(-1697596308, null);
            }
        }

        internal void SyncInventory()
        {
            try
            {
                StarmapHexCellInventory inv = Store;
                if (inv == null)
                {
                    return;
                }
                if (inv.Items == null)
                {
                    inv.Items = new List<StarmapHexCellInventory.SerializedItem>();
                }
                inv.Items.Clear();
                for (int i = 0; i < lootItemIds.Count; i++)
                {
                    string itemId = lootItemIds[i];
                    float mass = i < lootMasses.Count ? lootMasses[i] : 0f;
                    if (string.IsNullOrEmpty(itemId) || mass <= 0f)
                    {
                        continue;
                    }
                    StarmapHexCellInventory.SerializedItem item =
                        inv.AddItem(new Tag(itemId), mass, Element.State.Vacuum);
                    if (item != null)
                    {
                        item.RecalculateState();
                    }
                }
                NotifyStoreChanged();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带内容同步失败");
            }
        }

        internal void RefreshFromStore()
        {
            try
            {
                lootItemIds.Clear();
                lootMasses.Clear();
                lootCounts.Clear();
                StarmapHexCellInventory inv = Store;
                if (inv == null || inv.Items == null)
                {
                    return;
                }
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    StarmapHexCellInventory.SerializedItem item = inv.Items[i];
                    if (item == null || item.Mass <= 0f)
                    {
                        continue;
                    }
                    string id = item.ID.ToString();
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }
                    lootItemIds.Add(id);
                    lootMasses.Add(item.Mass);
                    lootCounts.Add(0);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带读取打捞清单失败");
            }
        }

        private Clustercraft miningCraft;
        private int miningCraftId;
        private float miningEndsAt;
        private bool fullNotified;
        private bool artifactBlocked;

        internal bool ArtifactBlocked
        {
            get { return artifactBlocked; }
        }

        internal bool FullNotified
        {
            get { return fullNotified; }
            set { fullNotified = value; }
        }

        internal bool IsMining(Clustercraft craft, float now)
        {
            if (craft == null)
            {
                StopMining();
                return false;
            }
            int id = craft.GetInstanceID();
            if (miningCraftId != id)
            {
                StopMining();
                miningCraft = craft;
                miningCraftId = id;
                miningEndsAt = now + 5f;
                fullNotified = false;
                artifactBlocked = false;
                try
                {
                    craft.AddTag(GameTags.RocketCollectingResources);
                }
                catch (Exception)
                {
                    // expected: 标签重复添加或飞船已回收，忽略后仍按采集中处理
                }
                return true;
            }
            if (now < miningEndsAt)
            {
                return true;
            }
            StopMining();
            return false;
        }

        internal void StopMining()
        {
            if (miningCraft != null)
            {
                try
                {
                    miningCraft.RemoveTag(GameTags.RocketCollectingResources);
                }
                catch (Exception)
                {
                    // expected: 标签已不存在或飞船已回收，忽略不影响停止采集
                }
            }
            miningCraft = null;
            miningCraftId = 0;
        }

        internal bool CollectInto(Clustercraft craft)
        {
            bool allDone = true;
            try
            {
                StarmapHexCellInventory inv = Store;
                if (inv == null || inv.Items == null)
                {
                    return true;
                }
                for (int k = inv.Items.Count - 1; k >= 0; k--)
                {
                    StarmapHexCellInventory.SerializedItem item = inv.Items[k];
                    if (item == null || item.Mass <= 0f)
                    {
                        inv.Items.RemoveAt(k);
                        continue;
                    }
                    if (RocketInventory.IsArtifact(item.ID))
                    {
                        if (artifactBlocked)
                        {
                            allDone = false;
                            continue;
                        }
                        if (!RocketInventory.DepositArtifact(craft, item.ID, 300f))
                        {
                            artifactBlocked = true;
                            allDone = false;
                            continue;
                        }
                        inv.Items.RemoveAt(k);
                        continue;
                    }
                    bool granted = (item.IsEntity || RocketInventory.IsDiscretePrefab(item.ID))
                        ? RocketInventory.GrantUnits(craft, item.ID, item.Mass, 300f)
                        : RocketInventory.Grant(craft, item.ID, item.Mass, 300f);
                    if (granted)
                    {
                        inv.Items.RemoveAt(k);
                    }
                    else
                    {
                        allDone = false;
                    }
                }
                NotifyStoreChanged();
                RefreshFromStore();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带入舱失败");
                allDone = false;
            }
            return allDone;
        }

        public override List<ClusterGridEntity.AnimConfig> AnimConfigs
        {
            get
            {
                List<ClusterGridEntity.AnimConfig> list = new List<ClusterGridEntity.AnimConfig>();
                KAnimFile file;
                Assets.TryGetAnim(new HashedString("harvestable_space_poi_kanim"), out file);
                bool vanillaPoiAnim = file != null;
                if (file == null)
                {
                    Assets.TryGetAnim(new HashedString("meteor_rock_kanim"), out file);
                }
                if (file != null)
                {
                    ClusterGridEntity.AnimConfig cfg = new ClusterGridEntity.AnimConfig();
                    cfg.animFile = file;
                    cfg.initialAnim = vanillaPoiAnim
                        ? (string.IsNullOrEmpty(beltStyle) ? "metallic_asteroid_field" : beltStyle)
                        : "fall_loop";
                    list.Add(cfg);
                }
                return list;
            }
        }

        internal static string AnimOf(BeltStyle style)
        {
            switch (style)
            {
                case BeltStyle.Gilded: return "gilded_asteroid_field";
                case BeltStyle.Rocky: return "rocky_asteroid_field";
                case BeltStyle.Oxidized: return "oxidized_asteroid_field";
                case BeltStyle.Ice: return "ice_asteroid_field";
                case BeltStyle.Cloud: return "cloud";
                default: return "metallic_asteroid_field";
            }
        }

        internal static GameObject CreateBeltPrefab(string id, string displayName)
        {
            GameObject go = EntityTemplates.CreateEntity(id, displayName, true);
            go.AddOrGet<SaveLoadRoot>();
            go.AddOrGet<SpaceAsteroidBelt>();
            go.AddOrGet<StarmapHexCellInventory>();
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            return go;
        }
    }

    public sealed class BeltBuilder
    {
        private readonly string _name;
        private readonly BeltStyle _style;
        private readonly List<string> _items = new List<string>();
        private readonly List<float> _masses = new List<float>();
        private readonly List<int> _counts = new List<int>();

        internal BeltBuilder(string name, BeltStyle style)
        {
            _name = name;
            _style = style;
        }

        public BeltBuilder Add(string itemId, float mass)
        {
            if (!string.IsNullOrEmpty(itemId) && mass > 0f)
            {
                _items.Add(itemId);
                _masses.Add(mass);
                _counts.Add(0);
            }
            return this;
        }

        public BeltBuilder AddPiece(string itemId, int count)
        {
            if (!string.IsNullOrEmpty(itemId) && count > 0)
            {
                _items.Add(itemId);
                _masses.Add(count * 1f);
                _counts.Add(count);
            }
            return this;
        }

        public SpaceAsteroidBelt At(AxialI cell)
        {
            return AsteroidBeltFactory.Spawn(_name, _style, _items, _masses, _counts, cell);
        }

        // 强制落指定格：击杀掉落的残骸带必须留在怪物原格，不走就近找空位
        public SpaceAsteroidBelt AtExact(AxialI cell)
        {
            return AsteroidBeltFactory.Spawn(_name, _style, _items, _masses, _counts, cell, true);
        }

        public SpaceAsteroidBelt Auto()
        {
            return AsteroidBeltFactory.Spawn(_name, _style, _items, _masses, _counts, AxialI.INVALID);
        }
    }

    public enum BeltKind
    {
        PirateWreck,
        BattleDebris,
        WarRuins,
        LostLogistics
    }

    internal static class AsteroidBeltFactory
    {
        private const string PrefabId = "SpaceAsteroidBelt";
        private static GameObject prefab;

        internal static BeltBuilder Create(string name, BeltStyle style)
        {
            return new BeltBuilder(name, style);
        }

        internal static BeltBuilder CreateAuto(BeltKind kind)
        {
            switch (kind)
            {
                case BeltKind.LostLogistics:
                    return new BeltBuilder("星际遗失物流箱", RollStyle(BeltKind.LostLogistics));
                case BeltKind.BattleDebris:
                    return new BeltBuilder("战斗残骸", RollStyle(BeltKind.BattleDebris));
                case BeltKind.WarRuins:
                    return new BeltBuilder("星际大战遗址", RollStyle(BeltKind.WarRuins));
                default:
                    return new BeltBuilder("海盗船坠毁", RollStyle(BeltKind.PirateWreck));
            }
        }

        internal static BeltStyle RollStyle(BeltKind kind)
        {
            BeltStyle[] pool = kind == BeltKind.LostLogistics
                ? new BeltStyle[] { BeltStyle.Gilded, BeltStyle.Metallic, BeltStyle.Rocky }
                : new BeltStyle[] { BeltStyle.Metallic, BeltStyle.Rocky, BeltStyle.Oxidized, BeltStyle.Ice };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        /// <summary>全外观随机。</summary>
        internal static BeltStyle RollAnyStyle()
        {
            BeltStyle[] pool = new BeltStyle[]
            {
                BeltStyle.Metallic, BeltStyle.Gilded, BeltStyle.Rocky,
                BeltStyle.Oxidized, BeltStyle.Ice, BeltStyle.Cloud
            };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private static GameObject EnsurePrefab()
        {
            if (prefab != null)
            {
                return prefab;
            }
            prefab = Assets.TryGetPrefab(new Tag(PrefabId));
            if (prefab == null)
            {
                prefab = SpaceAsteroidBelt.CreateBeltPrefab(PrefabId, "小行星带");
            }
            return prefab;
        }

        internal static int Feed(BeltBuilder builder, List<string> items, List<float> masses, List<int> counts)
        {
            if (builder == null || items == null)
            {
                return 0;
            }
            int added = 0;
            for (int i = 0; i < items.Count; i++)
            {
                string id = items[i];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                float mass = i < masses.Count ? masses[i] : 0f;
                int count = i < counts.Count ? counts[i] : 0;
                if (count > 1 && (float)count >= mass)
                {
                    builder.AddPiece(id, count);
                    added++;
                }
                else if (mass > 0f)
                {
                    builder.Add(id, mass);
                    added++;
                }
                else if (count > 0)
                {
                    builder.AddPiece(id, count);
                    added++;
                }
            }
            return added;
        }

        internal static StarmapHexCellInventory EnsureVanillaStore(AxialI cell)
        {
            try
            {
                ClusterGrid grid = ClusterGrid.Instance;
                if (grid == null)
                {
                    return null;
                }
                StarmapHexCellInventory store = grid.AddOrGetHexCellInventory(cell);
                if (store == null || store.gameObject == null)
                {
                    StarmapHexCellInventory.AllInventories.Remove(cell);
                    store = grid.AddOrGetHexCellInventory(cell);
                }
                if (store == null || store.gameObject == null)
                {
                    return null;
                }
                SaveLoadRoot slr = store.GetComponent<SaveLoadRoot>();
                if (slr != null)
                {
                    slr.SetRegistered(false);
                }
                store.gameObject.AddOrGet<SpaceBeltStoreMarker>();
                KSelectable selectable = store.GetComponent<KSelectable>();
                if (selectable != null)
                {
                    selectable.IsSelectable = false;
                }
                return store;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 创建原版打捞清单失败");
                return null;
            }
        }

        /// <summary>必须是空格；占用则就近找空格，找不到返回 InvalidCell。</summary>
        internal static AxialI ResolveFreeCell(AxialI cell)
        {
            if (SpaceRealEventRuntime.CellFree(cell))
            {
                return cell;
            }
            AxialI near = SpaceRealEventRuntime.FindFreeCellNear(cell, 1, 4);
            if (SpaceRealEventRuntime.CellFree(near))
            {
                MFLog.Warn(LogCat.Combat,"[Space] 小行星带落点 " + SpaceCfg.Pos(cell) + " 已被占用 → 就近改到 "
                    + SpaceCfg.Pos(near));
                return near;
            }
            AxialI origin = SpaceCfg.Mk(0, 0);
            try
            {
                origin = SpaceRealEventRuntime.HomePlanetCell();
            }
            catch (Exception)
            {
                // expected: 母星格探测失败时改从原点搜索落点
            }
            near = SpaceRealEventRuntime.FindFreeCellNear(origin, 3, 10);
            if (!SpaceRealEventRuntime.CellFree(near))
            {
                near = SpaceRealEventRuntime.FindFreeCellNear(SpaceCfg.Mk(0, 0), 2, 12);
            }
            if (SpaceRealEventRuntime.CellFree(near))
            {
                MFLog.Warn(LogCat.Combat,"[Space] 小行星带落点改到别处空位 " + SpaceCfg.Pos(near));
                return near;
            }
            return SpaceRealEventRuntime.InvalidCell();
        }

        internal static SpaceAsteroidBelt Spawn(string name, BeltStyle style, List<string> items, List<float> masses,
            List<int> counts, AxialI cell)
        {
            return Spawn(name, style, items, masses, counts, cell, false);
        }

        // exactCell = true：强制落指定格（击杀掉落用），不再就近改点，避免残骸被挤到可见外沿之外
        internal static SpaceAsteroidBelt Spawn(string name, BeltStyle style, List<string> items, List<float> masses,
            List<int> counts, AxialI cell, bool exactCell)
        {
            try
            {
                if (items == null || items.Count == 0)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带 " + name + " 没有清单，拒绝生成空壳");
                    return null;
                }
                GameObject template = EnsurePrefab();
                if (template == null)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带 prefab 创建失败");
                    return null;
                }
                // 落点必须是空格（占用则就近找，找不到放弃）；击杀掉落按原格生成
                AxialI wanted = cell;
                bool cellOk = exactCell && ClusterGrid.Instance != null && ClusterGrid.Instance.IsValidCell(cell);
                if (!cellOk)
                {
                    cell = ResolveFreeCell(cell);
                }
                if (cell.Q == int.MinValue)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带：" + name + " 找不到星图空位（"
                        + SpaceCfg.Pos(wanted) + " 已被占用）");
                    return null;
                }
                GameObject go = Util.KInstantiate(template, null, null);
                if (go == null)
                {
                    return null;
                }
                SpaceAsteroidBelt belt = go.GetComponent<SpaceAsteroidBelt>();
                if (belt == null)
                {
                    return null;
                }
                belt.beltStyle = SpaceAsteroidBelt.AnimOf(style);
                belt.sourceTag = style.ToString().ToLowerInvariant();
                belt.lootItemIds.Clear();
                belt.lootMasses.Clear();
                belt.lootCounts.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    belt.lootItemIds.Add(items[i]);
                    belt.lootMasses.Add(i < masses.Count ? masses[i] : 0f);
                    belt.lootCounts.Add(i < counts.Count ? counts[i] : 0);
                }
                belt.label = name;
                belt.createdEpoch = CombatRuntime.LoadEpoch;
                StarmapHexCellInventory store = EnsureVanillaStore(cell);
                if (store == null)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 小行星带 " + name + " 拿不到原版打捞清单，改用自带镜像");
                }
                belt.BindStore(store);
                belt.SyncInventory();
                belt.Location = cell;
                go.SetActive(true);
                try
                {
                    SaveLoadRoot slr = go.GetComponent<SaveLoadRoot>();
                    if (slr != null)
                    {
                        slr.SetRegistered(false);
                    }
                }
                catch (Exception ex)
                {
                    MFLog.Exc(LogCat.Combat, ex, "[Space] 关闭小行星带原版持久化失败");
                }
                belt.SyncInventory();
                CombatRuntime.LootFields.Add(belt);
                SpaceReveal.RevealIfMain(cell);
                StarMapVisuals.RefreshNow();
                MFLog.Info(LogCat.Combat,"[Space] ★ 生成小行星带 " + name + " @" + SpaceCfg.Pos(cell) + " 外观=" + belt.beltStyle
                    + "：" + belt.DescribeContents());
                return belt;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 小行星带生成失败");
                return null;
            }
        }
    }
}
