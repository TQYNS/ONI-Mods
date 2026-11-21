// SpaceRealEventBuffHub.cs — 持续 buff 统一挂载通道：只做机制（去重/校验/记账/转发），策略由 BuffActions 委托传入

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 目标通道：hub 只用它决定把新目标转给哪些 watch，不关心业务含义
    [Flags]
    internal enum BuffChannel
    {
        None = 0,
        Dupes = 1,      // 复制人 / 仿生人（Components.LiveMinionIdentities）
        Plants = 2,     // 植物（种盆种植 与 野生）
        Droppers = 4,   // 带 DiseaseDropper 的实体（DiseaseDropper.Instance.StartSM）
    }

    // 策略包：由调用者提供，hub 只调用
    internal sealed class BuffActions
    {
        // 要不要挂这个目标（null = 不过滤）
        public Func<GameObject, bool> filter;
        // 怎么挂（返回是否成功；成功才会记入去重表）
        public Func<RealBuff, GameObject, bool> apply;
    }

    internal static class SpaceRealEventBuffHub
    {
        private sealed class WatchEntry
        {
            internal string id;
            internal BuffActions actions;
            internal BuffChannel channel;
        }

        // 当前存活的持续 buff 的补挂登记（buff 结束时由 Unwatch 摘掉）
        private static readonly List<WatchEntry> watches = new List<WatchEntry>();

        // 目标标识用实例 id，仅本次会话去重记账，不做持久化（读档时全部重建）
        internal static int IdOf(GameObject go)
        {
            return go != null ? go.GetInstanceID() : 0;
        }

        // 唯一挂载入口：去重 → 有效性校验 → 业务过滤 → 施加，任一步不通过即静默跳过
        internal static bool Attach(RealBuff b, GameObject go, BuffActions a)
        {
            if (b == null || go == null)
            {
                return false;
            }
            int id = IdOf(go);
            if (id == 0)
            {
                return false;
            }
            if (b.boundIds.Contains(id))
            {
                return false;   // 已挂过（读档重挂 / 订阅重复回调）
            }
            try
            {
                // UnityEngine.Object 重载 ==，这里再排除「存在但未激活」
                if (!go.activeInHierarchy)
                {
                    return false;
                }
                if (a != null && a.filter != null && !a.filter(go))
                {
                    return false;
                }
                bool ok = a != null && a.apply != null && a.apply(b, go);
                if (ok)
                {
                    b.boundIds.Add(id);
                }
                return ok;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] buff 挂载失败（" + b.id + " @" + go.name + "）");
                return false;
            }
        }

        // 批量挂载（首发 / 读档恢复都走这里），返回命中数
        internal static int AttachRange(RealBuff b, List<GameObject> targets, BuffActions a)
        {
            int hit = 0;
            if (b == null || targets == null)
            {
                return 0;
            }
            for (int i = 0; i < targets.Count; i++)
            {
                if (Attach(b, targets[i], a))
                {
                    hit++;
                }
            }
            return hit;
        }

        // 补挂登记：登记后新出现的目标由 OnTargetAppeared 自动补挂
        internal static void Watch(RealBuff b, BuffActions a, BuffChannel ch)
        {
            if (b == null || ch == BuffChannel.None)
            {
                return;
            }
            for (int i = 0; i < watches.Count; i++)
            {
                if (watches[i] != null && watches[i].id == b.id)
                {
                    return;   // 同一 buff 只登记一次
                }
            }
            watches.Add(new WatchEntry { id = b.id, actions = a, channel = ch });
        }

        // buff 结束时摘掉登记（由 EndBuff 调用）
        internal static void Unwatch(RealBuff b)
        {
            if (b == null)
            {
                return;
            }
            for (int i = watches.Count - 1; i >= 0; i--)
            {
                WatchEntry w = watches[i];
                if (w == null || w.id != b.id)
                {
                    continue;
                }
                watches.RemoveAt(i);
            }
        }

        // 订阅回调：外部钩子调用，hub 只转发给登记了对应通道且存活的 buff
        internal static void OnTargetAppeared(GameObject go, BuffChannel ch)
        {
            if (go == null || ch == BuffChannel.None || watches.Count == 0)
            {
                return;
            }
            try
            {
                for (int i = 0; i < watches.Count; i++)
                {
                    WatchEntry w = watches[i];
                    if (w == null || (w.channel & ch) == 0)
                    {
                        continue;
                    }
                    RealBuff b = SpaceRealEventRuntime.FindBuff(w.id);
                    if (b == null)
                    {
                        continue;   // 该 buff 已结束
                    }
                    Attach(b, go, w.actions);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] buff 补挂异常");
            }
        }

        // 该通道当前有没有人在等（外部钩子可先问一句，省掉后续开销）
        internal static bool AnyWatch(BuffChannel ch)
        {
            for (int i = 0; i < watches.Count; i++)
            {
                WatchEntry w = watches[i];
                if (w != null && (w.channel & ch) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        // 读档 / 新档复位：登记全部作废（buff 实例本身由 ResetAll 清理）
        internal static void ResetAll()
        {
            watches.Clear();
        }
    }
}
