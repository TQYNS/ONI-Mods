using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    // 战斗 UI（K10/K12/K15）

    internal static class CombatTargeting
    {
        internal static Clustercraft TargetingCraft;
        internal static bool WarpMode;

        internal static bool Targeting
        {
            get { return TargetingCraft != null; }
        }

        internal static void Begin(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null)
            {
                TradeUi.Notify("无法攻击", "星图未打开");
                return;
            }
            ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
            if (selector == null)
            {
                TradeUi.Notify("无法攻击", craft.Name + " 没有目的地选择器（原版组件缺失）");
                return;
            }
            try
            {
                Traverse.Create(screen).Field("m_closeOnSelect").SetValue(false);
            }
            catch (Exception)
            {
                // expected: 原版字段改名后写不进去，选点模式照常进入，仅失去「选中后不自动关图」
            }
            WarpMode = false;
            TargetingCraft = craft;
            screen.ShowInSelectDestinationMode(selector);
            TradeUi.Notify("攻击", "点击目标怪物所在格下令（点空白格取消）");
        }

        internal static void BeginWarp(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null)
            {
                TradeUi.Notify("无法跃迁", "星图未打开");
                return;
            }
            if (!WarpRuntime.CanUse(craft))
            {
                TradeUi.Notify("无法跃迁", WarpRuntime.StatusText(craft).Replace("跃迁：", ""));
                return;
            }
            ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
            if (selector == null)
            {
                TradeUi.Notify("无法跃迁", craft.Name + " 没有目的地选择器（原版组件缺失）");
                return;
            }
            try
            {
                Traverse.Create(screen).Field("m_closeOnSelect").SetValue(false);
            }
            catch (Exception)
            {
                // expected: 原版字段改名后写不进去，跃迁选点照常进入，仅失去「选中后不自动关图」
            }
            WarpMode = true;
            TargetingCraft = craft;
            screen.ShowInSelectDestinationMode(selector);
            TradeUi.Notify("跃迁", "点击主星图内未被占用的合法格（点空白格取消）");
        }

        internal static void ExitTargeting(string why)
        {
            if (TargetingCraft == null)
            {
                return;
            }
            MFLog.Info(LogCat.Space, "[Space] 退出选目标模式（" + why + "）");
            TargetingCraft = null;
            WarpMode = false;
        }

        // true = 交回原版；false = 已接管
        internal static bool HandleSelectHex(ClusterMapScreen screen, ClusterMapHex hex)
        {
            Clustercraft craft = TargetingCraft;
            if (craft == null || craft.gameObject == null)
            {
                if (craft != null)
                {
                    ExitTargeting("火箭没了");
                }
                return true;
            }
            if (screen == null || hex == null)
            {
                return true;
            }
            object mode = screen.GetMode();
            if (mode == null || mode.ToString() != "SelectDestination")
            {
                ExitTargeting("不在选点模式");
                return true;
            }
            AxialI cell = hex.location;
            if (WarpMode)
            {
                WarpRuntime.TryWarp(craft, cell);
                ForceExitSelectMode(screen);
                ExitTargeting("跃迁选点完成");
                return false;
            }
            SpaceMonster monster = FindMonsterAt(cell);
            if (monster != null)
            {
                CombatRuntime.OrderAttack(craft, monster);
                // Bug2：不再自绘攻击箭头（原版选点模式自带箭头/高亮）。
                ForceExitSelectMode(screen);
                ExitTargeting("已下令攻击 " + monster.Name);
                return false;
            }
            SpaceStation station = FindStationAt(cell);
            if (station != null)
            {
                TradeUi.Notify("需先宣战", "攻击空间站需要先宣战（功能待接入，K12）");
            }
            else
            {
                TradeUi.Notify("该格没有攻击目标", "已退出攻击");
            }
            ForceExitSelectMode(screen);
            ExitTargeting("无有效目标");
            return false;
        }

        // SetMode 是私有方法且参数是私有枚举 → 反射调 Default（比 TryHandleCancel 更干净，不触发额外事件）。
        private static void ForceExitSelectMode(ClusterMapScreen screen)
        {
            try
            {
                MethodInfo m = screen.GetType().GetMethod("SetMode", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null)
                {
                    Type modeType = m.GetParameters()[0].ParameterType;
                    object def = Enum.Parse(modeType, "Default");
                    m.Invoke(screen, new object[] { def });
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 强制退出选点模式失败");
            }
        }

        private static SpaceMonster FindMonsterAt(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return null;
            }
            List<ClusterGridEntity> list;
            if (grid.cellContents.TryGetValue(cell, out list) && list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    SpaceMonster m = list[i] as SpaceMonster;
                    if (m != null)
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        private static SpaceStation FindStationAt(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return null;
            }
            List<ClusterGridEntity> list;
            if (grid.cellContents.TryGetValue(cell, out list) && list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    SpaceStation s = list[i] as SpaceStation;
                    if (s != null)
                    {
                        return s;
                    }
                }
            }
            return null;
        }
    }

    internal static class CombatUi
    {
        private static GameObject bar;
        private static Clustercraft barCraft;
        private static Button emergencyBtn;
        private static Button tradeBtn;
        private static Button refuelBtn;
        private static Button warpBtn;

        internal static bool AnyOpen()
        {
            return bar != null;
        }

        internal static void ResetForLoad()
        {
            HideBar();
        }

        internal static void ShowCraftBar(Clustercraft craft)
        {
            HideAllBars();   // 统一隐藏（问题7/8）
            if (craft == null)
            {
                return;
            }
            Transform parent = TradeUi.OverlayParent();
            if (parent == null)
            {
                Debug.LogWarning("[Space] 找不到 overlay 父节点，战斗按钮排无法显示");
                return;
            }
            barCraft = craft;
            float h = CombatDb.Shared.uiBarHeight;
            bar = TradeUi.Node("SpaceCombatBar", parent, 0f, 0f, 10f, 10f);
            RectTransform rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, CombatDb.Shared.uiBarY);
            rt.sizeDelta = new Vector2(730f, h);
            bar.transform.SetAsLastSibling();   // 层级置顶（问题7/8）

            Button attackBtn = TradeUi.MakeButton("攻 击", bar.transform, 0f, 0f, 110f, h, TradeUi.Panel, TradeUi.Accent,
                TradeUi.TextMain, delegate
                {
                    CombatTargeting.Begin(barCraft);
                    RefreshBar();
                });
            AddAttackTooltip(attackBtn.gameObject);   // 问题11：攻击按钮 tooltip 显示攻击距离
            warpBtn = TradeUi.MakeButton("跃 迁", bar.transform, 120f, 0f, 110f, h, TradeUi.Panel, TradeUi.Accent,
                TradeUi.TextMain, delegate
                {
                    CombatTargeting.BeginWarp(barCraft);
                    RefreshBar();
                });
            AddWarpTooltip(warpBtn.gameObject);
            // 捕获：K15 占位（灰、点了出提示；逃跑捕获机制留空）
            TradeUi.MakeButton("捕 获", bar.transform, 240f, 0f, 110f, h, TradeUi.PanelAlt, TradeUi.TextOff,
                TradeUi.TextOff, delegate
                {
                    TradeUi.Notify("暂未开放", "捕获功能暂未开放");
                });
            emergencyBtn = TradeUi.MakeButton("紧急脱离", bar.transform, 360f, 0f, 130f, h,
                TradeUi.Panel, TradeUi.Accent, TradeUi.TextMain, delegate
                {
                    RocketEmergencyExit.TryTrigger(barCraft, false);   // 条件不满足时内部会给提示
                    RefreshBar();
                });
            // 显示前现算一次：没有相邻站点就**直接做成灰色且不可点**（不是点了才提示）
            SpaceStation near = FindAdjacentStation(craft);
            bool canTrade = near != null;
            bool canRefuel = near != null && TradeUi.RefuelConfigured(near);
            tradeBtn = TradeUi.MakeButton("交 易", bar.transform, 500f, 0f, 110f, h,
                canTrade ? TradeUi.Panel : TradeUi.PanelAlt,
                canTrade ? TradeUi.Accent : TradeUi.TextOff,
                canTrade ? TradeUi.TextMain : TradeUi.TextOff, delegate
                {
                    SpaceStation s = FindAdjacentStation(barCraft);
                    if (s == null)
                    {
                        ShowCraftBar(barCraft);   // 期间状态变了 → 重建并置灰
                        return;
                    }
                    TradeUi.OpenTrade(s, false);
                });
            if (tradeBtn != null)
            {
                tradeBtn.interactable = canTrade;
            }
            // 火箭侧加油：只在相邻站点支持加油时点亮，复用同一套加油窗
            refuelBtn = TradeUi.MakeButton("加 油", bar.transform, 620f, 0f, 110f, h,
                canRefuel ? TradeUi.Panel : TradeUi.PanelAlt,
                canRefuel ? TradeUi.Accent : TradeUi.TextOff,
                canRefuel ? TradeUi.TextMain : TradeUi.TextOff, delegate
                {
                    SpaceStation s = FindAdjacentStation(barCraft);
                    if (s == null || !TradeUi.RefuelConfigured(s))
                    {
                        ShowCraftBar(barCraft);
                        return;
                    }
                    TradeUi.ToggleRefuel(s, barCraft);
                });
            if (refuelBtn != null)
            {
                refuelBtn.interactable = canRefuel;
            }
            RefreshBar();   // 只刷新攻击/跃迁/紧急脱离（交易/加油已在上面定好）
        }

        // 问题11：攻击按钮 tooltip —— 显示攻击距离（不再沿用原版"行程"文案）。
        private static void AddAttackTooltip(GameObject go)
        {
            try
            {
                ToolTip tip = go.AddComponent<ToolTip>();
                tip.SetSimpleTooltip(AttackTipText());
                AttackHover hover = go.AddComponent<AttackHover>();
                hover.tip = tip;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 攻击按钮 Tooltip 创建失败");
            }
        }

        private static string AttackTipText()
        {
            return "攻击距离：" + Mathf.RoundToInt(CombatRuntime.AttackRangeOf(barCraft))
                + " 星图格\n点击后选取目标格下令（超出射程会变红）";
        }

        private sealed class AttackHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler
        {
            internal ToolTip tip;

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (tip != null)
                {
                    tip.SetSimpleTooltip(AttackTipText());
                }
            }
        }

        private static void AddWarpTooltip(GameObject go)
        {
            try
            {
                ToolTip tip = go.AddComponent<ToolTip>();
                tip.SetSimpleTooltip(WarpRuntime.StatusText(barCraft));
                WarpHover hover = go.AddComponent<WarpHover>();
                hover.tip = tip;
                hover.craft = barCraft;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 跃迁按钮 Tooltip 创建失败");
            }
        }

        private sealed class WarpHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler
        {
            internal ToolTip tip;
            internal Clustercraft craft;

            public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (tip != null)
                {
                    tip.SetSimpleTooltip(WarpRuntime.StatusText(craft));
                }
            }
        }

        private static void SetWarpButton(Button btn, Clustercraft craft)
        {
            if (btn == null)
            {
                return;
            }
            bool on = WarpRuntime.CanUse(craft);
            // 与「捕获」「紧急脱离」一致 —— 恒 interactable=true，灰显只靠颜色；
            btn.interactable = true;
            Image body = btn.targetGraphic as Image;
            if (body != null)
            {
                body.color = on ? TradeUi.Panel : TradeUi.PanelAlt;
            }
            Image outline = btn.GetComponent<Image>();
            if (outline != null)
            {
                outline.color = on ? TradeUi.Accent : TradeUi.TextOff;
            }
            TMPro.TextMeshProUGUI label = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label == null)
            {
                Transform t = btn.transform.Find("label");
                if (t != null)
                {
                    label = t.GetComponent<TMPro.TextMeshProUGUI>();
                }
            }
            if (label != null)
            {
                label.color = on ? TradeUi.TextMain : TradeUi.TextOff;
            }
            ToolTip tip = btn.GetComponent<ToolTip>();
            if (tip != null)
            {
                tip.SetSimpleTooltip(WarpRuntime.StatusText(craft));
            }
        }

        // 与「捕获」占位按钮同款灰：底 PanelAlt / 描边 TextOff / 文字 TextOff。
        private static void SetEnabled(Button btn, bool on)
        {
            if (btn == null)
            {
                return;
            }
            btn.interactable = true;
            Image body = btn.targetGraphic as Image;
            if (body != null)
            {
                body.color = on ? TradeUi.Panel : TradeUi.PanelAlt;
            }
            Image outline = btn.GetComponent<Image>();
            if (outline != null)
            {
                outline.color = on ? TradeUi.Accent : TradeUi.TextOff;
            }
            TMPro.TextMeshProUGUI label = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label == null)
            {
                Transform t = btn.transform.Find("label");
                if (t != null)
                {
                    label = t.GetComponent<TMPro.TextMeshProUGUI>();
                }
            }
            if (label != null)
            {
                label.color = on ? TradeUi.TextMain : TradeUi.TextOff;
            }
        }

        // 刷新按钮可用性（只在火箭移动 / 受击 / 恢复 / 触发 / 冷却到点时调用，不轮询）。
        internal static void RefreshBar()
        {
            if (bar == null)
            {
                return;
            }
            if (barCraft == null || barCraft.gameObject == null)
            {
                HideBar();
                return;
            }
            if (emergencyBtn != null)
            {
                SetEnabled(emergencyBtn, RocketEmergencyExit.CanTriggerActive(barCraft));
            }
            SetWarpButton(warpBtn, barCraft);
            // 设计变更：交易/加油不在这里刷新。它们的可用态只在 ShowCraftBar（显示前）现算，
            //   点击时再校验一次；本方法只维护与"位置/站点"无关的按钮（攻击/跃迁/紧急脱离）。
        }

        // Bug5：火箭 → 交易站目标的记忆（火箭 instanceID → 站点 Key）。
        private static readonly Dictionary<int, string> tradeTargetByCraft = new Dictionary<int, string>();

        internal static SpaceStation FindAdjacentStation(Clustercraft craft)
        {
            // 设计变更：火箭必须在星图中（InFlight）且已停止飞行，落在地表的火箭不参与交易/加油
            if (!TradeUtil.InStarmap(craft))
            {
                return null;
            }
            try
            {
                ClusterTraveler tr = craft.GetComponent<ClusterTraveler>();
                if (tr != null && tr.IsTraveling())
                {
                    return null;   // 飞行途中路过站点不算停靠
                }
            }
            catch (Exception)
            {
                // expected: 状态机尚未建立时按未飞行处理
            }
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return null;
            }
            int id = craft.GetInstanceID();
            List<SpaceStation> found = new List<SpaceStation>();
            for (int d = 0; d < 6; d++)
            {
                AxialI c = SpaceCfg.Neighbor(craft.Location, d);
                List<ClusterGridEntity> list;
                if (!grid.cellContents.TryGetValue(c, out list) || list == null)
                {
                    continue;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    SpaceStation s = list[i] as SpaceStation;
                    // 修订：cellContents 可能残留站点在旧格的陈旧条目（改位置只走事件增减），
                    //   必须回验站点当前真实位置，否则"相邻"判定会把远处站点算进来。
                    if (s != null && s.Location == c && !found.Contains(s))
                    {
                        found.Add(s);
                    }
                }
            }
            if (found.Count == 0)
            {
                tradeTargetByCraft.Remove(id);
                return null;
            }
            // ① 记忆里的站点仍然相邻 → 沿用（固定目标，不随遍历顺序漂移）
            string remembered;
            if (tradeTargetByCraft.TryGetValue(id, out remembered))
            {
                for (int i = 0; i < found.Count; i++)
                {
                    if (found[i].Key == remembered)
                    {
                        return found[i];
                    }
                }
            }
            // ② 否则按站点 Key 稳定排序取第一个并记入（结果与遍历顺序无关 → 无竞态）
            found.Sort(delegate(SpaceStation a, SpaceStation b)
            {
                return string.CompareOrdinal(a.Key, b.Key);
            });
            SpaceStation chosen = found[0];
            tradeTargetByCraft[id] = chosen.Key;
            return chosen;
        }

        internal static void HideBar()
        {
            if (bar != null)
            {
                UnityEngine.Object.Destroy(bar);
                bar = null;
            }
            barCraft = null;
            emergencyBtn = null;
            tradeBtn = null;
            refuelBtn = null;
            CombatArrow.Hide();   // 问题11：关星图/收按钮排时一并收起自绘攻击箭头
        }

        // 统一隐藏底部所有条（问题7/8）：战斗按钮排 + TradeUi 的条，避免互相遮盖。
        internal static void HideAllBars()
        {
            HideBar();
            try
            {
                TradeUi.HideBar();
            }
            catch (Exception)
            {
                // expected: TradeUi 条清理失败时自己的战斗条已隐藏，界面不残留重叠
            }
        }

        // 星图关闭时清理（挂 ClusterMapShowPatch 的关闭分支）。
        internal static void OnMapClosed()
        {
            HideBar();
            CombatTargeting.ExitTargeting("星图关闭");
        }
    }
}
