using System.Collections.Generic;

namespace EventFramework
{
    public static class PatchValidator
    {
        // 只允许补丁层覆写的键
        public static readonly string[] PatchOnlyKeys = { "mode", "mode_target" };

        // 提取补丁 yaml 的顶层键集合（校验补丁只能改允许的字段）
        public static HashSet<string> ExtractTopLevelKeys(string text)
        {
            var keys = new HashSet<string>(System.StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return keys;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;

                char first = line[0];
                if (first == ' ' || first == '\t' || first == '#') continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                string key = line.Substring(0, colon).Trim();
                if (key.Length > 0) keys.Add(key);
            }
            return keys;
        }

        public static bool Validate(string targetRawText, string patchRawText,
                                    string targetFullId, string patchFile, out string errorMsg)
        {
            errorMsg = null;

            var targetKeys = ExtractTopLevelKeys(targetRawText);
            var patchKeys = ExtractTopLevelKeys(patchRawText);

            for (int i = 0; i < PatchOnlyKeys.Length; i++) patchKeys.Remove(PatchOnlyKeys[i]);

            var missing = new List<string>();
            foreach (var k in targetKeys)
            {
                if (!patchKeys.Contains(k)) missing.Add(k);
            }

            if (missing.Count == 0) return true;

            errorMsg = "补丁 '" + patchFile + "' 未通过完整性校验\n"
                     + "  目标：" + targetFullId + "\n"
                     + "  缺失字段：" + string.Join(", ", missing.ToArray()) + "\n"
                     + "  → 补丁被拒绝（完全覆盖要求补丁包含目标的所有顶层字段）";
            return false;
        }
    }
}
