// 用途：结算与掉落：击杀/逃跑、战利品生成、仇恨与警戒

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        internal static void KillMonster(MonsterRuntime rt, Clustercraft byCraft, float now, string cause)
        {
            if (rt == null || rt.resolved)
            {
                return;
            }
            rt.resolved = true;
            rt.hp = 0f;
            AxialI cell = rt.Cell;
            // 掉落死亡瞬间定死（漏洞7），读档不重 roll；真正生成放到移除怪物之后，残骸带才能落在原格
            List<string> items = null;
            List<float> masses = null;
            List<int> counts = null;
            string srcTag = null;
            string beltLabel = null;
            if (rt.def != null && rt.def.lootField != null)
            {
                List<CombatLootEntry> drops = RollLoot(rt.def);
                if (drops.Count > 0)
                {
                    beltLabel = string.IsNullOrEmpty(rt.def.lootField.name)
                        ? (rt.def.name + "残骸带") : rt.def.lootField.name;
                    items = new List<string>();
                    masses = new List<float>();
                    counts = new List<int>();
                    for (int di = 0; di < drops.Count; di++)
                    {
                        if (drops[di] == null || drops[di].max <= 0f)
                        {
                            continue;
                        }
                        items.Add(drops[di].item);
                        masses.Add(drops[di].max);
                        counts.Add(1);
                    }
                    srcTag = string.IsNullOrEmpty(rt.def.lootField.beltStyle)
                        ? "metallic" : rt.def.lootField.beltStyle;
                }
            }
            lastKillAt[rt.defId] = now;
            MFLog.Verbose(LogCat.Combat,"[Space] ★ 击杀 " + (rt.def != null ? rt.def.name : rt.defId) + "（" + rt.defId + "）@"
                + SpaceCfg.Pos(cell) + "（" + cause + "，掉落已定死）");
            // 目标死了 → 让所有瞄它的火箭结束战斗、待机（K12）
            for (int i = 0; i < Crafts.Count; i++)
            {
                Clustercraft craft = Crafts[i];
                if (craft == null)
                {
                    continue;
                }
                RocketCombatState st = craft.GetComponent<RocketCombatState>();
                if (st != null && st.orderedTargetUid == rt.uid)
                {
                    st.orderedTargetUid = 0;
                    StopCraft(craft);
                    TradeUi.Notify("目标已被摧毁", craft.Name + " 结束战斗，原地待机");
                }
            }
            MonsterResolvedEvent ev = new MonsterResolvedEvent();
            ev.monsterId = rt.defId;
            ev.questId = rt.questId;
            ev.ticket = rt.ticket;
            ev.cell = cell;
            ev.resolution = MonsterResolution.Killed;
            ev.byCraft = byCraft != null ? byCraft.Name : null;
            int index = Monsters.IndexOf(rt);
            if (index >= 0)
            {
                RemoveMonsterAt(index);   // 统一移除出口（销毁实体 + 迷你条 + 名额通知）
            }
            // 怪物实体已从格子移除 → 残骸带用原格生成，不再被「就近找空位」挤到可见外沿之外
            if (items != null && items.Count > 0)
            {
                SpawnLootFieldRaw(cell, items, masses, counts, srcTag, beltLabel, true);
            }
            // 残骸带是刚生成的新实体，让原版立刻为它建 visualizer（怪物那枚已在 RemoveMonsterAt 摘掉）
            StarMapVisuals.RefreshEntityVisuals();
            RocketPanelScreen.RequestRebuild();   // 击杀 → 交战面板重建（怪卡消失）
            SpaceCombatBus.Publish(ev);   // 击杀仍广播（任务/统计靠它结算）
        }

        // 调试按钮用：在场全部怪物按玩家击杀路线逐个结算（掉落定死 + 广播任务结算），无归属火箭
        internal static int DebugKillAll()
        {
            if (Monsters.Count == 0)
            {
                return 0;
            }
            MonsterRuntime[] snapshot = Monsters.ToArray();
            int n = 0;
            for (int i = snapshot.Length - 1; i >= 0; i--)
            {
                MonsterRuntime rt = snapshot[i];
                if (rt == null || !rt.IsAlive)
                {
                    continue;
                }
                KillMonster(rt, null, TradeRegistry.Now(), "调试全部击杀");
                n++;
            }
            return n;
        }

        internal static void EscapeMonster(MonsterRuntime rt)
        {
            if (rt == null || rt.resolved)
            {
                return;
            }
            rt.resolved = true;
            AxialI cell = rt.Cell;
            MonsterResolvedEvent ev = null;
            if (!string.IsNullOrEmpty(rt.questId) && !string.IsNullOrEmpty(rt.ticket))
            {
                ev = new MonsterResolvedEvent();
                ev.monsterId = rt.defId;
                ev.questId = rt.questId;
                ev.ticket = rt.ticket;
                ev.cell = cell;
                ev.resolution = MonsterResolution.Escaped;
                ev.byCraft = null;
            }
            MFLog.Verbose(LogCat.Combat,"[Space] " + (rt.def != null ? rt.def.name : rt.defId) + " 逃跑离场 @"
                + SpaceCfg.Pos(cell) + "（不掉落）");
            // 问题4+3：逃跑/离场必须通知所有瞄准它的火箭立即脱战 + 清攻击箭头，
            for (int i = 0; i < Crafts.Count; i++)
            {
                Clustercraft craft = Crafts[i];
                if (craft == null)
                {
                    continue;
                }
                RocketCombatState st = craft.GetComponent<RocketCombatState>();
                if (st != null && st.orderedTargetUid == rt.uid)
                {
                    st.orderedTargetUid = 0;
                    st.inCombat = false;
                    st.disengageTimer = 0f;
                    StopCraft(craft);
                    CombatBars.RemoveCraft(craft);
                    CombatArrow.Hide();   // 问题11：目标逃跑离场 → 收起自绘攻击箭头
                }
            }
            int index = Monsters.IndexOf(rt);
            if (index >= 0)
            {
                RemoveMonsterAt(index);
            }
            RocketPanelScreen.RequestRebuild();   // 逃跑/离场 → 交战面板重建（怪卡消失）
            if (ev != null)
            {
                SpaceCombatBus.Publish(ev);   // 任务目标离场：移除后广播，立即失败；普通野怪不影响任务
            }
        }

        private static List<CombatLootEntry> RollLoot(CombatMonsterDef def)
        {
            List<CombatLootEntry> result = new List<CombatLootEntry>();
            if (def == null || def.loot.Count == 0)
            {
                return result;
            }
            Dictionary<string, CombatLootEntry> merged = new Dictionary<string, CombatLootEntry>();
            for (int i = 0; i < def.loot.Count; i++)
            {
                CombatLootEntry e = def.loot[i];
                if (e == null || UnityEngine.Random.value > e.chance)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(e.group))
                {
                    // 池条目：抽 N 种、每种独立 kg（候选已在建池时校验过 prefab，这里不再逐个查）
                    List<string> ids;
                    List<float> kgs;
                    if (!AsteroidBeltPool.ComposeGroup(e.group, e.picksLo, e.picksHi,
                        e.min, e.max, e.hasRange, out ids, out kgs))
                    {
                        continue;
                    }
                    for (int k = 0; k < ids.Count; k++)
                    {
                        float poolKg = kgs[k];
                        if (CombatDb.Shared.kgRound == "round")
                        {
                            poolKg = Mathf.Max(1f, Mathf.Round(poolKg));
                        }
                        Accumulate(merged, ids[k], poolKg);
                    }
                    continue;
                }
                float kg = UnityEngine.Random.Range(e.min, e.max);
                if (CombatDb.Shared.kgRound == "round")
                {
                    kg = Mathf.Max(1f, Mathf.Round(kg));
                }
                if (kg <= 0f)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(e.item) && Assets.TryGetPrefab(new Tag(e.item)) == null)
                {
                    if (CombatDb.Shared.invalidItemSkip)
                    {
                        MFLog.Warn(LogCat.Combat,"[Space] 掉落跳过：物品不存在 " + e.item + "（怪物 " + def.id + "）");
                        continue;
                    }
                }
                Accumulate(merged, e.item, kg);
            }
            foreach (KeyValuePair<string, CombatLootEntry> kv in merged)
            {
                result.Add(kv.Value);
            }
            return result;
        }

        // 同 ID 合并累加（复用 max 字段当总 kg）
        private static void Accumulate(Dictionary<string, CombatLootEntry> merged, string item, float kg)
        {
            if (string.IsNullOrEmpty(item) || kg <= 0f)
            {
                return;
            }
            CombatLootEntry m;
            if (!merged.TryGetValue(item, out m))
            {
                m = new CombatLootEntry();
                m.item = item;
                m.min = 0f;
                m.max = 0f;
                merged[item] = m;
            }
            m.max += kg;
        }

        // 小行星带（K7）

        private static SpaceLootField SpawnLootField(AxialI cell, List<CombatLootEntry> loot, string sourceTag, string label)
        {
            // 落点必须是空格
            cell = AsteroidBeltFactory.ResolveFreeCell(cell);
            if (cell.Q == int.MinValue)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 打捞点找不到空格，不生成（" + (label ?? "小行星带") + "）");
                return null;
            }
            EnsureLootPrefab();
            GameObject go = Util.KInstantiate(lootPrefab, null, null);
            if (go == null)
            {
                return null;
            }
            SpaceLootField lf = go.GetComponent<SpaceLootField>();
            lf.sourceTag = sourceTag;
            lf.label = label;
            lf.lootItemIds.Clear();
            lf.lootMasses.Clear();
            lf.lootCounts.Clear();
            for (int i = 0; i < loot.Count; i++)
            {
                if (loot[i] == null || loot[i].max <= 0f)
                {
                    continue;
                }
                lf.lootItemIds.Add(loot[i].item);
                lf.lootMasses.Add(loot[i].max);
                lf.lootCounts.Add(1);
            }
            ClusterGridEntity e = go.GetComponent<ClusterGridEntity>();
            e.Location = cell;
            go.SetActive(true);
            try
            {
                SaveLoadRoot slr = go.GetComponent<SaveLoadRoot>();
                if (slr != null)
                {
                    slr.SetRegistered(false);   // K11：小行星带走 CombatSaveData，不进原版存档
                }
            }
            catch (Exception ex)
            {
                MFLog.Exc(LogCat.Combat, ex, "[Space] 关闭小行星带原版持久化失败");
            }
            if (lf.lootItemIds.Count > 0)
            {
                LootFields.Add(lf);
                MFLog.Verbose(LogCat.Combat,"[Space] 生成小行星带（" + sourceTag + "）@" + SpaceCfg.Pos(cell) + "：" + lf.DescribeContents());
            }
            return lf;
        }

        internal static SpaceLootField SpawnLootFieldRaw(AxialI cell, List<string> items, List<float> masses,
            List<int> counts, string sourceTag, string label)
        {
            return SpawnLootFieldRaw(cell, items, masses, counts, sourceTag, label, false);
        }

        // exactCell = true：强制用传入格（击杀掉落用），不再就近改点；否则沿用「占用就近找空位」
        internal static SpaceLootField SpawnLootFieldRaw(AxialI cell, List<string> items, List<float> masses,
            List<int> counts, string sourceTag, string label, bool exactCell)
        {
            if (items == null || items.Count == 0)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 打捞点清单为空，不生成（" + (label ?? "小行星带") + "）");
                return null;
            }
            ClusterGrid grid = ClusterGrid.Instance;
            if (exactCell && grid != null && grid.IsValidCell(cell))
            {
                MFLog.Verbose(LogCat.Combat,"[Space] 打捞点按原格生成（不走就近找空位）：" + SpaceCfg.Pos(cell));
            }
            else
            {
                cell = AsteroidBeltFactory.ResolveFreeCell(cell);   // 落点必须是空格，占用则就近找
            }
            if (cell.Q == int.MinValue)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 打捞点找不到空格，不生成（" + (label ?? "小行星带") + "）");
                return null;
            }
            // 统一走工厂（问题6）：yaml 来源与代码来源都生成带外观的 SpaceAsteroidBelt。
            BeltStyle style = ResolveBeltStyle(sourceTag);
            BeltBuilder builder = AsteroidBeltFactory.Create(
                string.IsNullOrEmpty(label) ? "小行星带" : label, style);
            int placed = AsteroidBeltFactory.Feed(builder, items, masses, counts);
            if (placed > 0)
            {
                SpaceAsteroidBelt belt = exactCell ? builder.AtExact(cell) : builder.At(cell);
                if (belt != null)
                {
                    belt.fromRestore = true;
                    return belt;
                }
            }
            List<CombatLootEntry> loot = new List<CombatLootEntry>();
            for (int i = 0; i < items.Count; i++)
            {
                CombatLootEntry e = new CombatLootEntry();
                e.item = items[i];
                int pieceCount = i < counts.Count ? counts[i] : 0;
                float mass = i < masses.Count ? masses[i] : 0f;
                if (mass <= 0f && pieceCount > 0)
                {
                    mass = pieceCount * 1f;
                }
                e.max = mass;
                e.min = 0f;
                if (e.max > 0f)
                {
                    loot.Add(e);
                }
            }
            SpaceLootField lf = SpawnLootField(cell, loot, sourceTag, label);
            if (lf != null && counts != null && counts.Count == items.Count)
            {
                lf.lootCounts.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    lf.lootCounts.Add(counts[i]);
                }
            }
            if (lf != null) { lf.label = (label ?? "小行星带") + "（" + lf.DescribePretty() + "）"; }
            return lf;
        }

        // 统一解析来源标记为小行星带外观风格（问题6）：未知来源返回默认金属风格，
        private static BeltStyle ResolveBeltStyle(string sourceTag)
        {
            BeltStyle style;
            if (!string.IsNullOrEmpty(sourceTag))
            {
                try
                {
                    if (Enum.TryParse<BeltStyle>(sourceTag, true, out style))
                    {
                        return style;
                    }
                }
                catch (Exception)
                {
                    // expected: 来源标记解析失败时改用随机金属风格，小行星带照常生成
                }
            }
            return AsteroidBeltFactory.RollAnyStyle();   // 解析不出风格时随机
        }

        // 到站自动采集（K7：原版入舱流程，无手动按钮）。
        private static void TickLootFields()
        {
            try
            {
            float now = TradeRegistry.Now();
            for (int i = LootFields.Count - 1; i >= 0; i--)
            {
                SpaceLootField lf = LootFields[i];
                if (lf == null)
                {
                    LootFields.RemoveAt(i);
                    continue;
                }
                SpaceAsteroidBelt belt = lf as SpaceAsteroidBelt;
                Clustercraft craft = FindCraftAt(lf.Location);
                if (craft == null)
                {
                    if (belt != null)
                    {
                        belt.StopMining();
                    }
                    continue;
                }
                if (belt != null)
                {
                    // 原版 CollectUpdate 已由 VanillaCollectorOverridePatch 接管开采
                    belt.RefreshFromStore();
                    if (belt.lootItemIds.Count == 0)
                    {
                        belt.StopMining();
                        TradeUi.Notify("已回收完毕", craft.Name + " 采完了" + belt.label + "，小行星带消失");
                        MFLog.Verbose(LogCat.Combat,"[Space] 小行星带 @" + SpaceCfg.Pos(belt.Location) + " 采空 → 销毁");
                        SpaceReveal.Forget(belt.Location);   // 撤销主动揭示，避免留下常亮的空格
                        Util.KDestroyGameObject(belt.gameObject);
                        LootFields.RemoveAt(i);
                        StarMapVisuals.RefreshNow();
                    }
                    continue;
                }
                bool failed = false;
                for (int k = lf.lootItemIds.Count - 1; k >= 0; k--)
                {
                    float mass = lf.lootMasses[k];
                    if (mass <= 0f)
                    {
                        lf.lootItemIds.RemoveAt(k);
                        lf.lootMasses.RemoveAt(k);
                        if (k < lf.lootCounts.Count)
                        {
                            lf.lootCounts.RemoveAt(k);
                        }
                        continue;
                    }
                    if (RocketInventory.Grant(craft, new Tag(lf.lootItemIds[k]), mass, 300f))
                    {
                        lf.lootItemIds.RemoveAt(k);
                        lf.lootMasses.RemoveAt(k);
                        if (k < lf.lootCounts.Count)
                        {
                            lf.lootCounts.RemoveAt(k);
                        }
                    }
                    else
                    {
                        failed = true;
                    }
                }
                if (failed)
                {
                    TradeUi.Notify("货舱装不下", "小行星带还有物资滞留：" + lf.DescribeContents());
                }
                if (lf.lootItemIds.Count == 0)
                {
                    TradeUi.Notify("已回收完毕", craft.Name + " 采完了" + lf.label + "，小行星带消失");
                    MFLog.Verbose(LogCat.Combat,"[Space] 小行星带 @" + SpaceCfg.Pos(lf.Location) + " 采空 → 销毁");
                    SpaceReveal.Forget(lf.Location);
                    Util.KDestroyGameObject(lf.gameObject);
                    LootFields.RemoveAt(i);
                    StarMapVisuals.RefreshNow();
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        // 警戒

        private static void AlertOnCraftMoved(Clustercraft craft)
        {
            // 仇恨只由实际伤害引起，火箭移动不再被动上仇，避免群体围攻
            return;
        }

        private static void OnMonsterMoved(MonsterRuntime rt)
        {
            // 仇恨只由实际伤害引起，怪物移动不再被动上仇，避免群体围攻
            return;
        }

        internal static void SetAggro(MonsterRuntime rt, Clustercraft craft)
        {
            if (rt == null || craft == null)
            {
                return;
            }
            rt.aggroCraft = craft;
            rt.aggroCraftName = craft.Name;
            rt.deaggroUntil = 0f;
            rt.nextMoveAt = 0f;   // 刚上仇立刻走第一步，之后按 moveInterval 节流
            int d = SpaceCfg.Distance(rt.Cell, craft.Location);
            rt.state = d <= rt.def.attackRange ? MonsterState.Combat : MonsterState.Pursue;
            MFLog.Verbose(LogCat.Combat,"[Space] " + rt.def.name + " 进入警戒（" + rt.state + "）目标=" + craft.Name
                + " 距离=" + d + "（state=" + rt.state + "）");
            CombatBars.PushMonster(rt);   // 状态变化也推送（详情里的"状态"行）
        }

        private static void ClearAggro(MonsterRuntime rt)
        {
            rt.aggroCraft = null;
            rt.aggroCraftName = "";
            rt.state = MonsterState.Idle;
            rt.deaggroUntil = 0f;
        }

        // 跨星图瞬移（时空裂缝按钮 / 进入时间裂口）：目标已不在本分区，立刻脱战，不等下一次 tick（≤5s）
        internal static void OnCraftLeftRegion(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            float now = TradeRegistry.Now();
            int n = 0;
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt == null || rt.aggroCraft != craft)
                {
                    continue;
                }
                ClearAggro(rt);
                rt.deaggroUntil = now + rt.def.deaggroSeconds;   // 脱战冷却：刚跨图的这一幕不再被瞬间重新锁上
                n++;
            }
            if (n > 0)
            {
                MFLog.Verbose(LogCat.Combat,"[Space] 跨星图瞬移 → " + n + " 只怪立刻脱战（目标 " + craft.Name + "）");
                RocketPanelScreen.RequestRebuild();   // 交战面板的怪物卡同步消失
            }
        }

        // 强制脱战清两层：怪物侧仇恨与火箭侧交战态，避免只清一层卡住
        internal static void ForceDisengageBothLayers(Clustercraft craft, float now, string reason)
        {
            if (craft == null)
            {
                return;
            }
            // 清怪物侧对该 craft 的仇恨并加冷却
            int n = 0;
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt == null || rt.aggroCraft != craft)
                {
                    continue;
                }
                ClearAggro(rt);
                rt.deaggroUntil = now + rt.def.deaggroSeconds;
                n++;
            }
            // 清火箭侧交战态、指令目标、迷你条与攻击箭头
            RocketCombatState st = craft.GetComponent<RocketCombatState>();
            if (st != null)
            {
                st.ClearCombatState("强制脱战(" + reason + ")");
            }
            if (n > 0 || st != null)
            {
                RocketPanelScreen.RequestRebuild();
            }
            MFLog.Verbose(LogCat.Combat,"[Space] " + craft.Name + " 强制脱战（" + reason + "）：怪物侧 " + n
                + " 只清仇恨；火箭侧交战态清空");
        }
    }
}
