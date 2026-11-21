// EventCustomNotify.cs — 事件框架 · 【状态行外观注册表 + 灾星倒计时内置实现】

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EventFramework
{
    // ① 抽象：每个状态行实例都要实现这 4 个能力
    public abstract class EventStatusRowBase
    {
        // 行的槽位键
        public string Key;
        // 来源事件实例
        public EventInstance Inst;
        // 事件定义
        public EventDef Def;

        public abstract bool IsAlive { get; }
        public abstract void SetText(string text);
        public abstract void Tick(float remaining, float total);
        public abstract void Destroy();

        // ★ 公共工具：UI 一致的倒计时文本
        public static string FormatCountdown(float remaining)
        {
            if (remaining <= 0f) return "已结束";
            if (remaining > 60f) return "还剩 " + (remaining / 600f).ToString("F1") + " 周期";
            return "还剩 " + Mathf.Max(0f, Mathf.RoundToInt(remaining)) + "s";
        }

        // 把"还剩 X"的纯数字部分抠出来（灾星表盘只需要数字，不需要"还剩"）
        public static string FormatCountdownShort(float remaining)
        {
            if (remaining <= 0f) return "0";
            if (remaining > 60f) return (remaining / 600f).ToString("F1") + " 周期";
            return Mathf.Max(0f, Mathf.RoundToInt(remaining)) + " 秒";
        }
    }

    // 创建上下文
    public sealed class EventStatusRowContext
    {
        // 槽位键
        public string Key;
        // 事件定义
        public EventDef Def;
        // 事件实例
        public EventInstance Inst;
        public string Text;             // 已做过 {name} 替换 + 富文本渲染
        // 描述文本
        public string Description;
        // 指定挂载父节点（默认挂通知区）
        public Transform ParentOverride;
        // 是否为「选项级」状态行
        public bool OptionSlot;
    }

    // ② 注册表：key → 工厂
    public static class EventCustomNotifyRegistry
    {
        public delegate EventStatusRowBase Factory(EventStatusRowContext ctx);

        private static readonly Dictionary<string, Factory> _map =
            new Dictionary<string, Factory>(StringComparer.Ordinal);

        public static int Count { get { return _map.Count; } }
        public static IEnumerable<string> AllKeys { get { return _map.Keys; } }

        // 内置外观键：原版 TemporaryActionRow
        public const string ALERT = "eventfw.notify.alert";
        // 内置外观键：灾难倒计时行
        public const string DISASTER = "eventfw.notify.disaster";

        // 注册一个「状态行外观」键 → 工厂（重复键告警并覆盖）
        public static bool Register(string key, Factory factory)
        {
            if (string.IsNullOrEmpty(key) || factory == null) return false;
            if (_map.ContainsKey(key))
            {
                Debug.LogWarning("[MoreFood/Event][Notify] 外观键 '" + key
                                 + "' 已被注册过（先到者胜）");
                return false;
            }
            _map[key] = factory;
            Debug.Log("[MoreFood/Event][Notify] 已注册状态行外观：" + key);
            return true;
        }

        // 按 key 取回外观工厂；未注册返回 null
        public static Factory Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Factory f;
            return _map.TryGetValue(key, out f) ? f : null;
        }

        // 该外观键是否已注册（★ 修复：先确保内置外观注册，否则首次 Has 误判 false）
        public static bool Has(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            EnsureBuiltins();
            return _map.ContainsKey(key);
        }

        // 注销外观键
        public static void Unregister(string key)
        {
            if (!string.IsNullOrEmpty(key)) _map.Remove(key);
        }

        public static void Clear()
        {
            _map.Clear();
            _builtinsDone = false;
        }

        // 内置外观是否已注册
        private static bool _builtinsDone;

        public static void EnsureBuiltins()
        {
            if (_builtinsDone) return;
            _builtinsDone = true;

            Register(ALERT, ctx => new TemporaryActionRowAdapter(ctx));
            Register(DISASTER, ctx => new DisasterCountdownRow(ctx));
        }

        // 建一个实例（工厂异常 → null，事件本体不受影响）
        public static EventStatusRowBase Create(string key, EventStatusRowContext ctx)
        {
            EnsureBuiltins();
            var f = Get(key);
            if (f == null)
            {
                EventWarnOnce.Warn("notify-factory:" + key,
                    "[MoreFood/Event][Notify] 未注册的外观键 '" + key + "' → 回退 alert");
                f = Get(ALERT);
                if (f == null) return null;
            }
            try { return f(ctx); }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] 外观 '" + key + "' 工厂异常: " + e);
                return null;
            }
        }
    }

    // ③ 内置 A：原版 TemporaryActionRow（eventfw.notify.alert）
    internal sealed class TemporaryActionRowAdapter : EventStatusRowBase
    {
        // 被适配的原版行对象
        private TemporaryActionRow _row;
        public TemporaryActionRowAdapter(EventStatusRowContext ctx)
        {
            if (ctx == null || ctx.Inst == null) return;

            var mgr = NotificationScreen_TemporaryActions.Instance;
            if (mgr == null) return;

            var m = typeof(NotificationScreen_TemporaryActions).GetMethod(
                "CreateActionRow",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
            if (m == null) return;

            _row = m.Invoke(mgr, null) as TemporaryActionRow;
            if (_row == null) return;

            _row.gameObject.name = "MoreFood_EventStatus_" + ctx.Key;
            _row.Setup(ctx.Text ?? "", ctx.Description ?? "", null);
            _row.HideOnClick = false;
            _row.ShowTimeout = false;
            _row.OnRowClicked = null;
            _row.OnRowHidden = null;
            _row.SetLifetime(ctx.Inst.GetDurationRemaining());

            try
            {
                var refresh = typeof(TemporaryActionRow).GetMethod(
                    "RefreshContentWidth",
                    System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance);
                if (refresh != null) refresh.Invoke(_row, null);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:TemporaryActionRowAdapter", "[CustomNotify] TemporaryActionRowAdapter 失败 → 已跳过本步，流程继续", e); }

            _row.ShowOnSpawn = true;
            _row.Show();
        }

        public override bool IsAlive
        {
            get
            {
                if (_row == null || _row.gameObject == null) return false;
                if (Inst == null) return true;
                return Inst.GetDurationRemaining() > 0f;
            }
        }

        // 设置状态行标题（圆环模式：正文走 Row.Setup）
        public override void SetText(string text)
        {
            if (_row == null) return;
            try { _row.Setup(text ?? "", "", null); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:SetText", "[CustomNotify] SetText 失败 → 已跳过本步，流程继续", e); }
        }

        // 每秒刷新圆环比例与剩余时间（remaining 剩余秒 / total 总秒）
        public override void Tick(float remaining, float total)
        {
            if (_row == null) return;
            try
            {
                // 反刷 Lifetime（原版是真实时间 dt；我们用游戏时间兜底）
                var f = typeof(TemporaryActionRow).GetField(
                    "<Lifetime>k__BackingField",
                    System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance);
                if (f != null) f.SetValue(_row, remaining);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:Tick", "[CustomNotify] Tick 失败 → 已跳过本步，流程继续", e); }
        }

        public override void Destroy()
        {
            if (_row == null) return;
            try
            {
                var mgr = NotificationScreen_TemporaryActions.Instance;
                if (mgr != null)
                {
                    var m = typeof(NotificationScreen_TemporaryActions).GetMethod(
                        "RemoveRow",
                        System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance);
                    if (m != null) { m.Invoke(mgr, new object[] { _row }); _row = null; return; }
                }
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:Destroy", "[CustomNotify] Destroy 失败 → 已跳过本步，流程继续", e); }
            try { UnityEngine.Object.Destroy(_row.gameObject); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:Destroy#2", "[CustomNotify] Destroy 失败 → 已跳过本步，流程继续", e); }
            _row = null;
        }
    }

    // ④ 内置 B：灾星倒计时外观（新版默认 alert）
    internal sealed class DisasterCountdownRow : EventStatusRowBase
    {
        // 复用的原版通知 prefab 名（从它克隆出倒计时行）
        private const string PREFAB_ID = "LargeImpactNotification";

        private static readonly string[] KILL_SCRIPTS =
        {
            "LargeImpactorNotificationUI",
            "LargeImpactorUINotificationHitEffects",
            "LargeImpactorNotificationUI_Clock",
            "LargeImpactorNotificationUI_CycleLabelEffects",
            "NotificationAnimator",   // ★ 修复崩溃：克隆体未调 Begin → layoutElement 为 null → LateUpdate NRE
        };

        // 本行的根节点（原版 prefab 克隆出来的实例）
        private GameObject _root;
        // 标题 LocText（显示灾难名）
        private LocText _titleLabel;
        // 周期/倒计时 LocText
        private LocText _cycleLabel;
        // 圆环进度 Image（360° 径向填充，按剩余时间比例）
        private Image _ringFill;
        // 原版血条节点（拿到即隐藏，改用圆环显示）
        private GameObject _healthbar;

        private float _total = 1f;             // 建行时记录的总时长（圆环比例用）
        private string _lastCycleText;         // 文本变化检测缓存
        // 视觉状态是否已初始化（避免每帧重复设置）
        private bool _stateInit;

        public DisasterCountdownRow(EventStatusRowContext ctx)
        {
            if (ctx == null || ctx.Inst == null) return;

            try
            {
                var prefab = FindPrefab();
                if (prefab == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] 找不到 " + PREFAB_ID + " prefab → 降级 alert");
                    // 简单降级：走 alert 的构造（同一槽位）
                    var fb = new TemporaryActionRowAdapter(ctx);
                    _root = null;
                    // 把 fb 的字段拷过来？不需要——降级时就是另一个对象，Ticker 会把它当 alert 用
                    _delegate = fb;
                    return;
                }

                var parent = ctx.ParentOverride != null ? ctx.ParentOverride : ResolveDefaultParent();
                var clone = UnityEngine.Object.Instantiate(prefab, parent, false);
                if (clone == null) return;

                clone.name = "MoreFood_DisasterNotify";
                clone.SetActive(false);

                // ★ 先杀脚本，再 SetActive(true)
                KillScripts(clone.transform);

                clone.SetActive(true);
                _root = clone;

// 缓存两个文本节点：标题、周期倒计时
                _titleLabel = FindLocText(clone.transform, "Label/pivot/Title");
                _cycleLabel = FindLocText(clone.transform,
                                          "Label/pivot/TimerSection/LabelSection/numberText");

                var fillT = clone.transform.Find("Label/pivot/TimerSection/Dial/FillMask/fill");
                if (fillT != null) _ringFill = fillT.GetComponent<Image>();

                var hb = clone.transform.Find("Label/pivot/HealthbarSection");
                if (hb != null) { _healthbar = hb.gameObject; _healthbar.SetActive(false); }

                if (_ringFill != null)
                {
                    _ringFill.type = Image.Type.Filled;
                    _ringFill.fillMethod = Image.FillMethod.Radial360;
                    _ringFill.fillOrigin = (int)Image.Origin360.Top;
                    _ringFill.fillClockwise = false;
                    _ringFill.fillAmount = 1f;
                }

                if (_titleLabel != null)
                {
                    // ★ 修复"文本太大"：原版大横幅标题字号大 → 自动缩小 + 换行
                    _titleLabel.enableAutoSizing = true;
                    _titleLabel.fontSizeMin = 10f;
                    _titleLabel.fontSizeMax = 20f;
                    _titleLabel.enableWordWrapping = true;
                    if (!string.IsNullOrEmpty(ctx.Text))
                        _titleLabel.text = ctx.Text;
                }

                // 记录总时长（圆环比例用）；读档后取存档里记的值
                _total = Mathf.Max(1f, ctx.Inst.GetDurationRemaining());

                // 立即写一次文本，避免开场空白
                if (_cycleLabel != null)
                {
                    _lastCycleText = FormatCountdownShort(_total);
                    _cycleLabel.text = _lastCycleText;
                    _stateInit = true;
                }

                Debug.Log("[MoreFood/Event][Notify] 已建灾星外观状态行：" + ctx.Key
                          + " total=" + _total.ToString("F0") + "s");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] 灾星外观创建异常: " + e);
            }
        }

        // 降级时挂的临时对象（几乎不会走）
        private EventStatusRowBase _delegate;

        public override bool IsAlive
        {
            get
            {
                if (_delegate != null) return _delegate.IsAlive;
                if (_root == null) return false;
                if (Inst == null) return true;
                return Inst.GetDurationRemaining() > 0f;
            }
        }

        // 状态行标题设置（有委托时转发给委托实现）
        public override void SetText(string text)
        {
            if (_delegate != null) { _delegate.SetText(text); return; }
            if (_titleLabel != null && !string.IsNullOrEmpty(text))
                _titleLabel.text = text;
        }

        // 每秒刷新圆环（有委托时转发）
        public override void Tick(float remaining, float total)
        {
            if (_delegate != null) { _delegate.Tick(remaining, total); return; }
            if (_root == null) return;

            // ---- 圆环：每 tick 都刷（开销极小，视觉平滑）----
            if (_ringFill != null)
            {
                float denom = _total > 0f ? _total : 1f;
                _ringFill.fillAmount = Mathf.Clamp01(remaining / denom);
            }

            // ---- 文本：只在【算出来的字符串变了】时才 SetText ----
            if (_cycleLabel != null)
            {
                string s = FormatCountdownShort(remaining);
                if (!_stateInit || s != _lastCycleText)
                {
                    _stateInit = true;
                    _lastCycleText = s;
                    _cycleLabel.text = s;
                }
            }
        }

        public override void Destroy()
        {
            if (_delegate != null) { _delegate.Destroy(); _delegate = null; return; }
            if (_root != null)
            {
                try { UnityEngine.Object.Destroy(_root); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:Destroy#3", "[CustomNotify] Destroy 失败 → 已跳过本步，流程继续", e); }
                _root = null;
            }
        }

        // -------------------------------------------------------------
        private static GameObject FindPrefab()
        {
            var s = NotificationScreen.Instance;
            if (s == null || s.customNotificationPrefabs == null) return null;

            var list = s.customNotificationPrefabs;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p != null && p.ID == PREFAB_ID && p.notificationPrefab != null)
                    return p.notificationPrefab;
            }
            return null;
        }

        private static Transform ResolveDefaultParent()
        {
            var s = NotificationScreen.Instance;
            if (s != null && s.customNotificationPrefabs != null)
            {
                var list = s.customNotificationPrefabs;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p != null && p.ID == PREFAB_ID && p.parentFolder != null)
                        return p.parentFolder.transform;
                }
            }
            try
            {
                if (GameScreenManager.Instance != null
                    && GameScreenManager.Instance.ssOverlayCanvas != null)
                    return GameScreenManager.Instance.ssOverlayCanvas.transform;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("customnotify:ResolveDefaultParent", "[CustomNotify] ResolveDefaultParent 失败 → 已跳过本步，流程继续", e); }
            return null;
        }

        private static LocText FindLocText(Transform root, string path)
        {
            if (root == null) return null;
            var t = root.Find(path);
            return t != null ? t.GetComponent<LocText>() : null;
        }

        private static void KillScripts(Transform root)
        {
            if (root == null) return;
            // ★ 修复崩溃：改清整棵子树（原只扫根节点/TimerSection，扫不到子节点 "Animator" 上的脚本）
            var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                string n = c.GetType().Name;
                for (int k = 0; k < KILL_SCRIPTS.Length; k++)
                {
                    if (n == KILL_SCRIPTS[k])
                    {
                        UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                }
            }
        }
    }
}
