// EventNarratorPanel.cs — 事件框架 · 叙事者选择面板（左选项卡 / 右描述 + 可配置滑块）；详见《事件.md》§23.1

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EventFramework
{
    // 自绘覆盖面板：挂在分类选择屏上，盖住原卡片（不改原屏结构，退出即销毁）
    internal static class EventNarratorPanel
    {
        private const float WinW = 1170f;
        private const float WinH = 705f;

        // 原版配色（取自 ClusterCategorySelectionScreen 实测色值）
        private static readonly Color ColBg      = new Color(0.251f, 0.271f, 0.349f, 0.99f);
        private static readonly Color ColHead    = new Color(0.498f, 0.239f, 0.369f, 1f);
        private static readonly Color ColRail    = new Color(0.204f, 0.220f, 0.286f, 0.85f);
        private static readonly Color ColText    = new Color(0.937f, 0.945f, 0.960f, 1f);
        private static readonly Color ColDim     = new Color(0.655f, 0.678f, 0.733f, 1f);
        private static readonly Color ColBody    = new Color(0.804f, 0.827f, 0.867f, 1f);
        private static readonly Color ColGold    = new Color(0.909f, 0.769f, 0.416f, 1f);
        private static readonly Color ColCyan    = new Color(0.431f, 0.827f, 0.909f, 1f);
        private static readonly Color ColTabOn   = new Color(0.361f, 0.302f, 0.478f, 1f);
        private static readonly Color ColTabOff  = new Color(0.196f, 0.212f, 0.271f, 1f);
        private static readonly Color ColDivider = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color ColTrack   = new Color(1f, 1f, 1f, 0.14f);

        // 行高与列宽（全部手算，面板子树内禁止布局组件）
        private const float RowH = 31f;
        private const float HeadH = 31f;
        private const float ColGap = 23f;
        private const float LabelW = 130f;
        private const float SliderX = 138f;
        private const float SliderW = 170f;
        private const float ReadoutX = 316f;
        private const float ReadoutW = 94f;

        private sealed class TabRef
        {
            internal string id;
            internal Image bg;
            internal Image bar;
            internal TextMeshProUGUI label;
        }

        // 事务式草稿：拖动只改它，点「继续」才一次性写回注册表实例
        private sealed class Draft
        {
            internal string id;
            internal float i0;
            internal float w;
            internal float r;
            internal float origI0;
            internal float origW;
            internal float origR;
        }

        private static GameObject _root;
        private static Transform _host;
        private static GameObject _body;
        private static readonly List<TabRef> _tabs = new List<TabRef>();
        private static string _sel;
        private static TMP_FontAsset _font;
        private static bool _fontScanned;

        // 当前面板绑定的叙事者与派生显示
        private static EventNarrator _n;
        private static readonly List<NarratorSlider> _sliders = new List<NarratorSlider>();
        private static TextMeshProUGUI _derivedTip;
        private static Draft _draft;

        // 选择屏在主菜单打开时事件尚未加载，先补一次扫描，保证池计数可用
        private static bool _eventsScannedOnce;

        // 极性计数按注册表版本缓存，拖动时不再全表扫
        private static int _poolCacheVersion = -1;
        private static string _poolCacheId;
        private static int _cachedPos;
        private static int _cachedNeu;
        private static int _cachedNeg;

        // ---- 对外 ----

        internal static void Show(Transform host)
        {
            if (host == null) return;
            EnsureEventsScanned();
            if (_root != null && _host == host)
            {
                RefreshTabs();
                RebuildBody();
                return;
            }
            Hide();
            _host = host;
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Warn("构建叙事者面板失败: " + e);
                Hide();
            }
        }

        internal static void Hide()
        {
            _tabs.Clear();
            _sliders.Clear();
            _derivedTip = null;
            _draft = null;
            _n = null;
            _body = null;
            _host = null;
            if (_root != null)
            {
                try { _root.SetActive(false); UnityEngine.Object.Destroy(_root); }
                catch (Exception e) { Warn("销毁面板根节点异常: " + e); }
                _root = null;
            }
        }

        internal static bool IsOpen { get { return _root != null; } }

        // 主菜单阶段注册表为空；提前加载一次，后续开局 Init 会 Clear 后重载，不影响正式流程
        private static void EnsureEventsScanned()
        {
            if (EventRegistry.Count > 0 || _eventsScannedOnce) return;
            try
            {
                EventYamlLoader.LoadAll();
                _eventsScannedOnce = true;
            }
            catch (Exception e)
            {
                Warn("选择面板提前扫描事件失败: " + e);
            }
        }

        // ---- 构建 ----

        private static void Build()
        {
            EventNarratorRegistry.EnsureBuiltins();
            if (_font == null) _fontScanned = false;   // 上一屏可能还没生成文本，允许重扫

            _root = EventUiKit.MakeNode("NarratorPanel", _host);
            EventUiKit.Stretch(_root);

            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            GameObject win = EventUiKit.MakeNode("Window", _root.transform);
            EventUiKit.CenterOnScreen(win, WinW, WinH);
            FitToScreen(win);

            Image bg = UiSkin.RoundSolid("bg", win.transform, 14, ColBg);
            EventUiKit.Stretch(bg.gameObject);
            Image edge = UiSkin.RoundOutline("edge", win.transform, 14, 1, ColDivider);
            EventUiKit.Stretch(edge.gameObject);

            BuildHead(win.transform);
            BuildRail(win.transform);
            BuildFooter(win.transform);

            _body = EventUiKit.MakeNode("Body", win.transform);
            EventUiKit.Place(_body, 309f, 118f, 843f, 480f);

            _sel = ResolveInitial();
            RefreshTabs();
            RebuildBody();

            Debug.Log("[MoreFood/Narr] 选择面板已打开：叙事者 " + EventNarratorRegistry.Count
                      + " 位，当前 '" + (_sel ?? "无") + "'");
        }

        private static void FitToScreen(GameObject win)
        {
            try
            {
                var rt = _root.GetComponent<RectTransform>();
                if (rt == null) return;
                float availW = rt.rect.width;
                float availH = rt.rect.height;
                if (availW < 120f || availH < 120f) return;
                float k = Mathf.Min(1f, Mathf.Min(availW / (WinW + 30f), availH / (WinH + 30f)));
                if (k < 0.999f) win.transform.localScale = new Vector3(k, k, 1f);
            }
            catch (Exception e) { Warn("面板自适应缩放异常: " + e); }
        }

        private static void BuildHead(Transform parent)
        {
            GameObject head = EventUiKit.MakeNode("Head", parent);
            EventUiKit.Place(head, 18f, 18f, WinW - 36f, 70f);

            Image headBg = UiSkin.RoundSolid("headbg", head.transform, 12, ColHead);
            EventUiKit.Stretch(headBg.gameObject);

            Image bar = UiSkin.RoundSolid("bar", head.transform, 2, ColGold);
            EventUiKit.Place(bar.gameObject, 16f, 14f, 5f, 42f);

            PutText("Title", head.transform, EventNarratorSelection.NARRATOR_TITLE,
                30f, ColText, TextAlignmentOptions.MidlineLeft, 34f, 0f, 620f, 70f);

            PutText("Sub", head.transform, "选择决定事件出现的节奏与风格",
                18f, new Color(1f, 1f, 1f, 0.70f), TextAlignmentOptions.MidlineRight,
                WinW - 36f - 560f - 26f, 0f, 560f, 70f);

            Image line = UiSkin.RoundSolid("divider", parent, 1, ColDivider);
            EventUiKit.Place(line.gameObject, 18f, 100f, WinW - 36f, 1f);
        }

        private static void BuildRail(Transform parent)
        {
            Image rail = UiSkin.RoundSolid("rail", parent, 12, ColRail);
            EventUiKit.Place(rail.gameObject, 18f, 118f, 273f, 480f);

            var all = EventNarratorRegistry.All;
            int n = all != null ? all.Count : 0;
            if (n > 5)
            {
                Warn("叙事者数量 " + n + " 超过面板可容纳的 5 个，仅显示前 5 个");
                n = 5;
            }

            float y = 132f;
            for (int i = 0; i < n; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                GameObject tab = EventUiKit.MakeNode("Tab_" + def.Id, parent);
                EventUiKit.Place(tab, 33f, y, 243f, 63f);

                Image bg = tab.AddComponent<Image>();
                bg.sprite = UiSkin.Rounded(11);
                bg.type = Image.Type.Sliced;
                bg.color = ColTabOff;

                Button btn = tab.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.ColorTint;
                var colors = btn.colors;
                colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(1f, 1f, 1f, 0.85f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                colors.fadeDuration = 0.08f;
                btn.colors = colors;

                string captured = def.Id;
                btn.onClick.AddListener(delegate { Select(captured); });

                Image selBar = UiSkin.RoundSolid("sel", tab.transform, 2, ColGold);
                EventUiKit.Place(selBar.gameObject, 0f, 10f, 4f, 43f);
                selBar.enabled = false;

                TextMeshProUGUI label = PutText("Label", tab.transform,
                    !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : def.Id,
                    21f, ColDim, TextAlignmentOptions.MidlineLeft, 22f, 0f, 210f, 63f);

                var tr = new TabRef();
                tr.id = def.Id;
                tr.bg = bg;
                tr.bar = selBar;
                tr.label = label;
                _tabs.Add(tr);

                y += 69f;
            }

            if (_tabs.Count == 0)
            {
                PutText("Empty", parent, "没有已注册的叙事者",
                    19f, ColDim, TextAlignmentOptions.MidlineLeft, 33f, 132f, 243f, 63f);
            }

            if (_tabs.Count <= 3)
            {
                PutText("Desc", parent, EventNarratorSelection.NARRATOR_DESC,
                    17f, ColDim, TextAlignmentOptions.TopLeft, 33f, y + 16f, 243f, 150f, true);
            }
        }

        private static void BuildFooter(Transform parent)
        {
            Image line = UiSkin.RoundSolid("footline", parent, 1, ColDivider);
            EventUiKit.Place(line.gameObject, 18f, WinH - 86f, WinW - 36f, 1f);

            GameObject go = EventUiKit.MakeNode("BtnContinue", parent);
            EventUiKit.Place(go, WinW - 18f - 130f, WinH - 78f, 130f, 50f);

            Image bg = go.AddComponent<Image>();
            bg.sprite = UiSkin.Rounded(11);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.196f, 0.310f, 0.400f, 1f);

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
            btn.onClick.AddListener(OnContinue);

            PutText("Label", go.transform, "继 续",
                22f, ColText, TextAlignmentOptions.Center, 0f, 0f, 130f, 50f);
        }

        // ---- 数据 ----

        private static string ResolveInitial()
        {
            string id = EventNarratorSelection.ResolveForSession();
            if (!string.IsNullOrEmpty(id) && EventNarratorRegistry.Get(id) != null) return id;
            id = EventNarratorDirector.PickNarratorId();
            if (!string.IsNullOrEmpty(id) && EventNarratorRegistry.Get(id) != null) return id;
            var all = EventNarratorRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && !string.IsNullOrEmpty(all[i].Id)) return all[i].Id;
            return null;
        }

        private static void Select(string id)
        {
            if (string.IsNullOrEmpty(id) || id == _sel) return;
            _sel = id;
            RefreshTabs();
            RebuildBody();
            var n = EventNarratorRegistry.Get(id);
            Debug.Log("[MoreFood/Narr] 选择面板切换到 '" + id + "'：" + (n != null ? n.ToString() : "取不到数据"));
        }

        private static void RefreshTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                var t = _tabs[i];
                if (t == null) continue;
                bool on = (t.id == _sel);
                if (t.bg != null) t.bg.color = on ? ColTabOn : ColTabOff;
                if (t.bar != null) t.bar.enabled = on;
                if (t.label != null)
                {
                    t.label.color = on ? ColText : ColDim;
                    t.label.rectTransform.anchoredPosition = new Vector2(on ? 24f : 22f, 0f);
                }
            }
        }

        private static void RebuildBody()
        {
            if (_body == null) return;

            for (int i = _body.transform.childCount - 1; i >= 0; i--)
            {
                var c = _body.transform.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
            _sliders.Clear();
            _derivedTip = null;
            _draft = null;

            var n = EventNarratorRegistry.Get(_sel);
            _n = n;
            if (n == null)
            {
                PutText("NoData", _body.transform, "取不到叙事者数据（registry 为空）",
                    19f, ColDim, TextAlignmentOptions.MidlineLeft, 0f, 0f, 800f, 46f);
                return;
            }

            PutText("Name", _body.transform,
                !string.IsNullOrEmpty(n.DisplayName) ? n.DisplayName : n.Id,
                30f, ColText, TextAlignmentOptions.MidlineLeft, 0f, 0f, 520f, 46f);

            string badge = n.IsDefault ? "默认" : (n.Enabled ? "" : "已停用");
            if (!string.IsNullOrEmpty(badge))
            {
                PutText("Badge", _body.transform, badge, 19f, ColGold,
                    TextAlignmentOptions.MidlineRight, 520f, 0f, 323f, 46f);
            }

            PutText("Desc", _body.transform, DescribeOf(n),
                18f, ColBody, TextAlignmentOptions.TopLeft, 0f, 52f, 843f, 84f, true);

            GameObject colA = EventUiKit.MakeNode("ColA", _body.transform);
            EventUiKit.Place(colA, 0f, 146f, 410f, 334f);
            GameObject colB = EventUiKit.MakeNode("ColB", _body.transform);
            EventUiKit.Place(colB, 410f + ColGap, 146f, 410f, 334f);

            LoadDraft(n);
            BuildSliders(colA.transform);
            BuildDerived(colB.transform);

            for (int i = 0; i < _sliders.Count; i++) _sliders[i].Refresh();
            RefreshDerived();
        }

        // 打开 / 切页时从注册表实例深拷贝三项参数，同时记住原值供失败回滚
        private static void LoadDraft(EventNarrator n)
        {
            _draft = new Draft();
            _draft.id = n.Id;
            _draft.i0 = _draft.origI0 = n.I0;
            _draft.w = _draft.origW = n.W;
            _draft.r = _draft.origR = n.R;
        }

        private static void BuildSliders(Transform col)
        {
            float y = Section(col, 0f, "三项可调参数");

            var d = _draft;
            y = AddSlider(col, y, "节奏", 600f, 12000f, 60f, 0,
                delegate { return d != null ? d.i0 : 6000f; },
                delegate (float v) { if (d != null) d.i0 = v; }, " 秒",
                "事件平均每隔多久出现一次；数值越大，事件越少、开局越平静。");
            y = AddSlider(col, y, "起伏", 0f, 0.90f, 0.05f, 2,
                delegate { return d != null ? d.w : 0.15f; },
                delegate (float v) { if (d != null) d.w = v; }, "",
                "事件间隔的波动幅度；数值越大，忽快忽慢越明显。");
            y = AddSlider(col, y, "喘息", 1.5f, 3.0f, 0.1f, 1,
                delegate { return d != null ? d.r : 2f; },
                delegate (float v) { if (d != null) d.r = v; }, " 倍",
                "平静期比活跃期慢多少倍；数值越大，平静时等待事件越久。");
        }

        private static void BuildDerived(Transform col)
        {
            float y = Section(col, 0f, "当前派生");
            _derivedTip = PutText("Derived", col, "", 16f, ColBody,
                TextAlignmentOptions.TopLeft, 0f, y, 410f, 280f, true);
        }

        // ---- 控件：滑块 ----

        private static float AddSlider(Transform col, float y, string label,
            float min, float max, float step, int decimals,
            Func<float> getter, Action<float> setter, string suffix, string tip)
        {
            TextMeshProUGUI labelText = PutText("K_" + label, col, label, 17f, ColDim,
                TextAlignmentOptions.MidlineLeft, 0f, y, LabelW, RowH);

            GameObject hit = EventUiKit.MakeNode("S_" + label, col);
            EventUiKit.Place(hit, SliderX, y, SliderW, RowH);

            // 透明命中区：吃点击/拖动（视觉件自身不放 raycast）
            var hitImg = hit.AddComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;

            Image track = UiSkin.RoundSolid("track", hit.transform, 3, ColTrack);
            EventUiKit.Place(track.gameObject, 0f, 12f, SliderW, 6f);

            Image fill = UiSkin.RoundSolid("fill", hit.transform, 3, ColCyan);
            EventUiKit.Place(fill.gameObject, 0f, 12f, 1f, 6f);

            Image knob = UiSkin.RoundSolid("knob", hit.transform, 5, ColText);
            EventUiKit.Place(knob.gameObject, 0f, 7f, 14f, 14f);

            var readout = PutText("V_" + label, col, "", 17f, ColText,
                TextAlignmentOptions.MidlineRight, ReadoutX, y, ReadoutW, RowH);

            var s = hit.AddComponent<NarratorSlider>();
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
                RefreshDerived();
                if (readout != null)
                    readout.text = v.ToString(decimals == 0 ? "F0" : (decimals == 1 ? "F1" : "F2")) + suffix;
            };
            _sliders.Add(s);

            // 悬停解释：与原版一致，只有选项文字挂 ToolTip；滑块轨道/把手/数值都不响应
            NarratorToolTip.Attach(labelText.gameObject, tip);

            return y + RowH;
        }

        // ---- 派生显示 ----

        // 拖动时按草稿值刷新：活跃/冷静期间隔、预期事件频率、负面目标与三类事件数量
        private static void RefreshDerived()
        {
            if (_derivedTip == null || _n == null || _draft == null) return;
            try
            {
                float t = EventSystem.GetGameTime() / 600f;
                float activity, trend;
                _n.Probe(t, out activity, out trend);
                float iMean = NarratorCurve.MeanInterval(_draft.i0, _n.LEnd, trend);
                float act, calm, aLo, aHi, cLo, cHi;
                NarratorCurve.Centers(iMean, activity, _draft.w, _draft.r,
                                      out act, out calm, out aLo, out aHi, out cLo, out cHi);
                float target = NarratorCurve.NegativeTarget(trend, _n.S0, _n.S1);

                int np, nn, ng;
                CountPool(_n.Pool, out np, out nn, out ng);

                _derivedTip.text =
                    "活跃期 " + (act / 600f).ToString("F1") + " 周期\n" +
                    "冷静期 " + (calm / 600f).ToString("F1") + " 周期\n" +
                    "预期事件频率 " + (iMean / 600f).ToString("F1") + " 周期/件\n" +
                    "负面事件目标 " + F0(target * 100f) + "%\n" +
                    "正向事件 " + np + " 个\n" +
                    "中性事件 " + nn + " 个\n" +
                    "负向事件 " + ng + " 个";
            }
            catch (Exception e)
            {
                Warn("刷新派生数值失败: " + e);
            }
        }

        // 统计某个池里三种极性各有多少个已注册事件；按注册表版本缓存，拖动时不全表扫
        private static void CountPool(string pool, out int np, out int nn, out int ng)
        {
            string want = string.IsNullOrEmpty(pool) ? "normal" : pool;
            int ver = EventRegistry.Version;
            if (_poolCacheVersion == ver && _poolCacheId == want)
            {
                np = _cachedPos;
                nn = _cachedNeu;
                ng = _cachedNeg;
                return;
            }

            np = nn = ng = 0;
            foreach (var d in EventRegistry.All)
            {
                if (d == null) continue;
                string dp = string.IsNullOrEmpty(d.pool) ? "normal" : d.pool;
                if (dp != want) continue;
                if (d.polarity == EventPolarity.positive) np++;
                else if (d.polarity == EventPolarity.negative) ng++;
                else nn++;
            }

            _poolCacheVersion = ver;
            _poolCacheId = want;
            _cachedPos = np;
            _cachedNeu = nn;
            _cachedNeg = ng;
        }

        private static string DescribeOf(EventNarrator n)
        {
            if (!string.IsNullOrEmpty(n.Description)) return n.Description;
            return "开局平均间隔 " + F0(n.I0) + " 秒，状态区间半宽 " + n.W.ToString("F2")
                   + "，平静期为活跃期的 " + n.R.ToString("F1") + " 倍。";
        }

        // ---- 绘图基元 ----

        private static float Section(Transform col, float y, string title)
        {
            Image bar = UiSkin.RoundSolid(title + "_bar", col, 1, ColCyan);
            EventUiKit.Place(bar.gameObject, 0f, y + 8f, 4f, 16f);
            PutText(title, col, title, 19f, ColCyan, TextAlignmentOptions.MidlineLeft,
                12f, y, 200f, HeadH);
            return y + HeadH;
        }

        private static string F0(float v) { return v.ToString("F0"); }

        // ---- 交互 ----

        private static void OnContinue()
        {
            string id = _sel;
            if (string.IsNullOrEmpty(id)) id = EventNarratorDirector.PickNarratorId();

            var n = EventNarratorRegistry.Get(id);
            try
            {
                if (n != null && _draft != null && _draft.id == id)
                {
                    float i0 = _draft.i0, w = _draft.w, r = _draft.r;
                    n.I0 = i0;
                    n.W = w;
                    n.R = r;
                    Debug.Log("[MoreFood/Narr] 选择面板「继续」：叙事者 = " + id
                              + "；配置 节奏=" + F0(i0) + "s 起伏=" + w.ToString("F2")
                              + " 喘息=" + r.ToString("F1")
                              + "；" + n.ToString());
                }
                else
                {
                    Debug.Log("[MoreFood/Narr] 选择面板「继续」：叙事者 = " + (id ?? "无")
                              + "（无草稿，保留内置默认）");
                }
            }
            catch (Exception e)
            {
                if (n != null && _draft != null)
                {
                    n.I0 = _draft.origI0;
                    n.W = _draft.origW;
                    n.R = _draft.origR;
                }
                Warn("提交叙事者参数失败，已回滚到内置默认: " + e);
            }

            Hide();
            EventNarratorSelection.CommitChoice(id);
        }

        // ---- 文本 ----

        private static TextMeshProUGUI PutText(string name, Transform parent, string text,
            float size, Color color, TextAlignmentOptions align, float x, float y, float w, float h)
        {
            return PutText(name, parent, text, size, color, align, x, y, w, h, false);
        }

        private static TextMeshProUGUI PutText(string name, Transform parent, string text,
            float size, Color color, TextAlignmentOptions align, float x, float y, float w, float h,
            bool wrap)
        {
            GameObject go = EventUiKit.MakeNode(name, parent);
            EventUiKit.Place(go, x, y, w, h);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = Font();
            t.text = text ?? "";
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = wrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static TMP_FontAsset Font()
        {
            if (_font != null) return _font;
            if (_fontScanned) return null;
            _fontScanned = true;
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<LocText>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].font != null)
                    {
                        _font = all[i].font;
                        return _font;
                    }
                }
            }
            catch (Exception e) { Warn("扫描屏内字体异常: " + e); }
            return null;
        }

        private static void Warn(string msg)
        {
            Debug.LogWarning("[MoreFood/Narr] " + msg);
        }
    }

    // 手绘滑块：透明命中区收点击/拖动，换算成 0~1 再吸附步长
    public sealed class NarratorSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
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
            float w = hit != null ? (hit.rect.width > 1f ? hit.rect.width : hit.sizeDelta.x) : 170f;

            if (fill != null)
                fill.sizeDelta = new Vector2(Mathf.Max(0f, w * t), fill.sizeDelta.y);

            if (knob != null)
                knob.anchoredPosition = new Vector2(Mathf.Clamp(w * t - 7f, -1f, Mathf.Max(-1f, w - 13f)),
                                                    knob.anchoredPosition.y);

            if (readout != null)
                readout.text = v.ToString(decimals == 0 ? "F0" : (decimals == 1 ? "F1" : "F2")) + suffix;
        }
    }

    // 原版悬停提示：挂 ONI 的 ToolTip 组件，悬停时在光标旁浮层显示，不占页面文字
    internal static class NarratorToolTip
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
}
