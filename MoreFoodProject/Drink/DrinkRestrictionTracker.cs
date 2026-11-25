// DrinkRestrictionTracker.cs — 饮水系统 · 【饮品管制的独立持久化状态】

using System;
using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class DrinkRestrictionTracker : KMonoBehaviour
    {
        // 被该小人【禁止作为饮水目标】的饮品 tag（与食物管制完全无关）
        [Serialize]
        public List<string> forbiddenDrinkTags = new List<string>();

        // 播种：机制照原版 ConsumableConsumer.OnPrefabInit —— 建 prefab 时把全局默认禁饮清单种给每个小人（读档后会被存档值覆盖）
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            try
            {
                if (forbiddenDrinkTags == null) forbiddenDrinkTags = new List<string>();
                DrinkRestrictionDefaults.SeedInto(forbiddenDrinkTags);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Restrict] 默认禁饮播种异常");
            }
        }


        public bool IsPermitted(string drinkTag)
        {
            if (string.IsNullOrEmpty(drinkTag)) return true;
            try { return !forbiddenDrinkTags.Contains(drinkTag); }
            catch { return true; }
        }

        // 设置某复制人对某类饮品的许可状态（独立持久化，跟存档走）
        public void SetPermitted(string drinkTag, bool permitted)
        {
            if (string.IsNullOrEmpty(drinkTag)) return;
            try
            {
                if (permitted)
                {
                    if (forbiddenDrinkTags.Contains(drinkTag)) forbiddenDrinkTags.Remove(drinkTag);
                }
                else
                {
                    if (!forbiddenDrinkTags.Contains(drinkTag)) forbiddenDrinkTags.Add(drinkTag);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Restrict] SetPermitted 异常");
            }
        }

        public bool HasAnyForbidden { get { return forbiddenDrinkTags != null && forbiddenDrinkTags.Count > 0; } }

        // 取小人的禁饮清单组件（没有则返回 null，调用方按"全允许"处理）
        public static DrinkRestrictionTracker Get(GameObject minion)
        {
            if (minion == null) return null;
            try
            {
                var t = minion.GetComponent<DrinkRestrictionTracker>();
                if (t == null) t = minion.GetComponentInChildren<DrinkRestrictionTracker>();
                return t;
            }
            catch { return null; }
        }
    }

    // 预挂到小人 prefab —— 与 WorkRush 的补丁同一手法
    [HarmonyPatch(typeof(MinionConfig), "CreatePrefab", new Type[0])]
    public static class DrinkRestrictionTracker_Prefab_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(GameObject __result)
        {
            if (__result == null) return;
            try
            {
                __result.AddOrGet<DrinkRestrictionTracker>();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Restrict] 预挂 DrinkRestrictionTracker 失败");
            }
        }
    }
}
