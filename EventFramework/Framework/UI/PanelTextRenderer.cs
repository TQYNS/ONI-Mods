using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class PanelTextRenderer
    {
        // 本类日志前缀
        public const string LOG_TAG = "[MoreFood/Event][UI/Panel]";

        private static readonly HashSet<string> _warned =
            new HashSet<string>(System.StringComparer.Ordinal);

 // 主动换行转义：文本里写 `\n`（反斜杠 + n）⇒ 换成真正的换行。
        public static string ApplyEscapes(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            if (raw.IndexOf("\\n", System.StringComparison.Ordinal) < 0) return raw;
            return raw.Replace("\\n", "\n");
        }

        // 渲染面板文本：处理转义与 [xxx] 标记；ownerForWarn 仅用于告警前缀
        public static string Render(string raw, string ownerForWarn)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            raw = ApplyEscapes(raw);
            if (raw.IndexOf('[') < 0) return raw;

            var sb = new System.Text.StringBuilder(raw.Length);
            int i = 0;
            while (i < raw.Length)
            {
                char c = raw[i];
                if (c != '[')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                int close = raw.IndexOf(']', i + 1);
                if (close < 0)
                {
                    sb.Append(raw, i, raw.Length - i);
                    break;
                }

                string id = raw.Substring(i + 1, close - i - 1).Trim();
                if (id.Length == 0)
                {
                    WarnOnce("empty:" + ownerForWarn,
                             ownerForWarn + " 中的 '[]' 是空图片 id → 跳过");
                }
                else
                {
                    string token = ResolveToken(id, ownerForWarn);
                    if (token != null) sb.Append(token);
                }

                i = close + 1;
            }
            return ApplyEscapes(sb.ToString());
        }

        // 把解析出的物品列表拼成展示文本；无有效物品返回 null
        public static string DescribeItems(ItemParseResult res, string ownerForWarn)
        {
            if (res == null || res.valid.Count == 0) return null;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < res.valid.Count; i++)
            {
                var s = res.valid[i];
                if (i > 0) sb.Append("  ");
                sb.Append(UiIconResolver.IconTag(s.id));
                sb.Append(ItemStackParser.DescribeEntry(s));
            }
            return sb.ToString();
        }

        public static void ClearCache()
        {
            _warned.Clear();
            UiIconResolver.ClearCache();
        }

        private static string ResolveToken(string id, string ownerForWarn)
        {
            string tag = UiIconResolver.IconTag(id);
            if (!string.IsNullOrEmpty(tag)) return tag;

            string name = ItemStackParser.ResolveDisplayName(id);
            if (!string.IsNullOrEmpty(name)
                && !string.Equals(name, id, System.StringComparison.Ordinal)) return name;

            WarnOnce("noicon:" + id + ":" + ownerForWarn,
                     ownerForWarn + " 中的图片 id '" + id + "' 未找到 → 跳过");
            return null;
        }

        private static void WarnOnce(string tag, string message)
        {
            if (_warned.Add(tag)) Debug.LogWarning(LOG_TAG + " " + message);
        }
    }
}
