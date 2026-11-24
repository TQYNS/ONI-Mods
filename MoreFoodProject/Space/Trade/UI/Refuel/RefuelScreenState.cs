// 用途：加油窗运行状态：百分比换算、油量条、预期结算与确认

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed class RefuelScreenState : KMonoBehaviour
    {
        internal GameObject root;
        internal SpaceStation station;
        internal StationDef def;
        internal Clustercraft craft;
        internal string fuelTag;
        internal string fuelName;
        internal bool oxidizerMode;
        internal List<Tag> oxidizerTags;
        internal Tag oxidizerTag;
        internal List<Storage> oxStorages;   // 打开窗口时抓一次，拖动滑条只读 MassStored
        internal float price;
        internal float capacity;
        internal TextMeshProUGUI wallet;
        internal TextMeshProUGUI walletSideText;
        internal TextMeshProUGUI unitText;
        internal TextMeshProUGUI addText;
        internal TextMeshProUGUI costText;
        internal TextMeshProUGUI keepText;
        internal TextMeshProUGUI readout;
        internal Image haveImg;
        internal Image addImg;
        internal Image markerImg;
        internal Button confirmBtn;
        internal AdvancedSlider slider;
        internal AdvancedSlider gaugeSlider;

        private float targetPct;
        private GameObject dialog;

        internal void Init()
        {
            targetPct = CurPct();
            if (slider != null)
            {
                slider.Refresh();
            }
            if (gaugeSlider != null)
            {
                gaugeSlider.Refresh();
            }
            Refresh();
        }

        internal float TargetPct()
        {
            return targetPct;
        }

        internal void SetTarget(float v)
        {
            targetPct = Mathf.Clamp(v, CurPct(), 100f);
            if (slider != null)
            {
                slider.Refresh();
            }
            Refresh();
        }

        internal void ResetTarget()
        {
            SetTarget(CurPct());
        }

        // 一键补充：按当前星币算出买得起的最大加注量，再反推目标百分比（模拟拖动）
        internal void FillAll()
        {
            float cur = CurPct();
            float room = TradeNumber.FuelKg(Mathf.Max(0f, capacity - CurrentKg()));
            float affordable = price > 0f ? TradeNumber.Money(TradeRegistry.Currency) / price : room;
            float want = TradeNumber.FuelKg(Mathf.Min(room, affordable));
            float pct = cur + (capacity > 0f ? want / capacity * 100f : 0f);
            SetTarget(Mathf.Min(100f, pct));
        }

        internal void Confirm()
        {
            try
            {
                CloseDialog();
                // 结算前按实时状态刷新一次：窗口不轮询，只在打开 / 拖动 / 确认时算
                capacity = TradeNumber.FuelKg(oxidizerMode ? TradeExecutor.OxidizerCapacityOf(oxStorages) :
                    (craft != null && craft.ModuleInterface != null ? craft.ModuleInterface.FuelCapacity : 0f));
                price = TradeNumber.Money(oxidizerMode ? TradeDb.OxidizerPricePerKg(oxidizerTag.Name)
                    : TradeDb.FuelPricePerKg(fuelTag));
                Refresh();
                float add = AddKg();
                float cost;
                if (add <= 0f)
                {
                    ShowDialog(oxidizerMode ? "无需补充" : "无需加油",
                        oxidizerMode ? "目标量没有高于当前量，先把滑条往右拖" : "目标油量没有高于当前油量，先把滑条往右拖", true);
                    return;
                }
                string reason;
                float actual;
                TradeStationState st = station != null ? TradeRegistry.EnsureState(station.stationId) : null;
                bool ok = oxidizerMode
                    ? TradeExecutor.TryRefuelOxidizer(craft, def, st, oxidizerTag, add, out cost, out reason, out actual)
                    : TradeExecutor.TryRefuel(craft, def, st, add, out cost, out reason, out actual);
                if (!ok)
                {
                    MFLog.Warn(LogCat.Trade,"[Space] " + (oxidizerMode ? "补氧化剂" : "加油") + "失败：" + reason);
                    ShowDialog((oxidizerMode ? "补氧化剂" : "加油") + "失败",
                        string.IsNullOrEmpty(reason) ? "补注失败" : reason, true);
                    return;
                }
                if (TradeSaveData.Attach() != null)
                {
                    TradeSaveData.Attach()?.Capture();
                }
                SpaceAchievements.Unlock(SpaceAchievements.FirstTradeId, "第一桶金");
                string action = oxidizerMode ? "补氧化剂" : "加油";
                MFLog.Info(LogCat.Trade,"[Space] " + action + "完成：" + fuelName + " " + actual.ToString("0.0")
                    + " 千克，花费 " + cost.ToString("0.00") + " 星币，余额 "
                    + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00"));
                ResetTarget();
                ShowDialog(oxidizerMode ? "补充完成" : "加油完成",
                    (oxidizerMode ? "已补入 " : "已加注 ") + actual.ToString("0.0") + " 千克 "
                    + fuelName + "，花费 " + cost.ToString("0.00") + " 星币", false);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 加油异常(Confirm)");
                ShowDialog("加油异常", "加油异常，请查看日志", true);
            }
        }

        internal void ShowDialog(string title, string message, bool neg)
        {
            CloseDialog();
            Transform parent = root != null ? root.transform : TradeUi.OverlayParent();
            if (parent == null)
            {
                return;
            }
            dialog = TradeUi.MakeResultDialog(parent, 90f, 110f, 520f, 200f,
                title, message, neg, CloseDialog);
            dialog.transform.SetAsLastSibling();   // 弹窗永远压在最上层，避免被面板盖住
        }

        internal void CloseDialog()
        {
            if (dialog != null)
            {
                UnityEngine.Object.Destroy(dialog);
                dialog = null;
            }
        }

        internal void HandleBlockerClick()
        {
            if (dialog != null)
            {
                CloseDialog();
                return;
            }
            TradeUi.CloseRefuel();
        }

        private float CurrentKg()
        {
            if (craft == null)
            {
                return 0f;
            }
            return oxidizerMode ? TradeExecutor.OxidizerRemainingOf(oxStorages)
                : (craft.ModuleInterface != null ? Mathf.Max(0f, craft.ModuleInterface.FuelRemaining) : 0f);
        }

        private float CurPct()
        {
            return capacity > 0.0001f ? Mathf.Clamp01(CurrentKg() / capacity) * 100f : 0f;
        }

        private float AddKg()
        {
            float cur;
            float target;
            float add;
            float cost;
            float keep;
            Compute(out cur, out target, out add, out cost, out keep);
            return add;
        }

        // 目标% → 加注千克 → 花费 → 预期结余；全窗口唯一一处换算口径
        private void Compute(out float cur, out float target, out float add, out float cost, out float keep)
        {
            float have = CurrentKg();
            cur = capacity > 0f ? Mathf.Clamp01(have / capacity) * 100f : 0f;
            target = Mathf.Clamp(targetPct, cur, 100f);
            add = TradeNumber.FuelKg(Mathf.Min(Mathf.Max(0f, (target - cur) * 0.01f * capacity),
                Mathf.Max(0f, capacity - have)));
            cost = TradeNumber.Money(add * price);
            keep = TradeNumber.Money(TradeRegistry.Currency - cost);
        }

        private void Refresh()
        {
            if (root == null)
            {
                return;
            }
            float cur;
            float target;
            float add;
            float cost;
            float keep;
            Compute(out cur, out target, out add, out cost, out keep);
            if (unitText != null)
            {
                unitText.text = TradeNumber.Money(price).ToString("0.00") + " /千克";
            }
            if (targetPct < cur)
            {
                targetPct = cur;
                if (slider != null)
                {
                    slider.Refresh();
                }
            }
            float full = 360f;
            float haveW = full * Mathf.Clamp01(cur / 100f);
            float addW = full * Mathf.Clamp01(capacity > 0.0001f ? add / capacity : 0f);
            float targetW = full * Mathf.Clamp01(target / 100f);
            if (haveImg != null)
            {
                haveImg.gameObject.SetActive(haveW >= 1f);
                haveImg.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, haveW), 24f);
            }
            if (addImg != null)
            {
                addImg.gameObject.SetActive(addW >= 1f);
                addImg.rectTransform.anchoredPosition = new Vector2(20f + haveW, -116f);
                addImg.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, addW), 24f);
            }
            if (markerImg != null)
            {
                markerImg.rectTransform.anchoredPosition = new Vector2(20f + targetW, -114f);
            }
            if (addText != null)
            {
                addText.text = add.ToString("0.0") + " 千克";
            }
            if (costText != null)
            {
                costText.text = "-" + TradeNumber.Money(cost).ToString("0.00");
            }
            if (walletSideText != null)
            {
                walletSideText.text = TradeNumber.Money(TradeRegistry.Currency).ToString("0.00");
            }
            if (keepText != null)
            {
                keepText.text = keep.ToString("0.00");
                keepText.color = keep < -0.0001f ? TradeUi.Neg : TradeUi.TextMain;
            }
            if (wallet != null)
            {
                wallet.text = "星币 " + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00");
            }
            if (confirmBtn != null)
            {
                confirmBtn.interactable = add > 0f;   // 星币不足也允许点，交给结算拒绝并弹窗说明原因
            }
        }


    }
}
