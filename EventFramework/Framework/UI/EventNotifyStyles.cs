// EventNotifyStyles.cs — 事件框架 · 【通知外观：工厂 + 集中注册】

using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public enum EventStatusStyle { plain, alert }

    public sealed class EventNotifyStyle
    {
        // 档位键（purple / yellow / blue / red / alert，其他作者可自定义）
        public string tier { get; set; } = null;

        // 事件通知：按钮底色
        public Color bg { get; set; } = Color.white;

        // 事件通知：文字 + 图标前景色
        public Color fg { get; set; } = Color.white;

        // 事件通知：图标（原版 NotificationScreen 的 sprite 字段名）；空 = 不改
        public string icon { get; set; } = null;

        // 常驻状态：文字 + 图标色
        public Color statusFg { get; set; } = new Color(0.62f, 0.84f, 1.00f, 1f);

        // 常驻状态：前置图标。只能填原版真实存在的 8 个之一：
        public string statusIcon { get; set; } = "icon_normal";

        // 常驻状态：文本模板，{name} 会被替换成事件名；空 = 直接用事件名
        public string statusText { get; set; } = null;

        // 常驻状态：文本加粗（富文本 <b>）
        public bool statusBold { get; set; } = false;

        // 常驻状态形态：plain = 普通通知条 / alert = 带倒计时的状态行
        public EventStatusStyle statusStyle { get; set; } = EventStatusStyle.plain;

        // 常驻状态：【外观注册键】—— 优先于 statusStyle
        public string customKey { get; set; } = null;

        // 事件通知的排序位置（null = 按 notification.level 推断）。
        public NotificationType? sort { get; set; } = null;

        // 常驻状态的排序位置（null = Tutorial(5)，即沉在原版参考通知那一档）
        public NotificationType? statusSort { get; set; } = null;

        // 备注（仅日志/文档用）
        public string note { get; set; } = null;
    }

    // 按参数自由组装一条样式；没给的参数一律走 EventNotifyStyle 的默认值
    public static class EventNotifyStyleFactory
    {
        // 常驻状态的默认排序档。DuplicantThreatening(7)：沉在 Tutorial(5) 那两条原版参考通知下面。
        public const NotificationType DEFAULT_STATUS_SORT = NotificationType.DuplicantThreatening;

        public static EventNotifyStyle Make(
            string tier,
            Color? bg = null,
            Color? fg = null,
            string icon = null,
            Color? statusFg = null,
            string statusIcon = null,
            string statusText = null,
            bool statusBold = false,
            EventStatusStyle statusStyle = EventStatusStyle.plain,
            NotificationType? sort = null,
            NotificationType? statusSort = null,
            string customKey = null,           // ★v4.0
            string note = null)
        {
            var s = new EventNotifyStyle { tier = tier, note = note };

            if (bg.HasValue) s.bg = bg.Value;
            if (fg.HasValue) s.fg = fg.Value;
            if (!string.IsNullOrEmpty(icon)) s.icon = icon;
            if (statusFg.HasValue) s.statusFg = statusFg.Value;
            if (!string.IsNullOrEmpty(statusIcon)) s.statusIcon = statusIcon;
            if (!string.IsNullOrEmpty(statusText)) s.statusText = statusText;

            s.statusBold = statusBold;
            s.statusStyle = statusStyle;
            s.sort = CheckSort(tier, "sort", sort);
            s.statusSort = CheckSort(tier, "statusSort", statusSort);
            if (!string.IsNullOrEmpty(customKey)) s.customKey = customKey;   // ★v4.0
            return s;
        }

        // 排序档校验：Messages / Custom 会被原版特殊对待（换容器 / 查 prefab），
        public static NotificationType? CheckSort(string tier, string field, NotificationType? t)
        {
            if (!t.HasValue) return null;

            if (t.Value == NotificationType.Messages)
            {
                Debug.LogWarning("[MoreFood/Event][NotifyStyle] 档位 '" + tier + "' 的 " + field
                                 + " 不能用 Messages（原版会把它放进 MessagesFolder）→ 忽略，走默认");
                return null;
            }
            if (t.Value == NotificationType.Custom)
            {
                Debug.LogWarning("[MoreFood/Event][NotifyStyle] 档位 '" + tier + "' 的 " + field
                                 + " 不能用 Custom（原版要查 customNotificationPrefabs，查不到会空引用）→ 忽略，走默认");
                return null;
            }
            return t;
        }

        // 排序档的可读序（供日志 / 文档；值越大越靠下）
        public static string SortDescribe(NotificationType? t)
        {
            return t.HasValue ? (t.Value + "(" + (int)t.Value + ")") : "默认";
        }
    }

    public static class EventNotifyStyles
    {
        // 通知标记前缀；完整值 = eventfw.event.<档位>[.status]
        public const string MARK = "eventfw.event";

        // 常驻状态行的标记后缀
        public const string STATUS_MARK_TAIL = "status";

        private static readonly List<EventNotifyStyle> _styles = new List<EventNotifyStyle>();
        // 内置样式是否已注册
        private static bool _builtinsDone;

        public static List<EventNotifyStyle> All() { return _styles; }

        public static int Count { get { return _styles.Count; } }

        // 生成该档位的「标记」串（挂在通知对象上用于换外观）
        public static string MarkFor(string tier)
        {
            return string.IsNullOrEmpty(tier) ? null : MARK + "." + tier;
        }

        // 生成该档位状态行的标记串
        public static string MarkForStatus(string tier)
        {
            return string.IsNullOrEmpty(tier) ? null : MARK + "." + tier + "." + STATUS_MARK_TAIL;
        }

        // 按档位取外观样式（先确保内置档位已注册）；未知档位返回 null
        public static EventNotifyStyle Get(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return null;
            EnsureBuiltins();

            for (int i = 0; i < _styles.Count; i++)
            {
                var s = _styles[i];
                if (s != null && s.tier == tier) return s;
            }
            return null;
        }

        // 集中注册：一次登记一整批（同名覆盖）
        public static int Register(params EventNotifyStyle[] styles)
        {
            int n = 0;
            if (styles == null) return 0;

            for (int i = 0; i < styles.Length; i++)
            {
                var s = styles[i];
                if (s == null || string.IsNullOrEmpty(s.tier))
                {
                    Debug.LogWarning("[MoreFood/Event][NotifyStyle] Register：tier 为空，忽略");
                    continue;
                }

                bool replaced = false;
                for (int j = 0; j < _styles.Count; j++)
                {
                    if (_styles[j] != null && _styles[j].tier == s.tier)
                    {
                        _styles[j] = s;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced) _styles.Add(s);
                n++;
            }
            return n;
        }

        // 注销某档位的外观样式
        public static void Unregister(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return;
            for (int i = 0; i < _styles.Count; i++)
            {
                if (_styles[i] != null && _styles[i].tier == tier) { _styles.RemoveAt(i); return; }
            }
        }

        public static void Clear() { _styles.Clear(); _builtinsDone = false; }

        // ★ 内置档位清单（唯一入口：加档位/改外观都在这里，全部走工厂组装）
        public static List<EventNotifyStyle> BuiltinTable()
        {
            return new List<EventNotifyStyle>
            {
                EventNotifyStyleFactory.Make("purple", note: "紫（剧情 / 特殊）",
                    bg: new Color(0.34f, 0.22f, 0.46f, 1f),
                    fg: new Color(0.86f, 0.66f, 1.00f, 1f),
                    icon: "icon_event",
                    statusText: "{name}（进行中）", statusBold: true),

                EventNotifyStyleFactory.Make("yellow", note: "黄（警告）",
                    bg: new Color(0.44f, 0.37f, 0.15f, 1f),
                    fg: new Color(1.00f, 0.85f, 0.36f, 1f),
                    icon: "icon_warning",
                    statusText: "{name}（进行中）", statusBold: true),

                EventNotifyStyleFactory.Make("blue", note: "蓝（信息）",
                    bg: new Color(0.20f, 0.32f, 0.50f, 1f),
                    fg: new Color(0.56f, 0.80f, 1.00f, 1f),
                    icon: "icon_event",
                    statusText: "{name}（进行中）", statusBold: true),

                EventNotifyStyleFactory.Make("red", note: "红（严重）",
                    bg: new Color(0.45f, 0.18f, 0.18f, 1f),
                    fg: new Color(1.00f, 0.56f, 0.50f, 1f),
                    icon: "icon_bad",
                    statusText: "{name}（进行中）", statusBold: true),

                // ：alert 档位改为走【灾星外观】—— 与"事件"图标/底色一并保留，
                EventNotifyStyleFactory.Make("alert", note: "灾星/倒计时（走灾星外观）",
                    bg: new Color(0.52f, 0.10f, 0.10f, 1f),
                    fg: new Color(1.00f, 0.42f, 0.36f, 1f),
                    icon: "icon_bad",
                    statusFg: new Color(1.00f, 0.45f, 0.40f, 1f),
                    statusIcon: "icon_bad",
                    statusText: "{name}（进行中）", statusBold: true,
                    statusStyle: EventStatusStyle.alert,
                    customKey: EventCustomNotifyRegistry.DISASTER),   // ★ 新增
            };
        }

        // 注册全部通知样式（幂等；返回注册条数）
        public static int RegisterAll()
        {
            return Register(BuiltinTable().ToArray());
        }

        public static void EnsureBuiltins()
        {
            if (_builtinsDone) return;
            _builtinsDone = true;

            try
            {
                int n = RegisterAll();
                Debug.Log("[MoreFood/Event][NotifyStyle] 外观档位表已就绪：注册 " + n + " 档 → " + Describe());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][NotifyStyle] 内置档位注册异常: " + e);
            }
        }

        // level → 档位（不写 tier 时的默认映射；紫需要显式指定）
        public static string DefaultTierFor(EventNotifyLevel lv)
        {
            switch (lv)
            {
                case EventNotifyLevel.critical: return "red";
                case EventNotifyLevel.warning: return "yellow";
                case EventNotifyLevel.hidden: return null;
                default: return "blue";
            }
        }

        // 档位 → 通知排序类型（供非事件实例的通道，如 TradeUi 行为回调通知，套档位外观时决定排序位置）
        //   优先用档位显式配的 sort；未配则按档位默认（与 DefaultTierFor 的 level 推断一致）
        public static NotificationType TypeForTier(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return NotificationType.Good;

            var st = Get(tier);
            if (st != null && st.sort.HasValue) return st.sort.Value;

            switch (tier)
            {
                case "red": return NotificationType.Bad;
                case "yellow": return NotificationType.BadMinor;
                case "purple": return NotificationType.Event;
                case "alert": return NotificationType.Bad;
                default: return NotificationType.Neutral;   // blue / 信息
            }
        }

        // 解析该通知该用的档位键
        public static string ResolveTier(EventNotificationBlock blk)
        {
            if (blk == null) return null;
            return !string.IsNullOrEmpty(blk.tier) ? blk.tier : DefaultTierFor(blk.level);
        }

        // 常驻状态该用的档位（status_tier 优先，其次跟事件通知同档）
        public static string ResolveStatusTier(EventNotificationBlock blk)
        {
            if (blk == null) return null;
            if (!string.IsNullOrEmpty(blk.status_tier)) return blk.status_tier;
            return ResolveTier(blk);
        }

        // 按通知块解析出应套用的外观样式
        public static EventNotifyStyle Resolve(EventNotificationBlock blk)
        {
            return Get(ResolveTier(blk));
        }

        // ★ 排序位置解析 —— **每条通知单独控**，优先级：本条 yaml > 档位 > 默认。
        public static NotificationType ResolveSort(EventNotificationBlock blk, EventNotifyStyle style,
                                                   NotificationType fallback)
        {
            string tier = style != null ? style.tier : (blk != null ? blk.tier : "?");

            var t = EventNotifyStyleFactory.CheckSort(tier ?? "?", "sort", blk != null ? blk.sort : null);
            if (t.HasValue) return t.Value;

            if (style != null && style.sort.HasValue) return style.sort.Value;
            return fallback;
        }

        // ★ 常驻状态的排序位置 —— 优先级：本条 yaml(status_sort) > 档位(statusSort) > 默认
        public static NotificationType ResolveStatusSort(EventNotificationBlock blk, EventNotifyStyle style)
        {
            string tier = style != null ? style.tier : (blk != null ? blk.status_tier : "?");

            var t = EventNotifyStyleFactory.CheckSort(tier ?? "?", "status_sort", blk != null ? blk.status_sort : null);
            if (t.HasValue) return t.Value;

            if (style != null && style.statusSort.HasValue) return style.statusSort.Value;
            return EventNotifyStyleFactory.DEFAULT_STATUS_SORT;
        }

        // 按档位组装常驻状态的最终文本（模板替换 + 加粗）
        public static string BuildStatusText(EventNotifyStyle style, string eventName)
        {
            string s = eventName;
            if (style != null && !string.IsNullOrEmpty(style.statusText))
                s = style.statusText.Replace("{name}", eventName ?? "");

            if (style != null && style.statusBold && !string.IsNullOrEmpty(s))
                s = "<b>" + s + "</b>";

            return s;
        }

        // 状态行【外观注册键】解析：
        public static string ResolveStatusCustom(EventNotificationBlock blk, EventNotifyStyle style)
        {
            if (blk != null && !string.IsNullOrEmpty(blk.status_custom)) return blk.status_custom;
            if (style != null && !string.IsNullOrEmpty(style.customKey)) return style.customKey;
            return null;
        }

        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("共 ").Append(_styles.Count).Append(" 个档位（");
            for (int i = 0; i < _styles.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(_styles[i] != null ? _styles[i].tier : "?");
            }
            sb.Append("）");
            return sb.ToString();
        }
    }
}
