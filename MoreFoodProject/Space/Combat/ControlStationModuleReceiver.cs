using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 控制台模块接收器（v1.7）

    internal sealed class ControlStationModuleReceiver : KMonoBehaviour
    {
        internal Storage storage;
        private Clustercraft craft;
        private string pendingModuleId;

        protected override void OnPrefabInit()
        {
            try
            {
            base.OnPrefabInit();
            storage = gameObject.AddOrGet<Storage>();
            storage.capacityKg = 20f;
            storage.storageFilters = new List<Tag> { new Tag("ModuleItem") };
            storage.showInUI = false;
            storage.allowItemRemoval = false;
            storage.fetchCategory = Storage.FetchCategory.GeneralStorage;
            }
            catch (System.Exception e) { MFLog.HotFail("prefab:ControlStationModuleReceiver", e); }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            try
            {
                WorldContainer w = ClusterManager.Instance.GetWorld(ClusterUtil.GetMyWorldId(this));
                craft = w != null && w.gameObject != null
                    ? w.gameObject.GetComponent<Clustercraft>() : null;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 控制台接收器反查火箭失败");
            }
            if (storage != null)
            {
                storage.Subscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
            }
        }

        protected override void OnCleanUp()
        {
            try
            {
                if (storage != null)
                {
                    storage.Unsubscribe((int)GameHashes.OnStorageChange, OnStorageChanged);
                }
            }
            catch (Exception)
            {
                // expected: 销毁时退订失败可忽略，storage 随组件一并销毁、无残留回调
            }
            base.OnCleanUp();
        }

        internal bool RequestDelivery(string prefabId, string moduleId)
        {
            if (string.IsNullOrEmpty(prefabId) || string.IsNullOrEmpty(moduleId))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(pendingModuleId))
            {
                TradeUi.Notify("模块配送", "已有模块正在配送，请稍候");
                return false;
            }
            if (storage == null)
            {
                return false;
            }
            pendingModuleId = moduleId;
            HashSet<Tag> tags = new HashSet<Tag> { new Tag(prefabId) };
            FetchChore chore = new FetchChore(
                Db.Get().ChoreTypes.StorageFetch,
                storage,
                1f,
                tags,
                FetchChore.MatchCriteria.MatchID,
                Tag.Invalid,
                null,
                null,
                true,
                OnFetchComplete,
                null,
                null,
                Operational.State.Operational,
                0);
            chore.allowMultifetch = false;
            return true;
        }

        internal bool HasModuleItem(string moduleId)
        {
            if (storage == null)
            {
                return false;
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            if (def == null || string.IsNullOrEmpty(def.prefabId))
            {
                return false;
            }
            return storage.GetAmountAvailable(new Tag(def.prefabId)) >= 0.5f;
        }

        internal bool StoreModuleItem(string prefabId)
        {
            if (storage == null || string.IsNullOrEmpty(prefabId))
            {
                return false;
            }
            GameObject prefab = Assets.TryGetPrefab(new Tag(prefabId));
            if (prefab == null)
            {
                Debug.LogWarning("[Space] 模块 prefab 缺失，无法装入控制台：" + prefabId);
                return false;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                return false;
            }
            go.transform.SetPosition(transform.GetPosition());
            go.SetActive(true);
            storage.Store(go, true);
            ModuleDef def = CombatDb.FindModuleByPrefab(prefabId);
            return def != null && HasModuleItem(def.id);
        }

        internal bool RemoveModuleItem(string moduleId)
        {
            if (storage == null)
            {
                return false;
            }
            ModuleDef def = CombatDb.FindModule(moduleId);
            if (def == null || string.IsNullOrEmpty(def.prefabId))
            {
                return false;
            }
            Tag tag = new Tag(def.prefabId);
            List<GameObject> items = storage.GetItems();
            if (items == null)
            {
                return false;
            }
            for (int i = 0; i < items.Count; i++)
            {
                GameObject item = items[i];
                if (item == null || !item.HasTag(tag))
                {
                    continue;
                }
                Util.KDestroyGameObject(item);
                return true;
            }
            return false;
        }

        private void OnFetchComplete(Chore chore)
        {
            ProcessStorage();
        }

        private void OnStorageChanged(object data)
        {
            ProcessStorage();
        }

        private void ProcessStorage()
        {
            if (storage == null)
            {
                return;
            }
            List<GameObject> items = storage.GetItems();
            if (items == null || items.Count == 0)
            {
                return;
            }
            List<GameObject> snapshot = new List<GameObject>(items);
            for (int i = 0; i < snapshot.Count; i++)
            {
                GameObject item = snapshot[i];
                if (item == null)
                {
                    continue;
                }
                KPrefabID pid = item.GetComponent<KPrefabID>();
                ModuleDef def = pid != null ? CombatDb.FindModuleByPrefab(pid.PrefabTag.Name) : null;
                if (def == null)
                {
                    continue;
                }
                RocketLoadout lo = RocketLoadout.Ensure(craft);
                if (lo == null)
                {
                    continue;
                }
                if (lo.equippedModules.Contains(def.id))
                {
                    continue;
                }
                if (!lo.virtualStorage.Contains(def.id))
                {
                    lo.virtualStorage.Add(def.id);
                }
                string reason = null;
                if (lo.Equip(craft, def.id, out reason))
                {
                    lo.virtualStorage.Remove(def.id);
                    pendingModuleId = null;
                    // 装配成功不再通知（面板/贴图本身就能看出来，通知属于噪音）
                }
                else
                {
                    lo.virtualStorage.Remove(def.id);
                    pendingModuleId = null;
                    TradeUi.Notify("装配", def.name + " 已在控制台，但暂未装备：" + reason);
                }
            }
        }
    }
}
