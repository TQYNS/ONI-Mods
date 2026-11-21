using System;
using UnityEngine;

namespace EventFramework
{
    public static class ItemTagValidator
    {
        // itemId：item id、reason：原因
        public static bool IsPickupableById(string itemId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(itemId))
            {
                reason = "空 id";
                return false;
            }

            GameObject prefab = null;
            try { prefab = Assets.TryGetPrefab(new Tag(itemId)); }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("tag:IsPickupableById", "[Tag] IsPickupableById 失败 → 已按兜底处理", e); prefab = null; }

            if (prefab == null)
            {
                reason = "Prefab 不存在";
                return false;
            }

            return IsPickupable(prefab, out reason);
        }

        // 校验 prefab 是否可被拾取（有 Pickupable 组件才可）；失败时 reason 给出原因
        public static bool IsPickupable(GameObject prefab, out string reason)
        {
            reason = null;
            if (prefab == null)
            {
                reason = "Prefab 为 null";
                return false;
            }

            var kpid = prefab.GetComponent<KPrefabID>();
            if (kpid == null)
            {
                reason = "缺少 KPrefabID";
                return false;
            }

            if (kpid.HasTag(GameTags.Pickupable)) return true;

            if (prefab.GetComponent<Pickupable>() != null) return true;

            reason = "不是可拾取物品（无 Pickupable 组件，也没有 Pickupable tag）";
            return false;
        }
    }
}
