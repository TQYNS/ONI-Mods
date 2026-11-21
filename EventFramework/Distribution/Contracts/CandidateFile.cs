using System;
using System.IO;

namespace EventFramework.Distribution.Contracts
{
    // 从文件名解析实现版本：EventFramework.Impl.<major>_<minor>_<patch>.dll
    internal static class CandidateFile
    {
        internal const string ImplPrefix = "EventFramework.Impl.";

        // 文件名即程序集简单名（不含扩展名）
        internal static string AssemblyNameOf(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return null;
                return Path.GetFileNameWithoutExtension(path);
            }
            catch (Exception e)
            {
                DistributionLog.WarnOnce("cand-asm-" + path, "文件名解析失败：" + path + " → " + e);
                return null;
            }
        }

        internal static bool TryParseVersion(string path, out Version ver)
        {
            ver = null;
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                string name = Path.GetFileNameWithoutExtension(path);
                if (name == null) return false;
                if (!name.StartsWith(ImplPrefix, StringComparison.OrdinalIgnoreCase)) return false;
                string[] parts = name.Substring(ImplPrefix.Length).Split('_');
                if (parts.Length != 3) return false;
                int major;
                int minor;
                int patch;
                if (!int.TryParse(parts[0], out major)) return false;
                if (!int.TryParse(parts[1], out minor)) return false;
                if (!int.TryParse(parts[2], out patch)) return false;
                if (major < 0 || minor < 0 || patch < 0) return false;
                ver = new Version(major, minor, patch, 0);
                return true;
            }
            catch (Exception e)
            {
                DistributionLog.WarnOnce("cand-parse-" + path, "文件名版本解析失败：" + path + " → " + e);
                return false;
            }
        }
    }
}
