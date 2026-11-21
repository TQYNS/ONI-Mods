// Buildings/AutoMiner/AutoMinerText.cs — 采集侧屏文案（独立 LocString 键，不触碰生成器维护的 Strings.cs）

using System;

namespace MoreFood_KFC
{
    internal static class AutoMinerText
    {
        // 标签随模式动态切换：默认挖矿 → ModeMining，切换后 → ModeHarvest
        internal static readonly string ModeMining = Resolve("STRINGS.MOREFOOD.AUTOMINER.MODE_MINING", "挖矿模式");
        internal static readonly string ModeHarvest = Resolve("STRINGS.MOREFOOD.AUTOMINER.MODE_HARVEST", "采集模式");
        // [保留兼容] 早期固定的单一标签键，现不再作为默认标签使用
        internal static readonly string ModeLabel = Resolve("STRINGS.MOREFOOD.AUTOMINER.MODE", "采集模式");
        internal static readonly string Mining = Resolve("STRINGS.MOREFOOD.AUTOMINER.MINING", "挖矿");
        internal static readonly string Harvest = Resolve("STRINGS.MOREFOOD.AUTOMINER.HARVEST", "采集");
        internal static readonly string Title = Resolve("STRINGS.MOREFOOD.AUTOMINER.TITLE", "自动采矿机 · 模式");
        internal static readonly string ModeTooltip = Resolve("STRINGS.MOREFOOD.AUTOMINER.TOOLTIP",
            "切换模式：挖矿=原生采矿逻辑；采集=自动收割范围内成熟植物（果实留原地）");

        private static string Resolve(string key, string fallback)
        {
            try
            {
                string s = Strings.Get(key);
                if (string.IsNullOrEmpty(s) || s.StartsWith("MISSING")) return fallback;
                return s;
            }
            catch (Exception) { return fallback; }
        }
    }
}
