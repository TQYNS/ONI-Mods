// MFLog.cs — 宿主统一日志门面：Error/Warn 恒开，Info/Verbose 随设置页三选一；异常一律带堆栈

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 日志级别（宿主四档；Verbose 只进环形缓冲）
    public enum Level { Off = 0, Error, Warn, Info, Verbose }

    // 日志分区（映射到固定前缀）
    public enum LogCat { General, Event, Space, Drink, Combat, Trade, UI, Audio, Codex, Food, Buff, Save, Patch }

    // 统一日志门面：先判级别再格式化；异常一律带堆栈
    public static class MFLog
    {
        // 0 精简（E/W）/ 1 标准（+Info）/ 2 诊断（+Verbose）
        static int verbosity = 1;

        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static readonly object gate = new object();
        static int crossThread;

        // 会话标识：仅用于 Dump 抬头，便于区分多次启动
        static readonly string sessionId = System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        // 读一次设置页（重启生效）；在 OnLoad 早期调用
        public static void Init()
        {
            try
            {
                var o = ModOptions.Current;
                verbosity = o.LogDiagnostic ? 2 : (o.LogInfo ? 1 : 0);
            }
            catch { verbosity = 1; }
        }

        // 级别是否输出：Error/Warn 恒开；Info 标准起；Verbose 仅诊断
        public static bool On(Level lv)
        {
            switch (lv)
            {
                case Level.Error:
                case Level.Warn: return true;
                case Level.Info: return verbosity >= 1;
                case Level.Verbose: return verbosity >= 2;
                default: return false;
            }
        }

        public static void Info(LogCat cat, string fmt, params object[] a)
        {
            if (verbosity < 1) return;
            Write(Level.Info, cat, Format(fmt, a));
        }

        public static void Warn(LogCat cat, string fmt, params object[] a)
        {
            Write(Level.Warn, cat, Format(fmt, a));
        }

        // 同 key 只打第一条（带堆栈的异常走 Exc 的同名重载）
        public static void Warn(string onceKey, LogCat cat, string fmt, params object[] a)
        {
            if (First(onceKey)) Warn(cat, fmt, a);
        }

        // 诊断细节：只进环形缓冲，不上控制台
        public static void Verbose(LogCat cat, string fmt, params object[] a)
        {
            if (verbosity < 2) return;
            Ring(Level.Verbose, cat, Format(fmt, a));
        }

        // 诊断细节带异常：保留类型、Message、内层与前 5 帧
        public static void Verbose(LogCat cat, Exception e, string fmt, params object[] a)
        {
            if (verbosity < 2) return;
            string head = Format(fmt, a);
            Ring(Level.Verbose, cat, e == null ? head : head + " | " + FrameText(e));
        }

        // 热路径计数：只加计数，退出时汇总一行
        public static void TraceAccum(string key) { Count(key); }

        private const int RingSize = 256;
        private static readonly string[] ring = new string[RingSize];
        private static int ringAt;
        private static int ringCount;

        static void Ring(Level lv, LogCat cat, string msg)
        {
            try
            {
                string line = "[" + System.DateTime.UtcNow.ToString("HH:mm:ss.fff") + "] [" + Letter(lv) + "/" + cat + "] " + msg;
                lock (gate)
                {
                    ring[ringAt] = line;
                    ringAt = (ringAt + 1) % RingSize;
                    if (ringCount < RingSize) ringCount++;
                }
            }
            catch
            {
                // expected: 留痕失败不得影响调用方
            }
        }

        // 手动导出：环形缓冲最近 256 条 + 计数表；只在显式调用时写盘
        public static string Dump()
        {
            try
            {
                string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "MoreFood_logs");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, "dump-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                sb.AppendLine("# MoreFood 运行留痕 session=" + sessionId + " " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("-- 计数表（key / 次数）--");
                lock (gate)
                {
                    foreach (System.Collections.Generic.KeyValuePair<string, int> kv in counts)
                    {
                        sb.AppendLine("  " + kv.Value + "  " + kv.Key);
                    }
                    sb.AppendLine("-- 环形缓冲（最近 " + ringCount + " 条，按时间正序）--");
                    int start = ringCount < RingSize ? 0 : ringAt;
                    for (int i = 0; i < ringCount; i++)
                    {
                        sb.AppendLine(ring[(start + i) % RingSize]);
                    }
                }

                System.IO.File.WriteAllText(file, sb.ToString(), new System.Text.UTF8Encoding(false));
                return file;
            }
            catch
            {
                // expected: 导出失败返回 null，由调用方提示
                return null;
            }
        }

        // 异常：带堆栈与内层；栈帧截断到前 5 帧
        public static void Exc(LogCat cat, Exception e, string fmt, params object[] a)
        {
            string head = Format(fmt, a);
            Write(Level.Error, cat, e == null ? head : head + " | " + FrameText(e));
        }

        // 同 key 只打第一条（带堆栈）
        public static void Exc(string onceKey, LogCat cat, Exception e, string fmt, params object[] a)
        {
            if (First(onceKey)) Exc(cat, e, fmt, a);
        }

        // 热路径异常：首错打一次堆栈，之后只计数，不做字符串插值
        public static void HotFail(string key, Exception e)
        {
            if (First("hot:" + key))
                Write(Level.Error, LogCat.General, "热路径异常 key=" + key + " | " + (e == null ? "(null)" : FrameText(e)));
        }

        // 类型 + Message + 前 5 帧；内层异常重新计帧，逐层保留
        static string FrameText(Exception e)
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

        // 汇总一行；无热路径异常时什么都不打
        public static void HotFlush()
        {
            int t = System.Threading.Interlocked.Exchange(ref crossThread, 0);
            if (t <= 0) return;
            Write(Level.Warn, LogCat.General, "热路径异常汇总 跨线程计数=" + t);
        }

        // 跨线程只准调这一个：原子自增，不碰字典
        public static void CountFromAnyThread() { System.Threading.Interlocked.Increment(ref crossThread); }

        // 协程兜底：整段包 try，异常记一次并干净退出
        public static IEnumerator SafeRoutine(IEnumerator inner, string key)
        {
            while (true)
            {
                object cur;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    cur = inner.Current;
                }
                catch (Exception e) { HotFail(key, e); yield break; }
                yield return cur;
            }
        }

        // key 首次出现返回 true
        static bool First(string key)
        {
            lock (gate)
            {
                int n;
                counts.TryGetValue(key, out n);
                counts[key] = n + 1;
                return n == 0;
            }
        }

        static void Count(string key)
        {
            lock (gate)
            {
                int n;
                counts.TryGetValue(key, out n);
                counts[key] = n + 1;
            }
        }

        static string Format(string fmt, object[] a)
        {
            return (a == null || a.Length == 0) ? fmt : string.Format(fmt, a);
        }

        // 实际输出：毫秒时间戳 + 级别字母 + 分区前缀
        static void Write(Level lv, LogCat cat, string msg)
        {
            string line = "[" + System.DateTime.UtcNow.ToString("HH:mm:ss.fff") + "] [" + Letter(lv) + "/" + cat + "] " + msg;
            switch (lv)
            {
                // 工程铁律：禁止 Debug.LogError —— 玩家侧 LogCatcher 会抓 LogError 连栈并判致命（崩溃报告）。
                // Error 仍以 [E/分区] 前缀输出并带完整堆栈，只把 Unity 控制台级别降为 Warning：
                // 异常照样看得见，但任何 MFLog.Exc / HotFail / SafeRoutine 都不再让游戏崩。
                case Level.Error: UnityEngine.Debug.LogWarning(line); break;
                case Level.Warn: UnityEngine.Debug.LogWarning(line); break;
                default: UnityEngine.Debug.Log(line); break;
            }
        }

        static string Letter(Level lv)
        {
            switch (lv)
            {
                case Level.Error: return "E";
                case Level.Warn: return "W";
                case Level.Info: return "I";
                case Level.Verbose: return "V";
                default: return "-";
            }
        }
    }
}
