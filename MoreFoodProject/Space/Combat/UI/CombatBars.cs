using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    // 战斗迷你条（★ 恒定屏幕像素 + 图标底部中心锚点 + 被动重排 + 1s节流）

    internal static class CombatBars
    {
        private sealed class Widget
        {
            internal GameObject root;
            internal Image hpFill;
            internal Image armorFill;
            internal CombatBarHover hover;
            internal ClusterGridEntity entity;
            internal AxialI cell;
        }

        // 恒定屏幕像素尺寸（改这里 = 改条在屏幕上的固定大小，与地图缩放无关）
        private const float BarWidth = 34f;
        private const float BarHeight = 5f;
        private const float BarGap = 2f;
        private const float BarPad = 1f;
        private const float RootBarWidth = BarWidth + BarPad * 2f;    // 36
        private const float RootBarHeight = BarHeight * 2f + BarGap + BarPad * 2f;  // 14

        // ★ 条顶边到图标底部中心的屏幕像素偏移（8 = 贴近图标下方；调大往下更多）
        private const float BarDropPx = 8f;

        // ★ 被动重排 1 秒节流：地图缩放拖动时最多 1 次/秒，避免高频重排
        private const float RepositionMinIntervalSeconds = 1f;

        private static readonly Dictionary<int, Widget> monsterBars = new Dictionary<int, Widget>();
        private static readonly Dictionary<int, Widget> craftBars = new Dictionary<int, Widget>();

        // 上次允许重排的时间戳（Time.unscaledTime 语义；与存档无关，仅会话内节流）
        private static float nextAllowedRepositionTime;

        internal static void ResetForLoad()
        {
            monsterBars.Clear();
            craftBars.Clear();
            nextAllowedRepositionTime = 0f;
        }

        private static bool MapOpen
        {
            get
            {
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                return screen != null && screen.gameObject.activeInHierarchy;
            }
        }

        internal static void OnMapShown()
        {
            // 重开星图允许立即重排一次（清节流）
            nextAllowedRepositionTime = 0f;
            for (int i = 0; i < CombatRuntime.Monsters.Count; i++)
            {
                MonsterRuntime rt = CombatRuntime.Monsters[i];
                if (rt != null && rt.IsAlive)
                {
                    EnsureMonster(rt);
                }
            }
            for (int i = 0; i < CombatRuntime.Crafts.Count; i++)
            {
                Clustercraft craft = CombatRuntime.Crafts[i];
                RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
                if (st != null && st.inCombat)
                {
                    EnsureCraft(craft, st);
                }
            }
        }

        internal static void OnMapClosed()
        {
            foreach (KeyValuePair<int, Widget> kv in monsterBars)
            {
                DestroyWidget(kv.Value);
            }
            monsterBars.Clear();
            foreach (KeyValuePair<int, Widget> kv in craftBars)
            {
                DestroyWidget(kv.Value);
            }
            craftBars.Clear();
            nextAllowedRepositionTime = 0f;
        }

        /// <summary>
        internal static void OnMapScaleChanged()
        {
            float now = Time.unscaledTime;
            if (now < nextAllowedRepositionTime)
            {
                return;
            }
            nextAllowedRepositionTime = now + RepositionMinIntervalSeconds;
            RepositionAll(monsterBars);
            RepositionAll(craftBars);
        }

        private static void RepositionAll(Dictionary<int, Widget> dict)
        {
            foreach (KeyValuePair<int, Widget> kv in dict)
            {
                Widget w = kv.Value;
                if (w == null || w.root == null || !w.root.activeSelf)
                {
                    continue;
                }
                Reposition(w);
            }
        }

        /// <summary>
        internal static void UpdateHover()
        {
            DriveIconHover(monsterBars);
            DriveIconHover(craftBars);
        }

        private static void DriveIconHover(Dictionary<int, Widget> dict)
        {
            foreach (KeyValuePair<int, Widget> kv in dict)
            {
                Widget w = kv.Value;
                if (w == null || w.root == null || w.hover == null)
                {
                    continue;
                }
                // 未揭示格里当作没有这只怪：条跟着藏、详情不弹（每次 hover 只判一次 IsVisible）
                bool show = w.entity != null && w.entity.IsVisible;
                if (w.root.activeSelf != show)
                {
                    w.root.SetActive(show);
                }
                if (!show)
                {
                    if (w.hover.IsOpen)
                    {
                        w.hover.CloseTooltip();
                    }
                    continue;
                }
                bool near = PointerNearIcon(w);
                if (near && !w.hover.IsOpen)
                {
                    w.hover.OnPointerEnter(null);
                }
                else if (!near && w.hover.IsOpen)
                {
                    w.hover.CloseTooltip();
                }
            }
        }

        private static bool PointerNearIcon(Widget w)
        {
            try
            {
                Vector2 m = Input.mousePosition;
                if (w.root != null)
                {
                    Vector3 bp = w.root.transform.position;
                    // 条屏幕宽 = 设计宽（恒定屏幕像素），命中半径按此估算
                    float rb = RootBarWidth * 0.5f + 14f;
                    float bdx = m.x - bp.x;
                    float bdy = m.y - bp.y;
                    if (bdx * bdx + bdy * bdy <= rb * rb)
                    {
                        return true;
                    }
                }
                ClusterMapScreen screen = ClusterMapScreen.Instance;
                if (screen != null && w.entity != null)
                {
                    ClusterMapVisualizer vis = screen.GetEntityVisAnim(w.entity);
                    if (vis != null)
                    {
                        RectTransform vrt = vis.GetComponent<RectTransform>();
                        if (vrt != null && vrt.rect.width > 0.01f)
                        {
                            Vector3 c = vrt.TransformPoint(new Vector3(vrt.rect.center.x, vrt.rect.center.y, 0f));
                            float r = vrt.rect.width * Mathf.Abs(vrt.lossyScale.x) * 0.5f + 12f;
                            float dx = m.x - c.x;
                            float dy = m.y - c.y;
                            return dx * dx + dy * dy <= r * r;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // expected: 命中判定失败时按未命中处理，点击穿透给星图正常选格
            }
            return false;
        }

        // 怪物

        internal static void EnsureMonster(MonsterRuntime rt)
        {
            try
            {
                if (rt == null || rt.monster == null || !rt.IsAlive || !MapOpen)
                {
                    return;
                }
                AxialI cell = rt.Cell;
                bool visible = rt.monster.IsVisible && CellVisible(cell);
                int id = rt.monster.GetInstanceID();
                Widget w;
                if (!monsterBars.TryGetValue(id, out w) || w == null || w.root == null)
                {
                    if (!visible)
                    {
                        return;
                    }
                    w = CreateWidget("SpaceMonsterBar", rt.monster, cell);
                    if (w == null)
                    {
                        return;
                    }
                    monsterBars[id] = w;
                }
                w.cell = cell;
                w.root.SetActive(visible);
                if (visible)
                {
                    Reposition(w);
                    ApplyMonster(w, rt);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 迷你条(怪物)创建失败");
            }
        }

        /// <summary>攻击者结算伤害后调用（事件推送，非轮询）。</summary>
        internal static void PushMonster(MonsterRuntime rt)
        {
            if (rt == null || rt.monster == null)
            {
                return;
            }
            int id = rt.monster.GetInstanceID();
            Widget w;
            if (!monsterBars.TryGetValue(id, out w) || w == null || w.root == null)
            {
                EnsureMonster(rt);
                return;
            }
            w.cell = rt.Cell;
            bool visible = rt.monster.IsVisible && CellVisible(w.cell) && MapOpen;
            w.root.SetActive(visible);
            if (!visible)
            {
                return;
            }
            Reposition(w);
            ApplyMonster(w, rt);
        }

        internal static void OnMonsterMoved(MonsterRuntime rt)
        {
            if (rt == null || rt.monster == null)
            {
                return;
            }
            int id = rt.monster.GetInstanceID();
            Widget w;
            if (!monsterBars.TryGetValue(id, out w) || w == null || w.root == null)
            {
                return;
            }
            w.cell = rt.Cell;
            bool visible = rt.monster.IsVisible && CellVisible(w.cell) && MapOpen;
            w.root.SetActive(visible);
            if (visible)
            {
                Reposition(w);   // 移动后重新挂到新格子
            }
        }

        internal static void Remove(SpaceMonster monster)
        {
            if (monster == null)
            {
                return;
            }
            int id = monster.GetInstanceID();
            Widget w;
            if (monsterBars.TryGetValue(id, out w))
            {
                DestroyWidget(w);
                monsterBars.Remove(id);
            }
        }

        private static void ApplyMonster(Widget w, MonsterRuntime rt)
        {
            float hpPct = rt.def != null && rt.def.hp > 0f ? Mathf.Clamp01(rt.hp / rt.def.hp) : 0f;
            bool hasArmor = rt.def != null && rt.def.armor > 0f;
            float armorPct = hasArmor ? Mathf.Clamp01(rt.armor / rt.def.armor) : 0f;
            SetFill(w.hpFill, hpPct, BarWidth);
            SetFill(w.armorFill, armorPct, BarWidth);
            if (w.armorFill != null)
            {
                w.armorFill.gameObject.SetActive(hasArmor);
            }
            if (w.hover != null)
            {
                CombatMonsterDef d = rt.def;
                string state = StateText(rt.state);
                string text = (d != null ? d.name : rt.defId) + "（狩猎等级 " + (d != null ? d.tier : "?") + "）"
                    + (string.IsNullOrEmpty(rt.questId) ? "" : "  [任务目标]")
                    + "\n" + (d != null && !string.IsNullOrEmpty(d.description) ? d.description : "（无描述）")
                    + "\n生命值：" + Mathf.Round(rt.hp) + "/" + (d != null ? Mathf.Round(d.hp) : 0)
                    + "\n装甲值：" + Mathf.Round(rt.armor) + "/" + (d != null ? Mathf.Round(d.armor) : 0)
                    + "\n护甲（防御）：" + (d != null ? Mathf.Round(d.defense) : 0)
                    + "\n攻击力：" + (d != null ? Mathf.Round(d.attack) : 0)
                    + "（穿甲 " + (d != null ? Mathf.Round(d.armorPenetration) : 0) + "）"
                    + "\n状态：" + state;
                w.hover.tooltipText = text;
            }
        }

        // 火箭

        internal static void EnsureCraft(Clustercraft craft, RocketCombatState st)
        {
            try
            {
                if (craft == null || st == null || !MapOpen)
                {
                    return;
                }
                int id = craft.GetInstanceID();
                Widget w;
                if (!craftBars.TryGetValue(id, out w) || w == null || w.root == null)
                {
                    w = CreateWidget("SpaceCraftBar", craft, craft.Location);
                    if (w == null)
                    {
                        return;
                    }
                    craftBars[id] = w;
                }
                w.cell = craft.Location;
                w.root.SetActive(st.inCombat);
                if (st.inCombat)
                {
                    Reposition(w);
                    ApplyCraft(w, craft, st);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 迷你条(火箭)创建失败");
            }
        }

        /// <summary>火箭受击/开火后调用（事件推送）。</summary>
        internal static void PushCraft(Clustercraft craft, RocketCombatState st)
        {
            if (craft == null || st == null)
            {
                return;
            }
            int id = craft.GetInstanceID();
            Widget w;
            if (!craftBars.TryGetValue(id, out w) || w == null || w.root == null)
            {
                if (st.inCombat)
                {
                    EnsureCraft(craft, st);
                }
                return;
            }
            w.cell = craft.Location;
            w.root.SetActive(st.inCombat && MapOpen);
            if (st.inCombat)
            {
                Reposition(w);
                ApplyCraft(w, craft, st);
            }
        }

        internal static void OnCraftMoved(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            int id = craft.GetInstanceID();
            Widget w;
            if (!craftBars.TryGetValue(id, out w) || w == null || w.root == null)
            {
                return;
            }
            RocketCombatState st = craft.GetComponent<RocketCombatState>();
            w.cell = craft.Location;
            bool show = st != null && st.inCombat && MapOpen;
            w.root.SetActive(show);
            if (show)
            {
                Reposition(w);   // 移动后重新挂到新格子
            }
        }

        internal static void RemoveCraft(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            int id = craft.GetInstanceID();
            Widget w;
            if (craftBars.TryGetValue(id, out w))
            {
                DestroyWidget(w);
                craftBars.Remove(id);
            }
        }

        private static void ApplyCraft(Widget w, Clustercraft craft, RocketCombatState st)
        {
            float hpPct = st.totalHpCache > 0f ? Mathf.Clamp01(st.Hp / st.totalHpCache) : 0f;
            bool hasArmor = st.maxArmor > 0f;
            float armorPct = hasArmor ? Mathf.Clamp01(st.armor / st.maxArmor) : 0f;
            SetFill(w.hpFill, hpPct, BarWidth);
            SetFill(w.armorFill, armorPct, BarWidth);
            if (w.armorFill != null)
            {
                w.armorFill.gameObject.SetActive(hasArmor);
            }
            if (w.hover != null)
            {
                w.hover.tooltipText = craft.Name + (st.inCombat ? "  (交战中)" : "  待机")
                    + "\n生命值：" + Mathf.Round(st.Hp) + "/" + Mathf.Round(st.totalHpCache)
                    + "\n装甲值：" + Mathf.Round(st.armor) + "/" + Mathf.Round(st.maxArmor)
                    + "\n护甲（防御）：" + Mathf.Round(st.defenseCache)
                    + "\n攻击距离：" + Mathf.Round(CombatRuntime.AttackRangeOf(craft)) + " 星图格"
                    + "\n（开火数据以当前弹药与炮台配置为准）";
            }
        }

        // 公共

        private static Widget CreateWidget(string name, ClusterGridEntity entity, AxialI cell)
        {
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null)
            {
                return null;
            }
            Vector3 world;
            Transform root;
            if (!StarMapVisuals.TryCellWorld(screen, cell, out world, out root) || root == null)
            {
                return null;
            }
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);   // 位置语义 = 条中心
            rt.sizeDelta = new Vector2(RootBarWidth, RootBarHeight);

            Widget w = new Widget();
            w.root = go;
            w.entity = entity;
            w.cell = cell;

            Image bg = NewImage(go.transform, "bg", RootBarWidth, RootBarHeight,
                new Color(0.05f, 0.07f, 0.10f, 0.85f), 0f, 0f);
            Image hp = NewImage(go.transform, "hp", BarWidth, BarHeight,
                new Color(0.86f, 0.24f, 0.22f, 1f), BarPad, BarPad);
            Image armor = NewImage(go.transform, "armor", BarWidth, BarHeight,
                new Color(0.30f, 0.62f, 0.95f, 1f), BarPad, BarPad + BarHeight + BarGap);
            w.hpFill = hp;
            w.armorFill = armor;
            bg.raycastTarget = true;   // 悬停区（保留条上悬停）
            CombatBarHover hover = go.AddOrGet<CombatBarHover>();
            hover.bar = w.root;
            w.hover = hover;
            Reposition(w);   // 创建时定位一次
            return w;
        }

        /// <summary>内部图像：锚在父节点左下角（父 pivot = 0.5,0.5 不影响子节点 anchor 语义）。</summary>
        private static Image NewImage(Transform parent, string name, float width, float height, Color color,
            float x, float y)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>按百分比改宽度（不用 Filled——无贴图时 Filled 渲染不可靠）。</summary>
        private static void SetFill(Image img, float pct, float fullWidth)
        {
            if (img == null)
            {
                return;
            }
            RectTransform rt = img.rectTransform;
            if (rt != null)
            {
                Vector2 size = rt.sizeDelta;
                rt.sizeDelta = new Vector2(Mathf.Max(0f, fullWidth * Mathf.Clamp01(pct)), size.y);
            }
        }

        /// <summary>
        private static Vector3 ComputeAnchor(Widget w, out float iconSizeWorld)
        {
            iconSizeWorld = 0f;
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            Vector3 cellPoint = Vector3.zero;
            try
            {
                Vector3 world;
                Transform root;
                if (screen != null && StarMapVisuals.TryCellWorld(screen, w.cell, out world, out root) && root != null)
                {
                    cellPoint = world;
                }
                if (screen != null && w.entity != null)
                {
                    ClusterMapVisualizer vis = screen.GetEntityVisAnim(w.entity);
                    if (vis != null)
                    {
                        RectTransform vrt = vis.GetComponent<RectTransform>();
                        if (vrt != null && vrt.rect.width > 0.01f)
                        {
                            // 四角变换到世界，取底边（yMin）中点
                            Vector3 bl = vrt.TransformPoint(new Vector3(vrt.rect.xMin, vrt.rect.yMin, 0f));
                            Vector3 br = vrt.TransformPoint(new Vector3(vrt.rect.xMax, vrt.rect.yMin, 0f));
                            iconSizeWorld = vrt.rect.width * Mathf.Abs(vrt.lossyScale.x);
                            return (bl + br) * 0.5f;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // expected: 图标锚点取不到时退回格子中心往下半个图标高度
            }
            // 兜底：格子中心往下半个图标
            iconSizeWorld = StarMapVisuals.GetEntityWidth(screen, w.entity);
            if (iconSizeWorld <= 0.01f)
            {
                iconSizeWorld = 24f;
            }
            return new Vector3(cellPoint.x, cellPoint.y - iconSizeWorld * 0.5f, cellPoint.z);
        }

        /// <summary>
        private static void Reposition(Widget w)
        {
            ClusterMapScreen screen = ClusterMapScreen.Instance;
            if (screen == null || w == null || w.root == null)
            {
                return;
            }
            Vector3 world;
            Transform root;
            if (!StarMapVisuals.TryCellWorld(screen, w.cell, out world, out root) || root == null)
            {
                return;
            }
            RectTransform rt = w.root.GetComponent<RectTransform>();
            if (rt == null)
            {
                return;
            }
            if (rt.parent != root)
            {
                rt.SetParent(root, false);
            }

            float scale = StarMapVisuals.GetMapScale(screen);
            if (scale <= 0.0001f)
            {
                scale = 1f;
            }
            float inv = 1f / scale;

            // 反向缩放：条在屏幕上恒定 34×14 像素
            rt.localScale = new Vector3(inv, inv, 1f);

            // ★ 锚点 = 图标底部中心（不是格子中心！）
            float iconSizeWorld;
            Vector3 anchor = ComputeAnchor(w, out iconSizeWorld);

            // 位置 = 图标底部中心往下 BarDropPx 屏幕像素（世界单位 = BarDropPx × inv）
            float dropWorld = BarDropPx * inv;
            rt.position = new Vector3(anchor.x, anchor.y - dropWorld, anchor.z);
        }

        private static void DestroyWidget(Widget w)
        {
            try
            {
                if (w == null)
                {
                    return;
                }
                if (w.hover != null)
                {
                    w.hover.CloseTooltip();
                }
                if (w.root != null)
                {
                    UnityEngine.Object.Destroy(w.root);
                }
            }
            catch (Exception)
            {
                // expected: 控件对象已被星图销毁时销毁失败可忽略，字典引用由调用方移除
            }
        }

        private static bool CellVisible(AxialI cell)
        {
            try
            {
                if (SaveGame.Instance == null)
                {
                    return false;
                }
                ClusterFogOfWarManager.Instance fow = SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>();
                return fow != null && fow.GetCellRevealLevel(cell) == ClusterRevealLevel.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string StateText(MonsterState s)
        {
            switch (s)
            {
                case MonsterState.Combat: return "交战";
                case MonsterState.Pursue: return "追击";
                case MonsterState.Flee: return "逃跑（停止攻击）";
                case MonsterState.Leaving: return "离场";
                default: return "游荡";
            }
        }
    }

    /// <summary>悬停详情：条带上指针进入/离开。</summary>
    internal sealed class CombatBarHover : KMonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal GameObject bar;
        internal string tooltipText = "";
        private GameObject panel;

        public void OnPointerEnter(PointerEventData eventData)
        {
            try
            {
                CloseTooltip();
                Transform parent = TradeUi.OverlayParent();
                if (parent == null || string.IsNullOrEmpty(tooltipText))
                {
                    return;
                }
                int lines = tooltipText.Split('\n').Length;
                float h = 26f + lines * 18f;
                float w = 340f;
                panel = TradeUi.Node("SpaceBarTip", parent, 0f, 0f, w, h);
                RectTransform rt = panel.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0f, 0f);
                rt.position = new Vector3(Input.mousePosition.x + 16f, Input.mousePosition.y + 12f, 0f);
                Image bg = panel.AddComponent<Image>();
                bg.color = new Color(0.04f, 0.07f, 0.10f, 0.96f);
                bg.raycastTarget = false;
                TextMeshProUGUI t = TradeUi.Text("text", panel.transform, 8f, 6f, w - 16f, h - 12f,
                    tooltipText, 14f, new Color(0.88f, 0.94f, 0.98f, 1f), false);
                t.textWrappingMode = TextWrappingModes.Normal;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 迷你条悬停详情失败");
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CloseTooltip();
        }

        internal bool IsOpen { get { return panel != null; } }

        internal void CloseTooltip()
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
                panel = null;
            }
        }

        protected override void OnCleanUp()
        {
            try
            {
            CloseTooltip();
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:CombatBars", e); }
        }
    }
}
