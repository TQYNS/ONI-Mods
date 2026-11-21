// 用途：真实事件管理：键常量、模板、前置条件与处理器注册

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
        internal const string TempStationPrefix = "evt_temp_";

        internal const string AnimalTamingKey = "real_animal_taming";
        internal const string EpiphanyKey = "real_epiphany";
        internal const string AlienBroadcastKey = "real_alien_broadcast";
        internal const string SpaceFreighterKey = "real_space_freighter";
        internal const string LostLogisticsKey = "real_lost_logistics";
        internal const string GasLeakKey = "real_gas_leak";
        internal const string OscillatorKey = "real_oscillator_overclock";
        internal const string PirateWreckKey = "real_pirate_wreck";
        internal const string GeologicalKey = "real_geological_anomaly";
        internal const string ExtinctionKey = "real_great_extinction";
        internal const string SneakEatingKey = "real_sneak_eating";
        internal const string HappyMoodKey = "real_happy_mood";
        internal const string OvercomeFearKey = "real_overcome_fear";
        internal const string GravityKey = "real_gravity_anomaly";
        internal const string FertileKey = "real_fertile_season";
        internal const string SporeKey = "real_spore_cloud";
        internal const string MagneticKey = "real_magnetic_storm";
        internal const string DrySeasonKey = "real_dry_season";
        internal const string CosmicKey = "real_cosmic_ray";

        internal const string DecodeHook = "alien_broadcast.decode";
        internal const string IgnoreHook = "alien_broadcast.ignore";
        internal const float ExtinctionDelaySeconds = 48000f;   // 80 周期（文档下限），仅异常兜底

        private static bool registered;
        private static GameObject doomTemplate;
        private static bool doomTried;

        // 退档清理：DontDestroyOnLoad 的灾星碎片模板跨会话滞留，下次触发按需重建
        internal static void DestroyTemplates()
        {
            try
            {
                if (doomTemplate != null)
                {
                    UnityEngine.Object.Destroy(doomTemplate);
                }
            }
            catch (Exception)
            {
                // expected: 模板已销毁或尚未创建，清理失败可忽略（随后统一置空）
            }
            doomTemplate = null;
            doomTried = false;
        }
        private static bool impactorPatchTried;
        private static object pendingImpactorSmi;

        internal static float PendingImpactorArrival;
        internal static float PendingImpactorDelay = 300f;   // 本次灾星倒计时（由 yaml 决定）
        internal static bool HasPendingImpactor;
        internal static int PendingImpactorWorldId;
        internal static float PendingImpactorStartedAt;

        // 前置条件返回 false 时框架直接判定"本次不通过"并跳过本轮抽取（不注册、不通知；失败计数与退避已移除）

        private static bool AnimalTamingPrecondition(IEventContext inst)
        {
            try
            {
                if (Components.Brains != null)
                {
                    foreach (Brain brain in Components.Brains)
                    {
                        if (brain == null) continue;
                        GameObject go = brain.gameObject;
                        if (go == null || !SpaceRealEventRuntime.IsInActiveWorld(go)) continue;
                        AmountInstance w = Db.Get().Amounts.Wildness.Lookup(go);
                        if (w == null || w.value <= 0f) continue;
                        if (string.IsNullOrEmpty(PrefabIdOf(go))) continue;
                        return true;
                    }
                }
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 动物驯化前置条件异常"); }
            MFLog.Verbose(LogCat.Event,"[Space] 动物驯化：前置条件不通过（本星球无野生生物）⇒ 跳过本轮抽取");
            return false;
        }

        private static bool EpiphanyPrecondition(IEventContext inst)
        {
            try
            {
                List<GameObject> dupes = LiveDupes(true);
                Database.Skills skills = Db.Get().Skills;
                for (int i = 0; i < dupes.Count; i++)
                {
                    MinionResume r = dupes[i].GetComponent<MinionResume>();
                    if (r == null) continue;
                    for (int k = 0; k < skills.Count; k++)
                    {
                        Database.Skill s = skills[k];
                        if (s == null || string.IsNullOrEmpty(s.Id)) continue;
                        if (r.HasMasteredSkill(s.Id)) continue;
                        if (!r.IsAbleToLearnSkill(s.Id)) continue;
                        return true;
                    }
                }
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 醍醐灌顶前置条件异常"); }
            MFLog.Verbose(LogCat.Event,"[Space] 醍醐灌顶：前置条件不通过（无可学技能的复制人）⇒ 跳过本轮抽取");
            return false;
        }

        private static bool OvercomeFearPrecondition(IEventContext inst)
        {
            try
            {
                LoadNegativeTraitIds();
                List<GameObject> dupes = LiveDupes(true);
                for (int i = 0; i < dupes.Count; i++)
                {
                    Klei.AI.Traits t = dupes[i].GetComponent<Klei.AI.Traits>();
                    if (t == null || t.TraitList == null) continue;
                    for (int k = 0; k < t.TraitList.Count; k++)
                    {
                        Klei.AI.Trait tr = t.TraitList[k];
                        if (tr == null || string.IsNullOrEmpty(tr.Id) || tr.Id == "None") continue;
                        if (negativeTraitIds.Contains(tr.Id)) return true;
                    }
                }
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 克服恐惧前置条件异常"); }
            MFLog.Verbose(LogCat.Event,"[Space] 克服恐惧：前置条件不通过（无可移除的负面特质）⇒ 跳过本轮抽取");
            return false;
        }

        private static bool SneakEatingPrecondition(IEventContext inst)
        {
            try
            {
                List<Edible> list = WorldFoods();
                float total = 0f;
                for (int i = 0; i < list.Count; i++) total += Mathf.Max(0f, list[i].Calories);
                if (total >= 1000f * 1000f) return true;
                MFLog.Verbose(LogCat.Event,"[Space] 偷吃：前置条件不通过（食物 " + (total / 1000f).ToString("0")
                          + " kcal < 1000）⇒ 跳过本轮抽取");
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 偷吃前置条件异常"); }
            return false;
        }

        private static bool GasLeakPrecondition(IEventContext inst)
        {
            try
            {
                int worldId = SpaceRealEventRuntime.MainWorldId();
                List<GeyserEntry> entries = CollectGeysers(worldId);
                for (int i = 0; i < entries.Count; i++)
                {
                    GeyserEntry e = entries[i];
                    bool isGas = (e.live != null)
                        ? IsGasGeyser(e.live)
                        : (e.spawn != null && IsGasSpawnable(e.spawn.spawnInfo.id));
                    if (isGas) return true;
                }
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 天然气泄漏前置条件异常"); }
            MFLog.Verbose(LogCat.Event,"[Space] 天然气泄漏：前置条件不通过（本星球无气态泉）⇒ 跳过本轮抽取");
            return false;
        }

        private static bool GeologicalPrecondition(IEventContext inst)
        {
            try
            {
                int worldId = SpaceRealEventRuntime.MainWorldId();
                if (CollectGeysers(worldId).Count > 0) return true;
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 地质异常前置条件异常"); }
            MFLog.Verbose(LogCat.Event,"[Space] 地质异常：前置条件不通过（本星球无间歇泉）⇒ 跳过本轮抽取");
            return false;
        }

        private static bool OscillatorPrecondition(IEventContext inst)
        {
            try
            {
                if (AccessTools.Method(typeof(GeneShuffler), "Recharge", null, null) == null) return false;
                List<GeneShuffler> all = SpaceRealEventRuntime.AllSceneComponents<GeneShuffler>();
                for (int i = 0; i < all.Count; i++)
                {
                    GeneShuffler g = all[i];
                    if (g != null && SpaceRealEventRuntime.IsInActiveWorld(g.gameObject)) return true;
                }
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 神经振荡器超频前置条件异常"); }
            MFLog.Verbose(LogCat.Event,"[Space] 神经振荡器超频：前置条件不通过（本星球无可用神经振荡器）⇒ 跳过本轮抽取");
            return false;
        }

        private static bool ExtinctionPrecondition(IEventContext inst)
        {
            try
            {
                if (HasPendingImpactor) return false;
                if (Db.Get().GameplayEvents.LargeImpactor == null) return false;
                if (GameplayEventManager.Instance == null) return false;
                if (GameplayEventManager.Instance.IsGameplayEventActive(Db.Get().GameplayEvents.LargeImpactor)) return false;
                if (Assets.TryGetPrefab(new Tag("LargeImpactor")) == null) return false;
                return true;
            }
            catch (Exception e) { MFLog.Verbose(LogCat.Event, e, "[Space] 大灭绝前置条件异常"); return false; }
        }

        internal static void RegisterHandlers()
        {
            SpaceRealEventRuntime.EnsureTicker();
            SpaceGeneShuffle.Ensure();
            SetUpPassiveHooks();
            HookBuffRestore();
            if (registered)
            {
                return;
            }
            registered = true;
            Reg(AnimalTamingKey, AnimalTaming, null);
            Reg(EpiphanyKey, Epiphany, null);
            Reg(AlienBroadcastKey, AlienBroadcast, null);
            Reg(SpaceFreighterKey, SpaceFreighter, null);
            Reg(LostLogisticsKey, LostLogistics, null);
            Reg(GasLeakKey, GasLeak, null);
            Reg(OscillatorKey, OscillatorOverclock, null);
            Reg(PirateWreckKey, PirateWreck, null);
            Reg(GeologicalKey, GeologicalAnomaly, null);
            Reg(ExtinctionKey, GreatExtinction, null);
            Reg(SneakEatingKey, SneakEating, null);
            // ★ 喜悦反应带前置条件：没有可用候选 ⇒ 走失败路线，跳过本轮抽取
            Reg(HappyMoodKey, HappyMood, null);
            Reg(OvercomeFearKey, OvercomeFear, null);
            // ★ 持续型：该 buff 正在持续 ⇒ 本轮不抽取（替代原"已在持续中"分支）
            Reg(GravityKey, GravityAnomalyOn, GravityAnomalyOff);
            Reg(FertileKey, FertileSeasonOn, FertileSeasonOff);
            Reg(SporeKey, SporeCloudOn, SporeCloudOff);
            Reg(MagneticKey, MagneticStormOn, MagneticStormOff);
            Reg(DrySeasonKey, DrySeasonOn, DrySeasonOff);
            Reg(CosmicKey, CosmicRayOn, CosmicRayOff);

            // ★ 预检条件统一注册（对应各事件 yaml 的 condition.function）
            RegisterConditions();

            try
            {
                EventFrameworkApi.RegisterHook(DecodeHook, DecodeBroadcast);
                EventFrameworkApi.RegisterHook(IgnoreHook, IgnoreBroadcast);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 外星广播选项钩子注册失败");
            }
            MFLog.Verbose(LogCat.Event,"[Space] 真实事件处理器已注册：一次性 13 个 / 持续型 6 个 / 选项钩子 2 个");
        }

        private static void Reg(string key, Action<IEventContext> onTrigger, Action<IEventContext> onEnd)
        {
            try
            {
                EventFramework.Distribution.Contracts.EventHandler h =
                    new EventFramework.Distribution.Contracts.EventHandler();
                h.onTrigger = onTrigger;
                if (onEnd != null)
                {
                    h.onEnd = onEnd;
                }
                EventFrameworkApi.RegisterHandler(key, h);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 真实事件处理器注册失败 " + key);
            }
        }

        // 预检条件集中注册：key = 事件 yaml 的 id（condition.function 同值）
        internal static void RegisterConditions()
        {
            // 一、需要活体目标（殖民地上有可作用的实体）
            Cond("animal_taming",        AnimalTamingPrecondition);
            Cond("epiphany",             EpiphanyPrecondition);
            Cond("overcome_fear",        OvercomeFearPrecondition);
            Cond("sneak_eating",         SneakEatingPrecondition);
            Cond("happy_mood",           HappyMoodPrecondition);

            // 二、需要泉眼 / 建筑
            Cond("gas_leak",             GasLeakPrecondition);
            Cond("geological_anomaly",   GeologicalPrecondition);
            Cond("oscillator_overclock", OscillatorPrecondition);

            // 三、玩法就绪（灾星事件可用）
            Cond("great_extinction",     ExtinctionPrecondition);

            // 四、持续型：该 buff 未被占用（替代原"已在持续中"分支）
            Cond("gravity_anomaly", _ => !SpaceRealEventRuntime.HasBuff(GravityKey));
            Cond("fertile_season",  _ => !SpaceRealEventRuntime.HasBuff(FertileKey));
            Cond("spore_cloud",     _ => !SpaceRealEventRuntime.HasBuff(SporeKey));
            Cond("magnetic_storm",  _ => !SpaceRealEventRuntime.HasBuff(MagneticKey));
            Cond("dry_season",      _ => !SpaceRealEventRuntime.HasBuff(DrySeasonKey));
            Cond("cosmic_ray",      _ => !SpaceRealEventRuntime.HasBuff(CosmicKey));
        }

        // 一行入口：无跨状态依赖（当前所有条件都不依赖 flag/counter/var）
        private static void Cond(string key, Func<IEventContext, bool> fn)
        {
            // 第三参显式传空数组 = "已声明、无依赖"；传 null 会被框架判为未声明并逐条告警
            EventFrameworkApi.RegisterCondition(key, fn, new string[0]);
        }

        internal static void PushDiseaseTable()
        {
            try
            {
                SimMessages.CreateDiseaseTable(Db.Get().Diseases);
                MFLog.Verbose(LogCat.Event,"[Space] 已重新下发病菌增长表（半衰期改动生效）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 病菌表下发失败");
            }
        }
    }
}
