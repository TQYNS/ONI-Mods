using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class ModPrefixResolver
    {
        private static readonly Dictionary<KMod.Mod, string> _cache =
            new Dictionary<KMod.Mod, string>();

        // 本文件日志前缀
        public const string LOG_TAG = "[MoreFood/Load]";

        // 从 mod 对象取标题（带缓存；取不到返回 null）
        public static string ResolveModTitle(KMod.Mod mod)
        {
            if (mod == null) return null;

            string cached;
            if (_cache.TryGetValue(mod, out cached)) return cached;

            string title = null;
            try
            {
                title = mod.title;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(LOG_TAG + " ⚠ 读取 mod.title 异常: " + e);
                _cache[mod] = null;
                return null;
            }

            string trimmed = title == null ? null : title.Trim();
            _cache[mod] = trimmed;
            return trimmed;
        }

        // 前缀最大长度
        public const int MAX_PREFIX_LENGTH = 48;

        // ★ 前缀的唯一入口：把一批 mod 一次性解析成 "mod → 合法且唯一的前缀"。
        public static Dictionary<KMod.Mod, string> ResolvePrefixes(List<KMod.Mod> mods)
        {
            var result = new Dictionary<KMod.Mod, string>();
            if (mods == null) return result;

            var used = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var renamed = new List<string>();

            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod == null) continue;

                string title = ResolveModTitle(mod);
                string prefix = MakeBasePrefix(title, i);
                if (string.IsNullOrEmpty(title) || !string.Equals(prefix, title, System.StringComparison.Ordinal))
                    renamed.Add("'" + (title ?? "<空>") + "' → " + prefix);

                string final = prefix;
                int n = 1;
                while (used.ContainsKey(final))
                {
                    n++;
                    final = prefix + "_" + n;
                }
                if (n > 1) renamed.Add("'" + (title ?? "<空>") + "' 撞名 → " + final);

                used[final] = i;
                result[mod] = final;
            }

            if (renamed.Count > 0)
            {
                Debug.Log(LOG_TAG + " ID 前缀（补丁里引用别的 mod 要用右边这个）："
                          + string.Join(" · ", renamed.ToArray()));
            }
            return result;
        }

        private static string MakeBasePrefix(string title, int index)
        {
            if (!string.IsNullOrEmpty(title))
            {
                if (IdentifierValidator.IsValid(title)) return title;

                string sanitized = IdentifierValidator.Sanitize(title, MAX_PREFIX_LENGTH);
                if (!string.IsNullOrEmpty(sanitized) && !IsOnlyDigitsAndUnderscores(sanitized))
                    return sanitized;
            }

            string fallback = "mod" + index;
            Debug.LogWarning(LOG_TAG + " ⚠ mod.title '" + (title ?? "<空>")
                             + "' 洗不出有意义的 ID 前缀 → 用 '" + fallback + "' 兜底");
            return fallback;
        }

        private static bool IsOnlyDigitsAndUnderscores(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '_' && (c < '0' || c > '9')) return false;
            }
            return true;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
