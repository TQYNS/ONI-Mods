using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework.Distribution.Contracts
{
    // 分发层统一日志，Contracts 与 Impl 共用；不依赖宿主的 MFLog
    // 铁律：全类禁止 Debug.LogError（玩家侧 LogCatcher 判致命），连 catch 兜底也只走 LogWarning。
    public static class DistributionLog
    {
        private const string Prefix = "[MoreFood/EF] ";

        private static readonly HashSet<string> Warned = new HashSet<string>();

        private static readonly List<string> Collected = new List<string>();

        public static void Info(string msg)
        {
            try
            {
                Debug.Log(Prefix + msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Prefix + "日志输出失败：" + e);
            }
        }

        public static void Warn(string msg)
        {
            try
            {
                Debug.LogWarning(Prefix + msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Prefix + "日志输出失败：" + e);
            }
        }

        // 带异常对象的告警：保留堆栈（前 5 帧）；配置错误仍走 Warning，不触发 LogCatcher
        public static void Exc(string msg, Exception e)
        {
            try
            {
                Debug.LogWarning(Prefix + msg + (e == null ? "" : " | ex=" + FrameText(e)));
            }
            catch (Exception inner)
            {
                Debug.LogWarning(Prefix + "日志输出失败：" + inner);
            }
        }

        // 同一 key 只打一次，仍带完整堆栈（热路径与逐帧回调专用）
        public static void ExcOnce(string key, string msg, Exception e)
        {
            try
            {
                if (!string.IsNullOrEmpty(key))
                {
                    if (Warned.Contains(key)) return;
                    Warned.Add(key);
                }
                Debug.LogWarning(Prefix + msg + (e == null ? "" : " | ex=" + FrameText(e)));
            }
            catch (Exception inner)
            {
                Debug.LogWarning(Prefix + "日志输出失败：" + inner);
            }
        }

        // 类型 + Message + 前 5 帧；内层异常重新计帧，逐层保留
        private static string FrameText(Exception e)
        {
            string s = e.ToString();
            string[] lines = s.Split('\n');
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            int frames = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string ln = lines[i];
                if (ln.TrimStart().StartsWith("at "))
                {
                    if (frames >= 5) continue;
                    frames++;
                }
                else if (ln.IndexOf("End of inner exception", StringComparison.Ordinal) >= 0)
                {
                    frames = 0;
                }
                sb.Append(ln).Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        // 同一 key 只打一次，避免加载期刷屏
        public static void WarnOnce(string key, string msg)
        {
            try
            {
                if (string.IsNullOrEmpty(key) || Warned.Contains(key)) return;
                Warned.Add(key);
                Debug.LogWarning(Prefix + msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Prefix + "日志输出失败：" + e);
            }
        }

        // 循环内只收集，循环外汇总，避免逐条刷屏
        public static void Collect(string msg)
        {
            try
            {
                Collected.Add(msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Prefix + "收集失败：" + e);
            }
        }

        public static void FlushCollected(string title)
        {
            try
            {
                if (Collected.Count == 0) return;
                Debug.LogWarning(Prefix + title + "：" + Collected.Count + " 条 → "
                    + string.Join(" | ", Collected.ToArray()));
                Collected.Clear();
            }
            catch (Exception e)
            {
                Debug.LogWarning(Prefix + "汇总失败：" + e);
            }
        }
    }
}
