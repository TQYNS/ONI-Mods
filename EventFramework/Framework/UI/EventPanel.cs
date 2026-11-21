// EventPanel.cs — 事件框架 · 【单事件详情弹窗】

using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EventFramework
{
    public class EventPanel : KScreen
    {
        // 当前展示的事件实例
        private EventInstance _inst;
        // 该事件的定义（模板）
        private EventDef _def;

        // 当前打开的实例（单例；null = 无面板）
        private static EventPanel _current;
        // 界面是否已构建（懒构建）
        private bool _built;

        internal static bool IsAnyOpen
        {
            get { return _current != null; }
        }

        // 驻留超时的强制弹窗：玩家关掉窗口却仍没选 ⇒ 走保底默认选项
        private bool _forced;

        // 布局常量（宽度固定；高度由内容算出）
        private const float WIN_W = 810f;    // 窗口宽（固定）
        private const float PAD = 30f;       // 左右内边距
        private const float HEADER_H = 100f; // 标题条高
        private const float ACCENT_W = 6f;   // 标题条左侧金条宽
        private const float X_S = 54f;       // 关闭菱形边长（外圈）
        private const float OPT_H = 62f;     // 单个选项行高
        private const float OPT_GAP = 11f;   // 选项行间距
        private const float MIN_H = 320f;    // 窗口最小高（内容少也不塌）
        private const float BRK = 26f;       // 四角角标臂长
        private const float BRK_T = 2f;      // 四角角标粗细
        private const float BRK_IN = 10f;    // 四角角标内缩

        // 信息栏（重做）
        private const float INFO_ROW_H = 26f;   // 每行（提交 / 获得）高度
        private const float INFO_PAD_V = 6f;    // 上下内边距
        private const float INFO_LABEL_W = 48f;   // 「提交 / 获得」标签列宽
        private const float INFO_ICON = 16f;      // ★v3.6 物品图标尺寸
        private const float INFO_ICON_GAP = 4f;   // 图标与名称间距
        private const float INFO_TOKEN_GAP = 14f; // 相邻物品（图标+名称）之间的间距

        // 信息栏文本可用宽度（算高度与绘制共用，必须一致）
        private const float INFO_TEXT_W = WIN_W - 2f * PAD - 34f - INFO_PAD_R;
        private const float INFO_PAD_R = 20f;   // 右侧留白

        // 字号
        private const float FS_TITLE = 25f;
        // 副标题字号
        private const float FS_SUB = 17f;
        // 正文字号
        private const float FS_BODY = 20f;
        // 小节标题字号
        private const float FS_SECT = 17f;
        // 选项文案字号
        private const float FS_OPT = 22f;
        private const float FS_INFO = 15f;   // ★v3.5 信息栏字号
        // 关闭按钮字号
        private const float FS_X = 26f;

        // 配色（暗夜空 + 金）
        private static readonly Color C_Glow = new Color(0.275f, 0.322f, 0.541f, 0.30f); // #46528A
        private static readonly Color C_Border = new Color(0.275f, 0.549f, 0.627f, 0.85f);
        private static readonly Color C_BodyTop = new Color(0.106f, 0.165f, 0.278f, 1.00f);
        private static readonly Color C_BodyBot = new Color(0.106f, 0.165f, 0.278f, 1.00f);
        private static readonly Color C_HeadTop = new Color(0.086f, 0.129f, 0.212f, 1.00f);
        private static readonly Color C_HeadBot = new Color(0.106f, 0.165f, 0.278f, 1.00f);
        private static readonly Color C_Gold = new Color(0.431f, 0.827f, 0.910f, 1.00f);
        private static readonly Color C_GoldDim = new Color(0.275f, 0.549f, 0.627f, 1.00f);
        private static readonly Color C_Accent = new Color(0.431f, 0.827f, 0.910f, 1.00f);
        private static readonly Color C_AccentDim = new Color(0.275f, 0.549f, 0.627f, 1.00f);
        private static readonly Color C_GoldAft = new Color(0.431f, 0.827f, 0.910f, 0.00f);
        private static readonly Color C_Txt = new Color(0.918f, 0.941f, 0.973f, 1.00f);
        private static readonly Color C_Dim = new Color(0.576f, 0.635f, 0.722f, 1.00f);
        private static readonly Color C_Sep = new Color(0.157f, 0.220f, 0.329f, 0.90f);
        private static readonly Color C_HiLight = new Color(1.000f, 1.000f, 1.000f, 0.07f);
        private static readonly Color C_RowFill = new Color(0.094f, 0.141f, 0.235f, 1.00f); // 选项行玻璃底(上)
        private static readonly Color C_RowFillBot = new Color(0.071f, 0.106f, 0.188f, 1.00f); // 选项行玻璃底(下)
        private static readonly Color C_OptTxt = new Color(1.000f, 1.000f, 1.000f, 1.00f); // 选项文字：纯白

 // 信息栏专用色
        private static readonly Color C_InfoBgTop = new Color(0.078f, 0.094f, 0.157f, 0.98f); // #141828
        private static readonly Color C_InfoBgBot = new Color(0.055f, 0.067f, 0.118f, 0.98f); // #0E111E

        private static readonly Color C_InfoSubmitLabel = new Color(0.671f, 0.510f, 0.235f, 1.00f); // #AB823C
        private static readonly Color C_InfoSubmitValue = new Color(0.878f, 0.741f, 0.471f, 1.00f); // #E0BD78
        private static readonly Color C_InfoRewardLabel = new Color(0.784f, 0.588f, 0.118f, 1.00f); // #C8961E
        private static readonly Color C_InfoRewardValue = new Color(1.000f, 0.925f, 0.596f, 1.00f); // #FFEC98

        // 选项行的点击索引（与 _optTexts 一一对应；跳过 null 项后不会错位）
        private readonly List<int> _optIds = new List<int>();
        private readonly List<string> _optTexts = new List<string>();

 // ：当前面板里"不可用"的选项（灰显 + 拒绝点击，见 §17.7）
        private readonly HashSet<int> _optDisabled = new HashSet<int>();

 // 提交物品 LocText 映射（选项索引 → [(itemId, LocText)])
        private readonly Dictionary<int, List<KeyValuePair<string, LocText>>> _submitItemLabels
            = new Dictionary<int, List<KeyValuePair<string, LocText>>>();

 // 面板队列：并发时多个事件同时请求弹窗 ⇒ 排队依次展示
        private sealed class QueuedPanel
        {
            // 排队等展示的实例
            public EventInstance inst;
            // 该面板是否强制弹出
            public bool forced;
        }

        private static readonly List<QueuedPanel> _queue = new List<QueuedPanel>();

        // 该实例是否已经在队列里等展示
        public static bool IsQueued(EventInstance inst)
        {
            if (inst == null) return false;
            for (int i = 0; i < _queue.Count; i++)
                if (_queue[i] != null && _queue[i].inst == inst) return true;
            return false;
        }

        public static int QueueLength { get { return _queue.Count; } }

        // 文本样式守卫：模板 LocText 会在自己的 Start 里回灌 textStyleSetting，
        private EventPanelStyleKeeper _style;

        // 打开面板。
        public static EventPanel ShowFor(EventInstance inst, bool forced = false)
        {
            if (inst == null) { Debug.LogWarning("[MoreFood/Event][UI/Panel] ShowFor(null)"); return null; }

            try
            {
                // ★ 只通知不弹窗：通知点击 / 历史点击 两条路径统一走 EventDef.AllowsPanel() 口径
                var def0 = inst.def ?? EventRegistry.Get(inst.templateId);
                if (!forced && def0 != null && !def0.AllowsPanel())
                {
                    Debug.Log("[MoreFood/Event][UI/Panel] '" + def0.id
                              + "' 无弹窗事件（show_panel=false / notify_only）→ 不弹窗");
                    return null;
                }
                if (_current != null && _current._inst == inst
                    && _current.gameObject.activeInHierarchy)
                {
                    if (forced) _current._forced = true;    // 已经开着 → 升级成强制模式
                    return _current;
                }

 // 并发：正在显示别的实例 ⇒ 入队（不抢占、不丢弃）
                if (_current != null && _current._inst != inst)
                {
                    if (IsQueued(inst))
                    {
                        for (int i = 0; i < _queue.Count; i++)
                            if (_queue[i] != null && _queue[i].inst == inst) _queue[i].forced |= forced;
                        Debug.Log("[MoreFood/Event][UI/Panel] 已在队列中 → 只升级 forced 标记: " + inst.templateId);
                        return null;
                    }

                    _queue.Add(new QueuedPanel { inst = inst, forced = forced });
                    Debug.Log("[MoreFood/Event][UI/Panel] 面板正忙 → 排队等展示（队列长度 "
                              + _queue.Count + "）: " + inst.templateId);
                    return null;
                }

                var parent = ResolveUiParent();
                if (parent == null)
                {
                    Debug.LogWarning("[MoreFood/Event][UI/Panel] 找不到 UI 父节点");
                    return null;
                }

                var go = new GameObject("MoreFood_EventPanel", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var panel = go.AddComponent<EventPanel>();
                panel._forced = forced;
                panel._inst = inst;
                panel._def = inst.def ?? EventRegistry.Get(inst.templateId);
                if (panel._def != null) inst.def = panel._def;

                // ★ 必须调 Activate() 而不是 PushScreen()：
                panel.Activate();

                _current = panel;
                Debug.Log("[MoreFood/Event][UI/Panel] 面板已打开: " + inst.templateId);
                return panel;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] ShowFor 异常: " + e);
                return null;
            }
        }

        // 关闭当前事件面板（取消 / 超时 / 背景点击都走这里，并记下原因）
        public static void CloseCurrent(string reason = null)
        {
            try
            {
                if (_current == null) return;
                var p = _current;
                _current = null;

                // ★ 强制弹窗被"关掉"而不是"选掉" ⇒ 走保底默认选项。
                if (p != null && p._forced && p._inst != null && !p._inst.ChoiceChosen)
                {
                    Debug.Log("[MoreFood/Event][UI/Panel] 强制弹窗被关闭且未选择 → 走保底默认选项");
                    try { EventSystem.ResolveByDefault(p._inst); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:CloseCurrent", "[Panel] CloseCurrent 失败 → 已跳过本步，流程继续", e); }
                }

                var sm = KScreenManager.Instance;
                if (sm != null) { try { sm.PopScreen(p); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:CloseCurrent#2", "[Panel] CloseCurrent 失败 → 已跳过本步，流程继续", e); } }
                try
                {
                    var es = UnityEngine.EventSystems.EventSystem.current;
                    if (es != null) es.SetSelectedGameObject(null);
                }
                catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:CloseCurrent#3", "[Panel] CloseCurrent 失败 → 已跳过本步，流程继续", e); }
                if (p != null && p.gameObject != null)
                {
                    try { p.Deactivate(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:CloseCurrent#4", "[Panel] CloseCurrent 失败 → 已跳过本步，流程继续", e); }
                    try { UnityEngine.Object.Destroy(p.gameObject); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:CloseCurrent#5", "[Panel] CloseCurrent 失败 → 已跳过本步，流程继续", e); }
                }
                Debug.Log("[MoreFood/Event][UI/Panel] 面板已关闭 reason=" + (reason ?? "-"));

 // 队列：关掉当前后自动显示下一个
                ShowNextQueued();
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][UI/Panel] CloseCurrent 异常: " + e); }
        }

        // 显示队列里的下一个（已结束的实例自动跳过；递归深度受队列长度限制）
        private static void ShowNextQueued()
        {
            try
            {
                while (_queue.Count > 0)
                {
                    var q = _queue[0];
                    _queue.RemoveAt(0);

                    if (q == null || q.inst == null) continue;
                    if (q.inst.state == EventInstanceState.Ended)
                    {
                        Debug.Log("[MoreFood/Event][UI/Panel] 队列里的实例已结束 → 跳过: " + q.inst.templateId);
                        continue;
                    }

                    Debug.Log("[MoreFood/Event][UI/Panel] 队列切换 → 显示下一个事件: " + q.inst.templateId);
                    ShowFor(q.inst, q.forced);
                    return;
                }
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][UI/Panel] ShowNextQueued 异常: " + e); }
        }

        // 清空队列（换局 / 重置时用）
        public static void ClearQueue()
        {
            _queue.Clear();
        }

        public static bool IsOpen { get { return _current != null; } }

        // 只读回顾（点「事件」屏里【已结束】的记录时用）
        private bool _review;
        // 回顾显示的标题
        private string _rName;
        // 回顾显示的周期
        private int _rCycle;
        // 回顾显示的结果文本
        private string _rOutcome;

        public static EventPanel ShowReview(string templateId, string name, int cycle,
                                            string outcome, int choiceIndex)
        {
            try
            {
                if (_current != null) CloseCurrent("换回顾");

                var parent = ResolveUiParent();
                if (parent == null) { Debug.LogWarning("[MoreFood/Event][UI/Panel] 找不到 UI 父节点"); return null; }

                var go = new GameObject("MoreFood_EventReview", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var panel = go.AddComponent<EventPanel>();
                panel._review = true;
                panel._inst = null;
                panel._def = EventRegistry.Get(templateId);
                panel._rName = string.IsNullOrEmpty(name) ? templateId : name;
                panel._rCycle = cycle;
                panel._rOutcome = outcome;
                // （choiceIndex 参数保留兼容调用方；"选了哪项"文案设计已废除，不再使用）

                panel.Activate();
                _current = panel;
                Debug.Log("[MoreFood/Event][UI/Panel] 回顾面板已打开: " + templateId
                          + "（" + (outcome ?? "-") + "）");
                return panel;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] ShowReview 异常: " + e);
                return null;
            }
        }

        private static Transform ResolveUiParent()
        {
            try
            {
                var gsm = GameScreenManager.Instance;
                if (gsm != null && gsm.ssOverlayCanvas != null) return gsm.ssOverlayCanvas.transform;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:ResolveUiParent", "[Panel] ResolveUiParent 失败 → 已跳过本步，流程继续", e); }
            try
            {
                var ws = WorldSelector.Instance;
                if (ws != null && ws.transform.parent != null && ws.transform.parent.parent != null)
                    return ws.transform.parent.parent;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:ResolveUiParent#2", "[Panel] ResolveUiParent 失败 → 已跳过本步，流程继续", e); }
            try { var sm = KScreenManager.Instance; if (sm != null) return sm.transform; } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:ResolveUiParent#3", "[Panel] ResolveUiParent 失败 → 已跳过本步，流程继续", e); }
            return null;
        }

        // ================================================================
        protected override void OnActivate()
        {
            try { if (!_built) { Build(); _built = true; } }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][UI/Panel] Build 异常: " + e); }
        }

        // 回调：OnDeactivate
        protected override void OnDeactivate()
        {
            try { EventNotifier.ReleasePause(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:OnDeactivate", "[Panel] OnDeactivate 失败 → 已跳过本步，流程继续", e); }
        }

        // 排版主流程：先量文字 → 再算总高 → 最后一次性摆位
        private void Build()
        {
            EnsureStyleKeeper();
            EnsureItemValidation();

            var rootRt = EventUiKit.RT(gameObject);
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            // ── 遮罩（吃点击，避免穿透到底下的游戏界面）──────────────
            var dim = EventUiKit.MakeSolid("Dim", transform, new Color(0f, 0f, 0f, 0.52f));
            EventUiKit.Stretch(dim.gameObject);
            dim.raycastTarget = false;
            var hit = EventUiKit.MakeSolid("BackdropHit", transform, new Color(0f, 0f, 0f, 0f));
            EventUiKit.Stretch(hit.gameObject);
            hit.raycastTarget = true;
            try { hit.rectTransform.offsetMax = new Vector2(0f, -96f); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:Build", "[Panel] Build 失败 → 已跳过本步，流程继续", e); }
            if (hit.gameObject.GetComponent<EventBackdropClose>() == null) hit.gameObject.AddComponent<EventBackdropClose>();

            // ── 窗口（尺寸最后补，先建出来当所有子节点的坐标原点）────
            var win = EventUiKit.NewRect("Window", transform);
            win.anchorMin = new Vector2(0.5f, 0.5f);
            win.anchorMax = new Vector2(0.5f, 0.5f);
            win.pivot = new Vector2(0.5f, 0.5f);
            win.anchoredPosition = Vector2.zero;
            win.sizeDelta = new Vector2(WIN_W, MIN_H);

            // 背景层：先建 → 排在子物体最前 → 画在最底层
            var glow = EventUiKit.MakeGradientV("Glow", win, C_Glow, new Color(C_Glow.r, C_Glow.g, C_Glow.b, 0.06f));
            var body = EventUiKit.MakeGradient("Body", win, C_BodyTop, C_BodyBot);
            body.raycastTarget = true;
            body.sprite = UiSkin.Rounded(10);
            body.type = Image.Type.Sliced;
            body.color = C_BodyTop;
            var bodyEdge = UiSkin.RoundOutline("BodyEdge", win, 10, 1, C_Border);
            EventUiKit.Stretch(bodyEdge.gameObject);

            // ── 文案 ──────────────────────────────────────────────────
            string title = ResolveTitle();
            string sub = ResolveSubtitle();
            string owner = _def != null ? _def.id : "panel";

            // ══ 标题条 ════════════════════════════════════════════════
            var headBg = EventUiKit.MakeGradient("HeaderBg", win, C_HeadTop, C_HeadBot);
            headBg.sprite = UiSkin.Rounded(10);
            headBg.type = Image.Type.Sliced;
            headBg.color = C_HeadBot;
            EventUiKit.Place(headBg.gameObject, ACCENT_W, 0f, WIN_W - ACCENT_W, HEADER_H);

            var accent = EventUiKit.MakeGradientV("Accent", win, C_Accent, C_AccentDim);
            EventUiKit.Place(accent.gameObject, 0f, 0f, ACCENT_W, HEADER_H);

            // 标题 + 副标题
            float textX = ACCENT_W + 22f;
            float textW = WIN_W - textX - (X_S * 0.85f + 46f);

            LocText titleTx;
            var titleRoot = EventUiKit.MakeFixedLabel("Title", win,
                                                      PanelTextRenderer.Render(title, owner),
                                                      C_Txt, FS_TITLE,
                                                      TextAlignmentOptions.Left, out titleTx);
            EventUiKit.FitLabel(titleRoot, titleTx, textX, 18f, textW, 34f);

            LocText subTx;
            var subRoot = EventUiKit.MakeFixedLabel("Subtitle", win, sub, C_Dim, FS_SUB,
                                                    TextAlignmentOptions.Left, out subTx);
            EventUiKit.FitLabel(subRoot, subTx, textX, 56f, textW, 24f);

            // 副标题（倒计时走字）需要每帧盯一下（见 TickSubtitle）
            _subTx = subTx;
            _subCache = sub;
            _subStateInit = (_inst != null);

            // 标题下分割线（细、低对比，避免金线割裂感）
            var rule = EventUiKit.MakeGradientH("TitleRule", win, C_Sep, C_GoldAft);
            EventUiKit.Place(rule.gameObject, textX, HEADER_H - 16f, textW * 0.72f, 1f);

            var sepHead = EventUiKit.MakeSolid("SepHeader", win, C_Sep);
            EventUiKit.Place(sepHead.gameObject, 0f, HEADER_H, WIN_W, 1f);

            // ══ 正文 ════════════════════════════════════════════════
            float y = HEADER_H + 1f + 26f;
            float bodyW = WIN_W - 2f * PAD;

            // ★ 强制弹窗提示条（驻留超时被强拉出来的那一次）
            if (_forced && !_review)
            {
                LocText warnTx;
                var warnRoot = EventUiKit.MakeFixedLabel("ForceWarn", win,
                    "驻留时间已到 —— 请做出选择；直接关闭将自动执行保底选项。",
                    C_Gold, FS_SUB, TextAlignmentOptions.Left, out warnTx);
                EventUiKit.FitLabel(warnRoot, warnTx, PAD, y, bodyW, 26f);
                if (warnTx != null) StyleText(warnTx, C_Gold, FS_SUB, TextAlignmentOptions.Left);
                y += 26f + 10f;
            }

            var blocks = BuildBodyBlocks();
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (b.gap > 0f) y += b.gap;

                LocText lt;
                var root = EventUiKit.MakeFixedLabel("Body" + i, win,
                                                     PanelTextRenderer.Render(b.text, owner),
                                                     b.color, b.size,
                                                     TextAlignmentOptions.TopLeft, out lt);
                float h = EventUiKit.MeasureHeight(lt, bodyW, b.size * 1.45f);
                EventUiKit.FitLabel(root, lt, PAD, y, bodyW, h);
                if (lt != null) StyleText(lt, b.color, b.size, TextAlignmentOptions.TopLeft);
                y += h;
            }
            y += 28f;

            // ══ 选项区 ══════════════════════════════════════════════
            CollectOptions();

            if (_optTexts.Count > 0)
            {
                var sepOpt = EventUiKit.MakeSolid("SepOptions", win, C_Sep);
                EventUiKit.Place(sepOpt.gameObject, PAD, y, bodyW, 1f);
                y += 1f + 18f;

                LocText secTx;
                var secRoot = EventUiKit.MakeFixedLabel("SectionTitle", win, "选择你的应对", C_Gold, FS_SECT,
                                                        TextAlignmentOptions.Left, out secTx);
                EventUiKit.FitLabel(secRoot, secTx, PAD, y, 240f, 24f);

                var secRule = EventUiKit.MakeGradientH("SectionRule", win, C_Sep, C_GoldAft);
                EventUiKit.Place(secRule.gameObject, PAD + 200f, y + 12f, bodyW - 200f, 1f);
                y += 24f + 16f;

                // ★ 选项行的可见外观【全部自绘】，原版按钮只当点击区。
                float[] optTotalH = new float[_optTexts.Count];
                for (int i = 0; i < _optTexts.Count; i++)
                {
                    int id = _optIds[i];
                    var opt = (id >= 0 && id < _def.options.Count) ? _def.options[id] : null;
                    optTotalH[i] = OPT_H + CalcInfoHeight(opt, owner);
                }

                float cy = y;
                for (int i = 0; i < _optTexts.Count; i++)
                {
                    int id = _optIds[i];
                    var opt = (id >= 0 && id < _def.options.Count) ? _def.options[id] : null;

                    float ry = cy;
                    float totalH = optTotalH[i];
                    bool hasInfo = totalH > OPT_H + 0.5f;

 // 先声明，后赋值：lambda 捕获的是变量本身，点击时读到的是最新值
                    EventRowShake shake = null;

                    // ① 点击区（文字留空，外观不用它的）
                    var btn = EventUiKit.MakeButton("Opt_" + id, win, "", () => OnClickOption(id, shake), OPT_H);
                    SkinButton(btn, PAD, ry, bodyW, OPT_H, FS_OPT, TextAlignmentOptions.Left, 0f);

                    // ② 行视觉：金色细边框 + 玻璃底
                    var frame = EventUiKit.MakeGradientV("RowFrame", win, C_AccentDim, C_AccentDim);
                    EventUiKit.Place(frame.gameObject, PAD, ry, bodyW, OPT_H);

                    var fill = EventUiKit.MakeGradientV("RowFill", win, C_RowFill, C_RowFillBot);
                    EventUiKit.Place(fill.gameObject, PAD + 1f, ry + 1f, bodyW - 2f, OPT_H - 2f);

                    var stripe = EventUiKit.MakeGradientV("Stripe", win, C_Accent, C_AccentDim);
                    EventUiKit.Place(stripe.gameObject, PAD, ry + (OPT_H - 28f) * 0.5f, 4f, 28f);

 // 捕获 Dot 变量，供抖动时一起位移
                    var dot = EventUiKit.MakeDiamond("Dot", win, PAD + bodyW - 30f, ry + OPT_H * 0.5f,
                                                     14f, C_Gold, null, 0f);

                    // ③ 文字最后建 → 画在最上层，颜色字号完全由我们定
                    LocText optLt;
                    var optRoot = EventUiKit.MakeFixedLabel("OptTx_" + id, win, _optTexts[i],
                                                            C_OptTxt, FS_OPT,
                                                            TextAlignmentOptions.Left, out optLt);
                    EventUiKit.FitLabel(optRoot, optLt, PAD + 34f, ry, bodyW - 34f - 64f, OPT_H);
                    if (optLt != null)
                    {
                        optLt.raycastTarget = false;   // 放行点击，落到下面的按钮上
                        StyleText(optLt, C_OptTxt, FS_OPT, TextAlignmentOptions.Left);
                    }

                    // ═══════════════════════════════════════════════════════
                    LocText[] infoLts = null;
                    if (hasInfo)
                    {
                        float infoH = totalH - OPT_H;
                        float infoY = ry + OPT_H;
                        List<KeyValuePair<string, LocText>> submitLabels = null;
                        BuildOptionInfo(win, opt, PAD, infoY, bodyW, infoH,
                                        out infoLts, out submitLabels);
                        if (submitLabels != null && submitLabels.Count > 0)
                            _submitItemLabels[id] = submitLabels;
                    }

                    // ④ 悬停 / 按下：文字变金 + 金条加宽 + 底色变亮 + 信息栏提亮
                    if (btn != null)
                    {
                        var hv = btn.gameObject.AddComponent<EventRowHover>();
                        hv.fill = fill;
                        hv.stripe = stripe;
                        hv.label = optLt;
                        hv.infoLabels = infoLts;
                        hv.CaptureBase();   // ★ 记录信息栏文字基础色，供悬停提亮

 // 提交不足时的"拒绝反馈"：整行抖动 + 材料文字染红
                        shake = btn.gameObject.AddComponent<EventRowShake>();
                        shake.targets = CollectShakeTargets(btn, frame, fill, stripe, dot, optRoot);
                    }

                    cy += totalH + OPT_GAP;
                }
                y = cy - OPT_GAP;   // 减掉最后一行的 GAP
            }
            // ★ 回顾模式不再画"（事件已结束 · 回顾模式，不可再选）"备注条

            // ══ 关闭：右上角圆角 ✕ 按钮 ══════════════════════════════
            float xCx = WIN_W - 44f;
            float xCy = 10f;

            var btnX = EventUiKit.MakeButton("BtnX", win, "", () => CloseCurrent("X"), 36f);
            SkinButton(btnX, xCx - 4f, xCy - 4f, 38f, 38f, FS_X, TextAlignmentOptions.Center, 0f);
            if (btnX != null)
            {
                var btnImg = btnX.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.sprite = null;
                    btnImg.color = new Color(1f, 1f, 1f, 0f);
                }
            }

            Image xHalo = UiSkin.RoundSolid("XHalo", win, 18, new Color(0.431f, 0.827f, 0.910f, 0.14f));
            EventUiKit.Place(xHalo.gameObject, WIN_W - 46f, 8f, 36f, 36f);

            LocText xTx;
            var xRoot = EventUiKit.MakeFixedLabel("XGlyph", win, "×", C_Dim, FS_X + 2f,
                                                  TextAlignmentOptions.Center, out xTx);
            EventUiKit.FitLabel(xRoot, xTx, WIN_W - 46f, 8f, 36f, 36f);
            if (xTx != null) xTx.raycastTarget = false;   // 必须放行，否则点不到底下的按钮

            if (btnX != null)
            {
                var xh = btnX.gameObject.AddComponent<EventXHover>();
                xh.halo = xHalo;
                xh.outer = xHalo;
                xh.glyph = xTx;
            }

            // ══ 窗口高度（内容实测 + 内边距，有下限 MIN_H）═════════
            float winH = Mathf.Max(MIN_H, y + PAD);

            // ══ 背景层 / 装饰层摆位（尺寸算完才摆）═══════════════════
            EventUiKit.Place(glow.gameObject, -8f, -8f, WIN_W + 16f, winH + 16f);
            EventUiKit.Place(body.gameObject, 0f, 0f, WIN_W, winH);

            var hi = EventUiKit.MakeSolid("HiLight", win, C_HiLight);
            EventUiKit.Place(hi.gameObject, 1f, 1f, WIN_W - 2f, 1f);

            MakeBorder(win, WIN_W, winH);

            win.sizeDelta = new Vector2(WIN_W, winH);

            // 全部文本的样式快照：MakeFixedLabel 已经设对了颜色/字号，
            SnapshotStyles();

            // ══ 整窗拖动 ══════════════════════════════════════════
            try
            {
                var drag = win.gameObject.AddComponent<EventPanelDrag>();
                drag.target = win;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] 拖动组件挂载失败: " + e);
            }

            Debug.Log("[MoreFood/Event][UI/Panel] 面板已构筑 " + WIN_W + "×" + winH
                      + " 标题=" + title + " 选项数=" + _optTexts.Count
                      + " 正文块=" + blocks.Count);
        }

        // 信息栏：内容自适应高度
        private void EnsureItemValidation()
        {
            if (_review || _def == null || _def.options == null) return;

            for (int i = 0; i < _def.options.Count; i++)
            {
                var o = _def.options[i];
                if (o == null) continue;

                string owner = _def.id + ".options[" + i + "]";
                ItemStackParser.EnsureValidated(o.submit_compiled, owner + ".submit_materials", o.Warning);
                ItemStackParser.EnsureValidated(o.reward_compiled, owner + ".grant_rewards", o.Warning);
            }
        }

        private float CalcInfoHeight(EventOption opt, string owner)
        {
            if (opt == null) return 0f;

            int rows = 0;
            try { rows = InfoRows(opt, INFO_TEXT_W); }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:CalcInfoHeight", "[Panel] CalcInfoHeight 失败 → 已按兜底处理", e); return 0f; }

            if (rows <= 0) return 0f;
            return rows * INFO_ROW_H + INFO_PAD_V * 2f;
        }

        // 干跑一遍布局，得到信息栏行数（与真正绘制共用同一套摆法）。
        private int InfoRows(EventOption opt, float textW)
        {
            if (opt == null) return 0;

            int rows = 0;
            rows += LayoutInfoGroup(null, 0f, 0f, textW, "提交", opt.submit_compiled, opt.submit_label,
                                    null, C_InfoSubmitLabel, C_InfoSubmitValue, "InfoSubmit",
                                    null, null, false);
            rows += LayoutInfoGroup(null, 0f, 0f, textW, "获得", opt.reward_compiled, opt.grant_label,
                                    opt.grant_icon, C_InfoRewardLabel, C_InfoRewardValue, "InfoReward",
                                    null, null, false);
            return rows;
        }

        // 信息栏布局：
        private int LayoutInfoGroup(RectTransform win, float x, float y, float w, string caption,
                                    ItemParseResult res, string customLabel, string customIcon,
                                    Color captionColor, Color valueColor, string namePrefix,
                                    List<LocText> labels,
                                    List<KeyValuePair<string, LocText>> itemLabels,
                                    bool draw)
        {
            // 奖励说明可引用刷新时冻结的实例参数；测高与实际绘制使用同一最终文本
            if (!string.IsNullOrEmpty(customLabel) && _inst != null && _inst.NoticeArgs != null)
            {
                foreach (KeyValuePair<string, string> arg in _inst.NoticeArgs)
                {
                    if (arg.Key != null) customLabel = customLabel.Replace("{" + arg.Key + "}", arg.Value ?? "");
                }
            }
            int count = res != null ? res.valid.Count : 0;
            bool displayOnly = count <= 0 && (res == null || res.totalCount <= 0)
                               && !string.IsNullOrEmpty(customLabel);
            if (count <= 0 && !displayOnly) return 0;

            string owner = _def != null ? _def.id : "panel";
            float startX = x + INFO_LABEL_W;
            float right = x + w;

            if (!string.IsNullOrEmpty(customLabel))
            {
                string text = PanelTextRenderer.Render(customLabel, owner);
                Sprite customSprite = null;
                Color customTint = Color.white;
                bool hasCustomIcon = !string.IsNullOrEmpty(customIcon)
                                     && ItemIconResolver.TryGet(customIcon, out customSprite, out customTint);
                float iconW = hasCustomIcon ? INFO_ICON + INFO_ICON_GAP : 0f;
                float textX = startX + iconW;
                float textW = Mathf.Max(16f, w - INFO_LABEL_W - iconW);
                int rows = Mathf.Max(1, Mathf.CeilToInt(MeasureWrapHeight(text, textW) / INFO_ROW_H));

                if (draw)
                {
                    DrawInfoCaption(win, x, y, caption, captionColor, namePrefix);
                    if (hasCustomIcon)
                    {
                        var img = EventUiKit.MakeSolid(namePrefix + "_CustomIco", win, customTint);
                        if (img != null)
                        {
                            img.sprite = customSprite;
                            img.type = Image.Type.Simple;
                            img.color = customTint;
                            img.raycastTarget = false;
                            EventUiKit.Place(img.gameObject, startX,
                                             y + (INFO_ROW_H - INFO_ICON) * 0.5f,
                                             INFO_ICON, INFO_ICON);
                        }
                    }

                    LocText itLt;
                    var itRoot = EventUiKit.MakeFixedLabel(namePrefix + "_Val", win, text,
                                                           valueColor, FS_INFO,
                                                           TextAlignmentOptions.Left, out itLt);
                    EventUiKit.FitLabel(itRoot, itLt, textX, y, textW, rows * INFO_ROW_H);
                    StyleInfoValue(itLt, valueColor, labels);

                    // 自定义文案：整块文本代表全部提交物品 → 特殊 key "*all*"
                    if (itemLabels != null && itLt != null)
                        itemLabels.Add(new KeyValuePair<string, LocText>("*all*", itLt));
                }
                return rows;
            }

            if (draw) DrawInfoCaption(win, x, y, caption, captionColor, namePrefix);

            float cx = startX;
            int rowsUsed = 1;

            for (int i = 0; i < count; i++)
            {
                var s = res.valid[i];
                string text = ItemStackParser.DescribeEntry(s);

                Sprite icon;
                Color tint;
                bool hasIcon = ItemIconResolver.TryGet(s.id, out icon, out tint);

                float textW = MeasureWidth(text);
                float tokenW = textW + (hasIcon ? INFO_ICON + INFO_ICON_GAP : 0f);

                if (cx > startX && cx + tokenW > right)   // 放不下才折行（本行已有内容）
                {
                    rowsUsed++;
                    cx = startX;
                    y += INFO_ROW_H;
                }

                if (draw)
                {
                    float ix = cx;
                    if (hasIcon)
                    {
                        var img = EventUiKit.MakeSolid(namePrefix + "_Ico" + i, win, tint);
                        if (img != null)
                        {
                            img.sprite = icon;
                            img.type = Image.Type.Simple;
                            img.color = tint;
                            img.raycastTarget = false;
                            EventUiKit.Place(img.gameObject, ix, y + (INFO_ROW_H - INFO_ICON) * 0.5f,
                                             INFO_ICON, INFO_ICON);
                        }
                        ix += INFO_ICON + INFO_ICON_GAP;
                    }

                    LocText itLt;
                    var itRoot = EventUiKit.MakeFixedLabel(namePrefix + "_" + i, win, text,
                                                           valueColor, FS_INFO,
                                                           TextAlignmentOptions.Left, out itLt);
                    EventUiKit.FitLabel(itRoot, itLt, ix, y, Mathf.Max(24f, right - ix), INFO_ROW_H);
                    StyleInfoValue(itLt, valueColor, labels);

                    // 每个物品单独记录，点击被拒时按 itemId 精确定位
                    if (itemLabels != null && itLt != null)
                        itemLabels.Add(new KeyValuePair<string, LocText>(s.id, itLt));
                }

                cx += tokenW + INFO_TOKEN_GAP;
            }

            return rowsUsed;
        }

        private void DrawInfoCaption(RectTransform win, float x, float y, string caption,
                                     Color color, string namePrefix)
        {
            if (string.IsNullOrEmpty(caption)) return;

            LocText capLt;
            var capRoot = EventUiKit.MakeFixedLabel(namePrefix + "_Cap", win,
                                                    "<b>" + caption + "</b>", color, FS_INFO,
                                                    TextAlignmentOptions.Left, out capLt);
            EventUiKit.FitLabel(capRoot, capLt, x, y, INFO_LABEL_W, INFO_ROW_H);
            if (capLt != null)
            {
                capLt.raycastTarget = false;
                StyleText(capLt, color, FS_INFO, TextAlignmentOptions.Left);
            }
        }

        private void StyleInfoValue(LocText lt, Color color, List<LocText> labels)
        {
            if (lt == null) return;
            lt.raycastTarget = false;
            StyleText(lt, color, FS_INFO, TextAlignmentOptions.Left);
            if (labels != null) labels.Add(lt);
        }

        // ── 实测字宽 / 折行高度（两个隐藏标签，互不干扰；都摆在屏幕外）──────
        private LocText _measureNoWrap;
        // 隐藏的「折行」测量标签
        private LocText _measureWrap;

        private float MeasureWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            try
            {
                EnsureMeasureLabels();
                if (_measureNoWrap != null)
                {
                    _measureNoWrap.text = text;
                    _measureNoWrap.ForceMeshUpdate();
                    float w = _measureNoWrap.preferredWidth;
                    if (w > 0f) return w;
                }
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:MeasureWidth", "[Panel] MeasureWidth 失败 → 已跳过本步，流程继续", e); }

            return EstimateWidth(text, FS_INFO);
        }

        private float MeasureWrapHeight(string text, float width)
        {
            if (string.IsNullOrEmpty(text)) return INFO_ROW_H;

            try
            {
                EnsureMeasureLabels();
                if (_measureWrap != null)
                {
                    var rt = _measureWrap.rectTransform;
                    rt.sizeDelta = new Vector2(Mathf.Max(16f, width), 0f);
                    _measureWrap.text = text;
                    _measureWrap.ForceMeshUpdate();
                    float h = _measureWrap.preferredHeight;
                    if (h > 1f) return h;
                }
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:MeasureWrapHeight", "[Panel] MeasureWrapHeight 失败 → 已跳过本步，流程继续", e); }

            return INFO_ROW_H;
        }

        private void EnsureMeasureLabels()
        {
            if (_measureNoWrap == null)
            {
                LocText lt;
                var root = EventUiKit.MakeFixedLabel("_MeasureNoWrap", transform, "", C_Txt, FS_INFO,
                                                     TextAlignmentOptions.Left, out lt);
                if (root != null)
                {
                    var rt = EventUiKit.RT(root);
                    if (rt != null) rt.anchoredPosition = new Vector2(-30000f, -30000f);
                }
                _measureNoWrap = lt;
                if (_measureNoWrap != null)
                {
                    _measureNoWrap.raycastTarget = false;
                    EventUiKit.SetWrap(_measureNoWrap, false);
                }
            }

            if (_measureWrap == null)
            {
                LocText lt;
                var root = EventUiKit.MakeFixedLabel("_MeasureWrap", transform, "", C_Txt, FS_INFO,
                                                     TextAlignmentOptions.Left, out lt);
                if (root != null)
                {
                    var rt = EventUiKit.RT(root);
                    if (rt != null) rt.anchoredPosition = new Vector2(-30000f, -30000f);
                }
                _measureWrap = lt;
                if (_measureWrap != null)
                {
                    _measureWrap.raycastTarget = false;
                    EventUiKit.SetWrap(_measureWrap, true);
                }
            }
        }

        private static float EstimateWidth(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return text.Length * Mathf.Max(1f, fontSize * 0.62f);
        }

        // 信息栏文案优先级（行数计算 / 绘制共用同一套判据）：
 
        private void BuildOptionInfo(RectTransform win, EventOption opt,
                                     float x, float y, float w, float h,
                                     out LocText[] infoLabels,
                                     out List<KeyValuePair<string, LocText>> submitItemLabels)
        {
            infoLabels = null;
            submitItemLabels = null;
            try
            {
                if (opt == null) return;

                const float padL = 34f;   // 与选项文字左对齐
                float textX = x + padL;
                float textW = w - padL - INFO_PAD_R;

                int totalRows = InfoRows(opt, textW);
                if (totalRows <= 0) return;

                // ---- ① 背景：比面板底再暗一档 ----
                var bg = EventUiKit.MakeGradientV("InfoBg", win, C_InfoBgTop, C_InfoBgBot);
                EventUiKit.Place(bg.gameObject, x + 1f, y, w - 2f, h);

                // ---- ② 顶边细分隔线：与选项行做清晰切分 ----
                var sepLine = EventUiKit.MakeSolid("InfoSep", win,
                    new Color(C_GoldDim.r, C_GoldDim.g, C_GoldDim.b, 0.22f));
                EventUiKit.Place(sepLine.gameObject, x + 1f, y, w - 2f, 1f);

                // ---- ③（已移除：原"左缘金色短竖条 InfoStripe"与上方选项行重复，直接去掉）----

                // ---- ④ 逐组绘制（同一套摆法已用于上面的行数计算）----
                float startY = y + (h - totalRows * INFO_ROW_H) * 0.5f;
                var labels = new List<LocText>(totalRows);
                var submitLabels = new List<KeyValuePair<string, LocText>>();

                int rowsSubmit = LayoutInfoGroup(win, textX, startY, textW, "提交",
                                                 opt.submit_compiled, opt.submit_label, null,
                                                 C_InfoSubmitLabel, C_InfoSubmitValue,
                                                 "InfoSubmit", labels, submitLabels, true);
                if (rowsSubmit > 0) startY += rowsSubmit * INFO_ROW_H;

                // 奖励行：不收集（不参与"提交不足"染色）
                LayoutInfoGroup(win, textX, startY, textW, "获得",
                                opt.reward_compiled, opt.grant_label, opt.grant_icon,
                                C_InfoRewardLabel, C_InfoRewardValue,
                                "InfoReward", labels, null, true);

                if (labels.Count > 0) infoLabels = labels.ToArray();
                if (submitLabels.Count > 0) submitItemLabels = submitLabels;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] 信息栏构建失败: " + e);
            }
        }

        // 提交不足反馈：辅助方法

        // 算出该选项的提交里"哪些材料不够"。
        private HashSet<string> ComputeShortfalls(EventOption opt)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (opt == null) return set;

            var res = opt.submit_compiled;
            if (res == null || res.IsEmpty || res.formatError) return set;

            for (int i = 0; i < res.valid.Count; i++)
            {
                var req = res.valid[i];
                if (req == null) continue;

                float have;
                try { have = ItemStackExecutor.SumAvailableAcrossMap(new Tag(req.id)); }
                catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:ComputeShortfalls", "[Panel] ComputeShortfalls 失败 → 已按兜底处理", e); continue; }

                if (have + 0.01f < req.mass) set.Add(req.id);   // 与 CanSubmit 里的 EPS 一致
            }
            return set;
        }

        // 把"短缺点"翻译成需要染色的 LocText 列表；无短缺点返回 null。
        private LocText[] GetShortfallLabels(int optIdx, HashSet<string> shortfalls)
        {
            if (shortfalls == null || shortfalls.Count == 0) return null;

            List<KeyValuePair<string, LocText>> list;
            if (!_submitItemLabels.TryGetValue(optIdx, out list) || list == null) return null;

            // 自定义文案场景：整块文本代表全部提交物品 → 整块染
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Key == "*all*")
                    return list[i].Value != null ? new LocText[] { list[i].Value } : null;
            }

            // 标准场景：逐个按 itemId 匹配
            var result = new List<LocText>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var lt = list[i].Value;
                var id = list[i].Key;
                if (lt == null || string.IsNullOrEmpty(id)) continue;
                if (shortfalls.Contains(id)) result.Add(lt);
            }
            return result.Count > 0 ? result.ToArray() : null;
        }

        // 收集需要随选项行一起抖动的 UI 元素
        private static RectTransform[] CollectShakeTargets(
            KButton btn, Image frame, Image fill, Image stripe, Image dot, GameObject optRoot)
        {
            var list = new List<RectTransform>(6);
            if (btn != null) list.Add(btn.transform as RectTransform);
            if (frame != null) list.Add(frame.rectTransform);
            if (fill != null) list.Add(fill.rectTransform);
            if (stripe != null) list.Add(stripe.rectTransform);
            if (dot != null) list.Add(dot.rectTransform);
            if (optRoot != null) list.Add(optRoot.transform as RectTransform);
            return list.ToArray();
        }

        private static void MakeBorder(RectTransform win, float w, float h)
        {
            var top = EventUiKit.MakeSolid("BdTop", win, C_Border);
            top.gameObject.SetActive(false);
            var bottom = EventUiKit.MakeSolid("BdBottom", win, C_Border);
            bottom.gameObject.SetActive(false);
            var left = EventUiKit.MakeSolid("BdLeft", win, C_Border);
            left.gameObject.SetActive(false);
            var right = EventUiKit.MakeSolid("BdRight", win, C_Border);
            right.gameObject.SetActive(false);
            EventUiKit.Place(top.gameObject, 0f, 0f, w, 1f);
            EventUiKit.Place(bottom.gameObject, 0f, h - 1f, w, 1f);
            EventUiKit.Place(left.gameObject, 0f, 0f, 1f, h);
            EventUiKit.Place(right.gameObject, w - 1f, 0f, 1f, h);
        }

        private static void MakeBrackets(RectTransform win, float w, float h)
        {
            float i = BRK_IN;
            float r = w - BRK_IN - BRK;
            float b = h - BRK_IN - BRK_T;

            MakeBar(win, "BrkTL_H", i, i, BRK, BRK_T);
            MakeBar(win, "BrkTL_V", i, i, BRK_T, BRK);
            MakeBar(win, "BrkTR_H", r, i, BRK, BRK_T);
            MakeBar(win, "BrkTR_V", w - BRK_IN - BRK_T, i, BRK_T, BRK);
            MakeBar(win, "BrkBL_H", i, b, BRK, BRK_T);
            MakeBar(win, "BrkBL_V", i, h - BRK_IN - BRK, BRK_T, BRK);
            MakeBar(win, "BrkBR_H", r, b, BRK, BRK_T);
            MakeBar(win, "BrkBR_V", w - BRK_IN - BRK_T, h - BRK_IN - BRK, BRK_T, BRK);
        }

        private static void MakeBar(RectTransform win, string name, float x, float y, float w, float h)
        {
            var img = EventUiKit.MakeSolid(name, win, new Color(C_Gold.r, C_Gold.g, C_Gold.b, 0.55f));
            EventUiKit.Place(img.gameObject, x, y, w, h);
        }

        // 按钮整形：原版模板 → 拆掉它的 LayoutGroup → 按我们的矩形摆位
        private static void SkinButton(KButton btn, float x, float y, float w, float h,
                                       float fontSize, TextAlignmentOptions align, float padL)
        {
            if (btn == null) return;
            try
            {
                var go = btn.gameObject;

                EventUiKit.StripLayout(go, true);
                EventUiKit.Place(go, x, y, w, h);
                EventUiKit.StretchAllChildren(go);

                var lt = go.GetComponentInChildren<LocText>();
                if (lt != null)
                {
                    EventUiKit.StripLayout(lt.gameObject, true);
                    EventUiKit.Place(lt.gameObject, padL, 0f, Mathf.Max(1f, w - padL - 40f), h);
                    lt.alignment = align;
                    lt.fontSize = fontSize;
                    EventUiKit.SetWrap(lt, false);
                }
                EventUiKit.CleanExtraLocTexts(go, lt);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] 按钮整形失败: " + e);
            }
        }

        // 文本样式守卫（见 EventPanelStyleKeeper）
        private void EnsureStyleKeeper()
        {
            if (_style != null) return;
            try { _style = gameObject.AddComponent<EventPanelStyleKeeper>(); }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] 样式守卫挂载失败: " + e);
            }
        }

        private void StyleText(LocText lt, Color color, float size, TextAlignmentOptions align)
        {
            if (_style == null) return;
            _style.Add(lt, color, size, align);
        }

        // 构筑完成后快照全部 LocText 的当前样式（MakeFixedLabel 已经设对了颜色/字号），
        private void SnapshotStyles()
        {
            if (_style == null) return;
            try
            {
                var all = gameObject.GetComponentsInChildren<LocText>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var lt = all[i];
                    if (lt == null) continue;
                    if (_style.Contains(lt)) continue;
                    _style.Add(lt, lt.color, lt.fontSize, lt.alignment);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel] 样式快照失败: " + e);
            }
        }

        // 文案内容
        private string ResolveTitle()
        {
            if (_review) return string.IsNullOrEmpty(_rName) ? "事件回顾" : _rName;
            if (_inst != null)
            {
                string sn = _inst.GetShownName();
                if (!string.IsNullOrEmpty(sn)) return sn;
            }
            if (_def != null)
            {
                string n = _def.GetNameText();
                if (!string.IsNullOrEmpty(n)) return n;
            }
            return _inst != null ? _inst.templateId : "事件";
        }

        private string ResolveSubtitle()
        {
            if (_review)
            {
                // ★ 固定文案，【不拼 outcome】。
                return "第 " + (_rCycle + 1) + " 周期 · 已结束";
            }

            string r = "第 " + ((_inst != null ? _inst.startCycle : 0) + 1) + " 周期";
            if (_inst != null)
            {
                r += " · " + StateText(_inst.state.ToString());

                string cd = CountdownText();
                if (!string.IsNullOrEmpty(cd)) r += " · " + cd;

                // 两层效果并存时（决策效果还没跑完）补一段"处置剩"；默认单层不加，保持简短
                if (_inst.hasChoiceEffect && !_inst.choiceEffectDone)
                    r += " · 处置 " + Mathf.Max(0f, _inst.GetChoiceEffectRemaining()).ToString("F0") + "s";
            }
            return r;
        }

 // 倒计时文本（《事件框架规划.md》§16.10）——显示【还能拖多久】(resident)：
        private string CountdownText()
        {
            if (_inst == null) return null;

            var phase = _inst.choicePhase;
            if (phase == EventChoicePhase.ForcedPanel) return "等待你的选择";
            if (phase == EventChoicePhase.Chosen)
                return _inst.EffectAxisDone() ? "已结束" : "处置中";

            float left = _inst.GetResidentRemaining();
            if (left > 60f) return "还剩 " + (left / 600f).ToString("F1") + " 周期";
            return "还剩 " + Mathf.Max(0f, Mathf.RoundToInt(left)) + "s";
        }

        // 状态枚举名 → 中文（字符串比较，不依赖枚举成员名）
        private static string StateText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "未知";
            switch (raw)
            {
                case "Pending": return "待触发";
                case "Active": return "进行中";
                case "Ended": return "已结束";
                case "Finished": return "已结束";
                case "Waiting": return "等待中";
                case "Expired": return "已超时";
                default: return raw;
            }
        }

        private struct Block
        {
            // 这一段的内容文本
            public string text;
            // 文字颜色
            public Color color;
            // 字号
            public float size;
            // 与下一段的间距
            public float gap;
            public Block(string t, Color c, float s, float g)
            { text = t; color = c; size = s; gap = g; }
        }

        private List<Block> BuildBodyBlocks()
        {
            var list = new List<Block>();

            if (_review)
            {
                string descR = _def != null ? _def.GetDescText() : "";
                if (string.IsNullOrEmpty(descR) && _def != null) descR = _def.ai_summary ?? "";
                if (string.IsNullOrEmpty(descR))
                    descR = _def == null
                            ? "（对应 yaml 已不存在，仅显示存档里的摘要）"
                            : "（本事件没有描述）";

                list.Add(new Block(descR, C_Dim, FS_BODY, 0f));

                // ★ 回顾不再输出"选择了哪项"文案（无选项区、无页脚）——该设计已整体废除。
                return list;
            }

            string desc = _inst != null ? _inst.GetShownDescription() : (_def != null ? _def.GetDescText() : "");
            if (string.IsNullOrEmpty(desc) && _def != null) desc = _def.ai_summary ?? "";
            if (string.IsNullOrEmpty(desc)) desc = "（本事件没有描述）";
            list.Add(new Block(desc, C_Txt, FS_BODY, 0f));

            if (_inst != null && _inst.playerChoices != null && _inst.playerChoices.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _inst.playerChoices.Count; i++)
                {
                    int idx = _inst.playerChoices[i];
                    if (i > 0) sb.Append("　·　");
                    sb.Append(_def != null && _def.options != null
                              && idx < _def.options.Count && _def.options[idx] != null
                              ? ResolveLocString(_def.options[idx].label, "选项 " + idx)
                              : ("选项 " + idx));
                }
                list.Add(new Block("已选：" + sb, C_Gold, FS_BODY - 2f, 16f));
            }

            return list;
        }

        private void CollectOptions()
        {
            _optIds.Clear();
            _optTexts.Clear();
            if (_review) return;
            if (_inst != null && _inst.ChoiceChosen) return;   // ★已表态 ⇒ 不再渲染选项区（只看"已选："那行）
            if (_def == null || _def.options == null) return;

            _optDisabled.Clear();

            for (int i = 0; i < _def.options.Count; i++)
            {
                var o = _def.options[i];
                if (o == null) continue;

                _optIds.Add(i);

                string text = PanelTextRenderer.Render(ResolveLocString(o.label, "选项 " + i), _def.id);

 // 选项可用性：自身条件不满足 / 分支目标当前不可触发 ⇒ 灰显 + 拒绝点击
                if (!EventSystem.IsOptionAvailable(_def, o))
                {
                    _optDisabled.Add(i);
                    text = text + "  <color=#8A8A8A>（当前不可用）</color>";
                    Debug.Log("[MoreFood/Event][UI/Panel] 选项 " + i + "（" + (o.id ?? "?")
                              + "）当前不可用 → 灰显");
                }

                _optTexts.Add(text);
            }
        }

        private static string ResolveLocString(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            if (!key.StartsWith("STRINGS.") && !key.StartsWith("MISSING.")) return key;
            try
            {
                string s = new LocString(key).text;
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:ResolveLocString", "[Panel] ResolveLocString 失败 → 已按兜底处理", e); return fallback; }
        }

        // 交互
        private void OnClickOption(int idx, EventRowShake shake)
        {
            try
            {
                if (_inst == null || _def == null) return;

                // ★ 已表态 ⇒ 当场锁定：重复点击会把 idx 反复追加进 playerChoices，
                if (_inst.ChoiceChosen)
                {
                    Debug.LogWarning("[MoreFood/Event][UI/Panel] 该事件已表态（选项 " + _inst.chosenIndex
                                     + "）→ 拒绝重复点击");
                    return;
                }

 // 不可用选项：拒绝点击（选择时再判一次，防止渲染后状态变化）
                if (_optDisabled.Contains(idx) || !EventSystem.IsOptionAvailable(_def, _def.options != null && idx >= 0 && idx < _def.options.Count ? _def.options[idx] : null))
                {
                    Debug.LogWarning("[MoreFood/Event][UI/Panel] 选项 " + idx + " 当前不可用 → 拒绝点击");
                    return;
                }

 // ：根据 ApplyChoice 的返回值决定是否关闭面板
                bool ok = EventSystem.ApplyChoice(_inst, _def, idx, false);
                if (ok)
                {
                    CloseCurrent("玩家选了选项 " + idx);
                }
                else
                {
                    Debug.Log("[MoreFood/Event][UI/Panel] 选项 " + idx
                              + " 被拒绝 → 面板保持打开（reason=提交失败）");

 // 拒绝反馈：整行小幅抖动 + 不够的材料文字染红加粗
                    try
                    {
                        var opt = (idx >= 0 && idx < _def.options.Count) ? _def.options[idx] : null;
                        var shortfalls = ComputeShortfalls(opt);
                        var labels = GetShortfallLabels(idx, shortfalls);
                        if (shake != null) shake.PlayReject(labels);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MoreFood/Event][UI/Panel] 拒绝反馈异常: " + e);
                        if (shake != null) shake.PlayReject(null);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][UI/Panel] OnClickOption 异常: " + e); }
        }

        private void OnClickEnd()
        {
            try
            {
                Debug.Log("[MoreFood/Event][UI/Panel] 玩家点「结束事件」");
                EventSystem.End(_inst, "玩家手动结束");
                CloseCurrent("玩家手动结束");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][UI/Panel] OnClickEnd 异常: " + e); }
        }

        private void OnClickClose() { CloseCurrent("玩家关闭"); }

        // 尺寸自检（一次性）：布局跑完一帧后把关键矩形摊进日志
        private float _checkAt = -1f;
        // 是否已做过尺寸自检
        private bool _layoutChecked;

 // 副标题实时刷新（：显示 resident —— 玩家"还能拖多久"）
        private LocText _subTx;
        // 副标题上次写入的文本（变了才写，避免每帧重排版）
        private string _subCache;
        // 副标题视觉状态是否已初始化
        private bool _subStateInit;
        // 下次允许刷新的时刻（节流 10 次/秒）
        private float _subNextAt;

        // 节流入口：顶多 10 次/秒。数值本来就是 1 游戏秒一跳，
        private void TickSubtitleThrottled()
        {
            if (Time.unscaledTime < _subNextAt) return;
            _subNextAt = Time.unscaledTime + 0.1f;
            TickSubtitle();
        }

        private void TickSubtitle()
        {
            if (_subTx == null || _inst == null || _review) return;

            string s;
            try { s = ResolveSubtitle(); }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:TickSubtitle", "[Panel] TickSubtitle 失败 → 已按兜底处理", e); return; }

 // ：以【显示文本】为唯一比较依据 —— 文本没变就不写回（每帧 SetText 会触发 TMP 重排版）
            if (_subStateInit && s == _subCache) return;
            _subCache = s;
            _subStateInit = true;
            try { _subTx.SetText(s); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:TickSubtitle#2", "[Panel] TickSubtitle 设置文本失败 → 已跳过本步，流程继续", e); }
        }

        // 兜底：万一 ScreenUpdate 没被 KScreenManager 调到（例如屏不在栈顶的那几帧），
        private void Update()
        {
            TickSubtitleThrottled();
        }

        // 每帧：驱动字幕节流 + 一次布局自检
        public override void ScreenUpdate(bool topLevel)
        {
            TickSubtitleThrottled();   // ★ 不能被自检的早退分支挡住
            OneShotLayoutCheck();
        }

        private void OneShotLayoutCheck()
        {
            if (_layoutChecked) return;
            if (_checkAt < 0f) { _checkAt = Time.unscaledTime + 0.6f; return; }
            if (Time.unscaledTime < _checkAt) return;
            _layoutChecked = true;

            try
            {
                Debug.Log("[MoreFood/Event][UI/Panel][尺寸自检] 屏=" + Describe(transform)
                          + " | 窗=" + Describe(transform.Find("Window"))
                          + " | 标题条=" + Describe(transform.Find("Window/HeaderBg"))
                          + " | 标题=" + Describe(transform.Find("Window/Title"))
                          + " | 正文0=" + Describe(transform.Find("Window/Body0"))
                          + " | 选项0=" + Describe(transform.Find("Window/Opt_0"))
                          + " | X=" + Describe(transform.Find("Window/XOuter")));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][UI/Panel][尺寸自检] 异常: " + e);
            }
        }

        private static string Describe(Transform t)
        {
            if (t == null) return "不存在";
            try
            {
                var rt = t as RectTransform;
                string comp = "";
                if (t.GetComponent<VerticalLayoutGroup>() != null) comp += "[VLG!]";
                if (t.GetComponent<HorizontalLayoutGroup>() != null) comp += "[HLG!]";
                if (t.GetComponent<ContentSizeFitter>() != null) comp += "[CSF!]";
                return "'" + t.name + "' " + (rt != null ? rt.rect.size.ToString() : "无RT") + comp;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:Describe", "[Panel] Describe 失败 → 已按兜底处理", e); return "'" + t.name + "' 读取异常"; }
        }
    }

    // 选项行的悬停 / 按下反馈 —— 自己实现，不依赖原版 KButton 的悬停机制。
    public class EventRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                 IPointerDownHandler, IPointerUpHandler
    {
        public Image fill;        // 行玻璃底（悬停时变亮）
        public Image stripe;      // 行首金条（悬停时加宽）
        public LocText label;     // ★ 选项文字：悬停时变色
        public LocText[] infoLabels; // ★v3.5：信息栏文字（提交 / 获得 各一行）

        // 信息栏每行的基础色（悬停时按比例提亮）
        private Color[] _infoBase;
        // 是否已捕获信息栏基础色
        private bool _captured;
        // 是否已捕获行视觉基线（底色 / 文字色 / 金条宽）
        private bool _visualBaseCaptured;
        // 行底色基线
        private Color _fillBase;
        // 行文字色基线
        private Color _labelBase;
        // 行首金条宽度基线
        private float _stripeBaseW;

        private void CaptureVisualBase()
        {
            if (_visualBaseCaptured) return;
            _visualBaseCaptured = true;
            if (fill != null) _fillBase = fill.color;
            if (label != null) _labelBase = label.color;
            if (stripe != null) _stripeBaseW = stripe.rectTransform.sizeDelta.x;
        }

        private static readonly Color TxtHover = new Color(0.431f, 0.827f, 0.910f, 1.00f);
        private static readonly Color TxtPress = new Color(0.600f, 0.900f, 0.960f, 1.00f);

        // 记录信息栏每行的基础色（悬停时按比例提亮，保留暗金 / 亮金的差别）
        public void CaptureBase()
        {
            if (_captured) return;
            _captured = true;

            if (infoLabels == null) { _infoBase = null; return; }
            _infoBase = new Color[infoLabels.Length];
            for (int i = 0; i < infoLabels.Length; i++)
                if (infoLabels[i] != null) _infoBase[i] = infoLabels[i].color;
        }

        private void Apply(Color fc, float sw, Color tc, float infoLift)
        {
            try
            {
                if (fill != null) fill.color = fc;
                if (label != null) label.color = tc;
                if (stripe != null)
                    stripe.rectTransform.sizeDelta =
                        new Vector2(sw, stripe.rectTransform.sizeDelta.y);

                if (infoLabels != null && _infoBase != null)
                {
                    for (int i = 0; i < infoLabels.Length; i++)
                    {
                        var lt = infoLabels[i];
                        if (lt == null) continue;
                        // infoLift > 0 → 向白插值提亮；= 0 → 还原
                        lt.color = infoLift > 0f
                            ? Color.Lerp(_infoBase[i], Color.white, infoLift)
                            : _infoBase[i];
                    }
                }
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:Apply", "[Panel] Apply 失败 → 已跳过本步，流程继续", e); }
        }

        public void OnPointerEnter(PointerEventData e) { CaptureVisualBase(); Apply(Color.Lerp(_fillBase, Color.white, 0.10f), _stripeBaseW + 1f, TxtHover, 0.20f); }
        public void OnPointerExit(PointerEventData e) { CaptureVisualBase(); Apply(_fillBase, _stripeBaseW, _labelBase, 0.00f); }
        public void OnPointerDown(PointerEventData e) { CaptureVisualBase(); Apply(Color.Lerp(_fillBase, Color.white, 0.16f), _stripeBaseW + 1f, TxtPress, 0.35f); }
        public void OnPointerUp(PointerEventData e) { OnPointerEnter(e); }
    }

 // 选项行的"拒绝反馈"：整行小幅抖动 + 不够的材料文字染红加粗
    public class EventRowShake : MonoBehaviour
    {
        // 抖动参数
        private const float SHAKE_DUR = 0.35f;   // 抖动时长
        private const float SHAKE_FREQ = 55f;     // 抖动频率（Hz），小幅高频 = 急促警示
        private const float SHAKE_AMP = 6f;      // 抖动幅度（像素）

        // 颜色时序：抖动结束后颜色还留 0.25s 让玩家看清，再花 0.15s 回落
        private const float TINT_HOLD = 0.25f;
        // 颜色回落时长（秒）
        private const float TINT_FADE = 0.15f;

        // 与 EventPanel 里的信息栏"提交"列基础色 / 警示红保持一致
        private static readonly Color C_Base = new Color(0.878f, 0.741f, 0.471f, 1.00f); // #E0BD78
        private static readonly Color C_Warn = new Color(1.000f, 0.353f, 0.302f, 1.00f); // #FF5A4D

        // 参与位置抖动的 UI 元素（Build 时设置一次）
        public RectTransform[] targets;

        // 各行原始位置（抖动后复位用）
        private Vector2[] _basePos;
        // 是否已捕获原始位置
        private bool _captured;

        // 正在变色的文本节点
        private LocText[] _activeTints;
        // 抖动计时（-1 = 未开始）
        private float _t = -1f;

        // 播放一次拒绝反馈；shortfallLabels 可为 null（只抖动，不染色）
        public void PlayReject(LocText[] shortfallLabels)
        {
            if (!_captured) CaptureBase();
            _activeTints = shortfallLabels;
            _t = 0f;
            enabled = true;
            ApplyTint(1f);   // 立即全红（0 帧延迟，与抖动同步）
        }

        private void CaptureBase()
        {
            _captured = true;
            if (targets == null) return;
            _basePos = new Vector2[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) _basePos[i] = targets[i].anchoredPosition;
        }

        private void LateUpdate()
        {
            if (_t < 0f) { enabled = false; return; }

            _t += Time.unscaledDeltaTime;

            // ---- 抖动：sin × 衰减包络 ----
            if (_t <= SHAKE_DUR)
            {
                float p = _t / SHAKE_DUR;
                float decay = 1f - p;
                float offset = Mathf.Sin(_t * SHAKE_FREQ) * SHAKE_AMP * decay;
                ApplyShake(offset);
            }
            else
            {
                ApplyShake(0f);
            }

            // ---- 颜色：抖动期间全红 → 保持 → 回落 ----
            if (_t <= SHAKE_DUR + TINT_HOLD)
            {
                ApplyTint(1f);
            }
            else if (_t <= SHAKE_DUR + TINT_HOLD + TINT_FADE)
            {
                float p = (_t - SHAKE_DUR - TINT_HOLD) / TINT_FADE;
                ApplyTint(1f - p);
            }
            else
            {
                ApplyTint(0f);
                _activeTints = null;
                _t = -1f;
                enabled = false;
            }
        }

        private void ApplyShake(float offset)
        {
            if (targets == null || _basePos == null) return;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].anchoredPosition = _basePos[i] + new Vector2(offset, 0f);
            }
        }

        private void ApplyTint(float t)
        {
            if (_activeTints == null) return;
            for (int i = 0; i < _activeTints.Length; i++)
            {
                var lt = _activeTints[i];
                if (lt == null) continue;
                lt.color = Color.Lerp(C_Base, C_Warn, t);
                lt.fontStyle = t > 0.5f ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // 面板销毁 / 组件停用时归位（LocText 也会一起销毁，但保持整洁）
        private void OnDisable()
        {
            ApplyShake(0f);
        }
    }

    // 关闭按钮（菱形 X）的悬停反馈：静息红 → 悬停蓝 → 按下亮蓝。
    public class EventBackdropClose : MonoBehaviour, IPointerClickHandler
    {
        // 点击面板背景 → 关闭面板
        public void OnPointerClick(PointerEventData e)
        {
            try { EventPanel.CloseCurrent("背景点击"); } catch (Exception ex) { Distribution.Contracts.DistributionLog.ExcOnce("panel:OnPointerClick", "[Panel] OnPointerClick 失败 → 已跳过本步，流程继续", ex); }
        }
    }
    public class EventXHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                               IPointerDownHandler, IPointerUpHandler
    {
        // 光晕 Image
        public Image halo;
        // 外圈 Image
        public Image outer;
        // 字符文本
        public LocText glyph;

        private static readonly Color Red = new Color(0.878f, 0.353f, 0.314f, 1.00f); // #E05A50
        private static readonly Color RedHalo = new Color(0.878f, 0.353f, 0.314f, 0.14f);
        private static readonly Color RedPress = new Color(1.000f, 0.478f, 0.424f, 1.00f); // #FF7A6C

        private static readonly Color Blue = new Color(0.310f, 0.706f, 0.910f, 1.00f); // #4FB4E8
        private static readonly Color BlueHalo = new Color(0.310f, 0.706f, 0.910f, 0.18f);

        private void Apply(Color main, Color haloC)
        {
            try
            {
                if (outer != null) outer.color = main;
                if (glyph != null) glyph.color = main;
                if (halo != null) halo.color = haloC;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:Apply#2", "[Panel] Apply 失败 → 已跳过本步，流程继续", e); }
        }

        // 静息蓝 → 悬停红 → 按下亮红
        public void OnPointerEnter(PointerEventData e) { Apply(Red, RedHalo); }
        public void OnPointerExit(PointerEventData e) { Apply(Blue, BlueHalo); }
        public void OnPointerDown(PointerEventData e) { Apply(RedPress, RedHalo); }
        public void OnPointerUp(PointerEventData e) { Apply(Red, RedHalo); }
    }

    // 文本样式守卫
    public class EventPanelStyleKeeper : MonoBehaviour
    {
        public class Item
        {
            // 文本节点
            public LocText lt;
            // 颜色
            public Color color;
            // 字号
            public float size;
            // 对齐方式
            public TextAlignmentOptions align;
        }

        public readonly List<Item> items = new List<Item>();

        // 只跑头几帧就自停：够压过模板在 Start 里的回灌，又不至于和悬停变色打架，
        private int _frames = 6;

        // 该文本是否已在自检列表里
        public bool Contains(LocText lt)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].lt == lt) return true;
            return false;
        }

        // 把一个文本登记进自检列表（记录颜色 / 字号 / 对齐供比对）
        public void Add(LocText lt, Color color, float size, TextAlignmentOptions align)
        {
            if (lt == null) return;
            var it = new Item();
            it.lt = lt; it.color = color; it.size = size; it.align = align;
            items.Add(it);
            EventUiKit.ApplyTextStyle(lt, color, size, align);
        }

        private void LateUpdate()
        {
            if (_frames <= 0) { enabled = false; return; }
            _frames--;
            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null || it.lt == null) continue;
                    EventUiKit.ApplyTextStyle(it.lt, it.color, it.size, it.align);
                }
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("panel:LateUpdate", "[Panel] LateUpdate 失败 → 已跳过本步，流程继续", e); }
        }
    }

    // 窗口拖动：挂在窗口根上，按住窗口任意位置都能拖（Unity 有拖动阈值，
    public class EventPanelDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        // 被拖动的目标
        public RectTransform target;

        // 拖动缩放系数
        private float _scale = 1f;

        // 开始拖拽面板：记下缩放比用于换算位移
        public void OnBeginDrag(PointerEventData e)
        {
            try
            {
                _scale = target != null ? target.lossyScale.x : 1f;
                if (_scale <= 0.0001f) _scale = 1f;
            }
            catch (Exception ex) { Distribution.Contracts.DistributionLog.ExcOnce("panel:OnBeginDrag", "[Panel] OnBeginDrag 失败 → 已按兜底处理", ex); _scale = 1f; }
        }

        // 拖拽中：把指针位移换算成面板位移
        public void OnDrag(PointerEventData e)
        {
            try
            {
                if (target == null || e == null) return;

                Vector2 p = target.anchoredPosition + e.delta / _scale;

                var parent = target.parent as RectTransform;
                if (parent != null)
                {
                    float mx = parent.rect.width * 0.5f - 80f;
                    float my = parent.rect.height * 0.5f - 80f;
                    p.x = Mathf.Clamp(p.x, -mx, mx);
                    p.y = Mathf.Clamp(p.y, -my, my);
                }
                target.anchoredPosition = p;
            }
            catch (Exception ex) { Distribution.Contracts.DistributionLog.ExcOnce("panel:OnDrag", "[Panel] OnDrag 失败 → 已跳过本步，流程继续", ex); }
        }
    }

    internal static class EventPanelEscPriorityPatch
    {
        private static bool Prefix(KButtonEvent e)
        {
            try
            {
                if (e == null || e.Consumed || !e.IsAction(global::Action.Escape)) return true;
                if (!EventPanel.IsAnyOpen) return true;
                e.TryConsume(global::Action.Escape);
                EventPanel.CloseCurrent("ESC 优先关闭事件面板");
                return false;
            }
            catch (Exception ex)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("panel:Prefix", "[Panel] Prefix 失败 → 已按兜底处理", ex);
                return true;
            }
        }
    }
}
