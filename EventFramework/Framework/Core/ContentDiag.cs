// ContentDiag.cs — 框架侧内容加载诊断：记录「哪个 Mod 的哪个文件 / 哪一条 / 为什么被拒 / 怎么改」

using System;
using System.Collections.Generic;
using System.IO;
using EventFramework.Distribution.Contracts;

namespace EventFramework
{
    // 内容加载诊断收口（event / reward / sounds / load 各开一个域），只记录不改判定
    internal static class ContentDiag
    {
        private sealed class Scope
        {
            public string name;
            public List<string> rejects = new List<string>();
            public List<string> fixes = new List<string>();
        }

        private const int MaxDetail = 30;
        private static readonly Dictionary<string, Scope> scopes = new Dictionary<string, Scope>();

        // 开一个加载域；同域重复调用会重置
        internal static void Begin(string scope)
        {
            try
            {
                Scope s = new Scope();
                s.name = scope;
                scopes[scope] = s;
            }
            catch
            {
                // X8 预期：诊断器自身失败不得反过来影响加载
            }
        }

        // 整份文件被拒（读不到 / 空 / 语法或类型错 / 顶层结构缺）
        internal static void File(string scope, string path, string reason, string fix)
        {
            Add(scope, true, "文件 " + Short(path) + "：" + reason + Tail(fix));
        }

        // 某一条被跳过（没进内存表）
        internal static void Skip(string scope, string path, string item, string reason, string fix)
        {
            Add(scope, true, Short(path) + Pos(item) + "：" + reason + Tail(fix));
        }

        // 已加载但不会按预期生效
        internal static void Suspect(string scope, string path, string item, string reason, string fix)
        {
            Add(scope, true, Short(path) + Pos(item) + "：" + reason + "（仍已载入，但不会按预期生效）" + Tail(fix));
        }

        // 值被兜底纠正
        internal static void Fix(string scope, string path, string item, string reason, string to)
        {
            Add(scope, false, Short(path) + Pos(item) + "：" + reason + " → 已按 " + to + " 处理");
        }

        // 汇总本域结果并清空；未 Begin 或已 End 时静默
        internal static void End(string scope)
        {
            try
            {
                Scope s;
                if (!scopes.TryGetValue(scope, out s) || s == null) return;
                int r = s.rejects.Count;
                int f = s.fixes.Count;
                if (r == 0 && f == 0)
                {
                    DistributionLog.Info("[Load] 内容校验 " + s.name + "：全部通过（无拒绝、无兜底）");
                }
                else
                {
                    DistributionLog.Warn("[Load] 内容校验 " + s.name + "：拒绝 " + r + " 条，兜底纠正 " + f
                        + " 条；其余内容照常加载（明细见下，逐条可照改）");
                    for (int i = 0; i < r && i < MaxDetail; i++)
                    {
                        DistributionLog.Warn("[Load]   × " + s.rejects[i]);
                    }
                    if (r > MaxDetail)
                    {
                        DistributionLog.Warn("[Load]   × 另有 " + (r - MaxDetail) + " 条拒绝已省略（先修前 " + MaxDetail + " 条）");
                    }
                    for (int i = 0; i < f && i < MaxDetail; i++)
                    {
                        DistributionLog.Info("[Load]   ~ " + s.fixes[i]);
                    }
                    if (f > MaxDetail)
                    {
                        DistributionLog.Info("[Load]   ~ 另有 " + (f - MaxDetail) + " 条纠正已省略");
                    }
                }
                scopes.Remove(scope);
            }
            catch
            {
                // X8 预期：诊断器自身失败不得反过来影响加载
            }
        }

        private static void Add(string scope, bool reject, string text)
        {
            try
            {
                Scope s;
                if (!scopes.TryGetValue(scope, out s) || s == null)
                {
                    // 没开域（单文件加载点）：直接输出一行，不记账
                    if (reject) DistributionLog.Warn("[Load] 拒绝 " + text);
                    else DistributionLog.Info("[Load] 兜底 " + text);
                    return;
                }
                if (reject) s.rejects.Add(text);
                else s.fixes.Add(text);
            }
            catch
            {
                // X8 预期：诊断器自身失败不得反过来影响加载
            }
        }

        private static string Pos(string item)
        {
            return string.IsNullOrEmpty(item) ? "" : " " + item;
        }

        private static string Tail(string fix)
        {
            return string.IsNullOrEmpty(fix) ? "" : " → " + fix;
        }

        // 绝对路径只留 event/ reward/ sounds/ 之后的部分；跨 Mod 时保留「Mod标题/相对路径」
        internal static string Short(string path)
        {
            if (string.IsNullOrEmpty(path)) return "(未命名文件)";
            string p = path.Replace('\\', '/');
            bool abs = p.Length > 1 && (p[1] == ':' || p[0] == '/');
            if (!abs) return p;
            string[] anchors = { "/event/", "/Event/", "/EventYaml/", "/reward/", "/sounds/", "/load.yaml", "/Load.yaml" };
            for (int i = 0; i < anchors.Length; i++)
            {
                int at = p.IndexOf(anchors[i], StringComparison.OrdinalIgnoreCase);
                if (at >= 0) return p.Substring(at + 1);
            }
            try { return Path.GetFileName(p); }
            catch { /* X8 预期：诊断器自身求文件名失败，退回原始路径，不递归报错 */ }
            return p;
        }
    }
}
