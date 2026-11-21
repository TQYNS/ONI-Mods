// NarratorCurve.cs — 事件框架 · 两状态区间抽样与负面占比翻译层（见 叙事者.md §3.1 / §3.6）

using UnityEngine;

namespace EventFramework
{
    public static class NarratorCurve
    {
        // 全局喘息地板（秒），不暴露给面板
        public const float MinGapSeconds = 240f;
        // 负面事件供给上限（次/周期），由 35 个 yaml 的冷却统计得出
        public const float NegativeSupplyPerCycle = 0.133f;
        // 供给钳制留出的余量
        public const float SupplySafety = 0.9f;

        // 一次调用同时给出 Δt 与本次闹钟的诊断量，避免重复掷色子
        public static float Sample(float t, float i0, float w, float r,
                                   NarratorRule rule, float lEnd,
                                   out NarratorState state, out float iMean,
                                   out float activity, out float trend)
        {
            state = rule(t, out activity, out trend);
            iMean = Mathf.Max(1f, i0 * (1f - (1f - lEnd) * trend));
            float iAct = iMean / Mathf.Max(0.01f, activity + (1f - activity) * r);
            float center = state == NarratorState.Active ? iAct : r * iAct;
            float lo = center * (1f - w);
            float hi = center * (1f + w);
            float dt = lo + (hi - lo) * UnityEngine.Random.value;
            return Mathf.Clamp(dt, MinGapSeconds, 3f * iMean);
        }

        // 面板派生显示用：同一趋势下的平均间隔
        public static float MeanInterval(float i0, float lEnd, float trend)
        {
            return Mathf.Max(1f, i0 * (1f - (1f - lEnd) * trend));
        }

        // 活跃 / 平静两个区间的中心值（秒）
        public static void Centers(float iMean, float activity, float w, float r,
                                   out float active, out float calm,
                                   out float activeLo, out float activeHi,
                                   out float calmLo, out float calmHi)
        {
            float iAct = iMean / Mathf.Max(0.01f, activity + (1f - activity) * r);
            active = iAct;
            calm = r * iAct;
            activeLo = active * (1f - w);
            activeHi = active * (1f + w);
            calmLo = calm * (1f - w);
            calmHi = calm * (1f + w);
        }

        // 事件率（次/周期）
        public static float EventRatePerCycle(float iMean)
        {
            return 600f / Mathf.Max(1f, iMean);
        }

        // 负面目标占比随趋势插值；s0 == s1 时不随时间变化
        public static float NegativeTarget(float trend, float s0, float s1)
        {
            return Mathf.Clamp01(s0 + (s1 - s0) * trend);
        }

        // 供给上限占比：供给量除以本时刻的事件率
        public static float NegativeCap(float iMean)
        {
            float rate = EventRatePerCycle(iMean);
            return rate <= 0f ? 1f : Mathf.Clamp01(NegativeSupplyPerCycle / rate);
        }

        // 实际用于翻译的目标占比（含供给钳制）
        public static float NegativeUse(float target, float cap)
        {
            return Mathf.Clamp(Mathf.Min(target, SupplySafety * cap), 0f, 0.95f);
        }

        // s(t) → 现有「每个候选事件各乘一次」的负面权重；缺负面事件自动降级
        public static float NegativeWeight(float sUse, int nPos, int nNeu, int nNeg)
        {
            if (nNeg <= 0 || sUse <= 0f) return 0f;
            float denom = (1f - sUse) * nNeg;
            if (denom <= 0f) return 0f;
            return sUse * (nPos + nNeu) / denom;
        }
    }
}
