using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    // 卷轴阅读结果容器：阅读回调往这里加文本，窗口负责显示
    public class ScrollReadView
    {
        private readonly List<string> _lines = new List<string>();

        // 读完后是否消耗卷轴：默认消耗；接取失败时由回调置 false，
        // 让卷轴留着可重试（配方卷轴不走此标记，仍由自己的查阅补丁销毁）。
        internal bool ConsumeAfterRead = true;

        // 往阅读窗口加一行纯文本
        public void AddPlainText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            string rendered = PanelTextRenderer.Render(text, "[MoreFood/Scroll]");
            _lines.Add(string.IsNullOrEmpty(rendered) ? text : rendered);
        }

        internal string Joined
        {
            get { return string.Join("\n\n", _lines.ToArray()); }
        }

        // 把已累积的文本回放进原版信息对话框（借用其排版）
        internal void ReplayInto(InfoDialogScreen screen)
        {
            if (screen == null)
            {
                return;
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                screen.AddPlainText(_lines[i]);
            }
        }
    }

    // 卷轴阅读窗口：自绘（暗夜底 + 金饰强调 + 四角角标），零美术资源，取代原版 InfoDialogScreen
    public class ScrollReadWindow : KScreen
    {
        // 窗口宽度（设计像素）
        private const float WinW = 560f;
        // 内容内边距
        private const float PAD = 22f;
        // 标题字号
        private const float FS_TITLE = 22f;
        // 正文字号
        private const float FS_BODY = 18f;

        private static readonly Color ColBg = new Color32(14, 22, 38, 247);
        private static readonly Color ColCard = new Color32(16, 25, 43, 240);
        private static readonly Color ColHead = new Color32(27, 42, 71, 255);
        private static readonly Color ColText = new Color32(234, 240, 248, 255);
        private static readonly Color ColGold = new Color32(232, 196, 106, 255);
        private static readonly Color ColEdge = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color ColBtnText = new Color32(43, 43, 51, 255);

        // 当前已打开的阅读窗（单例：开新的先关旧的）
        private static ScrollReadWindow _current;
        // 「确定」按钮的配色样式（缓存复用）
        private static ColorStyleSetting _styleOk;

        // 本次阅读的卷轴档位（ScrollItems.TierXxx）
        private string _tier = ScrollItems.TierCommon;
        // 窗口标题文本
        private string _title;
        // 正文滚动视图
        private ScrollReadView _view;
        // 窗口根 RectTransform（开合动画靠它缩放）
        private RectTransform _winRt;
        // 面板样式（背景/边框/角标等外观参数）
        private EventPanelStyleKeeper _style;
        // 开合动画进度（0 = 收起，1 = 展开）
        private float _animT = 1f;
        // 正在关闭（防重入）
        private bool _closing;

        // 打开阅读窗口显示该档位的文本（已有窗口先关旧的）
        public static bool Show(string tier, string title, ScrollReadView view)
        {
            try
            {
                if (_current != null)
                {
                    _current.CloseWindow();
                }
                Transform parent = ResolveUiParent();
                if (parent == null)
                {
                    Debug.LogWarning("[MoreFood/Scroll][UI] 找不到 UI 父节点，回落原版弹窗");
                    return false;
                }
                GameObject go = new GameObject("MoreFood_ScrollReadWindow", typeof(RectTransform));
                go.transform.SetParent(parent, false);
                ScrollReadWindow win = go.AddComponent<ScrollReadWindow>();
                win._tier = string.IsNullOrEmpty(tier) ? ScrollItems.TierCommon : tier;
                win._title = string.IsNullOrEmpty(title) ? ScrollItems.DisplayName(win._tier) : title;
                win._view = view != null ? view : new ScrollReadView();
                win.Activate();
                _current = win;
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll][UI] 自定义窗口创建失败，回落原版弹窗");
                return false;
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
                if (KScreenManager.Instance != null)
                {
                    return KScreenManager.Instance.transform;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll][UI] 取 UI 父节点失败");
            }
            return null;
        }

        // 回调：OnActivate
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
                Build();
                UiModalBlocker.Open(transform, 0.45f);
                _animT = 0f;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll][UI] OnActivate 异常");
            }
        }

        // 回调：OnDeactivate
        protected override void OnDeactivate()
        {
            UiModalBlocker.Close();
        }

        // Unity 回调：清理
        protected override void OnCleanUp()
        {
            try
            {
                if (_current == this)
                {
                    UiModalBlocker.Close();
                    _current = null;
                }
            }
            catch { /* expected: 清理时关闭模态遮罩失败，窗口仍照常销毁、遮罩状态由下次开启重置 */ }
            base.OnCleanUp();
        }

        // 每帧更新阅读窗口的开门 / 收起动画
        public override void ScreenUpdate(bool topLevel)
        {
            if (_animT >= 1f || _winRt == null)
            {
                return;
            }
            try
            {
                _animT = Mathf.Min(1f, _animT + Time.unscaledDeltaTime / 0.16f);
                float k = 1f - (1f - _animT) * (1f - _animT);
                float s = Mathf.Lerp(0.965f, 1f, k);
                _winRt.localScale = new Vector3(s, s, 1f);
                _winRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(-14f, 0f, k));
            }
            catch
            {
                _animT = 1f;
            }
        }

        public void CloseWindow()
        {
            if (_closing)
            {
                return;
            }
            _closing = true;
            try
            {
                UiModalBlocker.Close();
                KScreenManager sm = KScreenManager.Instance;
                if (sm != null)
                {
                    try { sm.PopScreen(this); } catch { /* expected: 屏幕栈弹出失败时继续走销毁流程，窗口对象照常清理 */ }
                }
                if (_current == this)
                {
                    _current = null;
                }
                if (gameObject != null)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll][UI] 关闭失败");
            }
        }

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
            _winRt = content.GetComponent<RectTransform>();
            Transform ct = content.transform;

            Image bgImg = UiSkin.RoundSolid("bg", ct, 14, ColBg);
            EventUiKit.Stretch(bgImg.gameObject);
            Image edgeImg = UiSkin.RoundOutline("edge", ct, 14, 1, ColEdge);
            EventUiKit.Stretch(edgeImg.gameObject);

            GameObject head = EventUiKit.MakeNode("Head", ct);
            EventUiKit.Place(head, 10f, 10f, WinW - 20f, 46f);
            Image headBg = UiSkin.RoundSolid("headbg", head.transform, 10, ColHead);
            EventUiKit.Stretch(headBg.gameObject);
            Image headGold = UiSkin.RoundSolid("headgold", head.transform, 2, ColGold);
            EventUiKit.Place(headGold.gameObject, 14f, 9f, 4f, 28f);
            LocText titleLt;
            GameObject titleGo = EventUiKit.MakeFixedLabel("Title", head.transform, _title,
                ColText, FS_TITLE, TextAlignmentOptions.MidlineLeft, out titleLt);
            EventUiKit.FitLabel(titleGo, titleLt, 30f, 0f, WinW - 10f - 30f - 20f, 46f);
            AddStyle(titleLt, ColText, FS_TITLE, TextAlignmentOptions.MidlineLeft);

            float y = 68f;
            string bodyText = _view != null ? _view.Joined : "";
            if (string.IsNullOrEmpty(bodyText))
            {
                bodyText = "（没有可显示的内容）";
            }
            float cardW = WinW - 2f * PAD;
            float textW = cardW - 42f;
            LocText bodyLt;
            GameObject bodyGo = EventUiKit.MakeFixedLabel("Body", ct, bodyText,
                ColText, FS_BODY, TextAlignmentOptions.TopLeft, out bodyLt);
            try
            {
                if (bodyLt != null)
                {
                    bodyLt.lineSpacing = 5f;
                }
            }
            catch { /* expected: 行距设置失败时沿用默认行距，正文排版与高度测量照常进行 */ }
            float textH = EventUiKit.MeasureHeight(bodyLt, textW, 26f);
            float cardH = textH + 36f;
            Image card = UiSkin.RoundSolid("BodyCard", ct, 10, ColCard);
            EventUiKit.Place(card.gameObject, PAD, y, cardW, cardH);
            EventUiKit.FitLabel(bodyGo, bodyLt, PAD + 22f, y + 18f, textW, cardH - 36f);
            bodyGo.transform.SetAsLastSibling();   // 文本先建（要先量高），置顶避免被后建的卡片盖住
            AddStyle(bodyLt, ColText, FS_BODY, TextAlignmentOptions.TopLeft);

            y += cardH + 16f;

            float btnW = 140f;
            float btnH = 38f;
            float btnX = WinW - PAD - btnW;
            KButton ok = EventUiKit.MakeButton("BtnOk", ct, "", OnOkClicked, 0f);
            if (ok != null)
            {
                EventUiKit.StripLayout(ok.gameObject, true);
                EventUiKit.Stretch(ok.gameObject);
                EventUiKit.Place(ok.gameObject, btnX, y, btnW, btnH);
                HideTemplateExtras(ok);
                KImage okBg = ResolveBg(ok);
                if (okBg != null)
                {
                    okBg.sprite = UiSkin.Rounded(10);
                    okBg.type = Image.Type.Sliced;
                }
                ApplyButtonStyle(ok, _styleOk);
                LocText okTx = AddOverlayText("BtnOkTx", ct, "确认", ColBtnText, 17f,
                    TextAlignmentOptions.Center, btnX, y, btnW, btnH);
                AddStyle(okTx, ColBtnText, 17f, TextAlignmentOptions.Center);
            }
            y += btnH + 20f;

            float winH = y;
            EventUiKit.CenterOnScreen(content, WinW, winH);
            BuildCorner(ct, 9f, 9f, 1f, 1f);
            BuildCorner(ct, WinW - 9f, 9f, -1f, 1f);
            BuildCorner(ct, 9f, winH - 9f, 1f, -1f);
            BuildCorner(ct, WinW - 9f, winH - 9f, -1f, -1f);

            _winRt.localScale = new Vector3(0.965f, 0.965f, 1f);
            _winRt.anchoredPosition = new Vector2(0f, -14f);

            MFLog.Info(LogCat.UI, "[MoreFood/Scroll][UI] 阅读窗口已建 " + ScrollItems.PrefabId(_tier)
                + " 尺寸=" + WinW.ToString("0") + "×" + winH.ToString("0"));
        }

        private void OnOkClicked()
        {
            CloseWindow();
        }

        private void AddStyle(LocText lt, Color color, float size, TextAlignmentOptions align)
        {
            if (lt == null)
            {
                return;
            }
            if (_style == null)
            {
                _style = gameObject.AddComponent<EventPanelStyleKeeper>();
            }
            _style.Add(lt, color, size, align);
        }

        private static LocText AddOverlayText(string name, Transform parent, string text, Color color,
            float fontSize, TextAlignmentOptions align, float x, float y, float w, float h)
        {
            LocText lt;
            GameObject root = EventUiKit.MakeFixedLabel(name, parent, text, color, fontSize, align, out lt);
            EventUiKit.FitLabel(root, lt, x, y, w, h);
            if (lt != null)
            {
                lt.raycastTarget = false;
            }
            return lt;
        }

        private void BuildCorner(Transform parent, float x, float y, float dx, float dy)
        {
            Color c = new Color(ColGold.r, ColGold.g, ColGold.b, 0.45f);
            Image h = UiSkin.RoundSolid("cornerH", parent, 1, c);
            EventUiKit.Place(h.gameObject, dx > 0f ? x : x - 18f, dy > 0f ? y : y - 2f, 18f, 2f);
            Image v = UiSkin.RoundSolid("cornerV", parent, 1, c);
            EventUiKit.Place(v.gameObject, dx > 0f ? x : x - 2f, dy > 0f ? y : y - 18f, 2f, 18f);
        }

        // 样式（按钮改皮管线与 EventHistoryUI 同款）
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
            if (_styleOk != null)
            {
                return;
            }
            _styleOk = MakeStyle(new Color32(226, 193, 122, 255),
                new Color32(242, 214, 150, 255), new Color32(206, 172, 102, 255));
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
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll][UI] 清理模板装饰失败");
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
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll][UI] 应用按钮样式失败");
            }
        }
    }
}
