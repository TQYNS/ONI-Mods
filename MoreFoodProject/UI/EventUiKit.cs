// EventUiKit.cs — 宿主自备的 UI 构建工具箱（自事件框架的同名实现整体复制，行为一致）
//
// 为什么宿主自己留一份：这是纯游戏 UI 反射（LocText / KButton / Image），不含任何框架状态；
// 宿主自己的界面（卷轴阅读窗口）要复用它，但不应因此在编译期依赖框架实现程序集。

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    public static class EventUiKit
    {
        // 配色（取自原版实测值，Doc-V §T12.2 / §T8.0）
        // 饮食行 BG：#586079
        public static readonly Color BgDiet    = new Color(0.346f, 0.374f, 0.485f, 1f);

        // 「缺模板」警告只打一次
        private static bool _warnedNoTemplate;

        // 基础节点
        public static GameObject MakeNode(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        // 取 GameObject 的 RectTransform（没有返回 null）
        public static RectTransform RT(GameObject go)
        {
            return go != null ? go.transform as RectTransform : null;
        }

        // 建一个纯色背景块（拉伸填满父节点）
        public static GameObject MakeBg(string name, Transform parent, Color c)
        {
            var go = MakeNode(name, parent);
            var rt = RT(go);
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = true;
            return go;
        }

// 绝对定位工具箱
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = MakeNode(name, parent);
            var rt = RT(go);
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }
            return rt;
        }

        // 左上角原点的绝对摆放：x 向右、y 向下（(0,0) = 父节点左上角）。
        public static RectTransform Place(GameObject go, float x, float y, float w, float h)
        {
            var rt = RT(go);
            if (rt == null) return null;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            return rt;
        }

        // 拉伸填满父节点（四边可内缩）
        public static RectTransform Stretch(GameObject go, float l = 0f, float t = 0f,
                                            float r = 0f, float b = 0f)
        {
            var rt = RT(go);
            if (rt == null) return null;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        // 把 root 的所有后代矩形都拉成"铺满自己的父节点"（不改 root 自己）
        public static void StretchAllChildren(GameObject root)
        {
            if (root == null) return;
            try
            {
                var all = root.GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || all[i].gameObject == root) continue;
                    all[i].anchorMin = Vector2.zero;
                    all[i].anchorMax = Vector2.one;
                    all[i].offsetMin = Vector2.zero;
                    all[i].offsetMax = Vector2.zero;
                }
            }
            catch { /* expected: 个别节点锚点设置失败不影响其余节点 */ }
        }

        // 拆掉子树上的布局组件。先 enabled=false（本帧立即退出布局回算），
        public static void StripLayout(GameObject root, bool includeSelf = true)
        {
            if (root == null) return;
            try
            {
                var all = root.GetComponentsInChildren<Behaviour>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var b = all[i];
                    if (b == null) continue;
                    if (!(b is LayoutGroup) && !(b is ContentSizeFitter)) continue;
                    if (!includeSelf && b.gameObject == root) continue;
                    b.enabled = false;
                    UnityEngine.Object.Destroy(b);
                }
            }
            catch { /* expected: 个别布局组件销毁失败不影响其余 */ }
        }

        // 渐变（运行时生成 2×2 纹理，不依赖任何美术资源）
        private static readonly Dictionary<string, Sprite> _gradCache = new Dictionary<string, Sprite>();

        // 竖向渐变 Sprite（上=top，下=bottom）。同一组颜色复用同一张纹理。
        public static Sprite GradientSprite(Color top, Color bottom)
        {
            return GradientSpriteEx(top, bottom, false);
        }

        // 横向渐变 Sprite（左=left，右=right）。标题下划线 / 分节线用。
        public static Sprite GradientSpriteH(Color left, Color right)
        {
            return GradientSpriteEx(left, right, true);
        }

        private static Sprite GradientSpriteEx(Color ca, Color cb, bool horizontal)
        {
            var a32 = (Color32)ca;
            var b32 = (Color32)cb;
            string key = (horizontal ? "H" : "V") + "|"
                       + a32.r + "," + a32.g + "," + a32.b + "," + a32.a
                       + "|" + b32.r + "," + b32.g + "," + b32.b + "," + b32.a;

            Sprite cached;
            if (_gradCache.TryGetValue(key, out cached) && cached != null) return cached;

            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.wrapMode   = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags  = HideFlags.HideAndDontSave;

                if (horizontal)
                {
                    // 纹理 x=0 是【左侧】
                    tex.SetPixel(0, 0, ca); tex.SetPixel(0, 1, ca);
                    tex.SetPixel(1, 0, cb); tex.SetPixel(1, 1, cb);
                }
                else
                {
                    // 纹理 y=0 是【底部】
                    tex.SetPixel(0, 0, cb); tex.SetPixel(1, 0, cb);
                    tex.SetPixel(0, 1, ca); tex.SetPixel(1, 1, ca);
                }
                tex.Apply(false, false);

                var sp = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
                sp.hideFlags = HideFlags.HideAndDontSave;
                _gradCache[key] = sp;
                return sp;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 渐变纹理生成失败，退回纯色");
                return null;
            }
        }

        // 竖向渐变块（MakeGradient 的显式别名，和 MakeGradientH 对称）
        public static Image MakeGradientV(string name, Transform parent, Color top, Color bottom)
        {
            return MakeGradient(name, parent, top, bottom);
        }

        // 横向渐变块
        public static Image MakeGradientH(string name, Transform parent, Color left, Color right)
        {
            var go = MakeNode(name, parent);
            var img = go.AddComponent<Image>();
            var sp = GradientSpriteH(left, right);
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.color = left;
            }
            img.raycastTarget = false;
            return img;
        }

        // 以【中心点】摆放，并可选绕 Z 轴旋转。
        public static RectTransform PlaceCentered(GameObject go, float cx, float cy,
                                                  float w, float h, float rotZ = 0f)
        {
            var rt = RT(go);
            if (rt == null) return null;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return rt;
        }

        // 菱形：外圈 + 内芯（内芯色填掉中间，剩下的就是一圈描边）
        public static Image MakeDiamond(string name, Transform parent, float cx, float cy,
                                        float side, Color c, Color? inner = null,
                                        float innerShrink = 6f)
        {
            var outer = MakeSolid(name, parent, c);
            PlaceCentered(outer.gameObject, cx, cy, side, side, 45f);
            if (inner.HasValue)
            {
                var i = MakeSolid(name + "_In", parent, inner.Value);
                PlaceCentered(i.gameObject, cx, cy,
                              Mathf.Max(2f, side - innerShrink), Mathf.Max(2f, side - innerShrink), 45f);
            }
            return outer;
        }

        // 纯色块（不拉伸、不定位；自己再 Place/Stretch）
        public static Image MakeSolid(string name, Transform parent, Color c)
        {
            var go = MakeNode(name, parent);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        // 竖向渐变块（不拉伸、不定位；自己再 Place/Stretch）
        public static Image MakeGradient(string name, Transform parent, Color top, Color bottom)
        {
            var go = MakeNode(name, parent);
            var img = go.AddComponent<Image>();
            var sp = GradientSprite(top, bottom);
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.color = top;      // 退化：纯色
            }
            img.raycastTarget = false;
            return img;
        }

        // 文本
        private static System.Reflection.PropertyInfo _wrapProp;
        // 是否已探测过该属性
        private static bool _wrapProbed;
        // 该属性是枚举（新 TMP）还是 bool（旧 TMP）
        private static bool _wrapIsEnum;

        // 开关自动换行。新版 TMP 用 textWrappingMode（枚举），旧版用 enableWordWrapping（bool）
        public static void SetWrap(LocText lt, bool wrap)
        {
            if (lt == null) return;
            try
            {
                if (!_wrapProbed)
                {
                    _wrapProbed = true;
                    var t = typeof(TMP_Text);
                    _wrapProp = t.GetProperty("textWrappingMode")
                             ?? t.GetProperty("enableWordWrapping");
                    _wrapIsEnum = _wrapProp != null && _wrapProp.PropertyType.IsEnum;
                }
                if (_wrapProp == null || !_wrapProp.CanWrite) return;
                object v = _wrapIsEnum
                           ? Enum.ToObject(_wrapProp.PropertyType, wrap ? 1 : 0)
                           : (object)wrap;
                _wrapProp.SetValue(lt, v, null);
            }
            catch { /* expected: 换行属性不可写时保持原版默认 */ }
        }

        // 建一个"配置好但还没定位"的文本；返回值是【模板根节点】（LocText 由 out 参数给出）
        public static GameObject MakeFixedLabel(string name, Transform parent, string text,
                                                Color color, float fontSize,
                                                TextAlignmentOptions align, out LocText lt)
        {
            lt = null;
            GameObject root = null;

            try
            {
                var prefab = GetWidget("Label");
                if (prefab != null)
                {
                    root = Util.KInstantiateUI(prefab, parent != null ? parent.gameObject : null, true);
                    if (root != null)
                    {
                        root.name = name;
                        lt = root.GetComponentInChildren<LocText>();
                    }
                }
            }
            catch (Exception e)
            {
                WarnNoTemplate(e, "Label 模板实例化失败");
            }

            if (root == null)
            {
                root = MakeNode(name, parent);
                lt = root.AddComponent<LocText>();     // 可能没字体，但不会崩
            }

            try
            {
                if (lt != null)
                {
                    lt.SetText(text ?? "");
                    CleanExtraLocTexts(root, lt);
                    StripLayout(root, true);
                    ApplyTextStyle(lt, color, fontSize, align);
                    SetWrap(lt, true);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 配置文本失败");
            }

            return root;
        }

        // 统一设置文本外观。★ 必须关掉 enableAutoSizing：
        public static void ApplyTextStyle(LocText lt, Color color, float fontSize,
                                          TextAlignmentOptions align)
        {
            if (lt == null) return;
            try
            {
                lt.enableAutoSizing = false;
                lt.color     = color;
                lt.fontSize  = fontSize;
                lt.alignment = align;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 设置文本外观失败");
            }
        }

        // 把 MakeFixedLabel 建出来的文本摆到 (x,y,w,h)（左上角原点，相对父节点）
        public static void FitLabel(GameObject root, LocText lt, float x, float y, float w, float h)
        {
            try
            {
                if (root != null) Place(root, x, y, w, h);
                StretchAllChildren(root);
                if (lt != null) Place(lt.gameObject, 0f, 0f, w, h);
            }
            catch { /* expected: 定位失败不影响文本内容本身 */ }
        }

        // 量一段文字在给定宽度下要占多高（TMP preferredHeight）。
        public static float MeasureHeight(LocText lt, float width, float lineH)
        {
            if (lt == null) return lineH + 4f;
            try
            {
                SetWrap(lt, true);
                var rt = lt.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot     = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(width, 0f);
                lt.ForceMeshUpdate();
                float h = lt.preferredHeight;
                if (h > 1f) return Mathf.Ceil(h) + 4f;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 文字测高失败，用估算");
            }

            float fs = 15f;
            try { fs = lt.fontSize; } catch { /* expected: 字号读取失败按默认 15 估算 */ }
            string s = "";
            try { s = lt.text ?? ""; } catch { /* expected: 文本读取失败按空串估算 */ }

            float per = Mathf.Max(1f, fs * 0.62f);
            int perLine = Mathf.Max(1, Mathf.FloorToInt(width / per));
            // 手工换行符单独算
            int lines = 0;
            string[] segs = s.Split('\n');
            for (int i = 0; i < segs.Length; i++)
                lines += Mathf.Max(1, Mathf.CeilToInt((float)segs[i].Length / perLine));
            return Mathf.Max(1, lines) * Mathf.Max(lineH, fs * 1.35f) + 4f;
        }

        // 布局
        public static VerticalLayoutGroup VLayout(GameObject go, float spacing,
                                                  RectOffset padding = null)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(10, 10, 8, 8);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, float spacing,
                                                    RectOffset padding = null)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            return h;
        }

        // 给节点加 ContentSizeFitter（默认只自适应高度）
        public static ContentSizeFitter Fit(GameObject go, bool vertical = true, bool horizontal = false)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.verticalFit   = vertical   ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        public static LayoutElement LE(GameObject go, float height = -1f, float width = -1f,
                                       float minH = -1f, float prefH = -1f)
        {
            var e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (height > 0f) e.preferredHeight = height;
            if (width  > 0f) e.preferredWidth  = width;
            if (minH   > 0f) e.minHeight = minH;
            if (prefH  > 0f) e.preferredHeight = prefH;
            return e;
        }

        // 文本
        public static LocText MakeLabel(string name, Transform parent, string text,
                                        Color? color = null, int fontSize = 0)
        {
            LocText lt = null;

            try
            {
                var prefab = GetWidget("Label");
                if (prefab != null)
                {
                    var go = Util.KInstantiateUI(prefab, parent != null ? parent.gameObject : null, true);
                    if (go != null)
                    {
                        go.name = name;
                        lt = go.GetComponentInChildren<LocText>();
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] Label 模板实例化失败，降级");
            }

            if (lt == null)
            {
                var go = MakeNode(name, parent);
                lt = go.AddComponent<LocText>();
            }

            try
            {
                lt.SetText(text ?? "");
                CleanExtraLocTexts(lt.gameObject, lt);   // ★ 清掉模板自带的 "Sub Label" 等
                if (color.HasValue) lt.color = color.Value;
                // ⚠ 不设 fontSize：LocText 的字号 API 各版本不同，写死会编译不过
                if (fontSize > 0) { /* 预留 */ }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 设置文本失败");
            }

            return lt;
        }

        // 按钮
        public static KButton MakeButton(string name, Transform parent, string text,
                                         System.Action onClick, float height = 28f)
        {
            KButton btn = null;
            GameObject go = null;

            try
            {
                var prefab = GetWidget("ButtonLabel");
                if (prefab != null)
                {
                    go = Util.KInstantiateUI(prefab, parent != null ? parent.gameObject : null, true);
                    if (go != null)
                    {
                        go.name = name;
                        btn = go.GetComponent<KButton>();
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] ButtonLabel 模板实例化失败，降级");
            }

            // 降级：裸 KButton（OnSpawn 不跑，但 onClick 是 public 字段，鼠标点击仍会触发）
            if (btn == null)
            {
                go = MakeNode(name, parent);
                MakeBg(name + "_bg", go.transform, BgDiet);
                btn = go.AddComponent<KButton>();
            }

            if (go != null && height > 0f) LE(go, height);

            try
            {
                // ⚠ KButton.onClick 是【event】，不能整体赋值，只能 += / -=
                if (btn != null && onClick != null)
                    btn.onClick += onClick;

                if (go != null)
                {
                    var lt = go.GetComponentInChildren<LocText>();
                    if (lt != null)
                    {
                        lt.SetText(text ?? "");
                        CleanExtraLocTexts(go, lt);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 设置按钮失败");
            }

            return btn;
        }

        // 清掉模板自带的其余文本。
        public static void CleanExtraLocTexts(GameObject root, LocText keep)
        {
            if (root == null) return;
            try
            {
                var all = root.GetComponentsInChildren<LocText>(true);
                if (all == null) return;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || all[i] == keep) continue;
                    all[i].SetText("");
                }
            }
            catch { /* expected: 清理多余文本失败不影响本行显示 */ }
        }

        // ★ 报告式行 —— 复用 ReportScreen 的行模板（外观 100% 与原版「每日报告」一致）

        // 建一行报告式行。取不到模板返回 null（调用方自行降级）。
        public enum ReportRowKind { Line, Header, Spacer }
        public static GameObject MakeReportRow(Transform parent, ReportRowKind kind)
        {
            try
            {
                var rs = ReportScreen.Instance;
                if (rs == null) return null;

                string fieldName = kind == ReportRowKind.Header ? "lineItemHeader"
                                 : kind == ReportRowKind.Spacer ? "lineItemSpacer"
                                 : "lineItem";

                var prefab = GetReportObjectField(rs, fieldName);
                if (prefab == null) return null;

                var go = Util.KInstantiateUI(prefab, parent != null ? parent.gameObject : null, true);
                if (go != null) go.name = "Row_" + kind;
                return go;
            }
            catch (Exception e)
            {
                WarnNoTemplate(e, "ReportScreen 行模板取用失败");
                return null;
            }
        }

        // 反射取 ReportScreen 上的 GameObject 字段（非 public）
        private static GameObject GetReportObjectField(ReportScreen rs, string name)
        {
            try
            {
                var f = typeof(ReportScreen).GetField(name,
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (f == null) return null;
                return f.GetValue(rs) as GameObject;
            }
            catch { return null; }
        }

        // 把一行报告行填上文字（取该行里第一个 LocText，清掉其余的）
        public static void SetReportRowText(GameObject row, string text, Color? color = null)
        {
            if (row == null) return;
            try
            {
                var lt = row.GetComponentInChildren<LocText>();
                if (lt == null) return;
                lt.SetText(text ?? "");
                CleanExtraLocTexts(row, lt);
                if (color.HasValue) lt.color = color.Value;
            }
            catch { /* expected: 行文本填充失败不影响该行载体 */ }
        }

        // 取原版报告屏的标题 LocText（可为 null；ReportScreen.title 非 public，反射取）
        public static LocText ReportTitle
        {
            get
            {
                try
                {
                    var rs = ReportScreen.Instance;
                    if (rs == null) return null;
                    var f = typeof(ReportScreen).GetField("title",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic);
                    return f != null ? f.GetValue(rs) as LocText : null;
                }
                catch { return null; }
            }
        }

        // 原版控件模板访问
        public static GameObject GetWidget(string widgetName)
        {
            try
            {
                // ⚠ Assets.UIPrefabs 是【结构体】(Assets.UIPrefabData)，不能 == null 比较，
                var w = Assets.UIPrefabs.TableScreenWidgets;

                GameObject go = null;
                switch (widgetName)
                {
                    case "Label":            go = w.Label;            break;
                    case "LabelHeader":      go = w.LabelHeader;      break;
                    case "ButtonLabel":      go = w.ButtonLabel;      break;
                    case "ButtonLabelWhite": go = w.ButtonLabelWhite; break;
                    case "Checkbox":         go = w.Checkbox;         break;
                    case "BlankCell":        go = w.BlankCell;        break;
                    case "Spacer":           go = w.Spacer;           break;
                    case "DropDownHeader":   go = w.DropDownHeader;   break;
                    case "NumericDropDown":  go = w.NumericDropDown;  break;
                    case "GenericPortrait":  go = w.GenericPortrait;  break;
                    default:                 go = null;               break;
                }

                if (go == null) WarnNoTemplate("模板 '" + widgetName + "' 为 null");
                return go;
            }
            catch (Exception e)
            {
                WarnNoTemplate(e, "控件模板表读取失败");
                return null;
            }
        }

        private static void WarnNoTemplate(string why)
        {
            if (_warnedNoTemplate) return;
            _warnedNoTemplate = true;
            Debug.LogWarning("[MoreFood/Event][UI] 原版控件模板不可用（" + why
                             + "），将使用降级控件。外观可能不同，功能不受影响。");
        }

        // 带异常的同款告警：仍只打一次，但保留堆栈
        private static void WarnNoTemplate(Exception e, string why)
        {
            if (_warnedNoTemplate) return;
            _warnedNoTemplate = true;
            MFLog.Exc(LogCat.UI, e, "[MoreFood/Event][UI] 原版控件模板不可用（" + why
                      + "），将使用降级控件。外观可能不同，功能不受影响。");
        }

        // 把节点放到屏幕中央（面板根节点用）
        public static void CenterOnScreen(GameObject go, float width, float height)
        {
            var rt = RT(go);
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
