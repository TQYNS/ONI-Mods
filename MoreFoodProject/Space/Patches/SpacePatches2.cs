using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 退档清理：DontDestroyOnLoad 模板不随场景卸载，离开存档时销毁并在下次按需重建
    [HarmonyPatch(typeof(Game), "StopBE")]
    internal static class SpaceTemplateCleanupPatch
    {
        private static void Prefix()
        {
            try
            {
                CometRain.DestroyTemplates();
                SpaceSatelliteCrashEvent.DestroyTemplates();
                SpaceRealEvents.DestroyTemplates();
                AdvancedGameplaySettings.Reset();   // 本局结束，高级设置不带入下一场
                MFLog.Verbose(LogCat.Space,"[Space] 退档清理：坠落体模板已销毁（下次触发重建）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 退档清理模板异常");
            }
        }
    }

    // 交易系统：存档挂载 + 读档恢复
    [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
    internal static class TradeSaveMountPatch
    {
        private static void Postfix(SaveGame __instance)
        {
            try
            {
                // 先复位恢复闸，再跑 Init：Init 里也会尝试补跑恢复，
                SpaceRestoreGate.Pending = true;
                SpaceRestoreGate.Deserialized = false;
                SpaceSaveHost.LoadFinished = false;   // 新会话重新等心跳第一拍
                ImpactorGate.ResetSession();          // 闸口镜像不跨会话（随档值由 EventSaveData 恢复）
                TradeRegistry.Init();
                SpaceTestRuntime.RegisterPrefabs();
                // 【探针定案】：不再挂 SaveGame（它不在存档对象列表里，写了也不落盘）。
                SpaceSaveHost.RegisterPrefab();
                SpaceAchievements.Register();
                CombatRuntime.inited = false;      // 让战斗运行时按新会话重建（刷怪器/prefab）
                // 改走 BeginSession —— 以前只把 inited 置 false，
                TradeRegistry.BeginSession();
                MFLog.Verbose(LogCat.Space,"[Space] 存档宿主 prefab 已注册（实体承载；载体在 Attach/读档时挂到宿主上）");
                if (__instance != null && __instance.gameObject.GetComponent<SpaceHost>() == null)
                {
                    __instance.gameObject.AddComponent<SpaceHost>();
                }
                MFLog.Verbose(LogCat.Space,"[Space] 交易存档载体已挂载（等待反序列化后恢复）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 存档挂载失败");
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoadRoot), "LoadInternal")]
    internal static class TradeSaveRestorePatch
    {
        private static void Postfix(GameObject gameObject)
        {
            try
            {
                if (!SpaceRestoreGate.Pending)
                {
                    return;
                }
                if (gameObject == null)
                {
                    return;
                }
                if (gameObject.GetComponent<SaveGame>() != null)
                {
                    SpaceRestoreGate.Deserialized = true;
                    MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L0 反序列化标记：LoadInternal(SaveGame) 完成");
                }
                if (gameObject.GetComponent<SpaceSaveHost>() != null)
                {
                    TradeSaveData t = gameObject.GetComponent<TradeSaveData>();
                    CombatSaveData c = gameObject.GetComponent<CombatSaveData>();
                    MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L1 宿主实体已载入：对象=" + gameObject.name + "#" + gameObject.GetInstanceID()
                        + " 贸易[站点=" + (t != null ? t.stationIds.Count + " 任务=" + t.questActive.Count : "无组件")
                        + "] 战斗[怪物=" + (c != null ? c.combatMonsterIds.Count + " 探针=" + c.debugMark : "无组件") + "]");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 贸易读档标记失败");
            }
        }
    }

    internal static class SpaceRestoreGate
    {
        internal static bool Pending;
        internal static bool Deserialized;

        // 【真根因】读档恢复必须在【星图环数扩到 27 之后】跑。
        internal static void RunDeferredRestores()
        {
            if (!Pending)
            {
                return;
            }
            // 【真根因·第二次踩同一个坑】：以前先消费闸再判 Deserialized。
            if (!Deserialized)
            {
                return;
            }
            // 宿主实体还没载入就别消费闸：否则会建空壳/读到 0，之后再也补不回来。
            if (!SpaceSaveHost.HasSceneHost() && !SpaceSaveHost.LoadFinished)
            {
                return;
            }
            Pending = false;
            MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L2 RunDeferredRestores：进入（闸已消费）");
            try
            {
                var sites = Resources.FindObjectsOfTypeAll<SpaceQuestSite>();
                int live = 0;
                for (int i = 0; i < sites.Length; i++)
                {
                    SpaceQuestSite site = sites[i];
                    if (site == null || site.gameObject == null || !site.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    live++;
                    WorldContainer wc = site.GetComponent<WorldContainer>();
                    MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L1b 任务地点实体：label=" + site.Name
                        + " 任务=" + site.questId + " 有WorldContainer=" + (wc != null)
                        + " worldId=" + (wc != null ? wc.id.ToString() : "-")
                        + " active=" + site.gameObject.activeInHierarchy);
                }
                MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L1b 任务地点实体清点：共 " + live + " 个（含未激活）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 清点任务地点实体失败");
            }
            try
            {
                TradeSaveData save = TradeSaveData.Attach();
                if (save == null)
                {
                    MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L2 ⚠ Attach()=null ⇒ 贸易恢复没跑");
                }
                else
                {
                    save.Restore();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 贸易读档恢复失败");
            }
            try
            {
                CombatSaveData csave = CombatSaveData.Attach();
                if (csave == null)
                {
                    MFLog.Verbose(LogCat.Space,"[SAVE-TRACE] L2 ⚠ Attach()=null ⇒ 战斗恢复没跑");
                }
                else
                {
                    csave.Restore();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 战斗读档恢复失败");
            }
        }
    }

    // 防崩 + 定案探针：ChoreConsumer.FindNextChore 的每帧 NRE（preconditionSnapshot 内部列表为 null）
    [HarmonyPatch(typeof(ChoreConsumer), "FindNextChore")]
    internal static class ChoreConsumerCrashGuardPatch
    {
        private static readonly HashSet<int> logged = new HashSet<int>();

        private static Exception Finalizer(ChoreConsumer __instance, Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }
            try
            {
                int id = __instance != null ? __instance.GetInstanceID() : 0;
                if (logged.Add(id))
                {
                    WorldContainer w = null;
                    try
                    {
                        w = __instance != null && __instance.gameObject != null
                            ? ClusterManager.Instance.GetWorldFromPosition(__instance.transform.position) : null;
                    }
                    catch (Exception)
                    {
                        // expected: 世界定位失败不影响异常日志输出，世界字段保持 "?"
                    }
                    MFLog.Warn(LogCat.Space,"[Space] 捕获 ChoreConsumer.FindNextChore 异常（已吞，防崩）：对象="
                        + (__instance != null && __instance.gameObject != null ? __instance.gameObject.name : "?")
                        + "#" + id + " 世界=" + (w != null ? w.id + "/" + w.GetProperName() : "?")
                        + " 异常=" + __exception);
                }
            }
            catch (Exception)
            {
                // expected: 记录异常日志本身失败也必须吞掉，避免补丁内二次崩溃
            }
            return null;   // 吞掉 ⇒ 不冒泡到 LogCatcher ⇒ 不崩
        }
    }

    [HarmonyPatch(typeof(Game), "OnSpawn")]
    internal static class TradeModScanPatch
    {
        private static void Postfix()
        {
            try
            {
                SpaceLoadTrace.Set("Game.OnSpawn（新局/读档）");
                TradeDb.OverlayMods();
                ModPerfProbe.RefreshFromOptions();   // 进游戏后各 Mod 补丁已全部就位，此时才装探针（未勾选则什么都不装）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 多 Mod 贸易扫描补丁异常（其余 Mod 的 trade/ 未合并）");
            }
            try
            {
                CombatDb.Load();
                CombatDb.OverlayMods();
                CombatDb.Build();
                // 问题15：Monsters 就绪后才生成百科（CodexCacheInit 早于此处，那时 Monsters 还空）
                CodexFactory.Reset();
                CodexFactory.EnsureRegistered();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 战斗配置加载补丁异常（战斗内容可能只有部分可用）");
                ContentDiag.End("combat");
                ContentDiag.End("combat-覆盖");
            }
            try
            {
                SpaceTemplates.ScanAllMods();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] map 模板扫描补丁异常（星际地点模板缺失）");
            }
            try
            {
                SpaceTimers.RunPendingPostLoad();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 装载后初始化补跑异常");
            }
            // 读档恢复不在这里做 —— 此刻星图还只有 12 环，我们的格子全被判"无效"。
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class DbReadyAchievementPatch
    {
        private static void Postfix()
        {
            try
            {
                SpaceAchievements.Register();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] Db 完成后注入成就失败（成就页缺条目）");
            }
            try
            {
                SpaceStarmapEvents.RegisterHandlers();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] Db 完成后注册事件处理器失败");
            }
        }
    }

    [HarmonyPatch(typeof(ColonyAchievementTracker), "Deserialize")]
    internal static class AchievementTrackerDeserializePatch
    {
        private static void Prefix(ColonyAchievementTracker __instance)
        {
            try
            {
                SpaceAchievementInjector.ClearEntries(__instance);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 成就字典预清理失败（继续读档）");
            }
        }

        private static void Postfix(ColonyAchievementTracker __instance)
        {
            try
            {
                SpaceAchievementInjector.EnsureEntries(__instance);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 成就字典补条目失败（继续读档）");
            }
        }
    }

    // 星图选中实体 → 屏幕下方按钮排
    [HarmonyPatch(typeof(ClusterMapScreen), "SetSelectedEntity")]
    internal static class ClusterMapSelectPatch
    {
        private static void Postfix(ClusterGridEntity entity)
        {
            try
            {
                if (TradeUi.AnyOpen())
                {
                    return;   // 交易/远程窗开着时不刷新底部按钮排（模态期间一切以窗口为准）
                }
                SpaceStation st = entity as SpaceStation;
                if (st != null)
                {
                    TradeUi.ShowBar(st);
                    return;
                }
                Clustercraft craft = entity as Clustercraft;
                if (craft != null)
                {
                    CombatUi.ShowCraftBar(craft);   // K10/K12：火箭按钮排 [攻击][捕获][紧急脱离][交易]
                    return;
                }
                SpaceMonster m = entity as SpaceMonster;
                if (m != null)
                {
                    TradeUi.HideBar();   // K12：怪物不出按钮排（攻击一律从火箭侧发起）
                    return;
                }
                SpaceQuestSite q = entity as SpaceQuestSite;
                if (q != null)
                {
                    TradeUi.ShowQuestBar(q);
                    return;
                }
                TradeUi.HideBar();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 站点按钮排处理失败");
            }
        }
    }

    // 燃料倍率：改 BurnFromTank 的 ref 参（D-09 的燃料项）
    [HarmonyPatch(typeof(Clustercraft), "BurnFromTank")]
    internal static class FuelMultiplierPatch
    {
        private static void Prefix(ref float attemptTravelAmount)
        {
            try
            {
                float mul = TradeRegistry.RankFuelMul();
                if (mul > 0f && mul < 1f)
                {
                    attemptTravelAmount *= mul;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 燃料倍率处理失败");
            }
        }
    }

    // 火箭速度：等级倍率 × 血量惩罚（1.0~0.6）
    [HarmonyPatch(typeof(Clustercraft), "OnSpawn")]
    internal static class CraftSpeedPatch
    {
        private static void Postfix(Clustercraft __instance)
        {
            try
            {
                SpaceLoadTrace.Set("火箭 OnSpawn：取 ClusterTraveler（" + __instance.Name + "）");
                ClusterTraveler t = __instance.GetComponent<ClusterTraveler>();
                if (t == null || t.getSpeedCB == null)
                {
                    return;
                }
                Func<float> orig = t.getSpeedCB;
                Clustercraft craft = __instance;
                t.getSpeedCB = delegate
                {
                    try
                    {
                        // 纯模块速度：HP 系数 × Π utility.speed_mul（不再叠等级）
                        return orig() * RocketStats.HpSpeedFactor(craft)
                            * CombatRuntime.CraftSpeedMul(craft);
                    }
                    catch (Exception)
                    {
                        return orig();
                    }
                };
                SpaceLoadTrace.Set("火箭 OnSpawn：速度回调已包，算血量系数");
                float hpFactor = RocketStats.HpSpeedFactor(__instance);
                SpaceLoadTrace.Set("火箭 OnSpawn：血量系数 " + hpFactor.ToString("0.00") + "，算模块系数");
                float modFactor = CombatRuntime.CraftSpeedMul(__instance);
                SpaceLoadTrace.Set("火箭 OnSpawn：模块系数 " + modFactor.ToString("0.00") + "，写日志");
                MFLog.Verbose(LogCat.Space,"[Space] 火箭 " + __instance.Name + " 速度倍率已挂上（纯模块/血量，"
                    + "血量系数 " + hpFactor.ToString("0.00")
                    + "，模块系数 " + modFactor.ToString("0.00") + "）");
                SpaceLoadTrace.Set("火箭 OnSpawn：完成");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 速度倍率挂载失败");
            }
        }
    }

    // 击杀归属：全工程唯一的"攻击者+阵营"点（A-18）
    [HarmonyPatch(typeof(Hit), "DeliverHit")]
    internal static class KillAttributionPatch
    {
        private static void Postfix(Hit __instance)
        {
            try
            {
                GameObject target = Traverse.Create(__instance).Field("target").GetValue<GameObject>();
                AttackProperties props = Traverse.Create(__instance).Field("properties").GetValue<AttackProperties>();
                if (target == null || props == null || props.attacker == null)
                {
                    return;
                }
                FactionAlignment fa = props.attacker.GetComponent<FactionAlignment>();
                if (fa != null)
                {
                    SpaceKillTracker.Mark(target, fa.Alignment);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 击杀归属记录失败");
            }
        }
    }
}

namespace MoreFood_KFC
{
    internal static class SpaceKillTracker
    {
        private sealed class Hit
        {
            internal float at;
            internal FactionManager.FactionID faction;
        }

        private static readonly Dictionary<int, Hit> Kills = new Dictionary<int, Hit>();
        internal const float WindowSeconds = 30f;

        internal static void Mark(GameObject target, FactionManager.FactionID faction)
        {
            if (target == null)
            {
                return;
            }
            Hit h = new Hit();
            h.at = Time.unscaledTime;
            h.faction = faction;
            Kills[target.GetInstanceID()] = h;
        }

        internal static bool WasKilledByPlayer(GameObject target)
        {
            if (target == null)
            {
                return false;
            }
            Hit h;
            if (!Kills.TryGetValue(target.GetInstanceID(), out h))
            {
                return false;
            }
            if (Time.unscaledTime - h.at > WindowSeconds)
            {
                return false;
            }
            return h.faction == FactionManager.FactionID.Duplicant;
        }

        internal static int Count()
        {
            return Kills.Count;
        }
    }

    internal sealed class SpaceHost : KMonoBehaviour
    {
        internal static SpaceHost Instance;

        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            Instance = this;
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:SpaceHost", e); }
        }

        protected override void OnCleanUp()
        {
            try
            {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:SpaceHost", e); }
        }
    }

    [HarmonyPatch(typeof(ClusterTraveler), "AdvancePathOneStep")]
    internal static class SpaceCraftStepPatch
    {
        private static void Postfix(ClusterTraveler __instance)
        {
            try
            {
                SpaceCraftNotify.OnMoved(__instance);
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceCraftStepPatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(Clustercraft), "SetCraftStatus")]
    internal static class SpaceCraftStatusPatch
    {
        private static void Postfix(Clustercraft __instance)
        {
            // 着陆 → 立刻清掉星图上残留的飞行路线/箭头（mapPath 只在状态变化时才会被重绘）
            try
            {
                if (__instance != null && __instance.Status == Clustercraft.CraftStatus.Grounded)
                {
                    StarMapPathCleanup.OnLanded(__instance);
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceCraftStatusPatch.Path", e);
            }
            try
            {
                SpaceCraftNotify.OnMoved(__instance != null
                    ? __instance.GetComponent<ClusterTraveler>() : null);
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceCraftStatusPatch", e);
            }
        }
    }

    internal static class SpaceCraftNotify
    {
        internal static void OnMoved(ClusterTraveler traveler)
        {
            try
            {
                if (traveler == null)
                {
                    return;
                }
                Clustercraft craft = traveler.GetComponent<Clustercraft>();
                if (craft == null)
                {
                    return;
                }
                // 设计变更：这里不再刷新按钮排。交易/加油的可用态只在「显示前」现算 +
                //   「点击时」校验，避免依赖事件导致漏刷新/错误点亮。
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 跨格通知异常");
            }
        }
    }

    internal static class SpaceLoadTrace
    {
        private static readonly System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        private static bool active;

        internal static bool Active
        {
            get { return active; }
        }

        internal static void Begin(string p)
        {
            try
            {
                active = true;
                sw.Reset();
                sw.Start();
                Set(p);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 装载探针启动失败");
            }
        }

        internal static void Set(string p)
        {
            try
            {
                // 性能：仅在装载阶段（active）写 Debug.Log。
                if (!active)
                {
                    return;
                }
                MFLog.Verbose(LogCat.Space,"[Space] 装载阶段：" + p + "（+" + sw.Elapsed.TotalSeconds.ToString("0.0") + "s）");
            }
            catch (Exception)
            {
                // expected: 探针写入失败不影响装载流程
            }
        }

        internal static void Stop()
        {
            active = false;
            sw.Stop();
        }
    }

    internal static class SpaceReloadReset
    {
        internal static void Wipe(string why)
        {
            try
            {
                CombatRuntime.ResetRuntimeForLoad();
                CombatBars.ResetForLoad();
                CombatUi.ResetForLoad();
                SpaceTestRuntime.ResetForLoad();
                StarMapVisuals.ResetForLoad();
                SpaceTimers.ResetForLoad();
                SpaceLootPools.ClearStatics();
                SpaceAchievements.ResetForSession();
                MFLog.Verbose(LogCat.Space,"[Space] 读档前清空运行时静态状态（" + why + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 清空运行时静态状态异常");
            }
        }
    }

    [HarmonyPatch(typeof(Resources), "UnloadUnusedAssets")]
    internal static class SpaceUnloadTracePatch
    {
        private static void Prefix()
        {
            try
            {
                if (SpaceLoadTrace.Active)
                {
                    SpaceLoadTrace.Set("资源卸载开始");
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceUnloadTracePatch", e);
            }
        }

        private static void Postfix()
        {
            try
            {
                if (SpaceLoadTrace.Active)
                {
                    SpaceLoadTrace.Set("资源卸载结束");
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceUnloadTracePatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoader), "Load", new Type[] { typeof(IReader) })]
    internal static class SpaceLoadCompletePatch
    {
        private static void Prefix()
        {
            try
            {
                SpaceReloadReset.Wipe("读档");
                SpaceLoadTrace.Begin("读档 Load 开始");
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceLoadCompletePatch", e);
            }
        }

        private static void Postfix(bool __result)
        {
            try
            {
                SpaceLoadTrace.Set("读档 Load 返回 " + __result);
                if (__result)
                {
                    SpaceTimers.OnLoadComplete("读档");
                    ModPerfProbe.RefreshFromOptions();
                }
                SpaceLoadTrace.Stop();
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceLoadCompletePatch", e);
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoader), "LoadFromWorldGen")]
    internal static class SpaceLoadNewGamePatch
    {
        private static void Prefix()
        {
            try
            {
                SpaceReloadReset.Wipe("新档");
                SpaceLoadTrace.Begin("新档 WorldGen 开始");
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceLoadNewGamePatch", e);
            }
        }

        private static void Postfix(bool __result)
        {
            try
            {
                SpaceLoadTrace.Set("新档 WorldGen 返回 " + __result);
                if (__result)
                {
                    SpaceTimers.OnLoadComplete("新档");
                }
                SpaceLoadTrace.Stop();
            }
            catch (Exception e)
            {
                MFLog.HotFail("patch:SpaceLoadNewGamePatch", e);
            }
        }
    }

    internal static class SpaceTimers
    {
        private static bool armed;
        private static float lastWatchdogAt;
        private const float WatchBaseSeconds = 30f;
        private const float WatchFactor = 2f;
        private const float WatchMinGap = 5f;
        private const float WatchPatrolFactor = 1f;
        private const float WatchMinPatrol = 5f;
        private const float WatchMaxPatrol = 600f;
        private static float lastPatrolPeriod = 600f;
        private static readonly Dictionary<string, Watch> watches =
            new Dictionary<string, Watch>(StringComparer.Ordinal);

        private sealed class Watch
        {
            internal string name;
            internal float gap;
            internal float last;
            internal int gen;
            internal System.Action restart;
        }

        private static bool postLoadPending;
        private static int staleGameId;
        private static bool gridReadyAfterLoad;

        internal static void OnLoadComplete(string source)
        {
            armed = false;
            staleGameId = Game.Instance != null ? Game.Instance.GetInstanceID() : 0;
            gridReadyAfterLoad = source == "新档" && ClusterGrid.Instance != null;
            postLoadPending = true;
            MFLog.Verbose(LogCat.Space,"[Space] 装载完成（" + source + "）：等待新世界网格就绪后再做一次性初始化"
                + "（网格已在=" + gridReadyAfterLoad + "）");
            SchedulePostLoad(0);
        }

        internal static void OnGridReadyAfterLoad()
        {
            gridReadyAfterLoad = true;
            if (!postLoadPending)
            {
                return;
            }
            MFLog.Verbose(LogCat.Space,"[Space] 新世界网格已就绪，安排装载后初始化");
            SchedulePostLoad(0);
        }

        internal static void ResetForLoad()
        {
            armed = false;
            watches.Clear();
            lastWatchdogAt = 0f;
            lastPatrolPeriod = 600f;
            postLoadPending = false;
            staleGameId = 0;
            gridReadyAfterLoad = false;
        }

        private static void SchedulePostLoad(int attempt)
        {
            postLoadPending = true;
            try
            {
                if (UIScheduler.Instance != null)
                {
                    UIScheduler.Instance.ScheduleNextFrame("SpacePostLoad", delegate(object o)
                    {
                        RunPostLoad(attempt);
                    }, null);
                    return;
                }
                if (GameScheduler.Instance != null)
                {
                    GameScheduler.Instance.Schedule("SpacePostLoad", 0f, delegate(object o)
                    {
                        RunPostLoad(attempt);
                    }, null, null);
                    return;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 装载后初始化排程失败");
            }
            if (Game.Instance != null || attempt >= 3)
            {
                RunPostLoad(attempt);
                return;
            }
            MFLog.Verbose(LogCat.Space,"[Space] 游戏实例还没建好（Game.OnSpawn 之前），装载后初始化推迟到 OnSpawn 再跑");
        }

        internal static void RunPendingPostLoad()
        {
            if (!postLoadPending)
            {
                return;
            }
            MFLog.Verbose(LogCat.Space,"[Space] Game.OnSpawn 到达，装载后初始化等网格就绪");
            SchedulePostLoad(0);
        }

        private static void RunPostLoad(int attempt)
        {
            bool newGame = Game.Instance != null && Game.Instance.GetInstanceID() != staleGameId;
            if (!newGame || !gridReadyAfterLoad)
            {
                if (attempt < 240)
                {
                    SchedulePostLoad(attempt + 1);
                    return;
                }
                MFLog.Warn(LogCat.Space,"[Space] 装载后初始化等不到新世界（新 Game=" + newGame
                    + " 网格就绪=" + gridReadyAfterLoad + "），按当前状态执行");
            }
            postLoadPending = false;
            try
            {
                SpaceQuestWorldFlags.NormalizeAll();
                SpaceSaveHost.LoadFinished = true;
                ImpactorGate.SyncWorldState();   // 装载对账：在途灾星补记闸口（事件.md §17.6）
                SpaceSaveHost.FindOrCreate();
                SpaceRestoreGate.RunDeferredRestores();
                SpaceQuestRuntime.CleanupOrphanSites();
                // 子图小行星带只在装载完成后铺一次
                AsteroidBeltPool.EnsureSubSpawn(ClusterGrid.Instance, StarMapVisuals.Zones);
                MFLog.Verbose(LogCat.Space,"[Space] 装载后一次性初始化完成（宿主/延迟恢复/孤儿任务点）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 装载后初始化异常");
            }
            ArmAll();
            SpaceLoadTrace.Set("装载后初始化完成");
            SpaceLoadTrace.Stop();
        }

        internal static void ArmAll()
        {
            if (armed)
            {
                return;
            }
            armed = true;
            ArmCombat();
            ArmQuestWake();
            ArmPirates();
            ArmWatchdog();
            ArmSpawner(CombatRuntime.MainSpawner);
            ArmSpawner(CombatRuntime.SubSpawner);
            MFLog.Verbose(LogCat.Space,"[Space] 各系统闹钟已挂载（战斗 " + CombatDb.Shared.tickSeconds
                + " 秒 / 任务与事件点按各自截止时刻 / 海盗站 60 秒 / 抽取器各自间隔）");
        }

        internal static void ArmPirates()
        {
            const float period = 60f;
            int gen = BeginChain("SpacePirates", period, ArmPirates);
            TickChain("SpacePirates", gen, period, delegate
            {
                try
                {
                    TradeRegistry.TickPirates();
                }
                finally
                {
                    ArmPirates();
                }
            });
        }

        internal static void ArmCombat()
        {
            float period = Mathf.Max(1f, CombatDb.Shared.tickSeconds);
            int gen = BeginChain("SpaceCombat", period, ArmCombat);
            TickChain("SpaceCombat", gen, period, delegate
            {
                try
                {
                    CombatRuntime.Tick(TradeRegistry.Now());
                }
                finally
                {
                    ArmCombat();
                }
            });
        }

        internal static void ArmQuestWake()
        {
            float now = TradeRegistry.Now();
            float next = SpaceQuestRuntime.NextWakeAt();
            float delay = next >= 1e9f ? 60f : Mathf.Max(0.5f, next - now);
            int gen = BeginChain("SpaceQuestWake", delay, ArmQuestWake);
            TickChain("SpaceQuestWake", gen, delay, delegate
            {
                try
                {
                    SpaceQuestRuntime.Tick();
                }
                finally
                {
                    ArmQuestWake();
                }
            });
        }

        internal static void ArmSpawner(SpawnerState sp)
        {
            if (sp == null || sp.cfg == null)
            {
                return;
            }
            float now = TradeRegistry.Now();
            if (sp.nextSpawnAt <= 0f)
            {
                sp.nextSpawnAt = now
                    + UnityEngine.Random.Range(sp.cfg.spawnIntervalMin, sp.cfg.spawnIntervalMax);
            }
            float delay = sp.paused ? 1f : Mathf.Max(0.5f, sp.nextSpawnAt - now);
            string name = "SpaceSpawner_" + sp.cfg.id;
            MFLog.Verbose(LogCat.Space,"[Space] 抽取器 " + sp.cfg.id + " 闹钟 " + Mathf.Round(delay)
                + " 秒后到点（暂停=" + sp.paused + "）");
            int gen = BeginChain(name, delay, delegate { ArmSpawner(sp); });
            TickChain(name, gen, delay, delegate
            {
                try
                {
                    CombatRuntime.TickSpawnerAlarm(sp);
                }
                finally
                {
                    ArmSpawner(sp);
                }
            });
        }

        internal static void ArmWatchdog()
        {
            float period = NextPatrolDelay();
            lastPatrolPeriod = period;
            int gen = BeginChain("SpaceWatchdog", period, ArmWatchdog);
            TickChain("SpaceWatchdog", gen, period, delegate
            {
                try
                {
                    CheckWatchdog();
                }
                finally
                {
                    ArmWatchdog();
                }
            });
        }

        private static float NextPatrolDelay()
        {
            float soonest = float.MaxValue;
            foreach (KeyValuePair<string, Watch> kv in watches)
            {
                Watch w = kv.Value;
                if (w == null || w.name == "SpaceWatchdog" || w.restart == null)
                {
                    continue;
                }
                float threshold = ThresholdOf(w);
                if (threshold < soonest)
                {
                    soonest = threshold;
                }
            }
            if (soonest >= 1e9f)
            {
                return WatchMaxPatrol;
            }
            return Mathf.Clamp(soonest * WatchPatrolFactor, WatchMinPatrol, WatchMaxPatrol);
        }

        private static void CheckWatchdog()
        {
            float now = TradeRegistry.Now();
            float selfSilent = lastWatchdogAt > 0f ? now - lastWatchdogAt : 0f;
            lastWatchdogAt = now;

            List<Watch> stale = new List<Watch>();
            foreach (KeyValuePair<string, Watch> kv in watches)
            {
                Watch w = kv.Value;
                if (w == null || w.name == "SpaceWatchdog" || w.restart == null)
                {
                    continue;
                }
                if (now - w.last <= ThresholdOf(w))
                {
                    continue;
                }
                stale.Add(w);
            }
            if (stale.Count == 0 && selfSilent <= lastPatrolPeriod * 3f)
            {
                return;
            }

            bool schedAlive = GameScheduler.Instance != null;
            bool hostAlive = SpaceHost.Instance != null;
            bool loaded = SpaceSaveHost.LoadFinished;
            MFLog.Warn(LogCat.Space,"[Space] 看门狗初步诊断异常：静默链 " + stale.Count + " 条 / 自身停顿 "
                + Mathf.Round(selfSilent) + " 秒；进入完整诊断（GameScheduler=" + schedAlive
                + " SpaceHost=" + hostAlive + " 装载完成=" + loaded + " armed=" + armed + "）");

            if (!hostAlive)
            {
                try
                {
                    if (Game.Instance != null)
                    {
                        Game.Instance.gameObject.AddOrGet<SpaceHost>();
                        MFLog.Warn(LogCat.Space,"[Space] 看门狗：SpaceHost 缺失（协程宿主/会话标记），已补挂");
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Space, e, "[Space] 看门狗补挂 SpaceHost 失败");
                }
            }

            int restarted = 0;
            for (int i = 0; i < stale.Count; i++)
            {
                Watch w = stale[i];
                MFLog.Warn(LogCat.Space,"[Space] 调度器 " + w.name + " 静默 " + Mathf.Round(now - w.last)
                    + " 秒（预期休眠 " + Mathf.Round(w.gap) + " → 自查阈值 "
                    + Mathf.Round(ThresholdOf(w)) + " 秒 = max(" + Mathf.Round(WatchMinGap) + ", 预期×"
                    + WatchFactor + ") + " + Mathf.Round(WatchBaseSeconds)
                    + "，代数 " + w.gen + "）→ 判定僵尸链，废弃旧链并重建");
                try
                {
                    w.restart();
                    restarted++;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Space, e, "[Space] 重建调度器 " + w.name + " 失败");
                }
            }

            bool globalBreak = !schedAlive || !loaded || selfSilent > lastPatrolPeriod * 3f || restarted >= 3;
            if (globalBreak)
            {
                MFLog.Warn(LogCat.Space,"[Space] 看门狗完整诊断：判定会话级失效（GameScheduler=" + schedAlive
                    + " 装载完成=" + loaded + " 重建 " + restarted + " 条 / 自身停顿 "
                    + Mathf.Round(selfSilent) + " 秒）→ 全量重建闹钟");
                armed = false;
                ArmAll();
                if (!loaded)
                {
                    OnLoadComplete("看门狗补跑");
                }
            }
        }

        private static float ThresholdOf(Watch w)
        {
            return Mathf.Max(WatchMinGap, w.gap * WatchFactor) + WatchBaseSeconds;
        }

        private static int BeginChain(string name, float gap, System.Action restart)
        {
            Watch w;
            if (!watches.TryGetValue(name, out w))
            {
                w = new Watch();
                w.name = name;
                watches[name] = w;
            }
            w.gap = Mathf.Max(1f, gap);
            w.restart = restart;
            w.last = TradeRegistry.Now();
            return ++w.gen;
        }

        private static void TickChain(string name, int gen, float delay, System.Action work)
        {
            Watch w;
            if (!watches.TryGetValue(name, out w))
            {
                return;
            }
            try
            {
                if (GameScheduler.Instance == null)
                {
                    return;
                }
                GameScheduler.Instance.Schedule(name, Mathf.Max(0f, delay),
                    delegate(object o)
                    {
                        if (w.gen != gen)
                        {
                            return;
                        }
                        w.last = TradeRegistry.Now();
                        try
                        {
                            work();
                        }
                        catch (Exception e)
                        {
                            MFLog.HotFail("space:SpaceTimers.TickChain", e);
                        }
                    }, null, null);
            }
            catch (Exception e)
            {
                MFLog.HotFail("space:SpaceTimers.TickChain.arm", e);
            }
        }
    }
}
