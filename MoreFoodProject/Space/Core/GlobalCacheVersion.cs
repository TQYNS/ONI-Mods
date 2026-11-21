
namespace MoreFood_KFC
{
    // ★ 通用缓存机制第一层：全局版本号（§M0）
    internal static class GlobalCacheVersion
    {
        internal const int VERSION = 1;
        internal const int TRADE_VERSION = 5;   // 修 items.yaml 语法错导致逐 ID 价全失效（整份回落 Tag 价）⇒ 货架缓存作废重算
        internal const int EVENT_VERSION = 1;
        internal const int STARMAP_VERSION = 1;
        internal const int COMBAT_VERSION = 1;
        // 奖励池新增「掉落组候选」缓存（key 前缀 g:），旧档池整块作废重扫一次
        internal const int LOOT_POOL_VERSION = 3;
    }
}
