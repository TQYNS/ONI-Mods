using System;
using System.Reflection;
using EventFramework.Distribution.Contracts;

namespace EventFramework.Distribution.Versioning
{
    // 读程序集内版本，用于校验与文件名是否一致
    internal static class FrameworkVersion
    {
        internal static bool TryReadAssemblyVersion(string path, out Version ver)
        {
            ver = null;
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                AssemblyName an = AssemblyName.GetAssemblyName(path);
                if (an == null) return false;
                ver = an.Version;
                return ver != null;
            }
            catch (Exception e)
            {
                DistributionLog.WarnOnce("asm-ver-" + path, "读取程序集版本失败：" + path + " → " + e);
                return false;
            }
        }
    }
}
