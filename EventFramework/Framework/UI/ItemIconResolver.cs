using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class ItemIconResolver
    {
        private sealed class Entry
        {
            // 图标
            public Sprite sprite;
            // 叠色
            public Color tint;
        }

        private static readonly Dictionary<string, Entry> _cache =
            new Dictionary<string, Entry>(System.StringComparer.Ordinal);

        // 按物品 id 取图标与着色；取不到返回 false（sprite=null、tint=白）
        public static bool TryGet(string itemId, out Sprite sprite, out Color tint)
        {
            sprite = null;
            tint = Color.white;
            if (string.IsNullOrEmpty(itemId)) return false;

            Entry entry;
            if (_cache.TryGetValue(itemId, out entry))
            {
                if (entry == null || entry.sprite == null) return false;
                sprite = entry.sprite;
                tint = entry.tint;
                return true;
            }

            entry = Build(itemId);
            _cache[itemId] = entry;

            if (entry.sprite == null) return false;
            sprite = entry.sprite;
            tint = entry.tint;
            return true;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static Entry Build(string itemId)
        {
            var entry = new Entry();
            entry.sprite = null;
            entry.tint = Color.white;

            try
            {
                // 先按 Tag 解析，星币等没有可靠 Prefab 的资源也能取到游戏内 UI 图标
                var pair = Def.GetUISprite(itemId, "ui", false);
                if (pair == null || pair.first == null)
                {
                    var prefab = Assets.TryGetPrefab(new Tag(itemId));
                    if (prefab != null) pair = Def.GetUISprite(prefab, "ui", false);
                }
                if (pair == null || pair.first == null) return entry;

                entry.sprite = pair.first;
                entry.tint = pair.second;
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("icon:item", "[Icon] 物品图标解析异常 → 用占位图标", e);
            }

            return entry;
        }
    }
}
