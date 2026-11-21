// TradeNotifyTier.cs — 贸易通知的档位映射（档位名 → 通知类型 / 排序标记）；见 交易.md A-30

using System;

namespace MoreFood_KFC
{
    internal static class TradeNotifyTier
    {
        // 与事件框架的档位标记同一前缀
        private const string MARK = "eventfw.event";

        // 生成该档位的标记串（空档位返回 null ⇒ 不挂标记）
        public static string MarkFor(string tier)
        {
            return string.IsNullOrEmpty(tier) ? null : MARK + "." + tier;
        }

        // 档位 → 通知排序类型（与事件框架的标准档位一致）
        public static NotificationType TypeForTier(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return NotificationType.Good;

            switch (tier)
            {
                case "red": return NotificationType.Bad;
                case "yellow": return NotificationType.BadMinor;
                case "purple": return NotificationType.Event;
                case "alert": return NotificationType.Bad;
                default: return NotificationType.Neutral;   // blue / 信息
            }
        }
    }
}
