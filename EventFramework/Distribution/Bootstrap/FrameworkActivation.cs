using System;
using HarmonyLib;
using EventFramework;
using EventFramework.Distribution.Contracts;
using EventFramework.Distribution.Diagnostics;
using EventFramework.Distribution.Patches;

namespace EventFramework.Distribution.Bootstrap
{
    // 装配顺序的唯一实现点
    internal static class FrameworkActivation
    {
        private static bool _done;

        internal static void Run(Harmony harmony)
        {
            if (_done) return;
            try
            {
                if (harmony == null)
                {
                    DistributionLog.Warn("Activate 收到空 harmony，跳过装配");
                    return;
                }
                _done = true;

                // 此刻是 OnAllModsLoaded：所有 Mod 的 loaded_content 都已就绪。
                // 通知加载顺序解析器「可以缓存了」，并清掉加载期可能写入的错误结果。
                LoadOrderResolver.MarkLoadComplete();

                // 先注入注册桥，宿主在 Db.Initialize 之后注册的事件处理器才有人接
                EventFrameworkApi.SetBridge(new EventBridgeImpl());

                // 存档桥自绑：必须在第一次 SaveGame.OnPrefabInit 之前完成，否则挂载点无人接
                FrameworkSaveManager.Ensure();

                // 顺序：8 生命周期 → 8 特性；handler、内建能力、槽位在后续批次接入
                int ok = LifecyclePatches.Install(harmony) + FeaturePatches.Install(harmony);
                int miss = LifecyclePatches.Skipped + FeaturePatches.Skipped;
                DistributionGuard.Check(EventFrameworkHub.WinnerCandidate, ok, miss);
                DistributionLog.Info("补丁安装完成：成功 " + ok + " 个，跳过 " + miss + " 个");
            }
            catch (Exception e)
            {
                DistributionLog.Warn("装配失败：" + e);
            }
        }
    }
}
