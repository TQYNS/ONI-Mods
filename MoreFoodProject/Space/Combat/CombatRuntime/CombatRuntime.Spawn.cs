// 用途：刷怪：生成器闹钟、选点、权重抽取、生成与名额统计

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        // 抽取器（有限尝试 K6）

        internal static void TickSpawnerAlarm(SpawnerState sp)
        {
            try
            {
            if (!inited)
            {
                Init();
                if (!inited)
                {
                    return;
                }
            }
            TickSpawner(sp, TradeRegistry.Now());
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        private static void TickSpawner(SpawnerState sp, float now)
        {
            try
            {
            if (sp == null || sp.cfg == null)
            {
                return;
            }
            if (sp.paused)
            {
                // ★ 名额满 → 闹钟暂停：不重排、不空抽；空位由 RemoveMonsterAt 的通知恢复（见 OnMonsterSlotFreed）
                return;
            }
            if (sp.nextSpawnAt <= 0f)
            {
                sp.nextSpawnAt = now + UnityEngine.Random.Range(sp.cfg.spawnIntervalMin, sp.cfg.spawnIntervalMax);
                return;
            }
            if (now < sp.nextSpawnAt)
            {
                return;
            }

            int alive = CountAliveInRegion(sp.cfg.region);
            if (alive >= sp.cfg.maxMonsters)
            {
                // ★ 到点时名额仍满 → 暂停计时（剩余 0 秒：空位一出现就抽），而不是像以前那样重排后干等到下次
                sp.paused = true;
                sp.pausedRemain = 0f;
                MFLog.Verbose(LogCat.Combat,"[Space] 抽取器 " + sp.cfg.id + " 名额已满（" + alive + "/" + sp.cfg.maxMonsters
                    + "）→ 闹钟暂停，等空位（离开/死亡/捕获都会算空位）");
                return;
            }
            sp.nextSpawnAt = now + UnityEngine.Random.Range(sp.cfg.spawnIntervalMin, sp.cfg.spawnIntervalMax);
            int n = UnityEngine.Random.Range(sp.cfg.spawnCountMin, sp.cfg.spawnCountMax + 1);
            n = Mathf.Min(n, sp.cfg.maxMonsters - alive);
            HashSet<AxialI> used = new HashSet<AxialI>();
            int ok = 0;
            for (int i = 0; i < n; i++)
            {
                AxialI cell;
                if (!TryPickEdgeCell(sp.cfg.region, used, out cell))
                {
                    break;
                }
                CombatMonsterDef def = DrawWeighted(sp.cfg.region);
                if (def == null)
                {
                    break;
                }
                used.Add(cell);
                MonsterRuntime rt = SpawnMonsterInternal(def.id, cell, "", sp.cfg.region, 0, -1f, -1f, null, 0f, 0f, 0f, 0f, false, "");
                if (rt != null)
                {
                    ok++;
                }
            }
            if (ok > 0)
            {
                MFLog.Verbose(LogCat.Combat,"[Space] 抽取器 " + sp.cfg.id + " 刷出 " + ok + " 只（区域 " + sp.cfg.region
                    + " 在场 " + CountAliveInRegion(sp.cfg.region) + "/" + sp.cfg.maxMonsters + "）");
            }
            else
            {
                SpaceZones z = StarMapVisuals.Zones;
                MFLog.Warn(LogCat.Combat,"[Space] 抽取器 " + sp.cfg.id + " 到点但没刷出：分区有效="
                    + (z != null && z.valid) + " 候选格 main="
                    + (z != null && z.mainEdgeCells != null ? z.mainEdgeCells.Count : 0) + " sub="
                    + (z != null && z.subEdgeCells != null ? z.subEdgeCells.Count : 0));
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        private static bool TryPickEdgeCell(string region, HashSet<AxialI> used, out AxialI cell)
        {
            cell = default(AxialI);
            SpaceZones z = StarMapVisuals.Zones;
            ClusterGrid grid = ClusterGrid.Instance;
            if (z == null || !z.valid || grid == null)
            {
                return false;
            }
            List<AxialI> list = region == "sub" ? z.subEdgeCells : z.mainEdgeCells;
            if (list == null || list.Count == 0)
            {
                return false;
            }
            int tries = Mathf.Max(1, CombatDb.Shared.spawnEdgeTries);
            for (int t = 0; t < tries; t++)
            {
                AxialI c = list[UnityEngine.Random.Range(0, list.Count)];
                if (used != null && used.Contains(c))
                {
                    continue;
                }
                if (!grid.IsValidCell(c))
                {
                    continue;
                }
                List<ClusterGridEntity> contents;
                if (grid.cellContents.TryGetValue(c, out contents) && contents != null && contents.Count > 0)
                {
                    continue;
                }
                cell = c;
                return true;
            }
            MFLog.Warn(LogCat.Combat,"[Space] 抽取器找边缘格失败（" + region + " 区，尝试 " + tries + " 次）");
            return false;
        }

        private static CombatMonsterDef DrawWeighted(string region)
        {
            float total = 0f;
            for (int i = 0; i < CombatDb.Monsters.Count; i++)
            {
                CombatMonsterDef d = CombatDb.Monsters[i];
                float w = EffectiveWeight(d, region);
                if (w <= 0f)
                {
                    continue;
                }
                if (d.spawn.maxAlive > 0 && CountAliveDef(d.id) >= d.spawn.maxAlive)
                {
                    continue;
                }
                if (d.spawn.minIntervalCycles > 0 && !PassedMinInterval(d))
                {
                    continue;
                }
                total += w;
            }
            if (total <= 0f)
            {
                return null;
            }
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < CombatDb.Monsters.Count; i++)
            {
                CombatMonsterDef d = CombatDb.Monsters[i];
                float w = EffectiveWeight(d, region);
                if (w <= 0f)
                {
                    continue;
                }
                if (d.spawn.maxAlive > 0 && CountAliveDef(d.id) >= d.spawn.maxAlive)
                {
                    continue;
                }
                if (d.spawn.minIntervalCycles > 0 && !PassedMinInterval(d))
                {
                    continue;
                }
                roll -= w;
                if (roll <= 0f)
                {
                    return d;
                }
            }
            return null;
        }

        /// <summary>
        internal static float EffectiveWeight(CombatMonsterDef d, string region)
        {
            if (d == null)
            {
                return 0f;
            }
            bool isSub = region == "sub";
            if (isSub && d.spawnRegion == "main")
            {
                return 0f;
            }
            if (!isSub && d.spawnRegion == "sub")
            {
                return 0f;
            }
            MonsterSpawnCfg sp = d.spawn;
            if (isSub)
            {
                return sp.subSet ? sp.sub : 1f;
            }
            return sp.mainSet ? sp.main : 1f;
        }

        private static bool PassedMinInterval(CombatMonsterDef d)
        {
            // min_interval_cycles 用周期数近似成秒（1 周期 = 600 秒），按最近击杀时间判断
            float now = TradeRegistry.Now();
            float last = LastKillAt(d.id);
            return now - last >= d.spawn.minIntervalCycles * 600f;
        }

        private static readonly Dictionary<string, float> lastKillAt = new Dictionary<string, float>();

        private static float LastKillAt(string defId)
        {
            float t;
            return lastKillAt.TryGetValue(defId, out t) ? t : -99999f;
        }

        private static int CountAliveInRegion(string region)
        {
            int n = 0;
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt != null && rt.IsAlive && rt.region == region)
                {
                    n++;
                }
            }
            return n;
        }

        internal static int CountAliveDef(string defId)
        {
            int n = 0;
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt != null && rt.IsAlive && rt.defId == defId)
                {
                    n++;
                }
            }
            return n;
        }

        // 生成 / 销毁

        internal static MonsterRuntime SpawnMonster(string defId, AxialI cell, string questId, string region)
        {
            return SpawnMonsterInternal(defId, cell, questId, region, 0, -1f, -1f, null, 0f, 0f, 0f, 0f, false, "");
        }

        private static MonsterRuntime SpawnMonsterInternal(string defId, AxialI cell, string questId,
            string region, int uid, float hpOverride, float armorOverride, string stateName,
            float idleUntil, float regenUntil, float attackUntil, float deaggroUntil, bool fleeUsed,
            string aggroName)
        {
            if (!inited)
            {
                Init();
            }
            CombatMonsterDef def = CombatDb.FindMonster(defId);
            if (def == null)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 找不到战斗怪物定义：" + defId + "（已跳过生成）");
                return null;
            }
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || !grid.IsValidCell(cell))
            {
                MFLog.Warn(LogCat.Combat,"[Space] 星图生物落点无效 " + SpaceCfg.Pos(cell));
                return null;
            }
            EnsureMonsterPrefab();
            GameObject go = Util.KInstantiate(monsterPrefab, null, null);
            if (go == null)
            {
                return null;
            }
            SpaceMonster m = go.GetComponent<SpaceMonster>();
            m.defId = def.id;
            m.hp = hpOverride > 0f ? hpOverride : def.hp;
            m.armor = armorOverride >= 0f ? armorOverride : def.armor;
            m.fled = false;
            ClusterGridEntity e = go.GetComponent<ClusterGridEntity>();
            e.Location = cell;
            go.SetActive(true);
            // K4：怪物存档以我方 CombatSaveData 为权威 —— 禁止原版持久化，否则读档双份
            try
            {
                SaveLoadRoot slr = go.GetComponent<SaveLoadRoot>();
                if (slr != null)
                {
                    slr.SetRegistered(false);
                }
            }
            catch (Exception ex)
            {
                MFLog.Exc(LogCat.Combat, ex, "[Space] 关闭怪物原版持久化失败");
            }

            MonsterRuntime rt = new MonsterRuntime();
            rt.monster = m;
            rt.def = def;
            rt.defId = def.id;
            rt.questId = questId ?? "";
            rt.region = string.IsNullOrEmpty(region) ? RegionOf(cell) : region;
            rt.hp = m.hp;
            rt.armor = m.armor;
            rt.uid = uid > 0 ? uid : NextUid++;
            if (rt.uid >= NextUid)
            {
                NextUid = rt.uid + 1;
            }
            rt.ticket = string.IsNullOrEmpty(questId) ? ("wild#" + rt.uid) : (questId + "#" + rt.uid);
            rt.state = ParseState(stateName);
            // 生成后先睡一段（yaml spawn_sleep_seconds；配 0 = 不睡）
            rt.idleUntil = idleUntil > 0f
                ? idleUntil
                : TradeRegistry.Now() + UnityEngine.Random.Range(
                    def.spawnSleepSeconds * 0.5f, def.spawnSleepSeconds);
            // ★ 离场闹钟（K18）：yaml 配了 leave_after_seconds 才排；任务下发的狩猎目标不排（离开会让任务做不成）
            rt.leaveAt = (!string.IsNullOrEmpty(questId) || def.leaveAfterMax <= 0f)
                ? 0f
                : TradeRegistry.Now() + UnityEngine.Random.Range(def.leaveAfterMin, def.leaveAfterMax);
            rt.regenUntil = regenUntil;
            rt.attackUntil = attackUntil;
            rt.deaggroUntil = deaggroUntil;
            rt.fleeUsed = fleeUsed;
            if (!string.IsNullOrEmpty(aggroName))
            {
                rt.aggroCraft = FindCraftByName(aggroName);
                rt.aggroCraftName = aggroName;
            }
            m.rt = rt;
            Monsters.Add(rt);
            byInstance[m.GetInstanceID()] = rt;
            CombatBars.EnsureMonster(rt);   // 迷你条：创建即画（星图开着才建）
            MFLog.Verbose(LogCat.Combat,"[Space] 生成星图生物 " + def.name + "（" + def.id + "）@" + SpaceCfg.Pos(cell)
                + " HP=" + Mathf.Round(rt.hp) + " 装甲=" + Mathf.Round(rt.armor)
                + " 区域=" + rt.region + " state=" + rt.state
                + (string.IsNullOrEmpty(questId) ? "" : " 任务=" + questId));
            return rt;
        }

        private static MonsterState ParseState(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return MonsterState.Idle;
            }
            try
            {
                return (MonsterState)Enum.Parse(typeof(MonsterState), s);
            }
            catch (Exception)
            {
                return MonsterState.Idle;
            }
        }

        /// <summary>★ 唯一的移除出口（击杀/逃跑离场/捕获/清理都走这里）：
        private static void RemoveMonsterAt(int index)
        {
            if (index < 0 || index >= Monsters.Count)
            {
                return;
            }
            MonsterRuntime rt = Monsters[index];
            if (rt != null && rt.monster != null)
            {
                CombatBars.Remove(rt.monster);                 // 迷你条跟着走（否则残留悬空条）
                byInstance.Remove(rt.monster.GetInstanceID());
                // ★ 死亡/离场即时摘图标：先标 removed 防原版重建，再调原版 RemoveDeletedEntities
                rt.monster.removed = true;
                StarMapVisuals.DropEntityVisual(rt.monster);
                Util.KDestroyGameObject(rt.monster.gameObject);
            }
            Monsters.RemoveAt(index);
            if (rt != null)
            {
                OnMonsterSlotFreed(rt.region);
            }
        }

        /// <summary>区域里的怪少了一只 → 若该区抽取器因名额满而暂停，则恢复计时（余量接着跑）。</summary>
        internal static void OnMonsterSlotFreed(string region)
        {
            SpawnerState sp = SpawnerForRegion(region);
            if (sp == null || !sp.paused)
            {
                return;
            }
            sp.paused = false;
            sp.nextSpawnAt = TradeRegistry.Now() + Mathf.Max(0f, sp.pausedRemain);
            sp.pausedRemain = 0f;
            MFLog.Verbose(LogCat.Combat,"[Space] 抽取器 " + sp.cfg.id + " 名额空出 → 恢复计时（"
                + sp.nextSpawnAt.ToString("F0") + " 到点）");
        }

        private static readonly HashSet<AxialI> debugSpawnReserved = new HashSet<AxialI>();

        // 调试按钮用：按原有规则（边缘格 + 占格校验 + 休眠/离场闹钟）在 def 允许的区域刷一只；返回 null = 没刷出
        internal static MonsterRuntime DebugSpawnOne(CombatMonsterDef def)
        {
            if (def == null)
            {
                return null;
            }
            if (def.spawnRegion == "main" || def.spawnRegion == "sub")
            {
                return DebugSpawnIn(def.id, def.spawnRegion);
            }
            MonsterRuntime rt = DebugSpawnIn(def.id, "main");
            return rt != null ? rt : DebugSpawnIn(def.id, "sub");
        }

        // 调试按钮用：开始一批刷新前清掉占格记录（同批内不复用同一格）
        internal static void DebugResetSpawnReserved()
        {
            debugSpawnReserved.Clear();
        }

        private static MonsterRuntime DebugSpawnIn(string defId, string region)
        {
            AxialI cell;
            if (!TryPickEdgeCell(region, debugSpawnReserved, out cell))
            {
                return null;
            }
            MonsterRuntime rt = SpawnMonster(defId, cell, "", region);
            if (rt != null)
            {
                debugSpawnReserved.Add(cell);
                SpaceReveal.RevealIfMain(cell);   // 调试用：点亮这只怪所在的那一格，免得刷在迷雾里看不见也点不到
                StarMapVisuals.RefreshNow();
            }
            return rt;
        }

        private static SpawnerState SpawnerForRegion(string region)
        {
            if (MainSpawner != null && MainSpawner.cfg != null && MainSpawner.cfg.region == region)
            {
                return MainSpawner;
            }
            if (SubSpawner != null && SubSpawner.cfg != null && SubSpawner.cfg.region == region)
            {
                return SubSpawner;
            }
            return null;
        }
    }
}
