// Buildings/AutoMiner/AutoMinerPatches.cs — 自动采矿机采集模式的全部 Harmony 补丁装配

using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 挂载模式状态组件到 prefab，随存档序列化
    [HarmonyPatch(typeof(AutoMinerConfig), "DoPostConfigureComplete")]
    internal static class AutoMiner_Config_Patch
    {
        private static void Postfix(GameObject go)
        {
            try
            {
                if (go != null) go.AddOrGet<AutoMinerHarvestMode>();
            }
            catch (Exception e) { MFLog.HotFail("patch:AutoMiner_Config", e); }
        }
    }

    // 成熟事件双钩：Prefix 抢在原生建 chore 前锁定，Postfix 投递节流信号（详见生态.md §6.2）
    [HarmonyPatch(typeof(Harvestable), "SetCanBeHarvested")]
    internal static class Harvestable_SetCanBeHarvested_Patch
    {
        private static void Prefix(Harvestable __instance, bool state)
        {
            try
            {
                if (state) AutoMinerHarvestScheduler.ClaimOnReady(__instance);
            }
            catch (Exception e) { MFLog.HotFail("patch:Harvestable_ReadyPrefix", e); }
        }

        private static void Postfix(Harvestable __instance, bool state)
        {
            try
            {
                if (state) AutoMinerHarvestScheduler.Signal(__instance);
            }
            catch (Exception e) { MFLog.HotFail("patch:Harvestable_Ready", e); }
        }
    }

    // 采矿机出生/清理时维护活跃列表，切回挖矿模式主动释放锁
    [HarmonyPatch(typeof(AutoMiner), "OnSpawn")]
    internal static class AutoMiner_OnSpawn_Patch
    {
        private static void Postfix(AutoMiner __instance)
        {
            try
            {
                AutoMinerHarvestScheduler.RegisterMiner(__instance);
                // 存档恢复 / 建造完成时若已是采集模式，立即检查一次，不等成熟信号或 600s 兜底
                AutoMinerHarvestScheduler.ScanNow(__instance);
            }
            catch (Exception e) { MFLog.HotFail("patch:AutoMiner_Spawn", e); }
        }
    }

    [HarmonyPatch(typeof(AutoMiner), "OnCleanUp")]
    internal static class AutoMiner_OnCleanUp_Patch
    {
        private static void Prefix(AutoMiner __instance)
        {
            try { AutoMinerHarvestScheduler.UnregisterMiner(__instance); }
            catch (Exception e) { MFLog.HotFail("patch:AutoMiner_CleanUp", e); }
        }
    }

    // 1Hz 节流入口：挖矿模式完全放行原生，采集模式走调度器并接管 dig_cell
    [HarmonyPatch(typeof(AutoMiner), "Sim1000ms")]
    internal static class AutoMiner_Sim1000ms_Patch
    {
        private static bool Prefix(AutoMiner __instance)
        {
            try
            {
                var mode = __instance.GetComponent<AutoMinerHarvestMode>();
                if (mode == null || !mode.harvestMode) return true;
                var op = __instance.GetComponent<Operational>();
                if (op == null || !op.IsOperational)
                {
                    // 断电/禁用 = 采集停止：启动空闲超时（超时应释放整批锁并归还 chore）
                    AutoMinerHarvestScheduler.MarkIdle(__instance);
                    AutoMinerHarvestScheduler.DropStale(__instance);
                    return false;
                }

                AutoMinerHarvestScheduler.Tick();
                AutoMinerHarvestScheduler.DropStale(__instance);
                // 推进 FIFO 队列：取出/校验当前工单一株，目标失效自动丢弃续下一株
                var lease = AutoMinerHarvestScheduler.Advance(__instance);
                int cell = lease != null ? lease.cell : Grid.InvalidCell;
                if (cell != AutoMinerReflector.GetDigCell(__instance))
                {
                    AutoMinerReflector.SetDigCell(__instance, cell);
                    AutoMinerReflector.SetRotationComplete(__instance, false);
                }
                // 目标一确定就同步激光 clip，避免 digging 首帧用旧的远距离 radius 闪一下
                if (cell != Grid.InvalidCell) AutoMinerReflector.SyncLaserClip(__instance, cell);
                op.SetActive(cell != Grid.InvalidCell, false);
                return false;
            }
            catch (Exception e) { MFLog.HotFail("patch:AutoMiner_Sim", e); return true; }
        }
    }

    // 采集中断：目标被复制人抢先或失效时，放弃本次并释放锁，机械臂复位
    [HarmonyPatch(typeof(AutoMiner), "UpdateDig")]
    internal static class AutoMiner_UpdateDig_Patch
    {
        private static bool Prefix(AutoMiner __instance, float dt)
        {
            try
            {
                var mode = __instance.GetComponent<AutoMinerHarvestMode>();
                if (mode == null || !mode.harvestMode) return true;
                int cell = AutoMinerReflector.GetDigCell(__instance);
                if (cell == Grid.InvalidCell) return false;
                if (!AutoMinerReflector.GetRotationComplete(__instance)) return false;

                var lease = AutoMinerHarvestScheduler.CurrentLease(__instance);
                var plant = lease != null ? lease.target : null;
                if (plant == null || !plant.CanBeHarvested || plant.HasChore() || plant.worker != null)
                {
                    // 目标失效：释放这一株，机械臂复位；下一秒 Advance 自动取队列下一株
                    AutoMinerHarvestScheduler.Complete(__instance);
                    AutoMinerReflector.SetDigCell(__instance, Grid.InvalidCell);
                    AutoMinerReflector.SetRotationComplete(__instance, false);
                    return false;
                }

                // 同步激光 clip 到目标格；原生 UpdateDig 被跳过后旧值会让光束射偏（详见生态.md §6.2）
                AutoMinerReflector.SyncLaserClip(__instance, cell);

                // 累加游戏秒 dt，满 5 秒才收割（对齐 20 级园艺复制人）
                if (!AutoMinerHarvestScheduler.TickProgress(__instance, dt)) return false;

                plant.Harvest();
                AutoMinerHarvestScheduler.Complete(__instance);
                AutoMinerReflector.SetDigCell(__instance, Grid.InvalidCell);
                AutoMinerReflector.SetRotationComplete(__instance, false);
                return false;
            }
            catch (Exception e) { MFLog.HotFail("patch:AutoMiner_Dig", e); return true; }
        }
    }

    // 采集锁定期间拒绝原生为复制人新建收割 chore，保证目标独占
    [HarmonyPatch(typeof(Harvestable), "OnMarkedForHarvest")]
    internal static class Harvestable_OnMarkedForHarvest_Patch
    {
        private static bool Prefix(Harvestable __instance)
        {
            try
            {
                if (AutoMinerHarvestScheduler.IsLocked(__instance)) return false;
                return true;
            }
            catch (Exception e) { MFLog.HotFail("patch:Harvestable_Chore", e); return true; }
        }
    }

    // 模式切换入口：写回状态并兜底释放旧锁，供侧屏 Toggle 调用
    internal static class AutoMinerModeSwitch
    {
        internal static void Set(AutoMiner miner, bool harvest)
        {
            if (miner == null) return;
            try
            {
                var mode = miner.GetComponent<AutoMinerHarvestMode>();
                if (mode == null) return;
                mode.harvestMode = harvest;
                if (harvest)
                {
                    // 切换即一次性扫描范围内已成熟植物，不等成熟信号或兜底
                    AutoMinerHarvestScheduler.ScanNow(miner);
                }
                else
                {
                    // 切回挖矿：释放整批队列锁（含 Active + Pending），归还 chore 给复制人
                    AutoMinerHarvestScheduler.ReleaseAllFor(miner);
                    AutoMinerReflector.SetDigCell(miner, Grid.InvalidCell);
                    AutoMinerReflector.SetRotationComplete(miner, false);
                    var op = miner.GetComponent<Operational>();
                    if (op != null) op.SetActive(false, false);
                }
            }
            catch (Exception e) { MFLog.HotFail("autominer.switch", e); }
        }
    }
}
