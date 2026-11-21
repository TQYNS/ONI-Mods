// LocalGapRepair.cs — 词条缺口通用修复（正向：LocString._text → Strings 表；反向：表/快照 → _text）；根因定案见 星图.md P-23

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class LocalGapRepair
    {
        private const string TAG = "[MoreFood/Event][Compat]";

        private static bool _installed;
        private static bool _disabled;      // 熔断：修复抛异常后彻底回退默认行为

        private sealed class Entry
        {
            internal FieldInfo field;
            internal string fallbackKey;
        }

        // 全库 LocString 字段索引（一次性反射，之后复用）
        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly HashSet<string> _entryIds = new HashSet<string>(StringComparer.Ordinal);
        // 加载期抓到的「好文本」快照：key → 文本（表被污染时仍能回填 _text）
        private static readonly Dictionary<string, string> _defaults =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static FieldInfo _textField;
        private static bool _indexBuilt;

        // 诊断
        public static int LastUnrepairable { get; private set; }
        public static int LastFromTable { get; private set; }
        public static int LastFromSnapshot { get; private set; }
        public static string LastBadKeys { get; private set; }
        public static int IndexedFields { get { return _entries.Count; } }

        // 手动装配（目标缺失时不会拖垮 PatchAll）
        public static void Install(Harmony harmony)
        {
            if (_installed) return;
            _installed = true;

            try
            {
                var t = AccessTools.TypeByName("Localization");
                if (t == null) { Debug.LogWarning(TAG + " 找不到 Localization，跳过词条缺口修复"); return; }

                var pi = AccessTools.Method(typeof(LocalGapRepair), nameof(Postfix));
                if (pi == null) { Debug.LogWarning(TAG + " 找不到自身 Postfix，跳过词条缺口修复"); return; }

                // Initialize 与 LoadPreinstalledTranslation 都挂：zh_klei 的正式译文在后者里加载
                string[] targets = { "Initialize", "LoadPreinstalledTranslation" };
                int n = 0;
                for (int i = 0; i < targets.Length; i++)
                {
                    var m = AccessTools.Method(t, targets[i]);
                    if (m == null) continue;
                    harmony.Patch(m, postfix: new HarmonyMethod(pi));
                    n++;
                }

                // 显示层两处装配：拦 set_text 写入的 MISSING，覆盖整批字体切换（见 星图.md【P-23】）
                int display = 0;
                try
                {
                    var setText = AccessTools.Method(typeof(LocText), "set_text");
                    var textPost = AccessTools.Method(typeof(LocalGapRepair), nameof(LocTextText_Postfix));
                    if (setText != null && textPost != null)
                    {
                        harmony.Patch(setText, postfix: new HarmonyMethod(textPost));
                        display++;
                    }
                    else Debug.LogWarning(TAG + " 找不到 LocText.set_text，显示层拦截未装");

                    var swap = AccessTools.Method(t, "SwapToLocalizedFont", new Type[] { typeof(string) });
                    var swapPost = AccessTools.Method(typeof(LocalGapRepair), nameof(SwapFont_Postfix));
                    if (swap != null && swapPost != null)
                    {
                        harmony.Patch(swap, postfix: new HarmonyMethod(swapPost));
                        display++;
                    }
                    else Debug.LogWarning(TAG + " 找不到 Localization.SwapToLocalizedFont(string)，字体切换兜底未装");
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Patch, e, TAG + " 显示层补丁装配异常（已忽略，不影响其它收口）");
                }

                Debug.Log(TAG + " 词条缺口修复已挂：Localization." + string.Join(" / ", targets)
                          + "（实际 " + n + " 处）；显示层 " + display + " 处");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, TAG + " 词条缺口修复安装失败（已放弃，等同无补丁）");
            }
        }

        // 在所有本地化补丁之后执行（Priority.Last）：先留快照，再做正向回填
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (_disabled) return;

            try
            {
                int captured = CaptureDefaults();
                int repaired = RepairNow();
                if (captured > 0 || repaired > 0)
                    Debug.Log(TAG + " 本地化收口：快照 " + captured + " 条，回表 " + repaired + " 条");
            }
            catch (Exception e)
            {
                _disabled = true;
                MFLog.Exc(LogCat.Patch, e, TAG + " 词条缺口修复异常，已熔断（回退默认行为）");
            }
        }

        private static bool IsBad(string s)
        {
            return string.IsNullOrEmpty(s)
                   || s.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase);
        }

        // 字段索引：不再依赖 TypeByName("STRINGS")

        private static void EnsureIndex()
        {
            if (_indexBuilt) return;
            _indexBuilt = true;

            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                int asmCount = 0;

                for (int a = 0; a < asms.Length; a++)
                {
                    var asm = asms[a];
                    if (asm == null) continue;

                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e)
                    {
                        types = e.Types;
                        if (types == null) continue;
                    }
                    catch { /* expected: 反射 GetTypes 失败，跳过该程序集 */ continue; }

                    bool touched = false;
                    for (int i = 0; i < types.Length; i++)
                    {
                        var t = types[i];
                        if (t == null || !t.IsClass || !IsStringsType(t)) continue;

                        FieldInfo[] fields;
                        try
                        {
                            fields = t.GetFields(BindingFlags.Static | BindingFlags.Public
                                                 | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                        }
                        catch { /* expected: 反射 GetFields 失败，跳过该类型 */ continue; }

                        for (int k = 0; k < fields.Length; k++)
                        {
                            var f = fields[k];
                            if (f == null || f.FieldType != typeof(LocString)) continue;

                            string id = (t.FullName ?? t.Name) + "|" + f.Name;
                            if (!_entryIds.Add(id)) continue;

                            // key 以实例为准；只有实例没带 key 时才用类型全名兜底
                            string fallback = (t.FullName ?? t.Name).Replace('/', '.') + "." + f.Name;
                            _entries.Add(new Entry { field = f, fallbackKey = fallback });
                            touched = true;
                        }
                    }
                    if (touched) asmCount++;
                }

                try { _textField = AccessTools.Field(typeof(LocString), "_text"); }
                catch (Exception e) { MFLog.Exc(LogCat.Patch, e, TAG + " 取 LocString._text 失败"); }

                Debug.Log(TAG + " LocString 字段索引：" + _entries.Count + " 个 / " + asmCount
                          + " 个程序集（不再用 TypeByName(\"STRINGS\")）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, TAG + " 建立 LocString 字段索引异常");
            }
        }

        private static bool IsStringsType(Type t)
        {
            string ns = t.Namespace ?? "";
            string full = t.FullName ?? "";
            if (ns.Equals("STRINGS", StringComparison.Ordinal)
                || ns.StartsWith("STRINGS.", StringComparison.Ordinal)) return true;
            return full.IndexOf("STRINGS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static LocString Read(Entry e)
        {
            try { return e.field.GetValue(null) as LocString; }
            catch { /* expected: 反射取值失败，按缺失处理 */ return null; }
        }

        private static string KeyOf(Entry e, LocString ls)
        {
            try
            {
                string k = ls.key.String;
                if (!string.IsNullOrEmpty(k)) return k;
            }
            catch { /* expected: key 解析失败，按无 key 处理 */ }
            return e.fallbackKey;
        }

        // 快照：加载期把每个键的「好文本」记下来

        public static int CaptureDefaults()
        {
            EnsureIndex();
            int n = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                try
                {
                    var ls = Read(_entries[i]);
                    if (ls == null) continue;
                    if (IsBad(ls.text)) continue;

                    string key = KeyOf(_entries[i], ls);
                    if (string.IsNullOrEmpty(key)) continue;

                    string old;
                    if (_defaults.TryGetValue(key, out old) && string.Equals(old, ls.text, StringComparison.Ordinal))
                        continue;

                    _defaults[key] = ls.text;   // 后一次加载（中文）覆盖前一次（英文）
                    n++;
                }
                catch { /* expected: 快照读取失败，跳过该词条 */ }
            }
            return n;
        }

        // 正向：表里缺/Missing，而 _text 是好的 ⇒ 写回表

        public static int RepairNow()
        {
            EnsureIndex();
            int fixedCount = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                try
                {
                    var ls = Read(_entries[i]);
                    if (ls == null) continue;

                    string key = KeyOf(_entries[i], ls);
                    if (string.IsNullOrEmpty(key)) continue;

                    string cur = Strings.Get(key);
                    if (!IsBad(cur)) continue;

                    string text = ls.text;
                    if (IsBad(text)) continue;        // ★ 绝不把 MISSING 烤进表

                    if (!_defaults.ContainsKey(key)) _defaults[key] = text;
                    Strings.Add(key, text);
                    fixedCount++;
                }
                catch { /* expected: 词条写回失败，跳过该条 */ }
            }
            return fixedCount;
        }

        // 反向：_text 坏了 ⇒ 先用表，再用快照，最后才认输

        public static int RepairLocStringTexts()
        {
            EnsureIndex();

            LastUnrepairable = 0;
            LastFromTable = 0;
            LastFromSnapshot = 0;
            LastBadKeys = "";

            if (_textField == null)
            {
                try { _textField = AccessTools.Field(typeof(LocString), "_text"); }
                catch { /* expected: _text 字段探测失败，沿用 null */ }
            }
            if (_textField == null) return 0;

            int fixedCount = 0;
            var bad = new List<string>();

            for (int i = 0; i < _entries.Count; i++)
            {
                try
                {
                    var e = _entries[i];
                    var ls = Read(e);
                    if (ls == null) continue;

                    string key = KeyOf(e, ls);
                    if (string.IsNullOrEmpty(key)) continue;

                    string cur = ls.text;
                    if (!IsBad(cur))
                    {
                        if (!_defaults.ContainsKey(key)) _defaults[key] = cur;
                        continue;
                    }

                    string good = null;
                    try
                    {
                        string fromTable = Strings.Get(key);
                        if (!IsBad(fromTable)) { good = fromTable; LastFromTable++; }
                    }
                    catch { /* expected: 词表读取失败，沿用快照 */ }

                    if (good == null)
                    {
                        string snap;
                        if (_defaults.TryGetValue(key, out snap) && !IsBad(snap))
                        {
                            good = snap;
                            LastFromSnapshot++;
                            try { Strings.Add(key, good); } catch { /* expected: 词条写回失败，跳过 */ }
                        }
                    }

                    if (good == null)
                    {
                        LastUnrepairable++;
                        if (bad.Count < 3) bad.Add(key);
                        continue;
                    }

                    _textField.SetValue(ls, good);
                    fixedCount++;
                }
                catch { /* expected: 单条修复失败，跳过该条 */ }
            }

            LastBadKeys = bad.Count > 0 ? string.Join(", ", bad.ToArray()) : "";
            return fixedCount;
        }

        // 显示层兜底：LocText 才是按钮文字的真源头，好文本优先取 Toggle: 按钮名后缀（见 星图.md【P-23】）

        public static int LastDisplayFixed { get; private set; }
        public static int LastDisplayUnresolved { get; private set; }
        public static string LastDisplayKeys { get; private set; }
        // 21:3x 降噪：扫描规模与历史上拦下的总数，供「本局无 MISSING」的精简行使用
        public static int LastDisplayScanned { get; private set; }
        public static int LastDisplayBad { get; private set; }
        public static long DisplayBlockedTotal { get { return _displayBlockedTotal; } }

        private static bool _inDisplayRepair;
        private static long _displayBlockedTotal;
        private static int _displayWarnCount;
        // 同一个 key 反复写 MISSING 只留一条 WARNING（21:15 那局 8 条其实是 4 个键各报 2 次）
        private static readonly HashSet<string> _warnedKeys = new HashSet<string>(StringComparer.Ordinal);
        // 「本次无可补文本」这类汇总全局只报一次，避免每局字体切换刷屏
        private static bool _fontQuietReported;

        // LocText.text 的 setter 之后：拦下刚写进去的 MISSING
        public static void LocTextText_Postfix(LocText __instance)
        {
            if (_inDisplayRepair || __instance == null) return;
            try
            {
                string cur = __instance.text;
                if (string.IsNullOrEmpty(cur)
                    || !cur.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase)) return;

                _inDisplayRepair = true;
                _displayBlockedTotal++;

                string good = ResolveDisplayText(__instance, cur, null);
                if (good == null)
                {
                    WarnDisplay("LocText 拦下 MISSING 但无可用文本：key=" + (__instance.key ?? "-")
                                + " value=" + cur, (__instance.key ?? "-") + "|" + cur);
                    return;
                }

                __instance.SetText(good);
                // 顺手把好文本写回表，防后续 Awake / SwapFont 再查到 MISSING
                WriteBackTable(KeyFromMissing(cur), good);
            }
            catch (Exception e)
            {
                WarnDisplay("LocText 显示层修复异常: " + e);
            }
            finally { _inDisplayRepair = false; }
        }

        // 整批字体/语言切换之后：全量补救（SwapFont 会重算所有带 key 的 LocText）
        // 21:3x 降噪：只有「真的修好了」才打详细行；「全是原版空占位、无文本可补」
        // 全局只报一次（那些键本来就没有译文，不是回归）；完全干净的切换保持静默。
        // 每局的可观测性由 ManagementMenuLocGuard.Postfix 的精简行保证，不靠这里刷屏。
        public static void SwapFont_Postfix()
        {
            try
            {
                int n = RepairVisibleLocTexts();
                if (n > 0)
                {
                    MFLog.Info(LogCat.UI, "[MoreFood/Loc] 字体切换后显示层兜底：修复 " + n
                                        + " 条；" + LastDisplayKeys);
                }
                else if (LastDisplayBad > 0 && !_fontQuietReported)
                {
                    _fontQuietReported = true;
                    MFLog.Info(LogCat.UI, "[MoreFood/Loc] 字体切换后显示层兜底：本次 "
                                        + LastDisplayBad + " 条 MISSING 无可用文本（原版空占位，非顶部栏），"
                                        + "后续同类切换不再逐条打日志；累计拦下 "
                                        + DisplayBlockedTotal + " 条");
                }
            }
            catch (Exception e)
            {
                WarnDisplay("字体切换兜底异常: " + e);
            }
        }

        // 扫描当前所有 LocText，把已经显示成 MISSING 的补回好文本
        public static int RepairVisibleLocTexts()
        {
            LastDisplayFixed = 0;
            LastDisplayUnresolved = 0;
            LastDisplayScanned = 0;
            LastDisplayBad = 0;
            LastDisplayKeys = "";
            var fixedKeys = new List<string>();   // 真修好的样例（关键判据，优先保留）
            var badKeys = new List<string>();     // 无可用文本样例（只看前 3 个）
            // 归位/重建中间态下，多个 LocText 可能暂时共享同一个 Toggle 祖先，
            // 导致 ToggleNameText 对所有按钮返回同一个名字（如"状态"）。
            // 同一轮修复中，每个 Toggle 名只允许用于一个 LocText，重复则降级到词表。
            var usedToggleNames = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                var all = Resources.FindObjectsOfTypeAll<LocText>();
                for (int i = 0; i < all.Length; i++)
                {
                    var lt = all[i];
                    if (lt == null) continue;
                    LastDisplayScanned++;

                    string cur;
                    try { cur = lt.text; } catch { /* expected: LocText 读取失败，跳过该行 */ continue; }
                    if (string.IsNullOrEmpty(cur)
                        || !cur.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase)) continue;
                    LastDisplayBad++;

                    string label = lt.key ?? KeyFromMissing(cur) ?? "?";
                    string good = ResolveDisplayText(lt, cur, usedToggleNames);
                    if (good == null)
                    {
                        LastDisplayUnresolved++;
                        if (badKeys.Count < 3 && !string.IsNullOrEmpty(label)) badKeys.Add(label);
                        continue;
                    }

                    try
                    {
                        _inDisplayRepair = true;
                        lt.SetText(good);
                        WriteBackTable(KeyFromMissing(cur), good);
                        LastDisplayFixed++;
                        if (fixedKeys.Count < 3) fixedKeys.Add(label + "→" + good);
                    }
                    catch { /* expected: 单条显示层修复失败，跳过 */ }
                    finally { _inDisplayRepair = false; }
                }
            }
            catch (Exception e)
            {
                WarnDisplay("全量 LocText 兜底异常: " + e);
            }

            LastDisplayKeys = fixedKeys.Count > 0
                ? string.Join(" | ", fixedKeys.ToArray())
                : (badKeys.Count > 0 ? "无可用文本样例 " + string.Join(" | ", badKeys.ToArray()) : "");
            return LastDisplayFixed;
        }

        // 好文本来源优先级：按钮名后缀 → Strings 表 → 加载期快照 → LocText.key 再查一轮
        private static string ResolveDisplayText(LocText lt, string missingValue, HashSet<string> usedToggleNames)
        {
            string fromName = ToggleNameText(lt);
            if (!IsBad(fromName))
            {
                // 同一轮修复中该 Toggle 名已被其他 LocText 占用 ⇒ 结构处于中间态，降级到词表
                if (usedToggleNames != null && !usedToggleNames.Add(fromName))
                    fromName = null;
            }
            if (!IsBad(fromName)) return fromName;

            string key = KeyFromMissing(missingValue);
            string t = TableText(key);
            if (!IsBad(t)) return t;

            string snap;
            if (!string.IsNullOrEmpty(key) && _defaults.TryGetValue(key, out snap) && !IsBad(snap))
                return snap;

            string lk = null;
            try { lk = lt.key; } catch { /* expected: key 探测失败，沿用空 key */ }
            if (!string.IsNullOrEmpty(lk) && lk != key)
            {
                t = TableText(lk);
                if (!IsBad(t)) return t;
                if (_defaults.TryGetValue(lk, out snap) && !IsBad(snap)) return snap;
            }
            return null;
        }

        private static string TableText(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try { return Strings.Get(key); }
            catch { /* expected: 词表缺失，按无文本处理 */ return null; }
        }

        private static void WriteBackTable(string key, string good)
        {
            if (string.IsNullOrEmpty(key) || IsBad(good)) return;
            try { Strings.Add(key, good); } catch { /* expected: 词条写回失败，跳过 */ }
        }

        // "MISSING.STRINGS.UI.CONSUMABLES" → "STRINGS.UI.CONSUMABLES"
        private static string KeyFromMissing(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int i = value.IndexOf('.');
            if (i > 0 && i < value.Length - 1) return value.Substring(i + 1);
            return null;
        }

        // KIconToggleMenu.RefreshButtons 用 toggleInfo.text 给按钮命名："Toggle:优先级"
        private static string ToggleNameText(LocText lt)
        {
            try
            {
                Transform t = lt.transform;
                for (int i = 0; i < 6 && t != null; i++, t = t.parent)
                {
                    string n = t.gameObject.name;
                    if (!string.IsNullOrEmpty(n) && n.StartsWith("Toggle:", StringComparison.Ordinal))
                        return n.Substring(7);
                }
            }
            catch { /* expected: 控件层级探测失败，按无名称处理 */ }
            return null;
        }

        private static void WarnDisplay(string msg)
        {
            WarnDisplay(msg, null);
        }

        // dedupKey 非空时同一个键只留第一条 WARNING；异常类消息（key 为空）仍按次数上限保护
        private static void WarnDisplay(string msg, string dedupKey)
        {
            try
            {
                if (!string.IsNullOrEmpty(dedupKey))
                {
                    if (_warnedKeys.Contains(dedupKey)) return;
                    _warnedKeys.Add(dedupKey);
                }
                if (_displayWarnCount >= 8) return;
                _displayWarnCount++;
                Debug.LogWarning(TAG + " " + msg + "（累计拦下 " + _displayBlockedTotal + " 条）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, TAG + " 显示层诊断写日志失败");
            }
        }
    }
}
