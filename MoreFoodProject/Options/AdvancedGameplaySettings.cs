// 用途：本局高级设置的运行期状态与落地取值（主/子星图刷怪器），仅当前殖民地生效

using UnityEngine;

namespace MoreFood_KFC
{
    internal static class AdvancedGameplaySettings
    {
        // 与 combat/spawn/*.yaml 的内置默认保持一致（yaml 调整后需同步这里）
        internal const float MainIntervalDefault = 600f;   // [400,800] 中值
        internal const int MainSpawnCountDefault = 2;      // [1,2] 上限
        internal const int MainMaxMonstersDefault = 10;
        internal const float SubIntervalDefault = 450f;    // [300,600] 中值
        internal const int SubSpawnCountDefault = 1;       // [1,1]
        internal const int SubMaxMonstersDefault = 8;

        // 0 表示未设置，沿用 yaml 原值
        internal static float MainIntervalSeconds;
        internal static int MainSpawnCount;
        internal static int MainMaxMonsters;
        internal static float SubIntervalSeconds;
        internal static int SubSpawnCount;
        internal static int SubMaxMonsters;

        // 新一轮开局清回默认，保证设置只作用于本局
        internal static void Reset()
        {
            MainIntervalSeconds = 0f;
            MainSpawnCount = 0;
            MainMaxMonsters = 0;
            SubIntervalSeconds = 0f;
            SubSpawnCount = 0;
            SubMaxMonsters = 0;
        }

        internal static bool IsDefault()
        {
            return MainIntervalSeconds <= 0f && MainSpawnCount <= 0 && MainMaxMonsters <= 0
                && SubIntervalSeconds <= 0f && SubSpawnCount <= 0 && SubMaxMonsters <= 0;
        }

        // 按本局设置生成独立的抽取器配置；不改 CombatDb 的共享实例，读档重建也不会叠加
        internal static SpawnerCfg ApplySpawner(SpawnerCfg src)
        {
            if (src == null) return src;

            bool isSub = (src.id != null && src.id == "sub")
                      || (src.region != null && src.region == "sub");

            float interval = isSub ? SubIntervalSeconds : MainIntervalSeconds;
            int count = isSub ? SubSpawnCount : MainSpawnCount;
            int max = isSub ? SubMaxMonsters : MainMaxMonsters;
            if (interval <= 0f && count <= 0 && max <= 0) return src;

            var c = new SpawnerCfg
            {
                id = src.id,
                region = src.region,
                idleMin = src.idleMin,
                idleMax = src.idleMax,
                idleChance = src.idleChance,
                allowCrossRegion = src.allowCrossRegion,
            };

            if (max > 0)
            {
                c.maxMonsters = Mathf.Max(1, max);
            }
            else
            {
                c.maxMonsters = src.maxMonsters;
            }

            if (count > 0)
            {
                c.spawnCountMin = Mathf.Max(1, count);
                c.spawnCountMax = c.spawnCountMin;
            }
            else
            {
                c.spawnCountMin = src.spawnCountMin;
                c.spawnCountMax = src.spawnCountMax;
            }

            if (interval > 0f)
            {
                c.spawnIntervalMin = Mathf.Max(SpawnerCfg.MinSpawnIntervalSeconds, interval);
                c.spawnIntervalMax = c.spawnIntervalMin;
            }
            else
            {
                c.spawnIntervalMin = src.spawnIntervalMin;
                c.spawnIntervalMax = src.spawnIntervalMax;
            }
            return c;
        }
    }
}
