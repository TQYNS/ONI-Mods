// EventNarratorDirector.cs — 事件框架 · 【调取层：谁来跑】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventNarratorDirector
    {
        // 当前运行的叙事者（null = 无）
        private static EventNarrator _running;

        // 读档待恢复的会话（只记数据，等 Game.OnSpawn 之后由 Resume 消费）
        private sealed class PendingSession
        {
            internal string id;
            internal float dueAt;
            internal float i0;
            internal float w;
            internal float r;
            internal List<string> slotNames;    // 调度器槽名（存档；null = 旧档无槽数据）
            internal List<float> slotDueAts;    // 对应槽的下次到点绝对游戏秒
        }
        private static PendingSession _pending;

        // 当前正在运行的叙事者（没有则为 null）
        public static EventNarrator Running { get { return _running; } }

        public static bool IsRunning { get { return _running != null && _running.IsRunning; } }

        // 调取
        public static EventNarrator ResolveDefault()
        {
            EventNarratorRegistry.EnsureBuiltins();
            var all = EventNarratorRegistry.All;

            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsDefault && all[i].Enabled) return all[i];

            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Enabled) return all[i];

            return null;
        }

        // 解析出要用的叙事者：先确保内置叙事者已建，再按 id 取
        public static EventNarrator Resolve(string id)
        {
            EventNarratorRegistry.EnsureBuiltins();
            return EventNarratorRegistry.Get(id);
        }

        // 当前在跑的（没有则取默认的）—— 读配置用
        public static EventNarrator CurrentOrDefault
        {
            get { return _running != null ? _running : ResolveDefault(); }
        }

        // 手动请求触发（M3 / B11）：调试按钮、界面按钮统一走这里。
        // 与调度器抽取的差别只在来源标记；真正的闸门在 EventSystem.Trigger 里按来源判断。
        public static bool RequestManual(string eventId, string source = "manual")
        {
            try
            {
                if (string.IsNullOrEmpty(eventId))
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 手动请求被忽略：事件 id 为空");
                    return false;
                }
                if (ResolveDefault() == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 手动请求被拒：没有可用叙事者 → " + eventId);
                    return false;
                }
                return EventSystem.Trigger(eventId, source);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] 手动请求异常 " + eventId + "：" + e);
                return false;
            }
        }

        // 叙事者选择函数（其他作者换叙事者只改这一处）
        public static string PickNarratorId()
        {
            var n = ResolveDefault();
            return n != null ? n.Id : null;
        }

        // 激活 / 停止
        public static bool ActivateDefault()
        {
            string id = PickNarratorId();
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[MoreFood/Event][Narr] PickNarratorId() 没给出可用的叙事者 id");
                return false;
            }
            return Activate(id);
        }

        // 调取入口：让指定叙事者开始跑。
        public static bool Activate(string id)
        {
            return Activate(id, -1f);
        }

        // dueAt > 0 = 读档续订闹钟；<= 0 = 新局首抽
        public static bool Activate(string id, float dueAt)
        {
            try
            {
                // 同 id 不再早退：读档 / 二次激活都必须先停旧闹钟再重排
                Deactivate();

                var n = Resolve(id);
                if (n == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 找不到叙事者 '" + id + "'");
                    return false;
                }
                if (!n.Enabled)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 叙事者 '" + id + "' 已停用（Enabled=false）→ 不激活");
                    return false;
                }

                _running = n;
                if (!n.StartWith(dueAt))
                {
                    _running = null;
                    return false;
                }
                Debug.Log("[MoreFood/Event][Narr] 调取层：当前叙事者 = '" + n.Id + "'（"
                          + EventNarratorRegistry.Describe() + "）");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] Activate 异常: " + e);
                return false;
            }
        }

        // 确保某个叙事者具备一个调度器槽（不存在则登记；已在跑则立刻开跑）
        public static EventSchedulerSlot EnsureSlot(string narratorId, EventSchedulerSlot slot)
        {
            if (string.IsNullOrEmpty(narratorId))
            {
                narratorId = PickNarratorId();
            }
            var n = Resolve(narratorId);
            if (n == null)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] EnsureSlot：找不到叙事者 '" + narratorId + "'");
                return null;
            }
            return n.AddSlot(slot);
        }

        public static void Deactivate()
        {
            try
            {
                if (_running != null)
                {
                    Debug.Log("[MoreFood/Event][Narr] 停止叙事者 '" + _running.Id + "'");
                    _running.Stop();
                    _running = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] Deactivate 异常: " + e);
            }
        }

        // 退档 / 进档：停闹钟、清面板与一次性警告，避免僵尸闹钟和跨会话残留
        public static void Suspend(string reason)
        {
            try
            {
                Deactivate();
                _pending = null;
                EventNarratorPanel.Hide();
                EventNarratorSelection.ResetForSession();
                EventWarnOnce.ResetPrefix("narr/");
                Debug.Log("[MoreFood/Event][Narr] 会话挂起（" + (reason ?? "-") + "）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] Suspend 异常: " + e);
            }
        }

        // 读档：只记 pending，真正的激活 / 续订留给 Resume（Game.OnSpawn 之后）
        public static void RestoreFrom(string id, float dueAt, float i0, float w, float r,
                                       List<string> slotNames = null, List<float> slotDueAts = null)
        {
            try
            {
                Deactivate();
                _pending = new PendingSession { id = id, dueAt = dueAt, i0 = i0, w = w, r = r,
                                                slotNames = slotNames, slotDueAts = slotDueAts };
                Debug.Log("[MoreFood/Event][Narr] 读档待恢复：id=" + (id ?? "无")
                          + " dueAt=" + dueAt.ToString("F0")
                          + " 配置=" + i0.ToString("F0") + "/" + w.ToString("F2") + "/" + r.ToString("F1"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] RestoreFrom 异常: " + e);
            }
        }

        // 读档后恢复：配置先写回注册表实例，再一次性激活并续订闹钟
        public static bool Resume()
        {
            try
            {
                var p = _pending;
                _pending = null;
                string id = (p != null && !string.IsNullOrEmpty(p.id))
                            ? p.id : EventNarratorSelection.ResolveForSession();
                if (string.IsNullOrEmpty(id)) id = PickNarratorId();
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] Resume 找不到叙事者 id，回退默认开局");
                    return ActivateDefault();
                }

                var n = Resolve(id);
                if (n == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] Resume 找不到叙事者 '" + id + "'，回退默认开局");
                    return ActivateDefault();
                }

                if (p != null && p.i0 > 0f)
                {
                    n.I0 = p.i0;
                    n.W = p.w;
                    n.R = p.r;
                    Debug.Log("[MoreFood/Event][Narr] 当局配置已恢复：" + n.ToString());
                }
                else
                {
                    // 旧档缺 4 字段 / 无 pending：复位到内置默认，不沿用上一局残留（D08）
                    EventNarratorRegistry.ResetParameters();
                    Debug.Log("[MoreFood/Event][Narr] 无当局配置可恢复 → 内置默认开局");
                }
                bool ok = Activate(id, p != null ? p.dueAt : -1f);

                // 调度器槽闹钟续订：有存档时刻的槽按剩余时间重排（缺失/过期 = 该槽保持满间隔重排）
                if (ok && p != null && p.slotNames != null)
                {
                    for (int i = 0; i < p.slotNames.Count; i++)
                    {
                        string sn = p.slotNames[i];
                        float slotDue = (p.slotDueAts != null && i < p.slotDueAts.Count) ? p.slotDueAts[i] : 0f;
                        if (string.IsNullOrEmpty(sn) || slotDue <= 0f) continue;
                        var slot = n.GetSlot(sn);
                        if (slot != null) slot.ResumeDue(slotDue);
                    }
                }
                return ok;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] Resume 异常: " + e);
                return false;
            }
        }

        // 新局首抽：不读 dueAt，也不恢复当局配置
        public static bool StartFresh(string id)
        {
            try
            {
                _pending = null;
                if (string.IsNullOrEmpty(id)) id = PickNarratorId();
                return Activate(id, -1f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] StartFresh 异常: " + e);
                return false;
            }
        }

        // 重排（改完配置 / 读档后）：在跑的照原样重启；没在跑的启动默认
        public static void RestartRunning()
        {
            if (_running != null) _running.Restart();
            else ActivateDefault();
        }

        // 对外便利读取（EventSystem 等直接问本层）
        public static float BranchWaitTimeoutSeconds
        {
            get
            {
                var n = CurrentOrDefault;
                return (n != null && n.BranchWaitTimeoutSeconds > 0f) ? n.BranchWaitTimeoutSeconds : 300f;
            }
        }

        // 把当前叙事者的配置打一行日志（排错用）
        public static void Dump()
        {
            var all = EventNarratorRegistry.All;
            Debug.Log("[MoreFood/Event][Narr] ── 叙事者清单（" + all.Count + "）当前='"
                      + (_running != null ? _running.Id : "无") + "'");
            for (int i = 0; i < all.Count; i++)
            {
                var n = all[i];
                if (n == null) continue;
                Debug.Log("[MoreFood/Event][Narr]   " + (n == _running ? "▶ " : "  ") + n);
            }
        }
    }
}
