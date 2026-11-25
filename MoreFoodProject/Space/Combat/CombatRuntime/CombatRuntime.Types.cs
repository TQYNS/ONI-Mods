// 用途：战斗运行时类型：怪物状态、运行时态、生成器态、结算广播与事件总线

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 战斗运行时（§12.5~12.9/§14/§16/§17）

    internal enum MonsterState
    {
        Idle,
        Roam,
        Combat,
        Pursue,
        Flee,
        Leaving
    }

    internal sealed class MonsterRuntime
    {
        internal int uid;
        internal string ticket = "";            // 身份证：区分"是不是任务下发的那个"（第五轮）
        internal SpaceMonster monster;
        internal CombatMonsterDef def;
        internal string defId;
        internal string questId;
        internal bool ordered;
        // 运行期一次性解决门闩：防止同一实体重复击杀/离场、重复掉落或广播
        internal bool resolved;
        internal float hp;
        internal float armor;
        internal MonsterState state = MonsterState.Idle;
        internal float idleUntil;
        internal float leaveAt;     // 离场闹钟（绝对游戏秒；0 = 不排）
        internal float fleeUntil;   // 本次逃跑窗口截止（绝对游戏秒）
        internal float regenUntil;
        internal float attackUntil;
        internal float deaggroUntil;
        internal float nextMoveAt;   // 追击节流：下一次可移动的绝对游戏秒（运行期，不入存档）
        internal bool fleeUsed;
        internal int fleeStepsLeft;   // 问题5：本次逃跑剩余可移动格数（3~5，耗尽即不再逃跑、不再远离）
        internal Clustercraft aggroCraft;        // 运行期引用
        internal string aggroCraftName = "";    // 存档用（K11）
        internal string region;                 // "main" / "sub"
        internal AxialI leaveTarget;
        internal bool leaveTargetSet;

        internal bool IsAlive
        {
            get { return monster != null && hp > 0f; }
        }

        internal AxialI Cell
        {
            get { return monster != null ? monster.Location : default(AxialI); }
        }
    }

    internal sealed class SpawnerState
    {
        internal SpawnerCfg cfg;
        internal float nextSpawnAt;
        internal bool paused;           // 区域名额满 → 闹钟暂停（不空转、不空抽）
        internal float pausedRemain;    // 暂停时剩余的秒数（恢复时接着跑；存档一并存）

        internal SpawnerState(SpawnerCfg c)
        {
            cfg = c;
        }
    }

    // 广播（§16）

    internal enum MonsterResolution
    {
        Killed,
        Captured,
        Escaped
    }

    internal sealed class MonsterResolvedEvent
    {
        public string monsterId;
        public string questId;
        public string ticket;      // 身份证（第五轮）：订阅者据此决定"收全部"还是"只收自己下发的"
        public AxialI cell;
        public MonsterResolution resolution;
        public string byCraft;
    }

    internal sealed class EmergencyExitEvent
    {
        public string craftId;
        public AxialI fromCell;
        public AxialI toCell;
        public bool passive;
        public bool coolingDown;
        public float lossPct;
    }

    internal static class SpaceCombatBus
    {
        internal static event Action<MonsterResolvedEvent> OnResolved;
        internal static event Action<EmergencyExitEvent> OnEmergencyExit;

        internal static void Publish(MonsterResolvedEvent e)
        {
            try
            {
                Action<MonsterResolvedEvent> h = OnResolved;
                if (h != null)
                {
                    h(e);
                }
            }
            catch (Exception ex)
            {
                MFLog.Exc(LogCat.Combat, ex, "[Space] 怪物结算广播异常");
            }
        }

        internal static void PublishEmergencyExit(EmergencyExitEvent e)
        {
            try
            {
                Action<EmergencyExitEvent> h = OnEmergencyExit;
                if (h != null)
                {
                    h(e);
                }
            }
            catch (Exception ex)
            {
                MFLog.Exc(LogCat.Combat, ex, "[Space] 紧急脱离广播异常");
            }
        }

        internal static void ClearSubscribers()
        {
            OnResolved = null;
            OnEmergencyExit = null;
        }
    }
}
