using System.Collections.Generic;
using System.Text;

namespace EventFramework
{
    public static class IdentifierValidator
    {
        // id 最大长度
        public const int MAX_LENGTH = 128;

        private static readonly HashSet<string> Keywords = BuildKeywords();

        private static HashSet<string> BuildKeywords()
        {
            var set = new HashSet<string>(System.StringComparer.Ordinal);
            string[] words =
            {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
                "char", "checked", "class", "const", "continue", "decimal", "default",
                "delegate", "do", "double", "else", "enum", "event", "explicit",
                "extern", "false", "finally", "fixed", "float", "for", "foreach",
                "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
                "lock", "long", "namespace", "new", "null", "object", "operator",
                "out", "override", "params", "private", "protected", "public",
                "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
                "stackalloc", "static", "string", "struct", "switch", "this",
                "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
                "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
            };
            for (int i = 0; i < words.Length; i++) set.Add(words[i]);
            return set;
        }

        // 校验标识符是否合法（非空、去空格后符合命名规则）
        public static bool IsValid(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;

            string t = s.Trim();
            if (t.Length < 1 || t.Length > MAX_LENGTH) return false;
            if (Keywords.Contains(t)) return false;

            char c0 = t[0];
            if (!IsLetter(c0) && c0 != '_') return false;

            for (int i = 1; i < t.Length; i++)
            {
                char c = t[i];
                if (!IsLetter(c) && !IsDigit(c) && c != '_') return false;
            }
            return true;
        }

        // 不合规的**具体**原因（IsValid 为 false 时给作者看；合规返回空串）
        public static string Why(string s)
        {
            if (string.IsNullOrEmpty(s)) return "为空";

            string t = s.Trim();
            if (t.Length == 0) return "只有空白字符";
            if (t.Length > MAX_LENGTH) return "长度 " + t.Length + " 超过上限 " + MAX_LENGTH;
            if (Keywords.Contains(t)) return "'" + t + "' 是 C# 保留字，会被当成类型/关键字";

            char c0 = t[0];
            if (!IsLetter(c0) && c0 != '_')
                return "首字符 '" + c0 + "' 不是字母或下划线";

            for (int i = 1; i < t.Length; i++)
            {
                char c = t[i];
                if (IsLetter(c) || IsDigit(c) || c == '_') continue;
                return "第 " + (i + 1) + " 个字符 '" + c + "' 不在 [A-Za-z0-9_] 里";
            }
            return "不符合命名规则";
        }

        // 规范化标识符：去首尾空格（null 原样返回）
        public static string Normalize(string s)
        {
            return s == null ? null : s.Trim();
        }

        // 把任意 mod.title 洗成一个合法 ID 前缀：
        public static string Sanitize(string raw, int maxLength)
        {
            if (string.IsNullOrEmpty(raw) || maxLength <= 0) return null;

            var sb = new StringBuilder(raw.Length);
            bool lastWasUnderscore = false;

            for (int i = 0; i < raw.Length && sb.Length < maxLength; i++)
            {
                char c = raw[i];
                if (IsLetter(c) || IsDigit(c))
                {
                    sb.Append(c);
                    lastWasUnderscore = false;
                    continue;
                }
                if (sb.Length == 0 || lastWasUnderscore) continue;
                sb.Append('_');
                lastWasUnderscore = true;
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == '_') sb.Length--;
            if (sb.Length == 0) return null;

            if (IsDigit(sb[0])) sb.Insert(0, '_');

            string s = sb.ToString();
            if (Keywords.Contains(s)) s += "_";
            return s;
        }

        private static bool IsLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
