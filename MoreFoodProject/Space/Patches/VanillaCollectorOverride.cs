using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 接管原版 RocketModuleHexCellCollector.CollectUpdate 对 Mod 小行星带的开采
    [HarmonyPatch(typeof(RocketModuleHexCellCollector), "CollectUpdate")]
    internal static class VanillaCollectorOverridePatch
    {
        private const float MiningSpeed = 8f;       // 8 kg/秒（原版 7.5，略微提升）
        private const float ThrottleSeconds = 1f;   // 1 秒结算一次
        private const float IntTolerance = 0.01f;   // 整数容差

        private static bool Prefix(ref bool __result, RocketModuleHexCellCollector.Instance smi, float dt)
        {
            if (smi == null || dt <= 0f)
            {
                return true; // 走原版
            }
            try
            {
                // 只对 Mod 小行星带 cell 生效
                if (!IsAsteroidBeltCell(smi))
                {
                    return true; // 非小行星带，走原版
                }

                // 火箭移动中不开采（同原版）
                if (smi.IsSpaceshipMoving)
                {
                    __result = true;
                    return false;
                }

                Storage storage = smi.storage;
                if (storage == null)
                {
                    return true;
                }

                // 节流：累积 dt
                smi.MassCharge += dt;
                if (smi.MassCharge < ThrottleSeconds)
                {
                    __result = false; // 跳过原版，等累积到 1 秒
                    return false;
                }

                float dtEff = smi.MassCharge;
                smi.MassCharge = 0f;

                // 本次开采量（取整避免浮点）
                float want = Mathf.Round(dtEff * MiningSpeed);
                if (want <= 0f)
                {
                    __result = false;
                    return false;
                }

                float capacity = storage.RemainingCapacity();
                if (capacity <= 0f)
                {
                    __result = true; // 货舱满
                    return false;
                }
                want = Mathf.Min(want, Mathf.Floor(capacity)); // 容量也取整
                if (want < 1f)
                {
                    // 边界：货舱容不下"一份"（≥1kg）→ 不再继续入库，停止采收，
                    __result = true;
                    return false;
                }

                StarmapHexCellInventory inv = ClusterGrid.Instance.AddOrGetHexCellInventory(smi.StarmapLocation);
                if (inv == null || inv.Items == null || inv.Items.Count == 0)
                {
                    __result = false;
                    return false;
                }

                // 开采前快照（用于求和校验）
                float totalBefore = 0f;
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    StarmapHexCellInventory.SerializedItem it = inv.Items[i];
                    if (it != null)
                    {
                        totalBefore += it.Mass;
                    }
                }

                // 按顺序开采
                float remaining = want;
                float extracted = 0f;
                int guard = 0;
                while (remaining >= 1f && inv.Items.Count > 0 && guard < 10000)
                {
                    guard++;
                    StarmapHexCellInventory.SerializedItem item = inv.Items[0];
                    if (item == null || item.Mass <= 0f)
                    {
                        inv.Items.RemoveAt(0);
                        continue;
                    }

                    // 计算本次扣减量
                    float avail = Mathf.Min(remaining, item.Mass);

                    // 判断是否整数物品（开采前质量接近整数）
                    bool isInteger = Mathf.Abs(item.Mass - Mathf.Round(item.Mass)) < IntTolerance;
                    float take;
                    if (isInteger)
                    {
                        // 整数物品：Floor 扣减，保证 take 是整数
                        take = Mathf.Floor(avail);
                        if (take < 1f)
                        {
                            // 剩余不够 1kg，结束本轮（剩余存回 MassCharge 下次结算）
                            break;
                        }
                    }
                    else
                    {
                        // 非整数物品：按质量扣减
                        take = avail;
                    }

                    // 执行开采（ExtractAndStoreItemMass 精确扣减：扣多少加多少）
                    float actual = inv.ExtractAndStoreItemMass(item.ID, take, storage);
                    remaining -= actual;
                    extracted += actual;

                    // ExtractAndStoreItemMass 内部 DeleteItemMass 会在 Mass<=0 时 Remove。
                }

                // 剩余量存回 MassCharge（转回时间），下次结算补上
                if (remaining > 0.001f)
                {
                    smi.MassCharge = remaining / MiningSpeed;
                }

                // 逐条目整数性校验 + 补偿
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    StarmapHexCellInventory.SerializedItem it = inv.Items[i];
                    if (it == null || it.Mass <= 0f)
                    {
                        continue;
                    }
                    float rounded = Mathf.Round(it.Mass);
                    float diff = it.Mass - rounded;
                    if (Mathf.Abs(diff) > 0.0001f && Mathf.Abs(diff) < IntTolerance)
                    {
                        // 接近整数但不是整数 → 补偿为整数
                        it.Mass = rounded;
                    }
                }

                // 求和校验
                float totalAfter = 0f;
                for (int i = 0; i < inv.Items.Count; i++)
                {
                    StarmapHexCellInventory.SerializedItem it = inv.Items[i];
                    if (it != null)
                    {
                        totalAfter += it.Mass;
                    }
                }
                float sum = totalAfter + extracted;
                float totalDiff = sum - totalBefore;
                if (Mathf.Abs(totalDiff) > IntTolerance)
                {
                    Debug.LogWarning("[Space] 开采求和校验偏差: before=" + totalBefore
                        + " after=" + totalAfter + " extracted=" + extracted
                        + " diff=" + totalDiff + "（已补偿，不阻断）");
                }

                // 返回：货舱满 或 没有可开采物品 → true（完成）；否则 false（继续）
                __result = storage.RemainingCapacity() <= 0f || inv.Items.Count == 0;
                return false; // 跳过原版均摊
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 接管开采异常（回退原版）");
                return true; // 异常走原版，不崩游戏
            }
        }

        // 检查 cell 上是否存在 Mod 自定义小行星带。
        private static bool IsAsteroidBeltCell(RocketModuleHexCellCollector.Instance smi)
        {
            try
            {
                AxialI loc = smi.StarmapLocation;
                ClusterGrid grid = ClusterGrid.Instance;
                if (grid == null || grid.cellContents == null)
                {
                    return false;
                }
                List<ClusterGridEntity> list;
                if (!grid.cellContents.TryGetValue(loc, out list) || list == null)
                {
                    return false;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is SpaceAsteroidBelt)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 位置内容查询失败时判为不是 Mod 小行星带，交回原版采集判定
            }
            return false;
        }
    }

    // 钻头模块对 Mod 小行星带的支持

    [HarmonyPatch(typeof(ResourceHarvestModule.StatesInstance), "CheckIfCanDrill")]
    internal static class DrillCanHarvestPatch
    {
        private static bool Prefix(ref bool __result, ResourceHarvestModule.StatesInstance __instance)
        {
            try
            {
                Clustercraft craft = CraftHelper.GetCraft(__instance);
                if (craft == null) return true;

                ClusterGridEntity poi = craft.GetPOIAtCurrentLocation();
                if (poi == null) return true;
                if (!(poi is SpaceAsteroidBelt)) return true; // 原版 POI 走原版

                // Mod 小行星带：有钻石才能钻
                Storage storage = __instance.GetComponent<Storage>();
                bool hasDiamond = storage != null &&
                    storage.GetAmountAvailable(SimHashes.Diamond.CreateTag()) > 0f;

                // 设置 canHarvest 参数（驱动状态机进入 drilling）
                var sm = Traverse.Create(__instance).Property("sm").GetValue<ResourceHarvestModule>();
                if (sm != null)
                {
                    sm.canHarvest.Set(hasDiamond, __instance, false);
                }
                __result = hasDiamond;
                return false;
            }
            catch (Exception)
            {
                return true; // 异常走原版
            }
        }
    }

    [HarmonyPatch(typeof(ResourceHarvestModule.StatesInstance), "HarvestFromPOI")]
    internal static class DrillHarvestPatch
    {
        private const float MiningSpeed = 8f;       // 跟 CollectUpdate 一致
        private const float DiamondRatio = 0.05f;   // 1:20

        private static bool Prefix(ResourceHarvestModule.StatesInstance __instance, float dt)
        {
            try
            {
                Clustercraft craft = CraftHelper.GetCraft(__instance);
                if (craft == null) return true;

                ClusterGridEntity poi = craft.GetPOIAtCurrentLocation();
                if (poi == null) return true;
                if (!(poi is SpaceAsteroidBelt)) return true; // 原版 POI 走原版

                // Mod 小行星带：只消耗钻石，不开采（开采由 CollectUpdate 处理）
                float diamondCost = MiningSpeed * dt * DiamondRatio;
                if (diamondCost > 0f)
                {
                    Storage storage = __instance.GetComponent<Storage>();
                    if (storage != null)
                    {
                        float available = storage.GetAmountAvailable(SimHashes.Diamond.CreateTag());
                        if (available > 0f)
                        {
                            storage.ConsumeIgnoringDisease(
                                SimHashes.Diamond.CreateTag(),
                                Mathf.Min(diamondCost, available));
                        }
                    }
                }
                return false; // 跳过原版开采逻辑
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    internal static class CraftHelper
    {
        // 从 StatesInstance 获取 Clustercraft（钻头/货舱共用）。
        internal static Clustercraft GetCraft(StateMachine.Instance smi)
        {
            try
            {
                RocketModuleCluster rmc = smi.GetComponent<RocketModuleCluster>();
                if (rmc == null || rmc.CraftInterface == null) return null;
                return rmc.CraftInterface.GetComponent<Clustercraft>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
