// EventMutexChecker.cs — 事件框架 · 【互斥检查】

using UnityEngine;

namespace EventFramework
{
    public static class EventMutexChecker
    {
        // def 是否被当前活跃事件 cur 互斥挡住
        public static bool IsBlocked(EventDef def, EventInstance cur)
        {
            if (def == null) return false;
            if (cur == null) return false;

            var curDef = cur.def ?? EventRegistry.Get(cur.templateId);
            if (curDef == null) return false;

            // 方向 1：候选的 mutex_running 列表里有当前事件
            if (Contains(curDef.id, MutexOf(def)))
            {
                Log(def.id, curDef.id, "候选.mutex_running 含当前");
                return true;
            }

            // 方向 2：当前事件的 mutex_running 列表里有候选
            var curMutex = MutexOf(curDef);
            if (curMutex != null && Contains(def.id, curMutex))
            {
                Log(def.id, curDef.id, "当前.mutex_running 含候选");
                return true;
            }

            return false;
        }

        // 并发互斥列表（已迁到 condition.mutex_running）
        private static System.Collections.Generic.List<string> MutexOf(EventDef d)
        {
            return d != null && d.condition != null ? d.condition.mutex_running : null;
        }

        private static bool Contains(string id, System.Collections.Generic.List<string> list)
        {
            if (string.IsNullOrEmpty(id) || list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == id) return true;
            return false;
        }

        private static void Log(string candidate, string current, string why)
        {
            Debug.Log("[MoreFood/Event][Mutex] '" + candidate + "' 被 '" + current + "' 挡住（" + why + "）");
        }
    }
}
