// 用途：选择器与弹药：模块选择、装填条、弹药选择与瓦片

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class RocketPanelScreen : KScreen
    {
        private void BuildModulePicker(ref float y)
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            if (lo == null) return;
            TradeUi.Text("pick_title", rightRoot.transform, 0f, y, 600f, 26f,
                "选择 " + SlotLabel(pickerType) + " 模块", 17f, TradeUi.Gold, false);
            TradeUi.MakeButton("返回", rightRoot.transform, 740f, y - 2f, 110f, 30f,
                TradeUi.Panel, TradeUi.Accent, TradeUi.TextMain,
                delegate { pickerOpen = false; dirty = true; });
            y += 36f;

            List<ModuleDef> list = new List<ModuleDef>();
            for (int i = 0; i < CombatDb.Modules.Count; i++)
            {
                if (CombatDb.Modules[i].slotType == pickerType)
                {
                    list.Add(CombatDb.Modules[i]);
                }
            }
            list.Sort(delegate(ModuleDef a, ModuleDef b)
            {
                string ra = null, rb = null;
                bool ca = lo.CanEquip(selected, a.id, out ra);
                bool cb = lo.CanEquip(selected, b.id, out rb);
                if (ca != cb) return ca ? -1 : 1;
                int c = RarityRank(b.rarity).CompareTo(RarityRank(a.rarity));
                if (c != 0) return c;
                return string.CompareOrdinal(a.id, b.id);
            });

            int cols = 9;
            for (int i = 0; i < list.Count; i++)
            {
                ModuleDef def = list[i];
                string reason = null;
                bool can = lo.CanEquip(selected, def.id, out reason);
                float x = (i % cols) * 84f;
                float yy = y + (i / cols) * 94f;
                BuildPickerButton(def, can, reason, x, yy);
            }
        }

        private void BuildPickerButton(ModuleDef def, bool canEquip, string equipReason, float x, float y)
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            bool has = lo != null && lo.virtualStorage.Contains(def.id);
            if (!has && !string.IsNullOrEmpty(def.prefabId))
            {
                // 模块实物可能在货舱/内舱地面/模块自带 Storage —— 按"全仓"统计
                has = RocketInventory.AvailableAnywhere(selected, new Tag(def.prefabId)) >= 0.9f;
            }
            bool can = canEquip && has;
            string reason = can ? null : (has ? (equipReason ?? "不可装") : "火箭货舱里没有这个模块");

            GameObject box = TradeUi.Node(def.id, rightRoot.transform, x, y, 72f, 84f);
            Image hit = box.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            Image bg = TradeUi.RoundPanel("bg", box.transform, 0f, 0f, 72f, 84f, 8,
                can ? TradeUi.PanelAlt : TradeUi.Panel);
            bg.raycastTarget = false;

            Sprite sprite = ModuleSprite(def);
            if (sprite != null)
            {
                GameObject iconGo = TradeUi.Node("icon", box.transform, 10f, 4f, 52f, 52f);
                Image icon = iconGo.AddComponent<Image>();
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.color = can ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                icon.raycastTarget = false;
            }
            TextMeshProUGUI name = TradeUi.Text("name", box.transform, 1f, 58f, 70f, 16f, def.name, 9f,
                can ? RarityColor(def.rarity) : TradeUi.TextOff, false);
            name.alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI cost = TradeUi.Text("cost", box.transform, 1f, 71f, 70f, 14f,
                can ? ("容量需求：" + def.slotCost) : (has ? "不可装" : "缺货"),
                8f, can ? TradeUi.TextDim : TradeUi.Neg, false);
            cost.alignment = TextAlignmentOptions.Center;

            if (!can)
            {
                AddTooltip(box, ModuleTooltip(def) + "\n<color=#E86A6A>" + reason + "</color>");
                return;
            }
            Button button = box.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(delegate
            {
                if (!CombatAssembly.RequestEquip(selected, def.id))
                {
                    TradeUi.Notify("装配失败", "货舱里的模块没装上，看日志");
                }
                pickerOpen = false;
                dirty = true;
            });
            AddTooltip(box, ModuleTooltip(def) + "\n点击装入");
        }

        private void BuildAmmoControls(ref float y, bool locked = false)
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            if (lo == null || y > 620f)
            {
                return;
            }
            if (lo.weaponModuleIds.Count == 0)
            {
                TradeUi.Text("ammoh", rightRoot.transform, 0f, y, 860f, 22f, "发射药", 14f, TradeUi.Gold, false);
                y += 24f;
                TradeUi.Text("ammlock", rightRoot.transform, 0f, y, 560f, 22f,
                    "未安装武器模块 · 主弹/备弹 已锁定", 13f, TradeUi.Neg, false);
                TradeUi.MakeButton("主弹 锁定", rightRoot.transform, 570f, y - 2f, 120f, 26f,
                    TradeUi.Panel, TradeUi.Neg, TradeUi.Neg, null);
                TradeUi.MakeButton("备弹 锁定", rightRoot.transform, 700f, y - 2f, 120f, 26f,
                    TradeUi.Panel, TradeUi.Neg, TradeUi.Neg, null);
                y += 30f;
                return;
            }
            // 交战时不再隐藏弹药区（问题4）：改为锁定态（置灰+禁点+装弹进度仍显示），脱战自动复原。
            TradeUi.Text("ammoh", rightRoot.transform, 0f, y, 860f, 22f,
                locked ? "发射药（交战中锁定）" : "发射药", 14f, TradeUi.Gold, false);
            y += 24f;
            Dictionary<string, float> dict = RocketAmmoDictionary.Build(selected);
            float nowSec = TradeRegistry.Now();
            float reloadBase = Mathf.Max(0.01f, CombatDb.Shared.attackIntervalMax);
            for (int i = 0; i < lo.weaponModuleIds.Count && y < 640f; i++)
            {
                ModuleDef w = CombatDb.FindModule(lo.weaponModuleIds[i]);
                if (w == null) continue;
                string primary = i < lo.weaponPrimaryAmmoIds.Count ? lo.weaponPrimaryAmmoIds[i] : "auto";
                string backup = i < lo.weaponBackupAmmoIds.Count ? lo.weaponBackupAmmoIds[i] : null;
                int captured = i;
                float maxKg = MaxKgOfCaliber(w.caliber, dict);
                float remain = i < lo.weaponAttackUntil.Count
                    ? Mathf.Max(0f, lo.weaponAttackUntil[i] - nowSec) : 0f;
                float reloadRatio = Mathf.Clamp01(remain / reloadBase);
                TradeUi.Text("w" + i, rightRoot.transform, 0f, y, 200f, 24f,
                    (i + 1) + " " + w.name, 12f, TradeUi.TextMain, false);
                System.Action pClick = locked ? (System.Action)null : delegate
                {
                    ammoPickerOpen = true;
                    ammoPickerWeapon = captured;
                    ammoPickerPrimary = true;
                    dirty = true;
                };
                Button pb = TradeUi.MakeButton("主弹 " + AmmoLabel(primary),
                    rightRoot.transform, 210f, y - 2f, 220f, 26f,
                    locked ? TradeUi.Panel : TradeUi.PanelAlt, TradeUi.Line,
                    locked ? TradeUi.TextDim : TradeUi.Accent, pClick);
                float pRatio = AmmoKgOf(dict, primary) / Mathf.Max(1f, maxKg);
                float bRatio = AmmoKgOf(dict, backup) / Mathf.Max(1f, maxKg);
                AmmoBar(pb, 220f, pRatio, AmmoTierColor(pRatio));
                ReloadBar(pb, 220f, captured, reloadRatio);   // 第十八轮-7：局部刷新灰条
                System.Action bClick = locked ? (System.Action)null : delegate
                {
                    ammoPickerOpen = true;
                    ammoPickerWeapon = captured;
                    ammoPickerPrimary = false;
                    dirty = true;
                };
                Button bb = TradeUi.MakeButton("备弹 " + AmmoLabel(backup),
                    rightRoot.transform, 440f, y - 2f, 220f, 26f,
                    locked ? TradeUi.Panel : TradeUi.PanelAlt, TradeUi.Line,
                    locked ? TradeUi.TextDim : TradeUi.TextDim, bClick);
                AmmoBar(bb, 220f, bRatio, AmmoTierColor(bRatio));
                ReloadBar(bb, 220f, captured, reloadRatio);   // 第十八轮-7：局部刷新灰条
                y += 34f;
            }
        }

        // 第十八轮-7：装弹灰条改为"局部可刷新" —— 战斗节拍只更新这条灰色遮罩的宽度，
        private void ReloadBar(Button btn, float w, int weaponIndex, float ratio)
        {
            if (btn == null)
            {
                return;
            }
            Image box = TradeUi.RoundPanel("rload", btn.transform, 3f, 3f,
                Mathf.Max(1f, Mathf.Clamp01(ratio) * (w - 6f)), 22f, 4,
                new Color(0.35f, 0.35f, 0.35f, 0.5f));
            box.gameObject.SetActive(ratio > 0.01f);
            System.Action upd = delegate
            {
                if (box == null)
                {
                    return;
                }
                RocketLoadout l = RocketLoadout.Get(selected);
                float nowSec = TradeRegistry.Now();
                float rb = Mathf.Max(0.01f, CombatDb.Shared.attackIntervalMax);
                float rm = l != null && weaponIndex < l.weaponAttackUntil.Count
                    ? Mathf.Max(0f, l.weaponAttackUntil[weaponIndex] - nowSec) : 0f;
                float rr = Mathf.Clamp01(rm / rb);
                if (rr <= 0.01f)
                {
                    box.gameObject.SetActive(false);
                }
                else
                {
                    box.gameObject.SetActive(true);
                    box.rectTransform.sizeDelta = new Vector2(Mathf.Clamp01(rr) * (w - 6f), 22f);
                }
            };
            liveUpdaters.Add(upd);
        }

        private void AmmoBar(Button btn, float w, float ratio, Color color)
        {
            if (btn == null)
            {
                return;
            }
            TradeUi.RoundPanel("abar_bg", btn.transform, 3f, 20f, w - 6f, 4f, 2,
                new Color(0f, 0f, 0f, 0.55f));
            float fw = Mathf.Clamp01(ratio) * (w - 8f);
            if (fw >= 1f)
            {
                TradeUi.RoundPanel("abar_f", btn.transform, 4f, 21f, fw, 2f, 1, color);
            }
        }

        private static float AmmoKgOf(Dictionary<string, float> dict, string ammoId)
        {
            if (dict == null || string.IsNullOrEmpty(ammoId) || ammoId == "auto")
            {
                return 0f;
            }
            AmmoDef a = CombatDb.FindAmmo(ammoId);
            if (a == null || string.IsNullOrEmpty(a.prefabId))
            {
                return 0f;
            }
            float kg;
            return dict.TryGetValue(a.prefabId, out kg) ? kg : 0f;
        }

        private static float MaxKgOfCaliber(string caliber, Dictionary<string, float> dict)
        {
            List<AmmoDef> pool = CombatDb.AmmoOfCaliber(caliber);
            float max = 0f;
            if (pool == null)
            {
                return max;
            }
            for (int i = 0; i < pool.Count; i++)
            {
                float kg = AmmoKgOf(dict, pool[i].id);
                if (kg > max)
                {
                    max = kg;
                }
            }
            return max;
        }

        private void BuildAmmoPicker(ref float y)
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            if (lo == null || ammoPickerWeapon < 0 || ammoPickerWeapon >= lo.weaponModuleIds.Count)
            {
                ammoPickerOpen = false;
                return;
            }
            ModuleDef w = CombatDb.FindModule(lo.weaponModuleIds[ammoPickerWeapon]);
            if (w == null)
            {
                ammoPickerOpen = false;
                return;
            }
            TradeUi.Text("ap_title", rightRoot.transform, 0f, y, 700f, 26f,
                "选择" + (ammoPickerPrimary ? "主弹" : "备弹") + " · " + w.name, 17f, TradeUi.Gold, false);
            TradeUi.MakeButton("返回", rightRoot.transform, 740f, y - 2f, 110f, 30f,
                TradeUi.Panel, TradeUi.Accent, TradeUi.TextMain,
                delegate { ammoPickerOpen = false; dirty = true; });
            y += 36f;
            TradeUi.Text("ap_tip", rightRoot.transform, 0f, y, 860f, 20f,
                ammoPickerPrimary
                    ? "主弹不要求有库存；无库存时按备弹 / 自动档发射"
                    : "备弹排除主弹；与主弹相同的弹种标记为不可选",
                12f, TradeUi.TextDim, false);
            y += 24f;

            Dictionary<string, float> dict = RocketAmmoDictionary.Build(selected);
            List<AmmoDef> pool = CombatDb.AmmoOfCaliber(w.caliber);
            if (pool == null || pool.Count == 0)
            {
                TradeUi.Text("ap_none", rightRoot.transform, 0f, y, 860f, 24f,
                    "该武器口径没有匹配弹药", 13f, TradeUi.Neg, false);
                return;
            }
            List<AmmoDef> list = new List<AmmoDef>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pool.Count; i++)
            {
                AmmoDef a = pool[i];
                if (a == null || string.IsNullOrEmpty(a.id) || !seen.Add(a.id))
                {
                    continue;
                }
                list.Add(a);
            }
            list.Sort(delegate(AmmoDef a, AmmoDef b)
            {
                float ka = AmmoKgOf(dict, a.id);
                float kb = AmmoKgOf(dict, b.id);
                if ((ka > 0f) != (kb > 0f))
                {
                    return ka > 0f ? -1 : 1;
                }
                if (Mathf.Abs(ka - kb) > 0.01f)
                {
                    return kb.CompareTo(ka);
                }
                return string.CompareOrdinal(a.id, b.id);
            });

            string curPrimary = ammoPickerWeapon < lo.weaponPrimaryAmmoIds.Count
                ? lo.weaponPrimaryAmmoIds[ammoPickerWeapon] : "auto";
            string curBackup = ammoPickerWeapon < lo.weaponBackupAmmoIds.Count
                ? lo.weaponBackupAmmoIds[ammoPickerWeapon] : null;
            string current = ammoPickerPrimary ? curPrimary : curBackup;
            float maxKg = MaxKgOfCaliber(w.caliber, dict);

            int cols = 9;
            int start = 0;
            if (ammoPickerPrimary)
            {
                BuildAutoTile(current == "auto", 0f, y);
                start = 1;
            }
            for (int i = 0; i < list.Count; i++)
            {
                AmmoDef a = list[i];
                bool reserved = !ammoPickerPrimary && a.id == curPrimary;
                float kg = AmmoKgOf(dict, a.id);
                int slot = i + start;
                float x = (slot % cols) * 92f;
                float yy = y + (slot / cols) * 100f;
                BuildAmmoTile(a, kg, maxKg, a.id == current, reserved, x, yy);
            }
        }

        private void BuildAutoTile(bool isSelected, float x, float y)
        {
            GameObject box = TradeUi.Node("ammo_auto", rightRoot.transform, x, y, 84f, 92f);
            Image hit = box.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            Image bg = TradeUi.RoundPanel("bg", box.transform, 0f, 0f, 84f, 92f, 8,
                isSelected ? TradeUi.Panel : TradeUi.PanelAlt);
            bg.raycastTarget = false;
            TextMeshProUGUI t = TradeUi.Text("name", box.transform, 1f, 30f, 82f, 22f, "自动", 13f,
                TradeUi.Accent, false);
            t.alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI s = TradeUi.Text("sub", box.transform, 1f, 54f, 82f, 18f, "按可用选",
                8f, TradeUi.TextDim, false);
            s.alignment = TextAlignmentOptions.Center;
            Button button = box.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(delegate
            {
                RocketLoadout lo = RocketLoadout.Get(selected);
                if (lo != null)
                {
                    string back = ammoPickerWeapon < lo.weaponBackupAmmoIds.Count
                        ? lo.weaponBackupAmmoIds[ammoPickerWeapon] : null;
                    lo.SetWeaponAmmo(ammoPickerWeapon, "auto", back);
                }
                ammoPickerOpen = false;
                dirty = true;
            });
            AddTooltip(box, "自动\n按可用库存挑选同口径弹药（主弹 → 备弹 → 自动）");
        }

        private void BuildAmmoTile(AmmoDef a, float kg, float maxKg, bool isSelected, bool reserved,
            float x, float y)
        {
            bool can = !reserved;
            GameObject box = TradeUi.Node(a.id, rightRoot.transform, x, y, 84f, 92f);
            Image hit = box.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            Image bg = TradeUi.RoundPanel("bg", box.transform, 0f, 0f, 84f, 92f, 8,
                can ? (isSelected ? TradeUi.Panel : TradeUi.PanelAlt) : TradeUi.Panel);
            bg.raycastTarget = false;
            if (isSelected)
            {
                TradeUi.RoundPanel("sel", box.transform, 0f, 0f, 84f, 92f, 8,
                    new Color(1f, 0.82f, 0.42f, 0.25f));
            }

            Sprite sprite = AmmoSprite(a);
            if (sprite != null)
            {
                GameObject iconGo = TradeUi.Node("icon", box.transform, 0f, 0f, 52f, 52f);
                RectTransform irt = iconGo.GetComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(52f, 52f);
                irt.anchoredPosition = new Vector2(0f, 8f);
                Image icon = iconGo.AddComponent<Image>();
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.color = can && kg > 0f ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                icon.raycastTarget = false;
            }
            TextMeshProUGUI name = TradeUi.Text("name", box.transform, 0f, 0f, 80f, 14f, a.name, 9f,
                can ? (kg > 0f ? TradeUi.TextMain : TradeUi.TextDim) : TradeUi.TextOff, false);
            name.alignment = TextAlignmentOptions.Center;
            RectTransform nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0.5f, 0.5f);
            nrt.anchorMax = new Vector2(0.5f, 0.5f);
            nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.sizeDelta = new Vector2(80f, 14f);
            nrt.anchoredPosition = new Vector2(0f, -18f);

            float ratio = kg / Mathf.Max(1f, maxKg);
            Color tier = AmmoTierColor(ratio);
            Image barBg = TradeUi.RoundPanel("tgbg", box.transform, 0f, 0f, 64f, 6f, 3,
                new Color(0f, 0f, 0f, 0.55f));
            BarToCenter(barBg.rectTransform, 0f, -32f, 64f, 6f);
            float fw = Mathf.Clamp01(ratio) * 62f;
            if (fw >= 1f)
            {
                Image fill = TradeUi.RoundPanel("tgf", box.transform, 0f, 0f, fw, 4f, 2, tier);
                BarToCenter(fill.rectTransform, -31f + fw * 0.5f, -32f, fw, 4f);
            }
            if (reserved)
            {
                TextMeshProUGUI tag = TradeUi.Text("tag", box.transform, 0f, 0f, 80f, 14f, "同主弹", 8f,
                    TradeUi.Neg, false);
                tag.alignment = TextAlignmentOptions.Center;
                RectTransform trt = tag.rectTransform;
                trt.anchorMin = new Vector2(0.5f, 0.5f);
                trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(80f, 14f);
                trt.anchoredPosition = new Vector2(0f, -32f);
            }

            if (!can)
            {
                return;
            }
            Button button = box.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(delegate
            {
                RocketLoadout lo = RocketLoadout.Get(selected);
                if (lo != null)
                {
                    string prim = ammoPickerWeapon < lo.weaponPrimaryAmmoIds.Count
                        ? lo.weaponPrimaryAmmoIds[ammoPickerWeapon] : "auto";
                    string back = ammoPickerWeapon < lo.weaponBackupAmmoIds.Count
                        ? lo.weaponBackupAmmoIds[ammoPickerWeapon] : null;
                    if (ammoPickerPrimary)
                    {
                        lo.SetWeaponAmmo(ammoPickerWeapon, a.id, back);
                    }
                    else
                    {
                        lo.SetWeaponAmmo(ammoPickerWeapon, prim, a.id);
                    }
                }
                ammoPickerOpen = false;
                dirty = true;
            });
            AddTooltip(box, a.name + "\n余量 " + Mathf.Round(kg) + " kg"
                + "\n点击设为" + (ammoPickerPrimary ? "主弹" : "备弹"));
        }

        private static void BarToCenter(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }

        private static Sprite AmmoSprite(AmmoDef a)
        {
            if (a == null || string.IsNullOrEmpty(a.prefabId))
            {
                return null;
            }
            try
            {
                var t = Def.GetUISprite(new Tag(a.prefabId), "ui", false);
                if (t != null && t.first != null)
                {
                    return t.first;
                }
            }
            catch (Exception)
            {
                // expected: 弹药没有 ui 精灵时返回 null，由调用方用默认图标
            }
            return null;
        }

        // 问题14：等级排序权重（S 最高）。
        private static int TierRank(string tier)
        {
            switch (string.IsNullOrEmpty(tier) ? "" : tier.ToUpperInvariant())
            {
                case "S": return 7;
                case "A": return 6;
                case "B": return 5;
                case "C": return 4;
                case "D": return 3;
                case "E": return 2;
                case "F": return 1;
                default: return 0;
            }
        }

        // 问题14：等级颜色（S红/A橙/B紫/C蓝/D绿/E·F白）。
        private static Color TierColor(string tier)
        {
            switch (string.IsNullOrEmpty(tier) ? "" : tier.ToUpperInvariant())
            {
                case "S": return new Color(0.95f, 0.25f, 0.25f, 1f);
                case "A": return new Color(1.00f, 0.55f, 0.15f, 1f);
                case "B": return new Color(0.70f, 0.40f, 0.95f, 1f);
                case "C": return new Color(0.35f, 0.60f, 1.00f, 1f);
                case "D": return new Color(0.35f, 0.85f, 0.45f, 1f);
                default: return Color.white;
            }
        }
    }
}
