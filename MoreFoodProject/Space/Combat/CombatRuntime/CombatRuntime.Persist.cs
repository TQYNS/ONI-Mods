// 用途：战斗存档：捕获、恢复、战利品回读与清场

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        // 存档（K4/K11）

        internal static void CaptureTo(CombatSaveData save)
        {
            save.combatMonsterIds.Clear();
            save.combatMonsterUids.Clear();
            save.combatMonsterHp.Clear();
            save.combatMonsterArmor.Clear();
            save.combatMonsterCellQ.Clear();
            save.combatMonsterCellR.Clear();
            save.combatMonsterRegion.Clear();
            save.combatMonsterState.Clear();
            save.combatMonsterQuestId.Clear();
            save.combatMonsterOrdered.Clear();
            save.combatMonsterFleeUsed.Clear();
            save.combatMonsterAggroName.Clear();
            save.monsterIdleUntil.Clear();
            save.monsterRegenUntil.Clear();
            save.monsterAttackUntil.Clear();
            save.monsterDeaggroUntil.Clear();
            save.monsterLeaveAt.Clear();      // ★ 忘了清这两个 ⇒ 列表只增不减（读档读到陈年数据）
            save.monsterFleeUntil.Clear();
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt == null || rt.monster == null)
                {
                    continue;
                }
                save.combatMonsterIds.Add(rt.defId);
                save.combatMonsterUids.Add(rt.uid);
                save.combatMonsterHp.Add(Mathf.RoundToInt(rt.hp));
                save.combatMonsterArmor.Add(Mathf.RoundToInt(rt.armor));
                save.combatMonsterCellQ.Add(rt.Cell.Q);
                save.combatMonsterCellR.Add(rt.Cell.R);
                save.combatMonsterRegion.Add(rt.region);
                save.combatMonsterState.Add(rt.state.ToString());
                save.combatMonsterQuestId.Add(rt.questId ?? "");
                save.combatMonsterOrdered.Add(rt.ordered ? 1 : 0);
                save.combatMonsterFleeUsed.Add(rt.fleeUsed ? 1 : 0);
                save.combatMonsterAggroName.Add(rt.aggroCraft != null ? rt.aggroCraft.Name : "");
                save.monsterIdleUntil.Add(rt.idleUntil);
                save.monsterRegenUntil.Add(rt.regenUntil);
                save.monsterAttackUntil.Add(rt.attackUntil);
                save.monsterDeaggroUntil.Add(rt.deaggroUntil);
                save.monsterLeaveAt.Add(rt.leaveAt);
                save.monsterFleeUntil.Add(rt.fleeUntil);
            }
            save.spawnerMainNextAt = MainSpawner != null ? MainSpawner.nextSpawnAt : 0f;
            save.spawnerSubNextAt = SubSpawner != null ? SubSpawner.nextSpawnAt : 0f;
            save.spawnerMainPaused = MainSpawner != null && MainSpawner.paused;
            save.spawnerMainRemain = MainSpawner != null ? MainSpawner.pausedRemain : 0f;
            save.spawnerSubPaused = SubSpawner != null && SubSpawner.paused;
            save.spawnerSubRemain = SubSpawner != null ? SubSpawner.pausedRemain : 0f;

            save.lootFieldCellQ.Clear();
            save.lootFieldCellR.Clear();
            save.lootFieldItemIds.Clear();
            save.lootFieldMasses.Clear();
            save.lootFieldCounts.Clear();
            save.lootFieldSourceTags.Clear();
            save.lootFieldLabels.Clear();
            save.lootFieldGroups.Clear();
            for (int i = 0; i < LootFields.Count; i++)
            {
                SpaceLootField lf = LootFields[i];
                if (lf == null || lf.lootItemIds.Count == 0)
                {
                    continue;
                }
                save.lootFieldCellQ.Add(lf.Location.Q);
                save.lootFieldCellR.Add(lf.Location.R);
                save.lootFieldSourceTags.Add(lf.sourceTag ?? "");
                save.lootFieldLabels.Add(lf.label ?? "");
                save.lootFieldGroups.Add(lf.lootItemIds.Count);   // 切块用：该 field 的条目数
                for (int k = 0; k < lf.lootItemIds.Count; k++)
                {
                    save.lootFieldItemIds.Add(lf.lootItemIds[k]);
                    save.lootFieldMasses.Add(k < lf.lootMasses.Count ? lf.lootMasses[k] : 0f);
                    save.lootFieldCounts.Add(k < lf.lootCounts.Count ? lf.lootCounts[k] : 1);
                }
            }
        }

        internal static int RestoreFrom(CombatSaveData save)
        {
            if (!inited)
            {
                Init();   // CombatDb.Build 已在同一补丁里先跑过；此时初始化抽取器，保住存档里的闹钟
            }
            if (save != null)
            {
                MFLog.Verbose(LogCat.Combat,"[SAVE-TRACE] L4 战斗恢复入口：原始列表 怪物=" + save.combatMonsterIds.Count
                    + " 离场闹钟=" + save.monsterLeaveAt.Count + " 探针=" + save.debugMark);
            }
            ClearAllRuntime();
            if (save == null)
            {
                return 0;
            }
            float now = TradeRegistry.Now();
            SpaceZones zones = StarMapVisuals.Zones;
            if (zones == null || !zones.valid)
            {
                try
                {
                    SpaceTestRuntime.RebuildZones();
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] 恢复前重建分区失败");
                }
                zones = StarMapVisuals.Zones;
            }
            int kept = 0;
            if (save.combatMonsterIds.Count > 0)
            {
                kept = RestoreNewFormat(save, now, zones);
            }
            else
            {
                TradeSaveData legacy = TradeSaveData.Attach();
                if (legacy != null && legacy.monsterIds != null && legacy.monsterIds.Count > 0)
                {
                    kept = RestoreLegacyFormat(legacy, now, zones);
                }
            }
            if (MainSpawner != null)
            {
                // ★ 闹钟不断裂：暂停态原样恢复（等空位通知才继续），计时态保留原到点时间（过期则重排）
                MainSpawner.paused = save.spawnerMainPaused;
                MainSpawner.pausedRemain = Mathf.Max(0f, save.spawnerMainRemain);
                // 到点时间过期时按本局已应用配置重排（不能回读 yaml 原值，否则高级设置被绕过）
                float mainMax = MainSpawner.cfg != null && MainSpawner.cfg.spawnIntervalMax > 0f
                    ? MainSpawner.cfg.spawnIntervalMax
                    : CombatDb.SpawnMain.spawnIntervalMax;
                MainSpawner.nextSpawnAt = save.spawnerMainNextAt > now
                    ? save.spawnerMainNextAt
                    : now + UnityEngine.Random.Range(0f, Mathf.Max(1f, mainMax));
            }
            if (SubSpawner != null)
            {
                SubSpawner.paused = save.spawnerSubPaused;
                SubSpawner.pausedRemain = Mathf.Max(0f, save.spawnerSubRemain);
                // 同上：读档后再排闹钟用已应用配置，保证本局高级设置不被 yaml 原值覆盖
                float subMax = SubSpawner.cfg != null && SubSpawner.cfg.spawnIntervalMax > 0f
                    ? SubSpawner.cfg.spawnIntervalMax
                    : CombatDb.SpawnSub.spawnIntervalMax;
                SubSpawner.nextSpawnAt = save.spawnerSubNextAt > now
                    ? save.spawnerSubNextAt
                    : now + UnityEngine.Random.Range(0f, Mathf.Max(1f, subMax));
            }
            int fields = RestoreLootFields(save);
            SpaceTimers.ArmSpawner(MainSpawner);
            SpaceTimers.ArmSpawner(SubSpawner);
            MFLog.Verbose(LogCat.Combat,"[Space] 战斗读档：怪物 " + kept + " 只，小行星带 " + fields
                + " 个（存档版本 " + CombatDb.Shared.saveVersion + "）");
            return kept;
        }

        private static int RestoreNewFormat(CombatSaveData save, float now, SpaceZones zones)
        {
            int kept = 0;
            for (int i = 0; i < save.combatMonsterIds.Count; i++)
            {
                string defId = save.combatMonsterIds[i];
                if (string.IsNullOrEmpty(defId))
                {
                    continue;
                }
                AxialI cell = SpaceCfg.Mk(GetAt(save.combatMonsterCellQ, i, 0), GetAt(save.combatMonsterCellR, i, 0));
                string region = GetAt(save.combatMonsterRegion, i, null);
                if (string.IsNullOrEmpty(region))
                {
                    region = zones != null && zones.valid && zones.IsSub(cell) ? "sub" : "main";
                }
                float hp = GetAt(save.combatMonsterHp, i, -1);
                float armor = GetAt(save.combatMonsterArmor, i, -1);
                MonsterRuntime rt = SpawnMonsterInternal(defId, cell, GetAt(save.combatMonsterQuestId, i, ""),
                    region, GetAt(save.combatMonsterUids, i, 0), hp, armor,
                    GetAt(save.combatMonsterState, i, null), 0f, 0f, 0f, 0f,
                    GetAt(save.combatMonsterFleeUsed, i, 0) != 0, GetAt(save.combatMonsterAggroName, i, ""));
                if (rt == null)
                {
                    continue;
                }
                rt.ordered = GetAt(save.combatMonsterOrdered, i, 0) != 0;
                // 读档错峰（§17.4）
                float idle = GetAt(save.monsterIdleUntil, i, 0f);
                rt.idleUntil = idle > now ? idle : now + UnityEngine.Random.Range(0f, rt.def.idleMax);
                float regen = GetAt(save.monsterRegenUntil, i, 0f);
                rt.regenUntil = regen > now ? regen : now + UnityEngine.Random.Range(0f, rt.def.regenInterval);
                float attack = GetAt(save.monsterAttackUntil, i, 0f);
                rt.attackUntil = attack > now ? attack : now;
                float deaggro = GetAt(save.monsterDeaggroUntil, i, 0f);
                rt.deaggroUntil = deaggro > now ? deaggro : 0f;
                // ★ 离场闹钟/逃跑窗口不断裂：原样恢复；老档没有这条（=0）就按 yaml 重排一个
                float leaveAt = GetAt(save.monsterLeaveAt, i, 0f);
                rt.leaveAt = leaveAt > 0f
                    ? leaveAt
                    : (string.IsNullOrEmpty(rt.questId) && rt.def.leaveAfterMax > 0f
                        ? now + UnityEngine.Random.Range(rt.def.leaveAfterMin, rt.def.leaveAfterMax)
                        : 0f);
                float fleeUntil = GetAt(save.monsterFleeUntil, i, 0f);
                rt.fleeUntil = fleeUntil > now ? fleeUntil : 0f;
                kept++;
            }
            return kept;
        }

        private static int RestoreLegacyFormat(TradeSaveData legacy, float now, SpaceZones zones)
        {
            int kept = 0;
            for (int i = 0; i < legacy.monsterIds.Count; i++)
            {
                string packed = legacy.monsterIds[i];
                if (string.IsNullOrEmpty(packed))
                {
                    continue;
                }
                string[] parts = packed.Split('|');
                string questId = parts.Length > 0 ? parts[0] : "";
                string defId = parts.Length > 1 ? parts[1] : "";
                AxialI cell = SpaceCfg.Mk(GetAt(legacy.monsterCellQ, i, 0), GetAt(legacy.monsterCellR, i, 0));
                string region = zones != null && zones.valid && zones.IsSub(cell) ? "sub" : "main";
                if (CombatDb.FindMonster(defId) == null)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 旧档怪物 '" + defId + "' 已不在战斗怪物表，丢弃（K3 迁移后应同 id 命中）");
                    continue;
                }
                MonsterRuntime rt = SpawnMonsterInternal(defId, cell, questId, region, 0, -1f, 0f,
                    null, 0f, 0f, 0f, 0f, false, "");
                if (rt == null)
                {
                    continue;
                }
                rt.idleUntil = now + UnityEngine.Random.Range(0f, rt.def.idleMax);
                kept++;
            }
            if (kept > 0)
            {
                MFLog.Verbose(LogCat.Combat,"[Space] 旧档战斗怪物迁移：" + kept + " 只（只读旧字段，不回写）");
            }
            return kept;
        }

        private static int RestoreLootFields(CombatSaveData save)
        {
            // 切块依据：lootFieldGroups[f] = 第 f 个小行星带的条目数（Capture 时写入）
            int fieldCount = save.lootFieldCellQ.Count;
            if (fieldCount == 0 || save.lootFieldItemIds.Count == 0)
            {
                return 0;
            }
            List<int> groups = save.lootFieldGroups;
            int cursor = 0;
            int made = 0;
            int purged = 0;
            SpaceZones zones = StarMapVisuals.Zones;
            for (int f = 0; f < fieldCount; f++)
            {
                int count = f < groups.Count ? groups[f] : 0;
                if (count <= 0)
                {
                    continue;
                }
                AxialI cell = SpaceCfg.Mk(save.lootFieldCellQ[f], save.lootFieldCellR[f]);
                string label = f < save.lootFieldLabels.Count ? save.lootFieldLabels[f] : "";
                // 一次性清理：旧档里被"读档恢复出 0 血怪 → 立刻判死 → 掉带"堆到主图之外（危险区/空白带/子图）的残骸带，
                // 这些格玩家从未到过，属异常堆积（见 星图.md P-33）；只清一次，之后新掉落的正常保留
                if (!save.outerBeltsPurged && zones != null && zones.valid
                    && SpaceCfg.Ring(cell) > zones.mainMaxRing
                    && !string.IsNullOrEmpty(label) && label.EndsWith("残骸带", StringComparison.Ordinal))
                {
                    cursor += count;
                    purged++;
                    continue;
                }
                List<string> items = new List<string>();
                List<float> masses = new List<float>();
                List<int> counts = new List<int>();
                for (int k = 0; k < count && cursor + k < save.lootFieldItemIds.Count; k++)
                {
                    items.Add(save.lootFieldItemIds[cursor + k]);
                    masses.Add(cursor + k < save.lootFieldMasses.Count ? save.lootFieldMasses[cursor + k] : 0f);
                    counts.Add(cursor + k < save.lootFieldCounts.Count ? save.lootFieldCounts[cursor + k] : 1);
                }
                cursor += count;
                if (items.Count == 0)
                {
                    continue;
                }
                SpawnLootFieldRaw(cell, items, masses, counts,
                    f < save.lootFieldSourceTags.Count ? save.lootFieldSourceTags[f] : "monster_death",
                    string.IsNullOrEmpty(label) ? "小行星带" : label, true);
                made++;
            }
            if (purged > 0)
            {
                MFLog.Info(LogCat.Combat, "[Space] ★ 清理外层残留残骸带 " + purged + " 条（主图之外、玩家未到过，见 星图.md P-33）");
            }
            if (zones != null && zones.valid)
            {
                save.outerBeltsPurged = true;   // 分区没算出来不记标记，下次读档再补一次
            }
            return made;
        }

        internal static void ClearAllRuntime()
        {
            for (int i = Monsters.Count - 1; i >= 0; i--)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt != null && rt.monster != null)
                {
                    Util.KDestroyGameObject(rt.monster.gameObject);
                }
            }
            Monsters.Clear();
            byInstance.Clear();
            for (int i = LootFields.Count - 1; i >= 0; i--)
            {
                SpaceLootField lf = LootFields[i];
                if (lf == null)
                {
                    LootFields.RemoveAt(i);
                    continue;
                }
                SpaceAsteroidBelt belt = lf as SpaceAsteroidBelt;
                if (belt != null && !belt.fromRestore && belt.createdEpoch == LoadEpoch)
                {
                    continue;
                }
                Util.KDestroyGameObject(lf.gameObject);
                LootFields.RemoveAt(i);
            }
        }

        private static T GetAt<T>(List<T> list, int i, T fallback)
        {
            if (list == null || i < 0 || i >= list.Count)
            {
                return fallback;
            }
            return list[i];
        }
    }
}
