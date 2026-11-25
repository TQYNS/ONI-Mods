// 用途：怪物页与通用文本：血条、状态、提示与绘制缓存

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class RocketPanelScreen : KScreen
    {
        private void BuildMonstersPage(ref float y)
        {
            List<MonsterRuntime> list = new List<MonsterRuntime>(CombatRuntime.Monsters);
            list.Sort(delegate(MonsterRuntime a, MonsterRuntime b)
            {
                if (a == null || a.def == null) return 1;
                if (b == null || b.def == null) return -1;
                // 问题14：按等级降序（S>A>B>C>D>E>F），同级按 HP 降序
                int ta = TierRank(a.def.tier);
                int tb = TierRank(b.def.tier);
                if (ta != tb) return tb.CompareTo(ta);
                return b.hp.CompareTo(a.hp);
            });
            float startY = y;
            const float cardH = 74f;         // 卡片本体高度
            const float stride = 82f;        // 卡距（本体 74 + 间隙 8）
            const float cardsBottom = 592f;  // 卡片区下边界：留出底部固定页码行
            const float footerY = 600f;      // 页码行固定 y（1440 面板 648 高，永不随卡片数量浮动）
            int perPage = Mathf.Max(1, 1 + Mathf.FloorToInt((cardsBottom - startY - cardH) / stride));
            int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)perPage));
            if (monsterPage >= pages)
            {
                monsterPage = pages - 1;
            }
            if (monsterPage < 0)
            {
                monsterPage = 0;
            }
            int from = monsterPage * perPage;
            int to = Mathf.Min(list.Count, from + perPage);
            for (int i = from; i < to; i++)
            {
                MonsterRuntime m = list[i];
                if (m == null || m.def == null) continue;
                Image mcard = TradeUi.RoundPanel("mcard" + i, rightRoot.transform, 0f, y, 840f, cardH, 8,
                    i % 2 == 0 ? TradeUi.RowInB : TradeUi.RowIn);
                mcard.raycastTarget = true;
                AddTooltip(mcard.gameObject, MonsterTooltip(m));
                string title;
                if (m.IsAlive)
                {
                    title = m.def.name + "<color=#" + ColorUtility.ToHtmlStringRGB(StateColor(m.state))
                        + ">（" + StateText(m.state) + "）</color>";
                }
                else
                {
                    title = m.def.name + "（已死亡）";
                }
                TradeUi.Text("mname" + i, rightRoot.transform, 14f, y + 6f, 600f, 22f, title,
                    15f, m.IsAlive ? TierColor(m.def.tier) : TradeUi.TextOff, false);   // 问题14：名字按等级染色

                BarLine("mhp" + i, 14f, y + 30f, 480f,
                    delegate { return m.def.hp > 0f ? Mathf.Clamp01(Mathf.Max(0f, m.hp) / m.def.hp) : 0f; },
                    delegate { float r = m.def.hp > 0f ? Mathf.Clamp01(Mathf.Max(0f, m.hp) / m.def.hp) : 0f; return HpFill(r); },
                    delegate { return "HP " + Mathf.Round(Mathf.Max(0f, m.hp)) + " / " + Mathf.Round(m.def.hp); });
                BarLine("marm" + i, 14f, y + 50f, 480f,
                    delegate { return m.def.armor > 0f ? Mathf.Clamp01(Mathf.Max(0f, m.armor) / m.def.armor) : 0f; },
                    delegate { return new Color(0.36f, 0.62f, 0.94f, 1f); },
                    delegate { return "装甲 " + Mathf.Round(Mathf.Max(0f, m.armor)) + " / " + Mathf.Round(m.def.armor); });
                y += stride;
            }
            if (list.Count == 0)
            {
                TradeUi.Text("nom", rightRoot.transform, 0f, y, 860f, 24f,
                    "当前没有星图怪物", 14f, TradeUi.TextDim, false);
                return;
            }
            // 底部固定行：左「共 N 只」+ 中间页码（卡片少也贴在底部，不跟着卡片跑）
            TradeUi.Text("mcount", rightRoot.transform, 0f, footerY + 4f, 300f, 22f,
                "共 " + list.Count + " 只" + (perPage < list.Count ? " · 每页 " + perPage : ""),
                12f, TradeUi.TextDim, false);
            if (pages > 1)
            {
                int target = monsterPage;
                TradeUi.MakeButton("◀", rightRoot.transform, 310f, footerY, 42f, 26f,
                    TradeUi.PanelAlt, TradeUi.Line, TradeUi.TextMain,
                    delegate { monsterPage = Mathf.Max(0, target - 1); dirty = true; });
                TradeUi.TextCenter("mpage", rightRoot.transform, 356f, footerY + 2f, 128f, 22f,
                    "第 " + (monsterPage + 1) + " / " + pages + " 页", 13f, TradeUi.TextMain);
                TradeUi.MakeButton("▶", rightRoot.transform, 488f, footerY, 42f, 26f,
                    TradeUi.PanelAlt, TradeUi.Line, TradeUi.TextMain,
                    delegate { monsterPage = Mathf.Min(pages - 1, target + 1); dirty = true; });
            }
        }

        private static Color AmmoTierColor(float ratio)
        {
            if (ratio >= 0.5f)
            {
                return new Color(0.36f, 0.82f, 0.45f, 1f);
            }
            if (ratio >= 0.2f)
            {
                return new Color(0.93f, 0.76f, 0.3f, 1f);
            }
            return new Color(0.88f, 0.33f, 0.33f, 1f);
        }

        private static Color HpFill(float ratio)
        {
            if (ratio > 0.6f)
            {
                return new Color(0.36f, 0.82f, 0.45f, 1f);
            }
            if (ratio > 0.3f)
            {
                return new Color(0.93f, 0.76f, 0.3f, 1f);
            }
            return new Color(0.88f, 0.33f, 0.33f, 1f);
        }

        private static Color StateColor(MonsterState s)
        {
            switch (s)
            {
                case MonsterState.Combat: return new Color(0.88f, 0.33f, 0.33f, 1f);
                case MonsterState.Pursue: return new Color(0.93f, 0.66f, 0.3f, 1f);
                case MonsterState.Flee: return new Color(0.93f, 0.78f, 0.3f, 1f);
                case MonsterState.Leaving: return new Color(0.62f, 0.66f, 0.74f, 1f);
                default: return new Color(0.36f, 0.82f, 0.45f, 1f);
            }
        }

        // 第十八轮-2/7：血量/装甲/燃料条改为"局部可刷新"：
        private void BarLine(string name, float x, float y, float w,
            System.Func<float> ratio, System.Func<Color> fill, System.Func<string> label)
        {
            float h = 14f;
            TradeUi.RoundPanel(name + "bg", rightRoot.transform, x, y, w, h, 4,
                new Color(0f, 0f, 0f, 0.55f));
            Image fillImg = TradeUi.RoundPanel(name + "fill", rightRoot.transform, x + 1f, y + 1f, 1f, h - 2f, 3, fill());
            TextMeshProUGUI txt = TradeUi.Text(name + "t", rightRoot.transform, x + w + 14f, y - 2f, 260f, 18f,
                label(), 12f, new Color(0.86f, 0.9f, 0.96f, 1f), false);
            System.Action upd = delegate
            {
                float f = Mathf.Clamp01(ratio());
                float fw = f * (w - 2f);
                if (fillImg != null)
                {
                    if (fw >= 1f)
                    {
                        fillImg.gameObject.SetActive(true);
                        fillImg.rectTransform.sizeDelta = new Vector2(fw, h - 2f);
                        fillImg.color = fill();
                    }
                    else
                    {
                        fillImg.gameObject.SetActive(false);
                    }
                }
                if (txt != null)
                {
                    txt.text = label();
                }
            };
            upd();
            liveUpdaters.Add(upd);
        }

        private MonsterRuntime FindTarget(int uid)
        {
            for (int i = 0; i < CombatRuntime.Monsters.Count; i++)
            {
                if (CombatRuntime.Monsters[i] != null && CombatRuntime.Monsters[i].uid == uid)
                {
                    return CombatRuntime.Monsters[i];
                }
            }
            return null;
        }

        private string AmmoSummary()
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            if (lo == null) return "无";
            Dictionary<string, float> dict = RocketAmmoDictionary.Build(selected);
            string s = "";
            for (int i = 0; i < lo.weaponModuleIds.Count; i++)
            {
                ModuleDef w = CombatDb.FindModule(lo.weaponModuleIds[i]);
                if (w == null) continue;
                string p = i < lo.weaponPrimaryAmmoIds.Count ? lo.weaponPrimaryAmmoIds[i] : "auto";
                s += (s.Length > 0 ? " | " : "") + (i + 1) + ":" + AmmoLabel(p);
            }
            foreach (KeyValuePair<string, float> kv in dict)
            {
                s += "  " + AmmoLabel(kv.Key) + " " + kv.Value.ToString("0.#") + "kg";
            }
            return s.Length == 0 ? "无" : s;
        }

        private static string AmmoLabel(string id)
        {
            if (string.IsNullOrEmpty(id)) return "无";
            if (id == "auto") return "自动";
            AmmoDef a = CombatDb.FindAmmo(id);
            return a != null ? a.name : id;
        }

        private static string CraftStatusText(Clustercraft.CraftStatus status)
        {
            switch (status)
            {
                case Clustercraft.CraftStatus.Grounded: return "未发射";
                case Clustercraft.CraftStatus.Launching: return "发射中";
                case Clustercraft.CraftStatus.InFlight: return "飞行中";
                case Clustercraft.CraftStatus.Landing: return "着陆中";
                default: return "未知";
            }
        }

        private static string ModuleTooltip(ModuleDef def)
        {
            if (def == null) return "";
            string s = def.name + "  " + RarityName(def.rarity) + "  容量需求 " + def.slotCost;
            ModuleStats st = def.stats;
            if (def.slotType == "weapon")
            {
                if (!string.IsNullOrEmpty(def.description))
                {
                    s += "\n" + def.description;
                }
                s += "\n射速 ×" + Pct(st.fireRateMul);
                s += "\n伤害 ×" + Pct(st.damageMul);
                if (st.rangeBonus != 0)
                {
                    s += "\n射程 +" + st.rangeBonus;
                }
            }
            else if (def.slotType == "utility")
            {
                if (st.armorPenAdd > 0f)
                {
                    s += "\n全火箭穿甲 +" + st.armorPenAdd.ToString("0.#");
                }
                if (st.armorDamageMul != 1f)
                {
                    s += "\n装甲伤害 ×" + Pct(st.armorDamageMul);
                }
                if (st.speedMul != 1f)
                {
                    s += "\n全火箭速度 ×" + Pct(st.speedMul);
                }
            }
            else if (def.slotType == "sensor")
            {
                if (st.scoutingBonus != 0)
                {
                    s += "\n视野 +" + st.scoutingBonus;
                }
                if (st.rangeBonus != 0)
                {
                    s += "\n射程 +" + st.rangeBonus;
                }
            }
            else if (def.slotType == "defense")
            {
                const int period = 60;
                float factor = period / (float)Mathf.Max(1, CombatDb.Shared.tickSeconds);
                if (st.hpRegenPctPerTick > 0f)
                {
                    s += "\n脱战 HP +"
                        + Mathf.RoundToInt(st.hpRegenPctPerTick * factor * 100f) + "%/min";
                }
                if (st.shieldRegenPctPerTick > 0f)
                {
                    s += "\n脱战护盾 +"
                        + Mathf.RoundToInt(st.shieldRegenPctPerTick * factor * 100f) + "%/min";
                }
            }
            return s;
        }

        private static string Pct(float mul)
        {
            return Mathf.RoundToInt(mul * 100f) + "%";
        }

        private static void AddTooltip(GameObject go, string text)
        {
            try
            {
                ToolTip tip = go.AddComponent<ToolTip>();
                tip.SetSimpleTooltip(text);
                go.AddOrGet<PanelHoverGuard>();   // 第十七轮-4：记录悬停 → 节拍刷新避让（防 Tooltip 闪烁）
            }
            catch (Exception)
            {
                // expected: tooltip 挂载失败不影响控件本身
            }
        }

        private static readonly Dictionary<string, Sprite> DashCache =
            new Dictionary<string, Sprite>();

        private static Sprite DashFrame(int w, int h)
        {
            string key = w + "x" + h;
            Sprite cached;
            if (DashCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }
            int iw = Mathf.Clamp(w, 16, 256);
            int ih = Mathf.Clamp(h, 16, 256);
            Texture2D tex = new Texture2D(iw, ih, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[iw * ih];
            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = clear;
            }
            int dash = 4;
            int gap = 4;
            int thick = 2;
            for (int x = 0; x < iw; x++)
            {
                if (x % (dash + gap) >= dash)
                {
                    continue;
                }
                for (int t = 0; t < thick; t++)
                {
                    px[t * iw + x] = Color.white;
                    px[(ih - 1 - t) * iw + x] = Color.white;
                }
            }
            for (int y = 0; y < ih; y++)
            {
                if (y % (dash + gap) >= dash)
                {
                    continue;
                }
                for (int t = 0; t < thick; t++)
                {
                    px[y * iw + t] = Color.white;
                    px[y * iw + (iw - 1 - t)] = Color.white;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            Sprite s = Sprite.Create(tex, new Rect(0f, 0f, iw, ih), new Vector2(0.5f, 0.5f), 1f, 0,
                SpriteMeshType.FullRect, new Vector4(0f, 0f, 0f, 0f));
            s.hideFlags = HideFlags.HideAndDontSave;
            DashCache[key] = s;
            return s;
        }

        private static Sprite ModuleSprite(ModuleDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.prefabId))
            {
                return null;
            }
            try
            {
                var t = Def.GetUISprite(new Tag(def.prefabId), "ui", false);
                if (t != null && t.first != null)
                {
                    return t.first;
                }
            }
            catch (Exception)
            {
                // expected: 预制体 ui 精灵取不到，回退到动画帧
            }
            try
            {
                if (!string.IsNullOrEmpty(def.anim))
                {
                    KAnimFile file = Assets.GetAnim(def.anim);
                    if (file != null)
                    {
                        return Def.GetUISpriteFromMultiObjectAnim(file, "ui", false, "");
                    }
                }
            }
            catch (Exception)
            {
                // expected: 动画帧精灵也取不到时返回 null，由调用方用默认图标
            }
            return null;
        }

        private static string SlotLabel(string type)
        {
            switch (type)
            {
                case "weapon": return "武器";
                case "defense": return "防御";
                case "utility": return "辅助";
                case "sensor": return "感知";
                default: return type;
            }
        }

        private static int RarityRank(string rarity)
        {
            if (rarity == "legendary") return 3;
            if (rarity == "master") return 2;
            return 1;
        }

        private static string RarityName(string rarity)
        {
            if (rarity == "legendary") return "传奇";
            if (rarity == "master") return "大师";
            return "普通";
        }

        private static Color RarityColor(string rarity)
        {
            if (rarity == "legendary") return TradeUi.Gold;
            if (rarity == "master") return TradeUi.Accent;
            return TradeUi.TextMain;
        }

        private static string StateText(MonsterState s)
        {
            switch (s)
            {
                case MonsterState.Combat: return "交战中";
                case MonsterState.Pursue: return "追击中";
                case MonsterState.Flee: return "逃跑中";
                case MonsterState.Leaving: return "离场中";
                case MonsterState.Roam: return "游荡";
                default: return "存活";
            }
        }

        private static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
            }
        }
    }
}
