// EventNotifyStylePatch.cs — 事件框架 · 【按档位覆盖我们自己的通知外观】

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace EventFramework
{
    public static class NotificationScreen_Style_Patch
    {
        // 反射缓存的「按消息取条目」字段
        private static FieldInfo _entriesByMessageField;
        // 反射缓存的条目 Label 字段
        private static FieldInfo _entryLabelField;
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // 仪器：证明补丁到底有没有被调用（只记一次，不刷屏）
        private static bool _anySeen;
        // 探针：已见过「条目标记」日志
        private static bool _markedSeen;
        // 探针：已见过「回退」日志
        private static bool _fallbackSeen;

        [HarmonyPostfix]
        // 补丁：通知屏加一条时，按档位套用我们的外观
        public static void Postfix(NotificationScreen __instance, Notification notification)
        {
            try
            {
                if (notification == null || __instance == null) return;

                if (!_anySeen)
                {
                    _anySeen = true;
                    Debug.Log("[MoreFood/Event][NotifyStyle] ✔ Postfix 已挂载（捕获到第 1 条通知）");
                }

                string mark = notification.customNotificationID;
                if (string.IsNullOrEmpty(mark)) return;
                if (!mark.StartsWith(EventNotifyStyles.MARK, StringComparison.Ordinal)) return;

                if (!_markedSeen)
                {
                    _markedSeen = true;
                    Debug.Log("[MoreFood/Event][NotifyStyle] ✔ 已认领到我们的通知，标记 = " + mark);
                }

                string title = notification.titleText;
                if (string.IsNullOrEmpty(title)) return;

                string tier;
                bool isStatus;
                if (!ParseMark(mark, out tier, out isStatus)) return;

                var style = EventNotifyStyles.Get(tier);
                if (style == null)
                {
                    EventWarnOnce.Warn("notifystyle:" + tier,
                        "[MoreFood/Event][NotifyStyle] 档位 '" + tier + "' 不在档位表里 → 保持原版配色");
                    return;
                }

                var go = FindEntryLabel(__instance, title);
                if (go == null)
                {
                    EventWarnOnce.Warn("noentry:" + title,
                        "[MoreFood/Event][NotifyStyle] ✘ 找不到通知条目（两条路都失败）: " + title);
                    return;
                }

                var hr = go.GetComponent<HierarchyReferences>();
                if (hr == null)
                {
                    EventWarnOnce.Warn("nohr:" + title,
                        "[MoreFood/Event][NotifyStyle] ✘ 条目上没有 HierarchyReferences: " + go.name);
                    return;
                }

                ApplyToEntry(__instance, hr, style, isStatus);
                Debug.Log("[MoreFood/Event][NotifyStyle] ✔ 已按档位 '" + style.tier + "'"
                          + (isStatus ? "（状态通知）" : "（事件通知）")
                          + " 覆盖通知外观: " + title);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][NotifyStyle] 覆盖通知外观失败（不影响通知本身）: " + e);
            }
        }

        // 标记 → 档位 + 是否状态通知（"eventfw.event.yellow.status" / "eventfw.event.yellow"）
        private static bool ParseMark(string mark, out string tier, out bool isStatus)
        {
            tier = null;
            isStatus = false;
            if (string.IsNullOrEmpty(mark)) return false;

            int idx = mark.LastIndexOf('.');
            if (idx < 0 || idx + 1 >= mark.Length) return false;

            string last = mark.Substring(idx + 1);
            if (last == EventNotifyStyles.STATUS_MARK_TAIL)
            {
                isStatus = true;
                string head = mark.Substring(0, idx);
                idx = head.LastIndexOf('.');
                if (idx < 0 || idx + 1 >= head.Length) return false;
                last = head.Substring(idx + 1);
            }

            tier = last;
            return true;
        }

        // 定位条目：主路 + 兜底
        private static GameObject FindEntryLabel(NotificationScreen ns, string title)
        {
            var go = FindByMessageMap(ns, title);
            if (go != null) return go;

            go = FindByLabelText(ns, title);
            if (go != null)
            {
                if (!_fallbackSeen)
                {
                    _fallbackSeen = true;
                    Debug.LogWarning("[MoreFood/Event][NotifyStyle] ⚠ entriesByMessage 主路失败，已回退按 Text 扫描 LabelsFolder");
                }
                return go;
            }
            return null;
        }

        // 主路：entriesByMessage[titleText] → 反射读 Entry.label（Entry 是 internal 嵌套类）
        private static GameObject FindByMessageMap(NotificationScreen ns, string title)
        {
            if (_entriesByMessageField == null)
                _entriesByMessageField = typeof(NotificationScreen).GetField(
                    "entriesByMessage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (_entriesByMessageField == null) return null;

            var dict = _entriesByMessageField.GetValue(ns) as IDictionary;
            if (dict == null) return null;

            object entry = dict[title];
            if (entry == null) return null;

            var t = entry.GetType();
            if (_entryLabelField == null || _entryLabelField.DeclaringType != t)
                _entryLabelField = t.GetField("label", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (_entryLabelField == null) return null;

            return _entryLabelField.GetValue(entry) as GameObject;
        }

        // 兜底：扫 LabelsFolder 子物体，按 Text 内容匹配
        //   ① 先精确相等（最可靠；同名多条时 Text 会带 " (N)" 后缀），② 再前缀匹配兜底
        //   —— 必须分两遍：单遍的 StartsWith 会把「标题是别人前缀」的条目误命中，导致套错行
        private static GameObject FindByLabelText(NotificationScreen ns, string title)
        {
            var folder = ns.LabelsFolder;
            if (folder == null) return null;

            var tr = folder.transform;

            for (int i = 0; i < tr.childCount; i++)
            {
                var child = tr.GetChild(i);
                if (child == null) continue;
                if (ChildLabelText(child) == title) return child.gameObject;
            }

            for (int i = 0; i < tr.childCount; i++)
            {
                var child = tr.GetChild(i);
                if (child == null) continue;
                string t = ChildLabelText(child);
                if (!string.IsNullOrEmpty(t) && t.StartsWith(title, StringComparison.Ordinal))
                    return child.gameObject;
            }
            return null;
        }

        private static string ChildLabelText(Transform child)
        {
            var hr = child.GetComponent<HierarchyReferences>();
            if (hr == null) return null;
            var text = hr.GetReference<LocText>("Text");
            return text != null ? text.text : null;
        }

        private static void ApplyToEntry(NotificationScreen ns, HierarchyReferences hr,
                                         EventNotifyStyle style, bool isStatus)
        {
            bool isAlert = style.statusStyle == EventStatusStyle.alert;
            bool plainStatus = isStatus && !isAlert;

            Color fg = isStatus ? style.statusFg : style.fg;
            string iconName = isStatus ? style.statusIcon : style.icon;

            try
            {
                var text = hr.GetReference<LocText>("Text");
                if (text != null) text.color = fg;
            }
            catch (Exception e) { LogPart("Text", e); }

            // 图标两种通知都换：状态通知用 statusIcon（默认 icon_normal，不带感叹号）
            try
            {
                var icon = hr.GetReference<KImage>("Icon");
                if (icon != null)
                {
                    icon.color = fg;
                    var sp = ResolveSprite(ns, iconName);
                    if (sp != null) icon.sprite = sp;
                }
            }
            catch (Exception e) { LogPart("Icon", e); }

            // ⚠ 常驻状态(plain)到此为止：底色保持原版 Tutorial 的，只改文字色和图标
            if (plainStatus)
            {
                // 排序档用到 Tutorial 以上时，原版会给这类通知带专属底色（黄/红…）
                try
                {
                    var cs = GlobalAssets.Instance != null ? GlobalAssets.Instance.colorSet : null;
                    var btn0 = hr.GetReference<Button>("MainButton");
                    if (cs != null && btn0 != null)
                    {
                        var cb0 = btn0.colors;
                        cb0.normalColor = cs.NotificationNormalBG;
                        btn0.colors = cb0;
                    }
                }
                catch (Exception e) { LogPart("PlainStatus", e); }
                return;
            }

            try
            {
                var btn = hr.GetReference<Button>("MainButton");
                if (btn != null)
                {
                    var cb = btn.colors;
                    cb.normalColor = style.bg;
                    btn.colors = cb;
                }
            }
            catch (Exception e) { LogPart("MainButton", e); }
        }

        // fieldName：field 名称
        public static Sprite SpriteFor(string fieldName)
        {
            return ResolveSprite(NotificationScreen.Instance, fieldName);
        }

        // 按字段名取原版 NotificationScreen 上的 sprite（作者可填 icon_event / icon_bad …）
        private static Sprite ResolveSprite(NotificationScreen ns, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return null;

            Sprite cached;
            if (_spriteCache.TryGetValue(fieldName, out cached)) return cached;

            Sprite sp = null;
            try
            {
                var f = typeof(NotificationScreen).GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(Sprite)) sp = f.GetValue(ns) as Sprite;
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("notify:sprite", "[NotifyStyle] 反射取原版通知图标失败 → 保留默认图标", e);
            }

            if (sp == null)
                EventWarnOnce.Warn("notifysprite:" + fieldName,
                    "[MoreFood/Event][NotifyStyle] NotificationScreen 上没有 sprite 字段 '" + fieldName + "'");

            _spriteCache[fieldName] = sp;
            return sp;
        }

        private static void LogPart(string part, Exception e)
        {
            Debug.LogWarning("[MoreFood/Event][NotifyStyle] " + part + " 覆盖失败: " + e.Message);
        }
    }

    // 探针：原版任何一处撤我们的通知（Notification.Clear）都会被记下来
    public static class Notification_Clear_Probe
    {
        [HarmonyPostfix]
        // 补丁：通知对象初始化时套用我们的外观（字体 / 图标 / 底色）
        public static void Postfix(Notification __instance)
        {
            try
            {
                if (__instance == null) return;

                string mark = __instance.customNotificationID;
                if (string.IsNullOrEmpty(mark)) return;
                if (!mark.StartsWith(EventNotifyStyles.MARK, StringComparison.Ordinal)) return;

                var st = new System.Diagnostics.StackTrace();
                var sb = new System.Text.StringBuilder();
                for (int i = 1; i < st.FrameCount && i <= 4; i++)
                {
                    var m = st.GetFrame(i) != null ? st.GetFrame(i).GetMethod() : null;
                    if (m == null) continue;
                    if (sb.Length > 0) sb.Append(" ← ");
                    sb.Append(m.DeclaringType != null ? m.DeclaringType.Name : "?");
                    sb.Append('.').Append(m.Name);
                }

                Debug.LogWarning("[MoreFood/Event][NotifyStyle] ⚠ 原版 Clear() 撤掉了我们的通知: '"
                                 + __instance.titleText + "' 标记=" + mark + " | 调用栈: " + sb);
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("notify:clear", "[NotifyStyle] Clear() 守护检查异常 → 本次跳过", e);
            }
        }
    }
}
