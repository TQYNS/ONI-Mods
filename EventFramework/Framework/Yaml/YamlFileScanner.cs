using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public sealed class YamlFile
    {
        // 所属 mod 标题
        public string modTitle;
        // 所属 mod 前缀
        public string modPrefix;
        // mod 根目录
        public string modRoot;
        // yaml 绝对路径
        public string filePath;
        // 文件正文
        public string text;
        // 所在目录名（归类 / 日志用）
        public string categoryName;
    }

    public static class YamlFileScanner
    {
        // 本文件日志前缀
        private const string LOG_TAG = "[MoreFood/Load]";

        // 扫描若干目录下的 yaml 文件并打上分类名（目录不存在返回空表）
        public static List<YamlFile> Scan(string[] dirNames, string categoryName)
        {
            var result = new List<YamlFile>();
            if (dirNames == null || dirNames.Length == 0) return result;

            var mods = LoadOrderResolver.Resolve();
            for (int m = 0; m < mods.Count; m++)
            {
                var info = mods[m];
                if (string.IsNullOrEmpty(info.prefix)) continue;

                var dirs = CollectDirs(info.modRoot, dirNames);
                for (int d = 0; d < dirs.Count; d++)
                {
                    string[] files;
                    try
                    {
                        files = Directory.GetFiles(dirs[d], "*.*", SearchOption.AllDirectories);
                    }
                    catch (Exception e)
                    {
                        DistributionLog.Exc("⚠ 枚举目录失败 " + dirs[d] + "（该目录下内容全部未加载）", e);
                        ContentDiag.File(categoryName, dirs[d], "目录枚举失败：" + e.Message,
                            "检查目录权限或是否被其它程序占用");
                        continue;
                    }

                    Array.Sort(files, StringComparer.Ordinal);

                    Debug.Log(LOG_TAG + " 发现 " + Path.GetFileName(dirs[d]) + "/ ：Mod=" + info.title
                              + "，yaml 文件 " + files.Length + " 个");

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
                            DistributionLog.Exc("⚠ 读取失败 " + files[i] + "（该文件未加载）", e);
                            ContentDiag.File(categoryName, info.title + "/" + Path.GetFileName(files[i]),
                                "读取失败：" + e.Message, "检查文件是否被占用、编码是否为 UTF-8");
                            continue;
                        }

                        var f = new YamlFile();
                        f.modTitle = info.title;
                        f.modPrefix = info.prefix;
                        f.modRoot = info.modRoot;
                        f.filePath = files[i];
                        f.text = text;
                        f.categoryName = categoryName;
                        result.Add(f);
                    }
                }
            }

            Debug.Log(LOG_TAG + " 扫描 " + categoryName + "：命中 " + result.Count + " 个文件");
            return result;
        }

        private static List<string> CollectDirs(string modRoot, string[] dirNames)
        {
            var dirs = new List<string>();
            if (string.IsNullOrEmpty(modRoot)) return dirs;

            for (int i = 0; i < dirNames.Length; i++)
            {
                if (string.IsNullOrEmpty(dirNames[i])) continue;

                string dir;
                try { dir = Path.Combine(modRoot, dirNames[i]); }
                catch (Exception e)
                {
                    // 该候选目录名拼不出来就跳过，其它候选目录照常扫
                    DistributionLog.ExcOnce("scan:dir:" + dirNames[i],
                        "⚠ 拼接目录名 '" + dirNames[i] + "' 失败（该目录跳过）：" + modRoot, e);
                    continue;
                }
                if (!Directory.Exists(dir)) continue;

                bool dup = false;
                for (int k = 0; k < dirs.Count; k++)
                {
                    if (string.Equals(dirs[k], dir, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                }
                if (!dup) dirs.Add(dir);
            }
            return dirs;
        }
    }
}
