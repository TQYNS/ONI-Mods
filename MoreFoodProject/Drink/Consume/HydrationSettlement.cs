// 用途：饮水值唯一写入口，饮品 / 上桌 / 食物共用。
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class HydrationSettlement
    {
        // 按质量把饮水值写入 Amount，返回是否命中可结算对象。
        public static bool Apply(GameObject worker, float perKg, float mass)
        {
            if (worker == null || perKg <= 0f || mass <= 0f) return false;
            if (!DrinkDatabase.Ready) return false;

            AmountInstance inst = DrinkDatabase.Hydration.Lookup(worker);
            if (inst == null) return false;   // 仿生人 / 无 Hydration 实例

            float before = inst.value;
            inst.ApplyDelta(perKg * mass);
            MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink] " + worker.name + " 结算 " + mass.ToString("F2")
                      + "kg，水位 " + before.ToString("F1") + " → " + inst.value.ToString("F1"));
            return true;
        }
    }
}
