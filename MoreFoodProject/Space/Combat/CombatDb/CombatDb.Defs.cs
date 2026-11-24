// 用途：战斗运行时数据结构：怪物/弹药/模块/火箭/生成器/共享配置

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    // 物化模型（严格，运行时用）

    internal sealed class MonsterSpawnCfg
    {
        public float main;
        public float sub;
        public bool mainSet;   // 该区权重是否显式配置（区分"配了 0"与"没配"）
        public bool subSet;
        public int minIntervalCycles;
        public int maxAlive;
    }

    internal sealed class LootFieldCfg
    {
        public string name;
        public string beltStyle;   // ★ 小行星带外观风格（问题6，来自 yaml loot_field.belt_style）
    }

    internal sealed class CombatLootEntry
    {
        public string item;
        // ★ 非空 = 池条目（从该组随机抽 picks 种）；item 非空时为固定条目
        public string group;
        public int picksLo;        // 0 = 用组自己的 picks
        public int picksHi;
        public bool hasRange;      // true = min/max 是显式区间；false = 用组的 tiers
        public float chance = 1f;
        public float min = 1f;
        public float max = 1f;
    }

    internal sealed class CombatMonsterDef
    {
        public string id;
        public string name;
        public string description;
        public string anim;
        public float hp = 100f;
        public float armor;
        public float defense;
        public float attack = 10f;
        public float armorPenetration = 5f;
        public float attackInterval = 3f;
        public int attackRange = 1;
        public int alertRange = 2;
        public float deaggroSeconds = 20f;
        // 追击每格间隔（秒）：0 = 自动按目标火箭每格耗时 ×2.25；>0 = 固定值覆盖（yaml move_interval）
        public float moveInterval = 0f;
        public float speed = 1f;
        public bool hostile;
        public bool chase = true;
        public bool canFlee = false;   // ★ 默认不逃跑（用户口径：不配置 = False）
        public float fleeHpPct = 0.25f;
        public bool fleeInstantStepOnce = true;
        // ★ 离场时机（K18）：yaml leave_after_seconds = [下限,上限]（秒）
        // ★ 不配置 = 取默认窗口 [3600, 60000]（6~100 周期随机），不再是"永不离场"
        public float leaveAfterMin;
        public float leaveAfterMax;
        // ★ 离场时间硬钳制（秒）：yaml 超出直接钳到区间内（下限 3600s = 6 周期，上限 600*100s）
        //   这一对同时就是"不配置时的默认值"
        public const float LeaveAfterFloorSeconds = 3600f;
        public const float LeaveAfterCeilSeconds = 60000f;
        public float idleMin = 360f;
        public float idleMax = 1200f;
        public float idleChance = 0.4f;
        // 生成后的初始睡眠（秒）：0 = 不睡，生成即可移动
        public float spawnSleepSeconds = 300f;
        public float regenInterval = 5f;
        public float regenHpPct = 0.02f;
        public float regenArmorPct = 0.05f;
        public bool regenStopWhenFull = true;
        public string tier = "F";
        public Color fxColor = new Color(1f, 0.35f, 0.30f, 1f);
        public string spawnRegion = "both";   // K17：main / sub / both（不配置 = 都可以刷）
        public MonsterSpawnCfg spawn = new MonsterSpawnCfg();
        public LootFieldCfg lootField;
        public readonly List<CombatLootEntry> loot = new List<CombatLootEntry>();
    }

    internal sealed class AmmoDef
    {
        public string id;
        public string name;
        public string prefabId;
        public string caliber = "small";
        public float shotMassKg = 1f;
        public string[] tags = new string[0];
        public float damage = 100f;
        public float armorPenetration = 10f;
        public float attackInterval = 12f;
        public int rangeBonus;
        public int pellets = 1;
        public float armorDamageMult = 1f;
        public string tier = "F";
        public string anim;
        public Color fxColor = new Color(0.35f, 0.95f, 1f, 1f);
        public string description;
    }

    internal sealed class ModuleStats
    {
        public float fireRateMul = 1f;
        public int rangeBonus;
        public float damageMul = 1f;
        public float armorPenAdd;
        public float armorDamageMul = 1f;
        public int scoutingBonus;
        public float speedMul = 1f;          // 乘积
        public float hpRegenPctPerTick;
        public float shieldRegenPctPerTick;
    }

    internal sealed class ModuleDef
    {
        public string id;
        public string name;
        public string slotType = "utility";
        public string category = "special";
        public string rarity = "common";
        public int slotCost = 1;
        public string caliber;
        public string prefabId;
        public string anim;
        public string description;
        public ModuleStats stats = new ModuleStats();
    }

    internal sealed class SlotEntry
    {
        public string type;
        public int count;
        public int capacity;
    }

    internal sealed class RocketComponentSlots
    {
        public string prefabId;
        public readonly Dictionary<string, SlotEntry> slots =
            new Dictionary<string, SlotEntry>(StringComparer.Ordinal);
    }

    internal sealed class RocketDef
    {
        public string id;
        public string[] matchTags = new string[0];
        public float baseHp;
        public int attackRange = 1;
        public float armor;
        public float defense;
    }

    internal sealed class SpawnerCfg
    {
        // ★ 抽取器间隔硬下限（游戏秒）：yaml 写多小都不许突破（防"测试用 1 秒"把抽取器变成高频轮询）
        public const float MinSpawnIntervalSeconds = 60f;

        public string id;
        public string region;
        public int maxMonsters = 10;
        public int spawnCountMin = 1;
        public int spawnCountMax = 1;
        public float spawnIntervalMin = 800f;
        public float spawnIntervalMax = 1600f;
        public float idleMin = 3f;
        public float idleMax = 10f;
        public float idleChance = 0.4f;
        public bool allowCrossRegion;
    }

    /// <summary>shared/* + config 合并后的运行期通用配置。</summary>
    internal sealed class CombatSharedCfg
    {
        // combat.yaml
        public float minAttackInterval = 10f;   // ★ 攻击间隔下限（秒，10~25 区间下界，原 6f）
        public float attackIntervalMax = 25f;   // ★ 攻击间隔上限（秒，10~25 区间上界，问题5）
        public int maxAttacksPerSecond = 10;
        public float combatMemorySeconds = 20f;
        public float disengageRangeMult = 1.5f;
        public float disengageSeconds = 10f;
        public float armorMinPenetration = 0.001f;
        public bool armorOverflowToHp = true;
        public int tickSeconds = 5;
        public int spawnEdgeTries = 8;
        public float crewSyncHpPct = 0.30f;
        public float crewDamageRatio = 0.20f;
        public float crewFloorHpPct = 0.30f;
        // movement.yaml
        public float defaultSpeed = 1f;
        // regen.yaml
        public float regenInterval = 5f;
        public float regenHpPct = 0.02f;
        public float regenArmorPct = 0.05f;
        public bool regenStopWhenFull = true;
        // loot.yaml
        public string defaultFieldAnim = "gravitas_space_poi_kanim";
        public string kgRound = "round";
        public bool invalidItemSkip = true;
        public bool chanceClamp = true;
        // ammo.yaml
        public float ammoDefaultInterval = 12f;
        public int ammoDefaultRangeBonus;
        public string ammoDefaultAnim = "longrange_missile_clustermap_kanim";
        public string ammoDefaultPriority = "by_damage_desc";
        // rocket.yaml
        public int minModuleHp = 1;
        public bool settleOnLand = true;
        public float armorRegenOnLand;
        public float rocketMinHp = 1f;
        public string[] ammoSourcePrefabs = new string[]
        {
            "SolidCargoBaySmall", "CargoBayCluster", "LiquidCargoBaySmall",
            "LiquidCargoBayCluster", "GasCargoBaySmall", "GasCargoBayCluster"
        };
        // emergency.yaml
        public float emergencyTriggerHpPct = 0.30f;
        public bool passiveOnMinHp = true;
        public float cooldownCycles = 30f;
        public float activeLossMin = 0.15f;
        public float activeLossMax = 0.35f;
        public float passiveLossMin = 0.45f;
        public float passiveLossMax = 0.65f;
        public float cooldownLossPct = 1f;
        public bool dropAsLootField = true;
        public string lootFieldSourceTag = "emergency_drop";
        public int homeSearchRadius = 3;
        public string warpAnim = "wormhole_kanim";
        public string warpSound = "Rocket_Warp";
        // fx.yaml
        public bool fxEnabled = true;
        public bool fxRequireMapOpen = true;
        public float rocketProjectileRatio = 0.30f;
        public float monsterProjectileRatio = 0.30f;
        public float fallbackHexRatio = 0.15f;
        public float minPixels = 4f;
        public float maxPixels = 64f;
        public float flyDuration = 0.22f;
        public float hitDuration = 0.22f;
        public int maxConcurrent = 60;
        public float jitterDelay = 0.05f;
        public float pelletDelay = 0.04f;
        public Color rocketDefaultColor = new Color(0.35f, 0.95f, 1f, 1f);
        public Color monsterDefaultColor = new Color(1f, 0.35f, 0.30f, 1f);
        public float pelletJitterPx = 6f;
        // ui.yaml
        public int uiBarHeight = 48;
        public float uiBarY = 48f;
        // save.yaml
        public int saveVersion = 3;
    }
}
