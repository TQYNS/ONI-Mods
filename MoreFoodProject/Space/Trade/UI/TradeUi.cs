using System;
using EventFramework.Distribution.Contracts;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class TradeUi
    {
        // ★ 重新设计：配色/圆角对齐 MoreFood 的「时间历史」面板（深海军蓝 + 金 + 青）
        internal static readonly Color BgRoot = new Color32(14, 22, 38, 247);
        internal static readonly Color Panel = new Color32(27, 42, 71, 255);
        internal static readonly Color PanelAlt = new Color32(22, 33, 54, 235);
        internal static readonly Color RowIn = new Color32(24, 36, 60, 235);
        internal static readonly Color RowInB = new Color32(18, 27, 48, 235);
        internal static readonly Color TabOn = new Color32(58, 42, 94, 255);
        internal static readonly Color TabOff = new Color32(22, 33, 54, 235);
        internal static readonly Color Line = new Color32(40, 56, 84, 255);
        internal static readonly Color Border = new Color32(60, 82, 120, 255);
        internal static readonly Color Accent = new Color32(110, 211, 232, 255);
        internal static readonly Color AccentDim = new Color32(70, 140, 160, 255);
        internal static readonly Color Gold = new Color32(232, 196, 106, 255);
        internal static readonly Color TextMain = new Color32(234, 240, 248, 255);
        internal static readonly Color TextDim = new Color32(147, 162, 184, 255);
        internal static readonly Color TextOff = new Color32(96, 108, 128, 255);
        internal static readonly Color Pos = new Color32(120, 216, 150, 255);
        internal static readonly Color Neg = new Color32(232, 106, 106, 255);
        internal static readonly Color Warn = new Color32(232, 196, 106, 255);

        // 圆角块：复用 MoreFood 生成的圆角九宫格图（UiSkin.Rounded 是 public，缓存过的）
        internal static Image RoundPanel(string name, Transform parent, float x, float y,
            float w, float h, int radius, Color color)
        {
            GameObject go = Node(name, parent, x, y, w, h);
            Image img = go.AddComponent<Image>();
            try
            {
                img.sprite = UiSkin.Rounded(radius);
                img.type = Image.Type.Sliced;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 圆角图取不到，退化为直角");
            }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // 1px 细线（描边/分隔）
        internal static Image HLine(string name, Transform parent, float x, float y, float w, Color c)
        {
            return RoundPanel(name, parent, x, y, w, 1f, 1, c);
        }

        // ★ v5.8 稀有度六色（§6）：极差/很差同白，靠物品名 + 排序区分
        internal static readonly Color[] RarityColor = new Color[]
        {
            new Color(0.875f, 0.965f, 0.976f, 1f),   // 极差 #DFF6F9
            new Color(0.875f, 0.965f, 0.976f, 1f),   // 很差 #DFF6F9
            new Color(0.310f, 0.878f, 0.541f, 1f),   // 一般 #4FE08A
            new Color(0.310f, 0.765f, 0.969f, 1f),   // 精良 #4FC3F7
            new Color(0.753f, 0.549f, 0.941f, 1f),   // 大师 #C08CF0
            new Color(1f, 0.761f, 0.290f, 1f)        // 传奇 #FFC24A
        };

        private static GameObject bar;
        // ★ 修订（站点按钮排不刷新）：记住按钮排当前对应的站点与"火箭是否到站"，
        //   由星图位置变更事件驱动重算；只在结论翻转时重建，避免飞行途中逐格重绘。
        private static SpaceStation barStation;
        // ★ 按钮引用：位置变化时就地改"点亮/灰掉"，不重建按钮排（无分配、无 GC）
        private static TMP_FontAsset cachedFont;
        private static GameObject window;
        private static GameObject remote;
        private static GameObject refuelWindow;
        private static string windowStation;
        private static string remoteStation;
        private static string refuelStation;
        private static bool remoteDonate;   // 远程窗是不是"查看捐款项"
        private static int windowMode;   // 0 = 交易窗，1 = 捐款窗

        internal static TMP_FontAsset GameFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }
            try
            {
                LocText[] texts = UnityEngine.Object.FindObjectsByType<LocText>(FindObjectsSortMode.InstanceID);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].font != null)
                    {
                        cachedFont = texts[i].font;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 取游戏字体失败");
            }
            if (cachedFont == null)
            {
                cachedFont = TMP_Settings.defaultFontAsset;
            }
            return cachedFont;
        }

        internal static void Notify(string title, string text)
        {
            Notify(title, text, null, null);
        }

        // jumpInstId 非空 ⇒ 点击该通知时调用事件定位器跳转（找不到目标则自动跳过）
        internal static void Notify(string title, string text, string jumpInstId)
        {
            Notify(title, text, jumpInstId, null);
        }

        // tier 非空 ⇒ 该通知按事件档位外观显示（挂 eventfw.event.<档位> 标记，由 EventNotifyStylePatch 接管）
        internal static void Notify(string title, string text, string jumpInstId, string tier)
        {
            if (PushVanillaNotification(title, text, jumpInstId, tier))
            {
                return;
            }
            MFLog.Info(LogCat.Trade,"[Space] 通知 " + title + "：" + text);
        }

        private static void OnNotifClicked(object data)
        {
            try
            {
                string id = data as string;
                if (string.IsNullOrEmpty(id))
                {
                    return;
                }
                // 先走星图定位，再回退世界实体定位
                if (StarmapLocator.Jump(id))
                {
                    return;
                }
                EventFrameworkApi.Jump(id);
            }
            catch (Exception e) { MFLog.Exc(LogCat.Trade, e, "[Space] 通知跳转失败"); }
        }

        private static string NotifTooltip(List<Notification> list, object data)
        {
            return data as string ?? "";
        }

        private static Notifier FindNotifier()
        {
            try
            {
                WorldContainer w = ClusterManager.Instance != null ? ClusterManager.Instance.GetStartWorld() : null;
                if (w != null)
                {
                    Notifier wn = w.gameObject.GetComponent<Notifier>();
                    if (wn != null)
                    {
                        return wn;
                    }
                }
                List<Notifier> all = Components.Notifiers.Items;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && !all[i].DisableNotifications)
                    {
                        return all[i];
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 取通知宿主失败");
            }
            return null;
        }

        private static bool PushVanillaNotification(string title, string text, string jumpInstId, string tier)
        {
            try
            {
                Notifier notifier = FindNotifier();
                if (notifier == null)
                {
                    return false;
                }
                NotificationType type = string.IsNullOrEmpty(tier)
                    ? NotificationType.Good
                    : TradeNotifyTier.TypeForTier(tier);
                Notification n = new Notification(
                    title,
                    type,
                    new Func<List<Notification>, object, string>(NotifTooltip),
                    text,
                    true,
                    0f,
                    new Notification.ClickCallback(OnNotifClicked),
                    jumpInstId,
                    null,
                    true,
                    true,
                    false);
                if (!string.IsNullOrEmpty(tier))
                {
                    n.customNotificationID = TradeNotifyTier.MarkFor(tier);
                }
                notifier.Add(n);
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 原版通知失败");
                return false;
            }
        }

        internal static GameObject Node(string name, Transform parent, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        internal static Image Solid(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            GameObject go = Node(name, parent, x, y, w, h);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        internal static TextMeshProUGUI Text(string name, Transform parent, float x, float y, float w, float h,
            string content, float size, Color color, bool alignRight)
        {
            GameObject go = Node(name, parent, x, y, w, h);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.font = GameFont();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = alignRight ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;
            return t;
        }

        // 居中文本：分页页码这类要卡在左右按钮正中间的场合用（Text 只支持左/右）
        internal static TextMeshProUGUI TextCenter(string name, Transform parent, float x, float y, float w, float h,
            string content, float size, Color color)
        {
            TextMeshProUGUI t = Text(name, parent, x, y, w, h, content, size, color, false);
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        // 估宽：中文按全角（=字号）、其余按半角，用于自动撑开标签卡（只按字符数估会把中文标签挤爆）
        internal static float LabelWidth(string s, float size)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0f;
            }
            float w = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                w += s[i] > 0x2E80 ? size : size * 0.56f;
            }
            return w;
        }

        internal static Button MakeButton(string label, Transform parent, float x, float y, float w, float h,
            Color fill, Color edge, Color textColor, System.Action onClick)
        {
            GameObject go = Node(label, parent, x, y, w, h);
            Image outline = go.AddComponent<Image>();
            try
            {
                outline.sprite = UiSkin.RoundedOutline(8, 1);
                outline.type = Image.Type.Sliced;
            }
            catch (Exception)
            {
                // expected: 圆角描边贴图不可用时退化为纯色边框
            }
            outline.color = edge;
            outline.raycastTarget = false;
            Image body = RoundPanel("body", go.transform, 1f, 1f, w - 2f, h - 2f, 7, fill);
            body.raycastTarget = true;
            TextMeshProUGUI t = Text("label", go.transform, 0f, 0f, w, h, label, 15f, textColor, false);
            t.alignment = TextAlignmentOptions.Center;
            Button b = go.AddComponent<Button>();
            b.targetGraphic = body;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.28f, 1.28f, 1.28f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            // ★ 禁用态不改色：灰色外观由调用方显式给的 fill/edge/text 决定，
            //   否则 Unity 会用默认 disabledColor(0.78 半透明白) 覆盖掉我们设的灰
            cb.disabledColor = Color.white;
            b.colors = cb;
            if (onClick != null)
            {
                b.onClick.AddListener(delegate { onClick(); });
            }
            return b;
        }

        // 图标吸附到指定圆心：pivot 居中 + anchoredPosition 指向圆心（交易行与加油窗共用）
        internal static void CenterIcon(Image ic, float cx, float cy, float size)
        {
            if (ic == null)
            {
                return;
            }
            RectTransform rt = ic.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(cx, -cy);
        }

        // 内部结果弹窗：交易窗与加油窗共用同一套外观（尺寸由调用方给）
        internal static GameObject MakeResultDialog(Transform parent, float x, float y, float w, float h,
            string title, string message, bool neg, System.Action onClose)
        {
            GameObject go = Node("resultDialog", parent, x, y, w, h).gameObject;
            RoundPanel("bg", go.transform, 0f, 0f, w, h, 14, Panel);
            HLine("top", go.transform, 0f, 0f, w, neg ? Neg : Accent);
            Text("t", go.transform, 28f, 20f, w - 56f, 34f, title, 22f, neg ? Neg : Accent, false);
            TextMeshProUGUI body = Text("m", go.transform, 28f, 66f, w - 56f, h - 144f, message, 16f,
                TextMain, false);
            body.textWrappingMode = TextWrappingModes.Normal;
            MakeButton("确定", go.transform, (w - 180f) * 0.5f, h - 66f, 180f, 48f,
                TabOn, Accent, Accent, delegate { if (onClose != null) onClose(); });
            return go;
        }

        // 行底：交替条纹 + 圆角
        internal static Image RowBg(string name, Transform parent, float y, float w, float h, bool alt)
        {
            return RoundPanel(name, parent, 0f, y, w, h, 4, alt ? RowInB : RowIn);
        }

        // ★ 关闭按钮：与 MoreFood「事件历史」屏右上角的 × 同款（36×36 圆角9，常态白6%，
        internal static Button MakeCloseButton(Transform parent, float x, float y, System.Action onClick)
        {
            GameObject go = Node("close", parent, x, y, 36f, 36f);
            Image bg = go.AddComponent<Image>();
            try
            {
                bg.sprite = UiSkin.Rounded(9);
                bg.type = Image.Type.Sliced;
            }
            catch (Exception)
            {
                // expected: 圆角贴图不可用时退化为纯色底
            }
            Color normal = new Color(1f, 1f, 1f, 0.06f);
            bg.color = normal;
            bg.raycastTarget = true;
            TextMeshProUGUI t = Text("glyph", go.transform, 0f, 0f, 36f, 36f, "×", 20f, TextMain, false);
            t.alignment = TextAlignmentOptions.Center;
            HoverTint hv = go.AddComponent<HoverTint>();
            hv.target = bg;
            hv.normal = normal;
            hv.hover = new Color(0.91f, 0.42f, 0.42f, 0.35f);
            hv.press = new Color(0.91f, 0.42f, 0.42f, 0.55f);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = bg;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = Color.white;
            b.colors = cb;
            if (onClick != null)
            {
                b.onClick.AddListener(delegate { onClick(); });
            }
            return b;
        }

        internal sealed class HoverTint : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler,
            UnityEngine.EventSystems.IPointerExitHandler, UnityEngine.EventSystems.IPointerDownHandler,
            UnityEngine.EventSystems.IPointerUpHandler
        {
            internal Image target;
            internal Color normal = Color.white;
            internal Color hover = Color.white;
            internal Color press = Color.white;

            private void Apply(Color c)
            {
                if (target != null)
                {
                    target.color = c;
                }
            }

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
            {
                Apply(hover);
            }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            {
                Apply(normal);
            }

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
            {
                Apply(press);
            }

            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e)
            {
                Apply(hover);
            }
        }

        // ★ 滚动视图：矩形裁剪 + ScrollRect（滚轮）。返回 viewport，content 通过 out 给出。
        internal static GameObject MakeScrollView(string name, Transform parent, float x, float y,
            float w, float h, out RectTransform content)
        {
            GameObject view = Node(name, parent, x, y, w, h);
            view.AddComponent<RectMask2D>();
            GameObject contentGo = new GameObject("content", typeof(RectTransform));
            RectTransform crt = contentGo.GetComponent<RectTransform>();
            crt.SetParent(view.transform, false);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, 0f);
            ScrollRect sr = view.AddComponent<ScrollRect>();
            sr.viewport = view.GetComponent<RectTransform>();
            sr.content = crt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;
            sr.inertia = false;
            content = crt;
            return view;
        }

        internal static Transform OverlayParent()
        {
            if (GameScreenManager.Instance != null && GameScreenManager.Instance.ssOverlayCanvas != null)
            {
                return GameScreenManager.Instance.ssOverlayCanvas.transform;
            }
            if (ScreenPrefabs.Instance != null && ScreenPrefabs.Instance.HudScreen != null)
            {
                return ScreenPrefabs.Instance.HudScreen.transform;
            }
            return null;
        }

        internal static bool WindowOpen()
        {
            return window != null;
        }

        internal static bool AnyOpen()
        {
            return window != null || remote != null || refuelWindow != null;
        }

        // 加油是独立窗口：与交易/远程窗互斥开启，但同款「再点一次按钮即关闭」
        internal static bool RefuelConfigured(SpaceStation station)
        {
            StationDef d = station != null ? station.Def : null;
            return d != null && d.fuel_refill && TradeDb.Fuels.Count > 0;
        }

        internal static void CloseRefuel()
        {
            if (refuelWindow != null)
            {
                UnityEngine.Object.Destroy(refuelWindow);
                refuelWindow = null;
                MFLog.Info(LogCat.Trade,"[Space] 关闭加油窗口");
            }
            refuelStation = null;
            if (!AnyOpen())
            {
                UiModalBlocker.Close();
            }
        }

        internal static void ToggleRefuel(SpaceStation station, Clustercraft craft = null,
            bool oxidizerMode = false, string oxidizerTag = null)
        {
            if (station == null)
            {
                return;
            }
            if (refuelWindow != null && refuelStation == station.stationId)
            {
                RefuelScreenState cur = refuelWindow.GetComponent<RefuelScreenState>();
                bool sameTag = !oxidizerMode || (cur != null && cur.oxidizerTag.IsValid
                    && cur.oxidizerTag.Name == oxidizerTag);
                if (cur != null && cur.oxidizerMode == oxidizerMode && sameTag)
                {
                    CloseRefuel();
                    return;
                }
            }
            if (!RefuelConfigured(station))
            {
                Notify("无法加油", "本站没有加油配置：请补齐 trade/fuel 下的燃料价目");
                return;
            }
            string reason;
            if (!RefuelScreen.Ready(station, out reason, craft))
            {
                Notify("无法加油", reason);
                return;
            }
            CloseWindow();
            CloseRemote();
            CloseRefuel();
            RefuelScreen.Build(station, ref refuelWindow, craft, oxidizerMode, oxidizerTag);
            refuelStation = station.stationId;
        }

        internal static void CloseWindow()
        {
            if (window != null)
            {
                UnityEngine.Object.Destroy(window);
                window = null;
                windowStation = null;
            }
            if (!AnyOpen())
            {
                UiModalBlocker.Close();
            }
        }

        internal static void CloseRemote()
        {
            if (remote != null)
            {
                UnityEngine.Object.Destroy(remote);
                remote = null;
            }
            remoteStation = null;
            remoteDonate = false;
            if (!AnyOpen())
            {
                UiModalBlocker.Close();
            }
        }

        internal static void HideBar()
        {
            barStation = null;
            if (bar != null)
            {
                UnityEngine.Object.Destroy(bar);
                bar = null;
            }
        }

        // 交易/加油的可用态只在显示按钮排前现算、点击时再校验，不做事件驱动刷新（见 交易.md P-49）

        /// <summary>★ 统一隐藏底部所有条（问题7/8）：TradeUi 自己的 + 战斗按钮排，
        internal static void HideAllBars()
        {
            HideBar();
            try
            {
                CombatUi.HideBar();
            }
            catch (Exception)
            {
                // expected: 战斗按钮排可能未创建，隐藏失败不影响交易条隐藏
            }
        }

        internal static void ShowBar(SpaceStation station)
        {
            HideAllBars();   // ★ 统一隐藏（问题7/8）
            if (station == null)
            {
                return;
            }
            Transform parent = OverlayParent();
            if (parent == null)
            {
                Debug.LogWarning("[Space] 找不到 overlay 父节点，底部按钮排无法显示");
                return;
            }
            // 按钮排对齐火箭底部按钮排 —— 取火箭"中间三个按钮"为基准：
            SpaceStation st = station;
            // ★ 显示前现算一次（不依赖任何位置事件）：此刻的按钮状态必然是最新的
            bool atStation = TradeUtil.RocketAtStation(st);
            if (atStation)
            {
                Clustercraft docked = TradeUtil.DockedCraftAt(st);
                MFLog.Info(LogCat.Trade, "[Space] 站点按钮排：火箭已停靠 " + st.StationName
                    + "（火箭 " + (docked != null ? SpaceCfg.Pos(docked.Location) : "?")
                    + " / 站点 " + SpaceCfg.Pos(st.Location) + "）");
            }
            barStation = st;
            bool hunter = st.Def != null && st.Def.id == TradeDb.Config.hunter_station_id;
            bool sandbox = SandboxDebugButtons.Visible;
            bool refuel = RefuelConfigured(st);
            // 选中实体时只判「配置 + 火箭到站」（复用上面已算好的 atStation），引擎/容量的完整判定留给点击时，不在按钮排构建里遍历模块
            bool refuelReady = refuel && atStation;
            const float bw = 110f;
            const float gap = 10f;
            const float step = bw + gap;   // 120
            float barH = CombatDb.Shared.uiBarHeight;
            int count = 3 + (refuel ? 1 : 0) + (hunter ? 2 : 0) + (sandbox ? 3 : 0);   // 交易/加油/查看收购项/宣战 (+捐款/查看捐款项) (+调试三项)
            float totalW = count * bw + (count - 1) * gap;

            bar = Node("SpaceTradeBar", parent, 0f, 0f, 10f, 10f);
            RectTransform rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, CombatDb.Shared.uiBarY);
            rt.sizeDelta = new Vector2(totalW, barH);
            bar.transform.SetAsLastSibling();   // ★ 层级置顶（问题7/8）

            float x = 0f;
            Color tradeFill = atStation ? Panel : PanelAlt;
            Color tradeEdge = atStation ? Accent : TextOff;
            Color tradeText = atStation ? TextMain : TextOff;
            Button tradeButton = MakeButton("交 易", bar.transform, x, 0f, bw, barH, tradeFill, tradeEdge, tradeText,
                delegate
                {
                    // ★ 点击时快速校对（不信按钮颜色）：确认火箭确实停靠本站，再开窗
                    if (TradeUtil.DockedCraftAt(st) == null)
                    {
                        ShowBar(st);   // 期间状态变了 → 重建并置灰，不做就地改色
                        return;
                    }
                    OpenTrade(st, false);
                });
            if (tradeButton != null)
            {
                tradeButton.interactable = atStation;   // 未停靠 = 灰且不可点（不是点了才提示）
            }
            x += step;
            if (refuel)
            {
                Button refuelButton = MakeButton("加 油", bar.transform, x, 0f, bw, barH,
                    refuelReady ? tradeFill : PanelAlt, refuelReady ? tradeEdge : TextOff,
                    refuelReady ? tradeText : TextOff,
                    delegate
                    {
                        if (TradeUtil.DockedCraftAt(st) == null)
                        {
                            ShowBar(st);
                            return;
                        }
                        ToggleRefuel(st);
                    });
                if (refuelButton != null)
                {
                    refuelButton.interactable = refuelReady;
                }
                x += step;
            }
            MakeButton("查看收购项", bar.transform, x, 0f, bw, barH, Panel, Border, TextMain,
                delegate { ToggleRemote(st); });
            x += step;
            // ★ 捐款相关只给美食猎人协会：按钮排 = 交易 / 查看收购项 / 捐款 / 查看捐款项 / 宣战
            if (hunter)
            {
                Button donateButton = MakeButton("捐 款", bar.transform, x, 0f, bw, barH, tradeFill, tradeEdge, tradeText,
                    delegate
                    {
                        if (TradeUtil.DockedCraftAt(st) == null)
                        {
                            ShowBar(st);
                            return;
                        }
                        OpenDonate(st);
                    });
                if (donateButton != null)
                {
                    donateButton.interactable = atStation;
                }
                x += step;
                MakeButton("查看捐款项", bar.transform, x, 0f, bw, barH, Panel, Border, TextMain,
                    delegate { ToggleDonateRemote(st); });
                x += step;
            }
            MakeButton("宣 战", bar.transform, x, 0f, bw, barH, PanelAlt, TextOff, TextOff,
                delegate { Notify("未开放", "宣战暂未开放"); });
            x += step;
            // 沙盒模式下额外提供「调试：刷新货物」——显示/隐藏跟随沙盒开关
            if (sandbox)
            {
                MakeButton("调试：刷新货物", bar.transform, x, 0f, bw, barH, Panel, Border, TextMain,
                    delegate
                    {
                        try
                        {
                            StationDef d = st.Def;
                            if (d == null)
                            {
                                d = new StationDef();
                                d.id = st.Key;
                                d.name = st.StationName;
                            }
                            TradeRegistry.Refresh(TradeRegistry.EnsureState(st.Key), d);
                            Notify("调试", "已刷新 " + st.StationName + " 的货物");
                        }
                        catch (Exception e)
                        {
                            MFLog.Exc(LogCat.Trade, e, "[Space] 刷新货物失败");
                        }
                    });
                x += step;
                MakeButton("调试：声望+50", bar.transform, x, 0f, bw, barH, Panel, Border, TextMain,
                    delegate
                    {
                        try
                        {
                            TradeRegistry.AddRep(50);
                            if (TradeSaveData.Attach() != null)
                            {
                                TradeSaveData.Attach()?.Capture();
                            }
                            Notify("调试", "协会声望 +50（当前 " + TradeRegistry.Rep + " · "
                                + TradeRegistry.RankName() + "）");
                        }
                        catch (Exception e)
                        {
                            MFLog.Exc(LogCat.Trade, e, "[Space] 声望调试失败");
                        }
                    });
                x += step;
                MakeButton("调试：星币+1万", bar.transform, x, 0f, bw, barH, Panel, Border, TextMain,
                    delegate
                    {
                        try
                        {
                            TradeRegistry.AddCurrency(10000f);
                            if (TradeSaveData.Attach() != null)
                            {
                                TradeSaveData.Attach()?.Capture();
                            }
                            Notify("调试", "星币 +10000（当前 " + TradeRegistry.Currency.ToString("0.00") + "）");
                        }
                        catch (Exception e)
                        {
                            MFLog.Exc(LogCat.Trade, e, "[Space] 星币调试失败");
                        }
                    });
                x += step;
            }
        }

        // ShowMonsterBar 已删除（K12/K15：怪物不出按钮排；攻击一律从火箭侧发起，战斗按钮排见 CombatUi）

        internal static void ShowQuestBar(SpaceQuestSite site)
        {
            HideAllBars();   // ★ 统一隐藏（问题7/8）
            if (site == null)
            {
                return;
            }
            Transform parent = OverlayParent();
            if (parent == null)
            {
                return;
            }
            bar = Node("SpaceQuestBar", parent, 0f, 0f, 10f, 10f);
            RectTransform rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 48f);
            rt.sizeDelta = new Vector2(600f, 48f);
            bar.transform.SetAsLastSibling();   // ★ 层级置顶（问题7/8）
            // 任务点实体随建图链停用而不再生成；此条只保留给旧档里残存的任务点用
            Text("info", bar.transform, 0f, 0f, 300f, 48f,
                site.Name + "  " + SpaceCfg.Pos(site.Location), 14f, Accent, false);
            MakeButton("派遣火箭前往", bar.transform, 310f, 0f, 180f, 48f, Panel, Accent, TextMain,
                delegate { SpaceQuestRuntime.SendCraftTo(site); });
            MakeButton("说明", bar.transform, 500f, 0f, 100f, 48f, PanelAlt, Border, TextDim,
                delegate
                {
                    Notify("任务地点", "火箭与地点重合 → 自动建图并降落；或停在相邻格 → 投放侦察仓/先锋仓");
                });
        }

        internal static void OpenTrade(SpaceStation station, bool remoteOnly)
        {
            if (station == null)
            {
                return;
            }
            if (remoteOnly)
            {
                ToggleRemote(station);   // ★ 再点一次就关掉（用户实测：以前点第二次关不掉）
                return;
            }
            if (window != null && windowStation == station.stationId && windowMode == 0)
            {
                CloseWindow();
                return;
            }
            CloseWindow();
            CloseRemote();
            CloseRefuel();
            TradeScreen.Build(station, ref window, false);
            windowStation = station.stationId;
            windowMode = 0;
        }

        // ★ 捐款窗：只有美食猎人协会才有入口（调用点已判，这里再兜一层）
        internal static void OpenDonate(SpaceStation station)
        {
            if (station == null)
            {
                return;
            }
            StationDef def = station.Def;
            if (def == null || def.id != TradeDb.Config.hunter_station_id)
            {
                return;
            }
            if (window != null && windowStation == station.stationId && windowMode == 1)
            {
                CloseWindow();
                return;
            }
            CloseWindow();
            CloseRemote();
            CloseRefuel();
            TradeScreen.Build(station, ref window, true);
            windowStation = station.stationId;
            windowMode = 1;
        }

        internal static void ToggleRemote(SpaceStation station)
        {
            if (station == null)
            {
                return;
            }
            if (remote != null && remoteStation == station.stationId && !remoteDonate)
            {
                CloseRemote();
                return;
            }
            OpenRemote(station);
        }

        // ★ 远程"查看捐款项"：只读列出可捐内容（不消耗、不给荣誉点）
        internal static void ToggleDonateRemote(SpaceStation station)
        {
            if (station == null)
            {
                return;
            }
            if (remote != null && remoteStation == station.stationId && remoteDonate)
            {
                CloseRemote();
                return;
            }
            CloseWindow();
            CloseRemote();
            CloseRefuel();
            TradeScreen.BuildRemote(station, ref remote, true);
            remoteStation = station.stationId;
            remoteDonate = true;
        }

        internal static void OpenRemote(SpaceStation station)
        {
            CloseWindow();
            CloseRemote();
            CloseRefuel();
            TradeScreen.BuildRemote(station, ref remote, false);
            remoteStation = station.stationId;
            remoteDonate = false;
        }

        internal sealed class Stepper : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
            UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            internal int dir = 1;
            internal System.Action<int> onStep;
            private bool held;
            private float nextStepAt;

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
            {
                NumberBox.CommitActive();   // 点加减前先提交正在编辑的数字框，避免两处同时改同一行
                held = true;
                Step();
                nextStepAt = Time.unscaledTime + 0.4f;
            }

            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e)
            {
                held = false;
            }

            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
            {
                held = false;
            }

            private void Update()
            {
                try
                {
                if (!held)
                {
                    return;
                }
                if (Time.unscaledTime >= nextStepAt)
                {
                    Step();
                    nextStepAt = Time.unscaledTime + 0.06f;
                }
                }
                catch (System.Exception e) { MFLog.HotFail("update:TradeUi", e); }
            }

            internal void Step()
            {
                if (onStep == null)
                {
                    return;
                }
                int mult = 1;
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)
                    || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                {
                    mult = 100;
                }
                else if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl)
                    || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl))
                {
                    mult = 10;
                }
                onStep(dir * mult);
            }

            private IEnumerator Hold()
            {
                Step();
                yield return new WaitForSecondsRealtime(0.4f);
                while (held)
                {
                    Step();
                    yield return new WaitForSecondsRealtime(0.06f);
                }
            }
        }

        private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Color> iconTintCache = new Dictionary<string, Color>();
        // 交易特殊行 → 承载 ui 图标的物品 prefab id
        private static readonly Dictionary<string, string> specialIconItems =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "__fuel", "Fuel" },
                { "__money", "HonorPoint" },
            };

        internal static Sprite ItemSprite(string itemId, out Color tint, bool quiet = false)
        {
            tint = Color.white;
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }
            string mapped;
            if (specialIconItems.TryGetValue(itemId, out mapped))
            {
                itemId = mapped;
            }
            if (itemId.StartsWith("__"))
            {
                return null;
            }
            Sprite cached;
            if (iconCache.TryGetValue(itemId, out cached))
            {
                Color cachedTint;
                tint = iconTintCache.TryGetValue(itemId, out cachedTint) ? cachedTint : Color.white;
                return cached;
            }
            Sprite sp = null;
            Color found = Color.white;
            Sprite pure = IngredientConfigs.IconOnlySprite(itemId);
            if (pure != null)
            {
                iconCache[itemId] = pure;
                iconTintCache[itemId] = Color.white;
                return pure;
            }
            try
            {
                Tuple<Sprite, Color> ui = Def.GetUISprite(itemId, "ui", false);
                if (ui != null && ui.first != null && !IsUnknownSprite(ui.first))
                {
                    sp = ui.first;
                    found = ui.second;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Trade, e, "[Space] 图标解析异常 " + itemId);
            }
            if (sp == null)
            {
                try
                {
                    string r = UiIconResolver.Resolve(itemId);
                    if (!string.IsNullOrEmpty(r))
                    {
                        sp = Assets.GetSprite(r);
                    }
                }
                catch (Exception)
                {
                    // expected: 图标解析失败按取不到图处理，走占位与告警
                }
            }
            if (sp == null && !quiet)
            {
                Debug.LogWarning("[Space] 图标取不到真图（原版只给了未知占位或没有贴图）：" + itemId);
            }
            iconCache[itemId] = sp;
            iconTintCache[itemId] = found;
            tint = found;
            return sp;
        }

        private static bool IsUnknownSprite(Sprite s)
        {
            if (s == null)
            {
                return true;
            }
            if (!string.IsNullOrEmpty(s.name) && s.name.StartsWith("unknown"))
            {
                return true;
            }
            Sprite unknown = Assets.GetSprite("unknown");
            return unknown != null && s == unknown;
        }

        private static Sprite TrySprite(string name, Type[] sig, object[] args)
        {
            try
            {
                System.Reflection.MethodInfo m = typeof(Def).GetMethod(name, sig);
                if (m == null)
                {
                    return null;
                }
                return m.Invoke(null, args) as Sprite;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal sealed class NumberBox : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler,
            UnityEngine.EventSystems.IDragHandler
        {
            private static NumberBox activeBox;
            internal System.Action<int> onCommit;
            internal TextMeshProUGUI label;
            internal int value;
            internal float min;
            internal float max;
            private bool editing;
            private string buf = "";
            private float dragAcc;

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
            {
                if (activeBox == this && editing)
                {
                    return;   // 已在编辑：重复点自己不清空已输入内容
                }
                if (activeBox != null && activeBox != this)
                {
                    activeBox.Commit();   // 点另一个框 = 上一个直接落地（不是丢弃）
                }
                activeBox = this;
                editing = true;
                buf = "";
                if (label != null)
                {
                    label.text = CursorOn() ? "|" : "";
                    label.color = Accent;
                }
            }

            private void OnDestroy()
            {
                if (activeBox == this)
                {
                    activeBox = null;
                }
            }

            internal static void CommitActive()
            {
                if (activeBox != null)
                {
                    activeBox.Commit();
                }
            }

            // ★ 丢弃当前输入态（不写回数值）：结算收尾时调用，避免下一次确认读到上一次的旧文本
            internal static void DropActive()
            {
                if (activeBox != null)
                {
                    activeBox.Discard();
                }
            }

            private void Discard()
            {
                editing = false;
                buf = "";
                if (label != null)
                {
                    label.text = value.ToString("0");
                }
                if (activeBox == this)
                {
                    activeBox = null;
                }
            }

            // 数字框正处于输入态（调试快捷键守卫读它：退格同时绑定了 Action.DebugToggle）
            internal static bool IsEditing
            {
                get { return activeBox != null && activeBox.editing; }
            }

            private void CancelEdit()
            {
                editing = false;
                buf = "";
                if (label != null)
                {
                    label.text = value.ToString("0");
                }
                if (onCommit != null)
                {
                    onCommit(value);   // 恢复行颜色与结算预览
                }
                if (activeBox == this)
                {
                    activeBox = null;
                }
            }

            public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                if (editing)
                {
                    return;   // 编辑态下拖动不改数值，避免边打字边跳
                }
                dragAcc += e.delta.x;
                while (dragAcc >= 6f)
                {
                    dragAcc -= 6f;
                    Apply(Step() * 1);
                }
                while (dragAcc <= -6f)
                {
                    dragAcc += 6f;
                    Apply(-Step() * 1);
                }
            }

            private static int Digits(string s)
            {
                return s.Length > 0 && s[0] == '-' ? s.Length - 1 : s.Length;   // 计数不含负号
            }

            // 上下界取「不越界」方向取整：下界向上、上界向下，避免 120.4 可卖却钳到 121
            private int Lo() { return Mathf.CeilToInt(min); }
            private int Hi() { return Mathf.FloorToInt(max); }

            // ★ 修订：区间必须有效（lo ≤ hi ≤ …）。库存/收购量/持有量异常（NaN、负值、
            //   上下界交叉）时，旧实现会让 Clamp 结果落在非法一侧 —— 这正是"持有 0 却能卖出"的入口。
            private void Range(out int lo, out int hi)
            {
                lo = Lo();
                hi = Hi();
                if (float.IsNaN(min) || float.IsNaN(max))
                {
                    lo = 0;
                    hi = 0;
                    return;
                }
                if (hi < lo)
                {
                    // 交叉区间 ⇒ 本行不可交易，强制 0（不允许任何输入逃逸到结算）
                    lo = 0;
                    hi = 0;
                }
            }

            private static int Step()
            {
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)
                    || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift))
                {
                    return 100;
                }
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl)
                    || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl))
                {
                    return 10;
                }
                return 1;
            }

            private void Apply(int delta)
            {
                int lo, hi;
                Range(out lo, out hi);
                int v = Mathf.Clamp(value + delta, lo, hi);
                if (v == value)
                {
                    return;
                }
                value = v;
                if (onCommit != null)
                {
                    onCommit(v);
                }
            }

            private void Update()
            {
                try
                {
                if (!editing)
                {
                    return;
                }
                // 点框外（其它框/±/确认/面板空白）→ 先把输入落地，随后控件读到的就是新值
                if (UnityEngine.Input.GetMouseButtonDown(0) && !PointerInside())
                {
                    Commit();
                    return;
                }
                string s = UnityEngine.Input.inputString;
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (c >= '0' && c <= '9')
                    {
                        if (Digits(buf) < 6)
                        {
                            buf += c;
                        }
                    }
                    else if (c == '-' && buf.Length == 0)
                    {
                        buf += c;   // 负号只在首位：数值框语义是「正 = 买入 / 负 = 卖出」
                    }
                    else if (c == '\b')
                    {
                        if (buf.Length > 0)
                        {
                            buf = buf.Substring(0, buf.Length - 1);
                        }
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        Commit();
                        return;
                    }
                }
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
                {
                    CancelEdit();
                    return;
                }
                if (label != null)
                {
                    label.text = CursorOn() ? buf + "|" : buf;   // 光标闪烁：与普通文本输入一致
                }
                }
                catch (System.Exception e) { MFLog.HotFail("update:TradeUi", e); }
            }

            private static bool CursorOn()
            {
                return Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
            }

            private bool PointerInside()
            {
                Canvas c = GetComponentInParent<Canvas>();
                Camera cam = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay ? c.worldCamera : null;
                return RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)transform, UnityEngine.Input.mousePosition, cam);
            }

            private void Commit()
            {
                editing = false;
                int v = 0;
                bool parsed = buf.Length > 0 && int.TryParse(buf, out v);
                if (parsed)
                {
                    int lo, hi;
                    Range(out lo, out hi);
                    int clamped = Mathf.Clamp(v, lo, hi);
                    if (clamped != value)
                    {
                        value = clamped;
                    }
                }
                if (onCommit != null)
                {
                    onCommit(value);   // RefreshRowVisual 回写文本与颜色，钳制结果同步到结算预览
                }
                if (label != null)
                {
                    label.text = value.ToString("0");
                }
                if (activeBox == this)
                {
                    activeBox = null;
                }
                buf = "";
            }
        }

        internal sealed class Shaker : KMonoBehaviour
        {
            internal RectTransform target;
            internal TextMeshProUGUI label;
            internal Color baseColor;
            private float until;
            private Vector2 origin;

            internal void Begin(float seconds)
            {
                if (target != null)
                {
                    origin = target.anchoredPosition;
                }
                if (label != null)
                {
                    baseColor = label.color;
                    label.color = Neg;
                }
                until = Time.unscaledTime + seconds;
            }

            private void Update()
            {
                try
                {
                if (target == null)
                {
                    return;
                }
                if (Time.unscaledTime >= until)
                {
                    target.anchoredPosition = origin;
                    if (label != null)
                    {
                        label.color = baseColor;
                    }
                    UnityEngine.Object.Destroy(this);
                    return;
                }
                float t = (until - Time.unscaledTime) * 12f;
                target.anchoredPosition = origin + new Vector2(Mathf.Sin(t) * 6f, 0f);
                }
                catch (System.Exception e) { MFLog.HotFail("update:TradeUi", e); }
            }
        }
    }

    internal static class TradeUtil
    {
        internal static Clustercraft FindCraftAt(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            List<ClusterGridEntity> list;
            if (grid == null || !grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return null;
            }
            for (int i = 0; i < list.Count; i++)
            {
                Clustercraft c = list[i] as Clustercraft;
                // ★ 修订（交易误亮）：cellContents 可能残留已飞走/已降落的火箭条目（按位置变更事件
                //   增减，事件漏一次就会留一条永不清理的陈旧记录），旧实现只看类型不看位置，
                //   于是"幽灵火箭"让远处站点也判定为可交易/可加油。
                //   这里必须回验实体当前真实位置（与 CombatRuntime.FindCraftAt 口径一致），
                //   并排除已销毁/未激活对象；纯字段比较，无分配、无遍历放大。
                if (c == null || c.Location != cell)
                {
                    continue;
                }
                if (c.gameObject == null || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }
                return c;
            }
            return null;
        }

        internal static Clustercraft NearestCraft(AxialI cell, int maxDistance)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return null;
            }
            Clustercraft best = null;
            int bestD = int.MaxValue;
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                if (kv.Value == null)
                {
                    continue;
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    Clustercraft c = kv.Value[i] as Clustercraft;
                    if (c == null)
                    {
                        continue;
                    }
                    int d = SpaceCfg.Distance(c.Location, cell);
                    if (d <= maxDistance && d < bestD)
                    {
                        bestD = d;
                        best = c;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// ★ 设计变更：所有「能不能交易/加油」的判定只在两处发生 ——
        /// ① 显示按钮排之前（ShowBar / ShowCraftBar）现算一次；② 点击按钮时再校验一次。
        /// 不再依赖任何位置变更事件，因此不会出现"某个事件漏了就永久显示错"的情况。
        /// </summary>
        internal static bool RocketAtStation(SpaceStation station)
        {
            return DockedCraftAt(station) != null;
        }

        /// <summary>火箭是否在星图中：必须是 Clustercraft 且状态为 InFlight（Grounded/Launching/Landing 都不算）。</summary>
        internal static bool InStarmap(Clustercraft c)
        {
            if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
            {
                return false;
            }
            // Grounded = 已落到星球表面（在星球内部世界），此时不能与轨道站交易
            return c.Status == Clustercraft.CraftStatus.InFlight;
        }

        /// <summary>点击/显示时的权威判定：返回真正停靠在本站的火箭，没有则 null。
        /// 判据只有两条：① 火箭在星图中且已停止飞行；② 与本站在六角格上距离 ≤ 1。</summary>
        internal static Clustercraft DockedCraftAt(SpaceStation station)
        {
            if (station == null)
            {
                return null;
            }
            Clustercraft c = CraftNear(station.Location);
            if (c == null)
            {
                return null;
            }
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid != null && !grid.IsInRange(c.Location, station.Location, 1))
            {
                // 走不到这里就该已经过滤掉；出现即说明索引有陈旧条目，记录一次便于定位
                MFLog.Warn(LogCat.Trade, "[Space] 停靠校验失败（超出 1 格）：火箭 "
                    + SpaceCfg.Pos(c.Location) + " / 站点 " + SpaceCfg.Pos(station.Location));
                return null;
            }
            return c;
        }

        /// <summary>同格或相邻一格上的、处于停靠状态的火箭 —— 所有"能否交易"判定的唯一几何入口。
        /// 用原版 AxialUtil.GetRing 取邻格，逐个回验：状态必须在星图中、未在飞行、且距离 ≤ 1。</summary>
        internal static Clustercraft CraftNear(AxialI cell)
        {
            Clustercraft c = FindCraftAt(cell);
            if (IsDocked(c))
            {
                return c;
            }
            try
            {
                List<AxialI> ring = AxialUtil.GetRing(cell, 1);
                if (ring != null)
                {
                    ClusterGrid grid = ClusterGrid.Instance;
                    for (int i = 0; i < ring.Count; i++)
                    {
                        c = FindCraftAt(ring[i]);
                        if (!IsDocked(c))
                        {
                            continue;
                        }
                        // 再核一次距离，防止 cellContents 里的陈旧条目把远处火箭算成邻居
                        if (grid != null && !grid.IsInRange(c.Location, cell, 1))
                        {
                            continue;
                        }
                        return c;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("trade:CraftNear", e);
            }
            return null;
        }

        // 停靠 = 在星图中（InFlight）+ 存在、激活、不在飞行途中
        private static bool IsDocked(Clustercraft c)
        {
            if (!InStarmap(c))
            {
                return false;
            }
            try
            {
                ClusterTraveler tr = c.GetComponent<ClusterTraveler>();
                if (tr != null && tr.IsTraveling())
                {
                    return false;
                }
            }
            catch (Exception)
            {
                // expected: 状态机尚未建立时视为非飞行
            }
            return true;
        }

        internal static string DisplayName(string itemId)
        {
            GameObject prefab = Assets.TryGetPrefab(itemId);
            if (prefab != null)
            {
                KPrefabID pid = prefab.GetComponent<KPrefabID>();
                if (pid != null)
                {
                    string n = pid.GetProperName();
                    if (!string.IsNullOrEmpty(n))
                    {
                        return n;
                    }
                }
            }
            return itemId;
        }
    }
}
