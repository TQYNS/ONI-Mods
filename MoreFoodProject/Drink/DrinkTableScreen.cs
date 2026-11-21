// DrinkTableScreen.cs — 饮水系统 · 【饮品管制屏：1:1 复刻原版「饮食」】

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    public class DrinkTableScreen : ConsumablesTableScreen
    {
        // 当前实例（单例）
        private static DrinkTableScreen _inst;
        // 是否已激活过一次（防重复初始化）
        private bool _activatedOnce;

        private readonly List<CheckboxTableColumn> _cols = new List<CheckboxTableColumn>();

        private const BindingFlags BF = BindingFlags.Instance
                                      | BindingFlags.Public
                                      | BindingFlags.NonPublic;

        // 取（没有就克隆）饮品管制屏实例 —— 1:1 复刻原版「饮食」屏
        public static KScreen GetOrCreate()
        {
            try
            {
                if (_inst != null && _inst.gameObject != null) return _inst;

                // ★ 克隆源优先用 ScheduledUIInstantiation.UIElements 里的【真 prefab】——
                var srcGO = FindConsumablesTablePrefab();
                string srcFrom = "UIElements.prefabs";

                if (srcGO == null)
                {
                    // 兜底取原版已实例化的「饮食」屏（克隆失败时用）
                    var inst = GetVanillaConsumablesScreen();
                    if (inst != null) { srcGO = inst.gameObject; srcFrom = "consumablesScreen 实例"; }
                }

                if (srcGO == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 两种途径都拿不到「饮食」屏模板"
                                   + "（UIElements.prefabs / consumablesScreen）—— 饮品屏不可用");
                    return null;
                }

                // —— 打印【源对象】自身的字段状态：区分"源就是空壳" vs "克隆没带过来" ——
                try
                {
                    var srcComp = srcGO.GetComponent<ConsumablesTableScreen>();
                    DumpFields(srcComp, "源对象(" + srcFrom + ")，源=" + srcGO.name);
                }
                catch { /* expected: 诊断打印失败不影响克隆流程 */ }

                // ★ 克隆：把 prefab 引用全部带过来
                var parent = srcGO.transform.parent;   // 与原版同级
                var go = UnityEngine.Object.Instantiate(srcGO, parent, false);
                if (go == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 克隆失败");
                    return null;
                }
                go.name = "MoreFood_DrinkTableScreen";
                go.SetActive(false);

                // ★★★ 关键顺序（上一版就是这里错了）：先取克隆体上的原版 ConsumablesTableScreen，再挂 DrinkTableScreen
                var old = go.GetComponent<ConsumablesTableScreen>();
                if (old == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 克隆体上没有 ConsumablesTableScreen（克隆源不对）");
                    return null;
                }

                var mine = go.AddComponent<DrinkTableScreen>();
                if (mine == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] AddComponent<DrinkTableScreen> 失败"
                                   + "（GameObject 上已有兼容组件？）");
                    return null;
                }

                // ① 搬字段：TableScreen 的全部 + ShowOptimizedKScreen 的两个
                int moved = CopyFields(old, mine, typeof(TableScreen));
                moved += CopyFields(old, mine, typeof(ShowOptimizedKScreen));
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 已从旧脚本搬过 " + moved + " 个字段");

                // ② 集合字段重建（克隆是浅拷贝，会与旧脚本共享引用）
                ResetAllColls(mine);

                // ③ 补 canvasGroup / graphicRaycaster（非序列化，Instantiate 不复制）
                MountFromClone(mine, go);

                // ④ 确认字段齐了，再删旧脚本
                int nullNow = DumpFieldsEx(mine, "搬完字段后");
                if (nullNow != 0)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 搬完仍有 " + nullNow
                                   + " 个字段为 NULL → 放弃本次克隆（旧脚本保留，不再动）");
                    // 把我们的脚本也撤掉，避免半死不活状态
                    try { UnityEngine.Object.DestroyImmediate(mine); } catch { /* expected: 仅回滚本次放弃的组件，销毁失败也不再上报 */ }
                    return null;
                }

                // ⑤ 字段齐了，安全删除旧脚本
                UnityEngine.Object.DestroyImmediate(old);

                _inst = mine;
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] ★ 已从原版「饮食」屏克隆出饮品屏（" + go.name + "）");
                return mine;
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] GetOrCreate 异常: " + e);
                return null;
            }
        }

        // 反射取原版「饮食」屏实例（字段可见性未定，用反射稳妥）
        private static ConsumablesTableScreen GetVanillaConsumablesScreen()
        {
            try
            {
                var menu = ManagementMenu.Instance;
                if (menu == null) return null;

                var f = typeof(ManagementMenu).GetField("consumablesScreen", BF);
                if (f == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 反射找不到 ManagementMenu.consumablesScreen");
                    return null;
                }
                return f.GetValue(menu) as ConsumablesTableScreen;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] 取 consumablesScreen 异常");
                return null;
            }
        }

        // ★ 从 ScheduledUIInstantiation.UIElements[].prefabs[] 里找「饮食」屏的【真 prefab】。
        private static GameObject FindConsumablesTablePrefab()
        {
            try
            {
                var gsm = GameScreenManager.Instance;
                if (gsm == null) return null;

                var sui = gsm.GetComponent<ScheduledUIInstantiation>();
                if (sui == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] ScreenSpaceOverlayCanvas 上没有 ScheduledUIInstantiation");
                    return null;
                }

                var fUI = typeof(ScheduledUIInstantiation).GetField("UIElements", BF);
                var arr = (fUI != null) ? fUI.GetValue(sui) as Array : null;
                if (arr == null)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] UIElements 取不到（字段名/可见性变化）");
                    return null;
                }

                int scanned = 0;
                foreach (var item in arr)
                {
                    if (item == null) continue;

                    var fP = item.GetType().GetField("prefabs", BF);
                    var prefabs = (fP != null) ? fP.GetValue(item) as Array : null;
                    if (prefabs == null) continue;

                    foreach (var p in prefabs)
                    {
                        var pf = p as GameObject;
                        if (pf == null) continue;
                        scanned++;
                        if (pf.GetComponent<ConsumablesTableScreen>() != null)
                        {
                            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 在 UIElements 里找到「饮食」屏 prefab："
                                      + pf.name + "（共扫过 " + scanned + " 个 prefab）");
                            return pf;
                        }
                    }
                }

                // 原版已无该 prefab 时走程序绘制回退，功能正常，属预期回退而非异常
                MFLog.Info(LogCat.Drink,"[MoreFood/Drink][UI/Table] UIElements 里没有带 ConsumablesTableScreen 的 prefab"
                                 + "（共扫过 " + scanned + " 个），改走程序绘制");
                return null;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] FindConsumablesTablePrefab 异常");
                return null;
            }
        }

        // ★ 把旧脚本上的字段搬到新脚本（字段属于组件，删了组件就丢了）
        private static int CopyFields(Component from, Component to, Type declaring)
        {
            int n = 0;
            if (from == null || to == null || declaring == null) return 0;
            try
            {
                var fields = declaring.GetFields(BF);   // public+nonpublic，含继承的 public/protected
                for (int i = 0; i < fields.Length; i++)
                {
                    try
                    {
                        var v = fields[i].GetValue(from);

                        // 集合字段不搬 —— 否则新旧脚本共享同一个集合实例
                        if (v is System.Collections.IDictionary) continue;
                        if (v is System.Collections.IList && !(v is Array)) continue;

                        fields[i].SetValue(to, v);
                        n++;
                    }
                    catch { /* expected: 单个字段不可读或不可写时跳过，其余字段继续搬运 */ }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] CopyFields(" + declaring.Name + ") 异常");
            }
            return n;
        }

        private static void ResetAllColls(DrinkTableScreen mine)
        {
            ResetColl(mine, "columns",                  typeof(Dictionary<string, TableColumn>));
            ResetColl(mine, "rows",                     typeof(List<TableRow>));
            ResetColl(mine, "all_sortable_rows",        typeof(List<TableRow>));
            ResetColl(mine, "column_scrollers",         typeof(List<string>));
            ResetColl(mine, "known_widget_rows",        typeof(Dictionary<GameObject, TableRow>));
            ResetColl(mine, "known_widget_columns",     typeof(Dictionary<GameObject, TableColumn>));
            ResetColl(mine, "worldDividers",            typeof(Dictionary<int, GameObject>));
            ResetColl(mine, "obsoleteMinionRowStatus",  typeof(Dictionary<IAssignableIdentity, bool>));
            ResetColl(mine, "obsoleteWorldDividerStatus", typeof(Dictionary<int, bool>));
        }

        // 克隆后修补（Instantiate 不复制的东西）
        private static void MountFromClone(DrinkTableScreen mine, GameObject go)
        {
            // ① ShowOptimizedKScreen 的两个 private 字段
            try
            {
                var t = typeof(ShowOptimizedKScreen);

                var fcg = t.GetField("canvasGroup", BF);
                if (fcg != null)
                {
                    var cgType = Type.GetType("UnityEngine.CanvasGroup, UnityEngine.UIModule")
                                 ?? AccessTools.TypeByName("UnityEngine.CanvasGroup");
                    object cg = (cgType != null) ? go.GetComponent(cgType) : null;
                    fcg.SetValue(mine, cg);
                }

                var fgr = t.GetField("graphicRaycaster", BF);
                if (fgr != null) fgr.SetValue(mine, go.GetComponent<GraphicRaycaster>());

                bool ok1 = fcg == null || fcg.GetValue(mine) != null;
                bool ok2 = fgr == null || fgr.GetValue(mine) != null;
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 补字段：canvasGroup=" + ok1 + " graphicRaycaster=" + ok2);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] 补 ShowOptimizedKScreen 字段异常");
            }

            // ② 诊断（集合字段已在 CopyFields 后统一重建）
            try { DumpFields(mine, "MountFromClone 后"); } catch { /* expected: 诊断打印失败不影响屏幕创建 */ }
        }

        private static void ResetColl(object target, string fieldName, Type collType)
        {
            try
            {
                var f = typeof(TableScreen).GetField(fieldName, BF);
                if (f == null) return;
                f.SetValue(target, Activator.CreateInstance(collType));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] 重置 " + fieldName + " 失败");
            }
        }

        // ★ 诊断：把 TableScreen 的全部 prefab 引用字段打印出来
        private static readonly string[] RequiredFields =
        {
            "prefab_row_empty", "prefab_row_header", "prefab_world_divider", "prefab_scroller_border",
            "header_content_transform", "scroll_content_transform", "scroller_borders_transform",
            "CloseButton", "title_bar",
        };

        // ⚠ 这 3 个【正常就该是 null】—— 它们是运行时状态，不是 prefab 引用
        private static readonly string[] OptionalFields =
        {
            "header_row", "default_row", "VLG",
        };

        // 诊断：把表屏字段逐一打印出来，核对是否与克隆源一致
        public static void DumpFields(TableScreen t, string tag)
        {
            int n = DumpFieldsEx(t, tag);
            if (n > 0)
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table][诊断] " + tag + " : NULL 数=" + n);
        }

        // 诊断并返回【必需字段】的 null 个数（不含 header_row / default_row / VLG 这 3 个运行时字段）。
        public static int DumpFieldsEx(TableScreen t, string tag)
        {
            if (t == null) { MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table][诊断] " + tag + " : target 为 null"); return -1; }

            string s = "";
            int requiredNull = 0;

            for (int i = 0; i < RequiredFields.Length; i++)
            {
                bool isNull = IsFieldNull(t, RequiredFields[i]);
                if (isNull) requiredNull++;
                s += RequiredFields[i] + "=" + (isNull ? "NULL" : "ok") + "  ";
            }
            for (int i = 0; i < OptionalFields.Length; i++)
            {
                bool isNull = IsFieldNull(t, OptionalFields[i]);
                s += "(" + OptionalFields[i] + "=" + (isNull ? "null" : "set") + ")  ";   // 括号=可空
            }

            if (requiredNull > 0)
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table][诊断] " + tag + " : " + s
                                 + "  （必需字段 NULL 数=" + requiredNull + "）");
            else
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table][诊断] " + tag + " : 必需字段全部 ok  " + s);

            return requiredNull;
        }

        private static bool IsFieldNull(TableScreen t, string fn)
        {
            try
            {
                var f = typeof(TableScreen).GetField(fn, BF);
                return f == null || f.GetValue(t) == null;
            }
            catch { return true; }
        }

        // AddRow 前置检查：缺字段就跳过，避免 NRE 崩
        private bool CanAddRow(out string why)
        {
            why = null;
            try
            {
                var fPrefab = typeof(TableScreen).GetField("prefab_row_empty", BF);
                if (fPrefab == null || fPrefab.GetValue(this) == null)
                {
                    why = "prefab_row_empty 为 NULL";
                    return false;
                }
                var fContent = typeof(TableScreen).GetField("scroll_content_transform", BF);
                if (fContent == null || fContent.GetValue(this) == null)
                {
                    why = "scroll_content_transform 为 NULL";
                    return false;
                }
                return true;
            }
            catch (Exception e) { why = "检查异常: " + e.GetType().Name; MFLog.Verbose(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] CanAddRow 检查异常"); return false; }
        }

        // OnActivate —— 手动复刻 TableScreen.OnActivate 的基础设施 + 建饮品列
        protected override void OnActivate()
        {
            try
            {
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] OnActivate 开始（复刻原版流程）");

                // ① 复刻 TableScreen.OnActivate 的 5 行基础设施
                try
                {
                    this.title = "饮品管制";
                    if (title_bar != null) title_bar.text = this.title;
                    ConsumeMouseScroll = true;

                    // 关闭按钮与小人增删订阅只做一次：KScreen.Activate() 每次进屏都会重跑 OnActivate
                    if (!_activatedOnce)
                    {
                        _activatedOnce = true;
                        if (CloseButton != null)
                            CloseButton.onClick += (System.Action)(() =>
                            {
                                var mm = ManagementMenu.Instance;
                                if (mm != null) mm.CloseAll();
                            });
                        Components.LiveMinionIdentities.OnAdd    += (Action<MinionIdentity>)(_ => MarkRowsDirty());
                        Components.LiveMinionIdentities.OnRemove += (Action<MinionIdentity>)(_ => MarkRowsDirty());
                    }

                    // incubating 是 TableScreen 的 private 字段 → 反射设
                    var finc = typeof(TableScreen).GetField("incubating", BF);
                    if (finc != null) finc.SetValue(this, true);

                    // ★ 照抄原版：进"孵化中"状态。
                    var rtr = transform.rectTransform();
                    if (rtr != null) rtr.localScale = Vector3.zero;

                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 基础设施已复刻（标题/关闭按钮/订阅）");
                }
                catch (Exception e)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 复刻基础设施失败: " + e);
                }

                // ② 列：照抄原版结构，数据源换成饮品
                BuildDrinkColumns();

                // ③ 字段自检：决定 _fieldsOk（不齐就停用本屏的每帧刷新，防刷屏）
                int nullCount = DumpFieldsEx(this, "OnActivate/建行前");
                _fieldsOk = (nullCount == 0);

                if (!_fieldsOk)
                {
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 本屏缺少 " + nullCount
                                   + " 个 prefab 引用 → 已停用 AddRow 与每帧刷新（不再崩，但表是空的）");
                    return;
                }

                // ④ ★ 行/表头全部交给原版流程建（不再手动 AddRow）
                try
                {
                    MarkRowsDirty();
                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 已 MarkRowsDirty()，"
                              + "表头行 + 小人行 + 世界分隔全部交给原版 RefreshRows 构建");
                }
                catch (Exception e)
                {
                    _fieldsOk = false;
                    MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] MarkRowsDirty 失败（已停用本屏刷新）: " + e);
                }

                // ⑤ 可见性自检：把"为什么看不见"摊开
                try
                {
                    var rtr = transform.rectTransform();
                    var parent = transform.parent;

                    int headerWidgets = 0, rowChildren = 0;
                    try
                    {
                        if (scroll_content_transform != null)
                            rowChildren = scroll_content_transform.childCount;
                    }
                    catch { /* expected: 自检统计失败按 0 计，不影响后续输出 */ }
                    try
                    {
                        if (header_content_transform != null)
                            headerWidgets = header_content_transform.childCount;
                    }
                    catch { /* expected: 自检统计失败按 0 计，不影响后续输出 */ }

                    // ★ 尺寸对比：把原版「饮食」屏的 RectTransform 也打出来
                    string vanillaSize = "?";
                    try
                    {
                        var v = GetVanillaConsumablesScreen();
                        if (v != null && v.transform != null)
                        {
                            var vrt = v.transform.rectTransform();
                            vanillaSize = "scale=" + (vrt != null ? vrt.localScale.ToString() : "?")
                                        + " pos=" + (vrt != null ? vrt.anchoredPosition.ToString() : "?")
                                        + " size=" + (vrt != null ? vrt.sizeDelta.ToString() : "?")
                                        + " active=" + v.gameObject.activeInHierarchy;
                        }
                    }
                    catch { /* expected: 原版屏不可得时跳过尺寸对比，保留占位串 */ }

                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table][自检] "
                              + "activeInHierarchy=" + gameObject.activeInHierarchy
                              + " activeSelf=" + gameObject.activeSelf
                              + " localScale=" + (rtr != null ? rtr.localScale.ToString() : "?")
                              + " anchoredPos=" + (rtr != null ? rtr.anchoredPosition.ToString() : "?")
                              + " sizeDelta=" + (rtr != null ? rtr.sizeDelta.ToString() : "?")
                              + " 父=" + (parent != null ? parent.name : "null")
                              + " 同级序号=" + (parent != null ? transform.GetSiblingIndex().ToString() : "?")
                              + " / 同级数=" + (parent != null ? parent.childCount.ToString() : "?")
                              + " 表头子物体=" + headerWidgets
                              + " 滚动区子物体=" + rowChildren
                              + " 标题文本=" + (title_bar != null ? title_bar.text : "null"));

                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table][自检] 原版「饮食」屏对比： " + vanillaSize);
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table][自检] 异常");
                }

                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] ★ 饮品管制屏已建（原版机制，饮品列 "
                          + _cols.Count + "）");
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] OnActivate 异常: " + e);
            }
        }

        protected override void OnDeactivate() { }

        // ★★ 每帧守卫（必须！）
        private bool _fieldsOk;

        // 每帧刷新：字段不齐时直接跳过（防克隆体字段未初始化时崩），齐了才走原版刷新
        public override void ScreenUpdate(bool topLevel)
        {
            if (!_fieldsOk) return;          // ← 字段不齐：什么都不做
            try { base.ScreenUpdate(topLevel); }
            catch (Exception e)
            {
                _fieldsOk = false;            // 出过一次错就永久闭嘴，避免刷屏
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] ScreenUpdate 异常（已停用本屏刷新）: " + e);
                return;
            }
        }

        // ⚠ 覆写 Deactivate：基类会 Object.Destroy(gameObject)
        public override void Deactivate()
        {
            try
            {
                var sm = KScreenManager.Instance;
                if (sm != null) { try { sm.PopScreen(this); } catch { /* expected: 屏已不在栈中时 PopScreen 抛异常，随后仍隐藏自身 */ } }
                if (gameObject != null) gameObject.SetActive(false);
            }
            catch (Exception e) { MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] Deactivate 异常: " + e); }
        }

        // ★★★ 原版回调反射句柄
        private static MethodInfo _miOnLoad, _miGetValue, _miOnClick, _miSetValue,
                                _miTooltip, _miSortTooltip, _miCompare, _miTooltipSuper;

        private static void InitVanillaCallbacks()
        {
            if (_miOnLoad != null) return;
            try
            {
                var t = typeof(ConsumablesTableScreen);
                _miOnLoad      = t.GetMethod("on_load_consumable_info", BF);
                _miGetValue    = t.GetMethod("get_value_consumable_info", BF);
                _miOnClick     = t.GetMethod("on_click_consumable_info", BF);
                _miSetValue    = t.GetMethod("set_value_consumable_info", BF);
                _miTooltip     = t.GetMethod("on_tooltip_consumable_info", BF);
                _miSortTooltip = t.GetMethod("on_tooltip_sort_consumable_info", BF);
                _miCompare     = t.GetMethod("compare_consumable_info", BF);
                _miTooltipSuper = t.GetMethod("on_tooltip_consumable_info_super", BF);

                int n = 0;
                if (_miOnLoad != null) n++;
                if (_miGetValue != null) n++;
                if (_miOnClick != null) n++;
                if (_miSetValue != null) n++;
                if (_miTooltip != null) n++;
                if (_miSortTooltip != null) n++;
                if (_miCompare != null) n++;

                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 原版回调反射：找到 " + n + "/7"
                          + "（onLoad=" + (_miOnLoad != null)
                          + " getValue=" + (_miGetValue != null)
                          + " onClick=" + (_miOnClick != null)
                          + " setValue=" + (_miSetValue != null) + "）");
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] InitVanillaCallbacks 异常: " + e);
            }
        }

        private object CallMI(MethodInfo mi, params object[] args)
        {
            if (mi == null) return null;
            try { return mi.Invoke(this, args); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] 调 " + mi.Name + " 异常");
                return null;
            }
        }

        // ★ 饮品专属 tooltip
        private void OnTooltipDrink(IAssignableIdentity minion, GameObject widget_go, ToolTip tooltip)
        {
            try
            {
                if (tooltip == null || widget_go == null) return;

                TableRow row = null;
                try { row = GetWidgetRow(widget_go); } catch { /* expected: 控件查不到对应行，按无行处理并提前返回 */ }
                if (row == null) return;

                if (row.rowType != TableRow.RowType.Header)
                {
                    var colN = GetWidgetColumn(widget_go) as ConsumableInfoTableColumn;
                    if (colN == null || colN.consumable_info == null) return;
                    string itemName = colN.consumable_info.ConsumableName;
                    bool on = (GetValue(minion, widget_go) == TableScreen.ResultValues.True);

                    tooltip.ClearMultiStringTooltip();
                    if (row.rowType == TableRow.RowType.Default)
                    {
                        string t = on
                            ? string.Format(STRINGS.UI.CONSUMABLESSCREEN.NEW_MINIONS_FOOD_PERMISSION_ON, itemName)
                            : string.Format(STRINGS.UI.CONSUMABLESSCREEN.NEW_MINIONS_FOOD_PERMISSION_OFF, itemName);
                        if (!string.IsNullOrEmpty(itemName)) t = t.Replace(itemName, "");
                        tooltip.AddMultiStringTooltip(DrinkWording(t), null);
                    }
                    else if (minion != null)
                    {
                        string who = minion.GetProperName();
                        string t = on
                            ? string.Format(STRINGS.UI.CONSUMABLESSCREEN.FOOD_PERMISSION_ON, who, itemName)
                            : string.Format(STRINGS.UI.CONSUMABLESSCREEN.FOOD_PERMISSION_OFF, who, itemName);
                        if (!string.IsNullOrEmpty(itemName)) t = t.Replace(itemName, "");
                        tooltip.AddMultiStringTooltip(DrinkWording(t), null);
                    }
                    return;
                }

                // ② 表头行 → 饮品专属
                var col = GetWidgetColumn(widget_go) as ConsumableInfoTableColumn;
                if (col == null || col.consumable_info == null) return;

                var item = col.consumable_info;
                var drink = item as DrinkConsumableItem;
                var data = (drink != null) ? drink.data : null;

                tooltip.ClearMultiStringTooltip();

// 名称行（tooltip 第 1 行）
                tooltip.AddMultiStringTooltip(item.ConsumableName, null);

                // 可用数量
                float amount = 0f;
                try
                {
                    amount = ClusterManager.Instance.activeWorld.worldInventory
                             .GetAmount(item.ConsumableId.ToTag(), false);
                }
                catch { /* expected: 世界库存取不到时可用量按 0 显示 */ }
                tooltip.AddMultiStringTooltip(
                    string.Format(STRINGS.UI.CONSUMABLESSCREEN.FOOD_AVAILABLE,
                        amount.ToString("0.###") + STRINGS.UI.UNITSUFFIXES.UNITS),
                    null);

                if (data == null) return;

                // ★ 补水值（去掉 "/单位" 后缀，简化）
                tooltip.AddMultiStringTooltip(
                    "补水：" + data.hydrationPerKg.ToString("0.#"),
                    null);

                // ★ 士气（照抄原版 FOOD_MORALE 的文案与符号格式）
                if (!Mathf.Approximately(data.moraleBonus, 0f))
                {
                    tooltip.AddMultiStringTooltip(
                        string.Format(STRINGS.UI.CONSUMABLESSCREEN.FOOD_MORALE,
                            GameUtil.AddPositiveSign(data.moraleBonus.ToString("0.#"),
                                                     data.moraleBonus > 0f)),
                        null);
                }

                // ★ 效果（buff 名）
                if (!string.IsNullOrEmpty(data.effectId))
                {
                    string effName = data.effectId;
                    try
                    {
                        var eff = Db.Get().effects.Get(data.effectId);
                        if (eff != null && !string.IsNullOrEmpty(eff.Name)) effName = eff.Name;
                    }
                    catch { /* expected: 效果库查不到时直接显示 effectId */ }
                    tooltip.AddMultiStringTooltip("效果：" + effName, null);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] OnTooltipDrink 异常");
            }
        }

        // 建列：照抄 ConsumablesTableScreen.OnActivate，数据源换成饮品
        private void BuildDrinkColumns()
        {
            _cols.Clear();

            try
            {
                // 头像列
                AddPortraitColumn("Portrait",
                    new Action<IAssignableIdentity, GameObject>(base.on_load_portrait),
                    new Comparison<IAssignableIdentity>(base.compare_rows_alphabetical),
                    true);

                // 名字列
                var noTooltip = new Action<IAssignableIdentity, GameObject, ToolTip>(
                    (id, w, t) => { });
                var noSortTooltip = new Action<IAssignableIdentity, GameObject, ToolTip>(
                    (id, w, t) => { });

                AddButtonLabelColumn("Names",
                    new Action<IAssignableIdentity, GameObject>(base.on_load_name_label),
                    new Func<IAssignableIdentity, GameObject, string>(base.get_value_name_label),
                    new Action<GameObject>(w => { try { GetWidgetRow(w).SelectMinion(); } catch { /* expected: 控件行缺失时点击选中不生效 */ } }),
                    new Action<GameObject>(w => { try { GetWidgetRow(w).SelectAndFocusMinion(); } catch { /* expected: 控件行缺失时点击聚焦不生效 */ } }),
                    new Comparison<IAssignableIdentity>(base.compare_rows_alphabetical),
                    noTooltip, noSortTooltip, false);
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 建 头像/名字 列失败: " + e);
            }

            // 滚动内容（原版对每个「饮食」屏都要先起滚动的）
            try { StartScrollableContent("consumableScroller"); }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] StartScrollableContent 异常"); }

            // ★ 每个饮品一列（对应原版每样食物一列）
            var addCol = typeof(ConsumablesTableScreen).GetMethod(
                "AddConsumableInfoColumn", BF);          // ⚠ non-public，实测 CS0122
            if (addCol == null)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] 反射找不到 AddConsumableInfoColumn"
                               + "（原版可能改名/改签名）→ 无法建列");
                return;
            }

            // ★ 从 Drinks.CollectItems() 取清单
            var entries = Drinks.CollectItems();
            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] ── 从 Drinks.CollectItems() 取到 "
                      + entries.Count + " 项饮品（仅物品，不含建筑货源）");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table]   [" + i + "] id='" + e.id
                          + "' name='" + e.name + "'"
                          + (e.isBuilding ? " [建筑]" : " [物品]")
                          + " priority=" + (e.data != null ? e.data.priority : 0));
            }

            InitVanillaCallbacks();

            int built = 0, failed = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string tag = entry.id;
                try
                {
                    var item = new DrinkConsumableItem { tag = tag, data = entry.data };

                    // ★ 7 个回调全部走原版（反射）—— 表头图标 / 勾选框状态 / 点击 / tooltip / 排序
                    var col = addCol.Invoke(this, new object[]
                    {
                        "Drink_" + tag,
                        item,
                        // ★ 加载：原版（图标/BG色）+ 覆盖小人行勾选框（读我们的 tracker）
                        new Action<IAssignableIdentity, GameObject>(OnLoadDrink),
                        // ★ 取值/设值：全部走我们自己的 tracker，不碰 ConsumableConsumer
                        new Func<IAssignableIdentity, GameObject, TableScreen.ResultValues>(GetValue),
                        new Action<GameObject>(OnClickDrink),
                        new Action<GameObject, TableScreen.ResultValues>(SetValue),
                        new Comparison<IAssignableIdentity>(
                            (a, b) =>
                            {
                                var r = CallMI(_miCompare, a, b);
                                return (r is int) ? (int)r : 0;
                            }),
                        // ★ tooltip：表头行显示饮品详情（补水/士气/效果），其余行仍走原版
                        new Action<IAssignableIdentity, GameObject, ToolTip>(OnTooltipDrink),
                        new Action<IAssignableIdentity, GameObject, ToolTip>(
                            (id, w, t) => CallMI(_miSortTooltip, id, w, t)),
                    });

                    if (col is CheckboxTableColumn)
                    {
                        _cols.Add((CheckboxTableColumn)col);
                        built++;

                        // 不让饮品格走"每行横向滚动区"
                        var ccol = col as ConsumableInfoTableColumn;
                        if (ccol != null) ccol.scrollerID = "";

                        MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table]   ✓ 建列 '" + tag + "' → " + col.GetType().Name
                                  + (ccol != null ? "（scrollerID 置空 → 格子直接挂行）" : ""));
                    }
                    else
                    {
                        failed++;
                        MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table]   ✗ 建列 '" + tag
                                         + "' 返回 " + (col == null ? "null" : col.GetType().Name)
                                         + "（不是 CheckboxTableColumn，未收入 _cols）");
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table]   ✗ 建列失败 " + tag);
                }
            }
            MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 建列结果：成功 " + built + " / 失败 " + failed
                      + "，_cols.Count=" + _cols.Count);

            // ★ scroller 占位锚（必须！撤掉会直接抛异常）
            try
            {
                RegisterColumn("DrinkScrollerAnchor", new DividerColumn(() => true, "consumableScroller"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] 注册 scroller 占位锚失败");
            }

            // 最右「全选」列（★ 全部用 TableScreen 基类的原版回调）
            try
            {
                AddSuperCheckboxColumn("SuperDrink", _cols.ToArray(),
                    new Action<IAssignableIdentity, GameObject>(base.on_load_value_checkbox_column_super),
                    // ★ 取值/设值走我们的 tracker（否则全选列读的仍是原版状态）
                    new Func<IAssignableIdentity, GameObject, TableScreen.ResultValues>(GetSuperValue),
                    new Action<GameObject>(base.on_press_checkbox_column_super),
                    new Action<GameObject, TableScreen.ResultValues>(SetSuperValue),
                    new Comparison<IAssignableIdentity>(base.compare_rows_alphabetical),
                    // ⚠ tooltip 不能传 null（同理，原版无条件调用）
                    new Action<IAssignableIdentity, GameObject, ToolTip>(
                        (id, w, t) => CallMI(_miTooltipSuper, id, w, t)));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] 建 全选列 失败");
            }
        }

        // 回调
        private static void OnLoadCell(IAssignableIdentity id, GameObject widget) { }
        private static int CompareRows(IAssignableIdentity a, IAssignableIdentity b) { return 0; }

        // 取值：★ 读【我们自己的】tracker，不碰原版 ConsumableConsumer
        private TableScreen.ResultValues GetValue(IAssignableIdentity id, GameObject widget_go)
        {
            try
            {
                var col = GetWidgetColumn(widget_go) as ConsumableInfoTableColumn;
                if (col == null || col.consumable_info == null) return TableScreen.ResultValues.NotApplicable;

                var row = GetWidgetRow(widget_go);
                if (row == null) return TableScreen.ResultValues.True;

                if (row.rowType == TableRow.RowType.Default)
                {
                    return DrinkRestrictionDefaults.IsForbidden(col.consumable_info.ConsumableId.ToTag())
                           ? TableScreen.ResultValues.False
                           : TableScreen.ResultValues.True;
                }

                if (row.rowType == TableRow.RowType.Header)
                {
                    bool allTrue = true, allFalse = true, anyPartial = false;
                    foreach (var kv in col.widgets_by_row)
                    {
                        var r = kv.Key;
                        if (r == null || kv.Value == null || kv.Value == widget_go) continue;
                        switch (GetValue(r.GetIdentity(), kv.Value))
                        {
                            case TableScreen.ResultValues.False: allTrue = false; break;
                            case TableScreen.ResultValues.True: allFalse = false; break;
                            case TableScreen.ResultValues.Partial: anyPartial = true; break;
                        }
                    }
                    if (anyPartial) return TableScreen.ResultValues.Partial;
                    if (allTrue) return TableScreen.ResultValues.True;
                    if (allFalse) return TableScreen.ResultValues.False;
                    return TableScreen.ResultValues.Partial;
                }

                var mi = id as MinionIdentity;
                var t = DrinkRestrictionTracker.Get(mi != null ? mi.gameObject : null);
                if (t == null) return TableScreen.ResultValues.True;

                return t.IsPermitted(col.consumable_info.ConsumableId)
                       ? TableScreen.ResultValues.True
                       : TableScreen.ResultValues.False;
            }
            catch { return TableScreen.ResultValues.NotApplicable; }
        }

        // 设值：默认行 → 饮品专属默认清单 DrinkRestrictionDefaults；
        private void SetValue(GameObject widget_go, TableScreen.ResultValues v)
        {
            try
            {
                var col = GetWidgetColumn(widget_go) as ConsumableInfoTableColumn;
                if (col == null || col.consumable_info == null) return;

                var row = GetWidgetRow(widget_go);
                if (row == null) return;

                string tag = col.consumable_info.ConsumableId;
                bool permitted = (v != TableScreen.ResultValues.False);

                if (row.rowType == TableRow.RowType.Default)
                {
                    DrinkRestrictionDefaults.SetForbidden(tag.ToTag(), !permitted);

                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 饮品默认管制已改：'" + tag + "' "
                              + (permitted ? "允许" : "禁止")
                              + "（新复制人的默认值；不影响食物管制）");
                    RefreshColumnCells(col);
                    return;
                }

                if (row.rowType == TableRow.RowType.Header)
                {
                    int n = 0;
                    foreach (MinionIdentity identity in Components.LiveMinionIdentities)
                    {
                        if (identity == null) continue;
                        var t2 = DrinkRestrictionTracker.Get(identity.gameObject);
                        if (t2 == null) continue;
                        t2.SetPermitted(tag, permitted);
                        n++;
                    }

                    DrinkRestrictionDefaults.SetForbidden(tag.ToTag(), !permitted);

                    MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 表头管制已改：饮品'" + tag + "' "
                              + (permitted ? "允许" : "禁止") + "（" + n + " 个当前小人 + 默认行）");
                    RefreshColumnCells(col);
                    return;
                }

                if (row.rowType == TableRow.RowType.StoredMinon) return;

                var id = row.GetIdentity();
                var mi = id as MinionIdentity;
                var t = DrinkRestrictionTracker.Get(mi != null ? mi.gameObject : null);
                if (t == null) return;

                t.SetPermitted(tag, permitted);

                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 饮品管制已改："
                          + (id != null ? id.GetProperName() : "?")
                          + " → 饮品'" + tag + "' " + (permitted ? "允许" : "禁止")
                          + "（仅影响饮水目标，不影响原版食物管制）");
            }
            catch (Exception e) { MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] SetValue 异常: " + e); }
        }

        private void RefreshColumnCells(ConsumableInfoTableColumn col)
        {
            try
            {
                foreach (var kv in col.widgets_by_row)
                {
                    var r = kv.Key;
                    if (r == null || kv.Value == null) continue;
                    OnLoadDrink(r.GetIdentity(), kv.Value);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] RefreshColumnCells 异常");
            }
        }

        private void OnClickDrink(GameObject widget_go)
        {
            try
            {
                var row = GetWidgetRow(widget_go);
                var col = GetWidgetColumn(widget_go) as ConsumableInfoTableColumn;
                if (row == null || col == null || col.consumable_info == null) return;
                if (row.rowType == TableRow.RowType.StoredMinon) return;

                var id = row.GetIdentity();
                var v = GetValue(id, widget_go);
                var next = (v == TableScreen.ResultValues.True)
                           ? TableScreen.ResultValues.False
                           : TableScreen.ResultValues.True;

                SetValue(widget_go, next);
                RefreshColumnCells(col);
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] OnClickDrink 异常: " + e);
            }
        }

        // 全选列
        private TableScreen.ResultValues GetSuperValue(IAssignableIdentity id, GameObject widget)
        {
            try
            {
                var mi = id as MinionIdentity;
                var t = DrinkRestrictionTracker.Get(mi != null ? mi.gameObject : null);
                if (t == null || Drinks.ItemTable.Count == 0) return TableScreen.ResultValues.NotApplicable;

                int allowed = 0, total = 0;
                foreach (var kv in Drinks.ItemTable)
                {
                    total++;
                    if (t.IsPermitted(kv.Key.ToString())) allowed++;
                }
                if (allowed == total) return TableScreen.ResultValues.True;
                if (allowed == 0) return TableScreen.ResultValues.False;
                return TableScreen.ResultValues.Partial;
            }
            catch { return TableScreen.ResultValues.NotApplicable; }
        }

        private void SetSuperValue(GameObject widget_go, TableScreen.ResultValues v)
        {
            try
            {
                var row = GetWidgetRow(widget_go);
                var id = (row != null) ? row.GetIdentity() : null;
                var mi = id as MinionIdentity;
                var t = DrinkRestrictionTracker.Get(mi != null ? mi.gameObject : null);
                if (t == null) return;

                bool permitted = (v != TableScreen.ResultValues.False);
                foreach (var kv in Drinks.ItemTable)
                    t.SetPermitted(kv.Key.ToString(), permitted);

                MFLog.Verbose(LogCat.Drink,"[MoreFood/Drink][UI/Table] 全选："
                          + (id != null ? id.GetProperName() : "?")
                          + " → 全部饮品 " + (permitted ? "允许" : "禁止"));
            }
            catch (Exception e) { MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][UI/Table] SetSuperValue 异常: " + e); }
        }

        // ★ 加载单元格：先跑原版（图标/BG色/默认行），再覆盖小人行勾选框
        private void OnLoadDrink(IAssignableIdentity minion, GameObject widget_go)
        {
            CallMI(_miOnLoad, minion, widget_go);

            try
            {
                var row = GetWidgetRow(widget_go);
                if (row == null) return;

                var col = GetWidgetColumn(widget_go) as ConsumableInfoTableColumn;
                if (col == null || col.consumable_info == null) return;

                bool ok = (GetValue(minion, widget_go) != TableScreen.ResultValues.False);

                var toggle = widget_go.GetComponent<MultiToggle>();
                if (toggle != null) toggle.ChangeState(ok ? 1 : 0);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][UI/Table] OnLoadDrink 覆盖勾选框异常");
            }
        }

        private static string DrinkWording(string s)
        {
            return string.IsNullOrEmpty(s) ? s : s.Replace("食用", "饮用");
        }

        private static void ShowTooltip(IAssignableIdentity id, GameObject widget, ToolTip tip,
                                        string tag, DrinkData data)
        {
            try
            {
                if (tip == null) return;
                tip.ClearMultiStringTooltip();
                tip.AddMultiStringTooltip(tag, null);
                if (data != null)
                {
                    tip.AddMultiStringTooltip("补水 " + data.hydrationPerKg + "/kg"
                                              + "　优先级 " + data.priority, null);
                }
            }
            catch { /* expected: tooltip 展示失败不影响表格刷新 */ }
        }


        private static ConsumableConsumer GetConsumer(IAssignableIdentity id)
        {
            try
            {
                var mi = id as MinionIdentity;
                if (mi == null) return null;
                var cc = mi.GetComponent<ConsumableConsumer>();
                if (cc == null) cc = mi.GetComponentInChildren<ConsumableConsumer>();
                return cc;
            }
            catch { return null; }
        }
    }

    // 饮品管制屏：仿生人不饮水，不建行（只作用于 DrinkTableScreen，不影响原版「饮食」屏）
    [HarmonyPatch(typeof(TableScreen), "AddRow")]
    public static class DrinkTableScreen_BionicFilter_Patch
    {
        // 补丁：建行前拦一道 —— 仿生人不饮水，饮品管制屏上不给他建行（不影响原版「饮食」屏）
        public static bool Prefix(TableScreen __instance, IAssignableIdentity minion)
        {
            try
            {
                if (!(__instance is DrinkTableScreen)) return true;

                MinionIdentity mi = minion as MinionIdentity;
                if (mi != null) return !DrinkDatabase.IsBionic(mi.gameObject);

                StoredMinionIdentity stored = minion as StoredMinionIdentity;
                if (stored != null) return stored.model != BionicMinionConfig.MODEL;

                return true;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkTableScreen_BionicFilter_Patch", e); return true; }
        }
    }
}
