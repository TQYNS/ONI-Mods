using System;
using EventFramework.Distribution.Contracts;

namespace EventFramework.Distribution.Versioning
{
    // 赢家文件校验：程序集内版本必须与登记时的文件名版本一致
    internal static class CandidateScanner
    {
        internal static bool Validate(FrameworkCandidate c)
        {
            try
            {
                if (c == null || c.Ver == null || string.IsNullOrEmpty(c.FullPath)) return false;
                Version asmVer;
                if (!FrameworkVersion.TryReadAssemblyVersion(c.FullPath, out asmVer)) return false;
                if (asmVer.CompareTo(c.Ver) == 0) return true;
                DistributionLog.Warn("赢家版本不一致：文件名 v" + c.Ver + " / 程序集 v" + asmVer
                    + " @ " + c.FullPath);
                return false;
            }
            catch (Exception e)
            {
                DistributionLog.Warn("赢家校验失败：" + e);
                return false;
            }
        }
    }
}
