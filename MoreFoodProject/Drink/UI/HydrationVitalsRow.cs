// 用途：小人面板「饮水」行，只依赖 Hydration Amount，不依赖饮品表。

using System;
using HarmonyLib;
using Klei.AI;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    public static class DrinkUIRow
    {
        // 建行日志只打一次
        private static bool _loggedCreated;
        // 解析日志只打一次
        private static bool _loggedResolve;

        // 锚点：只在 Calories 这个 Amount 之后插入（被 DrinkUI_AddAmountLine_Patch 调用，必须 public）
        public static bool IsAnchor(Amount amount)
        {
            var db = Db.Get();
            return db != null && db.Amounts != null && amount == db.Amounts.Calories;
        }

        // 尝试在面板上建出「饮水」行。已存在则直接返回 true（幂等）。
        public static bool TryBuildRow(MinionVitalsPanel panel, string source)
        {
            if (panel == null) return false;

            var amount = DrinkDatabase.Hydration;
            if (amount == null)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] Hydration 尚未注册，跳过建行");
                return false;
            }
            if (panel.Content == null || panel.LineItemPrefab == null || panel.amountsLines == null)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] 面板结构异常：Content="
                                 + (panel.Content != null) + " LineItemPrefab="
                                 + (panel.LineItemPrefab != null) + " amountsLines="
                                 + (panel.amountsLines != null));
                return false;
            }

            // 幂等：手写循环，避免每帧 lambda 捕获 amount 产生闭包分配
            for (int i = 0; i < panel.amountsLines.Count; i++)
            {
                if (panel.amountsLines[i].amount == amount) return true;
            }

            // 1) 复制模板行（追加到 Content 末尾）
            var go = Util.KInstantiateUI(panel.LineItemPrefab, panel.Content.gameObject, false);
            if (go == null)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] KInstantiateUI 返回 null");
                return false;
            }
            go.name = "DrinkRow_" + DrinkDatabase.AMOUNT_ID;

            var tooltip = go.GetComponent<ToolTip>();
            if (tooltip != null) tooltip.refreshWhileHovering = true;

            // 2) 图标（try/catch 兜住，缺 sprite 不至于中断整个补丁）
            Func<AmountInstance, Sprite> spriteFunc = null;
            try
            {
                if (!amount.CanDisplayerDisplayCustomIcons)
                {
                    var sprite = Assets.GetSprite(amount.uiSprite);
                    var img = go.GetComponentInChildren<Image>();
                    if (img != null && sprite != null) img.sprite = sprite;
                    if (sprite == null)
                        MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] 找不到 sprite: " + amount.uiSprite);
                }
                else
                {
                    spriteFunc = new Func<AmountInstance, Sprite>(amount.GetSprite);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/" + source + "] 设置图标失败");
            }

            // 3) 组装 AmountLine
            var line = new MinionVitalsPanel.AmountLine
            {
                amount      = amount,
                go          = go,
                locText     = go.GetComponentInChildren<LocText>(),
                toolTip     = tooltip,
                imageToggle = go.GetComponentInChildren<ValueTrendImageToggle>(),
                toolTipFunc = new Func<AmountInstance, string>(amount.GetTooltip),
                spriteFunc  = spriteFunc,
            };
            panel.amountsLines.Add(line);

            // 先置为不显示，交给 Refresh 的 TryUpdate 决定（原版也是这个流程）
            go.SetActive(false);

            // 4) 位置
            int anchorSibling = -1, ownSibling = go.transform.GetSiblingIndex();
            foreach (var l in panel.amountsLines)
            {
                if (l.go == null) continue;
                if (IsAnchor(l.amount)) { anchorSibling = l.go.transform.GetSiblingIndex(); break; }
            }
            if (anchorSibling >= 0 && ownSibling != anchorSibling + 1)
            {
                go.transform.SetSiblingIndex(anchorSibling + 1);
                ownSibling = go.transform.GetSiblingIndex();
            }

            if (!_loggedCreated)
            {
                _loggedCreated = true;
                MFLog.Info(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] 已建「饮水」行："
                          + "amountsLines=" + panel.amountsLines.Count
                          + " AnchorSibling=" + anchorSibling
                          + " 自身Sibling=" + ownSibling
                          + " LocText=" + (line.locText != null)
                          + " ImageToggle=" + (line.imageToggle != null));
            }
            return true;
        }

        // 对具体目标结算该行的显隐与文本（照抄 MinionVitalsPanel.Refresh 里那段逻辑）
        public static void ResolveRow(MinionVitalsPanel panel, GameObject target, string source)
        {
            if (panel == null || target == null || panel.amountsLines == null) return;

            // 每帧执行：手写循环代替 FindIndex，免去委托调用
            var hyd = DrinkDatabase.Hydration;
            int idx = -1;
            for (int i = 0; i < panel.amountsLines.Count; i++)
            {
                if (panel.amountsLines[i].amount == hyd) { idx = i; break; }
            }
            if (idx < 0) return;   // 还没建出来，TryBuildRow 会处理

            var line = panel.amountsLines[idx];
            if (line.go == null) return;

            var amounts = target.GetAmounts();
            if (amounts == null)
            {
                if (!_loggedResolve)
                {
                    _loggedResolve = true;
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] " + target.name
                                     + " 的 GetAmounts() 为 null，行保持隐藏");
                }
                return;
            }

            // U60: TryUpdate 新增 forceRefresh，增量刷新传 false 维持原行为
            bool show = line.TryUpdate(amounts, false);
            if (line.go.activeSelf != show) line.go.SetActive(show);

            if (!show && !_loggedResolve)
            {
                // 诊断信息：特殊实体（如总部）本来就没有 Hydration，属预期，不作为异常上报
                _loggedResolve = true;
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/" + source + "] " + target.name
                                 + " 没有 Hydration AmountInstance（值="
                                 + DrinkDatabase.GetValue(target) + "），行被隐藏");
            }
        }
    }

    // 主通道：拦截 AddAmountLine，在建完 Calories 行的那一刻紧跟插入
    [HarmonyPatch(typeof(MinionVitalsPanel), "AddAmountLine")]
    public static class DrinkUI_AddAmountLine_Patch
    {
        // 主通道补丁：原版建「卡路里」行时紧跟其后插入我们的「饮水」行
        public static void Postfix(MinionVitalsPanel __instance, Amount amount)
        {
            try
            {
            if (!DrinkUIRow.IsAnchor(amount)) return;   // 只在 Calories 之后插
            MFLog.Info(LogCat.Drink,"[MoreFood/Drink][UI/AddAmountLine] 抓到 Calories 行，准备紧跟插入");
            try
            {
                DrinkUIRow.TryBuildRow(__instance, "AddAmountLine");
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/AddAmountLine] 异常: " + e);
            }
            }
            catch (System.Exception ex) { MFLog.HotFail("patch:DrinkUI_AddAmountLine_Patch", ex); }
        }
    }

    // 兜底通道：Refresh —— 补建（若主通道没生效）+ 每帧结算显隐
    [HarmonyPatch(typeof(MinionVitalsPanel), "Refresh")]
    public static class DrinkUI_Refresh_Patch
    {
        // 日志只打一次
        private static bool _logged;

        // 兜底通道补丁：主通道没生效时补建饮水行，并每帧结算显隐
        public static void Postfix(MinionVitalsPanel __instance, GameObject selectedEntity)
        {
            try
            {
            if (!_logged)
            {
                _logged = true;
                // 无条件打一条：确认补丁到底有没有被调用（Refresh 必然会被调用）
                MFLog.Info(LogCat.Drink,"[MoreFood/Drink][UI/Refresh] 补丁触发 Ready=" + DrinkDatabase.Ready
                          + " target=" + (selectedEntity == null ? "null" : selectedEntity.name));
            }

            if (selectedEntity == null) return;

            try
            {
                // 只给复制人加，仿生人（机器人）直接跳过
                if (DrinkDatabase.IsBionic(selectedEntity)) return;

                if (DrinkUIRow.TryBuildRow(__instance, "Refresh"))
                    DrinkUIRow.ResolveRow(__instance, selectedEntity, "Refresh");
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Refresh] 异常: " + e);
            }
            }
            catch (System.Exception ex) { MFLog.HotFail("patch:DrinkUI_Refresh_Patch", ex); }
        }
    }
}
