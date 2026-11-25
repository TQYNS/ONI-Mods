// 用途：远程收购页：分类标签分组与远程列表

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class TradeScreenState : KMonoBehaviour
    {
        // 远程只读窗：按预处理 Tag 分类 + 滚动
        private void BuildRemoteBody()
        {
            body = TradeUi.Node("body", root.transform, 14f, 106f, 1152f, 600f).gameObject;
            TradeUi.RoundPanel("bodybg", body.transform, 0f, 0f, 1152f, 600f, 12, TradeUi.PanelAlt);
            TradeUi.Text("note", body.transform, 18f, 8f, 900f, 26f,
                "远程查看：按本站收购分类浏览，不能交易；火箭到站后才能买卖",
                14f, TradeUi.TextDim, false);

            EnsureTagGroups();
            if (tagOrder.Count == 0)
            {
                return;
            }
            if (string.IsNullOrEmpty(remoteTag) || !tagGroups.ContainsKey(remoteTag))
            {
                remoteTag = tagOrder[0];
            }
            // 分类标签卡：**全部类别都显示**（用户反馈"看不到对应的分类"——
            float tx = 18f;
            float ty = 36f;
            for (int i = 0; i < tagOrder.Count; i++)
            {
                string tag = tagOrder[i];
                string label = TradeValuation.TagLabel(tag) + " " + tagGroups[tag].Count;
                float w = Mathf.Max(92f, TradeUi.LabelWidth(label, 15f) + 22f);
                if (tx + w > 1134f)
                {
                    tx = 18f;
                    ty += 34f;
                }
                bool on = tag == remoteTag;
                string captured = tag;
                TradeUi.MakeButton(label, body.transform, tx, ty, w, 28f,
                    on ? TradeUi.TabOn : TradeUi.TabOff,
                    on ? TradeUi.Accent : TradeUi.Border,
                    on ? TradeUi.Accent : TradeUi.TextDim,
                    delegate
                    {
                        remoteTag = captured;
                        page = 0;
                        Rebuild();
                    });
                tx += w + 8f;
            }

            float listY = ty + 40f;
            TradeUi.HLine("tagline", body.transform, 18f, listY - 8f, 1116f, TradeUi.Line);
            TradeUi.Text("ih1", body.transform, 44f, listY, 540f, 24f, "物品", 13f, TradeUi.TextDim, false);
            TradeUi.Text("ih2", body.transform, 600f, listY, 110f, 24f, "稀有度", 13f, TradeUi.TextDim, true);
            TradeUi.Text("ih3", body.transform, 730f, listY, 150f, 24f, "回收价", 13f, TradeUi.TextDim, true);
            listY += 26f;

            // 分页（用户要求"能看见换页按钮"，且"每页多显示 2~3 条"）：每页 12 条，底部固定一排翻页控件。
            List<TradeItemValue> list = tagGroups[remoteTag];
            const int pageSize = 12;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)pageSize));
            if (page >= totalPages)
            {
                page = totalPages - 1;
            }
            if (page < 0)
            {
                page = 0;
            }
            int start = page * pageSize;
            int end = Mathf.Min(list.Count, start + pageSize);
            float y = listY;
            for (int i = start; i < end; i++)
            {
                TradeItemValue v = list[i];
                GameObject row = TradeUi.Node("r" + i, body.transform, 18f, y, 1116f, 30f);
                TradeUi.RoundPanel("bg", row.transform, 0f, 0f, 1116f, 30f, 4,
                    ((i - start) & 1) == 1 ? TradeUi.RowInB : TradeUi.RowIn);
                Color iconTint;
                Sprite icon = TradeUi.ItemSprite(v.id, out iconTint);
                if (icon != null)
                {
                    Image ic = TradeUi.Solid("icon", row.transform, 0f, 0f, 24f, 24f, Color.white);
                    ic.sprite = icon;
                    ic.color = iconTint;
                    ic.preserveAspect = true;
                    TradeUi.CenterIcon(ic, 20f, 15f, 24f);   // 吸附到图标区中心
                }
                TradeUi.Text("n", row.transform, 44f, 0f, 540f, 30f, TradeUtil.DisplayName(v.id),
                    15f, TradeUi.RarityColor[(int)v.rarity], false);
                TradeUi.Text("r", row.transform, 600f, 0f, 110f, 30f, TradeDb.RarityName(v.rarity),
                    13f, TradeUi.TextDim, true);
                TradeUi.Text("p", row.transform, 730f, 0f, 150f, 30f,
                    TradeRegistry.SellUnit(def, v).ToString("0.00"), 14f, TradeUi.Accent, true);
                y += 32f;
            }

            // 翻页条（固定贴底，永远看得见）
            float by = 552f;
            TradeUi.Text("pg", body.transform, 18f, by, 700f, 32f,
                "共 " + list.Count + " 种 · 每页 " + pageSize + " 条", 13f, TradeUi.TextDim, false);
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

        // 按预处理 Tag 归类（每类一个标签卡）
        private void EnsureTagGroups()
        {
            if (tagGroupsBuilt)
            {
                return;
            }
            tagGroupsBuilt = true;
            tagGroups.Clear();
            tagOrder.Clear();
            StationCatalog c = Cat();
            if (c == null)
            {
                return;
            }
            for (int i = 0; i < c.acceptList.Count; i++)
            {
                TradeItemValue v = c.acceptList[i];
                string tag = string.IsNullOrEmpty(v.primaryTag)
                    ? "其它" : TradeValuation.TagLabel(v.primaryTag);
                List<TradeItemValue> list;
                if (!tagGroups.TryGetValue(tag, out list) || list == null)
                {
                    list = new List<TradeItemValue>();
                    tagGroups[tag] = list;
                    tagOrder.Add(tag);
                }
                list.Add(v);
            }
            for (int i = 0; i < tagOrder.Count; i++)
            {
                tagGroups[tagOrder[i]].Sort(delegate(TradeItemValue a, TradeItemValue b)
                {
                    if (a.rarity != b.rarity)
                    {
                        return b.rarity.CompareTo(a.rarity);
                    }
                    return b.price.CompareTo(a.price);
                });
            }
            tagOrder.Sort(delegate(string a, string b)
            {
                return tagGroups[b].Count.CompareTo(tagGroups[a].Count);
            });
        }
    }
}
