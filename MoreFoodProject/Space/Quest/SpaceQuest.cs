using System;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    internal sealed class QuestIconEntry
    {
        internal string category;
        internal string anim;
        internal string anim_state;
    }

    internal static class SpaceQuestIcons
    {
        internal static List<QuestIconEntry> List()
        {
            List<QuestIconEntry> list = new List<QuestIconEntry>();
            list.Add(new QuestIconEntry
            {
                category = "combat",
                anim = "gravitas_space_poi_kanim",
                anim_state = "station_2"
            });
            list.Add(new QuestIconEntry
            {
                category = "explore",
                anim = "gravitas_space_poi_kanim",
                anim_state = "station_1"
            });
            list.Add(new QuestIconEntry
            {
                category = "other",
                anim = SpaceCfg.QuestSiteAnim,
                anim_state = SpaceCfg.QuestSiteAnimState
            });
            return list;
        }

        internal static QuestIconEntry Resolve(string category)
        {
            List<QuestIconEntry> list = List();
            QuestIconEntry fallback = null;
            for (int i = 0; i < list.Count; i++)
            {
                QuestIconEntry e = list[i];
                if (e == null || string.IsNullOrEmpty(e.anim))
                {
                    continue;
                }
                if (e.category == "other")
                {
                    fallback = e;
                }
                if (!string.IsNullOrEmpty(category) && e.category == category)
                {
                    return e;
                }
            }
            if (fallback != null)
            {
                return fallback;
            }
            return list.Count > 0 ? list[0] : null;
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]   // ★ KSerialization OptOut 只认 public 字段，必须 OptIn
    public class SpaceQuestSite : ClusterGridEntity
    {
        [Serialize]
        internal string questId;

        [Serialize]
        internal string label;

        [Serialize]
        internal string icon;

        public override string Name
        {
            get { return label; }
        }

        public override EntityLayer Layer
        {
            get
            {
                // ★ 动态层（崩溃实锤：[0x20f] ldfld WorldContainer::id）：
                WorldContainer w = GetComponent<WorldContainer>();
                return w != null ? EntityLayer.Asteroid : EntityLayer.POI;
            }
        }

        public override List<ClusterGridEntity.AnimConfig> AnimConfigs
        {
            get
            {
                QuestIconEntry e = SpaceQuestIcons.Resolve(icon);
                string anim = e != null && !string.IsNullOrEmpty(e.anim) ? e.anim : SpaceCfg.QuestSiteAnim;
                string state = e != null && !string.IsNullOrEmpty(e.anim_state)
                    ? e.anim_state : SpaceCfg.QuestSiteAnimState;
                return new List<ClusterGridEntity.AnimConfig>
                {
                    new ClusterGridEntity.AnimConfig
                    {
                        animFile = Assets.GetAnim(anim),
                        initialAnim = state
                    }
                };
            }
        }

        public override bool IsVisible
        {
            get { return true; }
        }

        public override ClusterRevealLevel IsVisibleInFOW
        {
            // ★ 跟随迷雾：Visible=原版语义"无视迷雾永显"；Peeked=半揭示问号/全揭示实体
            get { return ClusterRevealLevel.Peeked; }
        }

        internal static GameObject CreatePrefab(string id, string displayName)
        {
            GameObject go = EntityTemplates.CreateEntity(id, displayName, true);
            SaveLoadRoot root = go.AddOrGet<SaveLoadRoot>();
            root.DeclareOptionalComponent<WorldInventory>();
            root.DeclareOptionalComponent<WorldContainer>();
            go.AddOrGetDef<AlertStateManager.Def>();   // 建任务地图要挂 WorldContainer，须预置 AlertStateManager（原生火箭同款）
            go.AddOrGet<SpaceQuestSite>();
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            return go;
        }
    }

    /// <summary>
    internal static class SpaceQuestWorldFlags
    {
        private static FieldInfo flagField;

        /// <summary>任务世界 ⇒ 把 isModuleInterior 改回 false，返回是否真的改了。</summary>
        internal static bool Demote(WorldContainer w)
        {
            try
            {
                if (w == null || w.GetComponent<SpaceQuestSite>() == null)
                {
                    return false;
                }
                if (w.GetComponent<Clustercraft>() != null)
                {
                    return false;   // 万一将来真挂了火箭，就不抢这个旗标
                }
                if (flagField == null)
                {
                    flagField = typeof(WorldContainer).GetField("isModuleInterior",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                if (flagField == null)
                {
                    MFLog.Warn(LogCat.Space,"[Space] 没找到 WorldContainer.isModuleInterior 字段，任务世界旗标未修正");
                    return false;
                }
                if (!(bool)flagField.GetValue(w))
                {
                    return false;
                }
                flagField.SetValue(w, false);
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc("quest:flag", LogCat.Space, e, "[Space] 修正任务世界 isModuleInterior 旗标失败（任务地图可能被当成主世界）");
                return false;
            }
        }

        /// <summary>读档兜底：旧档里任务世界旗标是 true，进场后必须全部扫回 false。</summary>
        internal static void NormalizeAll()
        {
            try
            {
                ClusterManager cm = ClusterManager.Instance;
                if (cm == null || cm.WorldContainers == null)
                {
                    return;
                }
                int n = 0;
                for (int i = 0; i < cm.WorldContainers.Count; i++)
                {
                    if (Demote(cm.WorldContainers[i]))
                    {
                        n++;
                    }
                }
                if (n > 0)
                {
                    MFLog.Info(LogCat.Space,"[Space] 读档修正 " + n + " 个任务地图世界的 isModuleInterior 旗标（防过天 NRE 崩溃）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc("quest:scanflag", LogCat.Space, e, "[Space] 扫描任务世界旗标失败（本次不修正，按原版继续）");
            }
        }
    }

    /// <summary>★ 2026-10-06：原版 PlaceInteriorTemplate 把模板根位置写死成"世界中心"，
    [HarmonyPatch(typeof(TemplateLoader), "Stamp", new Type[] { typeof(TemplateContainer), typeof(Vector2), typeof(System.Action) })]
    internal static class QuestStampRootPatch
    {
        private static void Prefix(ref Vector2 rootLocation)
        {
            try
            {
                if (SpaceQuestRuntime.HasPendingStampRoot)
                {
                    MFLog.Info(LogCat.Space,"[Space] 印模板根位置修正：" + rootLocation + " → " + SpaceQuestRuntime.PendingStampRoot
                        + "（任务地图贴世界左下角）");
                    rootLocation = SpaceQuestRuntime.PendingStampRoot;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc("quest:template", LogCat.Space, e, "[Space] 印模板根位置修正失败（按原版继续，可能出现偏移）");
            }
        }
    }

    internal sealed class QuestState
    {
        // ★ 任务 = 事件实例：instanceId 是存档主键，eventId 只用于日志
        internal string instanceId;
        internal string eventId;
        // 委托名（事件实例创建时冻结的变体名；读档靠存档里的副本，不重抽）
        internal string shownName;
        // 品质：common / good / legendary（决定任务档位与目标规格）
        internal string tier;
        // 狩猎规格快照（来自事件 yaml 的 hunt: 段）
        internal List<string> creatureTiers = new List<string>();
        internal int creatureCount = 1;
        internal int durationCycles = 8;
        internal int rewardRep = 12;
        // 任务刷新时按基础区间与游戏周期 K 冻结的最终星币；接取、完成和读档均不重掷
        internal int rewardCurrency;

        internal AxialI cell;
        internal string creatureId;
        internal float deadline;
        internal int spawnedAlive;
        internal GameObject site;    // 任务点实体（建图链停用后恒为 null，保留给其它功能）
        internal int worldId = -1;
        internal bool mapBuilt;
        // 身份证 —— 本任务下发过的星图生物 ticket（非空 = 只接收自己下发的死亡广播）
        internal readonly List<string> issuedTickets = new List<string>();
    }

    internal static class SpaceQuestRuntime
    {
        internal static readonly List<QuestState> Active = new List<QuestState>();
        private const string RewardCurrencyArg = "reward_currency";

        /// <summary>事件刷新时冻结最终星币；同一实例重复触发只读取原值。</summary>
        internal static int FreezeRewardAtRefresh(EventFramework.Distribution.Contracts.IEventContext ctx)
        {
            if (ctx == null) return 0;
            string existing = ctx.GetNoticeArg(RewardCurrencyArg, null);
            int frozen;
            if (!string.IsNullOrEmpty(existing) && int.TryParse(existing, out frozen) && frozen >= 0)
                return frozen;

            EventFramework.Distribution.Contracts.HuntSpec hunt = ctx.Hunt;
            int currencyMin = hunt != null ? Math.Max(0, hunt.reward_currency_min) : 0;
            int currencyMax = hunt != null ? Math.Max(currencyMin, hunt.reward_currency_max) : 0;
            frozen = 0;
            if (currencyMax > 0)
            {
                int baseReward = UnityEngine.Random.Range(currencyMin, currencyMax + 1);
                int cycle = GameClock.Instance != null ? Math.Max(1, GameClock.Instance.GetCycle()) : 1;
                double k = 1.0 + 0.3 * Math.Min(1.0, Math.Max(0.0, (cycle - 1) / 199.0));
                frozen = (int)Math.Floor(baseReward * k + 0.5);
            }
            ctx.SetNoticeArg(RewardCurrencyArg, frozen.ToString());
            return frozen;
        }

        // ★ 印模板根位置握手：BuildMap 设，TemplateLoader.Stamp 前缀消费（见 QuestStampRootPatch）
        internal static Vector2 PendingStampRoot;
        internal static bool HasPendingStampRoot;
        private static int published;

        /// <summary>
        /// 卷轴阅读 = 接取：直接触发同名事件并代选 accept，走与面板点击完全相同的路径
        /// （事件实例由框架建，文案变体与 hunt 规格都从这里来）。
        /// </summary>
        internal static bool StartFromScroll(MoreFood_KFC.ScrollReadView screen, string tier)
        {
            string evt = SpaceStarmapEvents.EventPrefix + tier;
            int activeBefore = Active.Count;
            bool ok = EventFramework.Distribution.Contracts.EventFrameworkApi.TriggerOption(evt, "accept", "scroll:" + tier);
            // start_fn 是 void 钩子：框架返回 true 不代表 Publish 成功，必须以任务实际入列为准。
            if (Active.Count <= activeBefore)
            {
                if (screen != null)
                {
                    screen.AddPlainText("协会暂无可派遣的同类委托（或这一档刚发过一份，稍后再用）。");
                    screen.ConsumeAfterRead = false;   // 没接成 ⇒ 卷轴留着可重试
                }
                MFLog.Warn(LogCat.Space, "[Space] 卷轴接取失败：事件 " + evt + " 未生成进行中任务（见上方框架/投放日志）");
                return false;
            }
            if (!ok)
            {
                MFLog.Warn(LogCat.Space, "[Space] 卷轴接取的框架返回值为失败，但任务已实际投放，按接取成功处理：" + evt);
            }
            if (screen != null)
            {
                screen.AddPlainText("委托已接取：目标已投放到星图，开火箭过去开打即可。");
            }
            return true;
        }

        /// <summary>按 hunt.creature_tiers 在战斗怪物表里等概率抽一只。</summary>
        private static CombatMonsterDef PickMonsterByTiers(List<string> tiers)
        {
            if (tiers == null || tiers.Count == 0 || CombatDb.Monsters.Count == 0)
            {
                return null;
            }
            List<CombatMonsterDef> pool = new List<CombatMonsterDef>();
            for (int i = 0; i < CombatDb.Monsters.Count; i++)
            {
                CombatMonsterDef m = CombatDb.Monsters[i];
                if (m == null || string.IsNullOrEmpty(m.tier))
                {
                    continue;
                }
                if (!tiers.Contains(m.tier))
                {
                    continue;
                }
                pool.Add(m);
            }
            if (pool.Count == 0)
            {
                return null;
            }
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        // 旧的本地定时发布器已被事件框架取代（RegisterHook → SpawnSite → Publish），本方法连同 quest_pool_max 一并移除

        // 协会开窗抽任务已移除：任务只由事件框架 / 任务卷轴两条入口产生，UI 不再参与判定

        /// <summary>
        /// 发布一条狩猎任务：数据全部来自事件实例（hunt 规格 + 冻结文案），本方法不再读任何任务表。
        /// </summary>
        internal static QuestState Publish(EventFramework.Distribution.Contracts.IEventContext ctx, string tier)
        {
            SpaceZones zones = StarMapVisuals.Zones;
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid != null && (zones == null || !zones.valid))
            {
                SpaceTestRuntime.RebuildZones();   // 接收早于星图分区构建时兜一把
                zones = StarMapVisuals.Zones;
            }
            if (ctx == null || ctx.Hunt == null || grid == null || zones == null || !zones.valid)
            {
                MFLog.Warn(LogCat.Space, "[Space] 星图未就绪或事件未带 hunt 规格，任务无法发布");
                return null;
            }
            EventFramework.Distribution.Contracts.HuntSpec hunt = ctx.Hunt;
            string instanceId = ctx.InstanceId;
            if (string.IsNullOrEmpty(instanceId))
            {
                MFLog.Warn(LogCat.Space, "[Space] 事件实例 id 为空，无法登记任务");
                return null;
            }
            // ★ 同实例去重兜底（同一事件重复 accept / 读档重复恢复）
            for (int i = 0; i < Active.Count; i++)
            {
                QuestState live = Active[i];
                if (live != null && live.instanceId == instanceId)
                {
                    MFLog.Info(LogCat.Space, "[Space] 任务实例 " + instanceId + " 已在跑 ⇒ Publish 跳过");
                    return null;
                }
            }

            AxialI cell = PickQuestCell(zones, grid, tier);
            if (cell.Q == int.MinValue)
            {
                MFLog.Warn(LogCat.Space, "[Space] 星图没有空位放任务目标（品质 " + tier + "）");
                return null;
            }

            CombatMonsterDef pickedMonster = PickMonsterByTiers(hunt.creature_tiers);
            if (pickedMonster == null)
            {
                MFLog.Warn(LogCat.Space, "[Space] 档位 [" + string.Join(",", hunt.creature_tiers.ToArray())
                    + "] 里没有可用的战斗怪物（检查 combat/monsters/*.yaml 的 tier 字段）");
                return null;
            }
            int spawnCount = hunt.creature_count < 1 ? 1 : hunt.creature_count;

            QuestState st = new QuestState();
            st.instanceId = instanceId;
            st.eventId = ctx.DefId;
            st.tier = tier;
            st.shownName = ctx.ShownName;
            st.creatureTiers = new List<string>(hunt.creature_tiers);
            st.creatureCount = spawnCount;
            st.durationCycles = hunt.duration_cycles < 1 ? 8 : hunt.duration_cycles;
            st.rewardRep = hunt.reward_rep;
            int frozenCurrency;
            string frozenReward = ctx.GetNoticeArg(RewardCurrencyArg, null);
            if (!int.TryParse(frozenReward, out frozenCurrency) || frozenCurrency < 0)
            {
                MFLog.Warn(LogCat.Space, "[Space] 事件实例缺少刷新时冻结的星币，拒绝接取且不重新抽取：" + instanceId);
                return null;
            }
            st.rewardCurrency = frozenCurrency;
            st.cell = cell;
            st.creatureId = pickedMonster.id;
            st.site = null;

            MFLog.Info(LogCat.Space, "[Space] 狩猎目标：" + pickedMonster.name + "（" + pickedMonster.id + "，"
                + pickedMonster.tier + " 档）×" + spawnCount);

            float now = TradeRegistry.Now();
            st.deadline = now + st.durationCycles * 600f;
            Active.Add(st);
            published++;
            MFLog.Info(LogCat.Space, "[Space] ★ 发布（星图任务）【" + st.shownName + "】实例=" + instanceId
                + " 事件=" + st.eventId + " 品质=" + tier + " 地点=" + SpaceCfg.Pos(cell)
                + " 目标=" + st.creatureId + "×" + spawnCount
                + " 期限=" + st.durationCycles + "周期 声望=" + st.rewardRep
                + " 星币=" + st.rewardCurrency);
            int spawned = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                AxialI mcell = NextFreeCell(grid, cell, i + 1);
                if (mcell.Q == int.MinValue)
                {
                    // ★ 2 环内已无空格：少投几只，而不是叠到别的实体身上
                    MFLog.Warn(LogCat.Space, "[Space] 目标格周边没有空位，本次只投放 " + spawned + "/" + spawnCount
                        + " 只（中心格 " + SpaceCfg.Pos(cell) + "）");
                    break;
                }
                StarMapVisuals.RevealCell(mcell, 0);   // ★ 目标格随委托一并揭雾（格子没到 Visible 时迷你条不显示）
                MonsterRuntime mt = CombatRuntime.SpawnMonster(st.creatureId, mcell, instanceId,
                    CombatRuntime.RegionOf(mcell));
                if (mt != null)
                {
                    // ★ 狩猎目标的离场闹钟 = 本任务失败时间 + 1s（= 接收/刷新目标时的持续时间）
                    mt.leaveAt = st.deadline + 1f;
                    st.issuedTickets.Add(mt.ticket);   // 身份证：记录本任务下发的怪
                    spawned++;
                }
            }
            // ★ 一只都没投出来（格子失效 / prefab 缺失）⇒ 撤回任务记录，避免 Tick 把"没有目标"判成"全部击杀"
            if (st.issuedTickets.Count == 0)
            {
                Active.Remove(st);
                MFLog.Warn(LogCat.Space, "[Space] 星图狩猎目标投放失败（0 只），任务已撤回：实例=" + instanceId
                    + " 目标=" + st.creatureId + " 格=" + SpaceCfg.Pos(cell));
                return null;
            }
            MFLog.Info(LogCat.Space, "[Space] 星图狩猎：目标已投放星图（不生成任务地点地图），直接派火箭开打");
            if (TradeSaveData.Attach() != null)
            {
                TradeSaveData.Attach()?.Capture();
            }
            return st;
        }

        /// <summary>
        /// 取投放格：传奇 → 危险区空格；普通/精良 → 主星图可见范围内任意空格
        /// （排除子图、危险区、已有实体）。都找不到 → 退回主图边缘格。
        /// </summary>
        private static AxialI PickQuestCell(SpaceZones zones, ClusterGrid grid, string tier)
        {
            if (tier == "legendary")
            {
                List<AxialI> free = new List<AxialI>();
                for (int i = 0; i < zones.dangerCells.Count; i++)
                {
                    AxialI c = zones.dangerCells[i];
                    if (Occupied(grid, c))
                    {
                        continue;
                    }
                    free.Add(c);
                }
                if (free.Count > 0)
                {
                    return free[UnityEngine.Random.Range(0, free.Count)];
                }
                MFLog.Warn(LogCat.Space, "[Space] 危险区没有空格，传奇委托改投主星图");
            }

            List<AxialI> main = new List<AxialI>();
            int edge = zones.visibleEdgeRing;
            for (int q = -edge; q <= edge; q++)
            {
                int rLo = Math.Max(-edge, -q - edge);
                int rHi = Math.Min(edge, -q + edge);
                for (int r = rLo; r <= rHi; r++)
                {
                    AxialI c = SpaceCfg.Mk(q, r);
                    if (SpaceCfg.Ring(c) > edge)
                    {
                        continue;
                    }
                    if (zones.IsSub(c) || zones.IsDanger(c))
                    {
                        continue;
                    }
                    if (!grid.IsValidCell(c) || Occupied(grid, c))
                    {
                        continue;
                    }
                    main.Add(c);
                }
            }
            if (main.Count > 0)
            {
                return main[UnityEngine.Random.Range(0, main.Count)];
            }

            MFLog.Warn(LogCat.Space, "[Space] 主星图没有空格，退回边缘格投放");
            List<AxialI> edgeCells = zones.mainEdgeCells;
            if (edgeCells != null && edgeCells.Count > 0)
            {
                // ★ 边缘格也要判占用：这里以前直接随机取，会落到星球/站点/打捞场的格子上
                List<AxialI> edgeFree = new List<AxialI>();
                for (int i = 0; i < edgeCells.Count; i++)
                {
                    AxialI c = edgeCells[i];
                    if (!grid.IsValidCell(c) || Occupied(grid, c))
                    {
                        continue;
                    }
                    edgeFree.Add(c);
                }
                if (edgeFree.Count > 0)
                {
                    return edgeFree[UnityEngine.Random.Range(0, edgeFree.Count)];
                }
                MFLog.Warn(LogCat.Space, "[Space] 主图边缘格也全被占用，本次不放任务目标");
            }
            return SpaceCfg.Mk(int.MinValue, int.MinValue);
        }

        /// <summary>
        /// 取任务目标的落点：优先紧邻中心格的空位，紧邻全占再退到 2 环。
        /// ★ 找不到就返回无效格（调用方跳过这一只），**绝不回落到已占用的中心格**
        /// —— 以前 `return center` 会把整窝怪叠在同一个格子上。
        /// </summary>
        private static AxialI NextFreeCell(ClusterGrid grid, AxialI center, int prefer)
        {
            SpaceZones z = StarMapVisuals.Zones;
            for (int radius = 1; radius <= 2; radius++)
            {
                List<AxialI> ring = new List<AxialI>();
                List<AxialI> all = AxialUtil.GetAllPointsWithinRadius(center, radius);
                if (all == null)
                {
                    break;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    AxialI c = all[i];
                    if (SpaceCfg.Distance(c, center) != radius)
                    {
                        continue;
                    }
                    if (!grid.IsValidCell(c) || Occupied(grid, c))
                    {
                        continue;
                    }
                    if (z != null && z.valid && (z.IsVoid(c) || z.IsSub(c)))
                    {
                        continue;   // 隐藏带 / 子图不落点（子图另有抽取器管）
                    }
                    ring.Add(c);
                }
                if (ring.Count > 0)
                {
                    int idx = ((prefer - radius) % ring.Count + ring.Count) % ring.Count;
                    return ring[idx];
                }
            }
            return SpaceCfg.Mk(int.MinValue, int.MinValue);
        }

        /// <summary>
        /// 格子上是否已有任何星图实体（严格口径）。
        /// ★ 曾经只认 `SpaceMonster` / `SpaceQuestSite`，于是目标会被丢到
        ///   星球、空间站、时空裂隙、打捞场、小行星带的格子上——视觉上直接叠在一起，
        ///   而且这些格子的迷你条被实体挡住点不中。原版 `ClusterGridEntity.OnSpawn`
        ///   是同步 `RegisterEntity` 的，所以 `cellContents` 一定是最新的，直接用。
        /// </summary>
        private static bool Occupied(ClusterGrid grid, AxialI cell)
        {
            List<ClusterGridEntity> list;
            if (grid == null || !grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            return list.Count > 0;
        }

        internal static float NextWakeAt()
        {
            float now = TradeRegistry.Now();
            float next = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                QuestState st = Active[i];
                if (st == null)
                {
                    continue;
                }
                if (st.deadline > now && st.deadline < next)
                {
                    next = st.deadline;
                }
            }
            return next;
        }

        internal static void Tick()
        {
            try
            {
            float now = TradeRegistry.Now();
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                QuestState st = Active[i];
                if (st == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }
                // 成功只由明确的击杀/捕获事件驱动；Tick 仅负责期限兜底
                if (now > st.deadline)
                {
                    Settle(st, false, "任务过期（" + st.durationCycles + " 周期未完成）");
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:SpaceQuest", e); }
        }

        private static int LiveMinions(int worldId)
        {
            List<MinionIdentity> list = Components.LiveMinionIdentities.GetWorldItems(worldId, false);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    n++;
                }
            }
            return n;
        }

        private static readonly Dictionary<string, List<GameObject>> SpawnedCreatures =
            new Dictionary<string, List<GameObject>>();

        private static int CountAlive(QuestState st)
        {
            List<GameObject> list;
            if (!SpawnedCreatures.TryGetValue(st.instanceId, out list))
            {
                return 0;
            }
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 战斗层广播订阅入口（SpaceCombatBus.OnResolved）。
        /// 全部目标在期限内击杀/捕获才成功；任一任务目标离场立即失败。
        /// </summary>
        internal static void OnMonsterResolved(MonsterResolvedEvent e)
        {
            if (e == null)
            {
                return;
            }
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                QuestState st = Active[i];
                if (st == null || st.instanceId != e.questId)
                {
                    continue;
                }
                // 身份证过滤：只接收本任务明确下发的目标，已结算任务的迟到事件自然找不到 Active
                if (!st.issuedTickets.Contains(e.ticket))
                {
                    return;
                }
                if (e.resolution == MonsterResolution.Escaped)
                {
                    Settle(st, false, "任务目标已离场");
                    return;
                }
                if (e.resolution != MonsterResolution.Killed && e.resolution != MonsterResolution.Captured)
                {
                    return;
                }
                if (!CombatRuntime.HasAliveQuestMonster(st.instanceId))
                {
                    string why = e.resolution == MonsterResolution.Killed
                        ? "星图生物已全部击杀" : "目标已被捕获（活体交付）";
                    Settle(st, true, why);
                }
                return;
            }
        }

        internal static void SendCraftTo(SpaceQuestSite site)
        {
            if (site == null)
            {
                return;
            }
            ClusterGrid grid = ClusterGrid.Instance;
            Clustercraft craft = null;
            if (grid != null)
            {
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
                {
                    if (kv.Value == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < kv.Value.Count; i++)
                    {
                        Clustercraft c = kv.Value[i] as Clustercraft;
                        if (c != null)
                        {
                            craft = c;
                            break;
                        }
                    }
                    if (craft != null)
                    {
                        break;
                    }
                }
            }
            if (craft == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 没有可派遣的火箭");
                TradeUi.Notify("无法前往", "星图上没有火箭");
                return;
            }
            ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
            if (selector != null)
            {
                selector.SetDestination(site.Location);
            }
            MFLog.Info(LogCat.Space,"[Space] 已把 " + craft.Name + " 的目的地设为任务地点 " + SpaceCfg.Pos(site.Location)
                + "（到站后自动建图）");
        }


        // 单次原子结算：先移出 Active，再发奖/通知/保存；重复或迟到事件不会再次进入
        private static void Settle(QuestState st, bool success, string why)
        {
            if (st == null)
            {
                return;
            }
            int index = Active.IndexOf(st);
            if (index < 0)
            {
                return;
            }
            float now = TradeRegistry.Now();
            if (success && now > st.deadline)
            {
                success = false;
                why = "任务过期（" + st.durationCycles + " 周期未完成）";
            }
            Active.RemoveAt(index);

            if (success)
            {
                int rep = st.rewardRep;
                int currency = Math.Max(0, st.rewardCurrency);
                TradeRegistry.AddRep(rep);
                if (currency > 0)
                {
                    TradeRegistry.AddCurrency(currency);
                }
                MFLog.Info(LogCat.Space,"[Space] ★ 任务完成【" + st.shownName + "】（" + st.instanceId + "）：" + why
                    + "，声望 +" + rep + "，星币 +" + currency);
                TradeUi.Notify("任务完成（星图任务）", "（星图任务）" + st.shownName + "：声望 +" + rep
                    + "，星币 +" + currency);
            }
            else
            {
                // 失败后本任务仍存活的目标在 1s 后离场；其后续事件因 Active 已移除而被忽略
                try
                {
                    CombatRuntime.OrderIssuedLeaving(st.issuedTickets, now + 1f);
                }
                catch (Exception e)
                {
                    MFLog.Exc("quest:leavenotify", LogCat.Space, e, "[Space] 任务失败的离场通知异常（任务状态已改，只是没发通知）");
                }
                MFLog.Warn(LogCat.Space,"[Space] 任务失败【" + st.shownName + "】（" + st.instanceId + "）：" + why + "（不计入击杀进度）");
                TradeUi.Notify("任务失败（星图任务）", "（星图任务）" + st.shownName + "：" + why);
            }
            DestroySite(st);
            TradeSaveData.Attach()?.Capture();
        }

        private static void DestroySite(QuestState st)
        {
            if (st == null)
            {
                return;
            }
            // ★ 先清任务地图世界：旧实现直接销毁 site（site 即世界宿主）会把被销毁的
            if (st.mapBuilt && st.worldId >= 0)
            {
                if (!DestroyQuestWorld(st.worldId))
                {
                    return;   // 世界里还有乘员：世界连 site 一起留到人员撤出后再清，别把人埋进废墟
                }
                st.worldId = -1;
                st.mapBuilt = false;
            }
            if (st.site != null)
            {
                // 宿主身上就挂着 WorldContainer 时也必须先走世界清理，否则建筑会连同世界一起变孤儿
                WorldContainer hostWorld = st.site.GetComponent<WorldContainer>();
                if (hostWorld != null && !DestroyQuestWorld(hostWorld.id))
                {
                    return;
                }
                Util.KDestroyGameObject(st.site);
                st.site = null;
            }
        }

        /// <summary>任务地图世界的正规清理（裁剪自 ClusterManager.DestoryRocketInteriorWorld，去掉火箭专属部分）。</summary>
        internal static bool DestroyQuestWorld(int worldId)
        {
            try
            {
                ClusterManager cm = ClusterManager.Instance;
                if (cm == null)
                {
                    return false;
                }
                WorldContainer w = cm.GetWorld(worldId);
                if (w == null)
                {
                    return true;   // 世界已不在：调用方可以照常收尾
                }
                var crew = Components.LiveMinionIdentities.GetWorldItems(w.id, false);
                if (crew != null && crew.Count > 0)
                {
                    // ★ 原版这里靠 EjectAllDupes 把人挪走，取消后仍挂在已销毁世界里 ⇒ 下次读档被销毁并连崩
                    MFLog.Warn(LogCat.Space, "[Space] 任务地图 World#" + worldId + " 里还有 " + crew.Count
                        + " 名复制人 ⇒ 本轮不销毁（等人员撤出后再清）");
                    return false;
                }
                if (cm.activeWorldId == worldId)
                {
                    WorldContainer start = cm.GetStartWorld();
                    if (start != null)
                    {
                        cm.SetActiveWorld(start.id);
                    }
                }
                try
                {
                    w.CancelChores();
                }
                catch (Exception e)
                {
                    MFLog.Exc("quest:cancelchore", LogCat.Space, e, "[Space] 任务地图取消差事失败（继续清理，可能残留一个差事）");
                }
                try
                {
                    HashSet<int> noRefund = new HashSet<int>();
                    w.DestroyWorldBuildings(out noRefund);
                }
                catch (Exception e)
                {
                    MFLog.Exc("quest:clearbuild", LogCat.Space, e, "[Space] 清理任务地图建筑失败（继续，可能残留建筑）");
                }
                cm.UnregisterWorldContainer(w);   // ★ 崩溃根因：这一步以前缺了
                try
                {
                    Grid.FreeGridSpace(w.WorldSize, w.WorldOffset);
                }
                catch (Exception e)
                {
                    MFLog.Exc("quest:releasemesh", LogCat.Space, e, "[Space] 释放任务地图网格空间失败（继续，世界可能残留占用）");
                }
                try
                {
                    WorldInventory inv = w.GetComponent<WorldInventory>();
                    if (inv != null)
                    {
                        UnityEngine.Object.Destroy(inv);
                    }
                }
                catch (Exception)
                {
                    // expected: WorldInventory 销毁失败时随容器一并回收，世界对象仍在下一行销毁
                }
                UnityEngine.Object.Destroy(w);
                try
                {
                    cm.WorldContainers.Remove(w);   // 兜底：任何路径都保证列表不残留
                }
                catch (Exception)
                {
                    // expected: 兜底移除失败时前面已注销世界容器，列表不会因这一项卡死清理
                }
                MFLog.Info(LogCat.Space,"[Space] 任务地图 world#" + worldId + " 已清理（注销 + 释放网格 + 销毁容器）");
                return true;
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Space,"[Space] 清理任务地图世界失败: " + e);
                return false;
            }
        }

        /// <summary>★★ 2026-10-06：清理"孤儿任务点"。任务点实体是随档保存的（未 SetRegistered(false)），
        internal static void CleanupOrphanSites()
        {
            try
            {
                var sites = UnityEngine.Resources.FindObjectsOfTypeAll<SpaceQuestSite>();
                int killed = 0;
                for (int i = 0; i < sites.Length; i++)
                {
                    SpaceQuestSite site = sites[i];
                    if (site == null || site.gameObject == null || !site.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    // ★★ 两条硬性护栏（血泪）：
                    if (!site.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    bool live = false;
                    for (int k = 0; k < Active.Count; k++)
                    {
                        if (Active[k] != null && Active[k].instanceId == site.questId)
                        {
                            live = true;
                            break;
                        }
                    }
                    if (live)
                    {
                        continue;
                    }
                    WorldContainer wc = site.GetComponent<WorldContainer>();
                    if (wc != null)
                    {
                        int minions = Components.LiveMinionIdentities.GetWorldItems(wc.id, false).Count;
                        if (minions > 0)
                        {
                            MFLog.Warn(LogCat.Space,"[Space] 孤儿任务点 World#" + wc.id + " 里还有 " + minions
                                + " 个复制人 ⇒ 本轮不销毁（防止 ChoreConsumer 悬空崩游戏）；等人员撤出后下次读档再清");
                            continue;
                        }
                    }
                    MFLog.Warn(LogCat.Space,"[Space] 清理孤儿任务点：label=" + site.Name + " 任务=" + site.questId
                        + " 有WorldContainer=" + (wc != null) + (wc != null ? " worldId=" + wc.id : ""));
                    if (wc != null)
                    {
                        if (!DestroyQuestWorld(wc.id))
                        {
                            continue;   // 世界没清掉就别动宿主体，否则世界会失去容器变成孤儿网格
                        }
                    }
                    Util.KDestroyGameObject(site.gameObject);
                    killed++;
                }
                if (killed > 0)
                {
                    MFLog.Info(LogCat.Space,"[Space] 孤儿任务点已清理 " + killed + " 个");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc("quest:orphan", LogCat.Space, e, "[Space] 清理孤儿任务点失败（下次读档会再试）");
            }
        }

        /// <summary>★ 2026-10-06 深夜新增：按 questId 找【随档回来的现有任务点】，
        private static SpaceQuestSite FindExistingQuestSite(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return null;
            }
            var sites = Resources.FindObjectsOfTypeAll<SpaceQuestSite>();
            for (int i = 0; i < sites.Length; i++)
            {
                SpaceQuestSite site = sites[i];
                if (site == null || site.gameObject == null)
                {
                    continue;
                }
                if (!site.gameObject.scene.IsValid() || !site.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (site.questId == questId)
                {
                    return site;
                }
            }
            return null;
        }

        /// <summary>★ 2026-10-06 深夜新增：星图狩猎没有任务地图 ⇒ 不应有任务点实体。
        private static void CleanupStarMapHuntSites(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return;
            }
            var sites = Resources.FindObjectsOfTypeAll<SpaceQuestSite>();
            for (int i = 0; i < sites.Length; i++)
            {
                SpaceQuestSite site = sites[i];
                if (site == null || site.gameObject == null)
                {
                    continue;
                }
                if (!site.gameObject.scene.IsValid() || !site.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (site.questId != questId)
                {
                    continue;
                }
                // 带 WorldContainer 的 = 地点狩猎，别误杀
                if (site.GetComponent<WorldContainer>() != null)
                {
                    continue;
                }
                MFLog.Warn(LogCat.Space,"[Space] 清理星图狩猎孤儿任务点：" + site.Name + " 任务=" + site.questId);
                Util.KDestroyGameObject(site.gameObject);
            }
        }

        // 地点任务（建图）已停用：不再响应"火箭到达任务格→建图"，BuildMap 仅保留方法体供其它功能复用

        // [保留·暂无调用方] 在 Grid 开辟场地并盖章地图模板：尺寸/模板由调用方传入，不再读 trade/config.yaml
        internal static void BuildMap(QuestState st, bool focus, int width, int height, string template)
        {
            if (st == null || st.site == null)
            {
                return;
            }
            GameObject host = st.site;
            Vector2I size = new Vector2I(Mathf.Max(8, width), Mathf.Max(8, height));
            Vector2I offset;
            if (!Grid.GetFreeGridSpace(size, out offset))
            {
                MFLog.Warn(LogCat.Space,"[Space] Grid 里没有空间放任务地图（需要 " + size.X + "×" + size.Y + "）");
                return;
            }
            int id = NextWorldId();
            if (id < 0)
            {
                MFLog.Warn(LogCat.Space,"[Space] 取不到新的世界 id，任务地图放弃");
                return;
            }
            host.AddOrGet<WorldInventory>();
            WorldContainer w = host.AddOrGet<WorldContainer>();
            w.SetRocketInteriorWorldDetails(id, size, offset);
            SpaceQuestWorldFlags.Demote(w);   // ★ 不冒充火箭内舱（原版每日结算会因此 NRE 崩游戏，见该类注释）
            Vector2I end = offset + size;
            for (int y = offset.Y; y < end.Y; y++)
            {
                for (int x = offset.X; x < end.X; x++)
                {
                    int cell = Grid.XYToCell(x, y);
                    if (!Grid.IsValidCell(cell))
                    {
                        continue;
                    }
                    Grid.WorldIdx[cell] = (byte)id;
                    Pathfinding.Instance.AddDirtyNavGridCell(cell);
                }
            }
            st.worldId = id;
            st.mapBuilt = true;
            if (string.IsNullOrEmpty(template) || !SpaceTemplates.Ensure(template))
            {
                MFLog.Warn(LogCat.Space,"[Space] map 模板不可用（" + template + "）→ 改用原版 habitat_small");
                template = "expansion1::interiors/habitat_small";
                SpaceTemplates.Ensure(template);
            }
            try
            {
                // ★ 原版 PlaceInteriorTemplate 把模板根位置硬编码成"世界中心"，
                int tplW = 0;
                TemplateContainer tplInfo = TemplateCache.GetTemplate(template);
                if (tplInfo != null)
                {
                    tplW = Mathf.RoundToInt(tplInfo.info.size.X);
                }
                int stampX = w.WorldOffset.x + (tplW > 0 ? Mathf.Max(0, (size.X - tplW) / 2) : 0);
                PendingStampRoot = new Vector2(stampX, w.WorldOffset.y);
                HasPendingStampRoot = true;
                w.PlaceInteriorTemplate(template, delegate
                {
                    try
                    {
                        SpawnQuestCreatures(st, w);
                        if (focus)
                        {
                            GameUtil.FocusCameraOnWorld(id, w.GetSafeCell() >= 0 ? Grid.CellToPosCCC(w.GetSafeCell(), Grid.SceneLayer.Move) : Vector3.zero,
                                12f, null, true);
                        }
                        MFLog.Info(LogCat.Space,"[Space] ★ 任务地图已生成：world#" + id + " 尺寸 " + size.X + "×" + size.Y
                            + " 模板=" + template + "（岩石地形为模板预设，随机 = 模板抽签）");
                    }
                    catch (Exception e2)
                    {
                        MFLog.Warn(LogCat.Space,"[Space] 任务地图完成回调异常: " + e2);
                    }
                });
            }
            catch (Exception e)
            {
                // 历史 NRE 就发生在这里（同步段），全栈打出来便于定位；不得让它打断 Publish
                MFLog.Warn(LogCat.Space,"[Space] PlaceInteriorTemplate 同步段异常（任务地图可能未生成）: " + e);
            }
            finally
            {
                HasPendingStampRoot = false;
            }
        }

        // 模板选择随建图链移除：模板 id 改由 BuildMap 的调用方直接传入

        private static void SpawnQuestCreatures(QuestState st, WorldContainer w)
        {
            if (w == null || st == null || string.IsNullOrEmpty(st.creatureId))
            {
                return;
            }
            GameObject prefab = null;
            try
            {
                prefab = Assets.TryGetPrefab(st.creatureId);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 取生物 prefab 失败");
            }
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Space,"[Space] 找不到生物 prefab " + st.creatureId
                    + "（改 combat/monsters/*.yaml 的 id）");
                return;
            }
            List<GameObject> list = new List<GameObject>();
            int placed = 0;
            for (int i = 0; i < st.creatureCount; i++)
            {
                int cell = FindFreeCellInWorld(w, i);
                if (!Grid.IsValidCell(cell))
                {
                    break;
                }
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    continue;
                }
                go.transform.SetPosition(Grid.CellToPosCCC(cell, Grid.SceneLayer.Creatures));
                go.SetActive(true);
                list.Add(go);
                placed++;
            }
            SpawnedCreatures[st.instanceId] = list;
            st.spawnedAlive = placed;
            MFLog.Info(LogCat.Space,"[Space] 任务地图投放生物 " + st.creatureId + " ×" + placed
                + "（任务标记要求 " + st.creatureCount + "）");
        }

        private static int FindFreeCellInWorld(WorldContainer w, int salt)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int x = w.WorldOffset.X + 2 + UnityEngine.Random.Range(0, Mathf.Max(1, w.WorldSize.X - 4));
                int y = w.WorldOffset.Y + 2 + UnityEngine.Random.Range(0, Mathf.Max(1, w.WorldSize.Y - 4));
                int cell = Grid.XYToCell(x, y);
                if (!Grid.IsValidCell(cell))
                {
                    continue;
                }
                if (Grid.WorldIdx[cell] != (byte)w.id)
                {
                    continue;
                }
                if (!Grid.IsSolidCell(cell) && !Grid.IsLiquid(cell))
                {
                    return cell;
                }
            }
            return w.GetSafeCell();
        }

        private static int NextWorldId()
        {
            try
            {
                MethodInfo m = typeof(ClusterManager).GetMethod("GetNextWorldId",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (m != null)
                {
                    object v = m.Invoke(ClusterManager.Instance, null);
                    if (v is int)
                    {
                        return (int)v;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] GetNextWorldId 反射失败");
            }
            int max = 0;
            List<WorldContainer> all = ClusterManager.Instance != null ? ClusterManager.Instance.WorldContainers : null;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].id > max)
                    {
                        max = all[i].id;
                    }
                }
            }
            return max + 1;
        }

        internal static void Capture(TradeSaveData save)
        {
            save.questActive.Clear();
            save.questSiteCellQ.Clear();
            save.questSiteCellR.Clear();
            save.questCreature.Clear();
            save.questDeadline.Clear();
            save.questEvent.Clear();
            save.questTier.Clear();
            save.questShown.Clear();
            save.questRep.Clear();
            if (save.questCurrency == null) save.questCurrency = new List<int>();
            save.questCurrency.Clear();
            save.questCycles.Clear();
            save.questCount.Clear();
            save.questTiers.Clear();
            save.questTicketQuestId.Clear();
            save.questTickets.Clear();
            for (int i = 0; i < Active.Count; i++)
            {
                QuestState st = Active[i];
                if (st == null || string.IsNullOrEmpty(st.instanceId))
                {
                    continue;
                }
                save.questActive.Add(st.instanceId);
                save.questSiteCellQ.Add(st.cell.Q);
                save.questSiteCellR.Add(st.cell.R);
                save.questCreature.Add(st.creatureId);
                save.questDeadline.Add(st.deadline);
                save.questEvent.Add(st.eventId);
                save.questTier.Add(st.tier);
                save.questShown.Add(st.shownName);
                save.questRep.Add(st.rewardRep);
                save.questCurrency.Add(st.rewardCurrency);
                save.questCycles.Add(st.durationCycles);
                save.questCount.Add(st.creatureCount);
                save.questTiers.Add(st.creatureTiers != null ? string.Join("|", st.creatureTiers.ToArray()) : "");
                for (int t = 0; t < st.issuedTickets.Count; t++)
                {
                    save.questTicketQuestId.Add(st.instanceId);
                    save.questTickets.Add(st.issuedTickets[t]);
                }
            }
        }

        /// <summary>
        /// 读档恢复进行中的任务：全部字段取自存档（不依赖事件实例是否还在 —— 事件 30 秒就结束了）。
        /// </summary>
        internal static int Restore(TradeSaveData save)
        {
            SpaceStarmapEvents.FreezePendingRewardsAfterRestore();
            Active.Clear();
            if (save == null || save.questActive == null)
            {
                return 0;
            }
            ClusterGrid grid = ClusterGrid.Instance;
            int kept = 0;
            for (int i = 0; i < save.questActive.Count; i++)
            {
                string instanceId = save.questActive[i];
                AxialI cell = SpaceCfg.Mk(
                    i < save.questSiteCellQ.Count ? save.questSiteCellQ[i] : 0,
                    i < save.questSiteCellR.Count ? save.questSiteCellR[i] : 0);
                if (string.IsNullOrEmpty(instanceId) || grid == null || !grid.IsValidCell(cell))
                {
                    MFLog.Warn(LogCat.Space,"[Space] 丢弃无效任务记录 " + instanceId);
                    continue;
                }
                bool already = false;
                for (int k = 0; k < Active.Count; k++)
                {
                    if (Active[k] != null && Active[k].instanceId == instanceId)
                    {
                        already = true;
                        break;
                    }
                }
                if (already)
                {
                    MFLog.Warn(LogCat.Space,"[Space] 读档去重：任务实例 " + instanceId + " 已有在跑记录 ⇒ 丢弃这一条重复存档");
                    continue;
                }
                QuestState st = new QuestState();
                st.instanceId = instanceId;
                st.eventId = i < save.questEvent.Count ? save.questEvent[i] : null;
                st.tier = i < save.questTier.Count ? save.questTier[i] : "common";
                st.shownName = i < save.questShown.Count ? save.questShown[i] : st.eventId;
                st.rewardRep = i < save.questRep.Count ? save.questRep[i] : 12;
                st.rewardCurrency = save.questCurrency != null && i < save.questCurrency.Count
                    ? Math.Max(0, save.questCurrency[i]) : 0;
                st.durationCycles = i < save.questCycles.Count ? save.questCycles[i] : 8;
                st.creatureCount = i < save.questCount.Count ? save.questCount[i] : 1;
                string tiersRaw = i < save.questTiers.Count ? save.questTiers[i] : null;
                st.creatureTiers = new List<string>();
                if (!string.IsNullOrEmpty(tiersRaw))
                {
                    st.creatureTiers.AddRange(tiersRaw.Split('|'));
                }
                st.cell = cell;
                st.creatureId = i < save.questCreature.Count ? save.questCreature[i] : null;
                st.deadline = i < save.questDeadline.Count ? save.questDeadline[i] : TradeRegistry.Now() + 600f;
                st.worldId = -1;
                st.mapBuilt = false;
                // ★ 星图狩猎没有任务地图 ⇒ 不建 site，顺手清掉同实例 id 的孤儿任务点
                CleanupStarMapHuntSites(instanceId);
                st.site = null;
                Active.Add(st);
                kept++;
            }
            // 恢复任务下发的怪身份证（按任务实例 id 对回）
            if (save.questTicketQuestId != null && save.questTickets != null)
            {
                int n = Mathf.Min(save.questTicketQuestId.Count, save.questTickets.Count);
                for (int k = 0; k < n; k++)
                {
                    string qid = save.questTicketQuestId[k];
                    string ticket = save.questTickets[k];
                    if (string.IsNullOrEmpty(qid) || string.IsNullOrEmpty(ticket))
                    {
                        continue;
                    }
                    for (int j = 0; j < Active.Count; j++)
                    {
                        if (Active[j].instanceId == qid)
                        {
                            Active[j].issuedTickets.Add(ticket);
                            break;
                        }
                    }
                }
            }
            if (kept > 0)
            {
                MFLog.Info(LogCat.Space,"[Space] 恢复进行中的任务 " + kept + " 个");
            }
            return kept;
        }
    }
}
