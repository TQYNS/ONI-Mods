// 用途：装配页：槽位盒、原版模板与模块图标

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal sealed partial class RocketPanelScreen : KScreen
    {
        // 装配：仿生人升级槽式

        private void BuildAssemblyPage(RocketCombatState st, ref float y)
        {
            RocketLoadout lo = RocketLoadout.Get(selected);
            if (lo == null) return;
            string[] types = { "weapon", "defense", "utility", "sensor" };
            string[] names = { "武器", "防御", "辅助", "感知" };
            float slotH = SlotSize().y;
            float advance = Mathf.Min(slotH + 12f, Mathf.Max(40f, (648f - y) / 4f));
            for (int t = 0; t < types.Length; t++)
            {
                string type = types[t];
                SlotEntry slot = lo.GetSlots(selected, type);
                int used, cost;
                lo.GetSlotUsed(type, out used, out cost);
                TradeUi.Text("ah" + type, rightRoot.transform, 0f, y, 860f, 22f,
                    names[t] + "  " + used + "/" + slot.count + "   容量 " + cost + "/" + slot.capacity,
                    15f, (used > slot.count || cost > slot.capacity) ? TradeUi.Neg : TradeUi.Gold, false);
                y += 20f;

                if (slot.count <= 0)
                {
                    TradeUi.Text("cap" + type, rightRoot.transform, 12f, y, 820f, 20f,
                        slot.capacity > 0 ? "仅提供容量 " + slot.capacity + "，不提供挂点" : "未提供战斗槽位",
                        12f, TradeUi.TextOff, false);
                    y += 26f;
                    continue;
                }

                List<ModuleDef> equipped = EquippedOfType(lo, type);
                int boxes = Mathf.Max(slot.count, equipped.Count);
                float pitch = SlotSize().x + 12f;
                for (int s = 0; s < boxes; s++)
                {
                    ModuleDef def = s < equipped.Count ? equipped[s] : null;
                    BuildSlotBox(def, type, s * pitch, y);
                }
                y += advance;
            }
            // ★ 问题8：交战时模块下方显示红色"交战锁定"提示文本
            if (st != null && st.inCombat)
            {
                TradeUi.Text("combatlock", rightRoot.transform, 0f, y, 860f, 22f,
                    "交战锁定：交战中无法更换模块", 14f, TradeUi.Neg, false);
                y += 26f;
            }
        }

        private List<ModuleDef> EquippedOfType(RocketLoadout lo, string type)
        {
            List<ModuleDef> list = new List<ModuleDef>();
            for (int i = 0; i < lo.equippedModules.Count; i++)
            {
                ModuleDef d = CombatDb.FindModule(lo.equippedModules[i]);
                if (d != null && d.slotType == type)
                {
                    list.Add(d);
                }
            }
            return list;
        }

        private void BuildSlotBox(ModuleDef def, string type, float x, float y)
        {
            BionicSideScreenUpgradeSlot template = SlotTemplate();
            if (template != null)
            {
                try
                {
                    BuildVanillaSlotBox(template, def, type, x, y);
                    return;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.UI, e, "[Space] 原版升级槽克隆失败，退回程序绘制");
                }
            }
            BuildDrawnSlotBox(def, type, x, y);
        }

        private void BuildVanillaSlotBox(BionicSideScreenUpgradeSlot template, ModuleDef def,
            string type, float x, float y)
        {
            Vector2 size = SlotSize();
            GameObject box = UnityEngine.Object.Instantiate(template.gameObject);
            box.name = "SpaceSlot";
            RectTransform rt = box.GetComponent<RectTransform>();
            rt.SetParent(rightRoot.transform, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(x, -y);
            box.SetActive(true);

            BionicSideScreenUpgradeSlot slot = box.GetComponent<BionicSideScreenUpgradeSlot>();
            if (slot == null)
            {
                UnityEngine.Object.Destroy(box);
                BuildDrawnSlotBox(def, type, x, y);
                return;
            }

            if (def == null && slot.toggle != null && slot.toggle.states != null
                && slot.toggle.states.Length > 2)
            {
                slot.toggle.ChangeState(2);
            }

            Image frameImage = null;
            Image[] images = box.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null)
                {
                    continue;
                }
                if (img.name == "border")
                {
                    frameImage = img;
                    if (def == null)
                    {
                        img.enabled = true;
                        continue;
                    }
                }
                if (img.name == "highlight")
                {
                    img.enabled = true;
                    continue;
                }
                img.enabled = false;
            }
            if (slot.icon != null)
            {
                slot.icon.enabled = false;
            }
            Image rootImage = box.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.enabled = false;
            }
            if (slot.label != null)
            {
                slot.label.SetText("");
                slot.label.enabled = false;
            }

            bool loaded = def == null || ModuleLoaded(def.id);
            RectTransform frameRect = frameImage != null ? frameImage.rectTransform : null;
            if (def == null)
            {
                ForceVanillaSlotArt(box);
            }
            if (def != null)
            {
                Sprite sprite = ModuleSprite(def);
                if (sprite != null)
                {
                    Image icon = MakeOverlay(rt, frameRect, "spaceicon", sprite, Color.white);
                    icon.preserveAspect = true;
                }
                if (!loaded)
                {
                    Image veil = MakeOverlay(rt, frameRect, "grey", null,
                        new Color(0.38f, 0.42f, 0.5f, 0.62f));
                    veil.transform.SetAsLastSibling();
                }
            }

            ModuleDef captured = def;
            string capturedType = type;
            if (slot.toggle != null)
            {
                slot.toggle.onClick += delegate
                {
                    if (captured != null)
                    {
                        CombatAssembly.Unequip(selected, captured.id);
                    }
                    else
                    {
                        pickerOpen = true;
                        pickerType = capturedType;
                    }
                    dirty = true;
                };
            }
            if (slot.tooltip != null)
            {
                try
                {
                    string tip;
                    if (def == null)
                    {
                        tip = "空 " + SlotLabel(type) + " 槽\n点击选择模块";
                    }
                    else if (loaded)
                    {
                        tip = ModuleTooltip(def) + "\n点击卸下";
                    }
                    else
                    {
                        tip = ModuleTooltip(def)
                            + "\n<color=#E86A6A>未生效：控制台里没有这件模块实物</color>\n点击卸下重装";
                    }
                    slot.tooltip.SetSimpleTooltip(tip);
                }
                catch (Exception)
                {
                    // expected: tooltip 设置失败不影响槽位本身显示
                }
            }
        }

        private static Image MakeOverlay(RectTransform box, RectTransform reference, string name,
            Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            if (reference != null)
            {
                rt.SetParent(reference.parent, false);
                rt.anchorMin = reference.anchorMin;
                rt.anchorMax = reference.anchorMax;
                rt.pivot = reference.pivot;
                rt.sizeDelta = reference.sizeDelta;
                rt.anchoredPosition = reference.anchoredPosition;
                rt.SetSiblingIndex(reference.GetSiblingIndex() + 1);
            }
            else
            {
                rt.SetParent(box, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = box.sizeDelta;
            }
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private bool ModuleLoaded(string moduleId)
        {
            ControlStationModuleReceiver receiver = CombatAssembly.FindReceiver(selected);
            return receiver != null && receiver.HasModuleItem(moduleId);
        }

        private void BuildDrawnSlotBox(ModuleDef def, string type, float x, float y)
        {
            Vector2 size = SlotSize();
            float w = size.x;
            float h = size.y;
            GameObject box = TradeUi.Node(def != null ? def.id : "slot_" + type, rightRoot.transform,
                x, y, w, h + 20f);
            Image hit = box.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            Image tintTarget = null;
            Color tintNormal = Color.white;
            Color tintHover = TradeUi.Gold;
            if (def != null)
            {
                Sprite sprite = ModuleSprite(def);
                if (sprite != null)
                {
                    GameObject iconGo = TradeUi.Node("icon", box.transform, 4f, 2f, w - 8f, h - 6f);
                    Image icon = iconGo.AddComponent<Image>();
                    icon.sprite = sprite;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    tintTarget = icon;
                    tintNormal = Color.white;
                    tintHover = new Color(1f, 1f, 1f, 0.8f);
                }
                TextMeshProUGUI label = TradeUi.Text("name", box.transform, 0f, h + 1f, w, 18f,
                    def.name, 10f, RarityColor(def.rarity), false);
                label.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                Image frame = SlotFrame(box.transform, w, h);
                tintNormal = new Color(1f, 1f, 1f, 0.45f);
                tintHover = TradeUi.Gold;
                frame.color = tintNormal;
                tintTarget = frame;
            }
            if (tintTarget != null)
            {
                TradeUi.HoverTint tint = box.AddComponent<TradeUi.HoverTint>();
                tint.target = tintTarget;
                tint.normal = tintNormal;
                tint.hover = tintHover;
                tint.press = tintHover;
            }

            Button button = box.AddComponent<Button>();
            button.targetGraphic = hit;
            ModuleDef captured = def;
            string capturedType = type;
            button.onClick.AddListener(delegate
            {
                if (captured != null)
                {
                    CombatAssembly.Unequip(selected, captured.id);
                }
                else
                {
                    pickerOpen = true;
                    pickerType = capturedType;
                }
                dirty = true;
            });
            AddTooltip(box, def != null
                ? ModuleTooltip(def) + "\n点击卸下"
                : "空 " + SlotLabel(type) + " 槽\n点击选择模块");
        }

        private static BionicSideScreenUpgradeSlot slotTemplate;
        private static bool slotTemplateProbed;

        private static BionicSideScreenUpgradeSlot SlotTemplate()
        {
            if (slotTemplateProbed)
            {
                return slotTemplate;
            }
            slotTemplateProbed = true;
            try
            {
                BionicSideScreenUpgradeSlot[] all =
                    Resources.FindObjectsOfTypeAll<BionicSideScreenUpgradeSlot>();
                for (int i = 0; i < all.Length; i++)
                {
                    BionicSideScreenUpgradeSlot s = all[i];
                    if (s == null || s.gameObject == null || s.toggle == null || s.icon == null)
                    {
                        continue;
                    }
                    if (s.gameObject.name.StartsWith("SpaceSlot"))
                    {
                        continue;
                    }
                    slotTemplate = s;
                    MFLog.Info(LogCat.Space, "[Space] 找到原版仿生人升级槽 prefab：" + s.gameObject.name);
                    break;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 查找原版升级槽 prefab 失败");
            }
            if (slotTemplate == null)
            {
                Debug.LogWarning("[Space] 没有原版升级槽 prefab，装配槽退回程序绘制");
            }
            return slotTemplate;
        }

        private static Vector2 SlotSize()
        {
            BionicSideScreenUpgradeSlot t = SlotTemplate();
            if (t != null)
            {
                RectTransform rt = t.transform as RectTransform;
                if (rt != null)
                {
                    Vector2 s = rt.sizeDelta;
                    if (s.x < 24f || s.y < 24f)
                    {
                        s = rt.rect.size;
                    }
                    if (s.x >= 24f && s.y >= 24f)
                    {
                        return s;
                    }
                }
            }
            ProbeSlotArt();
            if (slotFrameSprite != null)
            {
                float ppu = Mathf.Max(0.01f, slotFrameSprite.pixelsPerUnit);
                Vector2 n = new Vector2(slotFrameSprite.rect.width / ppu,
                    slotFrameSprite.rect.height / ppu);
                if (n.x >= 24f && n.y >= 24f && n.x <= 160f && n.y <= 160f)
                {
                    return n;
                }
            }
            return new Vector2(72f, 84f);
        }

        private static Image SlotFrame(Transform parent, float w, float h)
        {
            GameObject go = TradeUi.Node("frame", parent, 0f, 0f, w, h);
            Image img = go.AddComponent<Image>();
            img.sprite = DashFrame((int)w, (int)h);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        private static Sprite slotFrameSprite;
        private static Sprite slotBgSprite;
        private static bool slotArtProbed;

        private static void ProbeSlotArt()
        {
            if (slotArtProbed)
            {
                return;
            }
            slotArtProbed = true;
            try
            {
                slotFrameSprite = Assets.GetSprite("bionicUpgradeSlot");
                slotBgSprite = Assets.GetSprite("bionicUpgradeSlotBG");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 取原版升级槽贴图失败");
            }
            if (slotFrameSprite == null)
            {
                Debug.LogWarning("[Space] 没取到 bionicUpgradeSlot 贴图，装配槽用程序虚线");
            }
            else
            {
                MFLog.Info(LogCat.Space, "[Space] 已取到原版升级槽贴图 bionicUpgradeSlot");
            }
        }

        private static void ForceVanillaSlotArt(GameObject box)
        {
            ProbeSlotArt();
            Image[] images = box.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image img = images[i];
                if (img == null)
                {
                    continue;
                }
                if (img.name == "border" && img.sprite == null && slotFrameSprite != null)
                {
                    img.sprite = slotFrameSprite;
                }
                else if (img.name == "base" && img.sprite == null && slotBgSprite != null)
                {
                    img.sprite = slotBgSprite;
                }
            }
        }
    }
}
