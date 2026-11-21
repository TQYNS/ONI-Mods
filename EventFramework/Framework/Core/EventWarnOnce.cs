// EventWarnOnce.cs — 事件框架 · 【同名只提醒一次的日志工具】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventWarnOnce
    {
        private static readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);

        // 同一个 tag 只打一次警告（tag 建议用 "模块:对象" 形式）
        public static void Warn(string tag, string message)
        {
            if (string.IsNullOrEmpty(tag)) { Debug.LogWarning(message); return; }
            if (_warned.Add(tag)) Debug.LogWarning(message);
        }

        // 清掉某个 tag 的"已提醒"记录，让它下次还能提醒
        public static void Reset(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            _warned.Remove(tag);
        }

        // 清掉某个前缀下所有 tag 的"已提醒"记录（框架进入新一轮 / 重置时用）
        public static void ResetPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return;

            List<string> hit = null;
            foreach (var k in _warned)
            {
                if (!k.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (hit == null) hit = new List<string>();
                hit.Add(k);
            }
            if (hit == null) return;
            for (int i = 0; i < hit.Count; i++) _warned.Remove(hit[i]);
        }

        public static void Clear()
        {
            _warned.Clear();
        }
    }
}
