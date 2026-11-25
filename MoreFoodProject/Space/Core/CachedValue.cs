
namespace MoreFood_KFC
{
    // 通用缓存机制第二层：脏标记（单条缓存）
    internal sealed class CachedValue<T>
    {
        internal T value;
        internal int globalVersion;
        internal object[] roots;
        internal bool dirty = true;

        internal bool IsValid(int currentGlobalVersion)
        {
            if (dirty)
            {
                return false;
            }
            return globalVersion == currentGlobalVersion;
        }

        internal void MarkDirty()
        {
            dirty = true;
        }

        internal void Update(T newValue, int version, params object[] newRoots)
        {
            value = newValue;
            globalVersion = version;
            roots = newRoots;
            dirty = false;
        }

        // 根数据快照不一致 → 只失效受影响的那一条
        internal bool RootsChanged(object[] newRoots)
        {
            if (roots == null || newRoots == null || roots.Length != newRoots.Length)
            {
                return roots != newRoots;
            }
            for (int i = 0; i < roots.Length; i++)
            {
                if (!Equals(roots[i], newRoots[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
