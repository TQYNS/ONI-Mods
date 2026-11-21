using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public static class RewardBoxLoader
    {
        // 认作 reward yaml 的目录名
        public static readonly string[] DIR_NAMES = { "reward", "Reward" };

        public static int LoadedFiles { get; private set; }
        public static int FailedFiles { get; private set; }
        public static int PatchApplied { get; private set; }

        // 加载 全部项
        public static int LoadAll()
        {
            LoadedFiles = 0;
            FailedFiles = 0;
            PatchApplied = 0;

            RewardBoxRegistry.Clear();
            ContentDiag.Begin("reward");

            var files = YamlFileScanner.Scan(DIR_NAMES, "reward");

            var normal = new List<RewardBoxDef>();
            var patches = new List<RewardBoxDef>();

            for (int i = 0; i < files.Count; i++)
            {
                var def = ParseOne(files[i]);
                if (def == null) continue;

                string mode = def.mode == null ? "" : def.mode.Trim();
                if (mode.Length == 0)
                {
                    def.fullId = RewardBoxRegistry.MakeFullId(def.sourceMod, def.id);
                    normal.Add(def);
                }
                else if (mode.Equals("override", StringComparison.OrdinalIgnoreCase))
                {
                    patches.Add(def);
                }
                else
                {
                    FailedFiles++;
                    ContentDiag.Skip("reward", def.sourceFile, "", "mode 只支持 'override'，其它值未定义：'" + mode + "'",
                        "改 mode: override 并补 mode_target，或删掉 mode 走普通奖励箱");
                }
            }

            for (int i = 0; i < normal.Count; i++)
            {
                if (RewardBoxRegistry.Add(normal[i]))
                {
                    LoadedFiles++;
                }
                else
                {
                    FailedFiles++;
                    var first = RewardBoxRegistry.Get(normal[i].fullId);
                    string from = first != null ? ContentDiag.Short(first.sourceFile) : "更早加载的文件";
                    ContentDiag.Skip("reward", normal[i].sourceFile, "「" + normal[i].id + "」",
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
                    ContentDiag.Skip("reward", patches[i].sourceFile, "「" + (patches[i].id ?? "?") + "」",
                        error, "mode_target 必须是 <modid>.reward.<id>，且短 id 与被覆盖的一致");
                    continue;
                }
                if (RewardBoxRegistry.Override(patches[i]))
                {
                    LoadedFiles++;
                    PatchApplied++;
                }
                else
                {
                    FailedFiles++;
                    ContentDiag.Skip("reward", patches[i].sourceFile, "「" + (patches[i].id ?? "?") + "」",
                        "mode_target '" + patches[i].mode_target + "' 找不到目标奖励箱",
                        "核对目标全 ID 是否已加载（<前缀>.reward.<短名>）");
                }
            }

            DistributionLog.Info("[Reward] 加载完成：" + RewardBoxRegistry.Describe()
                      + "，文件 成功=" + LoadedFiles + " 失败=" + FailedFiles
                      + "，覆盖补丁=" + PatchApplied);
            ContentDiag.End("reward");

            return LoadedFiles;
        }

        private static RewardBoxDef ParseOne(YamlFile file)
        {
            string path = file != null ? file.filePath : "<null>";
            if (file == null || string.IsNullOrEmpty(file.text) || file.text.Trim().Length == 0)
            {
                FailedFiles++;
                ContentDiag.File("reward", path, "空文件或只有空白", "删掉该文件，或按文档补齐 id 与 entries");
                return null;
            }

            RewardBoxDef def = YamlLoad.Parse<RewardBoxDef>(file.text, path, "reward");
            if (def == null)
            {
                FailedFiles++;
                return null;
            }

            def.sourceMod = file.modPrefix;
            def.sourceFile = file.filePath;
            def.sourceRawText = file.text;
            def.lastPatchedBy = file.modTitle;
            def.lastPatchedFrom = file.filePath;

            if (string.IsNullOrEmpty(def.id))
            {
                FailedFiles++;
                ContentDiag.Skip("reward", path, "", "缺少 id 字段",
                    "补 id: <短名>（全 ID = <前缀>.reward.<短名>）");
                return null;
            }

            def.id = IdentifierValidator.Normalize(def.id);
            if (!IdentifierValidator.IsValid(def.id))
            {
                FailedFiles++;
                ContentDiag.Skip("reward", path, "「" + def.id + "」",
                    "id 不合规：" + IdentifierValidator.Why(def.id),
                    "改短名：小写字母开头（或下划线），只用 [A-Za-z0-9_]，避开 C# 保留字，长度 ≤ "
                    + IdentifierValidator.MAX_LENGTH);
                return null;
            }

            PrepareEntries(def);
            return def;
        }

        private static void PrepareEntries(RewardBoxDef def)
        {
            PrepareEntryList(def.entries, def, "entries");
            PrepareEntryList(def.fallback, def, "fallback");
        }

        private static void PrepareEntryList(List<RewardEntry> list, RewardBoxDef def, string slot)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                string where = def.id + "." + slot + "[" + i + "]";
                if (e == null)
                {
                    ContentDiag.Skip("reward", def.sourceFile, where, "条目为空", "删掉这个空的 '- ' 项");
                    continue;
                }

                if (!e.HasSingleTarget && !e.HasTagTarget)
                {
                    ContentDiag.Suspect("reward", def.sourceFile, where, "既没有 id 也没有 tags",
                        "至少写 id: <物品> 或 tags: [...]，否则该条目永远不会产出");
                }

                if (e.draws <= 0)
                {
                    ContentDiag.Fix("reward", def.sourceFile, where, "draws=" + e.draws, "1");
                    e.draws = 1;
                }
                if (e.pity_count < 0)
                {
                    ContentDiag.Fix("reward", def.sourceFile, where, "pity_count=" + e.pity_count, "0");
                    e.pity_count = 0;
                }
                if (e.weight < 0)
                {
                    ContentDiag.Fix("reward", def.sourceFile, where, "weight=" + e.weight, "0");
                    e.weight = 0;
                }

                e.parsedAmount = RewardBoxDef.ParseAmount(e.amount);
                e.amount = null;
            }
        }

        // 与目标最接近的已加载全 ID（同前缀最多 3 个），拼错时能直接看出该写哪个
        private static string NearIds(string target)
        {
            string prefix = null;
            int dot = target.LastIndexOf('.');
            if (dot > 0) prefix = target.Substring(0, dot + 1);

            var hits = new List<string>();
            foreach (var d in RewardBoxRegistry.All)
            {
                if (d == null || string.IsNullOrEmpty(d.fullId)) continue;
                if (prefix == null || !d.fullId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                hits.Add(d.fullId);
                if (hits.Count >= 3) break;
            }
            return hits.Count == 0 ? "" : "；同前缀的已加载 id：" + string.Join("、", hits.ToArray());
        }

        private static bool ValidatePatch(RewardBoxDef patch, out string error)
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
                error = "mode_target 必须是三段式 <modid>.reward.<id>，实际为 '" + target + "'（"
                        + patch.sourceFile + "）";
                return false;
            }

            string middle = target.Substring(first + 1, last - first - 1);
            if (!string.Equals(middle, "reward", StringComparison.Ordinal))
            {
                error = "mode_target 的第二段必须是 'reward'，实际为 '" + middle + "'（"
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

            var targetDef = RewardBoxRegistry.Get(target);
            if (targetDef == null)
            {
                error = "补丁目标不存在: " + target + NearIds(target);
                return false;
            }

            return PatchValidator.Validate(targetDef.sourceRawText, patch.sourceRawText,
                                           target, patch.sourceFile, out error);
        }
    }
}
