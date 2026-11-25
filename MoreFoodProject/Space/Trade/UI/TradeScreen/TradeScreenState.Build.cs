// 用途：商品列表构建：行列表、燃料行、行控件与侧栏

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class TradeScreenState : KMonoBehaviour
    {
        // 本地窗口
        private void BuildBody()
        {
            body = TradeUi.Node("body", root.transform, 14f, 106f, 740f, 600f).gameObject;
            TradeUi.RoundPanel("bodybg", body.transform, 0f, 0f, 740f, 600f, 12, TradeUi.PanelAlt);
            if (!string.IsNullOrEmpty(blockReason))
            {
                TradeUi.Text("blocked", body.transform, 24f, 100f, 700f, 60f, blockReason, 18f, TradeUi.Warn, false);
                return;
            }
            // 计量口径统一：交易全链路以「份」为单位（食物 1 份 = 1kg，单件物品 1 份 = 1 件）
            TradeUi.Text("h1", body.transform, 16f, 8f, 176f, 26f, "物品", 14f, TradeUi.TextDim, false);
            TradeUi.Text("h2", body.transform, 196f, 8f, 76f, 26f, "售价/份", 13f, TradeUi.TextDim, true);
            TradeUi.Text("h3", body.transform, 280f, 8f, 68f, 26f, "库存", 13f, TradeUi.TextDim, true);
            TradeUi.Text("h4", body.transform, 352f, 8f, 68f, 26f, "持有", 13f, TradeUi.TextDim, true);
            TradeUi.Text("h5", body.transform, 428f, 8f, 100f, 26f, "份数(负卖)", 13f, TradeUi.TextDim, false);
            TradeUi.HLine("headline", body.transform, 14f, 36f, 712f, TradeUi.Line);

            // 行区独立成层，翻页只重建这一层（见 交易.md P-50）
            rowsRoot = TradeUi.Node("rows", body.transform, 0f, 0f, 740f, 600f).gameObject;
            BuildRows();
            if (pageCount > 1)
            {
                TradeUi.MakeButton("<", body.transform, 574f, 566f, 44f, 30f, TradeUi.TabOff, TradeUi.Border,
                    TradeUi.TextMain, delegate { TurnPage(-1); });
                pageLabel = TradeUi.TextCenter("page", body.transform, 618f, 566f, 64f, 30f,
                    (page + 1) + " / " + pageCount, 14f, TradeUi.TextDim);
                TradeUi.MakeButton(">", body.transform, 682f, 566f, 44f, 30f, TradeUi.TabOff, TradeUi.Border,
                    TradeUi.TextMain, delegate { TurnPage(1); });
            }
            // 筛选标签（仅售卖 / 仅购买）：只影响本窗口的显示，重开窗口即复位
            TradeUi.MakeButton(filterSellOnly ? "仅售卖 ✓" : "仅售卖", body.transform, 14f, 566f, 104f, 30f,
                filterSellOnly ? TradeUi.TabOn : TradeUi.TabOff,
                filterSellOnly ? TradeUi.Accent : TradeUi.Border,
                filterSellOnly ? TradeUi.Accent : TradeUi.TextMain,
                delegate
                {
                    filterSellOnly = !filterSellOnly;
                    page = 0;
                    Rebuild();
                });
            TradeUi.MakeButton(filterBuyOnly ? "仅购买 ✓" : "仅购买", body.transform, 126f, 566f, 104f, 30f,
                filterBuyOnly ? TradeUi.TabOn : TradeUi.TabOff,
                filterBuyOnly ? TradeUi.Accent : TradeUi.Border,
                filterBuyOnly ? TradeUi.Accent : TradeUi.TextMain,
                delegate
                {
                    filterBuyOnly = !filterBuyOnly;
                    page = 0;
                    Rebuild();
                });
        }

        // 只重建行区（表头/筛选/整窗保持不动），页号已按当前行数钳好。
        private void BuildRows()
        {
            if (rowsRoot == null || state == null)
            {
                return;
            }
            for (int i = rowsRoot.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(rowsRoot.transform.GetChild(i).gameObject);
            }
            rowRefs.Clear();
            List<string> rows = Rows();
            pageCount = Mathf.Max(1, Mathf.CeilToInt(rows.Count / (float)LocalPageSize));
            if (page >= pageCount)
            {
                page = pageCount - 1;
            }
            if (page < 0)
            {
                page = 0;
            }
            int start = page * LocalPageSize;
            int end = Mathf.Min(rows.Count, start + LocalPageSize);
            float y = 44f;
            for (int i = start; i < end; i++)
            {
                y = AddRow(y, rows[i]);
            }
        }

        // 行清单缓存（同一轮显示只算一次；整窗重建或库存变化时由 Rebuild/EnsureFresh 作废）。
        private List<string> Rows()
        {
            if (rowCache == null)
            {
                rowCache = BuildRowList();
            }
            return rowCache;
        }

        // 翻页：只换行，不销毁整窗（原来每次翻页都整窗 Rebuild ⇒ 一帧内新旧两套 UI 并存，明显卡顿）。
        private void TurnPage(int delta)
        {
            int next = Mathf.Clamp(page + delta, 0, Mathf.Max(0, pageCount - 1));
            if (next == page)
            {
                return;
            }
            page = next;
            TradeUi.NumberBox.CommitActive();   // 翻页前落地正在输入的数量，别让换行把编辑态静默丢掉
            BuildRows();
            if (pageLabel != null)
            {
                pageLabel.text = (page + 1) + " / " + pageCount;
            }
        }

        // 行 = 货架（本站卖给你的）+ 船上真有的、本站要收的
        private List<string> BuildRowList()
        {
            List<string> rows = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < state.shelf.Count; i++)
            {
                if (seen.Add(state.shelf[i].id))
                {
                    rows.Add(state.shelf[i].id);
                }
            }
            StationCatalog c = Cat();
            Clustercraft craft = Craft();
            if (c != null && craft != null && c.accept.Count > 0)
            {
                // 本次重建复用同一份持有量快照（见 交易.md P-50）
                Dictionary<string, float> snap = HoldSnap();
                List<string> carriedIds = new List<string>();
                foreach (KeyValuePair<string, float> kv in snap)
                {
                    if (kv.Value <= 0.0001f || seen.Contains(kv.Key))
                    {
                        continue;
                    }
                    if (c.accept.ContainsKey(kv.Key))
                    {
                        carriedIds.Add(kv.Key);
                    }
                }
                for (int i = 0; i < carriedIds.Count; i++)
                {
                    if (seen.Add(carriedIds[i]))
                    {
                        rows.Add(carriedIds[i]);
                    }
                }
            }
            // 二次排序 —— 先按稀有度（传奇→极差）降序，同档内按"价格"从高到低。
            rows.Sort(delegate(string a, string b)
            {
                int ra, rb;
                float pa, pb;
                SortInfo(a, out ra, out pa);
                SortInfo(b, out rb, out pb);
                if (ra != rb)
                {
                    return rb.CompareTo(ra);
                }
                if (Mathf.Abs(pa - pb) > 0.0001f)
                {
                    return pb.CompareTo(pa);
                }
                return string.CompareOrdinal(a, b);
            });
            // 筛选（仅售卖 / 仅购买）：只改显示，不动数据；两个都勾 = 不过滤
            if (filterSellOnly != filterBuyOnly)
            {
                List<string> kept = new List<string>(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                {
                    if (PassFilter(rows[i]))
                    {
                        kept.Add(rows[i]);
                    }
                }
                rows = kept;
            }
            // 零库存分块：有库存的排前面、无库存的统一排到后面（两块各自保留上面的排序）
            List<string> stocked = new List<string>(rows.Count);
            List<string> empty = new List<string>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                if (StockOf(rows[i]) > 0.0001f)
                {
                    stocked.Add(rows[i]);
                }
                else
                {
                    empty.Add(rows[i]);
                }
            }
            stocked.AddRange(empty);
            return stocked;
        }

        // 本站当前库存（0 表示只能收购/回收）
        private float StockOf(string id)
        {
            TradeStockEntry e = state != null ? state.Find(id) : null;
            return TradeNumber.Units(e != null ? e.stock : 0f);
        }

        // 筛选判定：仅售卖 = 在收购清单且船上有货；仅购买 = 本站有库存
        private bool PassFilter(string id)
        {
            if (filterBuyOnly)
            {
                return StockOf(id) > 0.0001f;
            }
            return ValueOf(id) != null && CarriedOf(id) > 0.0001f;
        }

        // 第十九轮-5：排序键 =（稀有度, 价格）；价格取实际成交单价（买价/回收价取大）。
        private void SortInfo(string id, out int rarity, out float price)
        {
            rarity = 0;
            price = 0f;
            TradeItemValue v = ValueOf(id);
            if (v == null)
            {
                TradeStockEntry e = state.Find(id);
                v = e != null ? e.value : null;
            }
            if (v == null)
            {
                return;
            }
            rarity = (int)v.rarity;
            price = Mathf.Max(TradeRegistry.BuyUnit(def, v), TradeRegistry.SellUnit(def, v));
        }

        private float AddRow(float y, string id)
        {
            TradeItemValue v = ValueOf(id);
            TradeStockEntry e = state.Find(id);
            float buyUnit = v != null ? TradeRegistry.BuyUnit(def, v) : 1f;
            float sellUnit = v != null ? TradeRegistry.SellUnit(def, v) : 1f;
            float stock = TradeNumber.Units(e != null ? e.stock : 0f);
            // 新增「持有」列：与结算校验同一口径（仓内 + 内舱地面 + 单件物品运输仓），
            //   数值来自本轮唯一的持有量快照，逐行复用，不再每行重扫全舱
            float carried = TradeNumber.Units(CarriedOf(id));
            // 列名只叫「售价」：本站卖给你的按站方售价显示；只有回收价的（船上带来的）显示回收价
            float shown = stock > 0f ? buyUnit : sellUnit;
            TradeRarity rar = v != null ? v.rarity : TradeRarity.Common;
            return Row(y, id, TradeUtil.DisplayName(id), shown, stock, carried, rar);
        }

        private float Row(float y, string id, string label, float unit, float stock, float carried, TradeRarity rar)
        {
            float a = Amount(id);
            GameObject row = TradeUi.Node("row_" + id, rowsRoot != null ? rowsRoot.transform : body.transform,
                0f, y, 740f, 34f).gameObject;
            TradeUi.RoundPanel("bg", row.transform, 6f, 0f, 728f, 34f, 4,
                (Mathf.RoundToInt(y / 34f) & 1) == 1 ? TradeUi.RowInB : TradeUi.RowIn);

            Color iconTint;
            Sprite icon = TradeUi.ItemSprite(id, out iconTint);
            if (icon != null)
            {
                Image ic = TradeUi.Solid("icon", row.transform, 0f, 0f, 26f, 26f, Color.white);
                ic.sprite = icon;
                ic.color = iconTint;
                ic.preserveAspect = true;
                TradeUi.CenterIcon(ic, 33f, 17f, 26f);   // 吸附到图标区中心
            }
            TradeUi.Text("name", row.transform, 52f, 0f, 140f, 34f, label, 15f,
                TradeUi.RarityColor[(int)rar], false);
            TradeUi.Text("unit", row.transform, 196f, 0f, 76f, 34f, unit.ToString("0.00"), 14f,
                TradeUi.TextMain, true);
            TradeUi.Text("stock", row.transform, 280f, 0f, 68f, 34f,
                stock.ToString("0"), 13f, TradeUi.TextDim, true);
            // 持有量：火箭上可交易的总数（与结算校验同源），为 0 时灰色提示
            TradeUi.Text("hold", row.transform, 352f, 0f, 68f, 34f,
                carried.ToString("0"), 13f,
                carried > 0f ? TradeUi.Pos : TradeUi.TextOff, true);

            Color amountColor = a > 0f ? TradeUi.Pos : (a < 0f ? TradeUi.Neg : TradeUi.TextDim);
            // 回收不设上限：卖出下限只受持有量限制
            float sellFloor = -carried;
            float buyCeil = stock;
            bool atFloor = a <= sellFloor;
            bool atCeil = a >= buyCeil;

            GameObject numBox = TradeUi.Node("num", row.transform, 428f, 2f, 90f, 30f);
            TradeUi.RoundPanel("numbg", numBox.transform, 0f, 0f, 90f, 30f, 5, TradeUi.Panel).raycastTarget = true;   // 数字框要能接收点击进编辑态
            TextMeshProUGUI amountText = TradeUi.Text("amt", numBox.transform, 0f, 0f, 90f, 30f,
                a.ToString("0"), 16f, amountColor, true);
            amountText.alignment = TextAlignmentOptions.Center;
            TradeUi.NumberBox nb = numBox.AddComponent<TradeUi.NumberBox>();
            nb.label = amountText;
            nb.value = (int)a;
            nb.min = sellFloor;
            nb.max = buyCeil;
            nb.onCommit = delegate(int val) { SetAmountLive(id, val); };
            RowRef rr = new RowRef();
            rr.id = id;
            rr.amount = amountText;
            rr.floor = sellFloor;
            rr.ceil = buyCeil;
            rowRefs[id] = rr;
            if (id == pendingShakeId)
            {
                pendingShake = row;
                shakeLabel = amountText;
            }
            GameObject minus = TradeUi.MakeButton("-", row.transform, 524f, 2f, 32f, 30f,
                TradeUi.TabOff, atFloor ? TradeUi.TextOff : TradeUi.Border,
                atFloor ? TradeUi.TextOff : TradeUi.TextMain, null).gameObject;
            TradeUi.Stepper sm = minus.AddComponent<TradeUi.Stepper>();
            sm.dir = -1;
            sm.onStep = delegate(int d) { SetAmountLive(id, Mathf.Max(sellFloor, Amount(id) + d)); };
            rr.minusImg = minus.GetComponent<Image>();
            rr.minusTxt = minus.GetComponentInChildren<TextMeshProUGUI>();
            GameObject plus = TradeUi.MakeButton("+", row.transform, 560f, 2f, 32f, 30f,
                TradeUi.TabOff, atCeil ? TradeUi.TextOff : TradeUi.Border,
                atCeil ? TradeUi.TextOff : TradeUi.TextMain, null).gameObject;
            TradeUi.Stepper sp = plus.AddComponent<TradeUi.Stepper>();
            sp.dir = 1;
            sp.onStep = delegate(int d) { SetAmountLive(id, Mathf.Min(buyCeil, Amount(id) + d)); };
            rr.plusImg = plus.GetComponent<Image>();
            rr.plusTxt = plus.GetComponentInChildren<TextMeshProUGUI>();
            TradeItemValue vv = ValueOf(id);
            string hint = vv != null
                ? "回收 " + TradeRegistry.SellUnit(def, vv).ToString("0.00")
                : "";
            TradeUi.Text("hint", row.transform, 598f, 0f, 138f, 34f, hint, 11f, TradeUi.AccentDim, true);
            return y + 34f;
        }

        private void BuildSide()
        {
            side = TradeUi.Node("side", root.transform, 766f, 106f, 400f, 600f).gameObject;
            TradeUi.RoundPanel("sidebg", side.transform, 0f, 0f, 400f, 600f, 12, TradeUi.Panel);
            TradeUi.Text("t1", side.transform, 18f, 12f, 364f, 26f, "结算预览", 16f, TradeUi.Accent, false);
            TradeUi.Text("shop", side.transform, 18f, 42f, 364f, 24f,
                "本店星币   " + (state != null ? TradeNumber.Money(state.currency).ToString("0.00") : "—"), 14f, TradeUi.Gold, false);
            TradeUi.Text("cash", side.transform, 18f, 66f, 364f, 24f,
                "预期结余   " + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00"), 15f, TradeUi.TextMain, false);

            float y = 100f;
            TradeUi.Text("sh", side.transform, 18f, y, 364f, 24f, "── 出售 ──", 14f, TradeUi.Pos, false);
            y += 24f;
            for (int i = 0; i < 7; i++)
            {
                TradeUi.Text("s" + i, side.transform, 18f, y, 364f, 22f, "", 13f, TradeUi.TextDim, false);
                y += 22f;
            }
            TradeUi.Text("sg", side.transform, 18f, y, 364f, 22f, "出售合计   0.00", 14f, TradeUi.Pos, false);
            y += 26f;
            TradeUi.Text("bh", side.transform, 18f, y, 364f, 24f, "── 购买 ──", 14f, TradeUi.Neg, false);
            y += 24f;
            for (int i = 0; i < 7; i++)
            {
                TradeUi.Text("b" + i, side.transform, 18f, y, 364f, 22f, "", 13f, TradeUi.TextDim, false);
                y += 22f;
            }

            TradeUi.MakeButton("清空", side.transform, 44f, 540f, 140f, 44f, TradeUi.TabOff, TradeUi.Border,
                TradeUi.TextMain, ClearCart);
            TradeUi.MakeButton("确认交易", side.transform, 200f, 540f, 160f, 44f, TradeUi.TabOn, TradeUi.Accent,
                TradeUi.Accent, Confirm);
            RefreshSide();
        }
    }
}
