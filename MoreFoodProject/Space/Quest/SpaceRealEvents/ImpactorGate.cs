// 用途：灾星闸口：任意来源的灾星广播一次并作废大灭绝，状态随档持久化（事件.md §17.6）

using System;

namespace MoreFood_KFC
{
    // 灾星闸口：广播只负责通知，状态写穿到 EventSaveData（挂 SaveGame，随档落盘）
    internal static class ImpactorGate
    {
        private const string ExtinctionEventId = "great_extinction";

        private static Action<string> occurred;
        private static bool pending;   // 载体不可用时的内存镜像（世界生成期 / 载入途中）

        internal static void Subscribe(Action<string> fn)
        {
            if (fn == null)
            {
                return;
            }
            occurred -= fn;
            occurred += fn;
        }

        // 每条只在生成时打一行；灾星频率极低，不在热路径（事件.md §20.7）
        internal static void Broadcast(string source)
        {
            MFLog.Info(LogCat.Event, "[Space] 灾星闸口 -1（{0}）", source);
            Action<string> h = occurred;
            if (h == null)
            {
                return;
            }
            Delegate[] list = h.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    ((Action<string>)list[i])(source);
                }
                catch (Exception e)
                {
                    MFLog.HotFail("impactor:gate", e);
                }
            }
        }

        // 消耗闸口并作废大灭绝：镜像先置位，载体可用时由 Flush 写穿
        internal static void MarkUsed()
        {
            pending = true;
            Flush();
            VoidExtinction("灾星已发生");
        }

        // 作废大灭绝的一次性资格：抽取阶段即不进池，避免每轮空转占掉抽取机会（事件.md §17.6）
        internal static void VoidExtinction(string why)
        {
            try
            {
                if (EventFramework.EventRegistry.Get(ExtinctionEventId) == null)
                {
                    return;   // 事件未加载（Init 之前）⇒ 由装载对账与条件函数兜底
                }
                if (EventFramework.EventSystem.MarkOneShotUsed(ExtinctionEventId, why))
                {
                    MFLog.Info(LogCat.Event, "[Space] 大灭绝作废（{0}）⇒ 不再进抽取池", why);
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("impactor:void", e);
            }
        }

        // 把只存在镜像里的状态补写进载体（世界生成期载体尚未建立）
        internal static void Flush()
        {
            if (!pending)
            {
                return;
            }
            EventSaveData d = EventSaveData.Attach();
            if (d != null)
            {
                d.impactorGateUsed = true;
            }
        }

        internal static bool IsSet()
        {
            EventSaveData d = EventSaveData.Attach();
            return pending || (d != null && d.impactorGateUsed);
        }

        // 装载对账：已置位就补作废；确有灾星在途而闸口未标记（世界生成期、旧档）⇒ 补记一次
        internal static void SyncWorldState()
        {
            try
            {
                Flush();                 // 生成期只有镜像，装载完成时载体已就绪 ⇒ 补落盘
                if (IsSet())
                {
                    VoidExtinction("装载对账");
                    return;
                }
                if (Db.Get() == null || Db.Get().GameplayEvents.LargeImpactor == null)
                {
                    return;
                }
                if (GameplayEventManager.Instance == null)
                {
                    return;
                }
                if (!GameplayEventManager.Instance.IsGameplayEventActive(Db.Get().GameplayEvents.LargeImpactor))
                {
                    return;
                }
                MarkUsed();
                MFLog.Verbose(LogCat.Event, "[Space] 灾星闸口：装载时已有灾星在途 ⇒ 补记为已消耗");
            }
            catch (Exception e)
            {
                MFLog.HotFail("impactor:gate", e);
            }
        }

        // 退档 / 新会话：镜像不跨会话；随档值由存档自行恢复
        internal static void ResetSession()
        {
            pending = false;
        }
    }
}
