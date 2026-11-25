// ItemStack.cs — 事件框架 · 【提交 / 奖励的数据结构与解析器】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    // 一条"物品 / 质量"条目。mass 单位 kg（ONI 内部结算全走质量）
    public sealed class ItemStack
    {
        // 物品 id
        public string id;
        // 质量（kg）
        public float mass;

        // 本条是「奖励池」引用：id = 池的完整 id，mass 无意义
        // 只在 grant_rewards 这类允许池引用的位置产生（写法 [[池id]]）
        public bool isRewardBox = false;
    }

    // 解析 + 校验结果。三态：
    public sealed class ItemParseResult
    {
        // 通过全部检查、可用于执行的条目
        public List<ItemStack> valid = new List<ItemStack>();

        // id 查找失败的条目的 id（用于汇总警告）
        public List<string> failIds = new List<string>();

        // 格式错误的具体描述（格式错误时，整组被拒绝）
        public List<string> formatMsgs = new List<string>();

        // yaml 里原始的条目数（不管有效无效）
        public int totalCount = 0;

        // 出现任一格式错误 ⇒ true（此时 valid 已清空）
        public bool formatError = false;

 // ：是否已做过 id 查表校验（资产索引没就绪时会推迟，见 EnsureValidated）
        public bool lookupValidated = false;

 // ：该选项压根没配这个键（区别于"配了空组"）—— 延迟校验时要跳过
        public bool notConfigured = false;

        // 没有任何有效条目
        public bool IsEmpty => valid.Count == 0;
        // 全部失败（配了内容但无一有效）
        public bool IsAllFailed => !formatError && totalCount > 0 && valid.Count == 0;
    }

    public static class ItemStackParser
    {
        // 低于这个质量视为无效（1g 容差下的最小可执行质量）
        public const float MIN_MASS = 0.0001f;

        // 纯语法解析（不查 Assets，不打日志）——由调用方决定如何汇报
        // 失败语义（全框架统一）：格式错 ⇒ 整组拒绝；id 查不到 ⇒ 只跳过该条
        public static ItemParseResult Parse(List<object> raw)
        {
            return Parse(raw, false);
        }

        // allowRewardBox = true 时，形如 [池id] 的条目会被识别为「奖励池引用」
        // 只有 grant_rewards 允许；submit_materials 不允许（材料必须是具体物品）
        public static ItemParseResult Parse(List<object> raw, bool allowRewardBox)
        {
            var res = new ItemParseResult();
            if (raw == null) return res;

            res.totalCount = raw.Count;

            for (int i = 0; i < raw.Count; i++)
            {
                var sub = raw[i] as System.Collections.IList;
                if (sub == null)
                {
                    res.formatError = true;
                    res.formatMsgs.Add("第 " + i + " 项不是 [id, mass] 结构（是 "
                                       + (raw[i] == null ? "null" : raw[i].GetType().Name) + "）");
                    continue;
                }

                if (sub.Count == 0)
                {
                    res.formatError = true;
                    res.formatMsgs.Add("第 " + i + " 项是空数组");
                    continue;
                }

                string id = sub[0] as string;
                if (string.IsNullOrEmpty(id))
                {
                    res.formatError = true;
                    res.formatMsgs.Add("第 " + i + " 项 id 不是字符串或为空");
                    continue;
                }

                // 奖池引用：单元素，且 id 能在池表里查到（查不到按普通物品继续解析）
                if (allowRewardBox)
                {
                    var box = RewardBoxRegistry.Get(id.Trim());
                    if (box != null)
                    {
                        res.valid.Add(new ItemStack { id = box.fullId, mass = 1f, isRewardBox = true });
                        continue;
                    }
                }

                if (sub.Count < 2)
                {
                    res.formatError = true;
                    res.formatMsgs.Add("第 " + i + " 项只有 " + sub.Count + " 个元素（需要 2）");
                    continue;
                }

                float m;
                try { m = System.Convert.ToSingle(sub[1]); }
                catch (Exception ex)
                {
                    Distribution.Contracts.DistributionLog.ExcOnce("item:mass", "[Item] 质量字段无法转成数字（已记为格式错误）", ex);
                    res.formatError = true;
                    res.formatMsgs.Add("第 " + i + " 项 mass '" + sub[1] + "' 无法转成数字");
                    continue;
                }

                if (m < MIN_MASS)
                {
                    res.formatError = true;
                    res.formatMsgs.Add("第 " + i + " 项 '" + id + "' mass=" + m + " <= 0");
                    continue;
                }

                res.valid.Add(new ItemStack { id = id, mass = m });
            }

            // 规则 3：只要有一条格式错误，整组拒绝
            if (res.formatError)
            {
                res.formatMsgs.Insert(0, "整组被拒绝（共 " + res.totalCount + " 条，问题见下）");
                res.valid.Clear();
            }

            return res;
        }

        // id 校验：查 Assets.GetPrefab
        public static void Validate(ItemParseResult res)
        {
            if (res == null || res.formatError) return;
            res.lookupValidated = true;

            for (int i = res.valid.Count - 1; i >= 0; i--)
            {
                var s = res.valid[i];

                // 池引用由 RewardBoxRegistry 保证存在（Parse 阶段已确认），不按物品校验
                if (s.isRewardBox) continue;

                string reason;
                if (ItemTagValidator.IsPickupableById(s.id, out reason)) continue;

                res.failIds.Add(string.IsNullOrEmpty(reason) ? s.id : (s.id + "（" + reason + "）"));
                res.valid.RemoveAt(i);
            }
        }

 // 资产索引就绪判定 + 延迟校验

        private static readonly string[] PROBE_IDS = { "Dirt", "SandStone", "Copper", "Sand" };
        public static bool IsAssetIndexReady()
        {
            try
            {
                for (int i = 0; i < PROBE_IDS.Length; i++)
                {
                    if (Assets.TryGetPrefab(new Tag(PROBE_IDS[i])) != null) return true;
                }
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("item:assetprobe", "[Item] 资源索引探测异常 → 视为未就绪", e);
            }
            return false;
        }

        // 没校验过且资产索引已就绪 ⇒ 现在校验 + 汇报。返回是否本次做了校验。
        public static bool EnsureValidated(ItemParseResult res, string slot, bool warnEnabled)
        {
            if (res == null || res.lookupValidated || res.formatError || res.notConfigured) return false;
            if (!IsAssetIndexReady()) return false;

            Validate(res);
            Report(res, slot, warnEnabled);
            return true;
        }

        // 汇报：按级别输出（受 warnEnabled 控制；格式错误不受控制）
        public static bool Report(ItemParseResult res, string slot, bool warnEnabled)
        {
            if (res == null) return false;

            // ---- 规则 3：格式错误，⚠ 无视 warnEnabled（安全性）----
            if (res.formatError)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[MoreFood/Event][Item] ").Append(slot).Append(" 数据格式错误（整组不生效，不影响游戏运行）：");
                for (int i = 0; i < res.formatMsgs.Count; i++)
                    sb.Append("\n    · ").Append(res.formatMsgs[i]);
                Debug.LogWarning(sb.ToString());
                return true;
            }

            // ---- 规则 4：空组 ----
            if (res.totalCount == 0)
            {
                if (warnEnabled)
                    Debug.LogWarning("[MoreFood/Event][Item] " + slot
                                     + " 提交/奖励组为空（配置了字段但没条目）");
                return false;
            }

            // ---- 规则 1 + 2：部分失败 / 全部失败 ----
            if (res.failIds.Count > 0)
            {
                var list = string.Join(", ", res.failIds.ToArray());

                if (res.valid.Count == 0)
                {
                    // 全部失败 → 该组视为空组（条件被略过 / 奖励为 0），只记日志
                    if (warnEnabled)
                        Debug.LogWarning("[MoreFood/Event][Item] " + slot
                                         + " 全部条目校验失败（" + res.failIds.Count
                                         + "/" + res.totalCount + "）：" + list
                                         + "  ⇒ 该组按空组处理（不影响事件本体与游戏运行）");
                }
                else
                {
                    // 部分失败 → 普通警告（一条）
                    if (warnEnabled)
                        Debug.LogWarning("[MoreFood/Event][Item] " + slot
                                         + " 有 " + res.failIds.Count + "/"
                                         + res.totalCount + " 条 id 查找失败已跳过："
                                         + list);
                }
                return true;
            }

            return false;
        }

 // ：生成人类可读的"名称 ×数量"文本，用于面板信息栏
        public static string Describe(ItemParseResult res)
        {
            if (res == null || res.valid.Count == 0) return null;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < res.valid.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(DescribeEntry(res.valid[i]));
            }
            return sb.ToString();
        }

        // 单条条目的展示文本；池引用只显示池名（质量列对池无意义）。
        public static string DescribeEntry(ItemStack s)
        {
            if (s == null) return "";
            if (s.isRewardBox) return ResolveDisplayName(s.id);
            return DisplayNameOf(s);
        }

        // "物品名 ×量"
        public static string DisplayNameOf(ItemStack s)
        {
            if (s == null) return "";
            return ResolveDisplayName(s.id) + " ×" + FormatMass(s.mass);
        }

        // 数字格式化：整数不带小数点，小数保留 2 位
        private static string FormatMass(float m)
        {
            if (UnityEngine.Mathf.Approximately(m, UnityEngine.Mathf.Round(m)))
                return ((int)m).ToString();
            return m.ToString("0.##");
        }

        // id → 显示名。物品优先；查不到再按奖励池的完整 id 兜一层。
        public static string ResolveDisplayName(string id)
        {
            try
            {
                // 1) 原版物品：从 prefab 的 KSelectable 拿名字
                var prefab = Assets.GetPrefab(new Tag(id));
                if (prefab != null)
                {
                    var sel = prefab.GetComponent<KSelectable>();
                    if (sel != null)
                    {
                        string name = sel.GetName();
                        if (!string.IsNullOrEmpty(name) && !name.StartsWith("MISSING"))
                            return name;
                    }
                }
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("item:name", "[Item] 显示名解析异常 → 用原始 id", e);
            }

            try
            {
                // 2) 奖励池引用：id 形如 <前缀>.reward.<短id>
                var box = RewardBoxRegistry.Get(id);
                if (box != null) return box.GetNameText();
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("item:name#2", "[Item] 显示名解析异常 → 用原始 id", e);
            }

            return id;   // 兜底
        }

    }
}
