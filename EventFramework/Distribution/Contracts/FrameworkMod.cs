using System;
using System.Collections.Generic;
using HarmonyLib;
using KMod;
using EventFramework.Distribution.Bootstrap;

namespace EventFramework.Distribution.Contracts
{
    // 框架入口：每个内容 Mod 的 DLL 都会实例化，装配由静态守卫收敛为一次
    public sealed class FrameworkMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            // 不调 base.OnLoad，避免按每个 Mod 的 owner 重复装补丁
            try
            {
                DistributionLog.Info("框架入口已加载，等待装配");
            }
            catch (Exception e)
            {
                DistributionLog.Warn("FrameworkMod.OnLoad 失败：" + e);
            }
        }

        public override void OnAllModsLoaded(Harmony harmony, IReadOnlyList<Mod> mods)
        {
            try
            {
                EventFrameworkHub.Winner = new FrameworkBootstrap();
                FrameworkActivation.Run(new Harmony(EventFrameworkApi.FixedHarmonyId));
                DistributionLog.Info("装配完成 v" + EventFrameworkHub.Winner.Ver);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("FrameworkMod.OnAllModsLoaded 失败：" + e);
            }
        }
    }
}
