// 用途：饮水值档案注册表，显示与结算的唯一查询口。
using System.Collections.Generic;

namespace MoreFood_KFC
{
    public static class ConsumeRegistry
    {
        private static readonly Dictionary<Tag, ConsumeProfile> _profiles =
            new Dictionary<Tag, ConsumeProfile>();

        // 登记档案；同一 Tag 重复登记时覆盖。
        public static void Register(Tag tag, ConsumeProfile profile)
        {
            if (profile == null) return;
            _profiles[tag] = profile;
        }

        // 查询档案；未登记返回 null。
        public static ConsumeProfile Find(Tag tag)
        {
            ConsumeProfile p;
            return _profiles.TryGetValue(tag, out p) ? p : null;
        }

        // 新会话/新档开始时清空，避免同进程残留。
        public static void ResetForSession()
        {
            _profiles.Clear();
        }
    }
}
