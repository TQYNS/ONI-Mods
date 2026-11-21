// 用途：交易界面状态核心：字段、行引用、取值、侧栏与确认入口

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class TradeScreenState : KMonoBehaviour
    {
        internal SpaceStation station;
        internal StationDef def;
        internal TradeStationState state;
        internal bool remote;
        internal bool donate;      // ★ 捐款窗模式（只有美食猎人协会）
        internal int page;
        internal string remoteTag;          // 远程视图当前选中的分类 Tag
        internal string blockReason;
        // ★ 交易列表筛选标签：仅临时改变显示，窗口每次重建（重新打开）都是新实例 ⇒ 自动复位为均不勾选
        internal bool filterSellOnly;
        internal bool filterBuyOnly;
        internal readonly Dictionary<string, float> amounts = new Dictionary<string, float>();
        internal GameObject root;
        internal GameObject body;
        internal GameObject side;
        internal GameObject dialog;
        internal TextMeshProUGUI status;
        internal TextMeshProUGUI title;
        internal TextMeshProUGUI wallet;
        internal GameObject pendingShake;
        internal TextMeshProUGUI shakeLabel;
        private string pendingShakeId;
        private bool tagGroupsBuilt;

        // 翻页性能：整窗复用同一份持有量快照与行清单，见 交易.md P-50
        private Dictionary<string, float> holdSnap;
        private List<string> rowCache;
        private GameObject rowsRoot;
        private TextMeshProUGUI pageLabel;
        private int pageCount = 1;

        // ★ 性能修复：运行时文本去抖 —— 仅在数值变化时重写 status/wallet（避免每帧拼接 + ToString 分配）
        private TradeStationState lastStatusState;
        private int lastStatusLeftSec = int.MinValue;
        private int lastStatusCurrency = int.MinValue;
        private int lastStatusRep = int.MinValue;
        private int lastWalletCents = int.MinValue;
        private readonly List<string> tagOrder = new List<string>();
        private readonly Dictionary<string, List<TradeItemValue>> tagGroups =
            new Dictionary<string, List<TradeItemValue>>(StringComparer.Ordinal);

        internal sealed class RowRef
        {
            internal string id;
            internal TextMeshProUGUI amount;
            internal Image minusImg;
            internal Image plusImg;
            internal TextMeshProUGUI minusTxt;
            internal TextMeshProUGUI plusTxt;
            internal float floor;
            internal float ceil;
        }

        internal readonly Dictionary<string, RowRef> rowRefs = new Dictionary<string, RowRef>();

        internal const int LocalPageSize = 12;

        internal StationCatalog Cat()
        {
            return def == null ? null : TradeCatalog.Catalog(def);
        }

        internal Clustercraft Craft()
        {
            // ★ Bug4：同格或相邻一格都算"本站"（与火箭侧、交易站按钮判定统一）。
            return station != null
                ? TradeUtil.CraftNear(station.Location)
                : TradeUtil.FindCraftAt(AxialI.ZERO);
        }

        // 回收不设上限：在收购清单内即返回无限，供"是否可卖"判定使用；显示层单独处理
        internal float RecycleLeftOf(string id)
        {
            StationCatalog c = Cat();
            TradeItemValue v;
            if (c == null || state == null || !c.accept.TryGetValue(id, out v) || v == null)
            {
                return 0f;
            }
            return TradeExecutor.RecycleLeft(state, v);
        }

        internal TradeItemValue ValueOf(string id)
        {
            StationCatalog c = Cat();
            if (c == null)
            {
                return null;
            }
            TradeItemValue v;
            if (c.sell.TryGetValue(id, out v) && v != null)
            {
                return v;
            }
            if (c.accept.TryGetValue(id, out v) && v != null)
            {
                return v;
            }
            return null;
        }

        internal void SetAmountLive(string id, float v)
        {
            amounts[id] = v;
            RowRef r;
            if (rowRefs.TryGetValue(id, out r) && r != null)
            {
                RefreshRowVisual(r, v);
            }
            RefreshSide();
        }

        internal void SetSideText(string name, string v)
        {
            if (side == null)
            {
                return;
            }
            Transform t = side.transform.Find("content/" + name);
            if (t == null)
            {
                t = side.transform.Find(name);
            }
            if (t == null)
            {
                return;
            }
            TextMeshProUGUI x = t.GetComponent<TextMeshProUGUI>();
            if (x != null)
            {
                x.text = v;
            }
        }

        private void SetSideColor(string name, Color c)
        {
            if (side == null)
            {
                return;
            }
            Transform t = side.transform.Find("content/" + name);
            if (t == null)
            {
                t = side.transform.Find(name);
            }
            if (t == null)
            {
                return;
            }
            TextMeshProUGUI x = t.GetComponent<TextMeshProUGUI>();
            if (x != null)
            {
                x.color = c;
            }
        }

        private float UnitOf(string id, float amount)
        {
            TradeItemValue v = ValueOf(id);
            if (v == null)
            {
                return 1f;
            }
            return amount < 0f ? TradeRegistry.SellUnit(def, v) : TradeRegistry.BuyUnit(def, v);
        }

        private void RefreshSide()
        {
            if (side == null)
            {
                return;
            }
            if (donate)
            {
                RefreshDonateSide();
                return;
            }
            float cost = 0f;
            float gain = 0f;
            List<string> sellLines = new List<string>();
            List<string> buyLines = new List<string>();
            foreach (KeyValuePair<string, float> kv in amounts)
            {
                if (Mathf.Abs(kv.Value) < 0.0001f)
                {
                    continue;
                }
                if (kv.Value < 0f)
                {
                    float v = UnitOf(kv.Key, -1f) * (-kv.Value);
                    gain += v;
                    sellLines.Add(TradeUtil.DisplayName(kv.Key) + "   ×" + Mathf.Round(-kv.Value)
                        + "   " + v.ToString("0.00"));
                }
                else
                {
                    float v = UnitOf(kv.Key, 1f) * kv.Value;
                    cost += v;
                    buyLines.Add(TradeUtil.DisplayName(kv.Key) + "   ×" + Mathf.Round(kv.Value)
                        + "   " + v.ToString("0.00"));
                }
            }
            SetSideText("shop", "本店星币   " + (state != null ? state.currency.ToString("0.00") : "—"));
            // ★ 原来这里是"我的星币 + 净变化 + 预期结余"三行，实测溢出面板 ⇒
            float keep = TradeRegistry.Currency + gain - cost;
            SetSideText("cash", "预期结余   " + keep.ToString("0.00"));
            SetSideColor("cash", keep < -0.0001f ? TradeUi.Neg : TradeUi.TextMain);
            for (int i = 0; i < 7; i++)
            {
                SetSideText("s" + i, i < sellLines.Count ? sellLines[i] : "");
                SetSideText("b" + i, i < buyLines.Count ? buyLines[i] : "");
            }
            SetSideText("sg", "出售合计   " + gain.ToString("0.00"));
        }

        private void RefreshRowVisual(RowRef r, float v)
        {
            if (r.amount != null)
            {
                r.amount.text = v.ToString("0");
                r.amount.color = v > 0f ? TradeUi.Pos : (v < 0f ? TradeUi.Neg : TradeUi.TextDim);
            }
            bool atFloor = v <= r.floor + 0.0001f;
            bool atCeil = v >= r.ceil - 0.0001f;
            if (r.minusImg != null)
            {
                r.minusImg.color = atFloor ? TradeUi.TextOff : TradeUi.Border;
            }
            if (r.plusImg != null)
            {
                r.plusImg.color = atCeil ? TradeUi.TextOff : TradeUi.Border;
            }
            if (r.minusTxt != null)
            {
                r.minusTxt.color = atFloor ? TradeUi.TextOff : TradeUi.TextMain;
            }
            if (r.plusTxt != null)
            {
                r.plusTxt.color = atCeil ? TradeUi.TextOff : TradeUi.TextMain;
            }
        }

        private void Update()
        {
            try
            {
            ClusterMapScreen map = ClusterMapScreen.Instance;
            if (map == null || !map.gameObject.activeInHierarchy)
            {
                if (station != null && root != null)
                {
                    MFLog.Info(LogCat.Trade,"[Space] 星图已关闭 → 自动收起交易界面");
                    if (remote)
                    {
                        TradeUi.CloseRemote();
                    }
                    else
                    {
                        TradeUi.CloseWindow();
                    }
                    root = null;
                }
                return;
            }
            if (station == null)
            {
                return;
            }
            if (status != null && state != null)
            {
                float left = Mathf.Max(0f, state.cooldownEnd - TradeRegistry.Now());
                int leftSec = left <= 0f ? 0 : Mathf.CeilToInt(left);
                int currency = Mathf.FloorToInt(state.currency + 0.5f);   // 对齐 ToString("0") 的显示粒度
                int rep = (int)TradeRegistry.Rep;
                // TPI 对所有站点的卖价生效，声望信息始终显示
                // ★ 仅当决定文本的数值/引用变化时才重写（原实现每帧重写）
                if (state != lastStatusState || leftSec != lastStatusLeftSec || currency != lastStatusCurrency
                    || rep != lastStatusRep)
                {
                    lastStatusState = state;
                    lastStatusLeftSec = leftSec;
                    lastStatusCurrency = currency;
                    lastStatusRep = rep;
                    int tpiPercent = Mathf.RoundToInt(TradeValuation.TpiOf(TradeRegistry.RankIndex()) * 100f);
                    string rankInfo = "   声望 " + TradeRegistry.RankName() + "（" + TradeRegistry.Rep
                        + "/550）出售 +" + tpiPercent + "%";
                    string money = "   本店星币 " + state.currency.ToString("0");
                    status.text = leftSec <= 0
                        ? "库存冷却已结束：下次打开窗口时重新抽签" + rankInfo + money
                        : "冷却中 " + leftSec + " 秒 · 本次数据来自上次刷新" + rankInfo + money;
                    status.color = leftSec <= 0 ? TradeUi.Pos : TradeUi.Warn;
                }
            }
            if (wallet != null)
            {
                int cents = Mathf.FloorToInt(TradeRegistry.Currency * 100f + 0.5f);
                if (cents != lastWalletCents)
                {
                    lastWalletCents = cents;
                    wallet.text = "星币 " + TradeRegistry.Currency.ToString("0.00");
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("update:TradeScreen", e); }
        }

        // ★ 性能修复：外部直接改过 status（如 Fail 的提示）后调用，强制下一帧重写
        private void InvalidateStatusCache()
        {
            lastStatusState = null;
            lastStatusLeftSec = int.MinValue;
            lastStatusCurrency = int.MinValue;
            lastStatusRep = int.MinValue;
            lastWalletCents = int.MinValue;
        }

        internal void EnsureFresh()
        {
            if (def == null || state == null)
            {
                return;
            }
            if (TradeRegistry.RefreshIfDue(state, def))
            {
                amounts.Clear();
                tagGroupsBuilt = false;
                InvalidateRows();
            }
        }

        internal float Amount(string id)
        {
            float v;
            return amounts.TryGetValue(id, out v) ? v : 0f;
        }

        /// <summary>本轮显示用的「火箭持有量」快照（prefabId → 份数）：整窗重建时失效，窗口内只扫一遍。</summary>
        private Dictionary<string, float> HoldSnap()
        {
            if (holdSnap == null)
            {
                Clustercraft craft = Craft();
                holdSnap = craft != null
                    ? RocketInventory.Snapshot(craft)
                    : new Dictionary<string, float>(StringComparer.Ordinal);
            }
            return holdSnap;
        }

        /// <summary>该物品在火箭上可交易的份数（与结算校验同源：货舱 + 内舱散落 + 单件物品运输仓）。</summary>
        internal float CarriedOf(string id)
        {
            float v;
            return HoldSnap().TryGetValue(id, out v) && v > 0f ? v : 0f;
        }

        /// <summary>作废行清单与持有量快照（库存/持有量/筛选变化时调用）。</summary>
        private void InvalidateRows()
        {
            rowCache = null;
            holdSnap = null;
        }

        internal void ClearCart()
        {
            amounts.Clear();
            CloseDialog();
            Rebuild();
        }

        internal void Confirm()
        {
            // ★ 任何原因导致的失败都弹"具体原因"；发生异常则弹"交易异常，请查看日志"（不暴露堆栈）
            try
            {
                ConfirmCore();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 交易异常(Confirm)");
                ShowResultDialog("交易异常", "交易异常，请查看日志", true);
            }
        }
    }
}
