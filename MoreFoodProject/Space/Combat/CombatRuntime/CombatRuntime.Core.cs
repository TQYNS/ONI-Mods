// 用途：战斗运行时核心：静态字段、载入重置、初始化与预制体注册

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        // 运行时
        internal static readonly List<MonsterRuntime> Monsters = new List<MonsterRuntime>();
        private static readonly Dictionary<int, MonsterRuntime> byInstance = new Dictionary<int, MonsterRuntime>();
        internal static readonly List<SpaceLootField> LootFields = new List<SpaceLootField>();
        internal static readonly List<Clustercraft> Crafts = new List<Clustercraft>();

        internal static SpawnerState MainSpawner;
        internal static SpawnerState SubSpawner;
        internal static int NextUid = 1;
        internal static int LoadEpoch = 1;

        internal static bool inited;
        private static bool busHooked;
        private static GameObject monsterPrefab;
        private static GameObject lootPrefab;
        private static readonly Dictionary<int, AxialI> lastCellByCraft = new Dictionary<int, AxialI>();
        private static readonly HashSet<int> warnedAmmoWhitelist = new HashSet<int>();

        internal const string PrefabId = "SpaceMonster";
        internal const string LootPrefabId = "SpaceLootField";

        internal static void ResetRuntimeForLoad()
        {
            LoadEpoch++;
            Monsters.Clear();
            byInstance.Clear();
            LootFields.Clear();
            Crafts.Clear();
            lastCellByCraft.Clear();
            warnedAmmoWhitelist.Clear();
            lastKillAt.Clear();
            inited = false;
            // 退订事件总线：否则读档/新档后 Init 二次挂接，结算逻辑会被重复执行
            SpaceCombatBus.ClearSubscribers();
            busHooked = false;
            NextUid = 1;
        }

        // 初始化

        internal static void Init()
        {
            if (inited)
            {
                return;
            }
            if (!CombatDb.built)
            {
                return;   // Db 未就绪，下个 Tick 再试
            }
            inited = true;
            MainSpawner = new SpawnerState(AdvancedGameplaySettings.ApplySpawner(CombatDb.SpawnMain));
            SubSpawner = new SpawnerState(AdvancedGameplaySettings.ApplySpawner(CombatDb.SpawnSub));
            EnsureMonsterPrefab();
            EnsureLootPrefab();
            if (!busHooked)
            {
                busHooked = true;
                SpaceCombatBus.OnResolved += SpaceQuestRuntime.OnMonsterResolved;
                SpaceCombatBus.OnEmergencyExit += OnEmergencyExitLog;
            }
            MFLog.Verbose(LogCat.Combat,"[Space] 战斗运行时就绪（怪物池 " + CombatDb.Monsters.Count + " 种，弹药 "
                + CombatDb.Ammo.Count + " 种，模块 " + CombatDb.Modules.Count + " 种，节拍 "
                + CombatDb.Shared.tickSeconds + " 秒）");
            SpaceTimers.ArmSpawner(MainSpawner);
            SpaceTimers.ArmSpawner(SubSpawner);
        }

        internal static void RegisterPrefabs()
        {
            EnsureMonsterPrefab();
            EnsureLootPrefab();
        }

        private static void EnsureMonsterPrefab()
        {
            if (monsterPrefab == null)
            {
                monsterPrefab = SpaceMonster.CreatePrefab(PrefabId, "星图生物");
                MFLog.Verbose(LogCat.Combat,"[Space] 注册 prefab " + PrefabId);
            }
        }

        private static void EnsureLootPrefab()
        {
            if (lootPrefab == null)
            {
                lootPrefab = SpaceLootField.CreatePrefab(LootPrefabId, "小行星带");
                MFLog.Verbose(LogCat.Combat,"[Space] 注册 prefab " + LootPrefabId);
            }
        }

        private static void OnEmergencyExitLog(EmergencyExitEvent e)
        {
            MFLog.Verbose(LogCat.Combat,"[Space] 紧急脱离广播：" + e.craftId + " " + SpaceCfg.Pos(e.fromCell)
                + " → " + SpaceCfg.Pos(e.toCell) + " 被动=" + e.passive + " 冷却中=" + e.coolingDown
                + " 丢货 " + Mathf.Round(e.lossPct * 100f) + "%");
        }
    }
}
