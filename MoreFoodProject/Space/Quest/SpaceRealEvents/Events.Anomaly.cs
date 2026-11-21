// 用途：异常类事件：漏气、振荡器、地质、大灭绝与灾星

using System;
using EventFramework.Distribution.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSerialization;
using Klei.AI;
using Klei.AI.DiseaseGrowthRules;
using STRINGS;
using TUNING;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC
{
    internal static partial class SpaceRealEvents
    {
        private static void GasLeak(IEventContext inst)
        {
            try
            {
                int worldId = SpaceRealEventRuntime.MainWorldId();
                List<GeyserEntry> entries = CollectGeysers(worldId);
                List<Geyser> gasLive = new List<Geyser>();
                List<GeyserEntry> gasPending = new List<GeyserEntry>();
                for (int i = 0; i < entries.Count; i++)
                {
                    GeyserEntry e = entries[i];
                    bool isGas = (e.live != null)
                        ? IsGasGeyser(e.live)
                        : (e.spawn != null && IsGasSpawnable(e.spawn.spawnInfo.id));
                    if (!isGas)
                    {
                        continue;
                    }
                    if (e.live != null)
                    {
                        gasLive.Add(e.live);
                    }
                    else
                    {
                        gasPending.Add(e);
                    }
                }
                int gasTotal = gasLive.Count + gasPending.Count;
                if (gasTotal == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 天然气泄漏：本星球没有气态泉眼（前置条件本应拦下）");
                    return;
                }
                int hit = 0;
                for (int i = 0; i < gasLive.Count; i++)
                {
                    if (ForceErupt(gasLive[i]))
                    {
                        hit++;
                    }
                }
                if (gasPending.Count > 0)
                {
                    for (int i = 0; i < gasPending.Count; i++)
                    {
                        SpawnAndRevealGeyser(gasPending[i]);
                    }
                    ScheduleApplyGeyserEffect(inst, gasPending, g => ForceErupt(g), "天然气泄漏(延迟)");
                }
                MFLog.Verbose(LogCat.Event,"[Space] ★ 天然气泄漏：气态泉 共 " + gasTotal + "（活体 " + gasLive.Count
                    + " + 迷雾 " + gasPending.Count + "），立即喷发 " + hit + "，延迟再喷发 " + gasPending.Count);
                // 正文由 yaml tooltip 提供（静态文案）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 天然气泄漏执行失败");
            }
        }

        private static void OscillatorOverclock(IEventContext inst)
        {
            try
            {
                MethodInfo recharge = AccessTools.Method(typeof(GeneShuffler), "Recharge", null, null);
                if (recharge == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 神经振荡器超频：GeneShuffler.Recharge 不存在，事件不产生效果");
                    return;
                }
                List<GeneShuffler> all = SpaceRealEventRuntime.AllSceneComponents<GeneShuffler>();
                int inWorld = 0;
                int consumed = 0;
                int ready = 0;
                int hit = 0;
                int failed = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    GeneShuffler g = all[i];
                    if (g == null || !SpaceRealEventRuntime.IsInActiveWorld(g.gameObject))
                    {
                        continue;
                    }
                    inWorld++;
                    if (g.IsConsumed)
                    {
                        consumed++;
                    }
                    else if (!g.RechargeRequested)
                    {
                        ready++;          // 本来就充满：无需处理，也避免无谓地碰 SMI
                        continue;
                    }

                    // ★ 逐座 try：一座失败不能连累其余（原实现整段 try 会让后面的座全部跳过）
                    try
                    {
                        recharge.Invoke(g, null);
                        hit++;
                    }
                    catch (TargetInvocationException tie)
                    {
                        failed++;
                        Exception inner = tie.InnerException != null ? tie.InnerException : tie;
                        MFLog.Exc(LogCat.Event, inner, "[Space] 神经振荡器超频：跳过 1 座「" + g.gameObject.name + "」格 "
                            + Grid.PosToCell(g.gameObject));
                    }
                    catch (Exception e)
                    {
                        failed++;
                        MFLog.Exc(LogCat.Event, e, "[Space] 神经振荡器超频：跳过 1 座「" + g.gameObject.name + "」");
                    }
                }
                if (all.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 神经振荡器超频：本星球没有神经振荡器（场景内 GeneShuffler 0 座），事件不产生效果");
                    return;
                }
                if (inWorld == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 神经振荡器超频：全场 " + all.Count
                        + " 座 GeneShuffler 都不在当前星球，事件不产生效果");
                    return;
                }
                SpaceRealEventRuntime.GeneShuffleBonus = true;
                SpaceGeneShuffle.SaveBonus();
                MFLog.Verbose(LogCat.Event,"[Space] ★ 神经振荡器超频：命中对象 = GeneShuffler（原版神经振荡器 / Neural Vacillator）"
                    + " 场景 " + all.Count + " 座，本星球 " + inWorld + " 座，其中待充能 " + consumed
                    + " 座 / 本就充满 " + ready + " 座；调用 GeneShuffler.Recharge() 成功 " + hit + " 座"
                    + (failed > 0 ? "、跳过 " + failed + " 座" : "")
                    + "（IsConsumed→false，SMI isCharged→true）；已挂起 1 次超频奖励（下次震荡仪特质效果 +20%）");
                // 正文由 yaml tooltip 提供（静态文案）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神经振荡器超频执行失败");
            }
        }

        private static void GeologicalAnomaly(IEventContext inst)
        {
            try
            {
                int worldId = SpaceRealEventRuntime.MainWorldId();
                List<GeyserEntry> entries = CollectGeysers(worldId);
                if (entries.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 地质异常：本星球没有间歇泉（前置条件本应拦下）");
                    return;
                }
                if (UnityEngine.Random.value < 0.7f)
                {
                    List<Geyser> live = new List<Geyser>();
                    List<GeyserEntry> pending = new List<GeyserEntry>();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].live != null)
                        {
                            live.Add(entries[i].live);
                        }
                        else
                        {
                            pending.Add(entries[i]);
                        }
                    }
                    int hit = 0;
                    for (int i = 0; i < live.Count; i++)
                    {
                        if (ForceErupt(live[i]))
                        {
                            hit++;
                        }
                    }
                    if (pending.Count > 0)
                    {
                        for (int i = 0; i < pending.Count; i++)
                        {
                            SpawnAndRevealGeyser(pending[i]);
                        }
                        ScheduleApplyGeyserEffect(inst, pending, g => ForceErupt(g), "地质异常70%(延迟)");
                    }
                    MFLog.Verbose(LogCat.Event,"[Space] ★ 地质异常（70% 分支）：全部间歇泉强制喷发 " + (hit + pending.Count) + "/" + entries.Count);
                }
                else
                {
                    int idx = UnityEngine.Random.Range(0, entries.Count);
                    GeyserEntry pick = entries[idx];
                    if (pick.live != null)
                    {
                        BoostGeyser(pick.live, inst);
                    }
                    else
                    {
                        SpawnAndRevealGeyser(pick);
                        int cell = pick.cell;
                        IEventContext instRef = inst;
                        if (GameScheduler.Instance != null)
                        {
                            GameScheduler.Instance.Schedule("SpaceGeyserBoost", 0.5f, delegate(object o)
                            {
                                try
                                {
                                    Geyser g = FindGeyserAtCell(cell);
                                    if (g != null)
                                    {
                                        BoostGeyser(g, instRef);
                                    }
                                    else
                                    {
                                        MFLog.Warn(LogCat.Event,"[Space] 地质异常30% 延迟取泉失败 cell=" + cell);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MFLog.Exc(LogCat.Event, ex, "[Space] 地质异常30% 延迟生效失败");
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 地质异常执行失败");
            }
        }

        // 唤醒后延迟一轮：世界生成器在后续帧实例化，届时重读并执行效果（原版即 Reveal+Dig→等一帧→取实例）
        private static void DeferGeyser(IEventContext inst, int worldId, bool gasOnly, float delay)
        {
            if (GameScheduler.Instance == null)
            {
                return;
            }
            IEventContext instRef = inst;
            string key = gasOnly ? "SpaceGasLeakWake" : "SpaceQuakeWake";
            GameScheduler.Instance.Schedule(key, delay, delegate(object o)
            {
                try
                {
                    List<Geyser> all = WorldGeysers();
                    int hit = 0;
                    if (gasOnly)
                    {
                        int gas = CountGasGeysers(all);
                        for (int i = 0; i < all.Count; i++)
                        {
                            if (IsGasGeyser(all[i]) && ForceErupt(all[i]))
                            {
                                hit++;
                            }
                        }
                        MFLog.Verbose(LogCat.Event,"[Space] ★ 天然气泄漏（延迟后）：泉=" + all.Count + " 气态=" + gas + " 喷发=" + hit);
                        if (instRef != null)
                        {
                            // 正文由 yaml tooltip 提供（静态，分支变体见 variants）
                        }
                        return;
                    }
                    MFLog.Verbose(LogCat.Event,"[Space] ★ 地质异常（延迟后）：泉=" + all.Count);
                    if (all.Count == 0)
                    {
                        if (instRef != null)
                        {
                            // 正文由 yaml tooltip 提供（静态，分支变体见 variants）
                        }
                        return;
                    }
                    if (UnityEngine.Random.value < 0.7f)
                    {
                        for (int i = 0; i < all.Count; i++)
                        {
                            if (ForceErupt(all[i]))
                            {
                                hit++;
                            }
                        }
                        MFLog.Verbose(LogCat.Event,"[Space] ★ 地质异常（延迟后 70%）：全部喷发 " + hit + "/" + all.Count);
                        if (instRef != null)
                        {
                            // 默认分支：正文由 yaml tooltip 提供
                        }
                    }
                    else
                    {
                        Geyser g = all[UnityEngine.Random.Range(0, all.Count)];
                        float pct = UnityEngine.Random.Range(0.15f, 0.30f);
                        Geyser.GeyserModification mod = new Geyser.GeyserModification();
                        mod.originID = "SpaceEventGeological";
                        mod.massPerCycleModifier = pct;
                        g.AddModification(mod);
                        MFLog.Verbose(LogCat.Event,"[Space] ★ 地质异常（延迟后 30%）：" + g.GetProperName() + " 产量 +" + Mathf.Round(pct * 100f) + "%");
                        if (instRef != null)
                        {
                            instRef.SetNoticeVariant("boost");
                            instRef.SetNoticeArg("target", g.GetProperName());
                            instRef.SetNoticeArg("pct", "+" + Mathf.Round(pct * 100f) + "%");
                        }
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Event, e, "[Space] 间歇泉事件（延迟）失败");
                }
            });
        }

        private static void GreatExtinction(IEventContext inst)
        {
            try
            {
                if (GameplayEventManager.Instance == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 大灭绝：GameplayEventManager 不可用");
                    inst.SetNoticeArg("result", "事件系统不可用，本次灾星没有降临");
                    return;
                }
                GameplayEvent ev = Db.Get().GameplayEvents.LargeImpactor;
                if (ev == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 大灭绝：本版没有 LargeImpactor 玩法事件");
                    return;
                }
                if (GameplayEventManager.Instance.IsGameplayEventActive(ev))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 大灭绝：灾星事件已在进行，跳过本次触发");
                    return;
                }
                if (Assets.TryGetPrefab(new Tag("LargeImpactor")) == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 大灭绝：prefab LargeImpactor 不存在（未装对应 DLC）");
                    return;
                }
                int worldId = SpaceRealEventRuntime.MainWorldId();
                float delaySeconds = ExtinctionDelaySeconds;
                if (inst != null)
                {
                    // 数值来自事件 yaml，随机与换算留在宿主侧
                    float lo = Mathf.Min(inst.GetDefNumber("impactor_delay_min_cycles", 0f),
                                         inst.GetDefNumber("impactor_delay_max_cycles", 0f));
                    float hi = Mathf.Max(inst.GetDefNumber("impactor_delay_min_cycles", 0f),
                                         inst.GetDefNumber("impactor_delay_max_cycles", 0f));
                    if (hi > 0f)
                    {
                        if (lo <= 0f) lo = hi;
                        delaySeconds = UnityEngine.Random.Range(lo, hi) * 600f;
                    }
                }
                if (delaySeconds <= 0f) delaySeconds = ExtinctionDelaySeconds;
                PendingImpactorDelay = delaySeconds;
                PendingImpactorArrival = SpaceRealEventRuntime.Now() + delaySeconds;
                PendingImpactorWorldId = worldId;
                PendingImpactorStartedAt = SpaceRealEventRuntime.Now();
                HasPendingImpactor = true;
                GameplayEventInstance gei = GameplayEventManager.Instance.StartNewEvent(ev, worldId, null);
                pendingImpactorSmi = gei != null ? gei.smi : null;
                TryConfigureImpactor();
                MFLog.Verbose(LogCat.Event,"[Space] ★ 大灭绝：已启动原版灾星流程，预告倒计时 " + Mathf.Round(delaySeconds)
                    + " 秒（" + (delaySeconds / 600f).ToString("0.0") + " 周期，撞击时刻 "
                    + Mathf.Round(PendingImpactorArrival) + " 秒游戏时间，世界 " + worldId + "）");
                inst.SetNoticeArg("countdown", Mathf.Max(0f, delaySeconds / 600f).ToString("0.0"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 大灭绝执行失败");
            }
        }

        private static bool TryConfigureImpactor()
        {
            try
            {
                LargeImpactorEvent.StatesInstance li = pendingImpactorSmi as LargeImpactorEvent.StatesInstance;
                GameObject imp = li != null ? li.impactorInstance : null;
                if (imp == null)
                {
                    return false;
                }
                try
                {
                    LargeImpactorNotificationMonitor.Instance monitor =
                        imp.GetSMI<LargeImpactorNotificationMonitor.Instance>();
                    if (monitor != null)
                    {
                        LargeImpactorNotificationMonitor.Discover(monitor);
                        monitor.SetNotificationVisibility(true);
                        MFLog.Verbose(LogCat.Event,"[Space] 大灭绝：已强制解锁灾星来临演出与通知（倒计时 "
                            + Mathf.Round(PendingImpactorArrival / 600f) + " 周期，世界 "
                            + PendingImpactorWorldId + "）");
                    }
                    else
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 大灭绝：灾星实体缺少 LargeImpactorNotificationMonitor，无法播放来临动画");
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Event, e, "[Space] 大灭绝：播放灾星预告失败");
                }
                HasPendingImpactor = false;
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 大灭绝：改写灾星流程失败");
                HasPendingImpactor = false;
                return false;
            }
        }

        private static void DoomCometFallback(string why)
        {
            try
            {
                int count = UnityEngine.Random.Range(14, 23);
                int ok = SpawnDoomComets(count);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 大灭绝（等价实现：" + why + "）：毁灭性陨石雨 " + ok + "/" + count + " 发");
                // 异步后备路径：触发时根通知可能已撤，无法写入 inst ⇒ 改用独立标题，避免与"大灭绝"根通知同名
                TradeUi.Notify("大灭绝·后备陨石雨", "灾星无法走原版流程（" + why + "），改为毁灭性陨石雨 " + ok + " 发");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 大灭绝陨石雨失败");
            }
        }

        private static GameObject EnsureDoomTemplate()
        {
            if (doomTemplate != null)
            {
                return doomTemplate;
            }
            if (doomTried)
            {
                return null;
            }
            doomTried = true;
            try
            {
                GameObject go = EntityTemplates.CreateEntity("SpaceDoomComet", "灾星碎片", true);
                go.AddOrGet<SaveLoadRoot>();
                go.AddOrGet<LoopingSounds>();
                Comet comet = go.AddOrGet<Comet>();
                comet.massRange = new Vector2(400f, 800f);
                comet.EXHAUST_ELEMENT = SimHashes.CarbonDioxide;
                comet.temperatureRange = new Vector2(673.15f, 873.15f);
                comet.entityDamage = 12;
                comet.explosionOreCount = new Vector2I(6, 10);
                comet.totalTileDamage = 24f;
                comet.splashRadius = 2;
                comet.impactSound = "Meteor_Large_Impact";
                comet.flyingSoundID = 1;
                comet.explosionEffectHash = SpawnFXHashes.MeteorImpactDust;
                comet.addTiles = 0;
                PrimaryElement pe = go.AddOrGet<PrimaryElement>();
                pe.SetElement(SimHashes.Obsidian, true);
                pe.Temperature = 700f;
                KBatchedAnimController kbac = go.AddOrGet<KBatchedAnimController>();
                kbac.AnimFiles = new KAnimFile[] { Assets.GetAnim("meteor_rock_kanim") };
                kbac.isMovable = true;
                kbac.initialAnim = "fall_loop";
                kbac.initialMode = KAnim.PlayMode.Loop;
                kbac.visibilityType = KAnimControllerBase.VisibilityType.OffscreenUpdate;
                go.AddOrGet<KCircleCollider2D>().radius = 0.5f;
                go.transform.localScale = new Vector3(2f, 2f, 1f);
                go.AddTag(GameTags.Comet);
                go.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(go);
                doomTemplate = go;
                return doomTemplate;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 灾星碎片模板创建失败");
                return null;
            }
        }

        private static int SpawnDoomComets(int count)
        {
            GameObject prefab = EnsureDoomTemplate();
            if (prefab == null)
            {
                return 0;
            }
            int ok = 0;
            for (int i = 0; i < count; i++)
            {
                int cell = RandomSurfaceCell();
                if (cell < 0)
                {
                    continue;
                }
                GameObject go = Util.KInstantiate(prefab, Grid.CellToPos(cell));
                if (go == null)
                {
                    continue;
                }
                Comet comet = go.GetComponent<Comet>();
                if (comet != null)
                {
                    comet.spawnWithOffset = true;
                    comet.Velocity = new Vector2(UnityEngine.Random.Range(-5f, 5f), -22f);
                }
                go.SetActive(true);
                ok++;
            }
            return ok;
        }

        private static int RandomSurfaceCell()
        {
            try
            {
                WorldContainer w = SpaceRealEventRuntime.MainWorld();
                if (w == null)
                {
                    return -1;
                }
                int x = w.WorldOffset.x + UnityEngine.Random.Range(4, Mathf.Max(5, w.Width - 4));
                int top = w.WorldOffset.y + w.Height - 2;
                for (int y = top; y >= w.WorldOffset.y; y--)
                {
                    int c = Grid.XYToCell(x, y);
                    if (Grid.IsValidCell(c) && Grid.Solid[c])
                    {
                        return c;
                    }
                }
            }
            catch (Exception)
            {
                // expected: 地表探测失败返回 -1，由调用方按无处可落处理
            }
            return -1;
        }
    }
}
