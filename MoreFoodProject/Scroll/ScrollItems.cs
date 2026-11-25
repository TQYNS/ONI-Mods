using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public class ScrollItems : IMultiEntityConfig
    {
        // 档位 id：普通
        public const string TierCommon = "common";
        // 档位 id：优质
        public const string TierGood = "good";
        // 档位 id：传奇
        public const string TierLegendary = "legendary";

        // 全部档位（顺序即 UI 顺序）
        public static readonly string[] Tiers = new string[] { TierCommon, TierGood, TierLegendary };

        // 全部任务卷轴共用同一张动画
        private const string AnimFile = "spacescroll_kanim";
        // 动画资源缺失时的回退
        private const string FallbackAnim = "floppy_disc_kanim";

        private static readonly Dictionary<string, Action<ScrollReadView>> Readers =
            new Dictionary<string, Action<ScrollReadView>>(StringComparer.Ordinal);

        // 档位元数据表（顺序与 Tiers 一致）：prefab id / 显示名 / 描述
        private static readonly string[] PrefabIds = new string[]
        {
            "SpaceScrollCommon", "SpaceScrollGood", "SpaceScrollLegendary"
        };
        private static readonly string[] DisplayNames = new string[]
        {
            "普通任务卷轴", "精良任务卷轴", "传奇任务卷轴"
        };
        private static readonly string[] Descriptions = new string[]
        {
            "猎人协会签发的普通委托凭证。选中后点【阅读】可接取一次普通星图任务。",
            "猎人协会签发的精良委托凭证。选中后点【阅读】可接取一次精良星图任务。",
            "猎人协会签发的传奇委托凭证。选中后点【阅读】可接取一次传奇星图任务。"
        };

        // 档位在 Tiers 里的下标（未知档位回落 0 = 普通）
        private static int IndexOf(string tier)
        {
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (Tiers[i] == tier)
                {
                    return i;
                }
            }
            return 0;
        }

        // 按档位取卷轴 prefab id
        public static string PrefabId(string tier)
        {
            return PrefabIds[IndexOf(tier)];
        }

        // 按档位取卷轴显示名
        public static string DisplayName(string tier)
        {
            return DisplayNames[IndexOf(tier)];
        }

        // 按档位取卷轴描述文案
        public static string Description(string tier)
        {
            return Descriptions[IndexOf(tier)];
        }

        // 注册某档位的「阅读」回调（打开阅读窗口时调）
        public static void RegisterReader(string tier, Action<ScrollReadView> reader)
        {
            if (string.IsNullOrEmpty(tier) || reader == null)
            {
                return;
            }
            Readers[tier] = reader;
            MFLog.Info(LogCat.UI, "[MoreFood/Scroll] 阅读回调已注册：" + PrefabId(tier));
        }

        // 该档位是否注册了阅读回调
        public static bool HasReader(string tier)
        {
            return !string.IsNullOrEmpty(tier) && Readers.ContainsKey(tier);
        }

        public List<GameObject> CreatePrefabs()
        {
            List<GameObject> list = new List<GameObject>(Tiers.Length);
            for (int i = 0; i < Tiers.Length; i++)
            {
                list.Add(Create(Tiers[i]));
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

        private static GameObject Create(string tier)
        {
            KAnimFile file = Assets.GetAnim(AnimFile);
            if (file == null)
            {
                MFLog.Warn(LogCat.UI, "[MoreFood/Scroll] 动画缺失 {0}，回退原版软盘", AnimFile);
                file = Assets.GetAnim(FallbackAnim);
            }
            // 卷轴一律不带 GameTags.TechComponents（那是数据磁盘那类科技组件的 tag）
            List<Tag> tags = new List<Tag>
            {
                GameTags.IndustrialIngredient, GameTags.PedestalDisplayable, new Tag("SpaceScroll")
            };
            // 散落物一律走 IngredientConfigs.CreateStandard 的标准口径（Front +
            // RECTANGLE 0.8×0.4 + EntitySplitter），与 37 个原料 / 模块 / 弹药完全一致。
            // 禁止回退到 CIRCLE 0.35：《战斗.md》§3.7 已实证 CIRCLE 分支把半径
            //   当成支撑高度，落地求解会把 transform 抬高 0.36 格，贴图随之悬空。
            GameObject go = IngredientConfigs.CreateStandard(
                PrefabId(tier), DisplayName(tier), Description(tier), file, tags);

            string captured = tier;
            LoreBearerUtil.AddLoreTo(go, delegate(InfoDialogScreen screen)
            {
                ScrollReadView view = new ScrollReadView();
                OnRead(captured, view);
                view.ReplayInto(screen);
            });

            return go;
        }

        // 读物品身上的档位标记（没有则返回 null）
        internal static string TierOf(GameObject go)
        {
            if (go == null)
            {
                return null;
            }
            string id = null;
            KPrefabID pid = go.GetComponent<KPrefabID>();
            if (pid != null)
            {
                id = pid.PrefabTag.Name;
            }
            if (string.IsNullOrEmpty(id))
            {
                id = go.name;
            }
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (id == PrefabId(Tiers[i]))
                {
                    return Tiers[i];
                }
            }
            return null;
        }

        // 消耗一个卷轴（阅读后扣掉 1 个本体）
        internal static void ConsumeOne(GameObject go)
        {
            try
            {
                if (go == null)
                {
                    return;
                }
                Pickupable p = go.GetComponent<Pickupable>();
                if (p != null && p.TotalAmount > 1f)
                {
                    Pickupable one = p.Take(1f);
                    if (one != null)
                    {
                        Util.KDestroyGameObject(one.gameObject);
                        return;
                    }
                }
                Util.KDestroyGameObject(go);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll] 消耗卷轴失败");
            }
        }

        // 执行该档位的阅读回调（未注册则回退到默认提示）
        internal static void OnRead(string tier, ScrollReadView view)
        {
            Action<ScrollReadView> reader;
            if (Readers.TryGetValue(tier, out reader) && reader != null)
            {
                try
                {
                    reader(view);
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll] 阅读回调异常（" + PrefabId(tier) + "）");
                    if (view != null)
                    {
                        view.AddPlainText("委托解析失败，卷轴已保留，请稍后重试或查看日志。");
                        view.ConsumeAfterRead = false;   // 异常 ⇒ 没接成，不吞卷轴
                    }
                }
                return;
            }
            if (view != null)
            {
                view.AddPlainText("这张卷轴上盖着猎人协会的印记，但眼下没有人能解读它的内容。");
                view.ConsumeAfterRead = false;           // 回调未注册 ⇒ 没有接取动作
            }
        }

        // 在默认位置生成一个卷轴（档位无效则失败）
        public static bool SpawnScroll(string tier)
        {
            return SpawnScroll(tier, Grid.InvalidCell);
        }

        // 在指定格子生成一个卷轴（cell 非法时用默认落点）
        public static bool SpawnScroll(string tier, int cell)
        {
            GameObject prefab = null;
            try
            {
                prefab = Assets.TryGetPrefab(PrefabId(tier));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll] 取卷轴 prefab 失败");
            }
            if (prefab == null)
            {
                Debug.LogWarning("[MoreFood/Scroll] 找不到卷轴 prefab：" + PrefabId(tier));
                return false;
            }
            if (!Grid.IsValidCell(cell))
            {
                WorldContainer start = ClusterManager.Instance != null
                    ? ClusterManager.Instance.GetStartWorld() : null;
                cell = start != null ? start.GetSafeCell() : Grid.InvalidCell;
            }
            if (!Grid.IsValidCell(cell))
            {
                Debug.LogWarning("[MoreFood/Scroll] 找不到可放置卷轴的位置");
                return false;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                return false;
            }
            go.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore));
            go.SetActive(true);
            MFLog.Info(LogCat.UI, "[MoreFood/Scroll] 已把任务卷轴放到殖民地：" + PrefabId(tier));
            return true;
        }
    }

    [HarmonyPatch(typeof(LoreBearer), "OnClickRead")]
    internal static class ScrollReadPatch
    {
        private static bool Prefix(LoreBearer __instance)
        {
            try
            {
            GameObject go = null;
            try
            {
                if (__instance != null)
                {
                    go = __instance.gameObject;
                }
            }
            catch
            {
                go = null;
            }
            if (go == null)
            {
                return false;
            }
            string tier = ScrollItems.TierOf(go);
            if (tier == null)
            {
                return true;   // 非我们的卷轴 → 原版流程照旧
            }
            try
            {
                KSelectable selectable = go.GetComponent<KSelectable>();
                string title = selectable != null ? selectable.GetProperName() : ScrollItems.DisplayName(tier);
                ScrollReadView view = new ScrollReadView();
                ScrollItems.OnRead(tier, view);
                if (!ScrollReadWindow.Show(tier, title, view))
                {
                    InfoDialogScreen screen = LoreBearer.ShowPopupDialog()
                        .SetHeader(title).AddDefaultOK(true);
                    view.ReplayInto(screen);
                }
                // 只有真正接取成功才消耗；任务池满 / 星图未就绪 / 无同档任务时卷轴保留
                if (view.ConsumeAfterRead)
                {
                    ScrollItems.ConsumeOne(go);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.UI, e, "[MoreFood/Scroll] 阅读处理失败");
            }
            return false;   // 整体接管：不再走原版（否则会掉磁盘数据 + 走背景故事文案）
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ScrollReadPatch", e); return true; }
        }
    }

    [HarmonyPatch(typeof(LoreBearer), "get_SidescreenButtonText")]
    internal static class ScrollReadLabelPatch
    {
        private static void Postfix(LoreBearer __instance, ref string __result)
        {
            try
            {
                if (__instance != null && ScrollItems.TierOf(__instance.gameObject) != null)
                {
                    __result = "阅读";
                }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ScrollReadLabelPatch", e); }
        }
    }

    [HarmonyPatch(typeof(LoreBearer), "get_SidescreenButtonTooltip")]
    internal static class ScrollReadTipPatch
    {
        private static void Postfix(LoreBearer __instance, ref string __result)
        {
            try
            {
                if (__instance != null && ScrollItems.TierOf(__instance.gameObject) != null)
                {
                    __result = "阅读这张委托凭证（使用后消耗 1 张）";
                }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:ScrollReadTipPatch", e); }
        }
    }

}
