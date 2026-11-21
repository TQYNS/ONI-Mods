using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(DiningTableConfig), "DoPostConfigureComplete")]
    public static class DrinkGarnish_DiningTable_Patch
    {
        // Harmony 补丁：Postfix（参数 go：目标对象）
        public static void Postfix(GameObject go)
        {
            try
            {
                DrinkGarnishTableUtil.AddDeliveries(go, 1);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkGarnish_DiningTable_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(MultiMinionDiningTableConfig), "DoPostConfigureComplete")]
    public static class DrinkGarnish_MultiDiningTable_Patch
    {
        // Harmony 补丁：Postfix（参数 go：目标对象）
        public static void Postfix(GameObject go)
        {
            try
            {
                DrinkGarnishTableUtil.AddDeliveries(go, Mathf.Max(1, MultiMinionDiningTableConfig.SeatCount));
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkGarnish_MultiDiningTable_Patch", e); }
        }
    }

    internal static class DrinkGarnishTableUtil
    {
        // 每个座位可放的调料量
        private const float CapacityPerSeat = 1f;
        // 每次每座位的补充量
        private const float RefillPerSeat = 0.5f;

        // 给餐桌追加调料配送：每桌 4 类调料各追加一个 ManualDeliveryKG
        public static void AddDeliveries(GameObject go, int seats)
        {
            if (go == null) return;

            Storage storage = go.GetComponent<Storage>();
            if (storage == null)
            {
                Debug.LogWarning("[MoreFood/Drink][Garnish] 餐桌没有 Storage，跳过饮品配送");
                return;
            }

            storage.capacityKg += CapacityPerSeat * seats * 4f;

            Add(go, storage, Drinks.DrinkWineTag, seats);
            Add(go, storage, Drinks.DrinkCoffeeTag, seats);
            Add(go, storage, Drinks.DrinkSodaTag, seats);
            Add(go, storage, Drinks.DrinkOtherTag, seats);
        }

        private static void Add(GameObject go, Storage storage, Tag tag, int seats)
        {
            ManualDeliveryKG md = go.AddComponent<ManualDeliveryKG>();
            md.SetStorage(storage);
            md.RequestedItemTag = tag;
            md.capacity = CapacityPerSeat * seats;
            md.refillMass = RefillPerSeat * seats;
            md.choreTypeIDHash = Db.Get().ChoreTypes.FoodFetch.IdHash;
            md.ShowStatusItem = false;
        }
    }
}
