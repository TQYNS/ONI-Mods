// ModYaml.cs — 宿主自己的跨 Mod yaml 扫描（trade / map / combat 三类目录），结果不缓存

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MoreFood_KFC
{
    // 一个 yaml 文件 + 它所属的 Mod
    internal sealed class ModYamlFile
    {
        // 所属 Mod 标题（日志 / 排除自身用）
        public string modTitle;
        // Mod 根目录
        public string modRoot;
        // yaml 绝对路径
        public string filePath;
        // 文件正文
        public string text;
        // 所在目录名（归类 / 日志用）
        public string categoryName;
    }

    internal static class ModYamlScanner
    {
        // 扫所有「已启用 Mod」根目录下名为 dirNames 的目录（递归找 .yaml/.yml）
        public static List<ModYamlFile> Scan(string[] dirNames, string categoryName, string scope)
        {
            var result = new List<ModYamlFile>();
            if (dirNames == null || dirNames.Length == 0) return result;

            List<KMod.Mod> mods = null;
            try
            {
                var g = Global.Instance;
                if (g != null && g.modManager != null) mods = g.modManager.mods;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Load] 取 Mod 列表失败（{0} 的跨 Mod 内容不会合并）", categoryName);
            }

            if (mods == null || mods.Count == 0)
            {
                MFLog.Warn(LogCat.General, "[Load] 没有可用 Mod 列表（" + categoryName + " 的跨 Mod 内容不会合并）");
                ContentDiag.File(scope, categoryName + "/", "取不到已启用 Mod 列表", "确认 Mod 由引擎正常加载（看启动日志的 Loading MOD dll）");
                return result;
            }

            for (int m = 0; m < mods.Count; m++)
            {
                var mod = mods[m];
                if (mod == null) continue;

                // 只认本次会话真正加载了内容的 Mod（已安装未勾选的 loaded_content == 0）
                try
                {
                    if (mod.loaded_content == (KMod.Content)0) continue;
                }
                catch (Exception e)
                {
                    // X8 预期：第三方 Mod 对象属性可能不可读；读不到按「未加载」处理
                    MFLog.Verbose(LogCat.General, "[Load] 某 Mod 的 loaded_content 读不到，其 {0} 内容不合并：{1}", categoryName, e);
                    continue;
                }

                string root = null;
                try { root = mod.ContentPath; }
                catch (Exception e)
                {
                    // X8 预期：ContentPath 不可读 → 无法定位目录
                    MFLog.Verbose(LogCat.General, "[Load] 某 Mod 的 ContentPath 读不到，其 {0} 内容不合并：{1}", categoryName, e);
                }
                if (string.IsNullOrEmpty(root)) continue;

                string title = null;
                try { title = mod.title; } catch { title = null; }

                for (int d = 0; d < dirNames.Length; d++)
                {
                    string dir = null;
                    try { dir = Path.Combine(root, dirNames[d]); } catch { continue; }
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;

                    string[] files;
                    try
                    {
                        files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.General, e, "[Load] 枚举目录失败 {0}", dir);
                        ContentDiag.File(scope, dir, "目录枚举失败：" + e.Message + "（该目录下内容全部未合并）",
                            "检查目录权限或是否被其它程序占用");
                        continue;
                    }

                    Array.Sort(files, StringComparer.Ordinal);

                    MFLog.Info(LogCat.General, "[Load] 发现 " + dirNames[d] + "/ ：Mod=" + title
                              + "，文件 " + files.Length + " 个");

                    for (int i = 0; i < files.Length; i++)
                    {
                        string ext = Path.GetExtension(files[i]);
                        if (!ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                         && !ext.Equals(".yml", StringComparison.OrdinalIgnoreCase)) continue;

                        string text;
                        try
                        {
                            text = File.ReadAllText(files[i], new UTF8Encoding(false));
                        }
                        catch (Exception e)
                        {
                            MFLog.Exc(LogCat.General, e, "[Load] 读取失败 {0}", files[i]);
                            ContentDiag.File(scope, title + "/" + dirNames[d] + "/" + Path.GetFileName(files[i]),
                                "读取失败：" + e.Message, "检查文件是否被占用、编码是否为 UTF-8");
                            continue;
                        }

                        var f = new ModYamlFile();
                        f.modTitle = title;
                        f.modRoot = root;
                        f.filePath = files[i];
                        f.text = text;
                        f.categoryName = categoryName;
                        result.Add(f);
                    }
                }
            }

            MFLog.Info(LogCat.General, "[Load] 扫描 " + categoryName + "：命中 " + result.Count + " 个文件");
            return result;
        }
    }
}
