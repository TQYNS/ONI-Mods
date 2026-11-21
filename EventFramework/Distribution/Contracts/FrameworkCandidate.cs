using System;

namespace EventFramework.Distribution.Contracts
{
    // 一个 Mod 携带的一份实现副本，版本取自文件名
    public sealed class FrameworkCandidate
    {
        public readonly string ModId;

        public readonly Version Ver;

        public readonly string FullPath;

        public FrameworkCandidate(string modId, Version ver, string fullPath)
        {
            ModId = modId;
            Ver = ver;
            FullPath = fullPath;
        }

        public override string ToString()
        {
            return "v" + (Ver == null ? "?" : Ver.ToString()) + " @ " + FullPath;
        }
    }
}
