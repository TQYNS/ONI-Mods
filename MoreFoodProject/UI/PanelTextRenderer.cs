// PanelTextRenderer.cs — 宿主自备的面板文本渲染（自事件框架的同名实现裁剪）
//
// 只保留宿主需要的部分：转义与 [xxx] 图片/名称标记解析。
// 框架版里的 DescribeItems（把物品组拼成文本）依赖事件奖励结构，宿主用不到，未搬。

using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class PanelTextRenderer
    {
        // 本类日志前缀
        private const string LOG_TAG = "[MoreFood/UI]";

        private static readonly HashSet<string> _warned =
            new HashSet<string>(System.StringComparer.Ordinal);

        // 主动换行转义：文本里写 `\n`（反斜杠 + n）⇒ 换成真正的换行
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

        public static void ClearCache()
        {
            _warned.Clear();
            UiIconResolver.ClearCache();
        }

        private static string ResolveToken(string id, string ownerForWarn)
        {
            string tag = UiIconResolver.IconTag(id);
            if (!string.IsNullOrEmpty(tag)) return tag;

            string name = ResolveDisplayName(id);
            if (!string.IsNullOrEmpty(name)
                && !string.Equals(name, id, System.StringComparison.Ordinal)) return name;

            WarnOnce("noicon:" + id + ":" + ownerForWarn,
                     ownerForWarn + " 中的图片 id '" + id + "' 未找到 → 跳过");
            return null;
        }

        // id → 显示名（原版物品从 prefab 的 KSelectable 取，查不到就返回 id）
        private static string ResolveDisplayName(string id)
        {
            try
            {
                var prefab = Assets.GetPrefab(new Tag(id));
                if (prefab != null)
                {
                    var sel = prefab.GetComponent<KSelectable>();
                    if (sel != null)
                    {
                        string name = sel.GetName();
                        if (!string.IsNullOrEmpty(name) && !name.StartsWith("MISSING"))
                            return name;
                    }
                }
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.UI, e, LOG_TAG + " 取 '" + id + "' 显示名失败");
            }
            return id;
        }

        private static void WarnOnce(string tag, string message)
        {
            if (_warned.Add(tag)) Debug.LogWarning(LOG_TAG + " " + message);
        }
    }
}
