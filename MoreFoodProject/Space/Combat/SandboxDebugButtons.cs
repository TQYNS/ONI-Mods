using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class SandboxDebugButtons
    {
        internal static bool Visible
        {
            get
            {
                try
                {
                    return Game.Instance != null && Game.Instance.SandboxModeActive;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        internal static void RefreshDetails()
        {
            try
            {
                DetailsScreen screen = DetailsScreen.Instance;
                if (screen != null && screen.target != null)
                {
                    screen.Refresh(screen.target);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 沙盒调试按钮刷新失败");
            }
        }
    }

    [HarmonyPatch(typeof(Game), "set_SandboxModeActive")]
    internal static class SandboxToggleRefreshPatch
    {
private static void Postfix()
        {
            try
            {
                SandboxDebugButtons.RefreshDetails();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SandboxToggleRefreshPatch", e); }
        }
    }

    [HarmonyPatch(typeof(ButtonMenuSideScreen), "IsValidForTarget")]
    internal static class SandboxButtonMenuValidPatch
    {
        private static bool Prefix(GameObject target, ref bool __result)
        {
            try
            {
                if (target == null)
                {
                    __result = false;
                    return false;
                }
                List<ISidescreenButtonControl> controls = target.GetAllSMI<ISidescreenButtonControl>();
                if (controls == null)
                {
                    controls = new List<ISidescreenButtonControl>();
                }
                controls.AddRange(target.GetComponents<ISidescreenButtonControl>());
                for (int i = 0; i < controls.Count; i++)
                {
                    ISidescreenButtonControl c = controls[i];
                    if (c != null && c.SidescreenEnabled())
                    {
                        __result = true;
                        return false;
                    }
                }
                __result = false;
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ButtonMenuSideScreen), "SetTarget")]
    internal static class SandboxButtonMenuFilterPatch
    {
        private static void Postfix(ButtonMenuSideScreen __instance)
        {
            try
            {
                FieldInfo field = AccessTools.Field(typeof(ButtonMenuSideScreen), "targets");
                if (field == null)
                {
                    return;
                }
                List<ISidescreenButtonControl> targets =
                    field.GetValue(__instance) as List<ISidescreenButtonControl>;
                if (targets == null)
                {
                    return;
                }
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    ISidescreenButtonControl c = targets[i];
                    if (c == null || !c.SidescreenEnabled())
                    {
                        targets.RemoveAt(i);
                    }
                }
                MethodInfo refresh = AccessTools.Method(typeof(ButtonMenuSideScreen), "Refresh");
                if (refresh != null)
                {
                    refresh.Invoke(__instance, null);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 沙盒调试按钮过滤失败");
            }
        }
    }
}
