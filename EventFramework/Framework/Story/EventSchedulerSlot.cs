// EventSchedulerSlot.cs — 事件框架 · 【预制池顺序调度器】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    // 预制池调度器：扫描 pool == "preset" 的事件，按 time.start 排序，到周期尝试触发一次。
    // 语义（用户定，2026-11-12）：
    //   · 成功触发 ⇒ one_shot 被消耗 ⇒ 永久熔断，不再触发第二次；
    //   · 失败或已错过触发周期 ⇒ 降级进常规池（改 pool=normal + 存档标记，不可逆）；
    //   · 不在启动时缓存队列下标：每次启动 / 读档 / 触发后都从注册表重算，避免读档重放。
    public sealed class EventSchedulerSlot
    {
        // ---- 身份 ----
        public string Name = "P";                      // 槽位名（同叙事者内唯一）
        public string Pool = "preset";                 // 抽取的事件池名
        public bool Enabled = true;                    // false = 不跑

        // ---- 运行时 ----
        private EventNarrator _owner;                  // 所属叙事者（Bind 时填）
        private SchedulerHandle _handle;               // 当前闹钟句柄
        private bool _running;
        private float _nextDueAt = -1f;                // 下次到点绝对游戏秒
        private int _fired;                            // 累计成功触发数
        private int _promoted;                         // 本轮累计降级数
        private readonly List<EventDef> _queue = new List<EventDef>();   // 重算出来的待发队列（有序）

        public bool IsRunning { get { return _running; } }
        public float NextDueAt { get { return _nextDueAt; } }
        public int FiredCount { get { return _fired; } }
        public int PromotedCount { get { return _promoted; } }

        public override string ToString()
        {
            return Name + "<预制 池=" + Pool + " 待发=" + _queue.Count + ">"
                   + (Enabled ? "" : " (停用)");
        }

        // 绑定所属叙事者
        internal void Bind(EventNarrator owner)
        {
            _owner = owner;
        }

        private string AlarmName()
        {
            string ownerId = _owner != null ? _owner.Id : "?";
            return "MoreFoodEvent_Preset_" + ownerId + "_" + Name;
        }

        // ── 启动 / 停止 ────────────────────────────────────────────────
        public void Start()
        {
            try
            {
                Stop();
                if (!Enabled) return;
                if (GameScheduler.Instance == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Preset] GameScheduler 为空，槽 '" + Name + "' 未启动");
                    return;
                }
                _running = true;
                Rebuild();
                ScheduleNext();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Preset] '" + Name + "' Start 异常: " + e);
            }
        }

        public void Stop()
        {
            try { if (_handle.IsValid) _handle.ClearScheduler(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Preset] 清理闹钟异常: " + e); }
            _handle = default(SchedulerHandle);
            _running = false;
            _nextDueAt = -1f;
        }

        // 读档：预制池不依赖存档时刻（队列按当前时间重算），直接重建
        public void ResumeDue(float dueAt)
        {
            Start();
        }

        public void Restart()
        {
            if (_running) Start();
        }

        // ── 队列 ──────────────────────────────────────────────────────
        // 从注册表重算待发队列：本池 + 未熔断 + 未降级，按 time.start 升序、id 兜底
        private void Rebuild()
        {
            _queue.Clear();
            foreach (EventDef def in EventRegistry.All)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                string p = string.IsNullOrEmpty(def.pool) ? "normal" : def.pool;
                if (p != Pool) continue;
                if (EventSystem.IsOneShotUsed(def)) continue;   // 已成功 → 熔断
                _queue.Add(def);
            }

            _queue.Sort(delegate(EventDef a, EventDef b)
            {
                float ta = a.time != null ? Mathf.Max(0f, a.time.start) : 0f;
                float tb = b.time != null ? Mathf.Max(0f, b.time.start) : 0f;
                int c = ta.CompareTo(tb);
                return c != 0 ? c : string.CompareOrdinal(a.id, b.id);
            });
        }

        // 事件在哪一个周期算“准点”：start 秒 ÷ 600 向下取整
        private static int DueCycle(EventDef def)
        {
            float start = def.time != null ? Mathf.Max(0f, def.time.start) : 0f;
            return Mathf.FloorToInt(start / 600f);
        }

        // 取下一个该处理的事件：跳过已熔断的；跨周期的一律降级；否则按到点时间排闹钟
        private void ScheduleNext()
        {
            if (!_running) return;

            EventDef next = null;
            while (_queue.Count > 0)
            {
                EventDef def = _queue[0];
                if (EventSystem.IsOneShotUsed(def)) { _queue.RemoveAt(0); continue; }
                if (EventSystem.GetCycle() > DueCycle(def))
                {
                    Promote(def, "已错过触发周期（当前周期 " + EventSystem.GetCycle()
                                 + " > 预定周期 " + DueCycle(def) + "）");
                    _queue.RemoveAt(0);
                    continue;
                }
                next = def;
                break;
            }

            if (next == null)
            {
                _running = false;
                Debug.Log("[MoreFood/Event][Preset] '" + Name + "' 队列已走完（成功 " + _fired
                          + " 条，降级 " + _promoted + " 条）");
                return;
            }

            float want = next.time != null ? Mathf.Max(1f, next.time.start) : 1f;
            float now = EventSystem.GetGameTime();
            float wait = Mathf.Max(1f, want - now);
            _nextDueAt = now + wait;

            _handle = GameScheduler.Instance.Schedule(AlarmName(), wait,
                                                      new Action<object>(OnAlarm), null, null);

            Debug.Log("[MoreFood/Event][Preset] '" + Name + "' 下一个：'" + next.id
                      + "'（预定 " + want.ToString("F0") + "s ⇒ 等 " + wait.ToString("F0") + "s）");
        }

        // ── 到点 ──────────────────────────────────────────────────────
        private void OnAlarm(object data)
        {
            try
            {
                if (_queue.Count == 0)
                {
                    _running = false;
                    return;
                }

                EventDef def = _queue[0];
                _queue.RemoveAt(0);

                // ① 已成功过（极端情况：同一事件被别的路径触发过）→ 直接跳过
                if (EventSystem.IsOneShotUsed(def))
                {
                    Continue();
                    return;
                }

                // ② 跨周期 = 已错过（读档晚到）
                if (EventSystem.GetCycle() > DueCycle(def))
                {
                    Promote(def, "读档时已过触发周期");
                    Continue();
                    return;
                }

                // ③ 准点尝试一次：成功即熔断；失败滚去常规池
                bool fired = EventSystem.Trigger(def, "preset:" + Name);
                if (fired && EventSystem.IsOneShotUsed(def))
                {
                    _fired++;
                    Debug.Log("[MoreFood/Event][Preset] '" + Name + "' 准点触发成功：'" + def.id + "'（已熔断）");
                }
                else if (fired)
                {
                    // 兜底：yaml 归一化保证 preset ⇒ one_shot=true，正常走不到这里。
                    // 若真走到（运行时塞进来的 EventDef 绕过加载器），不降级就会 1 秒后原地重放 ⇒ 一律降级。
                    Promote(def, "触发成功但 one_shot 未被消耗（非预制语义）");
                }
                else
                {
                    Promote(def, "准点触发失败（条件不满足或 handler 缺失）");
                }

                Continue();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Preset] '" + Name + "' 回调异常: " + e);
            }
        }

        // 每次处理完一条都重算队列：把“别处触发导致的熔断 / 降级”一并反映进来
        private void Continue()
        {
            Rebuild();
            ScheduleNext();
        }

        // 降级：改 pool 标记 + 记账（随存档），此后由常规池按普通事件规则抽取
        private void Promote(EventDef def, string why)
        {
            if (def == null) return;
            _promoted++;
            EventSystem.PromotePreset(def);
            Debug.Log("[MoreFood/Event][Preset] '" + def.id + "' 降级进常规池：" + why);
        }
    }
}
