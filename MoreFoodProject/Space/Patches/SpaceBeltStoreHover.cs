using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public class SpaceBeltStoreMarker : MonoBehaviour
    {
    }

    [HarmonyPatch(typeof(StarmapHexCellInventoryVisuals), "RefreshVisuals", new Type[0])]
    internal static class SpaceBeltStoreHoverFix
    {
        // 原版会 Assets.GetPrefab(Items[i].ID) 后直接 prefab.PrefabID()，坏 id 会让它 NRE 崩，先剔除坏项
        private static void Prefix(StarmapHexCellInventoryVisuals __instance)
        {
            try
            {
                if (__instance == null || __instance.inventory == null)
                {
                    return;
                }
                System.Collections.Generic.List<StarmapHexCellInventory.SerializedItem> items = __instance.inventory.Items;
                if (items == null)
                {
                    return;
                }
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    StarmapHexCellInventory.SerializedItem it = items[i];
                    if (it == null)
                    {
                        items.RemoveAt(i);
                        continue;
                    }
                    if (Assets.GetPrefab(it.ID) == null)
                    {
                        Debug.LogWarning("[Space] 星图库存剔除无效 id=" + it.ID + "（该 prefab 不存在）");
                        items.RemoveAt(i);
                    }
                }
            }
            catch (Exception)
            {
                // expected: 剔除无效条目失败时保持原清单，最多少一次无效项清理，不影响存档数据
            }
        }

        private static void Postfix(StarmapHexCellInventoryVisuals __instance)
        {
            try
            {
                if (__instance == null || __instance.gameObject == null)
                {
                    return;
                }
                if (__instance.GetComponent<SpaceBeltStoreMarker>() == null)
                {
                    return;
                }
                KSelectable selectable = __instance.GetComponent<KSelectable>();
                if (selectable != null && selectable.IsSelectable)
                {
                    selectable.IsSelectable = false;
                    MFLog.Info(LogCat.Space, "[Space] 打捞点看板：已移出星图悬停集合（" + __instance.gameObject.name + "）");
                }
            }
            catch (Exception)
            {
                // expected: 移出可选中集合失败时该看板仍可被选中，仅多一条悬停提示，不影响运行
            }
        }
    }
}
