using System;
using EventFramework.Distribution.Contracts;
using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC
{
    internal static class SpaceStarmapEvents
    {
        internal const string HandlerKey = "starmap_quest";
        internal const string EventPrefix = "starmap_quest_";

        private static bool handlersRegistered;

        internal static void RegisterHandlers()
        {
            if (handlersRegistered)
            {
                return;
            }
            handlersRegistered = true;
            try
            {
                EventFrameworkApi.RegisterHandler(HandlerKey, new EventFramework.Distribution.Contracts.EventHandler
                {
                    onTrigger = OnQuestTrigger,
                    onStart = OnQuestStart,
                    onEnd = OnQuestEnd
                });

                for (int i = 0; i < SpaceCfg.ScrollTiers.Length; i++)
                {
                    string tier = SpaceCfg.ScrollTiers[i];
                    EventFrameworkApi.RegisterHook(HookKey(tier), MakeSpawner(tier));        // 选项「接收任务」：生成任务地点
                    EventFrameworkApi.RegisterHook(DeclineKey(tier), MakeDecliner(tier));    // 选项「拒绝任务」：只记录，不生成
                    ScrollItems.RegisterReader(tier, MakeScrollReader(tier));
                }
                SpaceSatelliteCrashEvent.RegisterHandler();
                SpaceRealEvents.RegisterHandlers();
                SpacePlanEvents.RegisterHandlers();   // 计划事件不随测试面板停用而消失
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 注册事件处理器失败");
            }
        }

        internal static string HookKey(string tier)
        {
            return "starmap.spawn_site." + tier;
        }

        internal static string DeclineKey(string tier)
        {
            return "starmap.decline." + tier;
        }

        private static Action<IEventContext> MakeSpawner(string tier)
        {
            string captured = tier;
            return delegate(IEventContext ctx)
            {
                SpawnSite(captured, ctx);
            };
        }

        private static Action<IEventContext> MakeDecliner(string tier)
        {
            string captured = tier;
            return delegate(IEventContext inst)
            {
                MFLog.Info(LogCat.Event,"[Space] 玩家拒绝了星图任务（品质 " + captured + "）");
                TradeUi.Notify("星图任务", "已拒绝（" + TierName(captured) + "）委托，星图上不会投放目标");
            };
        }

        internal static string TierName(string tier)
        {
            if (tier == "common")
            {
                return "普通";
            }
            if (tier == "good")
            {
                return "精良";
            }
            if (tier == "legendary")
            {
                return "传奇";
            }
            return string.IsNullOrEmpty(tier) ? "未知" : tier;
        }

        private static Action<ScrollReadView> MakeScrollReader(string tier)
        {
            string captured = tier;
            return delegate(ScrollReadView screen)
            {
                // 卷轴是玩家主动消耗的委托凭证 ⇒ 阅读即接取：
                // 触发同名事件并代选 accept，走与面板点击完全相同的路径（文案变体 / hunt 规格同一来源）。
                SpaceQuestRuntime.StartFromScroll(screen, captured);
            };
        }

        private static void SpawnSite(string tier, IEventContext ctx)
        {
            try
            {
                QuestState st = SpaceQuestRuntime.Publish(ctx, tier);
                if (st == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 框架下发星图任务（品质 " + tier + "）但发布失败（星图未就绪 / 无可用怪物 / 无空格）");
                    // ★ 面板已随"接收"关闭，这里必须给玩家一句反馈，否则点完像什么都没发生
                    TradeUi.Notify("星图任务", "接取未生效：协会眼下派不出这一档的目标，稍后再来问。");
                    return;
                }
                TradeUi.Notify("星图任务", "已接收【" + st.shownName + "】，目标已投放到星图");
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Event,"[Space] 生成任务地点失败: " + e);   // 全栈（历史版本只打了 Message，定位困难）
            }
        }

        private static void OnQuestTrigger(IEventContext inst)
        {
            int rewardCurrency = SpaceQuestRuntime.FreezeRewardAtRefresh(inst);
            MFLog.Info(LogCat.Event,"[Space] 星图任务事件已刷新并冻结星币（handler=" + HandlerKey
                + "，星币=" + rewardCurrency + "）");
        }

        /// <summary>旧版未接取事件没有冻结参数，恢复时补写一次；新版实例因已有值不会重抽。</summary>
        internal static void FreezePendingRewardsAfterRestore()
        {
            IEventContext[] contexts = EventFrameworkApi.ActiveContexts();
            for (int i = 0; i < contexts.Length; i++)
            {
                IEventContext ctx = contexts[i];
                if (ctx != null && !string.IsNullOrEmpty(ctx.DefId) && ctx.DefId.StartsWith(EventPrefix))
                    SpaceQuestRuntime.FreezeRewardAtRefresh(ctx);
            }
        }

        private static void OnQuestStart(IEventContext inst)
        {
            MFLog.Info(LogCat.Event,"[Space] 星图任务事件开始（框架）");
        }

        private static void OnQuestEnd(IEventContext inst)
        {
            MFLog.Info(LogCat.Event,"[Space] 星图任务事件结束（框架）");
        }

    }

    internal static class SpaceSatelliteCrashEvent
    {
        internal const string EventId = "satellite_crash";
        internal const string HandlerKey = "space_satellite_crash";
        internal const string CometId = "SpaceSatelliteComet";

        private static GameObject cometTemplate;
        private static bool cometTemplateTried;

        // 退档清理：DontDestroyOnLoad 的坠落体模板跨会话滞留，下次触发按需重建
        internal static void DestroyTemplates()
        {
            try
            {
                if (cometTemplate != null)
                {
                    UnityEngine.Object.Destroy(cometTemplate);
                }
            }
            catch (Exception)
            {
                // expected: 模板对象可能已销毁，销毁失败仍清空引用，下次触发按需重建
            }
            cometTemplate = null;
            cometTemplateTried = false;
        }

        internal static void RegisterHandler()
        {
            try
            {
                EventFrameworkApi.RegisterHandler(HandlerKey, new EventFramework.Distribution.Contracts.EventHandler
                {
                    onTrigger = delegate(IEventContext inst)
                    {
                        CrashNow("事件触发");
                    },
                });
                // 前置条件统一走 yaml（condition.function），此处注册函数本体
                EventFrameworkApi.RegisterCondition("satellite_crash",
                    delegate(IEventContext inst) { return ActiveWorld() != null; }, new string[0]);
                MFLog.Info(LogCat.Event,"[Space] 卫星坠毁处理器已注册：handler=" + HandlerKey);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 卫星坠毁处理器注册失败");
            }
        }

        internal static void CrashNow(string why)
        {
            Crash(why);
        }

        private static WorldContainer ActiveWorld()
        {
            return SpaceRealEventRuntime.MainWorld();
        }

        private static int BaseColumn(WorldContainer w)
        {
            try
            {
                if (Components.Telepads == null || Components.Telepads.Count == 0)
                {
                    return -1;
                }
                Telepad pad = null;
                foreach (Telepad t in Components.Telepads)
                {
                    pad = t;
                    break;
                }
                if (pad == null)
                {
                    return -1;
                }
                int cell = Grid.PosToCell(pad);
                int x;
                int y;
                Grid.CellToXY(cell, out x, out y);
                return Grid.WorldIdx[cell] == w.id ? x : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static void Crash(string why)
        {
            try
            {
                WorldContainer w = ActiveWorld();
                if (w == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 卫星坠毁：没有活动世界，跳过");
                    return;
                }
                int x = w.WorldOffset.x + UnityEngine.Random.Range(6, Mathf.Max(7, w.Width - 6));
                int top = w.WorldOffset.y + w.Height - 2;
                int ground = top;
                for (int y = top; y >= w.WorldOffset.y; y--)
                {
                    int c = Grid.XYToCell(x, y);
                    if (Grid.IsValidCell(c) && Grid.Solid[c])
                    {
                        ground = c;
                        break;
                    }
                }
                if (!Grid.IsValidCell(ground))
                {
                    MFLog.Warn(LogCat.Event,"[Space] 卫星坠毁：找不到有效落点（x=" + x + "）");
                    return;
                }
                GameObject prefab = FindCometPrefab();
                if (prefab == null)
                {
                    SpawnCraterOnly(ground);
                    return;
                }
                GameObject go = Util.KInstantiate(prefab, Grid.CellToPos(ground));
                Comet comet = go != null ? go.GetComponent<Comet>() : null;
                if (comet != null)
                {
                    comet.spawnWithOffset = true;
                    comet.Velocity = new Vector2(UnityEngine.Random.Range(-2.5f, 2.5f), -16f);
                }
                if (go != null)
                {
                    go.SetActive(true);
                }
                MFLog.Info(LogCat.Event,"[Space] ★ 卫星坠毁（" + why + "）：落点格 " + ground
                    + "，坠落体 " + (prefab != null ? prefab.name : "?"));
                Notify();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 卫星坠毁执行失败");
            }
        }

        private static void SpawnCraterOnly(int ground)
        {
            string[] ids = { "PropSurfaceSatellite1", "PropSurfaceSatellite2", "PropSurfaceSatellite3" };
            string id = ids[UnityEngine.Random.Range(0, ids.Length)];
            GameObject prefab = Assets.TryGetPrefab(new Tag(id));
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Event,"[Space] 卫星坠毁：坠落体与残骸 prefab 都不存在（" + id + "）");
                return;
            }
            GameObject go = Util.KInstantiate(prefab, Grid.CellToPos(ground));
            if (go != null)
            {
                go.SetActive(true);
            }
            MFLog.Info(LogCat.Event,"[Space] ★ 卫星坠毁（无坠落体，直接生成残骸 " + id + "，格 " + ground + "）");
            Notify();
        }

        private static GameObject FindCometPrefab()
        {
            GameObject custom = BuildCometTemplate();
            if (custom != null)
            {
                return custom;
            }
            string[] ids = { "SatelliteCometComet", "SatelliteComet" };
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject go = Assets.TryGetPrefab(new Tag(ids[i]));
                if (go != null)
                {
                    return go;
                }
            }
            return null;
        }

        private static GameObject BuildCometTemplate()
        {
            if (cometTemplate != null)
            {
                return cometTemplate;
            }
            if (cometTemplateTried)
            {
                return null;
            }
            cometTemplateTried = true;
            try
            {
                GameObject go = EntityTemplates.CreateEntity(CometId, "失事卫星", true);
                go.AddOrGet<SaveLoadRoot>();
                go.AddOrGet<LoopingSounds>();
                Comet comet = go.AddOrGet<Comet>();
                comet.massRange = new Vector2(100f, 200f);
                comet.EXHAUST_ELEMENT = SimHashes.AluminumGas;
                comet.temperatureRange = new Vector2(473.15f, 573.15f);
                comet.entityDamage = 2;
                comet.explosionOreCount = new Vector2I(8, 8);
                comet.totalTileDamage = 2f;
                comet.splashRadius = 1;
                comet.impactSound = "Meteor_Large_Impact";
                comet.flyingSoundID = 1;
                comet.explosionEffectHash = SpawnFXHashes.MeteorImpactDust;
                comet.addTiles = 0;
                comet.craterPrefabs = new string[]
                {
                    "PropSurfaceSatellite1", "PropSurfaceSatellite2", "PropSurfaceSatellite3"
                };
                PrimaryElement pe = go.AddOrGet<PrimaryElement>();
                pe.SetElement(SimHashes.Aluminum, true);
                pe.Temperature = (comet.temperatureRange.x + comet.temperatureRange.y) / 2f;
                KBatchedAnimController kbac = go.AddOrGet<KBatchedAnimController>();
                kbac.AnimFiles = new KAnimFile[] { Assets.GetAnim("meteor_rock_kanim") };
                kbac.isMovable = true;
                kbac.initialAnim = "fall_loop";
                kbac.initialMode = KAnim.PlayMode.Loop;
                kbac.visibilityType = KAnimControllerBase.VisibilityType.OffscreenUpdate;
                go.AddOrGet<KCircleCollider2D>().radius = 0.5f;
                go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
                go.AddTag(GameTags.Comet);
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                cometTemplate = go;
                MFLog.Info(LogCat.Event,"[Space] 卫星坠毁：已自建坠落体模板 " + CometId);
                return cometTemplate;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 卫星坠毁：自建坠落体失败");
                return null;
            }
        }

        private static void Notify()
        {
            try
            {
                TradeUi.Notify("卫星坠毁", "一颗失事卫星正朝殖民地坠落！", null, "blue");
            }
            catch (Exception)
            {
                // expected: 提示弹窗失败时坠落事件照常触发，仅少一条文字通知
            }
        }
    }

    [HarmonyPatch(typeof(MissileLauncher.Instance), "StartSM")]
    internal static class SpaceSatelliteMissileFilterPatch
    {
        private static void Postfix(MissileLauncher.Instance __instance)
        {
            try
            {
                FlatTagFilterable filter = __instance.TargetFilter;
                if (filter == null)
                {
                    return;
                }
                Tag tag = new Tag(SpaceSatelliteCrashEvent.CometId);
                if (!filter.tagOptions.Contains(tag))
                {
                    filter.tagOptions.Add(tag);
                }
                if (!filter.selectedTags.Contains(tag))
                {
                    filter.selectedTags.Add(tag);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 卫星坠落体加入导弹拦截清单失败");
            }
        }
    }
}
