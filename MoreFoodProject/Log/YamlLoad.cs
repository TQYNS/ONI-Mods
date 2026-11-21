// YamlLoad.cs — 统一 yaml 解析入口：接管 Klei.YamlIO 的错误回调，避免原版 LogError 直通 LogCatcher

using System;
using Klei;

namespace MoreFood_KFC
{
    // 解析失败一律走 ContentDiag（Warn），成功时无输出
    internal static class YamlLoad
    {
        // 解析一个 yaml 文本；null 表示整份被拒，原因已进 ContentDiag
        internal static T Parse<T>(string text, string path, string scope) where T : class
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                ContentDiag.File(scope, path, "空文件或只有空白", "确认文件有内容且编码为 UTF-8");
                return null;
            }

            bool fatal = false;
            System.Collections.Generic.List<string> ignored = new System.Collections.Generic.List<string>();
            string fatalMsg = null;

            // YamlIO 内部吞掉所有异常，只通过这个回调报错；默认回调是 Debug.LogError
            YamlIO.ErrorHandler handler = new YamlIO.ErrorHandler(
                delegate(YamlIO.Error err, bool forceWarning)
                {
                    string msg = OneLine(err.message);
                    if (err.inner_exception != null)
                    {
                        msg = msg + " | 内层：" + OneLine(err.inner_exception.Message);
                    }
                    if (err.severity == YamlIO.Error.Severity.Fatal)
                    {
                        fatal = true;
                        fatalMsg = msg;
                    }
                    else
                    {
                        ignored.Add(msg);
                    }
                });

            T t = null;
            try
            {
                t = YamlIO.Parse<T>(text, default(FileHandle), handler, null);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Load] 解析器抛出异常（该文件已跳过）：{0}", path);
                ContentDiag.File(scope, path, "解析器抛出异常：" + OneLine(e.Message), "检查 YAML 缩进与字段类型");
                return null;
            }

            if (fatal)
            {
                ContentDiag.File(scope, path, "YAML 语法或类型错误：" + fatalMsg, Advise(fatalMsg));
                return null;
            }
            if (ignored.Count > 0)
            {
                // 同一文件的未知字段合成一条，最多列 3 个，避免一个文件刷十几行
                string brief = ignored.Count <= 3
                    ? string.Join("；", ignored.ToArray())
                    : string.Join("；", ignored.GetRange(0, 3).ToArray()) + " 等 " + ignored.Count + " 处";
                ContentDiag.Skip(scope, path, "", "字段被忽略：" + brief, Advise(ignored[0]));
            }
            if (t == null)
            {
                ContentDiag.File(scope, path, "解析结果为空", "检查顶层结构是否写对（如 points: / stations: 是否缺层）");
            }
            return t;
        }

        // 探测式解析：不记诊断，供「先试 A 结构，不中再试 B 结构」的场景
        internal static T TryParse<T>(string text) where T : class
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                YamlIO.ErrorHandler silent = new YamlIO.ErrorHandler(
                    delegate(YamlIO.Error err, bool forceWarning) { });
                return YamlIO.Parse<T>(text, default(FileHandle), silent, null);
            }
            catch { return null; }
        }

        private static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(无详细信息)";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            if (s.Length > 220) s = s.Substring(0, 220) + "…";
            return s;
        }

        // 按 YamlDotNet 的报错原文给出可执行的修改方向
        private static string Advise(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return "检查该文件的 YAML 语法与字段类型";
            string m = msg.ToLowerInvariant();
            if (m.Contains("not found on type") || m.Contains("property") || m.Contains("field"))
            {
                return "字段名不在该结构里：对照文档字段表改名，或删掉该行";
            }
            if (m.Contains("could not convert") || m.Contains("invalid") || m.Contains("expected type"))
            {
                return "字段类型不对：数字不要加引号，布尔写 true/false，区间写 [下限, 上限]";
            }
            if (m.Contains("did not find expected") || m.Contains("mapping") || m.Contains("sequence")
                || m.Contains("indentation") || m.Contains("tab"))
            {
                return "缩进或符号写错：只用空格不用 Tab，冒号后留空格，列表项以 '- ' 对齐";
            }
            return "检查该文件的 YAML 语法与字段类型";
        }
    }
}
