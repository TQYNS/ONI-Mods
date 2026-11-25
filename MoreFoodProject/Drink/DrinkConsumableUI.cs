// DrinkConsumableUI.cs — 饮水系统 · 【饮水管制 UI】：把饮品接入「饮食」消耗品表

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace MoreFood_KFC
{
    // 适配器：把 DrinkData 包成原版消耗品表认的项
    public class DrinkConsumableItem : IConsumableUIItem
    {
        // 饮品 tag
        public string tag;
        // 该饮品的数值
        public DrinkData data;

        // 分组序（MajorOrder）：原版「饮食」屏按 MajorOrder→MinorOrder 排序，并在 MajorOrder 变化处插分隔线（列名 "QualityDivider_<MajorOrder>"）
        public const int DRINK_MAJOR_ORDER = 90;

        public string ConsumableId { get { return tag; } }

        public string ConsumableName
        {
            get
            {
                try
                {
                    var go = Assets.TryGetPrefab(new Tag(tag));
                    if (go != null)
                    {
                        var kpid = go.GetComponent<KPrefabID>();
                        if (kpid != null)
                        {
                            string n = kpid.GetProperName();
                            if (!string.IsNullOrEmpty(n)) return n;
                        }
                    }
                }
                catch { /* expected: prefab 显示名取不到时回退 tag 串 */ }
                return tag;
            }
        }

        public int MajorOrder { get { return DRINK_MAJOR_ORDER; } }
        public int MinorOrder { get { return 0; } }
        public bool Display   { get { return true; } }

        public string OverrideSpriteName() { return null; }

        // 是否对该玩家"揭示"（未收集灰显的开关）。饮品恒为已揭示。
        public bool RevealTest() { return true; }
    }

    // 管制状态：真实管制走 DrinkRestrictionTracker（[Serialize]）；本类里那张 static 内存表 _forbidden 是验证版残留，仅 IsForbidden / Clear / Count 还在用（清理计划见《饮品.md》§11.2）
    public static class DrinkRestriction
    {
        // minionIdentityId → 被禁掉的饮品 tag 集合
        private static readonly Dictionary<string, HashSet<string>> _forbidden =
            new Dictionary<string, HashSet<string>>();

        // 判断某个复制人对某类饮品的消费管制开关（「饮食」表里的勾选状态）
        public static bool IsForbidden(string minionKey, string drinkTag)
        {
            HashSet<string> set;
            if (!_forbidden.TryGetValue(minionKey, out set) || set == null) return false;
            return set.Contains(drinkTag);
        }

        // 设置某个复制人对某类饮品的消费管制开关
        public static void SetForbidden(string minionKey, string drinkTag, bool forbidden)
        {
            if (string.IsNullOrEmpty(minionKey) || string.IsNullOrEmpty(drinkTag)) return;
            HashSet<string> set;
            if (!_forbidden.TryGetValue(minionKey, out set) || set == null)
            {
                set = new HashSet<string>();
                _forbidden[minionKey] = set;
            }
            if (forbidden) set.Add(drinkTag); else set.Remove(drinkTag);
        }

        public static void Clear() { _forbidden.Clear(); }

        // 真实管制：读写【我们自己的】DrinkRestrictionTracker
        public static bool IsUsableBy(GameObject duplicant, string drinkTag)
        {
            try
            {
                if (duplicant == null || string.IsNullOrEmpty(drinkTag)) return true;
                var t = DrinkRestrictionTracker.Get(duplicant);
                if (t == null) return true;          // 没有组件 → 不拦
                return t.IsPermitted(drinkTag);
            }
            catch { return true; }
        }

        // 被管制而跳过的日志（每个「小人 × 饮品」只打一次，防刷屏）
        private static readonly HashSet<string> _skipLogged = new HashSet<string>();

        // 某候选因管制被跳过时调用（节流：同人同饮品只报一次）。
        public static void NoteSkipped(GameObject duplicant, string drinkTag)
        {
            try
            {
                if (duplicant == null || string.IsNullOrEmpty(drinkTag)) return;

                if (_skipLogged.Count > 512) _skipLogged.Clear();   // 防无限增长
                string key = duplicant.GetInstanceID() + "|" + drinkTag;
                if (!_skipLogged.Add(key)) return;

                MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink][Restrict] " + duplicant.name
                          + " 已禁饮 '" + drinkTag + "' → 跳过（不作为主动饮用目标；"
                          + "食物管制与其他系统不受影响）");
            }
            catch { /* expected: 禁饮提示打印失败，不影响饮用目标挑选 */ }
        }

        // 按优先级从候选里挑一个饮品 tag。
        public static Tag PickByPriority(IList<Tag> candidates, GameObject duplicant = null)
        {
            try
            {
                if (candidates == null || candidates.Count == 0) return Tag.Invalid;

                int idx = DrinkPriorityPicker.PickIndex(
                    candidates.Count,
                    i =>
                    {
                        DrinkData d;
                        return Drinks.ItemTable.TryGetValue(candidates[i], out d) && d != null
                               ? d.priority : 0;
                    },
                    i => IsUsableBy(duplicant, candidates[i].ToString()));

                return idx >= 0 ? candidates[idx] : Tag.Invalid;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] PickByPriority 异常");
                return candidates.Count > 0 ? candidates[0] : Tag.Invalid;
            }
        }

        public static int Count
        {
            get
            {
                int n = 0;
                foreach (var kv in _forbidden) n += kv.Value != null ? kv.Value.Count : 0;
                return n;
            }
        }
    }

    // 补丁：往「饮食」表里加饮品列 —— 【已停用】
    public static class DrinkConsumable_OnActivate_Patch
    {
        // 停用后本方法不会被调用；留作参考（原版饮食屏插列的实现）
        private static bool _loggedOnce;

        // 补丁：「饮食」消耗品屏初始化后把饮品接进表（默认勾选）
        public static void Postfix(ConsumablesTableScreen __instance)
        {
            try
            {
                if (__instance == null) return;

                // 原版把列建完之后我们才插 —— 保证不破坏它的起始流程
                MFLog.Info(LogCat.Drink, "[MoreFood/Drink][UI/Consumable] 「饮食」表已打开，准备加饮品列"
                          + " 饮品数=" + Drinks.ItemTable.Count
                          + " 已管制项=" + DrinkRestriction.Count);

                if (Drinks.ItemTable.Count == 0)
                {
                    Debug.LogWarning("[MoreFood/Drink][UI/Consumable] Drinks.ItemTable 为空，无可加项");
                    return;
                }

                // ⚠ AddConsumableInfoColumn 是【非 public】（编译期报 CS0122 实测确认）
                var mi = typeof(ConsumablesTableScreen).GetMethod(
                    "AddConsumableInfoColumn",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (mi == null)
                {
                    Debug.LogWarning("[MoreFood/Drink][UI/Consumable] 反射找不到 AddConsumableInfoColumn"
                                     + "（原版可能改名/改签名）→ 本次不加列");
                    return;
                }

                int added = 0;
                foreach (var kv in Drinks.ItemTable)
                {
                    try
                    {
                        string tag = kv.Key.ToString();
                        var item = new DrinkConsumableItem { tag = tag, data = kv.Value };

                        object[] args =
                        {
                            "Drink_" + tag,
                            item,
                            new Action<IAssignableIdentity, GameObject>(OnLoad),
                            new Func<IAssignableIdentity, GameObject, TableScreen.ResultValues>(GetValue),
                            new Action<GameObject>(OnClick),
                            new Action<GameObject, TableScreen.ResultValues>(SetValue),
                            new Comparison<IAssignableIdentity>(Compare),
                            new Action<IAssignableIdentity, GameObject, ToolTip>(OnTooltip),
                            new Action<IAssignableIdentity, GameObject, ToolTip>(OnSortTooltip),
                        };

                        var col = mi.Invoke(__instance, args);
                        if (col != null) added++;
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Consumable] 加列失败 tag=" + kv.Key);
                    }
                }

                if (!_loggedOnce)
                {
                    _loggedOnce = true;
                    MFLog.Info(LogCat.Drink, "[MoreFood/Drink][UI/Consumable] ★ 饮品列已加入 " + added
                              + " 列（应出现在「饮食」表最右侧分组）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Consumable] OnActivate Postfix 异常");
            }
        }

        // 回调：都为最小实现，验证列能建出来即可
        private static void OnLoad(IAssignableIdentity id, GameObject widget) { }

        private static TableScreen.ResultValues GetValue(IAssignableIdentity id, GameObject widget)
        {
            try
            {
                if (id == null) return TableScreen.ResultValues.NotApplicable;
                string key = MinionKey(id);
                // 原版语义：True = 允许；False = 禁用
                return DrinkRestriction.Count > 0
                    ? TableScreen.ResultValues.Partial
                    : TableScreen.ResultValues.True;
            }
            catch { return TableScreen.ResultValues.NotApplicable; }
        }

        private static void OnClick(GameObject widget)
        {
            // 验证版：点击只记日志。真实实现需要从 widget 反查是哪一列。
            MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink][UI/Consumable] 玩家点击了饮品列（验证版：仅记日志）");
        }

        private static void SetValue(GameObject widget, TableScreen.ResultValues v)
        {
            MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink][UI/Consumable] set_value=" + v);
        }

        private static int Compare(IAssignableIdentity a, IAssignableIdentity b) { return 0; }

        private static void OnTooltip(IAssignableIdentity id, GameObject widget, ToolTip tip)
        {
            try
            {
                if (tip == null) return;
                tip.ClearMultiStringTooltip();
                tip.AddMultiStringTooltip("饮水管制（验证版）", null);
            }
            catch { /* expected: tooltip 填充失败不影响表格刷新 */ }
        }

        private static void OnSortTooltip(IAssignableIdentity id, GameObject widget, ToolTip tip) { }

        private static string MinionKey(IAssignableIdentity id)
        {
            try
            {
                var mi = id as MinionIdentity;
                if (mi != null) return mi.gameObject.GetInstanceID().ToString();
            }
            catch { /* expected: instanceID 读取失败，回退哈希键 */ }
            return id != null ? id.GetHashCode().ToString() : "?";
        }
    }
}
