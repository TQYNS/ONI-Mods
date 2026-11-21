// 用途：独立加油窗：可用性判定、窗口装配与模态遮罩

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class RefuelScreen
    {
        // 配置存在 + 站点支持 + 火箭到位 + 有引擎/燃料仓，才允许打开加油窗
        internal static bool Ready(SpaceStation station, out string reason, Clustercraft specificCraft = null)
        {
            reason = null;
            if (!TradeUi.RefuelConfigured(station))
            {
                reason = "本站没有加油配置：请补齐 trade/fuel 下的燃料价目";
                return false;
            }
            // 火箭侧按钮传当前选中的火箭；站点侧不传，才按站点邻格找
            Clustercraft craft = specificCraft != null ? specificCraft : TradeUtil.CraftNear(station.Location);
            if (craft == null)
            {
                reason = "请把火箭开到本站或相邻一格";
                return false;
            }
            CraftModuleInterface mi = craft.ModuleInterface;
            if (mi == null)
            {
                reason = "火箭模块接口未就绪";
                return false;
            }
            RocketEngineCluster engine = mi.GetEngine();
            if (engine == null)
            {
                reason = "火箭没有引擎";
                return false;
            }
            if (mi.FuelCapacity <= 0.0001f && TradeExecutor.OxidizerCapacityOf(craft) <= 0.0001f)
            {
                reason = "火箭没有可用的燃料仓或氧化剂舱";
                return false;
            }
            return true;
        }

        internal static void Build(SpaceStation station, ref GameObject window, Clustercraft specificCraft = null,
            bool oxidizerMode = false, string oxidizerTag = null)
        {
            Transform parent = TradeUi.OverlayParent();
            if (parent == null)
            {
                MFLog.Warn(LogCat.Trade,"[Space] 找不到 overlay 父节点，加油窗口无法打开");
                return;
            }
            string reason;
            if (!Ready(station, out reason, specificCraft))
            {
                MFLog.Warn(LogCat.Trade,"[Space] 加油窗口打不开：" + reason);
                return;
            }
            Clustercraft craft = specificCraft != null ? specificCraft : TradeUtil.CraftNear(station.Location);
            CraftModuleInterface mi = craft.ModuleInterface;
            string fuelTag = mi.GetEngine().fuelTag.Name;
            List<Tag> oxTags = TradeExecutor.OxidizerTagsOf(craft);
            oxidizerMode = oxidizerMode && oxTags.Count > 0;
            Tag selectedOx = Tag.Invalid;
            if (oxidizerMode)
            {
                selectedOx = oxTags.Find(t => t.Name == oxidizerTag);
                if (!selectedOx.IsValid)
                {
                    selectedOx = oxTags[0];
                }
            }

            GameObject blocker = UiModalBlocker.Open(parent, 0.35f);
            window = TradeUi.Node("SpaceRefuelWindow", parent, 0f, 0f, 10f, 10f);
            RectTransform rt = window.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(700f, 450f);
            Image bg = TradeUi.RoundPanel("root", window.transform, 0f, 0f, 700f, 450f, 14, TradeUi.BgRoot);
            bg.raycastTarget = true;   // 窗口本体吸收点击，只有点窗口外才落到遮罩
            TradeUi.RoundPanel("headbg", window.transform, 0f, 0f, 700f, 56f, 12, TradeUi.Panel);
            TradeUi.HLine("headline", window.transform, 0f, 54f, 700f, TradeUi.Gold);
            Image headHit = TradeUi.Solid("headdrag", window.transform, 0f, 0f, 700f, 30f,
                new Color(0f, 0f, 0f, 0f));
            headHit.raycastTarget = true;
            EventPanelDrag drag = headHit.gameObject.AddComponent<EventPanelDrag>();
            drag.target = rt;

            RefuelScreenState st = window.AddComponent<RefuelScreenState>();
            st.root = window;
            st.station = station;
            st.def = station.Def;
            st.craft = craft;
            st.fuelTag = fuelTag;
            st.fuelName = TradeDb.FuelDisplayName(fuelTag);
            st.oxidizerMode = oxidizerMode;
            st.oxidizerTags = oxTags;
            st.oxidizerTag = selectedOx;
            st.oxStorages = oxidizerMode ? TradeExecutor.OxidizerStorages(craft) : null;
            if (oxidizerMode)
            {
                st.fuelName = TradeDb.OxidizerDisplayName(selectedOx.Name);
                st.price = TradeDb.OxidizerPricePerKg(selectedOx.Name);
                st.capacity = TradeExecutor.OxidizerCapacityOf(st.oxStorages);
            }
            else
            {
                st.price = TradeDb.FuelPricePerKg(fuelTag);
                st.capacity = mi.FuelCapacity;
            }
            TradeUi.Text("title", window.transform, 26f, 12f, 360f, 36f,
                station.StationName + (oxidizerMode ? "  ·  补充氧化剂" : "  ·  加油"), 21f, TradeUi.TextMain, false);
            st.wallet = TradeUi.Text("wallet", window.transform, 396f, 12f, 240f, 36f,
                "星币 " + TradeRegistry.Currency.ToString("0.00"), 18f, TradeUi.Gold, true);
            TradeUi.MakeCloseButton(window.transform, 652f, 10f, TradeUi.CloseRefuel);
            if (oxTags.Count == 0)
            {
                TradeUi.Text("status", window.transform, 24f, 66f, 652f, 26f,
                    "拖动滑条选择目标油量；金色是已有油量，青色是本次加注。点窗口外或再点一次「加油」关闭。",
                    13f, TradeUi.TextDim, false);
            }

            RefuelModalCloser closer = blocker.AddComponent<RefuelModalCloser>();
            closer.state = st;

            GameObject left = TradeUi.Node("left", window.transform, 24f, 102f, 400f, 232f).gameObject;
            TradeUi.RoundPanel("bg", left.transform, 0f, 0f, 400f, 232f, 12, TradeUi.PanelAlt);
            BuildLeft(left.transform, st);
            if (oxTags.Count > 0)
            {
                BuildModeBar(window.transform, station, craft, st, oxTags, oxidizerMode);
            }
            GameObject right = TradeUi.Node("right", window.transform, 436f, 102f, 240f, 232f).gameObject;
            TradeUi.RoundPanel("bg", right.transform, 0f, 0f, 240f, 232f, 12, TradeUi.Panel);
            BuildRight(right.transform, st);

            TradeUi.Text("hint", window.transform, 24f, 344f, 652f, 24f,
                oxidizerMode
                    ? "一键补充 = 用当前星币买到最多氧化剂，并自动把滑条推到对应位置"
                    : "一键补充 = 用当前星币买到最多油量，并自动把滑条推到对应位置", 13f, TradeUi.TextOff, false);
            TradeUi.MakeButton("一键补充", window.transform, 24f, 376f, 180f, 48f,
                TradeUi.TabOn, TradeUi.Accent, TradeUi.Accent, st.FillAll);
            TradeUi.MakeButton("重 置", window.transform, 216f, 376f, 120f, 48f,
                TradeUi.PanelAlt, TradeUi.Border, TradeUi.TextMain, st.ResetTarget);
            st.confirmBtn = TradeUi.MakeButton("确认加油", window.transform, 452f, 376f, 224f, 48f,
                TradeUi.TabOn, TradeUi.Gold, TradeUi.Gold, st.Confirm);
            st.Init();
            MFLog.Info(LogCat.Trade,"[Space] 打开" + (oxidizerMode ? "氧化剂" : "加油") + "窗口：站点="
                + station.stationId + " 品类=" + st.fuelName + " 单价=" + st.price.ToString("0.00")
                + " 容量=" + st.capacity.ToString("0") + "千克");
        }

        // 燃料/氧化剂切换 + 氧化剂种类选择：每次点击重建窗口，复用同一套滑条与结算
        private static void BuildModeBar(Transform root, SpaceStation station, Clustercraft craft,
            RefuelScreenState st, List<Tag> oxTags, bool oxidizerMode)
        {
            TradeUi.MakeButton("燃料", root, 24f, 66f, 78f, 26f,
                oxidizerMode ? TradeUi.PanelAlt : TradeUi.TabOn,
                oxidizerMode ? TradeUi.Border : TradeUi.Accent,
                oxidizerMode ? TradeUi.TextDim : TradeUi.Accent,
                delegate { if (oxidizerMode) { TradeUi.ToggleRefuel(station, craft, false, null); } });
            TradeUi.MakeButton("氧化剂", root, 108f, 66f, 78f, 26f,
                oxidizerMode ? TradeUi.TabOn : TradeUi.PanelAlt,
                oxidizerMode ? TradeUi.Accent : TradeUi.Border,
                oxidizerMode ? TradeUi.Accent : TradeUi.TextDim,
                delegate { if (!oxidizerMode) { TradeUi.ToggleRefuel(station, craft, true, oxTags[0].Name); } });   // 已在该页不重复重建
        }

        // 氧化剂种类：点左侧图标弹列表选择（换"东西"点图标，比顶部排一排按钮更符合习惯）
        private static void BuildOxPicker(Transform left, RefuelScreenState st)
        {
            Transform old = left.Find("oxpick");
            if (old != null)
            {
                UnityEngine.Object.Destroy(old.gameObject);   // 再点一次收起
                return;
            }
            if (st.oxidizerTags == null || st.oxidizerTags.Count == 0)
            {
                return;
            }
            float h = st.oxidizerTags.Count * 30f + 8f;
            GameObject box = TradeUi.Node("oxpick", left, 90f, 14f, 180f, h);
            TradeUi.RoundPanel("bg", box.transform, 0f, 0f, 180f, h, 8, TradeUi.PanelAlt).raycastTarget = true;
            for (int i = 0; i < st.oxidizerTags.Count; i++)
            {
                Tag tag = st.oxidizerTags[i];
                bool on = tag == st.oxidizerTag;
                TradeUi.MakeButton(TradeDb.OxidizerDisplayName(tag.Name), box.transform, 4f, 4f + i * 30f, 172f, 28f,
                    on ? TradeUi.TabOn : TradeUi.Panel, on ? TradeUi.Accent : TradeUi.Border,
                    on ? TradeUi.Accent : TradeUi.TextMain,
                    delegate { if (!on) { TradeUi.ToggleRefuel(st.station, st.craft, true, tag.Name); } });
            }
        }

        private static void BuildLeft(Transform left, RefuelScreenState st)
        {
            Image iconBg = TradeUi.RoundPanel("iconbg", left, 18f, 18f, 64f, 64f, 32, TradeUi.Panel);
            if (st.oxidizerMode && st.oxidizerTags != null && st.oxidizerTags.Count > 1)
            {
                iconBg.raycastTarget = true;   // 点图标 → 弹氧化剂种类列表
                Button pick = iconBg.gameObject.AddComponent<Button>();
                pick.targetGraphic = iconBg;
                pick.onClick.AddListener(delegate { BuildOxPicker(left, st); });
            }
            Color iconTint;
            string iconTag = st.oxidizerMode && st.oxidizerTag.IsValid ? st.oxidizerTag.Name : st.fuelTag;
            Sprite icon = st.oxidizerMode ? TradeUi.ItemSprite(iconTag, out iconTint)
                : TradeUi.ItemSprite("__fuel", out iconTint);
            if (icon == null && !string.IsNullOrEmpty(iconTag))
            {
                icon = TradeUi.ItemSprite(iconTag, out iconTint);   // 兜底：真元素的 ui 贴图
            }
            if (icon == null && st.oxidizerMode)
            {
                icon = TradeUi.ItemSprite("OxyRock", out iconTint);   // 氧化剂贴图兜底：统一复用氧石图标
            }
            if (icon != null)
            {
                Image ic = TradeUi.Solid("icon", left, 0f, 0f, 60f, 60f, Color.white);
                ic.sprite = icon;
                ic.color = iconTint;
                ic.preserveAspect = true;
                TradeUi.CenterIcon(ic, 50f, 50f, 60f);   // 圆形底 (18,18,64,64) 的圆心 = (50,50)
            }
            TradeUi.Text("fuel", left, 96f, 20f, 286f, 34f, st.fuelName, 21f, TradeUi.Gold, false);
            TradeUi.Text("sub", left, 96f, 56f, 286f, 24f,
                st.oxidizerMode ? "本站不限量供应 · 按实际补入量结算" : "本站不限量供应 · 按实际加注量结算",
                13f, TradeUi.TextDim, false);

            TradeUi.Text("gaugeLabel", left, 18f, 90f, 364f, 22f, st.oxidizerMode ? "氧化剂表" : "油量表", 13f, TradeUi.TextDim, false);
            TradeUi.RoundPanel("track", left, 18f, 114f, 364f, 28f, 6, new Color(0f, 0f, 0f, 0.55f));
            st.haveImg = TradeUi.RoundPanel("have", left, 20f, 116f, 1f, 24f, 5, TradeUi.Gold);
            st.addImg = TradeUi.RoundPanel("add", left, 20f, 116f, 1f, 24f, 5, TradeUi.Accent);
            for (int i = 1; i <= 3; i++)
            {
                TradeUi.Solid("tick" + i, left, 20f + 360f * (i / 4f), 116f, 1f, 24f,
                    new Color(1f, 1f, 1f, 0.12f));
            }
            st.markerImg = TradeUi.Solid("target", left, 20f, 114f, 2f, 28f,
                new Color(1f, 1f, 1f, 0.85f));
            GameObject gaugeHit = TradeUi.Node("gaugehit", left, 18f, 114f, 364f, 28f);
            Image gaugeHitImg = gaugeHit.AddComponent<Image>();
            gaugeHitImg.color = new Color(0f, 0f, 0f, 0f);
            gaugeHitImg.raycastTarget = true;
            st.gaugeSlider = BindSlider(gaugeHit, null, null, null, st);

            TradeUi.Solid("lg1", left, 18f, 153f, 10f, 10f, TradeUi.Gold);
            TradeUi.Text("l1", left, 32f, 148f, 96f, 22f, st.oxidizerMode ? "已有量" : "已有油量", 12f, TradeUi.TextDim, false);
            TradeUi.Solid("lg2", left, 132f, 153f, 10f, 10f, TradeUi.Accent);
            TradeUi.Text("l2", left, 146f, 148f, 96f, 22f, st.oxidizerMode ? "本次补入" : "本次加注", 12f, TradeUi.TextDim, false);

            TradeUi.Text("sliderLabel", left, 18f, 180f, 96f, 28f, st.oxidizerMode ? "目标量" : "目标油量", 14f, TradeUi.TextDim, false);
            GameObject hit = TradeUi.Node("slider", left, 116f, 180f, 220f, 28f);
            Image hitImg = hit.AddComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;
            TradeUi.RoundPanel("track", hit.transform, 0f, 11f, 220f, 6f, 3, TradeUi.Line);
            Image fill = TradeUi.RoundPanel("fill", hit.transform, 0f, 11f, 1f, 6f, 3, TradeUi.Accent);
            Image knob = TradeUi.RoundPanel("knob", hit.transform, 0f, 6f, 14f, 14f, 5, TradeUi.TextMain);
            st.readout = TradeUi.Text("readout", left, 340f, 180f, 52f, 28f, "0%", 15f,
                TradeUi.Accent, true);
            st.slider = BindSlider(hit, fill, knob, st.readout, st);
            TradeUi.Text("sliderHint", left, 18f, 212f, 364f, 20f,
                st.oxidizerMode ? "拖动滑条或直接点剂量条，选择补充后的目标百分比" : "拖动滑条或直接点油量条，选择加油后的目标百分比", 12f, TradeUi.TextOff, false);
        }

        // 油量条与滑条共用现成的 AdvancedSlider（0~100%，步长 1%），只换命中区与外观
        private static AdvancedSlider BindSlider(GameObject hitGo, Image fill, Image knob,
            TextMeshProUGUI readout, RefuelScreenState st)
        {
            AdvancedSlider s = hitGo.AddComponent<AdvancedSlider>();
            s.hit = hitGo.GetComponent<RectTransform>();
            s.fill = fill != null ? fill.rectTransform : null;
            s.knob = knob != null ? knob.rectTransform : null;
            s.readout = readout;
            s.min = 0f;
            s.max = 100f;
            s.step = 1f;
            s.decimals = 0;
            s.suffix = "%";
            s.getter = st.TargetPct;
            s.onValue = st.SetTarget;
            s.Refresh();
            return s;
        }

        private static void BuildRight(Transform right, RefuelScreenState st)
        {
            TradeUi.Text("head", right, 16f, 12f, 208f, 26f, "预期结算", 16f, TradeUi.Accent, false);
            TradeUi.HLine("line1", right, 16f, 42f, 208f, TradeUi.Line);
            st.unitText = Line(right, 52f, st.oxidizerMode ? "氧化剂单价" : "燃料单价", "", TradeUi.TextMain, 14f);
            st.addText = Line(right, 82f, st.oxidizerMode ? "补入量" : "加注量", "0 千克", TradeUi.TextMain, 14f);
            st.costText = Line(right, 112f, "本次花费", "0.00", TradeUi.Neg, 14f);
            TradeUi.HLine("line2", right, 16f, 142f, 208f, TradeUi.Line);
            st.walletSideText = Line(right, 152f, "星币余额", "0.00", TradeUi.Gold, 14f);
            st.keepText = Line(right, 182f, "预期结余", "0.00", TradeUi.TextMain, 16f);
            TradeUi.Text("foot", right, 16f, 210f, 208f, 20f,
                "结算扣款与交易一致", 11f, TradeUi.TextOff, false);
        }

        private static TextMeshProUGUI Line(Transform parent, float y, string label, string value,
            Color valueColor, float size)
        {
            TradeUi.Text("l_" + label, parent, 16f, y, 110f, 24f, label, 13f, TradeUi.TextDim, false);
            return TradeUi.Text("v_" + label, parent, 126f, y, 98f, 24f, value, size, valueColor, true);
        }
    }

    // 遮罩点击 → 先关内部弹窗，再关加油窗（与交易窗同款）
    internal sealed class RefuelModalCloser : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        internal RefuelScreenState state;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (state != null)
            {
                state.HandleBlockerClick();
            }
        }
    }
}
