// NarratorRules.cs — 事件框架 · 三位叙事者的状态生成器（见 叙事者.md §3.3）

using UnityEngine;

namespace EventFramework
{
    // 活跃 / 平静两状态；每条 Rule 输出长期活跃占比 d 与趋势进度 a(t)
    public enum NarratorState
    {
        Active,
        Calm,
    }

    public delegate NarratorState NarratorRule(float t, out float activity, out float trend);

    public static class NarratorRules
    {
        // 卡桑德拉趋势走满所需的周期数
        public const float CassandraTrendCycles = 40f;
        // 菲比的相位周期与活跃占空
        public const float PhoebePeriodCycles = 12f;
        public const float PhoebeDuty = 0.2f;

        // 趋势确定、区间窄：随周期推进，活跃占比与间隔一起收紧
        public static NarratorState Cassandra(float t, out float activity, out float trend)
        {
            activity = trend = Mathf.Clamp01(t / CassandraTrendCycles);
            return UnityEngine.Random.value < activity ? NarratorState.Active : NarratorState.Calm;
        }

        // 每轮独立掷，无记忆、无趋势
        public static NarratorState Randy(float t, out float activity, out float trend)
        {
            activity = 0.5f;
            trend = 1f;
            return UnityEngine.Random.value < 0.5f ? NarratorState.Active : NarratorState.Calm;
        }

        // 固定相位：每 12 周期的前 2.4 周期活跃
        public static NarratorState Phoebe(float t, out float activity, out float trend)
        {
            activity = PhoebeDuty;
            trend = 1f;
            float phase = t % PhoebePeriodCycles;
            return phase < PhoebePeriodCycles * PhoebeDuty ? NarratorState.Active : NarratorState.Calm;
        }
    }
}
