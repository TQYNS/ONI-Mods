using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class SpaceTemplates
    {
        private static readonly Dictionary<string, TemplateContainer> parsed =
            new Dictionary<string, TemplateContainer>(StringComparer.Ordinal);
        private static bool scanned;
        private static FieldInfo cacheField;

        internal static void ScanAllMods()
        {
            if (scanned)
            {
                return;
            }
            scanned = true;
            List<ModYamlFile> files;
            ContentDiag.Begin("map", LogCat.Space);
            try
            {
                files = ModYamlScanner.Scan(new string[] { "map" }, "map", "map");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] map 模板扫描失败（星际地点模板缺失）");
                ContentDiag.End("map");
                return;
            }
            for (int i = 0; i < files.Count; i++)
            {
                ModYamlFile file = files[i];
                if (file == null || string.IsNullOrEmpty(file.text) || string.IsNullOrEmpty(file.filePath)
                    || string.IsNullOrEmpty(file.modRoot))
                {
                    continue;
                }
                string rel;
                try
                {
                    rel = file.filePath.Substring(file.modRoot.Length).TrimStart('\\', '/');
                }
                catch (Exception)
                {
                    continue;
                }
                string[] parts = rel.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !parts[0].Equals("map", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string id = string.Join("/", parts, 1, parts.Length - 1);
                if (id.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                {
                    id = id.Substring(0, id.Length - 5);
                }
                else if (id.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                {
                    id = id.Substring(0, id.Length - 4);
                }
                if (string.IsNullOrEmpty(id) || parsed.ContainsKey(id))
                {
                    if (!string.IsNullOrEmpty(id) && parsed.ContainsKey(id))
                    {
                        ContentDiag.Skip("map", file.modTitle + "/" + rel, "「" + id + "」",
                            "模板 id 已被先出现的同名文件占用（先到者胜）", "改名，或删掉重复的那一份");
                    }
                    continue;
                }
                TemplateContainer container = Parse(file.text, rel);
                if (container != null)
                {
                    container.name = id;
                    parsed[id] = container;
                    MFLog.Info(LogCat.Space, "[Space] map 模板已解析：" + id + " ← " + file.modTitle + "/" + rel);
                }
            }
            MFLog.Info(LogCat.Space, "[Space] map 模板加载：" + parsed.Count + " 个（来自全部 Mod 的 map/ 目录）");
            ContentDiag.End("map");
        }

        private static TemplateContainer Parse(string text, string label)
        {
            TemplateContainer container = YamlLoad.Parse<TemplateContainer>(text, label, "map");
            if (container == null)
            {
                return null;
            }
            // yaml 没写的集合字段 YamlIO 会留 null；原版 PlaceInteriorTemplate 会无判空遍历
            if (container.cells == null)
            {
                container.cells = new List<TemplateClasses.Cell>();
            }
            if (container.buildings == null)
            {
                container.buildings = new List<TemplateClasses.Prefab>();
            }
            return container;
        }

        internal static bool Ensure(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
            {
                return false;
            }
            if (templateId.IndexOf("::", StringComparison.Ordinal) >= 0)
            {
                return true;
            }
            if (!scanned)
            {
                ScanAllMods();
            }
            TemplateContainer container;
            if (!parsed.TryGetValue(templateId, out container) || container == null)
            {
                return true;
            }
            return Inject(templateId, container);
        }

        private static bool Inject(string id, TemplateContainer container)
        {
            try
            {
                if (cacheField == null)
                {
                    cacheField = typeof(TemplateCache).GetField("templates",
                        BindingFlags.NonPublic | BindingFlags.Static);
                }
                if (cacheField == null)
                {
                    MFLog.Warn("tpl:field", LogCat.Space, "[Space] 找不到 TemplateCache.templates 字段，map 模板无法注入（游戏版本可能变了）");
                    return false;
                }
                Dictionary<string, TemplateContainer> dict =
                    cacheField.GetValue(null) as Dictionary<string, TemplateContainer>;
                if (dict == null)
                {
                    TemplateCache.Init();
                    dict = cacheField.GetValue(null) as Dictionary<string, TemplateContainer>;
                }
                if (dict == null)
                {
                    MFLog.Warn("tpl:init", LogCat.Space, "[Space] TemplateCache 未初始化，map 模板无法注入：" + id);
                    return false;
                }
                if (!dict.ContainsKey(id) || dict[id] == null)
                {
                    container.name = id;
                    dict[id] = container;
                    MFLog.Info(LogCat.Space, "[Space] map 模板已注入 TemplateCache：" + id);
                }
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] map 模板注入失败 {0}（该模板生成的任务地点会退回默认地形）", id);
                return false;
            }
        }
    }
}
