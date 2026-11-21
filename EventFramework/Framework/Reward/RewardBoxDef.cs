using System;
using System.Collections.Generic;

namespace EventFramework
{
    public sealed class ParsedAmount
    {
        // 是否为固定值（否则走加权随机）
        public bool isFixed;
        // 固定值
        public int fixedValue;
        // 加权候选列表（每项是 int[]）
        public List<int[]> weightedList;
    }

    public class RewardEntry
    {
        // 指定单个奖励 id（与 tags 二选一）
        public string id { get; set; } = null;
        // 按标签收集候选（与 id 二选一）
        public List<string> tags { get; set; } = null;
        // 分组名（同组条目一起结算）
        public string group { get; set; } = null;
        // 抽取权重
        public int weight { get; set; } = 1;
        // 命中概率（万分比，10000 = 必中）
        public int chance_bp { get; set; } = DEFAULT_CHANCE_BP;
        // 概率偏移（万分比）
        public int chance_offset { get; set; } = 0;
        // 抽取次数
        public int draws { get; set; } = 1;
        // pity 保底计数
        public int pity_count { get; set; } = 0;
        // 数量（数字 / 区间 / 标签等，交 ParsedAmount 解析）
        public object amount { get; set; } = null;

        // 解析后的数量结果（运行时缓存）
        [System.NonSerialized] public ParsedAmount parsedAmount = null;

        // 默认概率：万分制 10000 = 100%
        public const int DEFAULT_CHANCE_BP = 10000;

        public bool HasSingleTarget { get { return !string.IsNullOrEmpty(id); } }
        public bool HasTagTarget { get { return tags != null && tags.Count > 0; } }
    }

    public class RewardBoxDef
    {
        // 奖励箱 id
        public string id { get; set; } = null;
        // 显示名
        public string name { get; set; } = null;
        // 描述
        public string description { get; set; } = null;
        // 正常奖励条目表
        public List<RewardEntry> entries { get; set; } = new List<RewardEntry>();
        // 兜底条目表（抽不到时用）
        public List<RewardEntry> fallback { get; set; } = new List<RewardEntry>();

        // 池分类（对齐事件 schema；此前缺此属性 → YamlIO 报 "doesn't have that property"）
        public string pool { get; set; } = null;

        // 覆盖的奖励模式（对应原版模式名）
        public string mode { get; set; } = null;
        // 模式作用的目标
        public string mode_target { get; set; } = null;

        // 完整 id（前缀:局部 id，运行时算）
        [System.NonSerialized] public string fullId = null;
        // 来源 mod 名（运行时记录）
        [System.NonSerialized] public string sourceMod = null;
        [System.NonSerialized] public string sourceFile = null;
        [System.NonSerialized] public string sourceRawText = null;
        [System.NonSerialized] public string lastPatchedBy = null;
        [System.NonSerialized] public string lastPatchedFrom = null;

        public string GetNameText()
        {
            return string.IsNullOrEmpty(name) ? id : name;
        }

        // 解析 yaml 的数量字段（数字 / 字符串 / 区间都归一成 ParsedAmount）
        public static ParsedAmount ParseAmount(object raw)
        {
            var parsed = new ParsedAmount();
            if (raw == null)
            {
                parsed.isFixed = true;
                parsed.fixedValue = 1;
                return parsed;
            }

            if (raw is string)
            {
                int v;
                if (int.TryParse((string)raw, out v) && v > 0)
                {
                    parsed.isFixed = true;
                    parsed.fixedValue = v;
                    return parsed;
                }
                parsed.isFixed = true;
                parsed.fixedValue = 1;
                return parsed;
            }

            var list = raw as System.Collections.IList;
            if (list == null)
            {
                parsed.isFixed = true;
                parsed.fixedValue = 1;
                return parsed;
            }

            parsed.weightedList = new List<int[]>();
            for (int i = 0; i < list.Count; i++)
            {
                var pair = list[i] as System.Collections.IList;
                if (pair == null || pair.Count < 2) continue;

                int value, weight;
                if (!TryToInt(pair[0], out value)) continue;
                if (!TryToInt(pair[1], out weight)) continue;
                if (value <= 0 || weight <= 0) continue;

                parsed.weightedList.Add(new int[] { value, weight });
            }

            if (parsed.weightedList.Count == 0)
            {
                parsed.weightedList = null;
                parsed.isFixed = true;
                parsed.fixedValue = 1;
            }
            return parsed;
        }

        private static bool TryToInt(object o, out int value)
        {
            value = 0;
            if (o == null) return false;
            try
            {
                value = Convert.ToInt32(o);
                return true;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("reward:TryToInt", "[Reward] TryToInt 失败 → 已按兜底处理", e); return false; }
        }
    }
}
