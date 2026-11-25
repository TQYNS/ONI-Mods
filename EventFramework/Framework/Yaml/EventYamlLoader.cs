// EventYamlLoader.cs — 事件框架 · 【YAML 加载层】

using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public static class EventYamlLoader
    {
        // 约定文件夹名（放在 Mod 根目录下）—— 支持多个，找到哪个扫哪个。
        public static readonly string[] YAML_DIR_NAMES = { "EventYaml", "event", "Event" };

// 已成功加载的 yaml 文件数（统计用）
        public static int LoadedFiles  { get; private set; }
        public static int FailedFiles  { get; private set; }
        public static int PatchApplied { get; private set; }

        // 入口
        public static int LoadAll()
        {
            LoadedFiles = 0; FailedFiles = 0; PatchApplied = 0;
            ContentDiag.Begin("event");

            var files = YamlFileScanner.Scan(YAML_DIR_NAMES, "event");

            var normal = new List<EventDef>();
            var patches = new List<EventDef>();

            for (int i = 0; i < files.Count; i++)
            {
                var def = ParseOne(files[i]);
                if (def == null) continue;

                string mode = def.mode == null ? "" : def.mode.Trim();
                if (mode.Length == 0)
                {
                    def.fullId = EventRegistry.MakeFullId(def.sourceMod, def.id);
                    def.lastPatchedBy = def.sourceMod;
                    def.lastPatchedFrom = def.sourceFile;
                    normal.Add(def);
                }
                else if (mode.Equals("override", StringComparison.OrdinalIgnoreCase))
                {
                    patches.Add(def);
                }
                else
                {
                    FailedFiles++;
                    ContentDiag.Skip("event", def.sourceFile, "", "mode 只支持 'override'，其它值未定义：'" + mode + "'",
                        "改 mode: override 并补 mode_target，或删掉 mode 走普通事件");
                }
            }

            for (int i = 0; i < normal.Count; i++)
            {
                if (EventRegistry.Add(normal[i]))
                {
                    LoadedFiles++;
                    if (normal[i].debug != null && normal[i].debug.verbose_log)
                        Debug.Log("[MoreFood/Event][Yaml] 已加载 '" + normal[i].id + "' ← "
                                  + RelPath(normal[i].sourceFile));
                }
                else
                {
                    FailedFiles++;
                    // 先到者是谁、在哪份文件里 —— 只报「已存在」作者无从下手
                    var first = EventRegistry.Get(normal[i].fullId);
                    string from = first != null ? ContentDiag.Short(first.sourceFile) : "更早加载的文件";
                    ContentDiag.Skip("event", normal[i].sourceFile, "「" + normal[i].id + "」",
                        "全 ID 已存在（" + normal[i].fullId + "），已被 " + from + " 占用，先到者胜",
                        "改名，或改用 mode: override + mode_target 覆盖；跨 Mod 同名请先看 Load.yaml 顺序");
                }
            }

            for (int i = 0; i < patches.Count; i++)
            {
                string error;
                if (!ValidatePatch(patches[i], out error))
                {
                    FailedFiles++;
                    ContentDiag.Skip("event", patches[i].sourceFile, "「" + (patches[i].id ?? "?") + "」",
                        error, "按提示补字段；对照目标事件的全 ID 与其字段表");
                    continue;
                }
                if (EventRegistry.Override(patches[i]))
                {
                    LoadedFiles++;
                    PatchApplied++;
                }
                else
                {
                    FailedFiles++;
                    ContentDiag.Skip("event", patches[i].sourceFile, "「" + (patches[i].id ?? "?") + "」",
                        "mode_target '" + patches[i].mode_target + "' 找不到目标事件",
                        "核对目标全 ID（<前缀>.event.<短名>）是否已加载，或该目标是否在本 Mod 之外");
                }
            }

            DistributionLog.Info("[Yaml] 扫描完成：文件 成功=" + LoadedFiles
                      + " 失败=" + FailedFiles + "，覆盖补丁=" + PatchApplied);
            ContentDiag.End("event");

            return EventRegistry.Count;
        }

        // 单个文件
        private static EventDef ParseOne(YamlFile file)
        {
            string path = file != null ? file.filePath : "<null>";
            if (file == null || string.IsNullOrEmpty(file.text) || file.text.Trim().Length == 0)
            {
                FailedFiles++;
                ContentDiag.File("event", path, "空文件或只有空白", "删掉该文件，或按文档补齐 id 与 options");
                return null;
            }

            // 核心：吃字符串不吃路径；ErrorHandler 由 YamlLoad 接管，失败原因进 ContentDiag
            EventDef def = YamlLoad.Parse<EventDef>(file.text, path, "event");
            if (def == null)
            {
                FailedFiles++;
                return null;
            }

            def.sourceFile = file.filePath;
            def.sourceRawText = file.text;
            def.sourceMod = file.modPrefix;

            if (string.IsNullOrEmpty(def.id))
            {
                FailedFiles++;
                ContentDiag.Skip("event", path, "", "缺少 id 字段",
                    "补 id: <短名>（全 ID = <前缀>.event.<短名>）");
                return null;
            }

            def.id = IdentifierValidator.Normalize(def.id);
            if (!IdentifierValidator.IsValid(def.id))
            {
                FailedFiles++;
                ContentDiag.Skip("event", path, "「" + def.id + "」",
                    "id 不合规：" + IdentifierValidator.Why(def.id),
                    "改短名：小写字母开头（或下划线），只用 [A-Za-z0-9_]，避开 C# 保留字，长度 ≤ "
                    + IdentifierValidator.MAX_LENGTH);
                return null;
            }

            // 预制池语义（不可由 yaml 覆盖）：pool == "preset" ⇒ one_shot + 无冷却。
            //   触发一次成功即熔断；失败/过期由顺序槽降级进常规池。
            if (!string.IsNullOrEmpty(def.pool) && def.pool == "preset")
            {
                if (def.repeat == null) def.repeat = new EventRepeatBlock();
                def.repeat.one_shot = true;
                def.repeat.min_interval = 0f;
                def.repeat.max_interval = 0f;
            }

            ValidateVariants(def, path);
            ValidateHunt(def, path);

            return def;
        }

        // 文案变体：逐组校验；与根级 name/description 并存时以 variants 为准（根级仅兜底）
        private static void ValidateVariants(EventDef def, string path)
        {
            if (def.variants == null || def.variants.Count == 0) return;

            int valid = 0;
            for (int i = 0; i < def.variants.Count; i++)
            {
                var v = def.variants[i];
                if (v == null)
                {
                    ContentDiag.Skip("event", path, "「" + def.id + "」",
                        "variants[" + i + "] 是空项", "删掉该空项，或补齐 name 与 description");
                    continue;
                }
                if (string.IsNullOrEmpty(v.name) || string.IsNullOrEmpty(v.description))
                {
                    ContentDiag.Skip("event", path, "「" + def.id + "」",
                        "variants[" + i + "] 缺 name 或 description（该组作废）",
                        "每组都要同时有 name（单行）与 description（块文本）");
                    v.name = null;
                    v.description = null;
                    continue;
                }
                valid++;
            }

            if (valid == 0)
            {
                def.variants = null;
                return;
            }

            if (!string.IsNullOrEmpty(def.name) || !string.IsNullOrEmpty(def.description))
            {
                ContentDiag.Skip("event", path, "「" + def.id + "」",
                    "variants 与根级 name/description 并存 → 以 variants 为准，根级已被忽略",
                    "删掉根级 name/description，只保留 variants");
                def.name = null;
                def.description = null;
            }
        }

        // 狩猎规格：档位只认 S/A/B/C/D/E/F；全空 ⇒ 告警（本次不投放怪物）
        private static void ValidateHunt(EventDef def, string path)
        {
            if (def.hunt == null) return;

            if (def.hunt.creature_tiers == null) def.hunt.creature_tiers = new List<string>();

            var kept = new List<string>();
            for (int i = 0; i < def.hunt.creature_tiers.Count; i++)
            {
                string t = def.hunt.creature_tiers[i];
                if (string.IsNullOrEmpty(t)) continue;
                t = t.Trim().ToUpperInvariant();
                if (t.Length != 1 || "SABCDEF".IndexOf(t[0]) < 0)
                {
                    ContentDiag.Skip("event", path, "「" + def.id + "」",
                        "hunt.creature_tiers 里的 '" + def.hunt.creature_tiers[i] + "' 不是合法档位（S/A/B/C/D/E/F）",
                        "改成合法档位，或删掉该值");
                    continue;
                }
                if (!kept.Contains(t)) kept.Add(t);
            }
            def.hunt.creature_tiers = kept;

            if (kept.Count == 0)
            {
                ContentDiag.Skip("event", path, "「" + def.id + "」",
                    "hunt 存在但 creature_tiers 为空 → 本次不会投放怪物",
                    "补 creature_tiers: [E, D] 这类档位列表");
            }
            if (def.hunt.creature_count < 1) def.hunt.creature_count = 1;
            if (def.hunt.duration_cycles < 1) def.hunt.duration_cycles = 8;
            if (def.hunt.reward_rep < 0) def.hunt.reward_rep = 0;
            if (def.hunt.reward_currency_min < 0) def.hunt.reward_currency_min = 0;
            if (def.hunt.reward_currency_max < 0) def.hunt.reward_currency_max = 0;
            if (def.hunt.reward_currency_max < def.hunt.reward_currency_min)
                def.hunt.reward_currency_max = def.hunt.reward_currency_min;
        }

        private static bool ValidatePatch(EventDef patch, out string error)
        {
            error = null;

            string target = patch.mode_target;
            if (string.IsNullOrEmpty(target))
            {
                error = "补丁缺少 mode_target: " + patch.sourceFile;
                return false;
            }

            target = target.Trim();
            int first = target.IndexOf('.');
            int last = target.LastIndexOf('.');
            if (first <= 0 || last <= first)
            {
                error = "mode_target 必须是三段式 <modid>.event.<id>，实际为 '" + target + "'（"
                        + patch.sourceFile + "）";
                return false;
            }

            string middle = target.Substring(first + 1, last - first - 1);
            if (!string.Equals(middle, "event", StringComparison.Ordinal))
            {
                error = "mode_target 的第二段必须是 'event'，实际为 '" + middle + "'（"
                        + patch.sourceFile + "）";
                return false;
            }

            string shortId = target.Substring(last + 1);
            if (!string.Equals(patch.id, shortId, StringComparison.Ordinal))
            {
                error = "补丁的 id ('" + patch.id + "') 必须等于 mode_target 的最后一段 ('"
                        + shortId + "')（" + patch.sourceFile + "）";
                return false;
            }

            var targetDef = EventRegistry.Get(target);
            if (targetDef == null)
            {
                error = "补丁目标不存在: " + target + NearIds(target);
                return false;
            }

            return PatchValidator.Validate(targetDef.sourceRawText, patch.sourceRawText,
                                           target, patch.sourceFile, out error);
        }

        // 与目标最接近的已加载全 ID（同前缀最多 3 个），拼错时能直接看出该写哪个
        private static string NearIds(string target)
        {
            string prefix = null;
            int dot = target.LastIndexOf('.');
            if (dot > 0) prefix = target.Substring(0, dot + 1);

            var hits = new List<string>();
            foreach (var d in EventRegistry.All)
            {
                if (d == null || string.IsNullOrEmpty(d.fullId)) continue;
                if (prefix == null || !d.fullId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                hits.Add(d.fullId);
                if (hits.Count >= 3) break;
            }
            return hits.Count == 0 ? "" : "；同前缀的已加载 id：" + string.Join("、", hits.ToArray());
        }

        // 取路径最后两段（日志用；嵌套时能直接看出文件在哪个子目录）
        private static string RelPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return path;

                int last = -1;
                for (int i = path.Length - 1; i >= 0; i--)
                {
                    if (path[i] != '\\' && path[i] != '/') continue;
                    if (last < 0) { last = i; continue; }
                    return path.Substring(i + 1);
                }
                return last >= 0 && last + 1 < path.Length ? path.Substring(last + 1) : path;
            }
            catch
            {
                // X8 预期：路径裁剪失败就用原样，不影响加载
            }
            return path;
        }
    }
}
