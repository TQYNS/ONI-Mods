// RecipeScrollItems.cs —— 配方卷轴 · 物品 prefab（每个注册项一个卷轴物品）

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public class RecipeScrollItems : IMultiEntityConfig
    {
        // 全部配方卷轴共用同一张动画
        private const string AnimFile = "recipescroll_kanim";
        // 动画资源缺失时的回退
        private const string FallbackAnim = "floppy_disc_kanim";

        public List<GameObject> CreatePrefabs()
        {
            RecipeScrollRegistry.RegisterAll();   // 幂等：必须在建 prefab 之前把表填好
            List<GameObject> list = new List<GameObject>();
            List<RecipeScrollInfo> all = RecipeScrollRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                list.Add(Create(all[i]));
            }
            return list;
        }

        // Unity 回调：prefab 初始化（参数 inst：目标实例）
        public void OnPrefabInit(GameObject inst)
        {
        }

        // Unity 回调：实例生成（参数 inst：目标实例）
        public void OnSpawn(GameObject inst)
        {
        }

        private static GameObject Create(RecipeScrollInfo info)
        {
            KAnimFile file = Assets.GetAnim(AnimFile);
            if (file == null)
            {
                MFLog.Warn(LogCat.UI, "[MoreFood/RecipeScroll] 动画缺失 {0}，回退原版软盘", AnimFile);
                file = Assets.GetAnim(FallbackAnim);
            }
            // 散落物一律走 IngredientConfigs.CreateStandard 的标准口径（Front +
            // RECTANGLE 0.8×0.4 + EntitySplitter），与 37 个原料 / 模块 / 弹药完全一致。
            // 禁止回退到 CIRCLE 0.35：《战斗.md》§3.7 已实证 CIRCLE 分支把半径
            //   当成支撑高度，落地求解会把 transform 抬高 0.36 格，贴图随之悬空。
            // 沙盒实体列表是【过滤器驱动】的，Tag 仍由 ScrollTags() 同源提供。
            GameObject go = IngredientConfigs.CreateStandard(
                info.itemId,
                string.Format(ModStrings.SCROLL.NAME_FMT, FoodName(info.foodId)),
                RecipeScrollText.ShortDesc(info.foodId),
                file,
                RecipeScrollRegistry.ScrollTags());

            string foodId = info.foodId;
            // 阅读链路整体由 RecipeScrollPatches 的 OnClickRead 前缀接管（那里才拿得到"读的是哪个物体"，
            LoreBearerUtil.AddLoreTo(go, delegate(InfoDialogScreen screen)
            {
                // 原版兜底链路：只负责显示，不写状态、不解锁。
                // 解锁是 RecipeScrollPatches.OnClickRead 的唯一职责；这里若也 MarkRead，
                // 主补丁异常回落时会出现「状态已读但卷轴没消耗」的不一致。
                // 阅读窗口平时由主补丁 return false 接管，此回调不会执行。
                screen.SetHeader(string.Format(ModStrings.SCROLL.NAME_FMT, FoodName(foodId)));
                screen.AddPlainText(RecipeScrollText.UnlockText(
                    foodId, RecipeScrollRegistry.IsReadFood(foodId)));
                screen.AddDefaultOK(true);
            });

            // 工厂化收尾：注册即打标 —— 追加卷轴专属 Tag（幂等，与 additionalTags 同源）。
            RecipeScrollRegistry.EnsureScrollTag(go);

            return go;
        }

        // 显示名：**优先用注册时给的名字**（Mod 物品的 prefab 名是英文 id，从 prefab 取会拿到英文）。
        internal static string FoodName(string foodId)
        {
            return RecipeScrollRegistry.DisplayNameOf(foodId);
        }
    }

    // 卷轴正文 —— 用户口径：只要"已解锁 + 去哪儿看"，不写配方怎么做。
    internal static class RecipeScrollText
    {
        // foodId：food id
        internal static string ShortDesc(string foodId)
        {
            return string.Format(ModStrings.SCROLL.DESC_FMT, RecipeScrollItems.FoodName(foodId));
        }

        // 「xx 配方已解锁 / 请在 xx 的配方列表查看」——纯文本。
        // alreadyRead 必须是「本次阅读之前」的快照，由调用方在 MarkRead 之前取好。
        //   不能在这里查 IsReadFood：查阅补丁先写已读再取正文，就地查会永远命中
        //   ALREADY_FMT，导致首次阅读也显示「之前已经解锁过了」。
        internal static string UnlockText(string foodId, bool alreadyRead)
        {
            string food = RecipeScrollItems.FoodName(foodId);
            string machine = MachineName(foodId);
            if (alreadyRead)
            {
                return string.Format(ModStrings.SCROLL.ALREADY_FMT, food, machine);
            }
            return string.Format(ModStrings.SCROLL.UNLOCK_FMT, food, machine);
        }

        // 这条配方属于哪台生产机器（ComplexRecipe.id 的前缀就是机器 prefab id，
        internal static string MachineName(string foodId)
        {
            try
            {
                List<string> ids = RecipeScrollRegistry.RecipeIdsOfFood(foodId);
                for (int i = 0; i < ids.Count; i++)
                {
                    string rid = ids[i];
                    if (string.IsNullOrEmpty(rid))
                    {
                        continue;
                    }
                    int cut = rid.IndexOf("_I_", StringComparison.Ordinal);
                    string fabId = cut > 0 ? rid.Substring(0, cut) : rid;
                    GameObject prefab = Assets.TryGetPrefab(fabId);
                    if (prefab != null)
                    {
                        KPrefabID pid = prefab.GetComponent<KPrefabID>();
                        if (pid != null)
                        {
                            string n = pid.GetProperName();
                            if (!string.IsNullOrEmpty(n))
                            {
                                return n;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(fabId))
                    {
                        return fabId;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 逐个 id 解析名字失败时整体退回「未知机器」占位文案
            }
            return ModStrings.SCROLL.MACHINE_FALLBACK;
        }
    }
}
