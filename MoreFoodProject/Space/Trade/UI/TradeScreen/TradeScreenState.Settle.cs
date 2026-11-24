// 用途：交易结算：确认核心、部分成交、结果对话框与失败提示

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class TradeScreenState : KMonoBehaviour
    {
        private void ConfirmCore()
        {
            TradeUi.NumberBox.CommitActive();   // 先落地正在编辑的数量，结算读到的才是输入值
            if (!string.IsNullOrEmpty(blockReason))
            {
                Fail(null, blockReason);
                return;
            }
            Clustercraft craft = Craft();
            if (craft == null)
            {
                Fail(null, "火箭不在本站：远程不能交易");
                return;
            }
            List<string> ids = new List<string>(amounts.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                float a = TradeNumber.SignedUnits(amounts[ids[i]]);
                if (a == 0f)
                {
                    continue;
                }
                if (a > 0f)
                {
                    TradeStockEntry entry = state.Find(ids[i]);
                    if (entry == null)
                    {
                        Fail(ids[i], TradeUtil.DisplayName(ids[i]) + " 不在本站售卖清单");
                        return;
                    }
                    if (TradeNumber.Units(entry.stock) < a)
                    {
                        Fail(ids[i], TradeUtil.DisplayName(ids[i]) + " 库存不足（" + Mathf.Round(entry.stock) + "）");
                        return;
                    }
                }
                else
                {
                    float want = -a;
                    // 回收不设上限：可卖判定只看是否在收购清单，数量只受持有量限制
                    if (ValueOf(ids[i]) == null || !(RecycleLeftOf(ids[i]) > 0.0001f))
                    {
                        Fail(ids[i], TradeUtil.DisplayName(ids[i]) + " 不在本站收购清单");
                        return;
                    }
                    float have = TradeNumber.Units(RocketInventory.Available(craft, new Tag(ids[i])));
                    if (have < want)
                    {
                        Fail(ids[i], TradeUtil.DisplayName(ids[i]) + " 火箭里只有 " + Mathf.Round(have));
                        return;
                    }
                }
            }

            float cost = 0f;
            float gain = 0f;
            for (int i = 0; i < ids.Count; i++)
            {
                float a = TradeNumber.SignedUnits(amounts[ids[i]]);
                if (a == 0f)
                {
                    continue;
                }
                if (a > 0f)
                {
                    cost = TradeNumber.Money(cost + TradeNumber.Money(UnitOf(ids[i], 1f) * a));
                }
                else
                {
                    gain = TradeNumber.Money(gain + TradeNumber.Money(UnitOf(ids[i], -1f) * (-a)));
                }
            }
            if (TradeRegistry.Currency + gain + 0.0001f < cost)
            {
                Fail(null, "星币不足：需要 " + cost.ToString("0.00") + "，只有 "
                    + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00") + "（含卖出 " + TradeNumber.Money(gain).ToString("0.00") + "）");
                return;
            }
            if (gain > 0f && TradeNumber.Money(state.currency) + 0.0001f < gain)
            {
                ShowPartialDialog(gain);
                return;
            }
            Settle(false);
        }

        private void ShowPartialDialog(float gain)
        {
            CloseDialog();
            Transform parent = root != null ? root.transform : TradeUi.OverlayParent();
            if (parent == null)
            {
                return;
            }
            dialog = TradeUi.Node("partialDialog", parent, 250f, 210f, 680f, 300f).gameObject;
            dialog.transform.SetAsLastSibling();   // 弹窗永远最上层
            TradeUi.RoundPanel("bg", dialog.transform, 0f, 0f, 680f, 300f, 14, TradeUi.Panel);
            TradeUi.HLine("top", dialog.transform, 0f, 0f, 680f, TradeUi.Gold);
            TradeUi.Text("t", dialog.transform, 28f, 22f, 624f, 34f, "本店星币不足", 22f, TradeUi.Gold, false);
            TradeUi.Text("l1", dialog.transform, 28f, 74f, 624f, 28f,
                "本店剩余星币：" + TradeNumber.Money(state.currency).ToString("0.00"), 16f, TradeUi.TextMain, false);
            TradeUi.Text("l2", dialog.transform, 28f, 106f, 624f, 28f,
                "本次回收应付：" + TradeNumber.Money(gain).ToString("0.00"), 16f, TradeUi.TextMain, false);
            TradeUi.Text("l3", dialog.transform, 28f, 138f, 624f, 28f,
                "超出：" + TradeNumber.Money(gain - state.currency).ToString("0.00"), 16f, TradeUi.Neg, false);
            TradeUi.Text("l4", dialog.transform, 28f, 176f, 624f, 24f,
                "部分结算：按物品列表顺序逐条结算，直到本店星币耗尽", 14f, TradeUi.TextDim, false);
            TradeUi.MakeButton("部分结算", dialog.transform, 260f, 228f, 180f, 48f,
                TradeUi.TabOn, TradeUi.Accent, TradeUi.Accent, delegate { Settle(true); });
            TradeUi.MakeButton("取消交易", dialog.transform, 456f, 228f, 180f, 48f,
                TradeUi.PanelAlt, TradeUi.Border, TradeUi.TextMain, delegate { CloseDialog(); });
        }

        /// <summary>★ 第二十一轮-1/2：内部结果弹窗（成功/失败/异常统一走这里；不用左侧通知）。
        internal void ShowResultDialog(string title, string message, bool neg)
        {
            CloseDialog();
            Transform parent = root != null ? root.transform : TradeUi.OverlayParent();
            if (parent == null)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 结果弹窗无父节点：" + title + " / " + message);
                return;
            }
            dialog = TradeUi.MakeResultDialog(parent, 250f, 236f, 680f, 248f,
                title, message, neg, CloseDialog);
            dialog.transform.SetAsLastSibling();   // 弹窗永远最上层
        }

        internal void CloseDialog()
        {
            if (dialog != null)
            {
                UnityEngine.Object.Destroy(dialog);
                dialog = null;
            }
        }

        /// <summary>★ 第十九轮-4：点击遮罩（窗口外非交互区）→ 优先关内部弹窗，其次关本窗口。</summary>
        internal void HandleBlockerClick()
        {
            if (dialog != null)
            {
                CloseDialog();
                return;
            }
            if (remote)
            {
                TradeUi.CloseRemote();
            }
            else
            {
                TradeUi.CloseWindow();
            }
        }

        private bool settling;   // ★ 防连点：一次确认只允许走到结算结束，避免重复结算/卡死

        private void Settle(bool allowPartial)
        {
            if (settling)
            {
                return;   // 上一次结算尚未收尾（双击/连点）→ 直接忽略，不再叠一次
            }
            settling = true;
            // ★ 结算异常统一弹窗（不暴露堆栈）
            try
            {
                SettleCore(allowPartial);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 交易异常(Settle)");
                ShowResultDialog("交易异常", "交易异常，请查看日志", true);
            }
            finally
            {
                settling = false;
                // ★ 关键：无论成功、失败还是中途抛异常，都必须清空本项目数量。
                //   旧实现只在 SettleCore 正常走完时清空，一旦中途异常/中止，残留的数量会在
                //   下一次点“确认交易”时被重复结算（表现为同一件物品反复成交、库存/收购量错位）。
                amounts.Clear();
                TradeUi.NumberBox.DropActive();   // 丢弃仍未落地的输入态，避免下次确认读到旧文本
                if (body != null || side != null)
                {
                    Rebuild();
                }
            }
        }

        private void SettleCore(bool allowPartial)
        {
            CloseDialog();
            Clustercraft craft = Craft();
            if (craft == null)
            {
                Fail(null, "火箭不在本站：远程不能交易");
                return;
            }
            List<string> ids = new List<string>(amounts.Keys);
            int done = 0;
            string reason = null;
            float totalGain = 0f;
            for (int i = 0; i < ids.Count; i++)
            {
                float a = TradeNumber.SignedUnits(amounts[ids[i]]);
                if (a >= 0f)
                {
                    continue;
                }
                float want = -a;
                // 回收不设上限：出售下限只钳到当前持有量，防"面板打开后飞船状态变化"
                float haveNow = TradeNumber.Units(RocketInventory.Available(craft, new Tag(ids[i])));
                float cap = haveNow;
                if (want > cap)
                {
                    want = cap;
                }
                if (!(want > 0f))
                {
                    reason = TradeUtil.DisplayName(ids[i]) + " 可卖 0（船上持有 " + Mathf.Floor(haveNow) + "）";
                    break;
                }
                if (allowPartial)
                {
                    float unit = UnitOf(ids[i], -1f);
                    float affordable = unit > 0f ? Mathf.Floor(TradeNumber.Money(state.currency) / unit) : 0f;
                    if (affordable <= 0f)
                    {
                        MFLog.Info(LogCat.Trade,"[Space] 部分结算：本店星币耗尽，余下收购项跳过");
                        break;
                    }
                    if (affordable < want)
                    {
                        want = affordable;
                    }
                }
                float g = 0f;
                if (!TradeExecutor.TrySell(craft, def, state, ids[i], want, out g, out reason))
                {
                    MFLog.Warn(LogCat.Trade,"[Space] 交易中止于 " + ids[i] + "：" + reason + "（已成交 " + done + " 项不回滚）");
                    break;
                }
                totalGain += g;
                done++;
            }
            for (int i = 0; i < ids.Count; i++)
            {
                float a = TradeNumber.SignedUnits(amounts[ids[i]]);
                if (a <= 0f)
                {
                    continue;
                }
                float c = 0f;
                if (!TradeExecutor.TryBuy(craft, def, state, ids[i], a, out c, out reason))
                {
                    MFLog.Warn(LogCat.Trade,"[Space] 交易中止于 " + ids[i] + "：" + reason + "（已成交 " + done + " 项不回滚）");
                    break;
                }
                done++;
            }
            MFLog.Info(LogCat.Trade,"[Space] 本次交易完成 " + done + " 项，卖出收入 " + totalGain.ToString("0.00")
                + "，余额 " + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00")
                + "，本店星币 " + state.currency.ToString("0.00"));
            if (done > 0)
            {
                SpaceAchievements.Unlock(SpaceAchievements.FirstTradeId, "第一桶金");
            }
            // amounts 的清空统一放到 Settle 的 finally（异常路径也必须清），这里不再重复清
            if (TradeSaveData.Attach() != null)
            {
                TradeSaveData.Attach()?.Capture();
            }
            // 刷新统一交给 Settle 的 finally（那时 amounts 已清空），见 交易.md P-51
            // ★ 交易结果统一走"内部弹窗"（成功/中止都用弹窗，含具体原因）
            if (!string.IsNullOrEmpty(reason))
            {
                ShowResultDialog("交易未完成", reason, true);
            }
            else if (done > 0)
            {
                ShowResultDialog("交易完成", "本次完成 " + done + " 项交易"
                    + (totalGain > 0.0001f ? "（卖出收入 " + totalGain.ToString("0.00") + " 星币）" : ""), false);
            }
        }

        private void Fail(string rowId, string message)
        {
            InvalidateStatusCache();   // ★ 性能修复：Fail 直接改 status，须让下一帧 Update 重新接管
            if (status != null)
            {
                status.text = message;
                status.color = TradeUi.Neg;
            }
            MFLog.Warn(LogCat.Trade,"[Space] 交易被拒绝：" + message);
            pendingShakeId = rowId;
            Rebuild();
            // ★ 交易失败用"内部弹窗"提示具体原因（不再用左侧通知）
            ShowResultDialog("交易失败", string.IsNullOrEmpty(message) ? "交易失败" : message, true);
        }

        internal void Rebuild()
        {
            if (body != null)
            {
                UnityEngine.Object.Destroy(body);
            }
            if (side != null)
            {
                UnityEngine.Object.Destroy(side);
            }
            body = null;
            side = null;
            rowsRoot = null;
            pageLabel = null;
            pageCount = 1;
            pendingShake = null;
            shakeLabel = null;
            rowRefs.Clear();
            // ★ 整窗重建 ⇒ 行清单与持有量快照一起作废（库存/持有量在两次重建之间可能已变）
            InvalidateRows();
            if (remote)
            {
                if (donate)
                {
                    BuildDonateRemoteBody();
                }
                else
                {
                    BuildRemoteBody();
                }
            }
            else if (donate)
            {
                BuildDonateBody();
            }
            else
            {
                BuildBody();
                if (string.IsNullOrEmpty(blockReason))
                {
                    BuildSide();
                }
            }
            if (pendingShake != null)
            {
                TradeUi.Shaker sh = pendingShake.GetComponent<TradeUi.Shaker>();
                if (sh == null)
                {
                    sh = pendingShake.AddComponent<TradeUi.Shaker>();
                }
                sh.target = pendingShake.GetComponent<RectTransform>();
                sh.label = shakeLabel;
                sh.Begin(0.35f);
                pendingShake = null;
                shakeLabel = null;
                pendingShakeId = null;
            }
            // 弹窗必须压在最上层，否则会被新建的两栏盖住只露出中缝（见 交易.md P-51）
            if (dialog != null)
            {
                dialog.transform.SetAsLastSibling();
            }
        }
    }
}
