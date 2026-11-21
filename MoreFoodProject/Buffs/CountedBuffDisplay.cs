// CountedBuffDisplay.cs — 计数类 buff 的 tooltip 展示工厂（Row/Span/SpanStyle 三级 + 补丁本体）

using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    // TMP 字体资源名常量
    public static class TmpFonts
    {
        public const string LiberationSans = "LiberationSans SDF";
        public const string LiberationSansFallback = "LiberationSans SDF - Fallback";
        public const string NotoSansRegular = "NotoSans-Regular";
        public const string NotoSansUnderlay = "NotoSans-Regular-Underlay";
        public const string NotoSansSkinQuantity = "NotoSans-SkinQuantityLabel";
        public const string NotoSansCJKjp = "NotoSansCJKjp-Regular";
        public const string NotoSansCJKkr = "NotoSansCJKkr-Regular";
        public const string NotoSansCJKsc = "NotoSansCJKsc-Regular";
        public const string RobotoCondensed = "RobotoCondensed-Regular";
        public const string GrayStrokeRegular = "GRAYSTROKE REGULAR SDF";
        public const string EconomicaBold = "Economica-Bold-OTF SDF";
        public const string GrayStrokeItemDrop = "GRAYSTROKE ITEM DROP SDF";
        public const string GrayStrokeName = "GRAYSTROKE NAME SDF";
        public const string GrayStrokeOutline = "GRAYSTROKE OUTLINE SDF";
    }

    // 片段样式：链式设定颜色 / 字体 / 加粗 / 斜体 / 下划线 / 字号
    public class SpanStyle
    {
        // 颜色（RichText 色值）
        private string _color;
        // 字体资源名
        private string _font;
        // 加粗
        private bool _bold;
        // 斜体
        private bool _italic;
        // 下划线
        private bool _underline;
        // 字号百分比（100 = 默认）
        private int _sizePercent;

        public SpanStyle() { }

        // 链式 API

        public SpanStyle Color(string hexOrName) { _color = hexOrName; return this; }

        // 设置片段颜色（直接给 Color，内部转 #RRGGBB —— 只支持十六进制色）
        public SpanStyle Color(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            _color = "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
            return this;
        }

        // ⚠ 已实测：ONI 当前 TMP 版本不支持运行时通过 <font="..."> 切换字体资源
        public SpanStyle Font(string fontResourceName) { _font = fontResourceName; return this; }

        public SpanStyle Bold(bool on = true) { _bold = on; return this; }
        public SpanStyle Italic(bool on = true) { _italic = on; return this; }
        public SpanStyle Underline(bool on = true) { _underline = on; return this; }

        // 字号百分比；100 = 默认。常用 110~120 做"醒目"效果。
        public SpanStyle Size(int percent) { _sizePercent = percent; return this; }

        // 内部：把文本包成 TMP 富文本

        internal string Wrap(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string s = text;
            if (!string.IsNullOrEmpty(_font)) s = "<font=\"" + _font + "\">" + s + "</font>";
            if (_sizePercent > 0 && _sizePercent != 100) s = "<size=" + _sizePercent + "%>" + s + "</size>";
            if (_bold) s = "<b>" + s + "</b>";
            if (_italic) s = "<i>" + s + "</i>";
            if (_underline) s = "<u>" + s + "</u>";
            if (!string.IsNullOrEmpty(_color)) s = "<color=" + _color + ">" + s + "</color>";
            return s;
        }
    }

    // 注册表
    public static class CountedBuffDisplay
    {
        private static readonly Dictionary<string, CountedBuffBuilder> _entries
            = new Dictionary<string, CountedBuffBuilder>();

        // 注册某个 effect 的 tooltip 构造器（回调里用 Row / Span / Line 拼行；未注册的 effect 走原版显示）
        public static void Register(string effectId, Action<CountedBuffBuilder> configure)
        {
            if (string.IsNullOrEmpty(effectId)) return;

            var b = new CountedBuffBuilder();
            try { configure?.Invoke(b); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e,
                    "[MoreFood/Buff] CountedBuffDisplay.Register(" + effectId
                    + ") 配置异常（已忽略，仍按默认渲染）");
            }
            _entries[effectId] = b;
        }

        // 极简注册：只加一行「特殊：<文本>」，可带颜色（"yellow" / "#FFD700" / Color 均可）
        public static void Register(string effectId, string specialText, string color = null)
            => Register(effectId, b => b.Line("特殊", specialText, color));

        // 同上一版，但颜色直接用 Color 结构（省掉字符串解析）
        public static void Register(string effectId, string specialText, Color color)
            => Register(effectId, specialText, CountedBuffBuilder.ColorToHex(color));

        // 判断该 effect 是否已注册自定义 tooltip（未注册的走原版效果显示）
        public static bool IsRegistered(string effectId)
            => !string.IsNullOrEmpty(effectId) && _entries.ContainsKey(effectId);

        public static void Clear() => _entries.Clear();

        // 取回该 effect 的 tooltip 构造器；未注册返回 false 且 builder 置 null
        internal static bool TryGet(string effectId, out CountedBuffBuilder builder)
        {
            builder = null;
            if (string.IsNullOrEmpty(effectId)) return false;
            return _entries.TryGetValue(effectId, out builder) && builder != null;
        }

        // 用 BuffDef 合成一个 Effect，供物品详情面板等"没有 EffectInstance"的场合渲染。
        public static bool TryBuildTooltip(string effectId, GameObject go, out string text)
        {
            text = null;
            CountedBuffBuilder builder;
            if (!TryGet(effectId, out builder)) return false;

            BuffDef def = BuffConfig.Get(effectId);
            if (def == null) return false;

            text = builder.Build(def.Build(), go);
            return !string.IsNullOrEmpty(text);
        }
    }

    // 行构建器：一个 Row = 一个前缀 + 若干 Span
    public class RowBuilder
    {
        // 每行的前缀文本
        private readonly string _prefix;
        private readonly List<Func<GameObject, string>> _spans
            = new List<Func<GameObject, string>>();

        internal RowBuilder(string prefix)
        {
            _prefix = prefix ?? "\n    • ";
        }

        // Span：旧 API（保留，向后兼容）

        public RowBuilder Span(string text, string color = null)
        {
            string c = color; string t = text;
            _spans.Add(_ => WrapColor(t, c));
            return this;
        }

        // 追加一个「动态文本」片段（按目标实时算，如剩余次数），可带颜色
        public RowBuilder Span(Func<GameObject, string> textFn, string color = null)
        {
            if (textFn == null) return this;
            string c = color;
            _spans.Add(go => WrapColor(SafeInvoke(textFn, go), c));
            return this;
        }

        // 追加一个静态文本片段，指定颜色
        public RowBuilder Span(string text, Color color)
            => Span(text, CountedBuffBuilder.ColorToHex(color));

        // 追加一个动态文本片段（按目标实时算），指定颜色
        public RowBuilder Span(Func<GameObject, string> textFn, Color color)
            => Span(textFn, CountedBuffBuilder.ColorToHex(color));

        // Span：新 API（样式化）

        // 追加一段带 SpanStyle 的静态文本
        public RowBuilder Span(string text, SpanStyle style)
        {
            if (style == null) return Span(text, (string)null);
            string t = text; var st = style;
            _spans.Add(_ => st.Wrap(t));
            return this;
        }

        // 追加一段带 SpanStyle 的动态文本
        public RowBuilder Span(Func<GameObject, string> textFn, SpanStyle style)
        {
            if (textFn == null) return this;
            if (style == null) return Span(textFn, (string)null);
            var st = style;
            _spans.Add(go => st.Wrap(SafeInvoke(textFn, go)));
            return this;
        }

        // Label

        public RowBuilder Label(string label, string color = null)
            => Span((label ?? "") + "：", color);

        // 追加标签片段（标签本身着色；冒号与正文另用 Span 追加）
        public RowBuilder Label(string label, Color color)
            => Label(label, CountedBuffBuilder.ColorToHex(color));

        // 同上一版，但样式用 SpanStyle（颜色 / 字体 / 加粗等组合）
        public RowBuilder Label(string label, SpanStyle style)
            => Span((label ?? "") + "：", style);

        // 内部

        internal string Build(GameObject go)
        {
            var sb = new StringBuilder();
            sb.Append(_prefix);
            for (int i = 0; i < _spans.Count; i++)
            {
                try { sb.Append(_spans[i](go)); }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] RowBuilder.Span 渲染异常（已忽略）");
                }
            }
            return sb.ToString();
        }

        private static string SafeInvoke(Func<GameObject, string> fn, GameObject go)
        {
            try { return fn(go) ?? ""; }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] Span 动态值异常（已忽略）");
                return "";
            }
        }

        // 把文本包成 <color=#RRGGBB>…</color>；颜色为空则原样返回
        internal static string WrapColor(string text, string color)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (string.IsNullOrEmpty(color)) return text;
            return "<color=" + color + ">" + text + "</color>";
        }
    }

    // buff 级 Builder：组织多个 Row
    public class CountedBuffBuilder
    {
        // 标题（通常 = buff 名）
        public string title = null;
        // 标题颜色（RichText 色值）
        public string titleColor = null;
        // 属性段的表头文字
        public string header = "效果：";
        // 是否显示属性数值行
        public bool showAttributeLines = true;
        // 属性行颜色
        public string attributeColor = null;
        // 每行前缀（默认换行 + 缩进点）
        public string linePrefix = "\n    • ";

        private readonly List<Func<GameObject, string>> _rowFuncs
            = new List<Func<GameObject, string>>();
        private readonly HashSet<string> _hiddenAttributes
            = new HashSet<string>(StringComparer.Ordinal);

        // 隐藏某个原生属性行（格式化不好看 / 已在自定义行里另行说明时用）。
        public CountedBuffBuilder HideAttribute(string attributeId)
        {
            if (!string.IsNullOrEmpty(attributeId)) _hiddenAttributes.Add(attributeId);
            return this;
        }

        // Row / 便捷方法

        public CountedBuffBuilder Row(Action<RowBuilder> configure)
        {
            var rb = new RowBuilder(linePrefix);
            try { configure?.Invoke(rb); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] Row(...) 配置异常（已忽略）");
            }
            _rowFuncs.Add(go => rb.Build(go));
            return this;
        }

        // 新建一行（可选给整行一个默认颜色），返回 RowBuilder 继续 .Span / .Label
        public CountedBuffBuilder Row(string text, string color = null)
            => Row(r => r.Span(text, color));

        // 「标签：值」简写（静态）
        public CountedBuffBuilder Line(string label, string value, string color = null)
            => Row(r => r.Label(label, color).Span(value, color));

        // 一行「标签：动态值」（值按目标实时算），返回自身继续链式
        public CountedBuffBuilder Line(string label, Func<GameObject, string> valueFn, string color = null)
            => Row(r => r.Label(label, color).Span(valueFn, color));

        // 一行「标签：静态值」，指定颜色
        public CountedBuffBuilder Line(string label, string value, Color color)
            => Line(label, value, ColorToHex(color));

        // 一行「标签：动态值」，指定颜色
        public CountedBuffBuilder Line(string label, Func<GameObject, string> valueFn, Color color)
            => Line(label, valueFn, ColorToHex(color));

        // 「标签：值」简写（SpanStyle 版）
        public CountedBuffBuilder Line(string label, string value, SpanStyle style)
            => Row(r => r.Label(label, style).Span(value, style));

        // 一行「标签：动态值」，值用 SpanStyle 样式
        public CountedBuffBuilder Line(string label, Func<GameObject, string> valueFn, SpanStyle style)
            => Row(r => r.Label(label, style).Span(valueFn, style));

        // 无「标签：」的纯文本行
        public CountedBuffBuilder Bullet(string text, string color = null)
            => Row(r => r.Span(text, color));

        // 追加一个「• 动态文本」要点行（行首自带 •）
        public CountedBuffBuilder Bullet(Func<GameObject, string> textFn, string color = null)
            => Row(r => r.Span(textFn, color));

        // 追加一个「• 静态文本」要点行，指定颜色
        public CountedBuffBuilder Bullet(string text, Color color)
            => Bullet(text, ColorToHex(color));

        // 原样追加（自己管换行和 RichText，不加前缀）
        public CountedBuffBuilder Raw(string text) => Raw(_ => text);

        // 追加一段不加 • 前缀的原始文本（整行接管，用于自绘格式）
        public CountedBuffBuilder Raw(Func<GameObject, string> textFn)
        {
            if (textFn == null) return this;
            _rowFuncs.Add(go =>
            {
                try { return textFn(go) ?? ""; }
                catch { return ""; }
            });
            return this;
        }

        // 颜色工具
        internal static string ColorToHex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        // 渲染
        internal string Build(EffectInstance inst, GameObject go)
        {
            return Build(inst != null ? inst.effect : null, go);
        }

        // 按注册的构造器拼出该 buff 的完整 tooltip 文本（构造器缺失/异常时返回 null 交回原版）
        internal string Build(Effect effect, GameObject go)
        {
            if (effect == null) return "";

            var body = new StringBuilder();

            if (showAttributeLines) AppendAttributeLines(body, effect);

            for (int i = 0; i < _rowFuncs.Count; i++)
            {
                try { body.Append(_rowFuncs[i](go)); }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] CountedBuffBuilder.Row 渲染异常（已忽略）");
                }
            }

            var sb = new StringBuilder();
            string head = string.IsNullOrEmpty(title) ? effect.Name : title;
            if (!string.IsNullOrEmpty(titleColor))
                head = "<color=" + titleColor + ">" + head + "</color>";
            sb.Append(head);

            if (body.Length > 0)
            {
                sb.Append("\n").Append(header);
                sb.Append(body);
            }

            return sb.ToString();
        }

        private void AppendAttributeLines(StringBuilder body, Effect effect)
        {
            var mods = effect.SelfModifiers;
            if (mods == null) return;

            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];
                if (mod == null) continue;
                if (_hiddenAttributes.Contains(mod.AttributeId)) continue;

                Klei.AI.Attribute attr = AttributeModifier.FetchAttribute(mod.AttributeId);
                if (attr == null || attr.ShowInUI == Klei.AI.Attribute.Display.Never) continue;

                string name = mod.GetName();
                if (string.IsNullOrEmpty(name)) continue;

                string value = mod.GetFormattedString();
                var rb = new RowBuilder(linePrefix);
                rb.Label(name, attributeColor);
                rb.Span(value, attributeColor);
                body.Append(rb.Build(null));
            }
        }
    }

    // 补丁本体
    [HarmonyPatch(typeof(EffectInstance), "ResolveTooltip")]
    public static class EffectInstance_ResolveTooltip_CountedBuff_Patch
    {
        // 补丁：EffectInstance.GetTooltip 之后，用我们注册的构造器覆写 tooltip 文本
        public static void Postfix(EffectInstance __instance, ref string __result)
        {
            try
            {
                if (__instance == null || __instance.effect == null) return;

                GameObject go = __instance.gameObject;
                if (go == null) return;

                CountedBuffBuilder builder;
                if (!CountedBuffDisplay.TryGet(__instance.effect.Id, out builder)) return;

                __result = builder.Build(__instance, go);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] CountedBuffTooltip 重写异常（已忽略）");
            }
        }
    }
}
