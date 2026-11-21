// EventNarrator.cs — 事件框架 · 【叙事者本体】

using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    // 叙事者的调取方式
    public enum EventPickMode
    {
        // 单抽：每次最多 1 个；已有活跃事件 ⇒ 本次跳过（严格串行）
        single,
        // 多抽：每次连抽 MultiPickCount 个
        multi,
        // 并发：PickMode=single 时严格串行；其余模式不限（M3 起取消 MaxConcurrent 上限）
        concurrent,
    }

    public sealed class EventNarrator
    {
        // ④ 身份
        public const string MARKER = "narrator";

        // 统一标记（实例侧只读访问）
        public string Marker { get { return MARKER; } }

        // 唯一键（注册表按它查）。重复注册 ⇒ 警告并只认第一个
        public readonly string Id;

        // 显示名
        public string DisplayName;

        // 选择屏上的一句话风格说明（空 = 按参数自动拼）
        public string Description = null;

        // ★ 开关：false 的叙事者不会被 Director 激活
        public bool Enabled = true;

        // ★ 默认叙事者：Director 无参调取时取它（多个时取第一个 Enabled 的）
        public bool IsDefault = false;

        // ① 调取方式
        public EventPickMode PickMode = EventPickMode.concurrent;

        // multi 模式每次连抽几个
        public int MultiPickCount = 2;

        // ★ 本叙事者的默认事件池（主闹钟只抽这个池；槽可以指定别的池）
        public string Pool = "normal";

        private readonly List<EventSchedulerSlot> _slots = new List<EventSchedulerSlot>();

        // 同叙事者内的多个调度器槽（A/B/C…）
        public IList<EventSchedulerSlot> Slots { get { return _slots; } }

        // 给叙事者追加一个调度槽（同名槽会被替换），返回最终生效的槽
        public EventSchedulerSlot AddSlot(EventSchedulerSlot slot)
        {
            if (slot == null) return null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null && _slots[i].Name == slot.Name)
                {
                    _slots[i].Stop();
                    _slots[i] = slot;
                    slot.Bind(this);
                    if (_running) slot.Start();
                    return slot;
                }
            }
            _slots.Add(slot);
            slot.Bind(this);
            if (_running) slot.Start();
            Debug.Log("[MoreFood/Event][Narr] 叙事者 '" + Id + "' 登记调度器槽：" + slot);
            return slot;
        }

        // 按名字取调度槽；找不到返回 null
        public EventSchedulerSlot GetSlot(string name)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null && _slots[i].Name == name) return _slots[i];
            return null;
        }

        // 预制池：只要注册表里存在 scheduled + pool=="preset" 的事件，就挂一个顺序槽（幂等）
        private void EnsurePresetSlot()
        {
            try
            {
                if (GetSlot("P") != null) return;

                bool hasPreset = false;
                foreach (EventDef d in EventRegistry.All)
                {
                    if (d == null || d.trigger_mode != EventTriggerMode.scheduled) continue;
                    if (!string.IsNullOrEmpty(d.pool) && d.pool == "preset")
                    {
                        hasPreset = true;
                        break;
                    }
                }
                if (!hasPreset) return;

                AddSlot(new EventSchedulerSlot { Name = "P", Pool = "preset" });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] 预制槽挂载失败: " + e);
            }
        }

        // ① 三个可调参数（面板 3 滑块）；详见主方案 §4.4
        // 开局平均间隔（秒）
        public float I0 = 6000f;
        // 状态区间半宽比例
        public float W = 0.15f;
        // 平静期相对活跃期的倍数
        public float R = 2.0f;

        // ② 内置性格：两状态生成器 + 趋势 / 活跃占比的纯函数（面板派生显示用，不掷色子）
        public NarratorRule Rule = NarratorRules.Randy;
        public Func<float, float> TrendFunc = delegate (float t) { return 1f; };
        public Func<float, float> ActivityFunc = delegate (float t) { return 0.5f; };

        // 卡桑德拉终局间隔倍率；其余恒 1
        public float LEnd = 1f;
        // 负面目标占比 s(t)：由 S0 随趋势插值到 S1，二者相等即不随时间变化
        public float S0 = 0.30f;
        public float S1 = 0.30f;

        // 分支等互斥窗口的超时（秒）；超时放弃并写历史。<= 0 = 无限等
        public float BranchWaitTimeoutSeconds = 300f;

        // 开局保护期（周期）：新局首抽的首个闹钟不早于该时刻，避免开局即被事件打断
        public const float OpeningGraceCycles = 3f;

        // 运行状态（每个实例各有一份）
        private SchedulerHandle _handle;
        // 下次间隔（秒）
        private float _nextInterval = 120f;
        // 下次闹钟的绝对游戏秒（随本局存档落盘，读档据此续订）
        private float _nextDueAt = -1f;
        // 累计排过的闹钟数（诊断）
        private int _alarmCount;
        // 是否正在运行
        private bool _running;
        private readonly List<EventDef> _pool = new List<EventDef>();
        private readonly List<EventDef> _passed = new List<EventDef>();
        // 抽取序号（日志对照用）
        private int _pickSeq;
        // 本次闹钟的诊断量（一行式追踪 / 面板派生显示）
        private float _wNeg = 1f;
        private float _sUse;
        private float _sCap;
        private float _actualNeg;
        private NarratorState _lastState;
        private float _lastIMean;

        // ★抽取追踪：每次抽取打一整块日志（谁参选 / 谁被拒 / 为什么 / 抽没抽到）。
// 改动代码即可开关（本框架没有 yaml 开关，见规划 §17.10）
        public static bool TracePicks = true;

        public bool IsRunning { get { return _running; } }
        public int AlarmCount { get { return _alarmCount; } }
        public float NextDueAt { get { return _nextDueAt; } }

        public EventNarrator(string id)
        {
            Id = id ?? "?";
        }

        public override string ToString()
        {
            return Id + "[" + PickMode
                   + " I0=" + I0.ToString("F0") + "s"
                   + " w=" + W.ToString("F2")
                   + " R=" + R.ToString("F1")
                   + " L_end=" + LEnd.ToString("F2")
                   + " s=" + S0.ToString("F2") + "→" + S1.ToString("F2")
                   + (IsDefault ? " ★默认" : "")
                   + (Enabled ? "" : " (已停用)");
        }

        // 内置默认在登记时捕获；新局流程开始时复位，保证每局独立、不做跨会话持久化
        private float _defI0;
        private float _defW;
        private float _defR;
        private bool _defsCaptured;

        public void CaptureDefaults()
        {
            if (_defsCaptured) return;
            _defI0 = I0;
            _defW = W;
            _defR = R;
            _defsCaptured = true;
        }

        public void RestoreDefaults()
        {
            if (!_defsCaptured) return;
            I0 = _defI0;
            W = _defW;
            R = _defR;
        }

        // 面板派生显示取当前趋势与活跃占比，不掷色子、不改运行状态
        public void Probe(float t, out float activity, out float trend)
        {
            activity = 0.5f;
            trend = 1f;
            try
            {
                if (TrendFunc != null) trend = Mathf.Clamp01(TrendFunc(t));
                if (ActivityFunc != null) activity = Mathf.Clamp01(ActivityFunc(t));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] '" + Id + "' Probe 异常: " + e);
            }
        }

        // 启动 / 停止
        public void Start()
        {
            StartWith(-1f);
        }

        // 读档续订：dueAt > 0 时按剩余游戏秒排，否则按曲线重新抽一次（旧档缺字段走这里）
        public bool ResumeAlarm(float dueAt)
        {
            return StartWith(dueAt);
        }

        // 唯一的排期入口：dueAt <= 0 = 新局首抽；dueAt > 0 = 读档续订
        public bool StartWith(float dueAt)
        {
            try
            {
                Stop();

                var gs = GameScheduler.Instance;
                if (gs == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] GameScheduler.Instance 为 null，'" + Id + "' 未启动");
                    return false;
                }

                float now = EventSystem.GetGameTime();
                if (dueAt > 0f)
                {
                    float left = dueAt - now;
                    if (left > 0f)
                    {
                        _nextInterval = Mathf.Max(EventSystem.MinDrawIntervalSeconds, left);
                        Debug.Log("[MoreFood/Event][Narr] '" + Id + "' 续订闹钟：剩余 "
                                  + _nextInterval.ToString("F0") + "s（配置随存档恢复）");
                    }
                    else
                    {
                        _nextInterval = NextDelayNow();
                        Debug.Log("[MoreFood/Event][Narr] '" + Id + "' 存档闹钟已过期，重新抽样 Δt="
                                  + _nextInterval.ToString("F0") + "s");
                    }
                }
                else
                {
                    _nextInterval = NextDelayNow();
                    // 开局保护：首个闹钟不早于第 OpeningGraceCycles 周期
                    float grace = OpeningGraceCycles * 600f;
                    if (now < grace)
                        _nextInterval = Mathf.Max(_nextInterval, grace - now);
                }
                _nextDueAt = now + _nextInterval;

                _handle = gs.Schedule(AlarmName(), _nextInterval,
                                      new System.Action<object>(OnEventAlarm), null, null);
                _running = true;

                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i] != null) _slots[i].Start();
                }

                EnsurePresetSlot();

                Debug.Log("[MoreFood/Event][Narr] 叙事者 '" + Id + "' 已启动：" + ToString()
                          + "（下次闹钟 @" + _nextDueAt.ToString("F0") + "s，调度器槽 "
                          + _slots.Count + " 个）");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] '" + Id + "' 排期异常: " + e);
                return false;
            }
        }

        public void Stop()
        {
            try { if (_handle.IsValid) _handle.ClearScheduler(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Narr] 清理闹钟异常: " + e); }
            _handle = default(SchedulerHandle);
            _running = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null) _slots[i].Stop();
            }
        }

        // 重排（改完配置 / 读档后）
        public void Restart()
        {
            if (_running) Start();
        }

        private string AlarmName()
        {
            return "MoreFoodEvent_Alarm_" + Id;
        }

        // 两状态区间抽样：排期与一行式日志共用同一组诊断量，只掷一次色子
        private float NextDelayNow()
        {
            float t = EventSystem.GetGameTime() / 600f;
            NarratorState state;
            float iMean, activity, trend;
            var rule = Rule ?? NarratorRules.Randy;
            float dt = NarratorCurve.Sample(t, I0, W, R, rule, LEnd,
                                           out state, out iMean, out activity, out trend);
            _lastState = state;
            _lastIMean = iMean;
            _sCap = NarratorCurve.NegativeCap(iMean);
            float target = NarratorCurve.NegativeTarget(trend, S0, S1);
            _sUse = NarratorCurve.NegativeUse(target, _sCap);
            if (target > NarratorCurve.SupplySafety * _sCap + 0.0001f)
            {
                EventWarnOnce.Warn("narr/supply_clamp:" + Id,
                    "[MoreFood/Event][Narr] '" + Id + "' 负面目标超供给上限，已钳制到 "
                    + _sUse.ToString("F3") + "（目标 " + target.ToString("F3")
                    + " / 上限 " + _sCap.ToString("F3") + "）");
            }
            return dt;
        }

        // 正 / 中固定 1；负面由 s(t) 翻译层现算（主方案 §4.6）
        private float PolarityWeight(EventPolarity p)
        {
            return p == EventPolarity.negative ? Mathf.Max(0f, _wNeg) : 1f;
        }

        // 按最小优先级组现算 N_pos/N_neu/N_neg → 负面权重，不改加权循环
        private void PrepareWeights()
        {
            int nPos = 0, nNeu = 0, nNeg = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                var d = _pool[i];
                if (d == null) continue;
                if (d.polarity == EventPolarity.positive) nPos++;
                else if (d.polarity == EventPolarity.negative) nNeg++;
                else nNeu++;
            }
            _wNeg = NarratorCurve.NegativeWeight(_sUse, nPos, nNeu, nNeg);
            float total = nPos + nNeu + nNeg * _wNeg;
            _actualNeg = total > 0f ? nNeg * _wNeg / total : 0f;
        }

        // 僵尸闹钟三守卫：不是当前叙事者 / 已停 / 时钟未就绪 ⇒ 不抽取、不重排
        private bool IsCurrentAlarm()
        {
            try
            {
                return _running
                       && ReferenceEquals(EventNarratorDirector.Running, this)
                       && GameClock.Instance != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] 闹钟守卫异常: " + e);
                return false;
            }
        }

        // 闹钟回调 —— 叙事者的主循环
        private void OnEventAlarm(object data)
        {
            if (!IsCurrentAlarm()) return;
            _alarmCount++;
            try
            {
                _nextInterval = NextDelayNow();
                string picked = "无候选";
                bool fired = false;

                if (!PassConcurrencyGate())
                {
                    picked = "并发闸门拦下";
                }
                else
                {
                    var def = PickCandidate(Pool);
                    if (def != null)
                    {
                        picked = def.id;
                        fired = EventSystem.Trigger(def, "scheduler");
                    }
                }

                Debug.Log("[MoreFood/Event][Narr] '" + Id + "' 闹钟 #" + _alarmCount
                          + " 状态=" + (_lastState == NarratorState.Active ? "活跃" : "平静")
                          + " Δt=" + _nextInterval.ToString("F0") + "s"
                          + " I_mean=" + _lastIMean.ToString("F0") + "s"
                          + " 事件率=" + NarratorCurve.EventRatePerCycle(_lastIMean).ToString("F2") + "/周期"
                          + " 负面目标=" + _sUse.ToString("F2")
                          + " 实际=" + _actualNeg.ToString("F2")
                          + " 供给上限=" + _sCap.ToString("F2")
                          + " → " + picked + (fired ? " 已触发" : " 未触发"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Narr] 事件回调异常: " + e);
                _nextInterval = NarratorCurve.MinGapSeconds;
            }
            finally
            {
                try
                {
                    var gs = GameScheduler.Instance;
                    if (gs != null && _running)
                    {
                        _nextDueAt = EventSystem.GetGameTime() + _nextInterval;
                        _handle = gs.Schedule(AlarmName(), _nextInterval,
                                              new System.Action<object>(OnEventAlarm), null, null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 重排闹钟异常: " + e);
                }
            }
        }

        // 抽取
        public EventDef PickCandidate()
        {
            return PickCandidate(null);
        }

        // 按事件池抽取；poolFilter 为空 = 不筛（兼容旧行为）
        public EventDef PickCandidate(string poolFilter)
        {
            _pool.Clear();
            _pickSeq++;

            var lines = TracePicks ? new List<string>() : null;
            var passed = _passed;
            passed.Clear();

            int registered = 0;
            int considered = 0;

            foreach (var def in EventRegistry.All)
            {
                try
                {
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    registered++;
                    if (!string.IsNullOrEmpty(poolFilter))
                    {
                        string dp = string.IsNullOrEmpty(def.pool) ? "normal" : def.pool;
                        if (dp != poolFilter)
                        {
                            if (lines != null) lines.Add("    ✘ " + def.id + "  池=" + dp + " ≠ " + poolFilter);
                            continue;
                        }
                    }

                    string why = FailReasonMode(def);
                    if (why == null) { considered++; why = FailReasonRest(def); }

                    if (why == null)
                    {
                        passed.Add(def);
                        if (lines != null)
                            lines.Add("    ✔ " + def.id + "  priority=" + def.priority
                                      + " polarity=" + def.polarity);
                        continue;
                    }

                    if (lines != null) lines.Add("    ✘ " + def.id + "  " + why);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 候选检查 '" + (def != null ? def.id : "?")
                                   + "' 异常: " + e);
                }
            }

            int bestPriority = int.MaxValue;
            for (int i = 0; i < passed.Count; i++)
                if (passed[i].priority < bestPriority) bestPriority = passed[i].priority;

            for (int i = 0; i < passed.Count; i++)
            {
                var def = passed[i];
                if (def.priority != bestPriority)
                {
                    if (lines != null)
                        lines.Add("    ▵ " + def.id + "  过了过滤但优先级= " + def.priority
                                  + " 高于最低组 " + bestPriority + " → 本轮搁置");
                    continue;
                }
                _pool.Add(def);
            }

            PrepareWeights();

            EventDef chosen = null;
            string pickWhy = null;

            if (_pool.Count == 0)
            {
                pickWhy = considered == 0 ? "没有 scheduled 事件参与抽取" : "全部候选都被过滤掉了";
            }
            else if (_pool.Count == 1)
            {
                chosen = _pool[0];
                pickWhy = "唯一候选";
            }
            else
            {
                float total = 0f;
                for (int i = 0; i < _pool.Count; i++) total += PolarityWeight(_pool[i].polarity);

                if (total <= 0f)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] '" + Id
                                     + "' 的极性权重全为 0 → 退化为取第一个候选");
                    chosen = _pool[0];
                    pickWhy = "极性权重全 0 → 取第一个";
                }
                else
                {
                    float best = -1f;
                    float r = UnityEngine.Random.value * total;
                    for (int i = 0; i < _pool.Count; i++)
                    {
                        float w = PolarityWeight(_pool[i].polarity);
                        if (w > best) { best = w; chosen = _pool[i]; }
                        r -= w;
                        if (r <= 0f) { chosen = _pool[i]; break; }
                    }

                    var sbw = new System.Text.StringBuilder();
                    sbw.Append("加权随机（极性权重 ");
                    for (int i = 0; i < _pool.Count; i++)
                    {
                        if (i > 0) sbw.Append(" : ");
                        sbw.Append(_pool[i].id).Append("=").Append(PolarityWeight(_pool[i].polarity));
                    }
                    sbw.Append("）");
                    pickWhy = sbw.ToString();
                }
            }

            if (lines != null) LogPickTrace(registered, considered, passed.Count, bestPriority, chosen, pickWhy, lines);
            return chosen;
        }

        private void LogPickTrace(int registered, int considered, int passedCount, int bestPriority,
                                  EventDef chosen, string pickWhy, List<string> lines)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[MoreFood/Event][Narr] ── 抽取 #").Append(_pickSeq).Append("（叙事者 ").Append(Id)
              .Append("）登记 ").Append(registered).Append(" 个，参与抽取 ").Append(considered)
              .Append(" 个，过过滤 ").Append(passedCount).Append(" 个");

            if (passedCount > 0)
                sb.Append("，最低优先级组 priority=").Append(bestPriority)
                  .Append(" 共 ").Append(_pool.Count).Append(" 个");

            if (chosen != null)
                sb.Append("\n    ⇒ 抽中：").Append(chosen.id).Append("（").Append(pickWhy).Append("）");
            else
                sb.Append("\n    ⇒ 没抽到：").Append(pickWhy);

            for (int i = 0; i < lines.Count; i++) sb.Append('\n').Append(lines[i]);

            Debug.Log(sb.ToString());
        }

        private static string FailReasonMode(EventDef def)
        {
            if (def.trigger_mode != EventTriggerMode.scheduled)
                return "trigger_mode=" + def.trigger_mode + "（不进抽取池）";
            return null;
        }

        private static string FailReasonRest(EventDef def)
        {
            // ★ 开局时间门槛（M3）：yaml 的 time.start（秒）。未写 time 段的事件
            //   取 EventTimeBlock 的默认 600s（= 1 周期），这是既有设计，不是疏漏。
            if (def.time != null && def.time.start > 0f)
            {
                float nowSec = EventSystem.GetGameTime();
                if (nowSec < def.time.start)
                    return "未到开局时间（还差 " + (def.time.start - nowSec).ToString("F0") + "s）";
            }

            string why = FailReasonRepeat(def);
            if (why != null) return why;

            why = FailReasonOneShot(def);
            if (why != null) return why;

            why = FailReasonUnique(def);
            if (why != null) return why;

            why = FailReasonMutex(def);
            if (why != null) return why;

            why = FailReasonHandler(def);
            if (why != null) return why;

            why = FailReasonPreconditionKey(def);
            if (why != null) return why;

            return FailReasonCondition(def);
        }

        private static string FailReasonRepeat(EventDef def)
        {
            if (def.repeat == null) return null;

            float now = EventSystem.GetGameTime();

            // ★ 冷却优先：事件成功执行时已 roll 一次 [min_interval, max_interval] 并固定，
            //   冷却期间不重掷、不重抽；到点后本次冷却结束。
            float until;
            if (EventSystem.TryGetCooldown(def.id, out until))
            {
                if (now < until)
                    return "冷却中（剩余 " + (until - now).ToString("F0") + "s）";
                return null;
            }

            // 老档 / 尚无冷却记录 ⇒ 回落"上次触发 + min_interval"
            float last = EventSystem.GetLastTriggerTime(def.id);
            if (last < 0f) return null;

            float gap = now - last;
            float need = Mathf.Max(0f, def.repeat.min_interval);
            if (gap >= need) return null;

            return "重复间隔未到（已过 " + gap.ToString("F0") + "s / 需要 " + need.ToString("F0") + "s）";
        }

        private static string FailReasonOneShot(EventDef def)
        {
            return EventSystem.IsOneShotUsed(def) ? "one_shot 已用过（本存档烧掉了）" : null;
        }

        private static string FailReasonUnique(EventDef def)
        {
            var list = EventSystem.ActiveInstances;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a != null && a.state == EventInstanceState.Active && a.templateId == def.id)
                    return "同 id 已在跑（不会开第二份）";
            }
            return null;
        }

        private static string FailReasonMutex(EventDef def)
        {
            string blocker = EventSystem.MutexBlocker(def);
            return blocker == null ? null : "互斥：正被 '" + blocker + "' 挡着";
        }

        private static string FailReasonHandler(EventDef def)
        {
            if (string.IsNullOrEmpty(def.handler)) return null;
            return EventHandlerRegistry.Has(def.handler)
                ? null : "handler '" + def.handler + "' 没注册（事件不会跑）";
        }

        private static string FailReasonPreconditionKey(EventDef def)
        {
            string key = def.condition != null ? def.condition.function : null;
            if (string.IsNullOrEmpty(key)) return null;
            return EventConditionRegistry.Has(key)
                ? null : "condition.function '" + key + "' 没注册（抽取阶段先挡下）";
        }

        private static string FailReasonCondition(EventDef def)
        {
            string reason;
            if (EventSystem.CheckCondition(def, out reason)) return null;
            return "条件门不通过：" + (reason ?? "?");
        }

        // 并发闸门（M3 / B04-D09）：只剩两种语义 ——
        //   single：严格串行（有任何活跃事件就不抽）
        //   其余：不限制并发（是否共存在事件自己的 mutex_groups 里表达，不靠全局上限）
        private bool PassConcurrencyGate()
        {
            if (PickMode != EventPickMode.single) return true;

            var list = EventSystem.ActiveInstances;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a != null && a.state == EventInstanceState.Active) return false;
            }
            return true;
        }

    }

    // 注册层 —— 谁被登记了（与定义同层）
    public static class EventNarratorRegistry
    {
        // ---- 内置叙事者 id（裸名，不套命名空间）----
        public const string CASSANDRA = "cassandra";   // A · 渐进：越玩越紧张
        public const string RANDY = "randy";           // B · 完全随机
        public const string PHOEBE = "phoebe";         // C · 间歇：歇一段、刺激一下

        private static readonly List<EventNarrator> _all = new List<EventNarrator>();
        // 内置叙事者是否已注册
        private static bool _builtinsDone;

        public static List<EventNarrator> All { get { return _all; } }
        public static int Count { get { return _all.Count; } }

        // 登记一个叙事者。id 重复 ⇒ 警告 + 只认第一个（先到者保留，后到者忽略）
        public static bool Register(EventNarrator n)
        {
            if (n == null || string.IsNullOrEmpty(n.Id))
            {
                Debug.LogWarning("[MoreFood/Event][Narr] Register：id 为空，忽略");
                return false;
            }

            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] != null && _all[i].Id == n.Id)
                {
                    Debug.LogWarning("[MoreFood/Event][Narr] 叙事者 id 重复 '" + n.Id
                                     + "' → 只认第一个（后到的「" + n.DisplayName
                                     + "」被忽略；要接替请先 Unregister）");
                    return false;
                }
            }

            n.CaptureDefaults();
            _all.Add(n);
            Debug.Log("[MoreFood/Event][Narr] 已注册叙事者：" + n);
            return true;
        }

        // 从全局叙事者表移除指定 id；移除成功返回 true
        public static bool Unregister(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] != null && _all[i].Id == id)
                {
                    _all.RemoveAt(i);
                    Debug.Log("[MoreFood/Event][Narr] 已注销叙事者 '" + id + "'");
                    return true;
                }
            }
            return false;
        }

        // 按 id 取叙事者；找不到返回 null
        public static EventNarrator Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i] != null && _all[i].Id == id) return _all[i];
            }
            return null;
        }

        public static void Clear()
        {
            _all.Clear();
            _builtinsDone = false;
        }

        // 新一轮开局流程：把三位内置参数复位为登记时的默认值（每局独立）
        public static void ResetParameters()
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null) _all[i].RestoreDefaults();
        }

        // 登记内置的默认叙事者（幂等）。其他作者可在其后 Register 自己的
        public static int EnsureBuiltins()
        {
            if (_builtinsDone) return _all.Count;
            _builtinsDone = true;

            // A · 卡桑德拉：趋势确定、区间窄，负面目标从 0.16 升到 0.25
            Register(new EventNarrator(CASSANDRA)
            {
                DisplayName = "卡桑德拉",
                Description = "卡桑德拉创造的故事会变得越来越紧张，越来越有挑战性。她会根据一个经典的曲线来创造故事事件，先推送一些威胁事件，然后给你一些喘息的空间，之后再来一些威胁事件。",
                Enabled = true,
                IsDefault = true,
                PickMode = EventPickMode.concurrent,
                I0 = 6000f,
                W = 0.15f,
                R = 2.0f,
                LEnd = 0.80f,
                S0 = 0.16f,
                S1 = 0.25f,
                Rule = NarratorRules.Cassandra,
                TrendFunc = delegate (float t) { return t / NarratorRules.CassandraTrendCycles; },
                ActivityFunc = delegate (float t) { return t / NarratorRules.CassandraTrendCycles; },
                BranchWaitTimeoutSeconds = 300f,
            });

            // B · 兰迪：每轮独立掷色子，无趋势、区间最宽
            Register(new EventNarrator(RANDY)
            {
                DisplayName = "兰迪",
                Description = "兰迪不遵守任何规矩。他会随机创造各种事件，而不管故事的结局是大获成功还是充满绝望。对他来说只是场戏。警告：兰迪的故事充满了随机性，他有时会创造一些极端的事件或是不公平的事件组，这使得戏剧性更强——不可避免的牺牲。",
                Enabled = true,
                IsDefault = false,
                PickMode = EventPickMode.concurrent,
                I0 = 4200f,
                W = 0.75f,
                R = 2.0f,
                LEnd = 1f,
                S0 = 0.30f,
                S1 = 0.30f,
                Rule = NarratorRules.Randy,
                TrendFunc = delegate (float t) { return 1f; },
                ActivityFunc = delegate (float t) { return 0.5f; },
                BranchWaitTimeoutSeconds = 300f,
            });

            // C · 菲比：每 12 周期固定相位，前 2.4 周期活跃，其余长平静
            Register(new EventNarrator(PHOEBE)
            {
                DisplayName = "菲比",
                Description = "菲比在两次事件之间会给你相当宽裕的时间让你放松地经营殖民地。注意：如果你选择了较高的难度，她会和其他人一样残酷无情！",
                Enabled = true,
                IsDefault = false,
                PickMode = EventPickMode.concurrent,
                I0 = 4800f,
                W = 0.10f,
                R = 2.5f,
                LEnd = 1f,
                S0 = 0.15f,
                S1 = 0.15f,
                Rule = NarratorRules.Phoebe,
                TrendFunc = delegate (float t) { return 1f; },
                ActivityFunc = delegate (float t) { return NarratorRules.PhoebeDuty; },
                BranchWaitTimeoutSeconds = 300f,
            });

            Debug.Log("[MoreFood/Event][Narr] 内置叙事者已登记：" + Describe());
            return _all.Count;
        }

        public static string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("共 ").Append(_all.Count).Append(" 个叙事者（");
            for (int i = 0; i < _all.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(_all[i] != null ? _all[i].Id : "?");
            }
            sb.Append("）");
            return sb.ToString();
        }
    }
}
