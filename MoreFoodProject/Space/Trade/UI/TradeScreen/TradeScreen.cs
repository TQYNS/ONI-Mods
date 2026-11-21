// 用途：交易窗装配：本地/远程入口、站栏隐藏与模态遮罩

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class TradeScreen
    {
        /// <summary>★ 第十九轮-4：给遮罩挂"点击非交互区关闭"（先关内部弹窗，再关交易/远程窗口）。</summary>
        internal static void AttachModalCloser(GameObject blocker, TradeScreenState st)
        {
            if (blocker == null || st == null)
            {
                return;
            }
            TradeModalCloser closer = blocker.AddComponent<TradeModalCloser>();
            closer.state = st;
        }

        internal static void Build(SpaceStation station, ref GameObject window, bool donate)
        {
            Transform parent = TradeUi.OverlayParent();
            if (parent == null)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 找不到 overlay 父节点，贸易窗口无法打开");
                return;
            }
            StationDef def = station.Def;
            if (def == null)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 站点 " + station.Key + " 没有配套配置（trade/station/*.yaml），用兜底参数");
                def = new StationDef();
                def.id = station.Key;
                def.name = station.StationName;
                def.k = 0.2f;
            }
            TradeStationState state = TradeRegistry.EnsureState(station.Key);
            // ★ 全屏遮罩（dim 0.35）—— 点"窗口外的非交互区"关闭（先关内部弹窗，再关本窗口）
            GameObject blocker = UiModalBlocker.Open(parent, 0.35f);
            window = TradeUi.Node("SpaceTradeWindow", parent, 0f, 0f, 10f, 10f);
            RectTransform rt = window.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(1180f, 720f);

            Image winBg = TradeUi.RoundPanel("root", window.transform, 0f, 0f, 1180f, 720f, 14, TradeUi.BgRoot);
            winBg.raycastTarget = true;   // ★ 第十九轮-4：窗口本体吸收点击 → 只有点"窗口外"才触发遮罩关闭
            TradeUi.RoundPanel("headbg", window.transform, 0f, 0f, 1180f, 58f, 12, TradeUi.Panel);
            TradeUi.HLine("headline", window.transform, 0f, 56f, 1180f, TradeUi.Gold);
            Image headHit = TradeUi.Solid("headdrag", window.transform, 0f, 0f, 1180f, 30f,
                new Color(0f, 0f, 0f, 0f));
            headHit.raycastTarget = true;
            EventPanelDrag drag = headHit.gameObject.AddComponent<EventPanelDrag>();
            drag.target = window.GetComponent<RectTransform>();
            TradeUi.RoundPanel("statusbg", window.transform, 14f, 66f, 1152f, 32f, 8, TradeUi.RowInB);

            // ★ 修复4：不再隐藏交易站底部按钮排。

            TradeScreenState st = window.AddComponent<TradeScreenState>();
            st.station = station;
            st.def = def;
            st.state = state;
            st.root = window;
            st.remote = false;
            st.donate = donate;
            AttachModalCloser(blocker, st);   // ★ 第十九轮-4：遮罩点击 → 关弹窗/关窗口
            st.title = TradeUi.Text("title", window.transform, 26f, 12f, 620f, 36f,
                def.name + (donate ? "  ·  捐款" : ""), 22f, TradeUi.TextMain, false);
            st.wallet = TradeUi.Text("wallet", window.transform, 660f, 12f, 420f, 36f,
                "星币 " + TradeRegistry.Currency.ToString("0.00"), 20f, TradeUi.Gold, true);
            st.status = TradeUi.Text("status", window.transform, 28f, 69f, 1120f, 26f, "", 15f,
                TradeUi.TextDim, false);
            TradeUi.MakeCloseButton(window.transform, 1122f, 11f, TradeUi.CloseWindow);
            st.EnsureFresh();
            if (def.IsPirate && TradeRegistry.PirateClosed(state))
            {
                st.blockReason = "站点已关闭：约 " + Mathf.CeilToInt(TradeRegistry.PirateCloseLeft(state))
                    + " 秒后重开";
            }
            st.Rebuild();
            MFLog.Info(LogCat.Trade,"[Space] 打开贸易窗口：站点=" + def.id + " 条目=" + state.shelf.Count
                + " 本店星币=" + state.currency.ToString("0"));
        }

        internal static void BuildRemote(SpaceStation station, ref GameObject remote, bool donate)
        {
            Transform parent = TradeUi.OverlayParent();
            if (parent == null)
            {
                return;
            }
            StationDef def = station.Def;
            if (def == null)
            {
                def = new StationDef();
                def.id = station.Key;
                def.name = station.StationName;
            }
            TradeStationState state = TradeRegistry.EnsureState(station.Key);
            TradeRegistry.RefreshIfDue(state, def);
            // ★ 远程窗同样加遮罩（点窗口外关闭）
            GameObject blocker = UiModalBlocker.Open(parent, 0.35f);
            remote = TradeUi.Node("SpaceTradeRemote", parent, 0f, 0f, 10f, 10f);
            RectTransform rt = remote.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(1180f, 720f);
            Image remoteBg = TradeUi.RoundPanel("root", remote.transform, 0f, 0f, 1180f, 720f, 14, TradeUi.BgRoot);
            remoteBg.raycastTarget = true;   // ★ 第十九轮-4：远程窗口本体吸收点击
            TradeUi.RoundPanel("headbg", remote.transform, 0f, 0f, 1180f, 58f, 12, TradeUi.Panel);
            TradeUi.HLine("headline", remote.transform, 0f, 56f, 1180f, TradeUi.Gold);
            Image headHit = TradeUi.Solid("headdrag", remote.transform, 0f, 0f, 1180f, 30f,
                new Color(0f, 0f, 0f, 0f));
            headHit.raycastTarget = true;
            EventPanelDrag remoteDrag = headHit.gameObject.AddComponent<EventPanelDrag>();
            remoteDrag.target = remote.GetComponent<RectTransform>();
            TradeUi.RoundPanel("statusbg", remote.transform, 14f, 66f, 1152f, 32f, 8, TradeUi.RowInB);
            TradeUi.Text("title", remote.transform, 26f, 12f, 1000f, 36f,
                def.name + (donate ? " · 捐款清单（远程只读）" : " · 收购分类（远程只读）"),
                22f, TradeUi.TextMain, false);
            TradeUi.Text("status", remote.transform, 28f, 69f, 1120f, 26f,
                donate ? "这里能看到本站接受哪些捐款；要真捐请把火箭开过来"
                    : "远程查看不计冷却、不能交易；火箭到站后可买卖", 15f, TradeUi.Warn, false);
            TradeUi.MakeCloseButton(remote.transform, 1122f, 11f, TradeUi.CloseRemote);
            TradeScreenState st = remote.AddComponent<TradeScreenState>();
            st.station = station;
            st.def = def;
            st.state = state;
            st.root = remote;
            st.remote = true;
            st.donate = donate;
            AttachModalCloser(blocker, st);   // ★ 第十九轮-4：遮罩点击 → 关弹窗/关远程窗
            st.Rebuild();
        }
    }

    internal static class TradeStationBarAnchor
    {
        internal static void KeepBarHidden()
        {
            TradeUi.HideBar();
        }
    }

    /// <summary>★ 第十九轮-4：交易遮罩点击处理 —— 优先关内部弹窗，其次关交易/远程窗口。</summary>
    internal sealed class TradeModalCloser : UnityEngine.MonoBehaviour,
        UnityEngine.EventSystems.IPointerClickHandler
    {
        internal TradeScreenState state;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (state != null)
            {
                state.HandleBlockerClick();
            }
        }
    }
}
