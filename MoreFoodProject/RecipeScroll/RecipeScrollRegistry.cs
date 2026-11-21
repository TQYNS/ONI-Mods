// RecipeScrollRegistry.cs —— 配方卷轴 · 注册与管理

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public sealed class RecipeScrollInfo
    {
        // 目标食物 id
        public string foodId;
        // 显示名
        public string displayName;
        // 卷轴物品 id
        public string itemId;
        // 是否已读过
        public bool read;
    }

    // 一条卷轴注册数据（工厂化清单条目）：itemId 显式指定，ID 可控。
    public sealed class RecipeScrollSpec
    {
        public string itemId;      // 卷轴物品 id（显式指定）
        public string foodId;      // 被解锁的食物 id
        public string displayName; // 显示名（缺省用 foodId）

        public RecipeScrollSpec(string itemId, string foodId, string displayName = null)
        {
            this.itemId = itemId;
            this.foodId = foodId;
            this.displayName = string.IsNullOrEmpty(displayName) ? foodId : displayName;
        }
    }

    public static class RecipeScrollRegistry
    {
        // 卷轴专属 Tag 名（Space 侧 YAML 按它定价/售卖）。
        public const string ItemTagName = "RecipeScroll";
        public static readonly Tag ItemTag = new Tag(ItemTagName);

        private static readonly List<RecipeScrollInfo> all = new List<RecipeScrollInfo>();
        private static readonly Dictionary<string, RecipeScrollInfo> byItem =
            new Dictionary<string, RecipeScrollInfo>(StringComparer.Ordinal);
        private static readonly Dictionary<string, RecipeScrollInfo> byFood =
            new Dictionary<string, RecipeScrollInfo>(StringComparer.Ordinal);
        private static readonly HashSet<string> readFoods = new HashSet<string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> recipeOwner =
            new Dictionary<string, string>(StringComparer.Ordinal);
        // 卷轴配方是否已解析完
        private static bool recipesResolved;

        public static List<RecipeScrollInfo> All { get { return all; } }

        public static void ResetForSession()
        {
            readFoods.Clear();
            foreach (KeyValuePair<string, RecipeScrollInfo> kv in byFood)
            {
                if (kv.Value != null) kv.Value.read = false;
            }
            recipesResolved = false;
            recipeOwner.Clear();
            MFLog.Info(LogCat.UI, "[MoreFood/RecipeScroll] 会话重置：已读状态清空（等待读档恢复）");
        }

        // 登记一个卷轴（itemId 由调用方指定）。
        public static RecipeScrollInfo Register(string itemId, string foodId, string displayName = null)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(foodId)) return null;

            RecipeScrollInfo exist;
            if (byItem.TryGetValue(itemId, out exist))
            {
                if (!string.IsNullOrEmpty(displayName)) exist.displayName = displayName;
                return exist;
            }
            RecipeScrollInfo info = new RecipeScrollInfo();
            info.itemId = itemId;
            info.foodId = foodId;
            info.displayName = string.IsNullOrEmpty(displayName) ? foodId : displayName;
            info.read = readFoods.Contains(foodId);
            all.Add(info);
            byItem[itemId] = info;
            if (!byFood.ContainsKey(foodId)) byFood[foodId] = info;
            return info;
        }

        // foodId：food id
        public static string DisplayNameOf(string foodId)
        {
            RecipeScrollInfo info;
            if (!string.IsNullOrEmpty(foodId) && byFood.TryGetValue(foodId, out info)
                && info != null && !string.IsNullOrEmpty(info.displayName))
            {
                return info.displayName;
            }
            return foodId;
        }

        // 全部受管卷轴清单（新增卷轴只改这里；ID 显式指定）。
        public static List<RecipeScrollSpec> PlannedScrolls()
        {
            return new List<RecipeScrollSpec>
            {
                new RecipeScrollSpec("RecipeScroll_CrazyThursday", "CrazyThursday", ModStrings.ITEMS.FOOD.CRAZYTHURSDAY.NAME),
                new RecipeScrollSpec("RecipeScroll_Rum", "Rum", ModStrings.ITEMS.FOOD.RUM.NAME),
                new RecipeScrollSpec("RecipeScroll_Moutai", "Moutai", ModStrings.ITEMS.FOOD.MOUTAI.NAME),
                new RecipeScrollSpec("RecipeScroll_MunichBeer", "MunichBeer", ModStrings.ITEMS.FOOD.MUNICHBEER.NAME),
                new RecipeScrollSpec("RecipeScroll_CoffeeDoubleSkinJelly", "CoffeeDoubleSkinJelly", ModStrings.ITEMS.FOOD.COFFEEDOUBLESKINJELLY.NAME),
            };
        }

        // 按清单批量注册卷轴。
        public static int RegisterFrom(List<RecipeScrollSpec> specs)
        {
            if (specs == null) return 0;
            int n = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                RecipeScrollSpec s = specs[i];
                if (s == null) continue;
                if (Register(s.itemId, s.foodId, s.displayName) != null) n++;
            }
            return n;
        }

        // 幂等批量注册（入口）。
        public static int RegisterAll()
        {
            if (all.Count > 0) return all.Count;

            int n = RegisterFrom(PlannedScrolls());

            MFLog.Info(LogCat.UI, "[MoreFood/RecipeScroll] 已注册配方卷轴 " + n + " 个（受管；Tag=" + ItemTagName + "）");
            return n;
        }

        // 卷轴物品固定的 Tag 集合（建 prefab 时用；与 EnsureScrollTag 同源）。
        public static List<Tag> ScrollTags()
        {
            return new List<Tag> { ItemTag, GameTags.IndustrialIngredient, GameTags.PedestalDisplayable };
        }

        // 给定卷轴物品 → 追加卷轴专属 Tag（幂等）。
        public static void EnsureScrollTag(GameObject go)
        {
            if (go == null) return;
            KPrefabID pid = go.GetComponent<KPrefabID>();
            if (pid == null) return;
            List<Tag> tags = ScrollTags();
            for (int i = 0; i < tags.Count; i++)
            {
                if (!pid.HasTag(tags[i])) pid.AddTag(tags[i], false);
            }
        }

        // 配方归属解析
        public static void ResolveRecipes()
        {
            if (recipesResolved) return;
            if (all.Count == 0) return;

            ComplexRecipeManager mgr = ComplexRecipeManager.Get();
            if (mgr == null || mgr.recipes == null || mgr.recipes.Count == 0)
            {
                return;   // 不锁死，等 recipes 有数据再解析
            }

            recipesResolved = true;
            recipeOwner.Clear();
            try
            {
                for (int i = 0; i < mgr.recipes.Count; i++)
                {
                    ComplexRecipe r = mgr.recipes[i];
                    if (r == null || string.IsNullOrEmpty(r.id) || r.results == null) continue;

                    for (int k = 0; k < r.results.Length; k++)
                    {
                        ComplexRecipe.RecipeElement el = r.results[k];
                        if (el == null) continue;

                        string outId = el.material.Name;
                        if (byFood.ContainsKey(outId))
                        {
                            recipeOwner[r.id] = outId;
                            break;
                        }
                    }
                }

                MFLog.Info(LogCat.UI, "[MoreFood/RecipeScroll] 配方归属解析完成：受管食物 " + all.Count
                    + "，命中受管配方 " + recipeOwner.Count + " 条");
            }
            catch (Exception e)
            {
                recipesResolved = false;
                MFLog.Exc(LogCat.UI, e, "[MoreFood/RecipeScroll] 解析配方归属失败");
            }
        }

        // 只读判据
        public static bool IsManaged(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            ResolveRecipes();
            return recipeOwner.ContainsKey(recipeId);
        }

        // recipeId：recipe id
        public static string FoodIdOfRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return null;
            ResolveRecipes();
            string foodId;
            return recipeOwner.TryGetValue(recipeId, out foodId) ? foodId : null;
        }

        // recipeId：标识 id
        public static bool IsReadRecipe(string recipeId)
        {
            string foodId = FoodIdOfRecipe(recipeId);
            if (foodId == null) return true;
            return readFoods.Contains(foodId);
        }

        // 查阅 / 解锁
        public static bool IsReadFood(string foodId)
        {
            return !string.IsNullOrEmpty(foodId) && readFoods.Contains(foodId);
        }

        // foodId：food id
        public static void MarkRead(string foodId)
        {
            if (string.IsNullOrEmpty(foodId)) return;
            ResolveRecipes();
            bool added = readFoods.Add(foodId);
            RecipeScrollInfo info;
            if (byFood.TryGetValue(foodId, out info) && info != null) info.read = true;
            if (added)
            {
                MFLog.Verbose(LogCat.UI, "[MoreFood/RecipeScroll] ★ 已查阅配方卷轴 " + foodId);
            }
            RecipeScrollSaveData.WriteThrough();
        }

        // 存档读写
        internal static void LoadReadSet(List<string> saved)
        {
            readFoods.Clear();
            if (saved != null)
            {
                for (int i = 0; i < saved.Count; i++)
                {
                    if (!string.IsNullOrEmpty(saved[i])) readFoods.Add(saved[i]);
                }
            }
            foreach (KeyValuePair<string, RecipeScrollInfo> kv in byFood)
            {
                if (kv.Value != null) kv.Value.read = readFoods.Contains(kv.Key);
            }
            MFLog.Info(LogCat.UI, "[MoreFood/RecipeScroll] 读档：已查阅的配方卷轴 " + readFoods.Count);
        }

        // 把已读配方集覆盖式写进 target 列表（存档序列化用）
        internal static void AppendReadSet(List<string> target)
        {
            target.Clear();
            foreach (string s in readFoods) target.Add(s);
        }

        // Space 只读接入
        public static bool TryGetByItem(string itemId, out RecipeScrollInfo info)
        {
            info = null;
            return !string.IsNullOrEmpty(itemId) && byItem.TryGetValue(itemId, out info) && info != null;
        }

        // 按食物 id 取回卷轴注册信息；未注册返回 false
        public static bool TryGetByFood(string foodId, out RecipeScrollInfo info)
        {
            info = null;
            return !string.IsNullOrEmpty(foodId) && byFood.TryGetValue(foodId, out info) && info != null;
        }

        // itemId：标识 id
        public static bool IsScrollItem(string itemId)
        {
            RecipeScrollInfo info;
            return TryGetByItem(itemId, out info);
        }

        // itemId：标识 id
        public static bool IsReadItem(string itemId)
        {
            RecipeScrollInfo info;
            return TryGetByItem(itemId, out info) && info.read;
        }

        // itemId：item id
        public static string FoodIdOfItem(string itemId)
        {
            RecipeScrollInfo info;
            return TryGetByItem(itemId, out info) ? info.foodId : null;
        }

        // foodId：food id
        public static List<string> RecipeIdsOfFood(string foodId)
        {
            ResolveRecipes();
            List<string> list = new List<string>();
            foreach (KeyValuePair<string, string> kv in recipeOwner)
            {
                if (kv.Value == foodId) list.Add(kv.Key);
            }
            return list;
        }
    }
}
