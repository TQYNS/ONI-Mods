using System;
using EventFramework.Distribution.Contracts;
using EventFramework.Distribution.Patches;
using EventFramework.Distribution.Versioning;

namespace EventFramework.Distribution.Diagnostics
{
    // 加载期自检：每项独立 try，一项失败不影响其它
    internal static class DistributionGuard
    {
        // 应有补丁总数：8 生命周期 + 7 特性
        internal const int ExpectedPatches = LifecyclePatches.Expected + FeaturePatches.Expected;

        internal static void Check(FrameworkCandidate winner, int ok, int miss)
        {
            try
            {
                if (winner != null && !CandidateScanner.Validate(winner))
                {
                    DistributionLog.Warn("自检 赢家文件校验未通过，继续装配以便定位");
                }
            }
            catch (Exception e)
            {
                DistributionLog.Warn("自检 版本校验失败：" + e);
            }
            try
            {
                string label = (winner == null || winner.Ver == null)
                    ? "单 DLL" : "赢家 v" + winner.Ver;
                DistributionLog.Info("自检 " + label + " 补丁 " + ok + "/" + ExpectedPatches + "，跳过 " + miss);
                if (ok != ExpectedPatches)
                {
                    DistributionLog.Warn("自检 补丁条数不足：期望 " + ExpectedPatches
                        + " 条，实际 " + ok + " 条；按目标方法可能改名处理，不阻断启动");
                }
            }
            catch (Exception e)
            {
                DistributionLog.Warn("自检 补丁计数失败：" + e);
            }
        }
    }
}
