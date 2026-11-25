// 用途：捐款页：行定义、本地/远程捐款列表与确认

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class TradeScreenState : KMonoBehaviour
    {
        // 捐款窗（只有美食猎人协会）
        internal sealed class DonateRowDef
        {
            internal string id;          // 行键（同一物品在不同渠道各一行）
            internal string itemId;      // 实际物品 id（money 行为空）
            internal string channelKey;  // trade/donation.yaml 里的渠道 key
            internal string channelName;
            internal string iconId;      // 行图标：食物取 itemId，星币渠道取渠道图标（HonorPoint）
            internal string label;
            internal int points;         // 每份荣誉点
            internal float unitCost;     // 每份星币成本（食物为 0）
            internal float maxAmount;
            internal bool isFood;
        }

        // 捐款内容全部由 trade/donation.yaml 的"渠道"驱动
        private List<DonateRowDef> BuildDonateRows()
        {
            List<DonateRowDef> rows = new List<DonateRowDef>();
            List<DonationChannelDef> chs = TradeDb.DonationChannels(def != null ? def.id : null);
            if (chs == null)
            {
                return rows;
            }
            Clustercraft craft = Craft();
            Dictionary<string, float> snap = craft != null
                ? RocketInventory.Snapshot(craft)
                : new Dictionary<string, float>(StringComparer.Ordinal);
            for (int c = 0; c < chs.Count; c++)
            {
                DonationChannelDef ch = chs[c];
                if (ch == null || string.IsNullOrEmpty(ch.key))
                {
                    continue;
                }
                int left = TradeRegistry.ChannelLeft(ch);
                if (ch.kind == "money")
                {
                    float per = Mathf.Max(1f, TradeNumber.Money(ch.credit_per_point));
                    DonateRowDef d = new DonateRowDef();
                    d.id = "__money@" + ch.key;
                    d.itemId = null;
                    d.channelKey = ch.key;
                    d.channelName = ch.name;
                    d.iconId = string.IsNullOrEmpty(ch.icon) ? "HonorPoint" : ch.icon;
                    d.label = ch.name;
                    d.points = 1;
                    d.unitCost = per;
                    d.maxAmount = Mathf.Max(0f, Mathf.Min(Mathf.Floor(TradeNumber.Money(TradeRegistry.Currency) / per), left));
                    d.isFood = false;
                    rows.Add(d);
                    continue;
                }
                if (ch.kind != "food" || left <= 0)
                {
                    continue;
                }
                int perUnit = Mathf.Max(1, Mathf.RoundToInt(ch.points_per_unit));
                List<DonateRowDef> foods = new List<DonateRowDef>();
                foreach (KeyValuePair<string, float> kv in snap)
                {
                    if (kv.Value < 1f)
                    {
                        continue;
                    }
                    int q = FoodQualityOf(kv.Key);
                    if (q < 0
                        || q < ch.min_quality.GetValueOrDefault(0f)
                        || q > ch.max_quality.GetValueOrDefault(99f))
                    {
                        continue;
                    }
                    float max = Mathf.Min(TradeNumber.Units(kv.Value), Mathf.Floor(left / (float)perUnit));
                    int lim = TradeDb.DonationItemLimit(def != null ? def.id : null, ch, kv.Key);
                    if (lim > 0)
                    {
                        max = Mathf.Min(max, lim);
                    }
                    if (max < 1f)
                    {
                        continue;
                    }
                    DonateRowDef d = new DonateRowDef();
                    d.id = kv.Key + "@" + ch.key;
                    d.itemId = kv.Key;
                    d.channelKey = ch.key;
                    d.channelName = ch.name;
                    d.label = TradeUtil.DisplayName(kv.Key) + "（" + ch.name + "）";
                    d.points = perUnit;
                    d.unitCost = 0f;
                    d.maxAmount = max;
                    d.isFood = true;
                    foods.Add(d);
                }
                foods.Sort(delegate(DonateRowDef a, DonateRowDef b)
                {
                    return string.CompareOrdinal(a.itemId, b.itemId);
                });
                rows.AddRange(foods);
            }
            return rows;
        }

        private static int FoodQualityOf(string id)
        {
            try
            {
                GameObject prefab = Assets.TryGetPrefab(id);
                if (prefab == null)
                {
                    return -1;
                }
                Edible ed = prefab.GetComponent<Edible>();
                return ed == null ? -1 : ed.GetQuality();
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private void BuildDonateBody()
        {
            body = TradeUi.Node("body", root.transform, 14f, 106f, 740f, 600f).gameObject;
            TradeUi.RoundPanel("bodybg", body.transform, 0f, 0f, 740f, 600f, 12, TradeUi.PanelAlt);
            if (!string.IsNullOrEmpty(blockReason))
            {
                TradeUi.Text("blocked", body.transform, 24f, 100f, 700f, 60f, blockReason, 18f, TradeUi.Warn, false);
                return;
            }
            TradeUi.Text("h1", body.transform, 16f, 8f, 250f, 26f, "物品", 14f, TradeUi.TextDim, false);
            TradeUi.Text("h2", body.transform, 276f, 8f, 110f, 26f, "单份荣誉点", 14f, TradeUi.TextDim, true);
            TradeUi.Text("h3", body.transform, 396f, 8f, 90f, 26f, "可捐数量", 14f, TradeUi.TextDim, true);
            TradeUi.Text("h4", body.transform, 496f, 8f, 220f, 26f, "数量", 14f, TradeUi.TextDim, false);
            TradeUi.HLine("headline", body.transform, 14f, 36f, 712f, TradeUi.Line);

            List<DonateRowDef> rows = BuildDonateRows();
            RectTransform content;
            TradeUi.MakeScrollView("donateScroll", body.transform, 6f, 44f, 728f, 548f, out content);
            content.sizeDelta = new Vector2(0f, rows.Count * 36f);
            for (int i = 0; i < rows.Count; i++)
            {
                DonateRow(content, i * 36f, rows[i]);
            }
            BuildDonateSide();
        }

        private void DonateRow(Transform parent, float y, DonateRowDef d)
        {
            float a = Amount(d.id);
            GameObject row = TradeUi.Node("row_" + d.id, parent, 0f, y, 728f, 34f);
            TradeUi.RoundPanel("bg", row.transform, 6f, 0f, 716f, 34f, 4,
                (Mathf.RoundToInt(y / 36f) & 1) == 1 ? TradeUi.RowInB : TradeUi.RowIn);
            Color iconTint = Color.white;
            string iconId = !string.IsNullOrEmpty(d.iconId) ? d.iconId : d.itemId;
            Sprite icon = !string.IsNullOrEmpty(iconId) ? TradeUi.ItemSprite(iconId, out iconTint) : null;
            if (icon != null)
            {
                Image ic = TradeUi.Solid("icon", row.transform, 0f, 0f, 26f, 26f, Color.white);
                ic.sprite = icon;
                ic.color = iconTint;
                ic.preserveAspect = true;
                TradeUi.CenterIcon(ic, 33f, 17f, 26f);   // 吸附到图标区中心
            }
            TradeUi.Text("name", row.transform, 52f, 0f, 220f, 34f, d.label, 15f,
                TradeUi.RarityColor[(int)TradeRarity.Legendary], false);
            TradeUi.Text("pts", row.transform, 276f, 0f, 110f, 34f, "+" + d.points, 15f, TradeUi.Gold, true);
            TradeUi.Text("max", row.transform, 396f, 0f, 90f, 34f,
                TradeNumber.Units(d.maxAmount).ToString("0"), 13f, TradeUi.TextDim, true);

            Color amountColor = a > 0f ? TradeUi.Pos : TradeUi.TextDim;
            GameObject numBox = TradeUi.Node("num", row.transform, 496f, 2f, 84f, 30f);
            TradeUi.RoundPanel("numbg", numBox.transform, 0f, 0f, 84f, 30f, 5, TradeUi.Panel).raycastTarget = true;   // 数字框要能接收点击进编辑态
            TextMeshProUGUI amountText = TradeUi.Text("amt", numBox.transform, 0f, 0f, 84f, 30f,
                a.ToString("0"), 16f, amountColor, true);
            amountText.alignment = TextAlignmentOptions.Center;
            TradeUi.NumberBox nb = numBox.AddComponent<TradeUi.NumberBox>();
            nb.label = amountText;
            nb.value = (int)a;
            nb.min = 0f;
            nb.max = Mathf.Max(0f, d.maxAmount);
            string rid = d.id;
            nb.onCommit = delegate(int v) { SetAmountLive(rid, v); };
            RowRef rr = new RowRef();
            rr.id = d.id;
            rr.amount = amountText;
            rr.floor = 0f;
            rr.ceil = Mathf.Max(0f, d.maxAmount);
            rowRefs[d.id] = rr;

            GameObject minus = TradeUi.MakeButton("-", row.transform, 590f, 2f, 34f, 30f,
                TradeUi.TabOff, TradeUi.Border, TradeUi.TextMain, null).gameObject;
            TradeUi.Stepper sm = minus.AddComponent<TradeUi.Stepper>();
            sm.dir = -1;
            sm.onStep = delegate(int dd) { SetAmountLive(rid, Mathf.Max(0f, Amount(rid) + dd)); };
            rr.minusImg = minus.GetComponent<Image>();
            rr.minusTxt = minus.GetComponentInChildren<TextMeshProUGUI>();
            GameObject plus = TradeUi.MakeButton("+", row.transform, 628f, 2f, 34f, 30f,
                TradeUi.TabOff, TradeUi.Border, TradeUi.TextMain, null).gameObject;
            TradeUi.Stepper sp = plus.AddComponent<TradeUi.Stepper>();
            sp.dir = 1;
            sp.onStep = delegate(int dd)
            {
                SetAmountLive(rid, Mathf.Min(rr.ceil, Amount(rid) + dd));
            };
            rr.plusImg = plus.GetComponent<Image>();
            rr.plusTxt = plus.GetComponentInChildren<TextMeshProUGUI>();
            TradeUi.Text("cost", row.transform, 668f, 0f, 56f, 34f,
                d.unitCost > 0f ? ("−" + TradeNumber.Money(d.unitCost * Mathf.Max(0f, a)).ToString("0.00")) : "",
                11f, TradeUi.Neg, true);
        }

        private void BuildDonateSide()
        {
            side = TradeUi.Node("side", root.transform, 766f, 106f, 400f, 600f).gameObject;
            TradeUi.RoundPanel("sidebg", side.transform, 0f, 0f, 400f, 600f, 12, TradeUi.Panel);
            TradeUi.Text("t1", side.transform, 18f, 12f, 364f, 26f, "捐款结算", 16f, TradeUi.Accent, false);
            // 渠道额度（来自 trade/donation.yaml，静态展示；捐完重建窗口会刷新）
            float y = 44f;
            List<DonationChannelDef> chs = TradeDb.DonationChannels(def != null ? def.id : null);
            if (chs != null)
            {
                for (int i = 0; i < chs.Count; i++)
                {
                    DonationChannelDef ch = chs[i];
                    if (ch == null)
                    {
                        continue;
                    }
                    TradeUi.Text("hb" + i, side.transform, 18f, y, 364f, 22f,
                        ch.name + "   荣誉点 " + TradeRegistry.ChannelPointsOf(ch.key) + "/" + TradeRegistry.ChannelCap(ch),
                        13f, TradeRegistry.ChannelLeft(ch) > 0 ? TradeUi.TextDim : TradeUi.TextOff, false);
                    y += 22f;
                }
            }
            TradeUi.Text("cash", side.transform, 18f, y + 10f, 364f, 24f, "预期声望   0", 15f, TradeUi.TextMain, false);
            TradeUi.Text("keep", side.transform, 18f, y + 44f, 364f, 26f, "本次荣誉点   0", 16f, TradeUi.Gold, false);
            TradeUi.Text("spend", side.transform, 18f, y + 76f, 364f, 26f, "本次星币支出   0", 14f, TradeUi.Neg, false);
            TradeUi.Text("note", side.transform, 18f, y + 120f, 364f, 180f,
                "荣誉点直接转为声望。\n渠道的每份荣誉点、每类上限、单 ID 份数限制\n都在 trade/donation.yaml 里配。",
                13f, TradeUi.TextDim, false);
            TradeUi.MakeButton("清空", side.transform, 44f, 540f, 140f, 44f, TradeUi.TabOff, TradeUi.Border,
                TradeUi.TextMain, ClearCart);
            TradeUi.MakeButton("确认捐款", side.transform, 200f, 540f, 160f, 44f, TradeUi.TabOn, TradeUi.Accent,
                TradeUi.Accent, ConfirmDonate);
            RefreshSide();
        }

        private void RefreshDonateSide()
        {
            int points = 0;
            float money = 0f;
            List<DonateRowDef> rows = BuildDonateRows();
            for (int i = 0; i < rows.Count; i++)
            {
                float a = Amount(rows[i].id);
                if (a <= 0f)
                {
                    continue;
                }
                points += Mathf.RoundToInt(a) * rows[i].points;
                money = TradeNumber.Money(money + TradeNumber.Money(TradeNumber.Units(a) * rows[i].unitCost));
            }
            SetSideText("cash", "预期声望   " + (TradeRegistry.Rep + points)
                + "（" + TradeRegistry.RankName() + "）");
            SetSideText("keep", "本次荣誉点   " + points);
            SetSideText("spend", "本次星币支出   " + money.ToString("0.00"));
        }

        internal void ConfirmDonate()
        {
            TradeUi.NumberBox.CommitActive();   // 先落地正在编辑的数量
            if (!string.IsNullOrEmpty(blockReason))
            {
                Fail(null, blockReason);
                return;
            }
            Clustercraft craft = Craft();
            if (craft == null)
            {
                Fail(null, "火箭不在本站：远程不能捐款");
                return;
            }
            List<DonateRowDef> rows = BuildDonateRows();
            float money = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                float a = TradeNumber.Units(Amount(rows[i].id));
                if (a <= 0f)
                {
                    continue;
                }
                if (a > TradeNumber.Units(rows[i].maxAmount))
                {
                    Fail(rows[i].id, rows[i].label + " 超出可捐数量（上限 " + TradeNumber.Units(rows[i].maxAmount) + "）");
                    return;
                }
                money = TradeNumber.Money(money + TradeNumber.Money(TradeNumber.Units(a) * rows[i].unitCost));
            }
            if (TradeRegistry.Currency + 0.0001f < money)
            {
                Fail(null, "星币不足：本次需 " + money.ToString("0.00") + "，只有 " + TradeNumber.Money(TradeRegistry.Currency).ToString("0.00"));
                return;
            }

            int done = 0;
            int points = 0;
            float spent = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                DonateRowDef d = rows[i];
                float a = TradeNumber.Units(Amount(d.id));
                if (a <= 0f)
                {
                    continue;
                }
                int n = (int)a;
                if (d.isFood)
                {
                    float taken = TradeNumber.Units(RocketInventory.Consume(craft, new Tag(d.itemId), n));
                    if (taken < n)
                    {
                        MFLog.Warn(LogCat.Trade,"[Space] 捐款：只扣到 " + taken + "/" + n + " 份 " + d.itemId);
                    }
                    int pts = Mathf.RoundToInt(taken) * d.points;
                    points += pts;
                    TradeRegistry.AddChannelPoints(d.channelKey, pts);
                    MFLog.Info(LogCat.Trade,"[Space] ★ 捐款（" + d.channelName + "）：" + d.itemId + " ×" + Mathf.Round(taken)
                        + " → 荣誉点 +" + pts);
                }
                else
                {
                    float cost = TradeNumber.Money(n * d.unitCost);
                    spent = TradeNumber.Money(spent + cost);
                    points += n * d.points;
                    TradeRegistry.AddChannelPoints(d.channelKey, n * d.points);
                    MFLog.Info(LogCat.Trade,"[Space] ★ 捐款（" + d.channelName + "）：" + cost.ToString("0.00")
                        + " 星币 → 荣誉点 +" + (n * d.points));
                }
                done++;
            }
            if (spent > 0f)
            {
                TradeRegistry.AddCurrency(-spent);
            }
            if (points > 0)
            {
                TradeRegistry.AddRep(points);
                TradeUi.Notify("美食猎人协会", "捐款 " + points + " 荣誉点：声望 +" + points);
            }
            MFLog.Info(LogCat.Trade,"[Space] 捐款完成 " + done + " 项，荣誉点 +" + points + "，支出 " + spent.ToString("0.00")
                + "星币，声望 " + TradeRegistry.Rep + "（" + TradeRegistry.RankName() + "）");
            amounts.Clear();
            if (TradeSaveData.Attach() != null)
            {
                TradeSaveData.Attach()?.Capture();
            }
            Rebuild();
        }

        // 远程"查看捐款项"（只读）：列出本站接受的捐款 + 可捐物品
        private void BuildDonateRemoteBody()
        {
            body = TradeUi.Node("body", root.transform, 14f, 106f, 1152f, 600f).gameObject;
            TradeUi.RoundPanel("bodybg", body.transform, 0f, 0f, 1152f, 600f, 12, TradeUi.PanelAlt);
            TradeUi.Text("note", body.transform, 18f, 8f, 1100f, 26f,
                "本站接受的捐款（远程只读）：渠道规则 + 可捐物品",
                14f, TradeUi.TextDim, false);
            TradeUi.Text("ih1", body.transform, 44f, 40f, 700f, 24f, "物品 / 渠道规则", 13f, TradeUi.TextDim, false);
            TradeUi.Text("ih2", body.transform, 770f, 40f, 160f, 24f, "渠道", 13f, TradeUi.TextDim, true);
            TradeUi.Text("ih3", body.transform, 940f, 40f, 176f, 24f, "每份荣誉点", 13f, TradeUi.TextDim, true);

            // 分页（不再用滚动视图 —— 用户实测滚轮在游戏里吃不到）：
            List<DonateRemoteRow> all = BuildDonateRemoteRows();
            if (all.Count == 0)
            {
                TradeUi.Text("none", body.transform, 20f, 80f, 1000f, 30f, "本站不接受捐款", 15f, TradeUi.TextDim, false);
                return;
            }
            const int pageSize = 12;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(all.Count / (float)pageSize));
            if (page >= totalPages)
            {
                page = totalPages - 1;
            }
            if (page < 0)
            {
                page = 0;
            }
            int start = page * pageSize;
            int end = Mathf.Min(all.Count, start + pageSize);
            float y = 70f;
            for (int i = start; i < end; i++)
            {
                DonateRemoteRow r = all[i];
                GameObject row = TradeUi.Node("r" + i, body.transform, 18f, y, 1134f, 30f);
                TradeUi.RoundPanel("bg", row.transform, 0f, 0f, 1134f, 30f, 4,
                    r.isHeader ? TradeUi.TabOff : (((i - start) & 1) == 1 ? TradeUi.RowInB : TradeUi.RowIn));
                string iconId = !string.IsNullOrEmpty(r.iconId) ? r.iconId : r.itemId;
                if (!string.IsNullOrEmpty(iconId))
                {
                    Color iconTint;
                    Sprite icon = TradeUi.ItemSprite(iconId, out iconTint);
                    if (icon != null)
                    {
                        Image ic = TradeUi.Solid("icon", row.transform, 0f, 0f, 24f, 24f, Color.white);
                        ic.sprite = icon;
                        ic.color = iconTint;
                        ic.preserveAspect = true;
                        TradeUi.CenterIcon(ic, 20f, 15f, 24f);   // 吸附到图标区中心
                    }
                }
                TradeUi.Text("n", row.transform, 44f, 0f, 700f, 30f, r.label, r.isHeader ? 15f : 14f,
                    r.isHeader ? TradeUi.Gold : TradeUi.TextMain, false);
                TradeUi.Text("ch", row.transform, 770f, 0f, 160f, 30f, r.channelName, 13f, TradeUi.TextDim, true);
                TradeUi.Text("p", row.transform, 940f, 0f, 176f, 30f, r.points, 13f, TradeUi.Gold, true);
                y += 32f;
            }
            float by = 552f;
            TradeUi.Text("pg", body.transform, 18f, by, 700f, 32f,
                "共 " + all.Count + " 行 · 每页 " + pageSize + " 条", 13f, TradeUi.TextDim, false);
            TradeUi.MakeButton("<", body.transform, 900f, by, 56f, 32f, TradeUi.TabOff, TradeUi.Border,
                TradeUi.TextMain, delegate
                {
                    page = Mathf.Max(0, page - 1);
                    Rebuild();
                });
            TradeUi.TextCenter("pgn", body.transform, 956f, by, 132f, 32f,
                (page + 1) + " / " + totalPages, 15f, TradeUi.TextMain);
            TradeUi.MakeButton(">", body.transform, 1088f, by, 56f, 32f, TradeUi.TabOff, TradeUi.Border,
                TradeUi.TextMain, delegate
                {
                    page = Mathf.Min(totalPages - 1, page + 1);
                    Rebuild();
                });
        }

        private sealed class DonateRemoteRow
        {
            internal bool isHeader;
            internal string label;
            internal string iconId;
            internal string channelName;
            internal string points;
            internal string itemId;
        }

        private List<DonateRemoteRow> BuildDonateRemoteRows()
        {
            List<DonateRemoteRow> all = new List<DonateRemoteRow>();
            List<DonationChannelDef> chs = TradeDb.DonationChannels(def != null ? def.id : null);
            if (chs == null)
            {
                return all;
            }
            for (int c = 0; c < chs.Count; c++)
            {
                DonationChannelDef ch = chs[c];
                if (ch == null || string.IsNullOrEmpty(ch.key))
                {
                    continue;
                }
                List<string> cand = ch.kind == "money" ? null : DonateCandidateFoods(ch);
                string per = "+" + (ch.kind == "money" ? 1 : Mathf.Max(1, Mathf.RoundToInt(ch.points_per_unit)));
                DonateRemoteRow head = new DonateRemoteRow();
                head.isHeader = true;
                // 渠道进度直接写在标题上：传奇食材（0/100）
                head.label = ch.name + "（" + TradeRegistry.ChannelPointsOf(ch.key) + "/"
                    + TradeRegistry.ChannelCap(ch) + "）";
                head.iconId = ChannelIconOf(ch, cand);
                head.points = per + "/份";
                all.Add(head);
                if (cand == null)
                {
                    continue;   // 星币渠道没有物品行
                }
                for (int i = 0; i < cand.Count; i++)
                {
                    DonateRemoteRow r = new DonateRemoteRow();
                    r.itemId = cand[i];
                    r.label = TradeUtil.DisplayName(cand[i]);
                    r.channelName = ch.name;
                    r.points = per;
                    all.Add(r);
                }
            }
            return all;
        }

        // 渠道可捐物品：品质落在渠道区间内、且已进贸易准入；品质高的排前面（列表与图标都用它）
        private static List<string> DonateCandidateFoods(DonationChannelDef ch)
        {
            List<KeyValuePair<int, string>> tmp = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < TradeCatalog.Prepared.Count; i++)
            {
                string id = TradeCatalog.Prepared[i];
                int q = FoodQualityOf(id);
                if (q < 0
                    || q < ch.min_quality.GetValueOrDefault(0f)
                    || q > ch.max_quality.GetValueOrDefault(99f))
                {
                    continue;
                }
                tmp.Add(new KeyValuePair<int, string>(q, id));
            }
            // 品质只在这里算一次，不放进比较器里反复查 prefab
            tmp.Sort(delegate(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
            {
                return a.Key != b.Key ? b.Key - a.Key : string.CompareOrdinal(a.Value, b.Value);
            });
            List<string> cand = new List<string>(tmp.Count);
            for (int i = 0; i < tmp.Count; i++)
            {
                cand.Add(tmp[i].Value);
            }
            return cand;
        }

        // 渠道图标：yaml 的 icon 能取到真图就用它，否则回退到该渠道第一个真实物品的 ui 贴图
        private static string ChannelIconOf(DonationChannelDef ch, List<string> cand)
        {
            if (!string.IsNullOrEmpty(ch.icon))
            {
                Color probe;
                if (TradeUi.ItemSprite(ch.icon, out probe, true) != null)
                {
                    return ch.icon;
                }
            }
            return cand != null && cand.Count > 0 ? cand[0] : null;
        }
    }
}
