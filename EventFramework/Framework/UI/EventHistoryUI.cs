using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EventFramework
{
    public class EventHistoryUI : KScreen
    {
        // 窗口宽（设计像素）
        private const float WinW = 1000f;
        // 窗口高（设计像素）
        private const float WinH = 660f;

        private static readonly Color ColBg = new Color32(14, 22, 38, 247);
        private static readonly Color ColHead = new Color32(27, 42, 71, 255);
        private static readonly Color ColText = new Color32(234, 240, 248, 255);
        private static readonly Color ColDim = new Color32(147, 162, 184, 255);
        private static readonly Color ColGold = new Color32(232, 196, 106, 255);
        private static readonly Color ColCyan = new Color32(110, 211, 232, 255);
        private static readonly Color ColDanger = new Color32(232, 106, 106, 255);
        private static readonly Color ColEdge = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color ColRowIn = new Color32(24, 36, 60, 235);
        private static readonly Color ColRowInB = new Color32(18, 27, 48, 235);
        private static readonly Color ColRowHover = new Color32(40, 62, 100, 245);
        private static readonly Color ColTabOn = new Color32(58, 42, 94, 255);
        private static readonly Color ColTabOnHover = new Color32(72, 54, 116, 255);
        private static readonly Color ColTabOff = new Color32(22, 33, 54, 235);
        private static readonly Color ColTabOffHover = new Color32(34, 50, 80, 245);

        // 当前打开的实例（单例）
        private static EventHistoryUI _inst;

        // 行配色样式 A（偶数行底色）
        private static ColorStyleSetting styleRowA;
        // 行配色样式 B（奇数行底色）
        private static ColorStyleSetting styleRowB;
        // 选中的 tab 配色
        private static ColorStyleSetting styleTabOn;
        // 未选中的 tab 配色
        private static ColorStyleSetting styleTabOff;
        // 普通按钮配色
        private static ColorStyleSetting styleBtn;
        // 关闭按钮配色
        private static ColorStyleSetting styleX;

        private sealed class RowRef
        {
            // 行根节点
            internal GameObject go;
            // 行底色（KImage，圆角切片）
            internal KImage bg;
            // 标题文本
            internal LocText name;
            // 副信息文本（"第 N 周期 · 状态"）
            internal LocText meta;
            // 行内竖条 Image（关键条目金色 / 普通青色）
            internal Image bar;
            // 行尾"跳转"按钮（定位器可用时亮起，否则灰禁用）
            internal KButton jump;
            // 行悬停 tooltip（与事件通知同口径）
            internal ToolTip tip;
        }

        // 界面是否已构建（首次显示时懒构建）
        private bool _built;
        // 诊断日志只打一次的标记
        private bool _probeLogged;
        // 当前 tab：0 = 事件页 / 1 = 历史页
        private int _page;
        // 事件页当前页码（从 1 起）
        private int _pageEvent = 1;
        // 历史页当前页码（从 1 起）
        private int _pageKey = 1;
        // 「清空」二次确认起点（>0 且 3 秒内再点才真清）
        private float _clearArmedAt = -1f;

        // 列表内容父节点
        private GameObject _content;
        // 页码文本
        private LocText _pageLabel;
        // 顶部统计文本（"进行中 N 个事件"）
        private LocText _statLabel;
        // 「清空」按钮的文本（待确认时改文案）
        private LocText _clearLabel;
        // 上一页按钮
        private KButton _btnPrev;
        // 下一页按钮
        private KButton _btnNext;
        // 「清空」按钮
        private KButton _btnClear;
        // 「事件」tab 按钮
        private KButton _tabEvent;
        // 「历史」tab 按钮
        private KButton _tabKey;
        private readonly List<RowRef> _rows = new List<RowRef>();
        private readonly EventSystem.HistoryEntry[] _slotEntries =
            new EventSystem.HistoryEntry[EventSystem.PAGE_SIZE];

        public static KScreen GetOrCreate()
        {
            try
            {
                if (_inst != null && _inst.gameObject != null)
                {
                    return _inst;
                }
                GameObject go = new GameObject("MoreFood_EventHistoryUI", typeof(RectTransform));
                Transform parent = ResolveUiParent();
                if (parent != null)
                {
                    go.transform.SetParent(parent, false);
                }
                _inst = go.AddComponent<EventHistoryUI>();
                go.SetActive(false);
                Debug.Log("[MoreFood/Event][UI/History] ★ 事件历史屏已就绪（自绘窗口，取代原版报告屏克隆）");
                return _inst;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] GetOrCreate 异常: " + e);
                return null;
            }
        }

        private static Transform ResolveUiParent()
        {
            try
            {
                if (GameScreenManager.Instance != null && GameScreenManager.Instance.ssOverlayCanvas != null)
                {
                    return GameScreenManager.Instance.ssOverlayCanvas.transform;
                }
                if (WorldSelector.Instance != null && WorldSelector.Instance.transform.parent != null
                    && WorldSelector.Instance.transform.parent.parent != null)
                {
                    return WorldSelector.Instance.transform.parent.parent;
                }
                if (KScreenManager.Instance != null)
                {
                    return KScreenManager.Instance.transform;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] 取 UI 父节点失败: " + e);
            }
            return null;
        }

        // 生命周期
        protected override void OnActivate()
        {
            try
            {
                if (transform.parent == null)
                {
                    Transform parent = ResolveUiParent();
                    if (parent != null)
                    {
                        transform.SetParent(parent, false);
                    }
                }
                if (!_built)
                {
                    Build();
                    _built = true;
                }
                UiModalBlocker.Open(transform, 0.35f);
                RefreshView();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] OnActivate 异常: " + e);
            }
        }

        // 面板是挂在 ssOverlayCanvas 上的常驻层，切到星图时它不会被 KScreen 弹掉
        internal static void CloseIfOpen()
        {
            try
            {
                if (_inst == null || _inst.gameObject == null)
                {
                    return;
                }
                if (!_inst.gameObject.activeInHierarchy)
                {
                    return;   // 面板没开着，别乱撤别人的遮挡（交易窗也在用同一个 blocker）
                }
                Debug.Log("[MoreFood/Event][UI/History] 星图已打开 → 自动收起事件面板并撤掉模态遮挡");
                _inst.Deactivate();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] CloseIfOpen 异常: " + e);
            }
        }

        // 显示 / 隐藏事件历史面板（隐藏时停掉刷新计时）
        protected override void OnShow(bool show)
        {
            try
            {
                base.OnShow(show);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("history:OnShow", "[History] OnShow 失败 → 已跳过本步，流程继续", e); }
            if (!show)
            {
                // ★ 隐藏必须撤遮挡：KScreen.Show(false) 只改 active、不走 OnDeactivate，
                //   留下的遮罩会把星图六角格的射线门永久关死（2026-11-12 实测：全图 tooltip 失效）。
                UiModalBlocker.Close();
                return;
            }
            try
            {
                _pageEvent = 1;
                _pageKey = 1;
                if (_built)
                {
                    UiModalBlocker.Open(transform, 0.35f);
                    RefreshView();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] OnShow 异常: " + e);
            }
        }

        // 回调：OnDeactivate
        protected override void OnDeactivate()
        {
            UiModalBlocker.Close();
        }

        public override void Deactivate()
        {
            try
            {
                UiModalBlocker.Close();
                KScreenManager sm = KScreenManager.Instance;
                if (sm != null)
                {
                    try { sm.PopScreen(this); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("history:Deactivate", "[History] Deactivate 失败 → 已跳过本步，流程继续", e); }
                }
                if (gameObject != null)
                {
                    gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] Deactivate 异常: " + e);
            }
        }

        // 构建（米哈游风自绘）
        private void Build()
        {
            EnsureStyles();
            RectTransform rootRt = transform as RectTransform;
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            GameObject content = EventUiKit.MakeNode("Window", transform);
            EventUiKit.CenterOnScreen(content, WinW, WinH);
            _content = content;
            Transform ct = content.transform;

            Image bgImg = UiSkin.RoundSolid("bg", ct, 14, ColBg);
            EventUiKit.Stretch(bgImg.gameObject);
            Image edgeImg = UiSkin.RoundOutline("edge", ct, 14, 1, ColEdge);
            EventUiKit.Stretch(edgeImg.gameObject);

            GameObject head = EventUiKit.MakeNode("Head", ct);
            EventUiKit.Place(head, 10f, 10f, WinW - 20f, 48f);
            Image headBg = UiSkin.RoundSolid("headbg", head.transform, 10, ColHead);
            EventUiKit.Stretch(headBg.gameObject);
            UiSkin.RoundSolid("headgold", head.transform, 2, ColGold);
            EventUiKit.Place(head.transform.Find("headgold").gameObject, 14f, 10f, 4f, 28f);
            GameObject headGrad = EventUiKit.MakeGradient("headglow", head.transform,
                new Color(0.43f, 0.83f, 0.91f, 0.22f), new Color(0.43f, 0.83f, 0.91f, 0f)).gameObject;
            // 蓝线（光带）位置为手感微调值（y=40 / 高 6），后续要再调只改这一行
            EventUiKit.Place(headGrad, 24f, 40f, WinW - 60f, 6f);
            LocText title;
            GameObject titleGo = EventUiKit.MakeFixedLabel("Title", head.transform, "事件历史",
                ColText, 22f, TextAlignmentOptions.MidlineLeft, out title);
            EventUiKit.FitLabel(titleGo, title, 28f, 0f, 400f, 48f);

            float xClose = (WinW - 20f) - 46f;
            float yClose = 6f;
            KButton btnX = EventUiKit.MakeButton("BtnX", head.transform, "", OnCloseClicked, 0f);
            if (btnX != null)
            {
                EventUiKit.StripLayout(btnX.gameObject, true);
                HideTemplateExtras(btnX);
                KImage xBg = ResolveBg(btnX);
                if (xBg != null)
                {
                    xBg.sprite = UiSkin.Rounded(9);
                    xBg.type = Image.Type.Sliced;
                }
                EventUiKit.Place(btnX.gameObject, xClose, yClose, 36f, 36f);
                ApplyButtonStyle(btnX, styleX);
                AddOverlayText("XGlyph", head.transform, "×", ColText, 20f,
                    TextAlignmentOptions.Center, xClose, yClose, 36f, 36f);
            }

            GameObject headDrag = EventUiKit.MakeNode("DragZone", head.transform);
            EventUiKit.Place(headDrag, 0f, 0f, WinW - 80f, 48f);
            Image dragImg = headDrag.AddComponent<Image>();
            dragImg.color = new Color(0f, 0f, 0f, 0f);
            dragImg.raycastTarget = true;
            EventPanelDrag drag = headDrag.AddComponent<EventPanelDrag>();
            drag.target = content.GetComponent<RectTransform>();

            _tabEvent = BuildTab("TabEvent", ct, 14f, "事件", 0);
            _tabKey = BuildTab("TabKey", ct, 152f, "历史", 1);

            LocText stat;
            GameObject statGo = EventUiKit.MakeFixedLabel("Stat", ct, "", ColDim, 14f,
                TextAlignmentOptions.MidlineRight, out stat);
            EventUiKit.FitLabel(statGo, stat, WinW - 420f, 74f, 400f, 30f);
            _statLabel = stat;

            GameObject rows = EventUiKit.MakeNode("Rows", ct);
            EventUiKit.Place(rows, 14f, 112f, WinW - 28f, EventSystem.PAGE_SIZE * 44f + 9 * 6f);
            for (int i = 0; i < EventSystem.PAGE_SIZE; i++)
            {
                _rows.Add(BuildRow(rows.transform, i));
            }

            UiSkin.RoundSolid("footline", ct, 1, ColEdge);
            EventUiKit.Place(ct.Find("footline").gameObject, 14f, WinH - 52f, WinW - 28f, 1f);

            LocText prevLt;
            LocText nextLt;
            LocText refreshLt;
            _btnPrev = BuildFooterButton("BtnPrev", ct, WinW - 298f, "上一页", delegate { TurnPage(-1); }, out prevLt);
            _btnNext = BuildFooterButton("BtnNext", ct, WinW - 204f, "下一页", delegate { TurnPage(1); }, out nextLt);
            BuildFooterButton("BtnRefresh", ct, WinW - 110f, "刷新", delegate { RefreshView(); }, out refreshLt);
            // ★ 第二十四轮-1：移除「清空」后，其余按钮整体右移一格（94px）补位，右侧不再留空。

            LocText page;
            GameObject pageGo = EventUiKit.MakeFixedLabel("Page", ct, "", ColDim, 14f,
                TextAlignmentOptions.MidlineLeft, out page);
            EventUiKit.FitLabel(pageGo, page, 18f, WinH - 46f, 500f, 34f);
            _pageLabel = page;

            BuildCorner(ct, 8f, 8f, 1f, 1f);
            BuildCorner(ct, WinW - 8f, 8f, -1f, 1f);
            BuildCorner(ct, 8f, WinH - 8f, 1f, -1f);
            BuildCorner(ct, WinW - 8f, WinH - 8f, -1f, -1f);

            RectTransform crt = content.GetComponent<RectTransform>();
            RectTransform r0rt = _rows.Count > 0 && _rows[0] != null && _rows[0].go != null
                ? _rows[0].go.GetComponent<RectTransform>() : null;
            Debug.Log("[MoreFood/Event][UI/History][尺寸自检] 窗=" + (crt != null ? crt.sizeDelta.ToString() : "无")
                + " 行数=" + _rows.Count
                + " 行0尺寸=" + (r0rt != null ? r0rt.sizeDelta.ToString() : "无")
                + " 页标签=" + (_pageLabel != null)
                + " 清空按钮=" + (_btnClear != null));
            LocText probe = _rows.Count > 0 && _rows[0] != null ? _rows[0].name : null;
            Debug.Log("[MoreFood/Event][UI/History][文本自检] 行0文本='" + (probe != null ? probe.text : "无")
                + "' 可见=" + (probe != null && probe.enabled && probe.gameObject.activeInHierarchy)
                + " 字号=" + (probe != null ? probe.fontSize.ToString("0") : "-")
                + " 颜色=" + (probe != null ? probe.color.ToString() : "-")
                + " 字体=" + (probe != null && probe.font != null ? probe.font.name : "空")
                + " 标题文本='" + (title != null ? title.text : "无") + "'");
        }

        private static LocText AddOverlayText(string name, Transform parent, string text, Color color,
            float fontSize, TextAlignmentOptions align, float x, float y, float w, float h)
        {
            LocText lt;
            GameObject root = EventUiKit.MakeFixedLabel(name, parent, text, color, fontSize, align, out lt);
            EventUiKit.FitLabel(root, lt, x, y, w, h);
            if (lt != null)
            {
                lt.raycastTarget = false;   // 覆盖层必须放行点击，否则点不到底下的按钮
            }
            return lt;
        }

        private KButton BuildTab(string name, Transform parent, float x, string text, int page)
        {
            KButton btn = EventUiKit.MakeButton(name, parent, "", delegate { SwitchPage(page); }, 0f);
            if (btn == null)
            {
                return null;
            }
            EventUiKit.StripLayout(btn.gameObject, true);
            EventUiKit.Stretch(btn.gameObject);
            EventUiKit.Place(btn.gameObject, x, 70f, 130f, 34f);
            HideTemplateExtras(btn);
            KImage tabBg = ResolveBg(btn);
            if (tabBg != null)
            {
                tabBg.sprite = UiSkin.Rounded(10);
                tabBg.type = Image.Type.Sliced;
            }
            AddOverlayText(name + "Tx", parent, text, ColText, 16f,
                TextAlignmentOptions.Center, x, 70f, 130f, 34f);
            return btn;
        }

        private RowRef BuildRow(Transform parent, int idx)
        {
            RowRef r = new RowRef();
            GameObject row = EventUiKit.MakeNode("Row_" + idx, parent);
            EventUiKit.Place(row, 0f, idx * 50f, WinW - 28f, 44f);
            r.go = row;

            KButton btn = EventUiKit.MakeButton("RowBtn", row.transform, "", null, 0f);
            if (btn == null)
            {
                return r;
            }
            EventUiKit.StripLayout(btn.gameObject, true);
            EventUiKit.Stretch(btn.gameObject);
            HideTemplateExtras(btn);
            r.bg = ResolveBg(btn);
            if (r.bg != null)
            {
                r.bg.sprite = UiSkin.Rounded(9);
                r.bg.type = Image.Type.Sliced;
            }
            ApplyButtonStyle(btn, (idx & 1) == 1 ? styleRowB : styleRowA);
            int captured = idx;
            btn.onClick += delegate { OnSlotClicked(captured); };

            r.name = AddOverlayText("RowTx", row.transform, "", ColText, 16f,
                TextAlignmentOptions.MidlineLeft, 46f, 0f, (WinW - 28f) - 46f - 330f, 44f);

            Image bar = UiSkin.RoundSolid("bar", row.transform, 2, ColCyan);
            EventUiKit.Place(bar.gameObject, 14f, 10f, 5f, 24f);
            r.bar = bar;

            // ★ 行尾「跳转」按钮：样式同底部按钮；无目标时灰禁用
            KButton jump = EventUiKit.MakeButton("RowJump", row.transform, "", null, 0f);
            if (jump != null)
            {
                float jx = (WinW - 28f) - 8f - 70f;
                EventUiKit.StripLayout(jump.gameObject, true);
                EventUiKit.Stretch(jump.gameObject);
                EventUiKit.Place(jump.gameObject, jx, 9f, 70f, 26f);
                HideTemplateExtras(jump);
                KImage jbg = ResolveBg(jump);
                if (jbg != null) { jbg.sprite = UiSkin.Rounded(9); jbg.type = Image.Type.Sliced; }
                ApplyButtonStyle(jump, styleBtn);
                AddOverlayText("RowJumpTx", row.transform, "跳转", ColText, 14f,
                    TextAlignmentOptions.Center, jx, 9f, 70f, 26f);
                int cj = idx;
                jump.onClick += delegate { OnJumpClicked(cj); };
                SetInteractable(jump, false);
            }
            r.jump = jump;

            // 悬停 tooltip：与事件通知同口径（事件描述）
            try { r.tip = btn.gameObject.AddOrGet<ToolTip>(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("history:BuildRow", "[History] BuildRow 失败 → 已按兜底处理", e); r.tip = null; }

            LocText meta;
            GameObject metaGo = EventUiKit.MakeFixedLabel("Meta", row.transform, "", ColDim, 14f,
                TextAlignmentOptions.MidlineRight, out meta);
            EventUiKit.FitLabel(metaGo, meta, WinW - 400f, 0f, 288f, 44f);
            r.meta = meta;
            return r;
        }

        private KButton BuildFooterButton(string name, Transform parent, float x, string text,
            System.Action onClick, out LocText label)
        {
            label = null;
            KButton btn = EventUiKit.MakeButton(name, parent, "", onClick, 0f);
            if (btn == null)
            {
                return null;
            }
            EventUiKit.StripLayout(btn.gameObject, true);
            EventUiKit.Stretch(btn.gameObject);
            EventUiKit.Place(btn.gameObject, x, WinH - 46f, 90f, 30f);
            HideTemplateExtras(btn);
            KImage footBg = ResolveBg(btn);
            if (footBg != null)
            {
                footBg.sprite = UiSkin.Rounded(9);
                footBg.type = Image.Type.Sliced;
            }
            ApplyButtonStyle(btn, styleBtn);
            label = AddOverlayText(name + "Tx", parent, text, ColText, 15f,
                TextAlignmentOptions.Center, x, WinH - 46f, 90f, 30f);
            return btn;
        }

        private void BuildCorner(Transform parent, float x, float y, float dx, float dy)
        {
            Image h = UiSkin.RoundSolid("cornerH", parent, 1, new Color(ColGold.r, ColGold.g, ColGold.b, 0.55f));
            EventUiKit.Place(h.gameObject, dx > 0f ? x : x - 18f, dy > 0f ? y : y - 2f, 18f, 2f);
            Image v = UiSkin.RoundSolid("cornerV", parent, 1, new Color(ColGold.r, ColGold.g, ColGold.b, 0.55f));
            EventUiKit.Place(v.gameObject, dx > 0f ? x : x - 2f, dy > 0f ? y : y - 18f, 2f, 18f);
        }

        // 刷新
        private void RefreshView()
        {
            if (!_built)
            {
                return;
            }
            try
            {
                IList<EventSystem.HistoryEntry> list = (_page == 0) ? EventSystem.History : EventSystem.KeyHistory;
                int total = list != null ? list.Count : 0;
                int maxPage = Mathf.Max(1, (total + EventSystem.PAGE_SIZE - 1) / EventSystem.PAGE_SIZE);
                int page = Mathf.Clamp(_page == 0 ? _pageEvent : _pageKey, 1, maxPage);
                if (_page == 0)
                {
                    _pageEvent = page;
                }
                else
                {
                    _pageKey = page;
                }
                int from = (page - 1) * EventSystem.PAGE_SIZE;
                for (int i = 0; i < _rows.Count && i < EventSystem.PAGE_SIZE; i++)
                {
                    int src = from + i;
                    RowRef r = _rows[i];
                    if (r == null || r.go == null)
                    {
                        continue;
                    }
                    if (list == null || src >= total)
                    {
                        _slotEntries[i] = null;
                        r.go.SetActive(false);
                        continue;
                    }
                    EventSystem.HistoryEntry h = list[src];
                    _slotEntries[i] = h;
                    r.go.SetActive(true);
                    if (r.name != null)
                    {
                        string label = !string.IsNullOrEmpty(h.name) ? h.name
                            : (!string.IsNullOrEmpty(h.id) ? h.id : "?");
                        r.name.SetText(label);
                    }
                    if (r.meta != null)
                    {
                        // ★ 状态判定与上方"进行中 N 个事件"严格同源（按实例是否仍活跃，见 IsOngoing），
                        string status = EventSystem.IsOngoing(h.instanceId) ? "进行中" : "已结束";
                        r.meta.SetText("第 " + (h.cycle + 1) + " 周期 · " + status);
                    }
                    if (r.bar != null)
                    {
                        r.bar.color = h.isKey ? ColGold : ColCyan;
                    }
                    if (r.jump != null)
                    {
                        SetInteractable(r.jump, EventLocator.CanJump(h.instanceId));   // ★ 有目标才亮
                    }
                    if (r.tip != null)
                    {
                        try { r.tip.SetSimpleTooltip(BuildHistoryTip(h)); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("history:RefreshView", "[History] RefreshView 失败 → 已跳过本步，流程继续", e); }
                    }
                }
                if (_pageLabel != null)
                {
                    _pageLabel.SetText((_page == 0 ? "事件" : "历史")
                        + "    第 " + page + " / " + maxPage + " 页    共 " + total + " 条");
                }
                if (_statLabel != null)
                {
                    _statLabel.SetText("进行中 " + CountActive() + " 个事件");
                }
                SetInteractable(_btnPrev, page > 1);
                SetInteractable(_btnNext, page < maxPage);
                PaintTabs();
                if (_clearArmedAt > 0f && Time.unscaledTime - _clearArmedAt > 3f)
                {
                    _clearArmedAt = -1f;
                    if (_clearLabel != null)
                    {
                        _clearLabel.SetText("清空");
                    }
                }
                Debug.Log("[MoreFood/Event][UI/History] 刷新：页=" + (_page == 0 ? "事件" : "历史")
                    + " 第" + page + "/" + maxPage + " 共" + total + " 条");
                if (!_probeLogged && _rows.Count > 0 && _rows[0] != null && _rows[0].name != null)
                {
                    _probeLogged = true;
                    RectTransform prt = _rows[0].name.rectTransform;
                    Debug.Log("[MoreFood/Event][UI/History][文本自检2] 行0填充后文本='" + _rows[0].name.text
                        + "' 尺寸=" + (prt != null ? prt.sizeDelta.ToString() : "无")
                        + " 位置=" + (prt != null ? prt.anchoredPosition.ToString() : "无"));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] RefreshView 异常: " + e);
            }
        }

        private static int CountActive()
        {
            try
            {
                IList<EventInstance> list = EventSystem.ActiveInstances;
                int n = 0;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null && list[i].state == EventInstanceState.Active)
                        {
                            n++;
                        }
                    }
                }
                return n;
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("history:CountActive", "[History] CountActive 失败 → 已按兜底处理", e);
                return 0;
            }
        }

        private void PaintTabs()
        {
            ApplyButtonStyle(_tabEvent, _page == 0 ? styleTabOn : styleTabOff);
            ApplyButtonStyle(_tabKey, _page == 1 ? styleTabOn : styleTabOff);
        }

        private static void SetInteractable(KButton btn, bool on)
        {
            if (btn == null)
            {
                return;
            }
            try
            {
                btn.isInteractable = on;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("history:SetInteractable", "[History] SetInteractable 失败 → 已跳过本步，流程继续", e); }
        }

        private void SwitchPage(int page)
        {
            _page = page;
            RefreshView();
        }

        private void TurnPage(int delta)
        {
            if (_page == 0)
            {
                _pageEvent += delta;
            }
            else
            {
                _pageKey += delta;
            }
            RefreshView();
        }

        private void OnSlotClicked(int idx)
        {
            if (idx < 0 || idx >= _slotEntries.Length)
            {
                return;
            }
            EventSystem.HistoryEntry h = _slotEntries[idx];
            if (h == null)
            {
                return;
            }
            Debug.Log("[MoreFood/Event][UI/History] 点行位 " + idx + " → " + (h.id ?? "?"));
            Open(h);
        }

        private void Open(EventSystem.HistoryEntry h)
        {
            try
            {
                if (h == null)
                {
                    return;
                }
                // 口径：与“通知点击”一致 —— 无弹窗事件（show_panel=false / notify_only）在历史里也不弹任何面板
                EventDef d = EventRegistry.Get(h.id);
                if (d != null && !d.AllowsPanel())
                {
                    Debug.Log("[MoreFood/Event][UI/History] '" + (h.id ?? "?") + "' 无弹窗事件 → 历史点击不弹面板");
                    return;
                }

                IList<EventInstance> active = EventSystem.ActiveInstances;
                if (active != null)
                {
                    for (int i = 0; i < active.Count; i++)
                    {
                        EventInstance cur = active[i];
                        if (cur == null || cur.state == EventInstanceState.Ended)
                        {
                            continue;
                        }
                        if (cur.instanceId != h.instanceId)
                        {
                            continue;
                        }
                        EventPanel.ShowFor(cur);
                        return;
                    }
                }
                EventPanel.ShowReview(h.id, h.name, h.cycle, h.outcome, h.choiceIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] Open 异常: " + e);
            }
        }

        // ★ 行尾「跳转」按钮：先查可用性；失败（队列已空）则按钮熄灭 + 一次性提示
        private void OnJumpClicked(int idx)
        {
            if (idx < 0 || idx >= _slotEntries.Length) return;
            EventSystem.HistoryEntry h = _slotEntries[idx];
            if (h == null) return;
            if (!EventLocator.CanJump(h.instanceId))
            {
                UpdateJumpBtn(idx, false);
                return;
            }
            bool ok = EventLocator.Jump(h.instanceId);
            bool can = EventLocator.CanJump(h.instanceId);
            UpdateJumpBtn(idx, can);
            if (!ok && !can) EventLocator.NotifyNoTarget();
            Debug.Log("[MoreFood/Event][UI/History] 跳转 " + (h.id ?? "?") + " ok=" + ok + " can=" + can);
        }

        private void UpdateJumpBtn(int idx, bool can)
        {
            if (idx >= 0 && idx < _rows.Count && _rows[idx] != null && _rows[idx].jump != null)
                SetInteractable(_rows[idx].jump, can);
        }

        // 历史条目 tooltip：与事件通知同源（同一条解析链，事件结束时已定稿）
        private static string BuildHistoryTip(EventSystem.HistoryEntry h)
        {
            try
            {
                if (h == null) return "";
                string head = (!string.IsNullOrEmpty(h.name) ? h.name : h.id) + "（第 " + (h.cycle + 1) + " 周期）";
                // ★ 正文直接用通知那套渲染结果（变体 + 占位符 + 富文本）；老档为空 ⇒ 只显示标题行
                if (!string.IsNullOrEmpty(h.tip)) return head + "\n" + h.tip;
                return head;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("history:BuildHistoryTip", "[History] BuildHistoryTip 失败 → 已按兜底处理", e); return ""; }
        }

        private void OnClearClicked()
        {
            try
            {
                if (_clearArmedAt < 0f || Time.unscaledTime - _clearArmedAt > 3f)
                {
                    _clearArmedAt = Time.unscaledTime;
                    if (_clearLabel != null)
                    {
                        _clearLabel.SetText("确认清空?");
                    }
                    return;
                }
                _clearArmedAt = -1f;
                if (_clearLabel != null)
                {
                    _clearLabel.SetText("清空");
                }
                EventSystem.ClearHistory();
                RefreshView();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] OnClear 异常: " + e);
            }
        }

        private void OnCloseClicked()
        {
            try
            {
                UiModalBlocker.Close();
                if (ManagementMenu.Instance != null)
                {
                    ManagementMenu.Instance.CloseAll();
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] 关闭失败: " + e);
            }
            Deactivate();
        }

        // 样式
        private static ColorStyleSetting MakeStyle(Color normal, Color hover, Color pressed)
        {
            ColorStyleSetting s = ScriptableObject.CreateInstance<ColorStyleSetting>();
            s.inactiveColor = normal;
            s.hoverColor = hover;
            s.activeColor = pressed;
            s.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.4f);
            s.disabledActiveColor = s.disabledColor;
            s.disabledhoverColor = s.disabledColor;
            return s;
        }

        private static void EnsureStyles()
        {
            if (styleRowA != null)
            {
                return;
            }
            styleRowA = MakeStyle(ColRowIn, ColRowHover, ColRowHover);
            styleRowB = MakeStyle(ColRowInB, ColRowHover, ColRowHover);
            styleTabOn = MakeStyle(ColTabOn, ColTabOnHover, ColTabOnHover);
            styleTabOff = MakeStyle(ColTabOff, ColTabOffHover, ColTabOffHover);
            styleBtn = MakeStyle(new Color32(30, 44, 70, 245), new Color32(46, 68, 106, 250), new Color32(56, 82, 128, 255));
            styleX = MakeStyle(new Color(1f, 1f, 1f, 0.06f),
                new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.35f),
                new Color(ColDanger.r, ColDanger.g, ColDanger.b, 0.55f));
        }

        private static void HideTemplateExtras(KButton btn)
        {
            if (btn == null)
            {
                return;
            }
            try
            {
                KImage bg = ResolveBg(btn);
                Image[] imgs = btn.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < imgs.Length; i++)
                {
                    if (imgs[i] == null || imgs[i] == bg)
                    {
                        continue;
                    }
                    imgs[i].enabled = false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] 清理模板装饰失败: " + e);
            }
        }

        private static KImage ResolveBg(KButton btn)
        {
            if (btn == null)
            {
                return null;
            }
            if (btn.bgImage == null)
            {
                btn.bgImage = btn.GetComponent<KImage>();
            }
            return btn.bgImage;
        }

        private static void ApplyButtonStyle(KButton btn, ColorStyleSetting style)
        {
            if (btn == null || style == null)
            {
                return;
            }
            try
            {
                KImage bg = ResolveBg(btn);
                if (bg != null)
                {
                    bg.colorStyleSetting = style;
                    bg.ApplyColorStyleSetting();
                }
                btn.isInteractable = btn.isInteractable;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/History] 应用按钮样式失败: " + e);
            }
        }
    }

    // 星图打开时自动收起事件历史屏；注册在 FeaturePatches，这里只留方法体
    public static class ClusterMapScreen_OnShow_CloseHistoryPatch
    {
        // 补丁：原版星图 / 报告屏切换后同步事件历史面板的显隐
        public static void Postfix(bool show)
        {
            if (!show)
            {
                return;
            }
            EventHistoryUI.CloseIfOpen();
            // ★ 兜底：星图已经开了，任何残留的框架遮罩都必须撤掉，否则六角格射线被永久关死
            UiModalBlocker.Close();
        }
    }
}
