// 用途：怪物装配：合并继承链、字段归一化与实体化

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatDb
    {
        // 物化 + 索引

        internal static void Build()
        {
            built = true;
            Monsters.Clear();
            Ammo.Clear();
            Modules.Clear();
            Components.Clear();
            Rockets.Clear();
            monsterById.Clear();
            ammoById.Clear();
            ammoByPrefab.Clear();
            ammoByTag.Clear();
            ammoByCaliber.Clear();
            moduleById.Clear();
            moduleByPrefab.Clear();
            componentByPrefab.Clear();

            BuildMonsters();
            BuildAmmo();
            BuildModules();
            BuildComponents();
            BuildRockets();

            MFLog.Info(LogCat.Combat,"[Space] 战斗索引：monsters " + Monsters.Count + " ammo " + Ammo.Count
                + " modules " + Modules.Count + " components " + Components.Count
                + " rockets " + Rockets.Count);
        }

        private static void BuildMonsters()
        {
            for (int i = 0; i < rawMonsters.Count; i++)
            {
                MonsterRaw raw = rawMonsters[i];
                if (raw == null || string.IsNullOrEmpty(raw.id))
                {
                    continue;
                }
                MonsterRaw merged = MergeWithChain(raw);
                CombatMonsterDef def = MaterializeMonster(merged);
                if (monsterById.ContainsKey(def.id))
                {
                    MFLog.Info(LogCat.Combat,"[Space] combat monster 覆盖：" + def.id);
                    for (int k = 0; k < Monsters.Count; k++)
                    {
                        if (Monsters[k].id == def.id)
                        {
                            Monsters[k] = def;
                            break;
                        }
                    }
                }
                else
                {
                    Monsters.Add(def);
                }
                monsterById[def.id] = def;
            }
            if (Monsters.Count == 0)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 没读到战斗怪物配置（combat/monsters/*.yaml）→ 星图刷怪停摆");
            }
        }

        private static MonsterRaw FindRawMonster(string id)
        {
            for (int i = 0; i < rawMonsters.Count; i++)
            {
                if (rawMonsters[i] != null && rawMonsters[i].id == id)
                {
                    return rawMonsters[i];
                }
            }
            return null;
        }

        private static MonsterRaw MergeWithChain(MonsterRaw raw)
        {
            MonsterRaw m = new MonsterRaw();
            m.id = raw.id;
            m.srcFile = raw.srcFile;
            MergeMonster(m, rawMonsterDefault);
            // 父链：raw.inherit → 父 → 祖父 …（最多 8 层，防环）
            List<MonsterRaw> chain = new List<MonsterRaw>();
            HashSet<string> seen = new HashSet<string>();
            MonsterRaw cur = raw;
            int depth = 0;
            while (cur != null && !string.IsNullOrEmpty(cur.inherit) && depth++ < 8)
            {
                if (!seen.Add(cur.inherit))
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 怪物 " + raw.id + " 的 inherit 链出现环：" + cur.inherit + "，已截断");
                    break;
                }
                MonsterRaw parent = cur.inherit == "monster_default" ? rawMonsterDefault : FindRawMonster(cur.inherit);
                if (parent == null)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 怪物 " + raw.id + " 的 inherit 目标不存在：" + cur.inherit + "（跳过该层）");
                    break;
                }
                chain.Add(parent);
                cur = parent;
            }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                MergeMonster(m, chain[i]);
            }
            MergeMonster(m, raw);
            if (raw != null && raw.@override != null)
            {
                MergeMonster(m, raw.@override);
            }
            return m;
        }

        private static void MergeMonster(MonsterRaw d, MonsterRaw s)
        {
            if (s == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(s.name)) d.name = s.name;
            if (!string.IsNullOrEmpty(s.description)) d.description = s.description;
            if (!string.IsNullOrEmpty(s.anim)) d.anim = s.anim;
            if (s.hp.HasValue) d.hp = s.hp;
            if (s.armor.HasValue) d.armor = s.armor;
            if (s.defense.HasValue) d.defense = s.defense;
            if (s.attack.HasValue) d.attack = s.attack;
            if (s.armor_penetration.HasValue) d.armor_penetration = s.armor_penetration;
            if (s.attack_interval.HasValue) d.attack_interval = s.attack_interval;
            if (s.attack_range.HasValue) d.attack_range = s.attack_range;
            if (s.alert_range.HasValue) d.alert_range = s.alert_range;
            if (s.deaggro_seconds.HasValue) d.deaggro_seconds = s.deaggro_seconds;
            if (s.move_interval.HasValue) d.move_interval = s.move_interval;
            if (s.speed.HasValue) d.speed = s.speed;
            if (s.hostile.HasValue) d.hostile = s.hostile;
            if (s.chase.HasValue) d.chase = s.chase;
            if (s.can_flee.HasValue) d.can_flee = s.can_flee;
            if (s.flee_hp_pct.HasValue) d.flee_hp_pct = s.flee_hp_pct;
            if (s.flee_instant_step_once.HasValue) d.flee_instant_step_once = s.flee_instant_step_once;
            if (s.idle_seconds != null) d.idle_seconds = s.idle_seconds;
            if (s.spawn_sleep_seconds.HasValue) d.spawn_sleep_seconds = s.spawn_sleep_seconds;
            if (s.leave_after_seconds != null && s.leave_after_seconds.Length >= 2) d.leave_after_seconds = s.leave_after_seconds;
            if (s.idle_chance.HasValue) d.idle_chance = s.idle_chance;
            if (s.regen != null) d.regen = s.regen;
            if (!string.IsNullOrEmpty(s.tier)) d.tier = s.tier;
            if (s.fx_color != null) d.fx_color = s.fx_color;
            if (s.spawn != null) d.spawn = s.spawn;
            if (!string.IsNullOrEmpty(s.spawn_region)) d.spawn_region = s.spawn_region;
            if (s.loot_field != null) d.loot_field = s.loot_field;
            if (s.loot != null) d.loot = s.loot;
        }

        private static CombatMonsterDef MaterializeMonster(MonsterRaw r)
        {
            CombatMonsterDef d = new CombatMonsterDef();
            d.id = r.id;
            d.name = string.IsNullOrEmpty(r.name) ? r.id : r.name;
            d.description = r.description;
            d.anim = r.anim;
            if (r.hp.HasValue && r.hp.Value <= 0f)
            {
                ContentDiag.Fix("combat", r.srcFile, "「" + r.id + "」", "hp=" + r.hp.Value + "（必须 > 0）", "100");
            }
            d.hp = r.hp.HasValue && r.hp.Value > 0f ? r.hp.Value : 100f;
            d.armor = Mathf.Max(0f, r.armor ?? 0f);
            d.defense = Mathf.Max(0f, r.defense ?? 0f);
            d.attack = r.attack ?? 10f;
            d.armorPenetration = r.armor_penetration ?? 5f;
            d.attackInterval = r.attack_interval.HasValue && r.attack_interval.Value > 0f ? r.attack_interval.Value : 10f;
            // ★ 攻击间隔钳制到 [min_attack_interval, attackIntervalMax]（当前配置 6~25 秒，再由 5 秒节拍对齐成 10/15/20/25）
            //   伤害按 DPS 不变缩放（每场总输出不变，问题5）
            {
                float oldInt = d.attackInterval;
                float newInt = Mathf.Clamp(oldInt, Shared.minAttackInterval, Shared.attackIntervalMax);
                if (oldInt > 0f && Mathf.Abs(newInt - oldInt) > 0.001f)
                {
                    ContentDiag.Fix("combat", r.srcFile, "「" + r.id + "」",
                        "attack_interval=" + oldInt + "s 超出 [" + Shared.minAttackInterval + ", " + Shared.attackIntervalMax + "]（伤害已按 DPS 等比缩放）",
                        newInt + "s");
                    d.attack *= newInt / oldInt;
                    d.attackInterval = newInt;
                }
            }
            d.attackRange = Mathf.Max(1, r.attack_range ?? 1);
            // ★ alert_range 默认跟随 attack_range（不配置 = 与攻击距离相同）
            d.alertRange = Mathf.Max(1, r.alert_range ?? d.attackRange);
            d.deaggroSeconds = Mathf.Max(0f, r.deaggro_seconds ?? 20f);
            d.moveInterval = r.move_interval.HasValue && r.move_interval.Value > 0f
                ? r.move_interval.Value : d.moveInterval;
            d.speed = r.speed.HasValue && r.speed.Value > 0f ? r.speed.Value : Shared.defaultSpeed;
            d.hostile = r.hostile ?? false;
            d.chase = r.chase ?? true;
            d.canFlee = r.can_flee ?? false;   // ★ 默认不逃跑（用户口径：不配置 = False）
            d.fleeHpPct = Mathf.Clamp01(r.flee_hp_pct ?? 0.25f);
            d.fleeInstantStepOnce = r.flee_instant_step_once ?? true;
            // 漫游切换间隔默认 [600,3600] 秒（与 CombatMonsterDef.idleMin/idleMax 保持一致）
            float iMin = 600f;
            float iMax = 3600f;
            if (r.idle_seconds != null && r.idle_seconds.Length >= 2 && r.idle_seconds[1] > 0f)
            {
                iMin = r.idle_seconds[0] > 0f ? r.idle_seconds[0] : 600f;
                iMax = r.idle_seconds[1] > 0f ? r.idle_seconds[1] : iMin;
                if (iMax < iMin)
                {
                    ContentDiag.Fix("combat", r.srcFile, "「" + r.id + "」",
                        "idle_seconds=[" + iMin + ", " + iMax + "] 下限大于上限", "[" + iMax + ", " + iMin + "]");
                    float t = iMin;
                    iMin = iMax;
                    iMax = t;
                }
            }
            d.idleMin = iMin;
            d.idleMax = iMax;
            d.idleChance = Mathf.Clamp01(r.idle_chance ?? 0.4f);
            d.spawnSleepSeconds = Mathf.Max(0f, r.spawn_sleep_seconds ?? 300f);
            // ★ 离场时机：leave_after_seconds = [下限,上限]（秒），超出 [600, 60000] 直接钳制
            if (r.leave_after_seconds != null && r.leave_after_seconds.Length >= 2 && r.leave_after_seconds[1] > 0f)
            {
                float lMinRaw = r.leave_after_seconds[0];
                float lMaxRaw = r.leave_after_seconds[1];
                float lMin = Mathf.Clamp(lMinRaw, CombatMonsterDef.LeaveAfterFloorSeconds, CombatMonsterDef.LeaveAfterCeilSeconds);
                float lMax = Mathf.Clamp(lMaxRaw, CombatMonsterDef.LeaveAfterFloorSeconds, CombatMonsterDef.LeaveAfterCeilSeconds);
                if (Mathf.Abs(lMin - lMinRaw) > 0.01f || Mathf.Abs(lMax - lMaxRaw) > 0.01f)
                {
                    ContentDiag.Fix("combat", r.srcFile, "「" + r.id + "」",
                        "leave_after_seconds=[" + lMinRaw + ", " + lMaxRaw + "] 超出 [" + CombatMonsterDef.LeaveAfterFloorSeconds + ", " + CombatMonsterDef.LeaveAfterCeilSeconds + "]",
                        "[" + lMin + ", " + lMax + "]");
                }
                if (lMax < lMin)
                {
                    ContentDiag.Fix("combat", r.srcFile, "「" + r.id + "」",
                        "leave_after_seconds 下限大于上限", "[" + lMax + ", " + lMin + "]");
                    float t = lMin;
                    lMin = lMax;
                    lMax = t;
                }
                d.leaveAfterMin = lMin;
                d.leaveAfterMax = lMax;
            }
            else
            {
                // ★ 不配置 = 默认窗口 [3600, 60000]（6~100 周期随机离场），不再"永不离场"占名额
                d.leaveAfterMin = CombatMonsterDef.LeaveAfterFloorSeconds;
                d.leaveAfterMax = CombatMonsterDef.LeaveAfterCeilSeconds;
            }
            MonsterRegenRaw rg = r.regen;
            d.regenInterval = rg != null && rg.interval_seconds.HasValue && rg.interval_seconds.Value > 0f
                ? rg.interval_seconds.Value : Shared.regenInterval;
            d.regenHpPct = rg != null && rg.hp_pct.HasValue ? rg.hp_pct.Value : Shared.regenHpPct;
            d.regenArmorPct = rg != null && rg.armor_pct.HasValue ? rg.armor_pct.Value : Shared.regenArmorPct;
            d.regenStopWhenFull = rg != null && rg.stop_when_full.HasValue ? rg.stop_when_full.Value : Shared.regenStopWhenFull;
            d.tier = string.IsNullOrEmpty(r.tier) ? "F" : r.tier;
            d.fxColor = ColorOf(r.fx_color, Shared.monsterDefaultColor);
            d.spawnRegion = NormalizeSpawnRegion(r.spawn_region, r.id);
            if (r.spawn != null)
            {
                d.spawn.mainSet = r.spawn.main.HasValue;
                d.spawn.subSet = r.spawn.sub.HasValue;
                d.spawn.main = Mathf.Max(0f, r.spawn.main ?? 0f);
                d.spawn.sub = Mathf.Max(0f, r.spawn.sub ?? 0f);
                d.spawn.minIntervalCycles = Mathf.Max(0, r.spawn.min_interval_cycles ?? 0);
                d.spawn.maxAlive = Mathf.Max(0, r.spawn.max_alive ?? 0);
            }
            if (r.loot_field != null)
            {
                LootFieldCfg lf = new LootFieldCfg();
                lf.name = string.IsNullOrEmpty(r.loot_field.name) ? (d.name + "残骸带") : r.loot_field.name;
                lf.beltStyle = r.loot_field.belt_style;   // ★ 外观风格（问题6）
                d.lootField = lf;
            }
            if (r.loot != null)
            {
                for (int i = 0; i < r.loot.Count; i++)
                {
                    LootEntryRaw le = r.loot[i];
                    if (le == null)
                    {
                        continue;
                    }
                    bool hasItem = !string.IsNullOrEmpty(le.item);
                    bool hasGroup = !string.IsNullOrEmpty(le.group);
                    if (!hasItem && !hasGroup)
                    {
                        ContentDiag.Skip("combat", r.srcFile, "怪物「" + d.id + "」掉落第 " + (i + 1) + " 条",
                            "既没有 item 也没有 group", "写 item: <物品ID>（固定）或 group: <池组名>（随机）");
                        continue;
                    }
                    if (hasItem && hasGroup)
                    {
                        MFLog.Warn(LogCat.Combat,"[Space] 怪物 " + d.id + " 掉落第 " + (i + 1)
                            + " 条同时写了 item 与 group → 以 item 为准");
                        hasGroup = false;
                    }
                    // 纯图标条目不是可投放物品，加载期直接拦下并留诊断，避免残骸带掉出图标
                    if (hasItem && IngredientConfigs.IsIconOnlyItem(le.item))
                    {
                        ContentDiag.Skip("combat", r.srcFile, "怪物「" + d.id + "」掉落第 " + (i + 1) + " 条",
                            le.item + " 是纯图标条目（不参与掉落 / 交易）", "换成可拾取物，例如 LargeHaunch");
                        continue;
                    }
                    float chance = le.chance ?? 1f;
                    if (Shared.chanceClamp)
                    {
                        chance = Mathf.Clamp01(chance);
                    }
                    if (chance <= 0f)
                    {
                        continue;
                    }
                    CombatLootEntry e = new CombatLootEntry();
                    e.item = hasItem ? le.item : null;
                    e.group = hasGroup ? le.group.Trim() : null;
                    e.chance = chance;
                    e.hasRange = le.min.HasValue || le.max.HasValue;
                    e.min = Mathf.Max(0f, le.min ?? 1f);
                    e.max = Mathf.Max(e.min, le.max ?? e.min);
                    if (hasGroup)
                    {
                        if (le.picks != null && le.picks.Count > 0)
                        {
                            e.picksLo = Mathf.Max(0, le.picks[0]);
                            e.picksHi = le.picks.Count > 1 ? Mathf.Max(0, le.picks[1]) : e.picksLo;
                        }
                        if (!e.hasRange)
                        {
                            MFLog.Warn(LogCat.Combat,"[Space] 怪物 " + d.id + " 掉落组「" + e.group
                                + "」没写 min/max → 用该组 asteroid_belt.yaml 的 tiers");
                        }
                    }
                    d.loot.Add(e);
                }
            }
            return d;
        }

        private static string NormalizeSpawnRegion(string s, string id)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "both";
            }
            string v = s.Trim().ToLowerInvariant();
            if (v == "main" || v == "sub" || v == "both")
            {
                return v;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 怪物 " + id + " 的 spawn_region '" + s
                + "' 无法识别（main/sub/both）→ 按 both 处理");
            return "both";
        }
    }
}
