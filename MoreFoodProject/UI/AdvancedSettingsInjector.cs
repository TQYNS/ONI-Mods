// 用途：在事件框架叙事者选择面板「继续」左侧注入 Morefood 专属「高级设置」按钮，并维护本局设置的重置

using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class AdvancedSettingsInjector
    {
        private const string TAG = "[MoreFood/Advanced]";

        private static bool _installed;

        internal static void Install(Harmony harmony)
        {
            if (_installed || harmony == null) return;
            _installed = true;

            try
            {
                var panelType = AccessTools.TypeByName("EventFramework.EventNarratorPanel");
                if (panelType != null)
                {
                    var show = AccessTools.Method(panelType, "Show");
                    var post = AccessTools.Method(typeof(AdvancedSettingsInjector), nameof(AfterPanelShow));
                    if (show != null && post != null)
                    {
                        harmony.Patch(show, postfix: new HarmonyMethod(post));
                        MFLog.Info(LogCat.Patch, TAG + " 已挂：EventNarratorPanel.Show");
                    }
                    else
                    {
                        MFLog.Info(LogCat.Patch, TAG + " 事件框架面板接口缺失，跳过按钮注入");
                    }
                }

                var flowType = AccessTools.TypeByName("NewGameFlow");
                if (flowType != null)
                {
                    var begin = AccessTools.Method(flowType, "BeginFlow");
                    var post = AccessTools.Method(typeof(AdvancedSettingsInjector), nameof(AfterBeginFlow));
                    if (begin != null && post != null)
                    {
                        harmony.Patch(begin, postfix: new HarmonyMethod(post));
                        MFLog.Info(LogCat.Patch, TAG + " 已挂：NewGameFlow.BeginFlow（重置本局设置）");
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, TAG + " 补丁安装失败（已放弃，不影响本体）");
            }
        }

        // 新一轮开局清回默认值；此时玩家尚未进入选择面板，不会清掉刚设的值
        public static void AfterBeginFlow()
        {
            try { AdvancedGameplaySettings.Reset(); }
            catch (Exception e) { MFLog.Exc(LogCat.General, e, TAG + " 重置本局设置失败"); }
        }

        // 面板每次显示完都检查一遍；已注入则跳过，重开面板会重建根节点故可再次注入
        public static void AfterPanelShow(Transform host)
        {
            try { Inject(host); }
            catch (Exception e) { MFLog.Exc(LogCat.UI, e, TAG + " 注入「高级设置」按钮失败"); }
        }

        private static void Inject(Transform host)
        {
            if (host == null) return;

            Transform root = host.Find("NarratorPanel");
            if (root == null) return;

            Transform cont = FindDeep(root, "BtnContinue");
            if (cont == null) return;

            Transform win = cont.parent;
            if (win == null || win.Find("MoreFood_AdvancedBtn") != null) return;

            var crt = cont as RectTransform;
            float y = crt != null ? -crt.anchoredPosition.y : 627f;
            float x = crt != null ? crt.anchoredPosition.x : 972f;
            const float w = 130f;
            const float h = 50f;
            x -= w + 8f;

            GameObject go = EventUiKit.MakeNode("MoreFood_AdvancedBtn", win);
            EventUiKit.Place(go, x, y, w, h);

            Image bg = go.AddComponent<Image>();
            bg.sprite = UiSkin.Rounded(11);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.196f, 0.310f, 0.400f, 1f);
            bg.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = new Color(0.92f, 0.96f, 1f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.92f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(delegate { AdvancedSettingsPanel.Open(root); });

            AdvancedSettingsPanel.PutText("Label", go.transform,
                "高级设置", 19f, new Color(0.937f, 0.945f, 0.960f, 1f),
                TextAlignmentOptions.Center, 0f, 0f, w, h);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = FindDeep(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }
    }
}
