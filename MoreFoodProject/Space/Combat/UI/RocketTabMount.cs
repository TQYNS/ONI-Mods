using System;
using EventFramework.Distribution.Contracts;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    // 在管理菜单「事件」右侧挂「火箭」tab；延迟一帧，确保 MoreFood 已挂完它的 tab。
    [HarmonyPatch(typeof(ManagementMenu), "OnPrefabInit")]
    internal static class RocketTabMountPatch
    {
        private static int mountedMenuId;

        private static void Postfix(ManagementMenu __instance)
        {
            try
            {
                ManagementMenu menu = __instance;
                if (menu == null)
                {
                    return;
                }
                if (mountedMenuId == menu.gameObject.GetInstanceID())
                {
                    return;
                }
                // 同一帧内先挂「饮品」（框架已不再代管），再挂「火箭」——顺序稳定为
                // 饮食 → 饮品 → 事件 → 火箭
                UIScheduler.Instance.ScheduleNextFrame("SpaceRocketTab", delegate(object o)
                {
                    MountDrinkTab(menu);
                    Mount(menu);
                }, null);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 火箭页签挂载失败");
            }
        }

        // 饮品页签：C09 之后由宿主自挂（框架不再代劳）
        private static void MountDrinkTab(ManagementMenu menu)
        {
            try
            {
                if (menu == null)
                {
                    return;
                }
                KScreen screen = DrinkTableScreen.GetOrCreate();
                if (screen == null)
                {
                    Debug.LogWarning("[Space] 饮品屏未就绪，本次跳过饮品页签");
                    return;
                }
                var spec = new MenuTabRegistry.TabSpec
                {
                    text = "饮品",
                    icon = "OverviewUI_consumables_icon",
                    tooltip = "饮品管制",
                    screen = screen,
                };
                int n = MenuTabRegistry.AddTabs(menu, new MenuTabRegistry.TabSpec[] { spec });
                if (n > 0)
                {
                    MFLog.Info(LogCat.Space, "[Space] 已挂载「饮品」页签");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 饮品页签挂载异常");
            }
        }

        private static void Mount(ManagementMenu menu)
        {
            try
            {
                if (menu == null)
                {
                    return;
                }
                RocketPanelScreen screen = RocketPanelScreen.GetOrCreate();
                if (screen == null)
                {
                    return;
                }
                MenuTabRegistry.TabSpec spec = new MenuTabRegistry.TabSpec();
                spec.text = "火箭";
                spec.icon = "OverviewUI_starmap_icon";
                spec.tooltip = "火箭总览";
                spec.screen = screen;
                if (Assets.GetSprite(spec.icon) == null)
                {
                    Debug.LogWarning("[Space] 火箭页签图标 sprite 缺失：" + spec.icon + "（会显示成白块）");
                }
                int n = MenuTabRegistry.AddTabs(menu,
                    new MenuTabRegistry.TabSpec[] { spec }, "事件");
                if (n > 0)
                {
                    mountedMenuId = menu.gameObject.GetInstanceID();
                    MFLog.Info(LogCat.Space, "[Space] 已挂载「火箭」页签（事件右侧）");
                }
                FixMissingTabIcons(menu, "火箭");
                ManagementMenuGlowFix.Repair(menu, true);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 火箭页签挂载异常");
            }
        }

        private static void FixMissingTabIcons(ManagementMenu menu, string ourText)
        {
            try
            {
                Transform root = menu.transform;
                Sprite fallback = FindIconSprite(root, "研究");
                if (fallback == null)
                {
                    fallback = FindIconSprite(root, "事件");
                }
                if (fallback == null)
                {
                    Debug.LogWarning("[Space] 找不到可借用的页签图标，跳过白块修复");
                    return;
                }
                int fixedCount = 0;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || !t.name.StartsWith("Toggle:"))
                    {
                        continue;
                    }
                    Transform fg = t.Find("FG");
                    if (fg == null)
                    {
                        continue;
                    }
                    Image img = fg.GetComponent<Image>();
                    if (img != null && img.sprite == null)
                    {
                        img.sprite = fallback;
                        img.color = Color.white;
                        fixedCount++;
                        MFLog.Info(LogCat.Space, "[Space] 页签白块修复：" + t.name + " → 借用图标");
                    }
                }
                if (fixedCount > 0)
                {
                    MFLog.Info(LogCat.Space, "[Space] 页签白块修复完成，共 " + fixedCount + " 个");
                }
                ApplyIcon(root, ourText, fallback);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 页签图标修复异常");
            }
        }

        private static void ApplyIcon(Transform root, string text, Sprite sprite)
        {
            Transform toggle = FindToggle(root, text);
            if (toggle == null)
            {
                return;
            }
            Transform fg = toggle.Find("FG");
            Image img = fg != null ? fg.GetComponent<Image>() : null;
            if (img != null && img.sprite == null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
        }

        private static Sprite FindIconSprite(Transform root, string text)
        {
            Transform toggle = FindToggle(root, text);
            if (toggle == null)
            {
                return null;
            }
            Transform fg = toggle.Find("FG");
            Image img = fg != null ? fg.GetComponent<Image>() : null;
            return img != null ? img.sprite : null;
        }

        private static Transform FindToggle(Transform root, string text)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "Toggle:" + text)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
