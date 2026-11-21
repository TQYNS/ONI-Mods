using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 打捞点奖励池：按标签取可用物品 id。
    internal static class SpaceLootPools
    {
        private static readonly Dictionary<string, CachedValue<List<string>>> cache =
            new Dictionary<string, CachedValue<List<string>>>();

        internal static void ClearStatics()
        {
            cache.Clear();
        }

        internal static string KeyOf(string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                return "";
            }
            string[] copy = (string[])tags.Clone();
            Array.Sort(copy);
            return string.Join("+", copy);
        }

        internal static List<string> Get(string key, params string[] tags)
        {
            return GetCore(key, false, tags);
        }

        /// <summary>允许液体的池（饮品）：与固体池分键缓存，互不污染。</summary>
        internal static List<string> GetAny(string key, params string[] tags)
        {
            return GetCore(key + "#any", true, tags);
        }

        // 掉落组构建器（AsteroidBeltPool.Load 注入一次：静态字段，命中路径零分配且不反向依赖）
        internal static Func<string, List<string>> groupBuilder;

        /// <summary>按 key 取缓存（内存 → 存档 → 构建），与 Get 共用同一 cache 与同一批存档字段。</summary>
        internal static List<string> GetOrBuild(string key, string arg)
        {
            CachedValue<List<string>> slot = Slot(key);
            if (slot.IsValid(GlobalCacheVersion.LOOT_POOL_VERSION) && slot.value != null)
            {
                return slot.value;
            }

            List<string> saved = LoadFromSave(key);
            if (saved != null && saved.Count > 0)
            {
                slot.Update(saved, GlobalCacheVersion.LOOT_POOL_VERSION, (object)key);
                MFLog.Info(LogCat.Space, "[Space] 奖励池读存档缓存：" + key + " 共 " + saved.Count + " 种");
                return saved;
            }

            List<string> built = groupBuilder != null ? groupBuilder(arg) : new List<string>();
            if (built == null)
            {
                built = new List<string>();
            }
            if (built.Count == 0)
            {
                slot.MarkDirty();   // 空池不写档也不缓存：多半是 prefab/注册表还没就绪，下次再试
                return built;
            }
            slot.Update(built, GlobalCacheVersion.LOOT_POOL_VERSION, (object)key);
            FlushToSave();
            MFLog.Info(LogCat.Space, "[Space] 奖励池首次扫描：" + key + " 共 " + built.Count + " 种（已写入存档缓存 v"
                + GlobalCacheVersion.LOOT_POOL_VERSION + "）");
            return built;
        }

        /// <summary>只查内存缓存，不触发构建（给百科这类早期调用方用）。</summary>
        internal static List<string> TryGetCached(string key)
        {
            CachedValue<List<string>> slot;
            if (string.IsNullOrEmpty(key) || !cache.TryGetValue(key, out slot) || slot == null)
            {
                return null;
            }
            return slot.IsValid(GlobalCacheVersion.LOOT_POOL_VERSION) ? slot.value : null;
        }

        private static CachedValue<List<string>> Slot(string key)
        {
            CachedValue<List<string>> slot;
            if (!cache.TryGetValue(key, out slot) || slot == null)
            {
                slot = new CachedValue<List<string>>();
                cache[key] = slot;
            }
            return slot;
        }

        private static List<string> GetCore(string key, bool allowLiquid, string[] tags)
        {
            CachedValue<List<string>> slot = Slot(key);
            if (slot.IsValid(GlobalCacheVersion.LOOT_POOL_VERSION) && slot.value != null)
            {
                return slot.value;
            }

            List<string> saved = LoadFromSave(key);
            if (saved != null && saved.Count > 0)
            {
                slot.Update(saved, GlobalCacheVersion.LOOT_POOL_VERSION, (object)key);
                MFLog.Info(LogCat.Space, "[Space] 奖励池读存档缓存：" + key + " 共 " + saved.Count + " 种");
                return saved;
            }

            List<string> scanned = Scan(tags, allowLiquid);
            slot.Update(scanned, GlobalCacheVersion.LOOT_POOL_VERSION, (object)key);
            FlushToSave();
            MFLog.Info(LogCat.Space, "[Space] 奖励池首次扫描：" + key + " 共 " + scanned.Count + " 种（已写入存档缓存 v"
                + GlobalCacheVersion.LOOT_POOL_VERSION + "）");
            return scanned;
        }

        private static List<string> Scan(string[] tags, bool allowLiquid)
        {
            List<string> result = new List<string>();
            if (tags == null)
            {
                return result;
            }
            try
            {
                for (int t = 0; t < tags.Length; t++)
                {
                    List<GameObject> list = Assets.GetPrefabsWithTag(new Tag(tags[t]));
                    if (list == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        GameObject go = list[i];
                        if (go == null)
                        {
                            continue;
                        }
                        string id = PrefabIdOf(go);
                        if (string.IsNullOrEmpty(id) || result.Contains(id))
                        {
                            continue;
                        }
                        if (!IsCarriableSolid(id, allowLiquid))
                        {
                            continue;
                        }
                        result.Add(id);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 扫描奖励池失败");
            }
            return result;
        }

        private static string PrefabIdOf(GameObject go)
        {
            try
            {
                KPrefabID pid = go.GetComponent<KPrefabID>();
                if (pid != null && !string.IsNullOrEmpty(pid.PrefabTag.Name))
                {
                    return pid.PrefabTag.Name;
                }
            }
            catch (Exception)
            {
                // expected: 取 KPrefabID 失败时退回 GameObject 名作 id 来源
            }
            return go != null ? go.name : null;
        }

        private static bool IsCarriableSolid(string id, bool allowLiquid)
        {
            try
            {
                GameObject prefab = Assets.TryGetPrefab(new Tag(id));
                if (prefab == null)
                {
                    return false;
                }
                if (prefab.GetComponent<Pickupable>() == null)
                {
                    return false;
                }
                if (prefab.GetComponent<Building>() != null)
                {
                    return false;
                }
                Element element = ElementLoader.GetElement(new Tag(id));
                return element == null || element.IsSolid || allowLiquid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<string> LoadFromSave(string key)
        {
            try
            {
                TradeSaveData sd = TradeSaveData.Attach();
                if (sd == null || sd.lootPoolKeys == null || sd.lootPoolIds == null)
                {
                    return null;
                }
                if (sd.lootPoolVersion != GlobalCacheVersion.LOOT_POOL_VERSION)
                {
                    if (sd.lootPoolKeys.Count > 0)
                    {
                        Debug.LogWarning("[Space] 奖励池缓存版本 " + sd.lootPoolVersion + " ≠ 当前 "
                            + GlobalCacheVersion.LOOT_POOL_VERSION + "：整块作废，重扫");
                    }
                    sd.lootPoolKeys.Clear();
                    sd.lootPoolIds.Clear();
                    sd.lootPoolCounts.Clear();
                    return null;
                }
                int idx = sd.lootPoolKeys.IndexOf(key);
                if (idx < 0)
                {
                    return null;
                }
                int cursor = 0;
                for (int i = 0; i < idx; i++)
                {
                    cursor += i < sd.lootPoolCounts.Count ? sd.lootPoolCounts[i] : 0;
                }
                int count = idx < sd.lootPoolCounts.Count ? sd.lootPoolCounts[idx] : 0;
                List<string> list = new List<string>();
                for (int k = 0; k < count && cursor + k < sd.lootPoolIds.Count; k++)
                {
                    string id = sd.lootPoolIds[cursor + k];
                    if (string.IsNullOrEmpty(id) || Assets.TryGetPrefab(new Tag(id)) == null)
                    {
                        continue;
                    }
                    list.Add(id);
                }
                return list;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 读取奖励池缓存失败");
                return null;
            }
        }

        // 全量重写前先载入存档里其它池：下面的 Clear 只重写内存已有的 key，不预载会把它们抹掉
        private static void PreloadSavedPools()
        {
            try
            {
                TradeSaveData sd = TradeSaveData.Attach();
                if (sd == null || sd.lootPoolKeys == null || sd.lootPoolKeys.Count == 0
                    || sd.lootPoolVersion != GlobalCacheVersion.LOOT_POOL_VERSION)
                {
                    return;
                }
                for (int i = 0; i < sd.lootPoolKeys.Count; i++)
                {
                    string key = sd.lootPoolKeys[i];
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    CachedValue<List<string>> exist;
                    if (cache.TryGetValue(key, out exist) && exist != null
                        && exist.IsValid(GlobalCacheVersion.LOOT_POOL_VERSION))
                    {
                        continue;
                    }
                    List<string> saved = LoadFromSave(key);
                    if (saved != null && saved.Count > 0)
                    {
                        Slot(key).Update(saved, GlobalCacheVersion.LOOT_POOL_VERSION, (object)key);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 预载奖励池缓存失败（本次仍会重写）");
            }
        }

        private static void FlushToSave()
        {
            try
            {
                PreloadSavedPools();
                TradeSaveData sd = TradeSaveData.Attach();
                if (sd == null || sd.lootPoolKeys == null)
                {
                    return;
                }
                sd.lootPoolKeys.Clear();
                sd.lootPoolIds.Clear();
                sd.lootPoolCounts.Clear();
                foreach (KeyValuePair<string, CachedValue<List<string>>> kv in cache)
                {
                    if (kv.Value == null || !kv.Value.IsValid(GlobalCacheVersion.LOOT_POOL_VERSION)
                        || kv.Value.value == null || kv.Value.value.Count == 0)
                    {
                        continue;   // 只写当前版本的条目：dirty / 空池都不落档
                    }
                    sd.lootPoolKeys.Add(kv.Key);
                    sd.lootPoolCounts.Add(kv.Value.value.Count);
                    sd.lootPoolIds.AddRange(kv.Value.value);
                }
                sd.lootPoolVersion = GlobalCacheVersion.LOOT_POOL_VERSION;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 写入奖励池缓存失败");
            }
        }
    }
}
