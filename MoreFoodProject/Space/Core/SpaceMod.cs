using HarmonyLib;
using KMod;
using UnityEngine;

namespace MoreFood_KFC
{
// Space 装载钩子：合并后宿主唯一入口是 vs_presetMod；
    public static class SpaceMod
    {
        public static void OnLoad(Harmony harmony)
        {
            // ★ 防回归（K1）：基类 KMod.UserMod2.OnLoad 自带 harmony.PatchAll(this.assembly)。
            try
            {
                SpaceTestRuntime.LogConfig();
                StarMapVisuals.VerifyReflection();
                TradeRegistry.Init();
                MFLog.Info(LogCat.Space, "[Space] 装载完成（不在装载期碰 Db：Db 未组装完，访问会触发原版 LogError 被 LogCatcher 崩溃）");
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[Space] 装载期初始化异常");
            }

            ManagementMenuGlowFix.Install(harmony);

            // 叙事者选择面板的「高级设置」注入（事件框架缺失时自行跳过）
            AdvancedSettingsInjector.Install(harmony);

            // 持续 buff 的补挂通道（孢子植物等）：失败只降级，不影响 mod 本体加载
            try { SpaceRealEvents.InstallOptionalPatches(harmony); }
            catch (System.Exception e) { MFLog.Exc(LogCat.General, e, "[Space] 可选补丁初始化异常"); }
        }
    }
}
