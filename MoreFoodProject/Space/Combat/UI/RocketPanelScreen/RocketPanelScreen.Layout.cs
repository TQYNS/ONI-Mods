// 用途：火箭面板布局：左右栏、页签、槽位概览与卡片

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class RocketPanelScreen : KScreen
    {
        private static List<Clustercraft> AllCrafts()
        {
            List<Clustercraft> list = new List<Clustercraft>();
            foreach (Clustercraft c in Components.Clustercrafts)
            {
                if (c != null && c.gameObject != null && c.gameObject.scene.IsValid()
                    && RocketValidity.IsValidCraft(c))
                {
                    list.Add(c);
                }
            }
            list.Sort(delegate(Clustercraft a, Clustercraft b)
            {
                return string.CompareOrdinal(a.Name, b.Name);
            });
            return list;
        }

        private void RebuildLeft()
        {
            DestroyChildren(leftRoot.transform);
            TradeUi.RoundPanel("listbg", leftRoot.transform, 0f, 0f, 270f, 648f, 10, TradeUi.PanelAlt);
            TradeUi.Text("h", leftRoot.transform, 12f, 10f, 240f, 24f, "火箭列表", 16f, TradeUi.Gold, false);
            List<Clustercraft> crafts = AllCrafts();
            for (int i = 0; i < crafts.Count && i < 16; i++)
            {
                Clustercraft c = crafts[i];
                RocketCombatState st = c.GetComponent<RocketCombatState>();
                bool combat = st != null && st.inCombat;
                string label = (combat ? "● " : "") + c.Name;
                Color col = c == selected ? TradeUi.Accent : TradeUi.TextMain;
                TradeUi.MakeButton(label, leftRoot.transform, 10f, 44f + i * 40f, 250f, 34f,
                    c == selected ? TradeUi.TabOn : TradeUi.TabOff,
                    c == selected ? TradeUi.Accent : TradeUi.Line,
                    col, delegate { Select(c); });
            }
            if (crafts.Count == 0)
            {
                TradeUi.Text("empty", leftRoot.transform, 14f, 50f, 240f, 24f,
                    "没有火箭", 14f, TradeUi.TextDim, false);
            }
        }

        private void RebuildRight()
        {
            DestroyChildren(rightRoot.transform);
            if (selected == null)
            {
                TradeUi.RoundPanel("nocard", rightRoot.transform, 0f, 76f, 840f, 92f, 8, TradeUi.PanelAlt);
                TradeUi.Text("none", rightRoot.transform, 14f, 84f, 800f, 26f,
                    "暂无火箭", 16f, TradeUi.TextMain, false);
                TradeUi.Text("nonetip", rightRoot.transform, 14f, 112f, 800f, 22f,
                    "在发射台造一艘火箭后，从火箭控制台的「装配」按钮或管理菜单「火箭」页签打开本面板",
                    13f, TradeUi.Neg, false);
                TradeUi.Text("nonebar", rightRoot.transform, 14f, 138f, 800f, 20f,
                    "没有火箭时，装配、怪物信息都不可用", 12f, TradeUi.TextDim, false);
                return;
            }
            RocketCombatState st = selected.GetComponent<RocketCombatState>();
            if (st != null)
            {
                st.SyncIfNeeded();
            }
            RocketLoadout lo = RocketLoadout.Get(selected);
            if (lo != null)
            {
                lo.SyncIfNeeded();
            }
            BuildTabs();
            float y;
            if (page == Page.Rocket)
            {
                float cursor = BuildRocketCard(st);
                if (st != null && st.inCombat)
                {
                    cursor = BuildEnemyCards(st, cursor + 6f);
                }
                BuildSlotOverview(cursor + 4f);
                y = cursor + 58f;
            }
            else
            {
                y = 76f;
            }
            switch (page)
            {
                case Page.Rocket:
                    if (ammoPickerOpen)
                    {
                        BuildAmmoPicker(ref y);
                    }
                    else
                    {
                        BuildRocketPage(st, ref y);
                    }
                    break;
                case Page.Assembly:
                    if (pickerOpen)
                    {
                        BuildModulePicker(ref y);
                    }
                    else
                    {
                        BuildAssemblyPage(st, ref y);
                    }
                    break;
                case Page.Monsters:
                    BuildMonstersPage(ref y);
                    break;
            }
        }

        private void BuildTabs()
        {
            string[] labels = { "火箭", "装配", "怪物" };
            Page[] pages = { Page.Rocket, Page.Assembly, Page.Monsters };
            for (int i = 0; i < labels.Length; i++)
            {
                Page p = pages[i];
                bool on = page == p;
                TradeUi.MakeButton(labels[i], rightRoot.transform, i * 120f, 0f, 116f, 34f,
                    on ? TradeUi.TabOn : TradeUi.TabOff,
                    on ? TradeUi.Accent : TradeUi.Line,
                    on ? TradeUi.Accent : TradeUi.TextDim,
                    delegate
                    {
                        page = p;
                        pickerOpen = false;
                        ammoPickerOpen = false;
                        dirty = true;
                    });
            }
        }

        private void BuildSlotOverview(float top)
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            string[] types = { "weapon", "defense", "utility", "sensor" };
            string[] names = { "武器", "防御", "辅助", "感知" };
            for (int i = 0; i < types.Length; i++)
            {
                SlotEntry slot = lo != null ? lo.GetSlots(selected, types[i])
                    : new SlotEntry { type = types[i] };
                int used, cost;
                if (lo != null)
                {
                    lo.GetSlotUsed(types[i], out used, out cost);
                }
                else
                {
                    used = 0;
                    cost = 0;
                }
                float x = (i % 2) * 430f;
                float yy = top + (i / 2) * 26f;
                string head = names[i] + " " + used + "/" + slot.count;
                Color headColor = (used > slot.count || cost > slot.capacity) ? TradeUi.Neg : TradeUi.Gold;
                TradeUi.Text("slot_" + types[i], rightRoot.transform, x, yy, 96f, 20f,
                    head, 13f, headColor, false);
                float countRatio = slot.count > 0 ? Mathf.Clamp01((float)used / slot.count) : 0f;
                float capRatio = slot.capacity > 0 ? Mathf.Clamp01((float)cost / slot.capacity) : 0f;
                bool over = slot.count > 0 ? used > slot.count : false;
                over = over || (slot.capacity > 0 && cost > slot.capacity);
                string capText = slot.capacity > 0 ? ("容量 " + cost + "/" + slot.capacity) : "容量 —";
                BarLine("slotb_" + types[i], x + 100f, yy + 2f, 120f,
                    delegate { return Mathf.Min(countRatio, capRatio); },
                    delegate { return over ? TradeUi.Neg : TradeUi.Accent; },
                    delegate { return capText; });
            }
        }

        private void BuildRocketPage(RocketCombatState st, ref float y)
        {
            if (st == null) return;
            // 交战时不再隐藏弹药区（问题4）：改为锁定态，脱战自动复原。
            BuildAmmoControls(ref y, st.inCombat);
        }

        private static Color CraftStatusColor(Clustercraft.CraftStatus s)
        {
            switch (s)
            {
                case Clustercraft.CraftStatus.Grounded: return new Color(0.36f, 0.82f, 0.45f, 1f);
                case Clustercraft.CraftStatus.Launching: return new Color(0.93f, 0.78f, 0.3f, 1f);
                case Clustercraft.CraftStatus.InFlight: return new Color(0.36f, 0.62f, 0.94f, 1f);
                case Clustercraft.CraftStatus.Landing: return new Color(0.93f, 0.66f, 0.3f, 1f);
                default: return TradeUi.TextDim;
            }
        }

        private static float FuelCapacity(Clustercraft craft)
        {
            float sum = 0f;
            try
            {
                if (craft == null || craft.ModuleInterface == null)
                {
                    return 0f;
                }
                foreach (Ref<RocketModuleCluster> r in craft.ModuleInterface.ClusterModules)
                {
                    RocketModuleCluster m = r.Get();
                    if (m == null)
                    {
                        continue;
                    }
                    IFuelTank t = m.GetComponent<IFuelTank>();
                    Storage tank = t != null ? t.Storage as Storage : null;
                    if (tank != null)
                    {
                        sum += tank.capacityKg;
                    }
                }
            }
            catch (Exception)
            {
                return sum;
            }
            return sum;
        }

        // 一次遍历同时取质量与容量：顶栏分子分母同源，且不多一次模块枚举
        private static void OxidizerAmounts(Clustercraft craft, out float remaining, out float capacity)
        {
            remaining = 0f;
            capacity = 0f;
            try
            {
                if (craft == null || craft.ModuleInterface == null)
                {
                    return;
                }
                foreach (Ref<RocketModuleCluster> r in craft.ModuleInterface.ClusterModules)
                {
                    RocketModuleCluster m = r.Get();
                    if (m == null)
                    {
                        continue;
                    }
                    OxidizerTank t = m.GetComponent<OxidizerTank>();
                    if (t != null && t.storage != null)
                    {
                        capacity += t.storage.capacityKg;
                        remaining += t.storage.MassStored();
                    }
                }
            }
            catch (Exception)
            {
                // expected: 模块枚举失败时保留已累计的部分值
            }
        }

        private float BuildRocketCard(RocketCombatState st)
        {
            float top = 76f;
            TradeUi.RoundPanel("rcard", rightRoot.transform, 0f, top, 840f, 164f, 8, TradeUi.PanelAlt);
            // 交战时优先显示"交战中"（此前只显示 CraftStatus，交战态在标题不可见 → 问题1）
            bool titleInCombat = st != null && st.inCombat;
            string titleStatus = titleInCombat ? "交战中" : CraftStatusText(selected.Status);
            Color titleColor = titleInCombat
                ? new Color(0.95f, 0.35f, 0.30f, 1f)
                : CraftStatusColor(selected.Status);
            string title = selected.Name
                + "<color=#" + ColorUtility.ToHtmlStringRGB(titleColor)
                + ">（" + titleStatus + "）</color>";
            TradeUi.Text("rname", rightRoot.transform, 14f, top + 4f, 800f, 22f, title, 16f,
                TradeUi.TextMain, false);

            BarLine("rhp", 14f, top + 28f, 210f,
                delegate { return st != null && st.totalHpCache > 0f ? Mathf.Clamp01(st.Hp / st.totalHpCache) : 0f; },
                delegate { float r = st != null && st.totalHpCache > 0f ? Mathf.Clamp01(st.Hp / st.totalHpCache) : 0f; return HpFill(r); },
                delegate { return st != null ? "HP " + Mathf.Round(st.Hp) + " / " + Mathf.Round(st.totalHpCache) : "HP —"; });
            BarLine("rarm", 430f, top + 28f, 210f,
                delegate { return st != null && st.maxArmor > 0f ? Mathf.Clamp01(st.armor / st.maxArmor) : 0f; },
                delegate { return new Color(0.36f, 0.62f, 0.94f, 1f); },
                delegate { return st != null ? "装甲 " + Mathf.Round(st.armor) + " / " + Mathf.Round(st.maxArmor) : "装甲 —"; });
            float fuel = 0f;
            string hexText = "—";
            if (selected.ModuleInterface != null)
            {
                fuel = selected.ModuleInterface.FuelRemaining;
                float per = selected.FuelPerDistance;
                hexText = per > 0.0001f
                    ? Mathf.FloorToInt(Mathf.Max(0f,
                        selected.ModuleInterface.BurnableMassRemaining / per / 600f)).ToString()
                    : "—";
            }
            float fuelCap = FuelCapacity(selected);
            float ox;
            float oxCap;
            OxidizerAmounts(selected, out ox, out oxCap);   // 顶栏分子分母同为 kg，推进当量不能当质量用
            BarLine("rfuel", 14f, top + 52f, 210f,
                delegate { return fuelCap > 0f ? Mathf.Clamp01(fuel / fuelCap) : 0f; },
                delegate { return new Color(0.93f, 0.66f, 0.3f, 1f); },
                delegate { return fuelCap > 0f ? "燃料 " + Mathf.Round(fuel) + " / " + Mathf.Round(fuelCap) + " kg" : "燃料 " + Mathf.Round(fuel) + " kg"; });
            BarLine("rox", 430f, top + 52f, 210f,
                delegate { return oxCap > 0f ? Mathf.Clamp01(ox / oxCap) : 0f; },
                delegate { return new Color(0.36f, 0.78f, 0.82f, 1f); },
                delegate { return oxCap > 0f ? "氧化剂 " + Mathf.Round(ox) + " / " + Mathf.Round(oxCap) + " kg" : "氧化剂 " + Mathf.Round(ox) + " kg"; });

            TradeUi.Text("rrange", rightRoot.transform, 14f, top + 78f, 800f, 20f,
                "预计可运动 " + hexText + " 格    防御 "
                + (st != null ? Mathf.Round(st.defenseCache).ToString() : "—"),
                13f, TradeUi.TextDim, false);

            // Bug1：攻击距离改为"预计可运动"下方独立一行（旧版画在右上方，压住了装甲条/卡片外沿）
            TradeUi.Text("ratk", rightRoot.transform, 14f, top + 102f, 800f, 20f,
                "攻击距离 " + Mathf.Round(CombatRuntime.AttackRangeOf(selected)) + " 星图格",
                13f, TradeUi.TextMain, false);

            bool control = RocketLoadout.Get(selected) != null
                && RocketLoadout.Get(selected).HasControlStation(selected);
            TradeUi.Text("rctrl", rightRoot.transform, 14f, top + 126f, 800f, 22f,
                control ? "控制台  正常（可改装）" : "控制台  缺失（改装锁定）",
                13f, control ? TradeUi.Pos : TradeUi.Neg, false);
            return top + 168f;
        }

        private float BuildEnemyCards(RocketCombatState st, float top)
        {
            List<MonsterRuntime> list = new List<MonsterRuntime>();
            for (int i = 0; i < CombatRuntime.Monsters.Count; i++)
            {
                MonsterRuntime m = CombatRuntime.Monsters[i];
                if (m == null || m.def == null || !m.IsAlive)
                {
                    continue;
                }
                // 只显示"仍与本火箭交战（交战/追击）"的怪；逃跑/游荡/离场一律撤下卡片，
                if (m.aggroCraft != selected
                    || (m.state != MonsterState.Combat && m.state != MonsterState.Pursue))
                {
                    continue;
                }
                list.Add(m);
            }
            if (list.Count == 0)
            {
                return top;
            }
            list.Sort(delegate(MonsterRuntime a, MonsterRuntime b)
            {
                return b.hp.CompareTo(a.hp);
            });
            float y = top;
            int shown = Mathf.Min(3, list.Count);
            for (int i = 0; i < shown; i++)
            {
                MonsterRuntime m = list[i];
                Image ecard = TradeUi.RoundPanel("ecard" + i, rightRoot.transform, 0f, y, 840f, 46f, 8,
                    TradeUi.RowInB);
                ecard.raycastTarget = true;
                AddTooltip(ecard.gameObject, MonsterTooltip(m));
                TradeUi.Text("ename" + i, rightRoot.transform, 14f, y + 4f, 300f, 20f,
                    m.def.name + "<color=#" + ColorUtility.ToHtmlStringRGB(StateColor(m.state))
                    + ">（" + StateText(m.state) + "）</color>",
                    14f, TradeUi.TextMain, false);
                BarLine("ehp" + i, 14f, y + 24f, 240f,
                    delegate { return m.def.hp > 0f ? Mathf.Clamp01(Mathf.Max(0f, m.hp) / m.def.hp) : 0f; },
                    delegate { float r = m.def.hp > 0f ? Mathf.Clamp01(Mathf.Max(0f, m.hp) / m.def.hp) : 0f; return HpFill(r); },
                    delegate { return "HP " + Mathf.Round(Mathf.Max(0f, m.hp)) + " / " + Mathf.Round(m.def.hp); });
                BarLine("earm" + i, 430f, y + 24f, 160f,
                    delegate { return m.def.armor > 0f ? Mathf.Clamp01(Mathf.Max(0f, m.armor) / m.def.armor) : 0f; },
                    delegate { return new Color(0.36f, 0.62f, 0.94f, 1f); },
                    delegate { return "装甲 " + Mathf.Round(Mathf.Max(0f, m.armor)) + " / " + Mathf.Round(m.def.armor); });
                y += 50f;
            }
            if (list.Count > shown)
            {
                TradeUi.Text("emore", rightRoot.transform, 14f, y, 840f, 20f,
                    "另有 " + (list.Count - shown) + " 个交战方（见「怪物」页）", 12f, TradeUi.Neg, false);
                y += 22f;
            }
            return y;
        }

        private static string MonsterTooltip(MonsterRuntime m)
        {
            if (m == null || m.def == null)
            {
                return "";
            }
            string s = STRINGS.UI.FormatAsLink(m.def.name, MonsterCodexGenerator.LinkId(m.def.id)) + "  " + m.def.tier + " 级";   // 问题15：名字可点击跳百科
            if (!string.IsNullOrEmpty(m.def.description))
            {
                s += "\n" + m.def.description;
            }
            s += "\n生命 " + Mathf.Round(Mathf.Max(0f, m.hp)) + " / " + Mathf.Round(m.def.hp);
            s += "\n装甲 " + Mathf.Round(Mathf.Max(0f, m.armor)) + " / " + Mathf.Round(m.def.armor);
            s += "\n防御 " + Mathf.Round(m.def.defense)
                + "  攻击 " + Mathf.Round(m.def.attack)
                + "  穿甲 " + Mathf.Round(m.def.armorPenetration);
            s += "\n射程 " + m.def.attackRange + " 格  警戒 " + m.def.alertRange + " 格"
                + "  攻击间隔 " + m.def.attackInterval.ToString("0.#") + " 秒";
            s += "\n速度 " + m.def.speed.ToString("0.##");
            s += "\n状态 " + StateText(m.state);
            if (!string.IsNullOrEmpty(m.region))
            {
                s += "  区域 " + (m.region == "sub" ? "次星区" : "主星区");
            }
            return s;
        }
    }
}
