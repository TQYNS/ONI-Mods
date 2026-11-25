// EventNotifier.cs — 事件框架 · 【通知层：走原版通知系统】

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace EventFramework
{
    public static class EventNotifier
    {
        // 承载通知的常驻对象
        private static GameObject _host;
        // 原版 Notifier 组件
        private static Notifier _notifier;

        // instanceId → 事件通知（可决策的那条）
        private static readonly Dictionary<string, Notification> _live =
            new Dictionary<string, Notification>();

        // instanceId → 常驻状态行（TemporaryActionRow；仅 statusStyle=alert 且未走 customKey 时使用）
        private static readonly Dictionary<string, TemporaryActionRow> _statusRows =
            new Dictionary<string, TemporaryActionRow>();

        // 自定义外观的行追踪：slotKey → 行对象
        private static readonly Dictionary<string, EventStatusRowBase> _customRows =
            new Dictionary<string, EventStatusRowBase>();

        // clickData → EventInstance（点击回调反查用；状态行的 clickData 带后缀）
        private static readonly Dictionary<string, EventInstance> _instByNotif =
            new Dictionary<string, EventInstance>();

        // instanceId → 常驻状态通知（statusStyle = plain 时用；Notifications/ 里的一条普通通知）
        private static readonly Dictionary<string, Notification> _liveStatus =
            new Dictionary<string, Notification>();

        // 状态行 clickData 的后缀（与事件通知区分）
        private const string STATUS_DATA_SUFFIX = "#status";
        // 状态行对象名前缀
        private const string STATUS_ROW_PREFIX = "MoreFood_EventStatus_";

         // 选项级状态行的槽位后缀（同一实例可同时挂根级 + 选项级两条）
        private const string STATUS_OPT_SUFFIX = "#opt";

        private static string SlotKey(EventInstance inst, bool optionSlot)
        {
            return optionSlot ? inst.instanceId + STATUS_OPT_SUFFIX : inst.instanceId;
        }

        // 状态行文本（= 持续 buff 的【名字】）：有自定义文本就用它
        // （渲染富文本 / [id] / {name} / {占位符}），否则走档位模板
        private static string StatusTextOf(EventDef def, EventNotifyStyle style, string textOverride,
                                          EventInstance inst)
        {
            string name = inst != null ? inst.GetShownName()
                          : (def != null ? def.GetNameText() : "");

            if (string.IsNullOrEmpty(textOverride))
                return EventNotifyStyles.BuildStatusText(style, name);

            string t = textOverride.Replace("{name}", name ?? "");
            return RenderNoticeText(t, inst);
        }

        // 常驻状态行的正文（= 持续 buff 的【tooltip】）：
        //   优先级：status_tooltip（常驻专用）> 事件 notification.tooltip > description > ai_summary
        //   统一走「占位符填充 + 富文本 / [id] 贴图渲染」，与事件通知完全同一套口径。
        // 目的：让「10%~20%」「1.5~8 周期」这类区间占位被替换成【本次实际抽到的值】。
        internal static string StatusTooltipOf(EventDef def, EventInstance inst, string tooltipOverride)
        {
            if (def == null) return "";

            string name = inst != null ? inst.GetShownName() : (def.GetNameText() ?? "");

            string raw = null;
            var variant = VariantOf(def, inst);
            if (!string.IsNullOrEmpty(tooltipOverride)) raw = tooltipOverride;
            if (string.IsNullOrEmpty(raw) && variant != null && !string.IsNullOrEmpty(variant.status_tooltip))
                raw = variant.status_tooltip;
            if (string.IsNullOrEmpty(raw) && def.notification != null
                && !string.IsNullOrEmpty(def.notification.status_tooltip))
                raw = def.notification.status_tooltip;
            if (string.IsNullOrEmpty(raw) && def.notification != null
                && !string.IsNullOrEmpty(def.notification.tooltip))
                raw = def.notification.tooltip;
            if (string.IsNullOrEmpty(raw)) raw = inst != null ? inst.GetShownDescription() : def.GetDescText();
            if (string.IsNullOrEmpty(raw)) raw = def.ai_summary ?? "";

            if (!string.IsNullOrEmpty(name)) raw = raw.Replace("{name}", name);

            // 正文是否自己用了「剩余」占位符（用了就不再自动追加，避免重复显示周期）
            bool usedDynamic = raw.IndexOf("{cycles}", StringComparison.Ordinal) >= 0
                            || raw.IndexOf("{remain}", StringComparison.Ordinal) >= 0;
            raw = InjectRemainingCycles(raw, inst);

            string rendered = RenderNoticeText(raw, inst);

            // 占位符未解析（读档重推 / handler 早返回没写 arg）⇒ 回落描述，避免把字面量 {xxx} 显示给玩家
            if (!string.IsNullOrEmpty(rendered))
            {
                int lb = rendered.IndexOf('{');
                if (lb >= 0)
                {
                    int rb = rendered.IndexOf('}', lb + 1);
                    if (rb > lb)
                    {
                        string desc = inst != null ? inst.GetShownDescription() : def.GetDescText();
                        if (string.IsNullOrEmpty(desc)) desc = def.ai_summary ?? "";
                        if (!string.IsNullOrEmpty(desc))
                            rendered = InjectRemainingCycles(
                                RenderNoticeText(desc.Replace("{name}", name), inst), inst);
                    }
                }
            }

            // 兜底：正文没用 {cycles}/{remain} —— 多数情况是 handler 一次性拼好的静态 {result} 串，
            //   里面的周期数字是死的。此时自动补一行【剩余 X.X 周期】（每次悬浮重新求值），
            //   保证"鼠标挪上去就刷新一次"的默认体验不依赖 yaml 是否配置。
            if (!usedDynamic) rendered = AppendRemainingLine(rendered, inst);

            return rendered;
        }

        // 给正文追加一行动态剩余周期（只在正文自己没写 {cycles}/{remain} 时调用）
        private static string AppendRemainingLine(string s, EventInstance inst)
        {
            if (string.IsNullOrEmpty(s) || inst == null || !inst.hasDuration) return s;
            try
            {
                float rem = inst.GetDurationRemaining();
                if (rem < 0f) rem = 0f;
                return s + "\n剩余 " + (rem / 600f).ToString("0.0") + " 周期";
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:AppendRemainingLine", "[Notify] AppendRemainingLine 失败 → 已按兜底处理", e); return s; }
        }

        // 持续 buff 的【剩余】周期：{cycles} / {remain} → 当前还剩多少周期（1 周期 = 600 秒）。
        //   状态行 tooltip 走延迟解析（BuildStatusTooltip），玩家每次把鼠标挪上去都会重新求值一次
        //   ⇒ 显示的就是「当前剩余」（如总 7.2 周期、已过 1 周期 ⇒ 显示 6.2），
        //   即【被动式刷新，仅在鼠标挪上去时触发一次】，不做轮询。
        //   注：这只作用于常驻状态行；事件通知（notification.tooltip）的 {cycles} 仍是 handler 写的总时长。
        private static string InjectRemainingCycles(string s, EventInstance inst)
        {
            if (string.IsNullOrEmpty(s) || inst == null || !inst.hasDuration) return s;
            try
            {
                float rem = inst.GetDurationRemaining();
                if (rem < 0f) rem = 0f;
                string c = (rem / 600f).ToString("0.0");
                return s.Replace("{cycles}", c).Replace("{remain}", c);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:InjectRemainingCycles", "[Notify] InjectRemainingCycles 失败 → 已按兜底处理", e); return s; }
        }

        // 我们是否持有一份暂停（必须配对 Unpause）
        private static bool _holdingPause;

        public static int LiveCount { get { return _live.Count; } }

        // 宿主
        internal static Notifier GetNotifier()
        {
            try
            {
                if (_notifier != null) return _notifier;

                var game = Game.Instance;
                if (game == null) return null;

                _host = new GameObject("MoreFood_EventSource");
                _host.transform.SetParent(game.gameObject.transform, false);
                _notifier = _host.AddComponent<Notifier>();
                _host.AddComponent<EventStatusRowTicker>();

                Debug.Log("[MoreFood/Event][Notify] 通知宿主已建立（挂在 " + game.gameObject.name + " 下）");
                return _notifier;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] 建立通知宿主失败: " + e);
                return null;
            }
        }

        // 推送
        public static void Push(EventDef def, EventInstance inst)
        {
            if (def == null || inst == null) return;

            try
            {
                if (def.notification == null) return;

                // hidden 级别 = 不弹通知（原方案 §3.1）
                if (def.notification.level == EventNotifyLevel.hidden)
                {
                    Debug.Log("[MoreFood/Event][Notify] '" + def.id + "' level=hidden，不弹通知");
                    return;
                }

                var notifier = GetNotifier();
                if (notifier == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] Notifier 不可用，跳过通知");
                    return;
                }

                // 反编译实证：这里曾经错位了两个参数：
                var style = EventNotifyStyles.Resolve(def.notification);
                NotificationType notifyType = EventNotifyStyles.ResolveSort(
                    def.notification, style, MapLevel(def.notification.level));

                // 无选项事件（触发即自动决策）⇒ 点击通知直接消除；有选项事件保留通知，直到玩家决策
                int optCount = def.options != null ? def.options.Count : 0;
                bool clearOnClick = optCount == 0;

                var notif = new Notification(
                    inst != null ? inst.GetShownName() : def.GetNameText(),   // 1  title（冻结变体优先）
                    notifyType,                                          // 2  type（档位的 sort 可自定义排序位置）
                    new System.Func<List<Notification>, object, string>(BuildTooltip), // 3 tooltip
                    inst,                                                // 4  tooltip_data（要填占位符）
                    false,                                               // 5  expires：不自动过期（原版 Update 会按 lifetime 清）
                    0f,                                                  // 6  delay
                    new Notification.ClickCallback(OnNotificationClicked),// 7 custom_click_callback
                    inst.instanceId,                                     // 8  custom_click_data
                    null,                                                // 9  click_focus（暂无目标）
                    true,                                                // 10 volume_attenuation
                    clearOnClick,                                        // 11 clear_on_click：无选项=点了即消除
                    false);                                              // 12 show_dismiss_button

                // 外观档位标记：原版只在 NotificationType.Custom 分支读这个字段，
                string tier = EventNotifyStyles.ResolveTier(def.notification);
                if (!string.IsNullOrEmpty(tier))
                    notif.customNotificationID = EventNotifyStyles.MarkFor(tier);

                _live[inst.instanceId] = notif;
                _instByNotif[inst.instanceId] = inst;

                notifier.Add(notif, " · 事件");

                Debug.Log("[MoreFood/Event][Notify] 已推入左侧通知栈：'" + def.id
                          + "' level=" + def.notification.level + " tier=" + (tier ?? "-"));

                // ---- 暂停（引用计数，必须配对）----
                if (def.notification.pause_game) AcquirePause();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] Push 异常（不影响事件本体）: " + e);
            }
        }

        // 选项被触发那一刻推的一条通知（notice_name 非空才推）
        public static void PushOptionNotice(EventDef def, EventInstance inst, EventOption opt)
        {
            if (def == null || inst == null || opt == null) return;
            if (string.IsNullOrEmpty(opt.notice_name)) return;

            try
            {
                Notifier notifier = GetNotifier();
                if (notifier == null) return;

                string tier = !string.IsNullOrEmpty(opt.notice_tier) ? opt.notice_tier
                            : (def.notification != null ? def.notification.tier : null);
                string text = RenderNoticeText(opt.notice_tooltip ?? "", inst);

                Notification n = new Notification(
                    opt.notice_name,
                    NotificationType.Neutral,
                    new System.Func<List<Notification>, object, string>(TooltipText),
                    text,
                    false,
                    0f,
                    new Notification.ClickCallback(NoopClick),   // 非 null 空实现，堵住原版 else 分支
                    null,
                    null,
                    true,
                    true,        // clear_on_click：点一下即消除
                    false);

                if (!string.IsNullOrEmpty(tier))
                    n.customNotificationID = EventNotifyStyles.MarkFor(tier);

                notifier.Add(n, " · 选项结果");
                Debug.Log("[MoreFood/Event][Notify] 选项级通知: '" + def.id + "." + opt.id
                          + "' 标题=" + opt.notice_name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] PushOptionNotice 异常: " + e);
            }
        }

        private static string TooltipText(List<Notification> all, object data) { return data as string ?? ""; }
        private static void NoopClick(object data) { }

        // 推送根级常驻状态（不带选项槽）)
        public static void PushStatus(EventDef def, EventInstance inst)
        {
            PushStatus(def, inst, false, null, null);
        }

        // 常驻状态推送（根级 / 选项级共用）。
        //   textOverride    = 状态行【名字】（status_text；不写 ⇒ 走档位模板）
        //   tooltipOverride = 状态行【正文】（status_tooltip；不写 ⇒ 回落事件 tooltip / 描述）
        public static void PushStatus(EventDef def, EventInstance inst, bool optionSlot,
                                      string textOverride, string tooltipOverride = null)
        {
            if (def == null || inst == null) return;

            try
            {
                if (def.notification == null) return;

                string key = SlotKey(inst, optionSlot);
                string tier = EventNotifyStyles.ResolveStatusTier(def.notification);
                var style = EventNotifyStyles.Get(tier);
                string text = StatusTextOf(def, style, textOverride, inst);
                // 持续 buff 的 tooltip：优先 status_tooltip；占位符延迟到显示时解析（见 StatusTooltipPayload 说明）
                string tooltip = StatusTooltipOf(def, inst, tooltipOverride);
                var tooltipPayload = new StatusTooltipPayload
                {
                    def = def,
                    inst = inst,
                    tooltipOverride = tooltipOverride,
                };
                NotificationType sortType = EventNotifyStyles.ResolveStatusSort(def.notification, style);

                // 自定义外观优先：
                string customKey = EventNotifyStyles.ResolveStatusCustom(def.notification, style);
                if (!string.IsNullOrEmpty(customKey) && EventCustomNotifyRegistry.Has(customKey))
                {
                    var ctx = new EventStatusRowContext
                    {
                        Key = key,
                        Def = def,
                        Inst = inst,
                        Text = text,
                        Description = tooltip,
                        ParentOverride = null,
                        OptionSlot = optionSlot,
                    };

                    var row = EventCustomNotifyRegistry.Create(customKey, ctx);
                    if (row != null)
                    {
                        row.Key = key;
                        row.Inst = inst;
                        row.Def = def;

                        // 幂等：同 slot 已有行 → 先销毁再挂新的
                        EventStatusRowBase old;
                        if (_customRows.TryGetValue(key, out old) && old != null)
                        {
                            try { old.Destroy(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:PushStatus", "[Notify] PushStatus 失败 → 已跳过本步，流程继续", e); }
                        }
                        _customRows[key] = row;
                        _instByNotif[key + STATUS_DATA_SUFFIX] = inst;

                        Debug.Log("[MoreFood/Event][Notify] 已挂自定义外观 '" + customKey
                                  + "' 状态行：" + def.id
                                  + " 剩余=" + inst.GetDurationRemaining().ToString("F0") + "s");
                    }
                    else
                    {
                        Debug.LogWarning("[MoreFood/Event][Notify] 自定义外观 '" + customKey
                                         + "' 工厂返回 null（事件 " + def.id + "）→ 本次状态行不显示");
                    }
                    return;
                }

                // 原有分支（保持向后兼容）
                if (style == null || style.statusStyle != EventStatusStyle.alert)
                {
                    PushStatusNotif(def, inst, style, key, text, tooltipPayload, sortType);
                    return;
                }

                var mgr = NotificationScreen_TemporaryActions.Instance;
                if (mgr == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] TemporaryActions 管理器不可用，跳过状态行");
                    return;
                }

                var trow = CreateStatusRow(mgr);
                if (trow == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] 创建状态行失败，跳过");
                    return;
                }

                trow.gameObject.name = STATUS_ROW_PREFIX + key;

                string iconName = style != null ? style.statusIcon : null;
                var sprite = string.IsNullOrEmpty(iconName)
                             ? null
                             : NotificationScreen_Style_Patch.SpriteFor(iconName);

                trow.Setup(text, tooltip, sprite);

                if (style != null && trow.Image != null) trow.Image.color = style.statusFg;

                trow.HideOnClick = false;
                trow.ShowTimeout = style != null && style.statusStyle == EventStatusStyle.alert;
                trow.OnRowClicked = null;
                trow.OnRowHidden = null;

                float remain = inst.GetDurationRemaining();
                trow.SetLifetime(remain > 0f ? remain : 0.01f);

                RefreshContentWidth(trow);
                trow.ShowOnSpawn = true;
                trow.Show();

                _statusRows[key] = trow;
                _instByNotif[key + STATUS_DATA_SUFFIX] = inst;

                Debug.Log("[MoreFood/Event][Notify] 已挂常驻状态行：'" + def.id
                          + "' 剩余=" + remain.ToString("F0") + "s tier=" + (tier ?? "-")
                          + " 样式=" + (style != null ? style.statusStyle.ToString() : "?"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] PushStatus 异常（不影响事件本体）: " + e);
            }
        }

        private static void PushStatusNotif(EventDef def, EventInstance inst, EventNotifyStyle style,
                                            string key, string text, StatusTooltipPayload payload,
                                            NotificationType sortType)
        {
            var notifier = GetNotifier();
            if (notifier == null) return;

            string clickData = key + STATUS_DATA_SUFFIX;

            var notif = new Notification(
                text,                                                  // 1 标题（必须与事件通知不同：原版按 titleText 合并条目）
                MapLevel(def.notification.level),                      // 2  type
                // 3 tooltip：延迟解析 —— handler 在本次推送之后才写 NoticeArgs，
                //    必须等显示时再按 status_tooltip + {占位符} 求值（否则 {result} 会显示成字面量）
                new System.Func<List<Notification>, object, string>(BuildStatusTooltip),
                payload,                                               // 4  tooltip_data
                false,                                                 // 5  expires
                0f,                                                    // 6  delay
                new Notification.ClickCallback(OnStatusNotifClicked),  // 7  空回调：必须非 null，
                                                                       //    否则原版 OnClick 会掉进
                clickData,                                             // 8  custom_click_data
                null,                                                  // 9  click_focus
                true,                                                  // 10 volume_attenuation
                false,                                                 // 11 clear_on_click：点了不清
                false);                                                // 12 show_dismiss_button

            notif.clearOnClick = false;
            notif.expires = false;
            notif.playSound = false;

            // 类型选 Tutorial：和原版那两条参考通知（黄色 + 感叹号）用同一个 NotificationType，
            notif.Type = sortType;

            string tier = EventNotifyStyles.ResolveStatusTier(def.notification);
            if (!string.IsNullOrEmpty(tier))
                notif.customNotificationID = EventNotifyStyles.MarkForStatus(tier);

            _liveStatus[key] = notif;
            _instByNotif[clickData] = inst;

            notifier.Add(notif, " · 状态");

            Debug.Log("[MoreFood/Event][Notify] 已推入常驻状态通知：'" + def.id
                      + "' 剩余=" + inst.GetDurationRemaining().ToString("F0") + "s tier=" + (tier ?? "-"));
        }

        private static void OnStatusNotifClicked(object data)
        {
        }

        private static TemporaryActionRow CreateStatusRow(NotificationScreen_TemporaryActions mgr)
        {
            try
            {
                var m = typeof(NotificationScreen_TemporaryActions).GetMethod(
                    "CreateActionRow", BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] 找不到 CreateActionRow（原版改名了？）");
                    return null;
                }
                return m.Invoke(mgr, null) as TemporaryActionRow;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] CreateActionRow 反射失败: " + e);
                return null;
            }
        }

        private static void RefreshContentWidth(TemporaryActionRow row)
        {
            try
            {
                var m = typeof(TemporaryActionRow).GetMethod("RefreshContentWidth",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (m != null) m.Invoke(row, null);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RefreshContentWidth", "[Notify] RefreshContentWidth 失败 → 已跳过本步，流程继续", e); }
        }

        // 反射缓存的 TemporaryActionRow.Lifetime 字段
        private static FieldInfo _lifetimeField;
        // 反射缓存的 UpdateTimeout 方法
        private static MethodInfo _updateTimeoutMethod;

        private static void SetRowLifetime(TemporaryActionRow row, float remaining)
        {
            try
            {
                if (_lifetimeField == null)
                    _lifetimeField = typeof(TemporaryActionRow).GetField(
                        "<Lifetime>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_lifetimeField != null) _lifetimeField.SetValue(row, remaining);

                if (_updateTimeoutMethod == null)
                    _updateTimeoutMethod = typeof(TemporaryActionRow).GetMethod(
                        "UpdateTimeout", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_updateTimeoutMethod != null) _updateTimeoutMethod.Invoke(row, null);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:SetRowLifetime", "[Notify] SetRowLifetime 失败 → 已跳过本步，流程继续", e); }
        }

        // 由 EventStatusRowTicker 每 0.5 秒调一次。
        public static void TickStatusRows()
        {
            // ---- 原有：TemporaryActionRow（老 alert 分支）----
            if (_statusRows.Count > 0)
            {
                try
                {
                    List<string> dead = null;

                    foreach (var kv in _statusRows)
                    {
                        var row = kv.Value;
                        if (row == null)
                        {
                            if (dead == null) dead = new List<string>();
                            dead.Add(kv.Key);
                            continue;
                        }

                        EventInstance inst;
                        if (!_instByNotif.TryGetValue(kv.Key + STATUS_DATA_SUFFIX, out inst) || inst == null) continue;

                        float remain = inst.GetDurationRemaining();
                        if (remain > 0f) SetRowLifetime(row, remain);
                    }

                    if (dead != null)
                        for (int i = 0; i < dead.Count; i++) _statusRows.Remove(dead[i]);
                }
                catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:TickStatusRows", "[Notify] TickStatusRows 失败 → 已跳过本步，流程继续", e); }
            }

            // ---- ：自定义外观行 ----
            if (_customRows.Count > 0)
            {
                List<string> dead2 = null;
                foreach (var kv in _customRows)
                {
                    var row = kv.Value;
                    if (row == null)
                    {
                        if (dead2 == null) dead2 = new List<string>();
                        dead2.Add(kv.Key);
                        continue;
                    }

                    try
                    {
                        if (!row.IsAlive)
                        {
                            if (dead2 == null) dead2 = new List<string>();
                            dead2.Add(kv.Key);
                            continue;
                        }

                        float total = row.Inst != null ? row.Inst.GetDurationRemaining() : 0f;
                        // (remaining, total)：当前都传"剩余"；圆环比例在行内部用构造时记的 _total 换算
                        row.Tick(total, total);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MoreFood/Event][Notify] 自定义行 tick 异常（已摘除）: " + e);
                        if (dead2 == null) dead2 = new List<string>();
                        dead2.Add(kv.Key);
                    }
                }

                if (dead2 != null)
                {
                    for (int i = 0; i < dead2.Count; i++)
                    {
                        EventStatusRowBase r;
                        if (_customRows.TryGetValue(dead2[i], out r) && r != null)
                        {
                            try { r.Destroy(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:TickStatusRows#2", "[Notify] TickStatusRows 失败 → 已跳过本步，流程继续", e); }
                        }
                        _customRows.Remove(dead2[i]);
                        _instByNotif.Remove(dead2[i] + STATUS_DATA_SUFFIX);
                    }
                }
            }
        }

        // 反射缓存的「移除状态行」方法
        private static MethodInfo _removeRowMethod;
        // 是否已尝试取过该方法（失败不重试）
        private static bool _removeRowLooked;

        private static void RemoveStatusRow(NotificationScreen_TemporaryActions mgr, TemporaryActionRow row)
        {
            if (row == null) return;

            try
            {
                if (!_removeRowLooked)
                {
                    _removeRowLooked = true;
                    _removeRowMethod = typeof(NotificationScreen_TemporaryActions).GetMethod(
                        "RemoveRow", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (_removeRowMethod == null)
                        Debug.LogWarning("[MoreFood/Event][Notify] 找不到 RemoveRow（原版改名了？）→ 走 Destroy 兜底");
                }

                if (_removeRowMethod != null)
                {
                    _removeRowMethod.Invoke(mgr, new object[] { row });
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] RemoveRow 反射失败，走 Destroy 兜底: " + e);
            }

            try { row.OnRowHidden = null; } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RemoveStatusRow", "[Notify] RemoveStatusRow 失败 → 已跳过本步，流程继续", e); }
            try { UnityEngine.Object.Destroy(row.gameObject); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RemoveStatusRow#2", "[Notify] RemoveStatusRow 失败 → 已跳过本步，流程继续", e); }
        }

        private static void OnStatusRowClicked(TemporaryActionRow row)
        {
            try
            {
                if (row == null || row.gameObject == null) return;

                string name = row.gameObject.name;
                if (string.IsNullOrEmpty(name) || !name.StartsWith(STATUS_ROW_PREFIX, StringComparison.Ordinal)) return;

                string id = name.Substring(STATUS_ROW_PREFIX.Length);
                if (id.EndsWith(STATUS_OPT_SUFFIX, StringComparison.Ordinal))
                    id = id.Substring(0, id.Length - STATUS_OPT_SUFFIX.Length);

                EventInstance inst;
                if (!_instByNotif.TryGetValue(id + STATUS_DATA_SUFFIX, out inst) || inst == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] 状态行点击找不到实例: " + id);
                    return;
                }

                Debug.Log("[MoreFood/Event][Notify] 玩家点击状态行 → 打开事件面板: " + id);
                EventPanel.ShowFor(inst);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] 状态行点击回调异常: " + e);
            }
        }

        // 常驻状态 tooltip 的【延迟解析】载荷。
        // 时序：handler（写 NoticeArgs 的那个，如 real_dry_season）是 Trigger 在 PushStatus **之后**才调的
        //   （见 EventSystem.BeginEffect §16.9），所以状态行正文不能在推送时就定死，
        //   必须延迟到玩家真正悬浮/显示时再解析，否则 {result} / {pct} 会永远拿不到值。
        private sealed class StatusTooltipPayload
        {
            public EventDef def;
            public EventInstance inst;
            public string tooltipOverride;
        }

        // 常驻状态通知的 tooltip 回调（延迟解析；优先 status_tooltip，其次事件 tooltip / 描述）
        private static string BuildStatusTooltip(List<Notification> all, object data)
        {
            try
            {
                var p = data as StatusTooltipPayload;
                if (p != null) return StatusTooltipOf(p.def, p.inst, p.tooltipOverride);

                // 兜底：历史存档 / 其它通道传进来的裸对象
                EventInstance inst = data as EventInstance;
                EventDef def = inst != null ? inst.def : data as EventDef;
                return StatusTooltipOf(def, inst, null);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:BuildStatusTooltip", "[Notify] BuildStatusTooltip 失败 → 已按兜底处理", e); return ""; }
        }

        // 取该实例当前分支的变体（未指定 / 未命中 → null，回落默认模板）
        private static EventNotifyVariant VariantOf(EventDef def, EventInstance inst)
        {
            try
            {
                if (def == null || inst == null) return null;
                if (string.IsNullOrEmpty(inst.notifyVariant)) return null;
                if (def.notification == null || def.notification.variants == null) return null;
                EventNotifyVariant v;
                if (def.notification.variants.TryGetValue(inst.notifyVariant, out v)) return v;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:VariantOf", "[Notify] VariantOf 失败 → 已跳过本步，流程继续", e); }
            return null;
        }

        // 通知正文的唯一解析入口（通知 / 历史页共用）。
        //   优先级：variants[键].tooltip → notification.tooltip → description → ai_summary
        internal static string RenderNoticeTooltipNow(EventDef def, EventInstance inst)
        {
            try
            {
                if (def == null) return "";

                string raw = null;
                var variant = VariantOf(def, inst);
                if (variant != null && !string.IsNullOrEmpty(variant.tooltip)) raw = variant.tooltip;
                if (string.IsNullOrEmpty(raw) && def.notification != null
                    && !string.IsNullOrEmpty(def.notification.tooltip))
                    raw = def.notification.tooltip;
                if (string.IsNullOrEmpty(raw)) raw = inst != null ? inst.GetShownDescription() : def.GetDescText();
                if (string.IsNullOrEmpty(raw)) raw = def.ai_summary ?? "";

                return RenderNoticeText(raw, inst);
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RenderNoticeTooltipNow", "[Notify] RenderNoticeTooltipNow 失败 → 已按兜底处理", e); return ""; }
        }

        private static string BuildTooltip(List<Notification> all, object data)
        {
try
            {
                EventInstance inst = data as EventInstance;
                EventDef def = inst != null ? inst.def : data as EventDef;
                if (def == null) return "";

                string rendered = RenderNoticeTooltipNow(def, inst);

                // 占位符未解析（读档重推 / handler 早返回未写 arg）⇒ 回落描述，避免显示字面量 {result}
                if (!string.IsNullOrEmpty(rendered))
                {
                    int lb = rendered.IndexOf('{');
                    int rb = rendered.IndexOf('}');
                    if (lb >= 0 && rb > lb)
                    {
                        string desc = def.GetDescText();
                        if (string.IsNullOrEmpty(desc)) desc = def.ai_summary ?? "";
                        if (!string.IsNullOrEmpty(desc)) rendered = RenderNoticeText(desc, inst);
                    }
                }
                return rendered;
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:BuildTooltip", "[Notify] BuildTooltip 失败 → 已按兜底处理", e); return ""; }
        }

        // 占位符填充 + 统一走面板同一套文本渲染（富文本 / [id] 贴图 / 已有转义）
        internal static string RenderNoticeText(string raw, EventInstance inst)
        {
            try
            {
                if (string.IsNullOrEmpty(raw)) return "";
                if (inst != null && inst.NoticeArgs != null)
                {
                    foreach (var kv in inst.NoticeArgs)
                        raw = raw.Replace("{" + kv.Key + "}", kv.Value ?? "");
                }
                return PanelTextRenderer.Render(raw, "notice");
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RenderNoticeText", "[Notify] RenderNoticeText 失败 → 已按兜底处理", e); return raw; }
        }

        private static NotificationType MapLevel(EventNotifyLevel lv)
        {
            switch (lv)
            {
                case EventNotifyLevel.critical: return NotificationType.Bad;
                case EventNotifyLevel.warning: return NotificationType.BadMinor;
                case EventNotifyLevel.info: return NotificationType.Neutral;
                default: return NotificationType.Event;
            }
        }

        // 点击 → 开面板
        private static void OnNotificationClicked(object data)
        {
            try
            {
                var id = data as string;
                if (string.IsNullOrEmpty(id)) { Debug.LogWarning("[MoreFood/Event][Notify] 点击回调缺 instanceId"); return; }

                EventInstance inst;
                if (!_instByNotif.TryGetValue(id, out inst) || inst == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Notify] 点击回调找不到实例: " + id);
                    return;
                }

                EventLocator.Jump(id);    // 定位器：点击通知 → 跳转目标（无效则自动出队；notify_only 仍可跳）
                Debug.Log("[MoreFood/Event][Notify] 玩家点击通知 → 打开事件面板: " + id);
                EventPanel.ShowFor(inst);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] 点击回调异常: " + e);
            }
        }

        // 结束 / 清理
        public static void RemoveEvent(EventInstance inst)
        {
            if (inst == null) return;
            try
            {
                Notification n;
                if (_live.TryGetValue(inst.instanceId, out n) && n != null && _notifier != null)
                {
                    try { _notifier.Remove(n); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RemoveEvent", "[Notify] RemoveEvent 失败 → 已跳过本步，流程继续", e); }
                }
                _live.Remove(inst.instanceId);
                _instByNotif.Remove(inst.instanceId);
                Debug.Log("[MoreFood/Event][Notify] 撤事件通知: " + inst.instanceId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] RemoveEvent 异常: " + e);
            }
        }

        // 撤掉该事件实例的状态行（根级那条）
        public static void RemoveStatus(EventInstance inst)
        {
            RemoveStatus(inst, false);
        }

        // optionSlot = true ⇒ 只撤选项级那条（根级那条照旧留着）
        public static void RemoveStatus(EventInstance inst, bool optionSlot)
        {
            if (inst == null) return;
            try
            {
                string key = SlotKey(inst, optionSlot);

                // ---- 原有：TemporaryActionRow ----
                TemporaryActionRow row;
                if (_statusRows.TryGetValue(key, out row) && row != null)
                {
                    var mgr = NotificationScreen_TemporaryActions.Instance;
                    if (mgr != null) RemoveStatusRow(mgr, row);
                    else { try { UnityEngine.Object.Destroy(row.gameObject); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RemoveStatus", "[Notify] RemoveStatus 失败 → 已跳过本步，流程继续", e); } }
                }
                _statusRows.Remove(key);

                // ---- ：自定义外观行 ----
                EventStatusRowBase crow;
                if (_customRows.TryGetValue(key, out crow) && crow != null)
                {
                    try { crow.Destroy(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RemoveStatus#2", "[Notify] RemoveStatus 失败 → 已跳过本步，流程继续", e); }
                }
                _customRows.Remove(key);

                // ---- 普通通知（plain 分支）----
                Notification n;
                if (_liveStatus.TryGetValue(key, out n) && n != null && _notifier != null)
                {
                    try { _notifier.Remove(n); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:RemoveStatus#3", "[Notify] RemoveStatus 失败 → 已跳过本步，流程继续", e); }
                }
                _liveStatus.Remove(key);

                _instByNotif.Remove(key + STATUS_DATA_SUFFIX);
                Debug.Log("[MoreFood/Event][Notify] 撤常驻状态: " + key);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Notify] RemoveStatus 异常: " + e);
            }
        }

        // 该事件结束时清干净：事件通知 + 状态行一起撤
        public static void Remove(EventInstance inst)
        {
            RemoveEvent(inst);
            RemoveStatus(inst);
        }

        public static void Clear()
        {
            try
            {
                if (_notifier != null)
                {
                    foreach (var kv in _live) { try { _notifier.Remove(kv.Value); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:Clear", "[Notify] Clear 失败 → 已跳过本步，流程继续", e); } }
                    foreach (var kv in _liveStatus) { try { _notifier.Remove(kv.Value); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:Clear#2", "[Notify] Clear 失败 → 已跳过本步，流程继续", e); } }
                }

                var mgr = NotificationScreen_TemporaryActions.Instance;
                if (mgr != null)
                    foreach (var kv in _statusRows) RemoveStatusRow(mgr, kv.Value);

                // 自定义外观行
                foreach (var kv in _customRows)
                {
                    try { if (kv.Value != null) kv.Value.Destroy(); } catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:Clear#3", "[Notify] Clear 失败 → 已跳过本步，流程继续", e); }
                }
            }
            catch (Exception e) { Distribution.Contracts.DistributionLog.ExcOnce("notify:Clear#4", "[Notify] Clear 失败 → 已跳过本步，流程继续", e); }
            _live.Clear();
            _liveStatus.Clear();
            _statusRows.Clear();
            _customRows.Clear();
            _instByNotif.Clear();
            ReleasePause();
        }

        // 暂停（引用计数配对）
        public static void AcquirePause()
        {
            try
            {
                var sc = SpeedControlScreen.Instance;
                if (sc == null) return;
                if (_holdingPause) return;
                sc.Pause(false, false);
                _holdingPause = true;
                Debug.Log("[MoreFood/Event][Notify] 已暂停（持有引用，结束时会 Unpause）");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Notify] Pause 异常: " + e); }
        }

        public static void ReleasePause()
        {
            try
            {
                if (!_holdingPause) return;
                _holdingPause = false;
                var sc = SpeedControlScreen.Instance;
                if (sc != null) sc.Unpause(false);
                Debug.Log("[MoreFood/Event][Notify] 已解除暂停（配对完成）");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Notify] Unpause 异常: " + e); }
        }

        public static bool HoldingPause { get { return _holdingPause; } }
    }

    // 常驻状态行的"节流检测器"。
    public class EventStatusRowTicker : MonoBehaviour
    {
        // 刷新间隔（秒）
        private const float INTERVAL = 0.5f;
        // 下次刷新时刻
        private float _nextTick;

        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + INTERVAL;
            EventNotifier.TickStatusRows();
        }
    }
}
