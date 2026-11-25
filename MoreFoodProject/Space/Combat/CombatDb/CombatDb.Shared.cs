// 用途：共享配置应用与文件读写：shared 段、生成器、颜色与文本读取

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatDb
    {
        // shared 应用

        private static void ApplyCombatShared(CombatSharedRaw r)
        {
            if (r == null) return;
            Shared.minAttackInterval = r.min_attack_interval ?? Shared.minAttackInterval;
            Shared.maxAttacksPerSecond = r.max_attacks_per_second ?? Shared.maxAttacksPerSecond;
            Shared.combatMemorySeconds = r.combat_memory_seconds ?? Shared.combatMemorySeconds;
            Shared.disengageRangeMult = r.disengage_range_mult ?? Shared.disengageRangeMult;
            Shared.disengageSeconds = r.disengage_seconds ?? Shared.disengageSeconds;
            Shared.armorMinPenetration = r.armor_min_penetration ?? Shared.armorMinPenetration;
            Shared.armorOverflowToHp = r.armor_overflow_to_hp ?? Shared.armorOverflowToHp;
            Shared.tickSeconds = Mathf.Clamp(r.tick_seconds ?? Shared.tickSeconds, 1, 60);
            Shared.spawnEdgeTries = Mathf.Clamp(r.spawn_edge_tries ?? Shared.spawnEdgeTries, 1, 64);
            Shared.crewSyncHpPct = Mathf.Clamp01(r.crew_sync_hp_pct ?? Shared.crewSyncHpPct);
            Shared.crewDamageRatio = Mathf.Clamp01(r.crew_damage_ratio ?? Shared.crewDamageRatio);
            Shared.crewFloorHpPct = Mathf.Clamp01(r.crew_floor_hp_pct ?? Shared.crewFloorHpPct);
        }

        private static void ApplyMovementShared(MovementSharedRaw r)
        {
            if (r == null) return;
            Shared.defaultSpeed = r.default_speed ?? Shared.defaultSpeed;
        }

        private static void ApplyRegenShared(RegenSharedRaw r)
        {
            if (r == null) return;
            Shared.regenInterval = r.default_interval_seconds ?? Shared.regenInterval;
            Shared.regenHpPct = r.default_hp_pct ?? Shared.regenHpPct;
            Shared.regenArmorPct = r.default_armor_pct ?? Shared.regenArmorPct;
            Shared.regenStopWhenFull = r.stop_when_full ?? Shared.regenStopWhenFull;
        }

        private static void ApplyLootShared(LootSharedRaw r)
        {
            if (r == null) return;
            Shared.defaultFieldAnim = string.IsNullOrEmpty(r.default_field_anim) ? Shared.defaultFieldAnim : r.default_field_anim;
            Shared.kgRound = string.IsNullOrEmpty(r.kg_round) ? Shared.kgRound : r.kg_round;
            Shared.invalidItemSkip = r.invalid_item_skip ?? Shared.invalidItemSkip;
            Shared.chanceClamp = r.chance_clamp ?? Shared.chanceClamp;
        }

        private static void ApplyAmmoShared(AmmoSharedRaw r)
        {
            if (r == null) return;
            Shared.ammoDefaultInterval = r.default_attack_interval ?? Shared.ammoDefaultInterval;
            Shared.ammoDefaultRangeBonus = r.default_range_bonus ?? Shared.ammoDefaultRangeBonus;
            Shared.ammoDefaultAnim = string.IsNullOrEmpty(r.default_anim) ? Shared.ammoDefaultAnim : r.default_anim;
            Shared.ammoDefaultPriority = string.IsNullOrEmpty(r.default_priority) ? Shared.ammoDefaultPriority : r.default_priority;
        }

        private static void ApplyRocketShared(RocketSharedRaw r)
        {
            if (r == null) return;
            Shared.minModuleHp = Mathf.Max(0, r.min_module_hp ?? Shared.minModuleHp);
            Shared.settleOnLand = r.settle_on_land ?? Shared.settleOnLand;
            Shared.armorRegenOnLand = r.armor_regen_on_land ?? Shared.armorRegenOnLand;
            Shared.rocketMinHp = r.rocket_min_hp ?? Shared.rocketMinHp;
            if (r.ammo_source_prefabs != null && r.ammo_source_prefabs.Length > 0)
            {
                Shared.ammoSourcePrefabs = r.ammo_source_prefabs;
            }
        }

        private static void ApplyEmergencyShared(EmergencySharedRaw r)
        {
            if (r == null) return;
            Shared.emergencyTriggerHpPct = r.trigger_hp_pct ?? Shared.emergencyTriggerHpPct;
            Shared.passiveOnMinHp = r.passive_on_min_hp ?? Shared.passiveOnMinHp;
            Shared.cooldownCycles = r.cooldown_cycles ?? Shared.cooldownCycles;
            Shared.activeLossMin = r.active_loss_min ?? Shared.activeLossMin;
            Shared.activeLossMax = r.active_loss_max ?? Shared.activeLossMax;
            Shared.passiveLossMin = r.passive_loss_min ?? Shared.passiveLossMin;
            Shared.passiveLossMax = r.passive_loss_max ?? Shared.passiveLossMax;
            Shared.cooldownLossPct = r.cooldown_loss_pct ?? Shared.cooldownLossPct;
            Shared.dropAsLootField = r.drop_as_loot_field ?? Shared.dropAsLootField;
            Shared.lootFieldSourceTag = string.IsNullOrEmpty(r.loot_field_source_tag) ? Shared.lootFieldSourceTag : r.loot_field_source_tag;
            Shared.homeSearchRadius = Mathf.Max(1, r.home_search_radius ?? Shared.homeSearchRadius);
            Shared.warpAnim = string.IsNullOrEmpty(r.warp_anim) ? Shared.warpAnim : r.warp_anim;
            Shared.warpSound = string.IsNullOrEmpty(r.warp_sound) ? Shared.warpSound : r.warp_sound;
        }

        private static void ApplyFxShared(FxSharedRaw r)
        {
            if (r == null) return;
            Shared.fxEnabled = r.enabled ?? Shared.fxEnabled;
            Shared.fxRequireMapOpen = r.require_map_open ?? Shared.fxRequireMapOpen;
            Shared.rocketProjectileRatio = ClampRatio(r.rocket_projectile_ratio ?? Shared.rocketProjectileRatio);
            Shared.monsterProjectileRatio = ClampRatio(r.monster_projectile_ratio ?? Shared.monsterProjectileRatio);
            Shared.fallbackHexRatio = ClampRatio(r.fallback_hex_ratio ?? Shared.fallbackHexRatio);
            Shared.minPixels = r.min_pixels ?? Shared.minPixels;
            Shared.maxPixels = r.max_pixels ?? Shared.maxPixels;
            Shared.flyDuration = r.fly_duration ?? Shared.flyDuration;
            Shared.hitDuration = r.hit_duration ?? Shared.hitDuration;
            Shared.maxConcurrent = Mathf.Max(1, r.max_concurrent ?? Shared.maxConcurrent);
            Shared.jitterDelay = r.jitter_delay ?? Shared.jitterDelay;
            Shared.pelletDelay = r.pellet_delay ?? Shared.pelletDelay;
            Shared.rocketDefaultColor = ColorOf(r.rocket_default_color, Shared.rocketDefaultColor);
            Shared.monsterDefaultColor = ColorOf(r.monster_default_color, Shared.monsterDefaultColor);
            Shared.pelletJitterPx = r.pellet_jitter_px ?? Shared.pelletJitterPx;
        }

        private static void ApplyUiShared(UiSharedRaw r)
        {
            if (r == null) return;
            Shared.uiBarHeight = r.bar_height ?? Shared.uiBarHeight;
            Shared.uiBarY = r.bar_y ?? Shared.uiBarY;
        }

        private static void ApplySaveShared(SaveSharedRaw r)
        {
            if (r == null) return;
            Shared.saveVersion = r.save_version ?? Shared.saveVersion;
        }

        // 工具

        private static SpawnerCfg MaterializeSpawner(SpawnerRaw r, SpawnerCfg d)
        {
            SpawnerCfg c = new SpawnerCfg();
            c.id = string.IsNullOrEmpty(r.id) ? d.id : r.id;
            c.region = string.IsNullOrEmpty(r.region) ? d.region : r.region;
            c.maxMonsters = Mathf.Max(1, r.max_monsters ?? d.maxMonsters);
            if (r.spawn_count != null && r.spawn_count.Length >= 2)
            {
                c.spawnCountMin = Mathf.Max(1, r.spawn_count[0]);
                c.spawnCountMax = Mathf.Max(c.spawnCountMin, r.spawn_count[1]);
            }
            else
            {
                c.spawnCountMin = d.spawnCountMin;
                c.spawnCountMax = d.spawnCountMax;
            }
            // 抽取器间隔硬下限：yaml 写多小都不许突破（防"测试用 1 秒"这类配置把抽取器变成高频轮询）
            if (r.spawn_interval != null && r.spawn_interval.Length >= 2 && r.spawn_interval[1] > 0f)
            {
                c.spawnIntervalMin = Mathf.Max(SpawnerCfg.MinSpawnIntervalSeconds, r.spawn_interval[0]);
                c.spawnIntervalMax = Mathf.Max(c.spawnIntervalMin, r.spawn_interval[1]);
            }
            else
            {
                c.spawnIntervalMin = Mathf.Max(SpawnerCfg.MinSpawnIntervalSeconds, d.spawnIntervalMin);
                c.spawnIntervalMax = Mathf.Max(c.spawnIntervalMin, d.spawnIntervalMax);
            }
            if (r.idle_seconds != null && r.idle_seconds.Length >= 2 && r.idle_seconds[1] > 0f)
            {
                c.idleMin = Mathf.Max(0.5f, r.idle_seconds[0]);
                c.idleMax = Mathf.Max(c.idleMin, r.idle_seconds[1]);
            }
            else
            {
                c.idleMin = d.idleMin;
                c.idleMax = d.idleMax;
            }
            c.idleChance = Mathf.Clamp01(r.idle_chance ?? d.idleChance);
            c.allowCrossRegion = r.allow_cross_region ?? d.allowCrossRegion;
            return c;
        }

        private static float ClampRatio(float v)
        {
            return Mathf.Clamp(v, 0.05f, 0.30f);
        }

        internal static Color ColorOf(float[] a, Color fallback)
        {
            if (a == null || a.Length < 3)
            {
                return fallback;
            }
            return new Color(a[0], a[1], a[2], a.Length > 3 ? a[3] : 1f);
        }

        private static int LoadDir<T>(string dirOrFile, Action<T, string> apply) where T : class
        {
            if (File.Exists(dirOrFile))
            {
                T one = LoadOne<T>(dirOrFile);
                if (one != null)
                {
                    try
                    {
                        apply(one, dirOrFile);
                        return 1;
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Combat, e, "[Space] {0} 解析后处理失败", Path.GetFileName(dirOrFile));
                        ContentDiag.File("combat", dirOrFile, "应用阶段抛异常：" + e.Message, "该文件后续条目未生效");
                    }
                }
                return 0;
            }
            if (!Directory.Exists(dirOrFile))
            {
                MFLog.Verbose(LogCat.Combat, "[Space] 目录不存在，跳过：" + dirOrFile);
                return 0;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(dirOrFile, "*.yaml");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 枚举目录失败 {0}", dirOrFile);
                ContentDiag.File("combat", dirOrFile, "目录枚举失败：" + e.Message, "检查目录权限或是否被占用");
                return 0;
            }
            int ok = 0;
            for (int i = 0; i < files.Length; i++)
            {
                T one = LoadOne<T>(files[i]);
                if (one == null)
                {
                    continue;
                }
                try
                {
                    apply(one, files[i]);
                    ok++;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] {0} 解析后处理失败", Path.GetFileName(files[i]));
                    ContentDiag.File("combat", files[i], "应用阶段抛异常：" + e.Message, "该文件后续条目未生效");
                }
            }
            return ok;
        }

        private static string ReadText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 读取失败 {0}", path);
                ContentDiag.File("combat", path, "读取失败：" + e.Message, "检查文件是否存在、是否被占用、编码是否为 UTF-8");
                return null;
            }
        }

        private static T LoadOne<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }
            string text = ReadText(path);
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                ContentDiag.File("combat", path, "空文件或只有空白", "删掉该文件，或按文档补齐内容");
                return null;
            }
            return YamlLoad.Parse<T>(text, path, "combat");
        }

        private static T ParseOne<T>(string text, string path) where T : class
        {
            return YamlLoad.Parse<T>(text, path, "combat");
        }
    }
}
