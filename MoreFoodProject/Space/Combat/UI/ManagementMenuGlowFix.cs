using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class ManagementMenuGlowFix
    {
        private static bool running;
        private static MethodInfo notifyMethod;
        private static bool installed;

        internal static void Install(Harmony harmony)
        {
            if (installed || harmony == null)
            {
                return;
            }
            installed = true;
            try
            {
                MethodInfo target = AccessTools.Method(typeof(ManagementMenu), "RefreshButtons")
                    ?? AccessTools.Method(typeof(KIconToggleMenu), "RefreshButtons")
                    ?? AccessTools.Method(typeof(ManagementMenu), "OnNotificationsChanged")
                    ?? AccessTools.Method(typeof(ManagementMenu), "Refresh");
                if (target == null)
                {
                    Debug.LogWarning("[Space] 找不到 ManagementMenu.RefreshButtons，页签指示器兜底补丁跳过");
                    return;
                }
                HarmonyMethod post = new HarmonyMethod(typeof(ManagementMenuGlowFix).GetMethod(
                    "Postfix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(target, postfix: post);
                MFLog.Info(LogCat.Space, "[Space] 页签指示器兜底补丁已挂：" + target.DeclaringType.Name + "." + target.Name);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 页签指示器兜底补丁挂载失败");
            }
        }

        private static void Postfix(ManagementMenu __instance)
        {
            if (running)
            {
                return;
            }
            running = true;
            try
            {
                Repair(__instance, false);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 页签指示器修复异常");
            }
            finally
            {
                running = false;
            }
        }

        internal static void Repair(ManagementMenu menu, bool clearAlertFirst)
        {
            if (menu == null)
            {
                return;
            }
            if (clearAlertFirst)
            {
                ClearAllIndicators(menu);
            }
            CallNotifications(menu);
            EnforceGlow(menu);
        }

        internal static void ClearAllIndicators(ManagementMenu menu)
        {
            if (menu == null)
            {
                return;
            }
            try
            {
                KToggle[] toggles = menu.GetComponentsInChildren<KToggle>(true);
                int count = 0;
                for (int i = 0; i < toggles.Length; i++)
                {
                    KToggle kt = toggles[i];
                    if (kt == null)
                    {
                        continue;
                    }
                    Transform alert = kt.transform.Find("AlertIcon");
                    if (alert != null && alert.gameObject.activeSelf)
                    {
                        alert.gameObject.SetActive(false);
                        count++;
                    }
                    Transform glow = kt.transform.Find("OuterGlow");
                    if (glow != null)
                    {
                        bool want = false;
                        try
                        {
                            want = kt.isOn;
                        }
                        catch (Exception)
                        {
                            // expected: 读不到页签开关状态时按未选中处理，光晕隐藏而非误亮
                        }
                        if (glow.gameObject.activeSelf != want)
                        {
                            glow.gameObject.SetActive(want);
                        }
                    }
                }
                if (count > 0)
                {
                    MFLog.Info(LogCat.Space, "[Space] 页签指示器清理：关闭 " + count + " 个 AlertIcon");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 页签指示器清理异常");
            }
        }

        private static void CallNotifications(ManagementMenu menu)
        {
            try
            {
                if (menu == null)
                {
                    return;
                }
                // ★ 修复：在实例真实类型链上解析（否则 Invoke 抛 "Object does not match target type"）
                if (notifyMethod == null
                    || notifyMethod.DeclaringType == null
                    || !notifyMethod.DeclaringType.IsInstanceOfType(menu))
                {
                    notifyMethod = FindNotificationsMethod(menu.GetType());
                }
                if (notifyMethod != null)
                {
                    notifyMethod.Invoke(menu, null);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 调 OnNotificationsChanged 失败");
            }
        }

        // 在类型链上找无参的 OnNotificationsChanged（DeclaringOnly 逐级向上）
        private static MethodInfo FindNotificationsMethod(Type t)
        {
            while (t != null)
            {
                MethodInfo m = t.GetMethod("OnNotificationsChanged",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (m != null && m.GetParameters().Length == 0)
                {
                    return m;
                }
                t = t.BaseType;
            }
            return null;
        }

        private static void EnforceGlow(ManagementMenu menu)
        {
            try
            {
                KToggle[] toggles = menu.GetComponentsInChildren<KToggle>(true);
                for (int i = 0; i < toggles.Length; i++)
                {
                    KToggle kt = toggles[i];
                    if (kt == null)
                    {
                        continue;
                    }
                    Transform glow = kt.transform.Find("OuterGlow");
                    if (glow == null)
                    {
                        continue;
                    }
                    bool want = false;
                    try
                    {
                        want = kt.isOn;
                    }
                    catch (Exception)
                    {
                        // expected: 读不到页签开关状态时按未选中处理，光晕隐藏而非误亮
                    }
                    if (glow.gameObject.activeSelf != want)
                    {
                        glow.gameObject.SetActive(want);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[Space] 页签光晕校正异常");
            }
        }
    }
}
