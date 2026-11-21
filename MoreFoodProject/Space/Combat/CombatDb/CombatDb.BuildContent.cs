// 用途：内容装配：弹药/模块/组件/火箭

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatDb
    {
        private static void BuildAmmo()
        {
            for (int i = 0; i < rawAmmo.Count; i++)
            {
                AmmoRaw r = rawAmmo[i];
                if (r == null || string.IsNullOrEmpty(r.id))
                {
                    ContentDiag.Skip("combat", r != null ? r.srcFile : null, "#" + (i + 1), "弹药缺少 id", "补 id: <唯一短名>");
                    continue;
                }
                string caliber = NormalizeCaliber(r.caliber, r.id);
                if (caliber == null)
                {
                    continue;
                }
                if (ammoById.ContainsKey(r.id))
                {
                    MFLog.Warn("combatammo:" + r.id, LogCat.Combat, "[Space] 弹药 " + r.id + " 重复定义，已忽略后出现的那条");
                    continue;
                }
                AmmoDef d = new AmmoDef();
                d.id = r.id;
                d.name = string.IsNullOrEmpty(r.name) ? r.id : r.name;
                d.prefabId = r.prefab_id;
                d.caliber = caliber;
                d.shotMassKg = r.shot_mass_kg.HasValue && r.shot_mass_kg.Value > 0f
                    ? r.shot_mass_kg.Value : 1f;
                d.tags = r.tags ?? new string[0];
                d.damage = r.damage ?? 100f;
                d.armorPenetration = r.armor_penetration ?? 10f;
                d.attackInterval = r.attack_interval.HasValue && r.attack_interval.Value > 0f
                    ? r.attack_interval.Value : Shared.ammoDefaultInterval;
                // ★ 攻击间隔钳制到 [10,25] 秒；伤害按 DPS 不变缩放（每场总输出不变，问题5）
                {
                    float oldInt = d.attackInterval;
                    float newInt = Mathf.Clamp(oldInt, Shared.minAttackInterval, Shared.attackIntervalMax);
                    if (oldInt > 0f && Mathf.Abs(newInt - oldInt) > 0.001f)
                    {
                        d.damage *= newInt / oldInt;
                        d.attackInterval = newInt;
                    }
                }
                d.rangeBonus = r.range_bonus.HasValue ? r.range_bonus.Value : Shared.ammoDefaultRangeBonus;
                d.pellets = Mathf.Max(1, r.pellets ?? 1);
                d.armorDamageMult = r.armor_damage_mult.HasValue && r.armor_damage_mult.Value > 0f
                    ? r.armor_damage_mult.Value : 1f;
                d.tier = string.IsNullOrEmpty(r.tier) ? "F" : r.tier;
                d.anim = string.IsNullOrEmpty(r.anim) ? Shared.ammoDefaultAnim : r.anim;
                d.fxColor = ColorOf(r.fx_color, Shared.rocketDefaultColor);
                d.description = r.description;

                if (ammoById.ContainsKey(d.id))
                {
                    MFLog.Info(LogCat.Combat,"[Space] combat ammo 覆盖：" + d.id);
                    for (int k = 0; k < Ammo.Count; k++)
                    {
                        if (Ammo[k].id == d.id)
                        {
                            Ammo[k] = d;
                            break;
                        }
                    }
                }
                else
                {
                    Ammo.Add(d);
                }
                ammoById[d.id] = d;
                if (!string.IsNullOrEmpty(d.prefabId))
                {
                    ammoByPrefab[d.prefabId] = d;
                    try
                    {
                        if (Assets.TryGetPrefab(new Tag(d.prefabId)) == null)
                        {
                            MFLog.Warn(LogCat.Combat,"[Space] 弹药 " + d.id + " 的 prefab_id '" + d.prefabId
                                + "' 在游戏里不存在（该弹药不会发射，漏洞44）");
                        }
                    }
                    catch (Exception)
                    {
                        // X8 预期：prefab 校验探测失败不影响弹药定义本身
                    }
                }
                for (int t = 0; t < d.tags.Length; t++)
                {
                    if (!string.IsNullOrEmpty(d.tags[t]) && !ammoByTag.ContainsKey(d.tags[t]))
                    {
                        ammoByTag[d.tags[t]] = d;
                    }
                }
                List<AmmoDef> byCal;
                if (!ammoByCaliber.TryGetValue(d.caliber, out byCal))
                {
                    byCal = new List<AmmoDef>();
                    ammoByCaliber[d.caliber] = byCal;
                }
                bool dup = false;
                for (int k = 0; k < byCal.Count; k++)
                {
                    if (byCal[k] != null && byCal[k].id == d.id)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup)
                {
                    byCal.Add(d);
                }
            }
        }

        private static string NormalizeCaliber(string raw, string id)
        {
            string v = raw == null ? "" : raw.Trim().ToLowerInvariant();
            if (v == "small" || v == "medium" || v == "large")
            {
                return v;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 弹药 " + id + " 的 caliber '" + raw
                + "' 非法（只认 small/medium/large），已丢弃");
            return null;
        }

        private static void BuildModules()
        {
            // 先做 inherit 合并（模块也支持 inherit，如 weapon_laser_mk2）
            for (int i = 0; i < rawModules.Count; i++)
            {
                ModuleRaw r = rawModules[i];
                if (r == null || string.IsNullOrEmpty(r.id))
                {
                    ContentDiag.Skip("combat", r != null ? r.srcFile : null, "#" + (i + 1), "模块缺少 id", "补 id: <唯一短名>");
                    continue;
                }
                if (!string.IsNullOrEmpty(r.inherit))
                {
                    ModuleRaw parent = FindRawModule(r.inherit);
                    if (parent == null)
                    {
                        MFLog.Warn(LogCat.Combat,"[Space] 模块 " + r.id + " 的 inherit 目标不存在：" + r.inherit);
                    }
                    else
                    {
                        MergeModuleInto(parent, r);
                    }
                }
                string slotType = NormalizeSlotType(r.slot_type, r.id);
                if (slotType == null)
                {
                    continue;
                }
                ModuleDef d = new ModuleDef();
                d.id = r.id;
                d.name = string.IsNullOrEmpty(r.name) ? r.id : r.name;
                d.slotType = slotType;
                d.category = string.IsNullOrEmpty(r.category)
                    ? DefaultCategory(slotType) : r.category.Trim();
                d.rarity = NormalizeRarity(r.rarity, r.id);
                int defaultCost = DefaultSlotCost(d.rarity);
                d.slotCost = r.slot_cost.HasValue && r.slot_cost.Value > 0
                    ? r.slot_cost.Value : defaultCost;
                if (r.slot_cost.HasValue && r.slot_cost.Value > 0 && r.slot_cost.Value != defaultCost)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 模块 " + d.id + " 的 slot_cost=" + r.slot_cost.Value
                        + " 与稀有度 " + d.rarity + " 默认值 " + defaultCost + " 不一致，以 yaml 为准");
                }
                d.caliber = NormalizeModuleCaliber(r.caliber, r.id, slotType);
                if (slotType == "weapon" && string.IsNullOrEmpty(d.caliber))
                {
                    continue;
                }
                d.prefabId = r.prefab_id;
                d.anim = r.anim;
                d.description = r.description;
                if (r.stats != null)
                {
                    d.stats.fireRateMul = r.stats.fire_rate_mul.HasValue && r.stats.fire_rate_mul.Value > 0f
                        ? r.stats.fire_rate_mul.Value : 1f;
                    d.stats.rangeBonus = r.stats.range_bonus ?? 0;
                    d.stats.damageMul = r.stats.damage_mul.HasValue && r.stats.damage_mul.Value > 0f
                        ? r.stats.damage_mul.Value : 1f;
                    d.stats.armorPenAdd = Mathf.Max(0f, r.stats.armor_pen_add ?? 0f);
                    d.stats.armorDamageMul = r.stats.armor_damage_mul.HasValue && r.stats.armor_damage_mul.Value > 0f
                        ? r.stats.armor_damage_mul.Value : 1f;
                    d.stats.scoutingBonus = Mathf.Max(0, r.stats.scouting_bonus ?? 0);
                    float sm = r.stats.speed_mul ?? 1f;
                    if (sm <= 0f)
                    {
                        MFLog.Warn(LogCat.Combat,"[Space] 模块 " + d.id + " 的 speed_mul=" + sm
                            + " 非法（应为 >0 的乘数），按 1.0 处理");
                        sm = 1f;
                    }
                    d.stats.speedMul = sm;
                    d.stats.hpRegenPctPerTick = Mathf.Max(0f, r.stats.hp_regen_pct_per_tick ?? 0f);
                    d.stats.shieldRegenPctPerTick = Mathf.Max(0f, r.stats.shield_regen_pct_per_tick ?? 0f);
                }
                if (moduleById.ContainsKey(d.id))
                {
                    MFLog.Info(LogCat.Combat,"[Space] combat module 覆盖：" + d.id);
                    for (int k = 0; k < Modules.Count; k++)
                    {
                        if (Modules[k].id == d.id)
                        {
                            Modules[k] = d;
                            break;
                        }
                    }
                }
                else
                {
                    Modules.Add(d);
                }
                moduleById[d.id] = d;
                if (!string.IsNullOrEmpty(d.prefabId))
                {
                    moduleByPrefab[d.prefabId] = d;
                }
            }
        }

        private static string NormalizeSlotType(string raw, string id)
        {
            string v = raw == null ? "" : raw.Trim().ToLowerInvariant();
            if (v == "weapon" || v == "defense" || v == "utility" || v == "sensor")
            {
                return v;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 模块 " + id + " 的 slot_type '" + raw
                + "' 非法（weapon/defense/utility/sensor），已丢弃");
            return null;
        }

        private static string NormalizeRarity(string raw, string id)
        {
            string v = raw == null ? "" : raw.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(v))
            {
                return "common";
            }
            if (v == "common" || v == "master" || v == "legendary")
            {
                return v;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 模块 " + id + " 的 rarity '" + raw
                + "' 非法（common/master/legendary），按 common 处理");
            return "common";
        }

        private static int DefaultSlotCost(string rarity)
        {
            if (rarity == "legendary") return 3;
            if (rarity == "master") return 2;
            return 1;
        }

        private static string DefaultCategory(string slotType)
        {
            switch (slotType)
            {
                case "weapon": return "weapon";
                case "defense": return "sustain";
                case "sensor": return "sensor";
                default: return "penetration";
            }
        }

        private static string NormalizeModuleCaliber(string raw, string id, string slotType)
        {
            if (slotType != "weapon")
            {
                return raw;
            }
            string v = raw == null ? "" : raw.Trim().ToLowerInvariant();
            if (v == "small" || v == "medium" || v == "large")
            {
                return v;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 武器模块 " + id + " 的 caliber '" + raw
                + "' 非法（只认 small/medium/large），已丢弃");
            return null;
        }

        private static ModuleRaw FindRawModule(string id)
        {
            for (int i = 0; i < rawModules.Count; i++)
            {
                if (rawModules[i] != null && rawModules[i].id == id)
                {
                    return rawModules[i];
                }
            }
            return null;
        }

        /// <summary>把子级未配置的字段用父级补齐（写回子 raw，供后续物化）。</summary>
        private static void MergeModuleInto(ModuleRaw parent, ModuleRaw child)
        {
            if (string.IsNullOrEmpty(child.name)) child.name = parent.name;
            if (string.IsNullOrEmpty(child.slot_type)) child.slot_type = parent.slot_type;
            if (string.IsNullOrEmpty(child.category)) child.category = parent.category;
            if (string.IsNullOrEmpty(child.rarity)) child.rarity = parent.rarity;
            if (!child.slot_cost.HasValue) child.slot_cost = parent.slot_cost;
            if (string.IsNullOrEmpty(child.caliber)) child.caliber = parent.caliber;
            if (string.IsNullOrEmpty(child.prefab_id)) child.prefab_id = parent.prefab_id;
            if (string.IsNullOrEmpty(child.anim)) child.anim = parent.anim;
            if (string.IsNullOrEmpty(child.description)) child.description = parent.description;
            if (child.stats == null)
            {
                child.stats = parent.stats;
            }
            else if (parent.stats != null)
            {
                if (!child.stats.fire_rate_mul.HasValue) child.stats.fire_rate_mul = parent.stats.fire_rate_mul;
                if (!child.stats.range_bonus.HasValue) child.stats.range_bonus = parent.stats.range_bonus;
                if (!child.stats.damage_mul.HasValue) child.stats.damage_mul = parent.stats.damage_mul;
                if (!child.stats.armor_pen_add.HasValue) child.stats.armor_pen_add = parent.stats.armor_pen_add;
                if (!child.stats.armor_damage_mul.HasValue) child.stats.armor_damage_mul = parent.stats.armor_damage_mul;
                if (!child.stats.scouting_bonus.HasValue) child.stats.scouting_bonus = parent.stats.scouting_bonus;
                if (!child.stats.speed_mul.HasValue) child.stats.speed_mul = parent.stats.speed_mul;
                if (!child.stats.hp_regen_pct_per_tick.HasValue) child.stats.hp_regen_pct_per_tick = parent.stats.hp_regen_pct_per_tick;
                if (!child.stats.shield_regen_pct_per_tick.HasValue) child.stats.shield_regen_pct_per_tick = parent.stats.shield_regen_pct_per_tick;
            }
        }

        private static void BuildComponents()
        {
            for (int i = 0; i < rawComponents.Count; i++)
            {
                RocketComponentRaw r = rawComponents[i];
                if (r == null || string.IsNullOrEmpty(r.prefab_id) || r.slots == null)
                {
                    continue;
                }
                RocketComponentSlots d = new RocketComponentSlots();
                d.prefabId = r.prefab_id;
                for (int k = 0; k < r.slots.Count; k++)
                {
                    SlotEntryRaw raw = r.slots[k];
                    if (raw == null || string.IsNullOrEmpty(raw.type))
                    {
                        continue;
                    }
                    string type = raw.type.Trim().ToLowerInvariant();
                    if (type != "weapon" && type != "defense" && type != "utility" && type != "sensor")
                    {
                        MFLog.Warn(LogCat.Combat,"[Space] 组件 " + d.prefabId + " 的槽位类型 '" + raw.type
                            + "' 非法，已跳过该条");
                        continue;
                    }
                    int count = Mathf.Max(0, raw.count ?? 0);
                    int capacity = Mathf.Max(0, raw.capacity ?? 0);
                    if (count == 0 && capacity == 0)
                    {
                        continue;
                    }
                    if (count > 0 && capacity == 0)
                    {
                        MFLog.Warn(LogCat.Combat,"[Space] 组件 " + d.prefabId + " 的 " + type
                            + " 槽 count>0 但 capacity=0（无意义），已跳过该条");
                        continue;
                    }
                    SlotEntry e = new SlotEntry();
                    e.type = type;
                    e.count = count;
                    e.capacity = capacity;
                    d.slots[type] = e;
                }
                if (d.slots.Count == 0)
                {
                    continue;
                }
                if (componentByPrefab.ContainsKey(d.prefabId))
                {
                    MFLog.Info(LogCat.Combat,"[Space] combat component 覆盖：" + d.prefabId);
                    for (int k = 0; k < Components.Count; k++)
                    {
                        if (Components[k].prefabId == d.prefabId)
                        {
                            Components[k] = d;
                            break;
                        }
                    }
                }
                else
                {
                    Components.Add(d);
                }
                componentByPrefab[d.prefabId] = d;
            }
        }

        private static void BuildRockets()
        {
            for (int i = 0; i < rawRockets.Count; i++)
            {
                RocketDefRaw r = rawRockets[i];
                if (r == null || string.IsNullOrEmpty(r.id))
                {
                    continue;
                }
                RocketDef d = new RocketDef();
                d.id = r.id;
                d.matchTags = r.match_tags ?? new string[0];
                if (r.@base != null)
                {
                    d.baseHp = r.@base.base_hp ?? 0f;
                    d.attackRange = Mathf.Max(1, r.@base.attack_range ?? 1);
                    d.armor = r.@base.armor ?? 0f;
                    d.defense = r.@base.defense ?? 0f;
                }
                Rockets.Add(d);
            }
            if (Rockets.Count == 0)
            {
                RocketDef fallback = new RocketDef();
                fallback.id = "default";
                fallback.matchTags = new string[] { "Rocket" };
                Rockets.Add(fallback);
            }
        }
    }
}
