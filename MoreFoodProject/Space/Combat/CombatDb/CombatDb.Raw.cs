// 用途：战斗 YAML 原始模型：shared/monster/ammo/module/rocket 等可空字段

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    // 战斗数据层

    // 原始解析模型（可空）

    internal sealed class CombatSharedRaw
    {
        public float? min_attack_interval { get; set; }
        public int? max_attacks_per_second { get; set; }
        public float? combat_memory_seconds { get; set; }
        public float? disengage_range_mult { get; set; }
        public float? disengage_seconds { get; set; }
        public float? armor_min_penetration { get; set; }
        public bool? armor_overflow_to_hp { get; set; }
        // K6 / K9 增补（缺省走代码默认值）
        public int? tick_seconds { get; set; }
        public int? spawn_edge_tries { get; set; }
        public float? crew_sync_hp_pct { get; set; }
        public float? crew_damage_ratio { get; set; }
        public float? crew_floor_hp_pct { get; set; }
    }

    internal sealed class MovementSharedRaw
    {
        public float? default_speed { get; set; }
    }

    internal sealed class RegenSharedRaw
    {
        public float? default_interval_seconds { get; set; }
        public float? default_hp_pct { get; set; }
        public float? default_armor_pct { get; set; }
        public bool? stop_when_full { get; set; }
    }

    internal sealed class LootEntryRaw
    {
        public string item { get; set; }
        // 与 item 二选一：引用 combat/loot/asteroid_belt.yaml 里的 composition 组名
        public string group { get; set; }
        // 组条目抽几种（[最少, 最多]）；不写则用组自己的 picks
        public List<int> picks { get; set; }
        public float? chance { get; set; }
        public float? min { get; set; }
        public float? max { get; set; }
    }

    internal sealed class LootFieldCfgRaw
    {
        public string name { get; set; }
        public string belt_style { get; set; }   // 小行星带外观风格（问题6，yaml 可配）
    }

    internal sealed class LootSharedRaw
    {
        public string default_field_anim { get; set; }
        public string kg_round { get; set; }
        public bool? invalid_item_skip { get; set; }
        public bool? chance_clamp { get; set; }
        public List<LootEntryRaw> loot { get; set; }
    }

    internal sealed class AmmoSharedRaw
    {
        public float? default_attack_interval { get; set; }
        public int? default_range_bonus { get; set; }
        public string default_anim { get; set; }
        public string default_priority { get; set; }
    }

    internal sealed class RocketSharedRaw
    {
        public int? min_module_hp { get; set; }
        public bool? settle_on_land { get; set; }
        public float? armor_regen_on_land { get; set; }
        public float? rocket_min_hp { get; set; }
        public string[] ammo_source_prefabs { get; set; }
    }

    internal sealed class EmergencySharedRaw
    {
        public float? trigger_hp_pct { get; set; }
        public bool? passive_on_min_hp { get; set; }
        public float? cooldown_cycles { get; set; }
        public float? active_loss_min { get; set; }
        public float? active_loss_max { get; set; }
        public float? passive_loss_min { get; set; }
        public float? passive_loss_max { get; set; }
        public float? cooldown_loss_pct { get; set; }
        public bool? drop_as_loot_field { get; set; }
        public string loot_field_source_tag { get; set; }
        public int? home_search_radius { get; set; }
        public string warp_anim { get; set; }
        public string warp_sound { get; set; }
    }

    internal sealed class FxSharedRaw
    {
        public bool? enabled { get; set; }
        public bool? require_map_open { get; set; }
        public float? rocket_projectile_ratio { get; set; }
        public float? monster_projectile_ratio { get; set; }
        public float? fallback_hex_ratio { get; set; }
        public float? min_pixels { get; set; }
        public float? max_pixels { get; set; }
        public float? fly_duration { get; set; }
        public float? hit_duration { get; set; }
        public int? max_concurrent { get; set; }
        public float? jitter_delay { get; set; }
        public float? pellet_delay { get; set; }
        public float[] rocket_default_color { get; set; }
        public float[] monster_default_color { get; set; }
        public float? pellet_jitter_px { get; set; }
    }

    internal sealed class UiSharedRaw
    {
        public int? bar_height { get; set; }
        public float? bar_y { get; set; }
    }

    internal sealed class SaveSharedRaw
    {
        public int? save_version { get; set; }
    }

    internal sealed class MonsterRegenRaw
    {
        public float? interval_seconds { get; set; }
        public float? hp_pct { get; set; }
        public float? armor_pct { get; set; }
        public bool? stop_when_full { get; set; }
    }

    internal sealed class MonsterSpawnRaw
    {
        public float? main { get; set; }
        public float? sub { get; set; }
        public int? min_interval_cycles { get; set; }
        public int? max_alive { get; set; }
    }

    internal sealed class MonsterRaw
    {
        public string id { get; set; }
        public string inherit { get; set; }
        public string name { get; set; }
        public string description { get; set; }   // 悬停详情用（第五轮）
        public string anim { get; set; }
        public float? hp { get; set; }
        public float? armor { get; set; }
        public float? defense { get; set; }
        public float? attack { get; set; }
        public float? armor_penetration { get; set; }
        public float? attack_interval { get; set; }
        public int? attack_range { get; set; }
        public int? alert_range { get; set; }
        public float? deaggro_seconds { get; set; }
        public float? move_interval { get; set; }
        public float? speed { get; set; }
        public bool? hostile { get; set; }
        public bool? chase { get; set; }
        public bool? can_flee { get; set; }
        public float? flee_hp_pct { get; set; }
        public bool? flee_instant_step_once { get; set; }
        public float[] idle_seconds { get; set; }
        public float? spawn_sleep_seconds { get; set; }
        public float[] leave_after_seconds { get; set; }
        public float? idle_chance { get; set; }
        public MonsterRegenRaw regen { get; set; }
        public string tier { get; set; }
        public float[] fx_color { get; set; }
        public string spawn_region { get; set; }   // K17：main / sub / both（不配置 = both）
        public MonsterSpawnRaw spawn { get; set; }
        public LootFieldCfgRaw loot_field { get; set; }
        public List<LootEntryRaw> loot { get; set; }
        public MonsterRaw @override { get; set; }

        // 来源文件（诊断用；yaml 不映射 internal 字段）
        internal string srcFile;
    }

    internal sealed class MonstersFileDef { public List<MonsterRaw> monsters { get; set; } }
    internal sealed class MonsterDefaultsFile { public MonsterRaw monster_default { get; set; } }

    internal sealed class AmmoRaw
    {
        public string id { get; set; }
        public string name { get; set; }
        public string prefab_id { get; set; }
        public string caliber { get; set; }
        public float? shot_mass_kg { get; set; }
        public string[] tags { get; set; }
        public float? damage { get; set; }
        public float? armor_penetration { get; set; }
        public float? attack_interval { get; set; }
        public int? range_bonus { get; set; }
        public int? pellets { get; set; }
        public float? armor_damage_mult { get; set; }
        public string tier { get; set; }
        public string anim { get; set; }
        public float[] fx_color { get; set; }
        public string description { get; set; }

        // 来源文件（诊断用；yaml 不映射 internal 字段）
        internal string srcFile;
    }

    internal sealed class AmmoFileDef { public List<AmmoRaw> ammo { get; set; } }
    internal sealed class ModuleStatsRaw
    {
        public float? fire_rate_mul { get; set; }
        public int? range_bonus { get; set; }
        public float? damage_mul { get; set; }
        public float? armor_pen_add { get; set; }
        public float? armor_damage_mul { get; set; }
        public int? scouting_bonus { get; set; }
        public float? speed_mul { get; set; }
        public float? hp_regen_pct_per_tick { get; set; }
        public float? shield_regen_pct_per_tick { get; set; }
    }

    internal sealed class ModuleRaw
    {
        public string id { get; set; }
        public string inherit { get; set; }
        public string name { get; set; }
        public string slot_type { get; set; }
        public string category { get; set; }
        public string rarity { get; set; }
        public int? slot_cost { get; set; }
        public string caliber { get; set; }
        public string prefab_id { get; set; }
        public string anim { get; set; }
        public string description { get; set; }
        public ModuleStatsRaw stats { get; set; }

        // 来源文件（诊断用；yaml 不映射 internal 字段）
        internal string srcFile;
    }

    internal sealed class ModulesFileDef { public List<ModuleRaw> modules { get; set; } }

    // components.yaml（组件 → 槽位 count/capacity）
    internal sealed class SlotEntryRaw
    {
        public string type { get; set; }
        public int? count { get; set; }
        public int? capacity { get; set; }
    }

    internal sealed class RocketComponentRaw
    {
        public string prefab_id { get; set; }
        public List<SlotEntryRaw> slots { get; set; }
    }

    internal sealed class RocketComponentsFileDef
    {
        public List<RocketComponentRaw> rocket_components { get; set; }
    }

    internal sealed class RocketBaseRaw
    {
        public float? base_hp { get; set; }
        public int? attack_range { get; set; }
        public float? armor { get; set; }
        public float? defense { get; set; }
    }

    internal sealed class RocketDefRaw
    {
        public string id { get; set; }
        public string[] match_tags { get; set; }
        public RocketBaseRaw @base { get; set; }
    }

    internal sealed class RocketsFileDef { public List<RocketDefRaw> rockets { get; set; } }

    internal sealed class SpawnerRaw
    {
        public string id { get; set; }
        public string region { get; set; }
        public int? max_monsters { get; set; }
        public int[] spawn_count { get; set; }
        public float[] spawn_interval { get; set; }
        public float[] idle_seconds { get; set; }
        public float? idle_chance { get; set; }
        public bool? allow_cross_region { get; set; }
    }

    internal sealed class SpawnControllerFile { public SpawnerRaw spawn_controller { get; set; } }

    internal sealed class LootFieldRaw
    {
        public string id { get; set; }
        public string name { get; set; }
        public string anim { get; set; }
        // 小行星带存活秒数（不写 / 0 = 永久存在；单位：秒）【未实现·计划·待接线：当前不读取】
        public float? lifetime_seconds { get; set; }
        public List<LootEntryRaw> loot { get; set; }
    }

    internal sealed class LootFieldsFileDef { public List<LootFieldRaw> fields { get; set; } }
}
