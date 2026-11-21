using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class UiIconResolver
    {
        // 是否走 sprite tag 取图（否则用代码表）
        public static bool UseSpriteTags = false;
        // sprite 资源名
        public static string SpriteAssetName = "oni_sprite_assets";

        // sprite 名前缀
        public const string SPRITE_PREFIX = "ui_icon_";

        private static readonly Dictionary<string, string> _codeMap =
            new Dictionary<string, string>(System.StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _cache =
            new Dictionary<string, string>(System.StringComparer.Ordinal);

        // iconId：icon id、spriteName：sprite 名称
        public static void Register(string iconId, string spriteName)
        {
            if (string.IsNullOrEmpty(iconId) || string.IsNullOrEmpty(spriteName)) return;
            _codeMap[iconId] = spriteName;
            _cache.Remove(iconId);
        }

        // iconId：icon id
        public static string Resolve(string iconId)
        {
            if (string.IsNullOrEmpty(iconId) || !UseSpriteTags) return null;

            string cached;
            if (_cache.TryGetValue(iconId, out cached)) return cached;

            string result = null;
            string registered;
            if (_codeMap.TryGetValue(iconId, out registered)) result = registered;

            if (string.IsNullOrEmpty(result) && HasSprite(iconId)) result = iconId;

            if (string.IsNullOrEmpty(result))
            {
                string prefixed = SPRITE_PREFIX + iconId;
                if (HasSprite(prefixed)) result = prefixed;
            }

            _cache[iconId] = result;
            return result;
        }

        // iconId：icon id
        public static string IconTag(string iconId)
        {
            string spriteName = Resolve(iconId);
            if (string.IsNullOrEmpty(spriteName)) return "";
            if (string.IsNullOrEmpty(SpriteAssetName)) return "<sprite name=\"" + spriteName + "\">";
            return "<sprite=\"" + SpriteAssetName + "\" name=\"" + spriteName + "\">";
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static bool HasSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return false;
            try { return Assets.GetSprite(spriteName) != null; }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("icon:ui:" + spriteName, "[Icon] 查 UI 精灵 '" + spriteName + "' 失败 → 视为不存在", e);
                return false;
            }
        }
    }
}
