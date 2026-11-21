// 用途：Morefood 高级设置弹窗（主/子星图抽取器），沿用事件框架配色，点空白或「返回」关闭

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class AdvancedSettingsPanel
    {
        private const float WinW = 720f;
        private const float WinH = 580f;

        private static readonly Color ColBg      = new Color(0.251f, 0.271f, 0.349f, 0.99f);
        private static readonly Color ColHead    = new Color(0.498f, 0.239f, 0.369f, 1f);
        private static readonly Color ColText    = new Color(0.937f, 0.945f, 0.960f, 1f);
        private static readonly Color ColDim     = new Color(0.655f, 0.678f, 0.733f, 1f);
        private static readonly Color ColBody    = new Color(0.804f, 0.827f, 0.867f, 1f);
        private static readonly Color ColCyan    = new Color(0.431f, 0.827f, 0.909f, 1f);
        private static readonly Color ColDivider = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color ColTrack   = new Color(1f, 1f, 1f, 0.14f);

        private const float LabelW = 110f;
        private const float SliderX = 140f;
        private const float SliderW = 330f;
        private const float ReadoutX = 486f;
        private const float ReadoutW = 180f;
        private const float RowH = 28f;
        private const float RowStep = 40f;

        private static GameObject _root;
        private static readonly List<AdvancedSlider> _sliders = new List<AdvancedSlider>();
        private static TMP_FontAsset _font;
        private static bool _fontScanned;

        internal static void Open(Transform host)
        {
            if (host == null || _root != null) return;
            try
            {
                Build(host);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Advanced] 打开高级设置失败");
                Close();
            }
        }

        internal static void Close()
        {
            _sliders.Clear();
            if (_root == null) return;
            try { UnityEngine.Object.Destroy(_root); }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, "[MoreFood/Advanced] 关闭高级设置失败"); }
            _root = null;
        }

        private static void Build(Transform host)
        {
            if (_font == null) _fontScanned = false;   // 上一屏可能还没生成文本，允许重扫
            _root = EventUiKit.MakeNode("MoreFood_AdvancedSettings", host);
            EventUiKit.Stretch(_root);

            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;
            AdvancedBackdropClose closer = _root.AddComponent<AdvancedBackdropClose>();

            GameObject win = EventUiKit.MakeNode("Window", _root.transform);
            closer.window = (RectTransform)win.transform;
            EventUiKit.CenterOnScreen(win, WinW, WinH);
            FitToScreen(win);

            Image bg = UiSkin.RoundSolid("bg", win.transform, 14, ColBg);
            EventUiKit.Stretch(bg.gameObject);
            bg.raycastTarget = true;   // 窗口内空白不触发底层遮罩的关闭

            Image edge = UiSkin.RoundOutline("edge", win.transform, 14, 1, ColDivider);
            EventUiKit.Stretch(edge.gameObject);

            Image headBar = UiSkin.RoundSolid("headbar", win.transform, 12, ColHead);
            EventUiKit.Place(headBar.gameObject, 4f, 4f, WinW - 8f, 60f);

            Label(win.transform, "Title", "高级设置", ColText, 26f,
                TextAlignmentOptions.MidlineLeft, 24f, 6f, 400f, 56f);
            Image headLine = UiSkin.RoundSolid("headline", win.transform, 1, ColDivider);
            EventUiKit.Place(headLine.gameObject, 24f, 72f, WinW - 48f, 1f);

            float y = 90f;
            y = Section(win.transform, y, "主星图");
            y = AddSlider(win.transform, y, "刷新间隔", 60f, 1200f, 10f, 0, " 秒",
                delegate { return MainInterval(); },
                delegate (float v) { AdvancedGameplaySettings.MainIntervalSeconds = v; },
                "抽取器多久尝试刷一次怪；数值越大越稀疏。");
            y = AddSlider(win.transform, y, "刷新数量", 1f, 10f, 1f, 0, " 只",
                delegate { return MainCount(); },
                delegate (float v) { AdvancedGameplaySettings.MainSpawnCount = Mathf.RoundToInt(v); },
                "每次成功刷新时出怪的数量；数值越大越多。");
            y = AddSlider(win.transform, y, "在场上限", 1f, 40f, 1f, 0, " 只",
                delegate { return MainMax(); },
                delegate (float v) { AdvancedGameplaySettings.MainMaxMonsters = Mathf.RoundToInt(v); },
                "同一区域同时存在的怪物上限；数值越大越拥挤。");

            y += 6f;
            y = Section(win.transform, y, "子星图");
            y = AddSlider(win.transform, y, "刷新间隔", 60f, 1200f, 10f, 0, " 秒",
                delegate { return SubInterval(); },
                delegate (float v) { AdvancedGameplaySettings.SubIntervalSeconds = v; },
                "抽取器多久尝试刷一次怪；数值越大越稀疏。");
            y = AddSlider(win.transform, y, "刷新数量", 1f, 10f, 1f, 0, " 只",
                delegate { return SubCount(); },
                delegate (float v) { AdvancedGameplaySettings.SubSpawnCount = Mathf.RoundToInt(v); },
                "每次成功刷新时出怪的数量；数值越大越多。");
            y = AddSlider(win.transform, y, "在场上限", 1f, 40f, 1f, 0, " 只",
                delegate { return SubMax(); },
                delegate (float v) { AdvancedGameplaySettings.SubMaxMonsters = Mathf.RoundToInt(v); },
                "同一区域同时存在的怪物上限；数值越大越拥挤。");

            Footer(win.transform);
            for (int i = 0; i < _sliders.Count; i++) _sliders[i].Refresh();
        }

        // ---- 取值：未设置时显示内置默认，拖动后写回本局状态 ----

        private static float MainInterval()
        {
            return AdvancedGameplaySettings.MainIntervalSeconds > 0f
                ? AdvancedGameplaySettings.MainIntervalSeconds
                : AdvancedGameplaySettings.MainIntervalDefault;
        }

        private static float MainCount()
        {
            return AdvancedGameplaySettings.MainSpawnCount > 0
                ? AdvancedGameplaySettings.MainSpawnCount
                : AdvancedGameplaySettings.MainSpawnCountDefault;
        }

        private static float MainMax()
        {
            return AdvancedGameplaySettings.MainMaxMonsters > 0
                ? AdvancedGameplaySettings.MainMaxMonsters
                : AdvancedGameplaySettings.MainMaxMonstersDefault;
        }

        private static float SubInterval()
        {
            return AdvancedGameplaySettings.SubIntervalSeconds > 0f
                ? AdvancedGameplaySettings.SubIntervalSeconds
                : AdvancedGameplaySettings.SubIntervalDefault;
        }

        private static float SubCount()
        {
            return AdvancedGameplaySettings.SubSpawnCount > 0
                ? AdvancedGameplaySettings.SubSpawnCount
                : AdvancedGameplaySettings.SubSpawnCountDefault;
        }

        private static float SubMax()
        {
            return AdvancedGameplaySettings.SubMaxMonsters > 0
                ? AdvancedGameplaySettings.SubMaxMonsters
                : AdvancedGameplaySettings.SubMaxMonstersDefault;
        }

        private static float Section(Transform parent, float y, string title)
        {
            Label(parent, "Sec_" + title, title, ColBody, 17f,
                TextAlignmentOptions.MidlineLeft, 24f, y, 300f, 26f);
            Image line = UiSkin.RoundSolid("SecLine_" + title, parent, 1, ColDivider);
            EventUiKit.Place(line.gameObject, 24f, y + 27f, WinW - 48f, 1f);
            return y + 34f;
        }

        private static float AddSlider(Transform parent, float y, string label,
            float min, float max, float step, int decimals, string suffix,
            Func<float> getter, Action<float> setter, string tip)
        {
            TextMeshProUGUI labelText = Label(parent, "K_" + label, label, ColDim, 16f,
                TextAlignmentOptions.MidlineLeft, 28f, y, LabelW, RowH);

            GameObject hit = EventUiKit.MakeNode("S_" + label, parent);
            EventUiKit.Place(hit, SliderX, y, SliderW, RowH);

            var hitImg = hit.AddComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;

            Image track = UiSkin.RoundSolid("track", hit.transform, 3, ColTrack);
            EventUiKit.Place(track.gameObject, 0f, 11f, SliderW, 6f);

            Image fill = UiSkin.RoundSolid("fill", hit.transform, 3, ColCyan);
            EventUiKit.Place(fill.gameObject, 0f, 11f, 1f, 6f);

            Image knob = UiSkin.RoundSolid("knob", hit.transform, 5, ColText);
            EventUiKit.Place(knob.gameObject, 0f, 6f, 14f, 14f);

            var readout = Label(parent, "V_" + label, "", ColText, 16f,
                TextAlignmentOptions.MidlineRight, ReadoutX, y, ReadoutW, RowH);

            var s = hit.AddComponent<AdvancedSlider>();
            s.hit = hit.transform as RectTransform;
            s.fill = fill.rectTransform;
            s.knob = knob.rectTransform;
            s.min = min;
            s.max = max;
            s.step = step;
            s.decimals = decimals;
            s.suffix = suffix;
            s.readout = readout;
            s.getter = getter;
            s.onValue = delegate (float v)
            {
                if (setter != null) setter(v);
            };
            _sliders.Add(s);

            // 原版悬停提示：只有选项文字挂 ONI ToolTip；滑块轨道/把手/数值都不响应
            AdvancedToolTip.Attach(labelText.gameObject, tip);

            return y + RowStep;
        }

        private static void Footer(Transform parent)
        {
            Image line = UiSkin.RoundSolid("footline", parent, 1, ColDivider);
            EventUiKit.Place(line.gameObject, 24f, WinH - 70f, WinW - 48f, 1f);

            Label(parent, "FootHint", "点空白处或「返回」关闭。", ColDim, 15f,
                TextAlignmentOptions.MidlineLeft, 28f, WinH - 62f, 420f, 44f);

            GameObject go = EventUiKit.MakeNode("BtnBack", parent);
            EventUiKit.Place(go, WinW - 24f - 130f, WinH - 64f, 130f, 44f);

            Image bg = go.AddComponent<Image>();
            bg.sprite = UiSkin.Rounded(11);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.196f, 0.310f, 0.400f, 1f);
            bg.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.92f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(delegate { Close(); });

            Label(go.transform, "Label", "返 回", ColText, 19f,
                TextAlignmentOptions.Center, 0f, 0f, 130f, 44f);
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text,
            Color color, float size, TextAlignmentOptions align,
            float x, float y, float w, float h)
        {
            return PutText(name, parent, text, size, color, align, x, y, w, h);
        }

        // 前端无 Assets.UIPrefabs 保证，按框架做法直接建 TMP 文本，字体取自屏上现成 LocText
        internal static TextMeshProUGUI PutText(string name, Transform parent, string text,
            float size, Color color, TextAlignmentOptions align,
            float x, float y, float w, float h)
        {
            GameObject go = EventUiKit.MakeNode(name, parent);
            EventUiKit.Place(go, x, y, w, h);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = Font();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        internal static TMP_FontAsset Font()
        {
            if (_font != null) return _font;
            if (_fontScanned) return _font;
            _fontScanned = true;
            try
            {
                var all = UnityEngine.Object.FindObjectsByType<LocText>(FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].font != null)
                    {
                        _font = all[i].font;
                        break;
                    }
                }
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, "[MoreFood/Advanced] 查找字体失败"); }
            return _font;
        }

        private static void FitToScreen(GameObject win)
        {
            try
            {
                if (_root == null) return;
                var rt = _root.GetComponent<RectTransform>();
                if (rt == null) return;
                float availW = rt.rect.width;
                float availH = rt.rect.height;
                if (availW < 120f || availH < 120f) return;
                float k = Mathf.Min(1f, Mathf.Min(availW / (WinW + 30f), availH / (WinH + 30f)));
                if (k < 0.999f) win.transform.localScale = new Vector3(k, k, 1f);
            }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, "[MoreFood/Advanced] 自适应缩放失败"); }
        }
    }

    // 只有点击落在窗口矩形之外才关闭。
    // 注意：Unity 在命中点找不到 IPointerDownHandler 时，会把 click 目标回退到父链上的
    // IPointerClickHandler（本组件挂在全屏根节点），因此只靠 bg 的 raycastTarget 挡不住，
    // 必须用窗口矩形判定，否则窗口内任意空白点击都会误关。
    public sealed class AdvancedBackdropClose : MonoBehaviour, IPointerClickHandler
    {
        internal RectTransform window;

        public void OnPointerClick(PointerEventData e)
        {
            if (window != null &&
                RectTransformUtility.RectangleContainsScreenPoint(window, e.position, e.pressEventCamera))
                return;
            AdvancedSettingsPanel.Close();
        }
    }

    // 原版悬停提示：挂 ONI 的 ToolTip，悬停时在光标旁浮层显示
    internal static class AdvancedToolTip
    {
        internal static void Attach(GameObject go, string message)
        {
            if (go == null || string.IsNullOrEmpty(message)) return;
            ToolTip tip = go.GetComponent<ToolTip>();
            if (tip == null) tip = go.AddComponent<ToolTip>();
            tip.UseFixedStringKey = false;
            tip.enabled = true;
            tip.tooltipPivot = new Vector2(0.5f, 1f);
            tip.tooltipPositionOffset = new Vector2(0f, -20f);
            tip.parentPositionAnchor = new Vector2(0.5f, 0.5f);
            tip.SetSimpleTooltip(message);
            var g = go.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
        }
    }

    // 手绘滑块：透明命中区收点击/拖动，换算成 0~1 再吸附步长
    public sealed class AdvancedSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        internal RectTransform hit;
        internal RectTransform fill;
        internal RectTransform knob;
        internal TextMeshProUGUI readout;
        internal float min;
        internal float max;
        internal float step = 1f;
        internal int decimals;
        internal string suffix = "";
        internal Func<float> getter;
        internal Action<float> onValue;

        private bool _dragging;

        public void OnPointerDown(PointerEventData e)
        {
            _dragging = true;
            Apply(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_dragging) Apply(e);
        }

        private void Apply(PointerEventData e)
        {
            if (hit == null || getter == null) return;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(hit, e.position,
                    e.pressEventCamera, out local))
                return;

            float w = hit.rect.width > 1f ? hit.rect.width : hit.sizeDelta.x;
            if (w <= 1f) return;

            float t = Mathf.Clamp01(local.x / w);
            float v = min + t * (max - min);
            if (step > 0f) v = min + Mathf.Round((v - min) / step) * step;
            v = Mathf.Clamp(v, min, max);

            if (Mathf.Abs(getter() - v) < 0.0005f) return;

            if (onValue != null) onValue(v);
            Refresh();
        }

        internal void Refresh()
        {
            float v = getter != null ? getter() : min;
            float t = Mathf.InverseLerp(min, max, v);
            float w = hit != null ? (hit.rect.width > 1f ? hit.rect.width : hit.sizeDelta.x) : SliderWidthFallback;

            if (fill != null)
                fill.sizeDelta = new Vector2(Mathf.Max(0f, w * t), fill.sizeDelta.y);

            if (knob != null)
                knob.anchoredPosition = new Vector2(Mathf.Clamp(w * t - 7f, -1f, Mathf.Max(-1f, w - 13f)),
                                                    knob.anchoredPosition.y);

            if (readout != null)
                readout.text = v.ToString(decimals == 0 ? "F0" : (decimals == 1 ? "F1" : "F2")) + suffix;
        }

        private const float SliderWidthFallback = 330f;
    }
}
