// 用途：怪物行为状态机：游荡、战斗、逃跑、离场与回血

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        // 怪物状态机

        private static void TickMonster(MonsterRuntime rt, float now)
        {
            try
            {
            if (rt.monster == null)
            {
                return;
            }
            // ★ 主图怪不许停在"星图外"（可见外沿之外的隐藏带）—— 旧分区/旧档遗留的怪
            SpaceZones zz = StarMapVisuals.Zones;
            if (zz != null && zz.valid && rt.region != "sub" && zz.IsVoid(rt.Cell))
            {
                for (int i = 0; i < 6; i++)
                {
                    AxialI c = SpaceCfg.Neighbor(rt.Cell, i);
                    if (TryStep(rt, c))
                    {
                        MoveMonster(rt, c);
                        StarMapVisuals.RevealCell(c, 0);
                        break;
                    }
                }
            }
            // ★ 离场闹钟（K18）：到点且【处于漫游】→ 随机选一个边缘空格走过去，走到边缘直接消失。
            if (rt.leaveAt > 0f && now >= rt.leaveAt
                && (rt.state == MonsterState.Idle || rt.state == MonsterState.Roam))
            {
                rt.leaveAt = 0f;
                MFLog.Verbose(LogCat.Combat,"[Space] " + rt.def.name + " 离场时间到（区域 " + rt.region + "，" + SpaceCfg.Pos(rt.Cell) + "）→ 前往边缘离场");
                OrderLeave(rt);
            }
            // 漫游状态下不应残留任何仇恨 —— 逃跑结束回游荡后旧 aggroCraft 会悬挂，
            if ((rt.state == MonsterState.Idle || rt.state == MonsterState.Roam) && rt.aggroCraft != null)
            {
                ClearAggro(rt);
            }
            switch (rt.state)
            {
                case MonsterState.Idle:
                case MonsterState.Roam:
                    TickMonsterIdle(rt, now);
                    break;
                case MonsterState.Combat:
                case MonsterState.Pursue:
                    TickMonsterCombat(rt, now);
                    break;
                case MonsterState.Flee:
                    TickMonsterFlee(rt, now);
                    break;
                case MonsterState.Leaving:
                    TickMonsterLeaving(rt, now);
                    break;
            }
            TickMonsterRegen(rt, now);
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        // ★ 漫游节流硬闸（游戏秒）：睡眠/移动之间切换状态至少隔这么久 —— 改时长只改这一行（测试期 10s）
        private const float RoamSwitchMinSeconds = 10f;

        // ★ 逃跑窗口（游戏秒）：一次逃跑最长持续这么久，到点回漫游 —— 改时长只改这一行（测试期 300s）
        private const float FleeMaxSeconds = 300f;

        // ★ 追击节流倍率：怪物每格耗时 = 目标火箭每格耗时 × 该值（>1 保证玩家开跑必甩得掉）
        private const float ChaseIntervalScale = 2.25f;

        private static void TickMonsterIdle(MonsterRuntime rt, float now)
        {
            try
            {
            if (now < rt.idleUntil)
            {
                return;
            }
            float interval = UnityEngine.Random.Range(rt.def.idleMin, rt.def.idleMax) / Mathf.Max(0.05f, rt.def.speed);
            interval = Mathf.Max(RoamSwitchMinSeconds, interval);   // ★ 硬闸：fast/speed 高也不许缩短切换间隔
            rt.idleUntil = now + interval;
            if (UnityEngine.Random.value < rt.def.idleChance)
            {
                return;
            }
            AxialI step;
            if (!PickFreeNeighbor(rt, UnityEngine.Random.Range(0, 6), false, out step))
            {
                return;
            }
            MoveMonster(rt, step);
            rt.state = MonsterState.Idle;
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        private static bool PickFreeNeighbor(MonsterRuntime rt, int startDir, bool towardTarget, out AxialI result)
        {
            result = default(AxialI);
            int[] dirs = new int[6];
            for (int i = 0; i < 6; i++)
            {
                dirs[i] = (startDir + i) % 6;
            }
            int found = -1;
            int bestScore = int.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                AxialI c = SpaceCfg.Neighbor(rt.Cell, dirs[i]);
                if (!TryStep(rt, c))
                {
                    continue;
                }
                if (found < 0)
                {
                    found = dirs[i];
                }
                if (towardTarget)
                {
                    int score = SpaceCfg.Distance(c, rt.leaveTargetSet ? rt.leaveTarget : c);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        found = dirs[i];
                    }
                }
            }
            if (found < 0)
            {
                return false;
            }
            result = SpaceCfg.Neighbor(rt.Cell, found);
            return true;
        }

        private static void MoveMonster(MonsterRuntime rt, AxialI cell)
        {
            rt.monster.Location = cell;
            OnMonsterMoved(rt);
            CombatBars.OnMonsterMoved(rt);   // 迷你条：跟随移动（事件驱动）
        }

        private static void TickMonsterCombat(MonsterRuntime rt, float now)
        {
            try
            {
            Clustercraft craft = rt.aggroCraft;
            if (craft == null)
            {
                ClearAggro(rt);
                return;
            }
            // 目标火箭不在星图或 HP=1 且无法开火 → 怪物侧直接脱战，与火箭侧共用同一判定
            if (ShouldDisengageFromCraft(craft))
            {
                MFLog.Verbose(LogCat.Combat,"[Space] " + rt.def.name + " 脱战（目标火箭不在星图或无法开火）；目标 " + craft.Name);
                ClearAggro(rt);
                rt.deaggroUntil = now + rt.def.deaggroSeconds;
                RocketPanelScreen.RequestRebuild();
                return;
            }
            int d = SpaceCfg.Distance(rt.Cell, craft.Location);
            // 跨星图（子↔主）轴坐标距离没有意义，视作无穷远；距离达到警戒 +2 立刻脱战，不留延迟
            if (RegionOf(craft.Location) != rt.region || d >= rt.def.alertRange + 2)
            {
                MFLog.Verbose(LogCat.Combat,"[Space] " + rt.def.name + " 脱战（达到警戒+2 或已跨星图）；目标 " + craft.Name);
                ClearAggro(rt);
                rt.deaggroUntil = now + rt.def.deaggroSeconds;   // 脱战冷却：这段时间内不重新上仇
                RocketPanelScreen.RequestRebuild();              // 交战面板同步撤下怪物卡（面板只在 dirty 时重建）
                return;
            }
            rt.deaggroUntil = 0f;
            rt.state = d <= rt.def.attackRange ? MonsterState.Combat : MonsterState.Pursue;
            // 逃跑判定（§9.2）：★ 问题5 —— 一次逃跑（fleeUsed 耗尽后不再触发），仅逃 3~5 格
            if (rt.def.canFlee && !rt.fleeUsed && rt.def.fleeHpPct > 0f && rt.hp <= rt.def.hp * rt.def.fleeHpPct)
            {
                if (rt.state != MonsterState.Flee)
                {
                    rt.fleeUntil = now + FleeMaxSeconds;   // ★ 逃跑触发即开窗口（最长 300s）
                    rt.fleeStepsLeft = UnityEngine.Random.Range(3, 6);   // ★ 问题5：仅逃 3~5 格
                }
                rt.state = MonsterState.Flee;
                rt.fleeUsed = true;   // ★ 问题5：耗尽逃跑次数（此后不再触发逃跑远离玩家）
                if (rt.def.fleeInstantStepOnce)
                {
                    AxialI step;
                    if (PickFreeNeighbor(rt, 0, false, out step))
                    {
                        MoveMonster(rt, step);
                        rt.fleeStepsLeft--;
                    }
                }
                return;
            }
            if (rt.state == MonsterState.Combat)
            {
                if (d <= rt.def.attackRange)
                {
                    if (now >= rt.attackUntil)
                    {
                        MonsterAttackCraft(rt, craft, now);
                    }
                }
                else
                {
                    rt.state = MonsterState.Pursue;
                }
            }
            if (rt.state == MonsterState.Pursue)
            {
                if (rt.def.chase && now >= rt.nextMoveAt)
                {
                    AxialI step;
                    if (PickToward(rt, craft.Location, out step))
                    {
                        MoveMonster(rt, step);
                        rt.nextMoveAt = now + ChaseInterval(rt, craft);   // 每格间隔：目标火箭每格 ×2.25
                    }
                    else if (now >= rt.idleUntil)
                    {
                        rt.idleUntil = now + UnityEngine.Random.Range(rt.def.idleMin, rt.def.idleMax);
                    }
                }
                if (SpaceCfg.Distance(rt.Cell, craft.Location) <= rt.def.attackRange)
                {
                    rt.state = MonsterState.Combat;
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        // ★ 追击间隔兜底（秒）：取不到目标速度（目标丢失 / 数值非法）时按 900 秒/格，保证绝不抛异常也不会追太快
        private const float ChaseIntervalFallback = 900f;

        // 追击每格间隔（秒）= 目标火箭每格耗时 × 2.25；火箭每格攒满 600 点潜力、Speed = 功率 ÷ 总载重
        private static float ChaseInterval(MonsterRuntime rt, Clustercraft craft)
        {
            if (rt.def.moveInterval > 0f)
            {
                return Mathf.Max(CombatDb.Shared.tickSeconds, rt.def.moveInterval);
            }
            if (craft == null || craft.ModuleInterface == null)
            {
                return ChaseIntervalFallback;   // 目标已失效/模块表未就绪：兜底 900s，不读 Speed（避开 null 迭代）
            }
            if (!craft.IsFlightInProgress())
            {
                return 0f;   // 目标没在飞（停靠/原地交火）：不节流，否则停着也追不上、永远打不到
            }
            float speed = craft.Speed;
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                return ChaseIntervalFallback;   // 数值非法：兜底，避免 nextMoveAt 变 NaN 后永远不再移动
            }
            if (speed <= 0.01f)
            {
                return 0f;   // 目标飞不起来（无驾驶员/机器人）：不节流，照常每 tick 扑上去，保证停着的火箭打得到
            }
            return Mathf.Max(CombatDb.Shared.tickSeconds, 600f / speed * ChaseIntervalScale);
        }

        private static bool PickToward(MonsterRuntime rt, AxialI target, out AxialI result)
        {
            result = default(AxialI);
            int best = -1;
            int bestD = int.MaxValue;
            int start = UnityEngine.Random.Range(0, 6);
            for (int i = 0; i < 6; i++)
            {
                AxialI c = SpaceCfg.Neighbor(rt.Cell, (start + i) % 6);
                if (!TryStep(rt, c))
                {
                    continue;
                }
                int dd = SpaceCfg.Distance(c, target);
                if (dd < bestD)
                {
                    bestD = dd;
                    best = (start + i) % 6;
                }
            }
            if (best < 0)
            {
                return false;
            }
            result = SpaceCfg.Neighbor(rt.Cell, best);
            return true;
        }

        private static void MonsterAttackCraft(MonsterRuntime rt, Clustercraft craft, float now)
        {
            rt.attackUntil = now + rt.def.attackInterval;
            rt.state = MonsterState.Combat;
            CombatFx.PlayMonster(rt.monster, craft, rt.def);
            RocketCombatState st = craft.GetComponent<RocketCombatState>();
            if (st == null)
            {
                return;
            }
            // ★ Bug3：受击【被动还击】——被攻击时，若本火箭没有仍然存活的指定目标，
            MonsterRuntime cur = FindByUid(st.orderedTargetUid);
            if (cur == null || !cur.IsAlive)
            {
                st.orderedTargetUid = rt.uid;
                rt.ordered = true;
            }
            st.inCombat = true;
            st.TakeHit(rt.def.attack, rt.def.armorPenetration, rt.def, now);
        }

        private static void TickMonsterFlee(MonsterRuntime rt, float now)
        {
            try
            {
            if (rt.fleeUntil > 0f && now >= rt.fleeUntil)
            {
                // ★ 逃跑窗口用完 → 回漫游（脱战本来也会回；这里是保底）
                rt.fleeUntil = 0f;
                rt.state = MonsterState.Idle;
                return;
            }
            if (rt.def.canFlee && rt.def.fleeHpPct > 0f && rt.hp > rt.def.hp * rt.def.fleeHpPct && rt.aggroCraft == null)
            {
                // 血量回来了且没仇恨 → 结束逃跑（回血只发生在无仇恨时）
                rt.fleeUntil = 0f;
                rt.state = MonsterState.Idle;
                return;
            }
            if (IsEdgeCell(rt.Cell, rt.region) && rt.fleeStepsLeft <= 0)
            {
                // ★ 问题5：逃跑不再"到边缘即离场"；只有步数耗尽（下面分支）才结束逃跑，
                rt.fleeUntil = 0f;
                rt.state = MonsterState.Idle;
                return;
            }
            // ★ 问题5：逃跑步数耗尽 → 结束逃跑，回到漫游（此后 fleeUsed 已置位，不再逃跑）
            if (rt.fleeStepsLeft <= 0)
            {
                rt.fleeUntil = 0f;
                rt.state = MonsterState.Idle;
                return;
            }
            Clustercraft threat = rt.aggroCraft;
            AxialI away = threat != null
                ? SpaceCfg.Mk(rt.Cell.Q + (rt.Cell.Q - threat.Location.Q), rt.Cell.R + (rt.Cell.R - threat.Location.R))
                : rt.Cell;
            AxialI step;
            if (PickToward(rt, away, out step))
            {
                MoveMonster(rt, step);
                rt.fleeStepsLeft--;   // ★ 问题5：消耗一跑步数
            }
            if (rt.aggroCraft == null && now >= rt.attackUntil)
            {
                // 逃跑中不攻击（K15），仅推进闹钟避免卡死
                rt.attackUntil = now;
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        private static void TickMonsterLeaving(MonsterRuntime rt, float now)
        {
            try
            {
            if (!rt.leaveTargetSet)
            {
                AxialI tgt = PickLeaveTarget(rt);
                if (tgt == rt.Cell)
                {
                    // ★ 边缘格全被占：自己已在边缘格就就地消失，否则原地等下一拍再选（不许在地图内部凭空消失）
                    if (IsEdgeCell(rt.Cell, rt.region))
                    {
                        EscapeMonster(rt);
                    }
                    return;
                }
                rt.leaveTargetSet = true;
                rt.leaveTarget = tgt;
            }
            if (rt.Cell == rt.leaveTarget)
            {
                EscapeMonster(rt);
                return;
            }
            AxialI step;
            if (PickToward(rt, rt.leaveTarget, out step))
            {
                MoveMonster(rt, step);
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        private static AxialI PickLeaveTarget(MonsterRuntime rt)
        {
            SpaceZones z = StarMapVisuals.Zones;
            if (z == null || !z.valid)
            {
                return rt.Cell;
            }
            List<AxialI> list = rt.region == "sub" ? z.subEdgeCells : z.mainEdgeCells;
            ClusterGrid grid = ClusterGrid.Instance;
            if (list == null || grid == null)
            {
                return rt.Cell;
            }
            for (int t = 0; t < Mathf.Max(1, CombatDb.Shared.spawnEdgeTries); t++)
            {
                AxialI c = list[UnityEngine.Random.Range(0, list.Count)];
                List<ClusterGridEntity> contents;
                if (grid.cellContents.TryGetValue(c, out contents) && contents != null && contents.Count > 0)
                {
                    continue;
                }
                if (RegionOf(c) != rt.region)
                {
                    continue;
                }
                return c;
            }
            return rt.Cell;
        }

        /// <summary>★ 任务失败/过期通知：把本任务下发的怪（ticket 认领）离场闹钟压到 leaveAt → 到点自己走。
        internal static void OrderIssuedLeaving(List<string> tickets, float leaveAt)
        {
            if (tickets == null || tickets.Count == 0)
            {
                return;
            }
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt == null || rt.monster == null)
                {
                    continue;
                }
                if (!tickets.Contains(rt.ticket))
                {
                    continue;
                }
                if (rt.leaveAt <= 0f || rt.leaveAt > leaveAt)
                {
                    rt.leaveAt = leaveAt;
                }
            }
        }

        /// <summary>主动离场（§6.4 的口子 + K18 离场闹钟）：随机选一个边缘空格 → 走到即消失。</summary>
        internal static void OrderLeave(MonsterRuntime rt)
        {
            if (rt == null)
            {
                return;
            }
            rt.state = MonsterState.Leaving;
            rt.leaveTargetSet = false;
        }

        private static void TickMonsterRegen(MonsterRuntime rt, float now)
        {
            try
            {
            if (rt.aggroCraft != null)
            {
                rt.regenUntil = 0f;
                return;
            }
            if (rt.hp >= rt.def.hp && rt.armor >= rt.def.armor)
            {
                if (rt.def.regenStopWhenFull)
                {
                    rt.regenUntil = 0f;
                }
                return;
            }
            if (rt.regenUntil <= 0f)
            {
                rt.regenUntil = now + rt.def.regenInterval;
                return;
            }
            if (now < rt.regenUntil)
            {
                return;
            }
            rt.regenUntil = now + rt.def.regenInterval;
            float oldHp = rt.hp;
            float oldArmor = rt.armor;
            rt.hp = Mathf.Min(rt.def.hp, rt.hp + rt.def.hp * rt.def.regenHpPct);
            rt.armor = Mathf.Min(rt.def.armor, rt.armor + rt.def.armor * rt.def.regenArmorPct);
            if (rt.monster != null)
            {
                rt.monster.hp = rt.hp;
                rt.monster.armor = rt.armor;
            }
            if (!Mathf.Equals(oldHp, rt.hp) || !Mathf.Equals(oldArmor, rt.armor))
            {
                CombatBars.PushMonster(rt);   // 回血/回甲也走事件推送
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }
    }
}
