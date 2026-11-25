// EventInstance.cs — 事件框架 · 【运行时状态】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
 // 轴①：效果（两条效果线共用一个轴，各自有具名槽）
    public enum EventEffectPhase { NotStarted = 0, Running = 1, Ended = 2 }

 // 轴②：选择（玩家表态了没有）
    public enum EventChoicePhase { Waiting = 0, ForcedPanel = 1, Chosen = 2 }

    public enum EventInstanceState
    {
        Pending = 0,   // 已创建，尚未进入
        Active  = 1,   // 正在进行
        Ended   = 2,   // 已结束（等待清理）
    }

    public class EventInstance
    {
        // ---- 标识 ----
        public string instanceId = null;
        // 模板外键 → EventRegistry 查
        public string templateId = null;

        // ---- 时间 ----
        public int   startCycle = 0;
        public float startTimeSeconds = 0f;      // GameClock.GetTime()
        // 剩余持续时间（游戏秒）。<= 0 且有 duration 时表示"效果该停了"
        public float remainingDuration = 0f;
        // 本实例是否带持续时间
        public bool  hasDuration = false;

        // ---- 驻留（等玩家选择）——与 duration 完全解耦 ----
        public float residentRemaining = 0f;
        // 本实例是否有驻留计时
        public bool  hasResident = false;

        // ---- 到期时刻（运行时，不存盘）----
        [NonSerialized] public float triggerEffectEndsAt = 0f;   // 根级效果槽
        [NonSerialized] public float choiceEffectEndsAt  = 0f;   // 决策效果槽
        [NonSerialized] public float residentEndsAt      = 0f;   // 选择轴（resident）
        // 通知文本的运行时占位参数（{key} → 值）；仅本会话有效，不入存档
        [NonSerialized] private Dictionary<string, string> _noticeArgs;
        public Dictionary<string, string> NoticeArgs { get { return _noticeArgs; } }
        public void SetNoticeArg(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_noticeArgs == null) _noticeArgs = new Dictionary<string, string>();
            _noticeArgs[key] = value;
        }

        // 分支变体键（对应 yaml notification.variants 的键）；null/空 = 走默认模板。
        //   随实例存盘：读档重推状态行时仍能取到正确变体。
        public string notifyVariant = null;
        public void SetNoticeVariant(string key) { notifyVariant = key; }

        // 冻结文案（随实例存盘）：创建时从 def.variants 抽一组，之后面板/通知/历史一律读它。
        //   空 = 该事件没配 variants，回落 def 根级 name/description。
        public string shownName = null;
        public string shownDescription = null;
        // 狩猎规格（不存盘）：创建时从 def.hunt 取；读档同样从 def 取回（规格本身不随机）
        [NonSerialized] public EventFramework.Distribution.Contracts.HuntSpec hunt = null;

        // 显示名：冻结值优先，其次事件模板名
        public string GetShownName()
        {
            if (!string.IsNullOrEmpty(shownName)) return shownName;
            return def != null ? def.GetNameText() : (templateId ?? "");
        }

        // 显示正文：冻结值优先，其次事件模板正文
        public string GetShownDescription()
        {
            if (!string.IsNullOrEmpty(shownDescription)) return shownDescription;
            return def != null ? def.GetDescText() : "";
        }
        [NonSerialized] public SchedulerHandle triggerEffectHandle;
        [NonSerialized] public SchedulerHandle choiceEffectHandle;
        [NonSerialized] public SchedulerHandle residentHandle;

        // 决策效果槽的剩余（存盘用；没配决策效果时为 0）
        public float choiceEffectRemaining = 0f;

 // 两条正交轴（《事件框架规划.md》§16.3）
        [NonSerialized] public bool triggerEffectDone = false;
        // 决策效果槽是否已结束
        [NonSerialized] public bool choiceEffectDone = false;
        // 是否有过任一效果启动（区分 NotStarted / Running 用）
        [NonSerialized] public bool effectAnyStarted = false;
        // 本实例是否有决策效果（选中项配了 duration）
        [NonSerialized] public bool hasChoiceEffect = false;
        // 轴② 当前状态
        [NonSerialized] public EventChoicePhase choicePhase = EventChoicePhase.Waiting;
        // 本实例是否持有一份"强制弹窗暂停"的引用
        [NonSerialized] public bool forcePauseHeld = false;

 // ---- 提交语义（见《事件框架规划.md》§17.2）----
        [NonSerialized] public bool triggerEffectExpiring = false;
        // 决策效果【结束函数执行中】
        [NonSerialized] public bool choiceEffectExpiring = false;
        // 收尾中（幂等守卫；此期间仍占互斥窗口）
        [NonSerialized] public bool finalizing = false;

 // ---- 分支（表态/无选项那一刻算好，事件收尾时兑现）----
        public bool   pendingBranchDecided = false;
        // 决定接的下一个事件 id；空串 = 明确不接后续
        public string pendingBranchNext = "";
        // 最早可触发时刻（绝对游戏秒 = 表态时刻 + 随机延迟）
        public float  pendingBranchAtTime = 0f;

        // 根级效果是否已结束（没配根级效果也算"已结束"）
        public bool RootEffectDone()
        {
            return !hasDuration || triggerEffectDone;
        }

        // 轴② 是否已表态
        public bool ChoiceChosen { get { return choicePhase == EventChoicePhase.Chosen; } }

        // 轴② 是否已在强制弹窗中（含已表态）
        public bool ChoiceForced { get { return choicePhase >= EventChoicePhase.ForcedPanel; } }

        // 轴① 是否已"判完"：两个槽都到头（没有该槽也算到头）
        public bool EffectAxisDone()
        {
            return RootEffectDone() && (!hasChoiceEffect || choiceEffectDone);
        }

        // 轴② 的文本形式（日志用）
        public string GetChoicePhaseText()
        {
            switch (choicePhase)
            {
                case EventChoicePhase.Chosen:
                    return "Chosen" + (chosenByDefault ? "(保底)" : "(玩家)");
                case EventChoicePhase.ForcedPanel:
                    return "ForcedPanel";
                default:
                    return "Waiting(剩 " + GetResidentRemaining().ToString("F0") + "s)";
            }
        }

        // 轴① 当前阶段（派生只读，日志/文档用）
        public EventEffectPhase GetEffectPhase()
        {
            if (!effectAnyStarted) return EventEffectPhase.NotStarted;
            return EffectAxisDone() ? EventEffectPhase.Ended : EventEffectPhase.Running;
        }

        // ---- 状态 ----
        public EventInstanceState state = EventInstanceState.Pending;

 // ---- 链上访问过的事件 id（：防环用；不存对象引用，只存事件 id）----
        public List<string> participantIds = new List<string>();

        // visited 上限：超过就丢最早的（环长通常 < 10，留足余量）
        public const int VISITED_MAX = 64;

        // 把一个事件 id 记进 visited（去重 + 上限裁剪）
        public void AddVisited(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (participantIds.Contains(id)) return;          // 去重
            participantIds.Add(id);
            while (participantIds.Count > VISITED_MAX) participantIds.RemoveAt(0);   // 丢最早
        }

        public string VisitedText()
        {
            return participantIds.Count == 0 ? "(空)" : string.Join(" → ", participantIds.ToArray());
        }

        // ---- 玩家选择 ----
        public List<int> playerChoices = new List<int>();
 // ：本次决策最终选中的项（-1 = 还没选）
        public int chosenIndex = -1;
 // ：最终选择是否由系统替选（超时保底 / 无面板自动 / 无选项）
        public bool chosenByDefault = false;

        // ---- 运行时（不存盘）----
        [NonSerialized] public EventDef def = null;
        [NonSerialized] public bool paused = false;

        // 工具
        public static EventInstance Create(EventDef def, int cycle, float nowSeconds)
        {
            var inst = new EventInstance
            {
                instanceId  = def.id + "#" + cycle + "#" + UnityEngine.Random.Range(1000, 9999),
                templateId  = def.id,
                startCycle  = cycle,
                startTimeSeconds = nowSeconds,
                state       = EventInstanceState.Pending,
                def         = def,
            };

            if (def.duration != null && def.duration.max > 0f)
            {
                inst.hasDuration = true;
                inst.remainingDuration = RollDuration(def);
                inst.triggerEffectEndsAt = nowSeconds + inst.remainingDuration;
            }

            // 变体冻结：此刻抽定，之后（含读档）都用这一组文案
            EventFramework.Distribution.Contracts.EventVariantDef variant = def.PickVariant();
            if (variant != null)
            {
                inst.shownName = variant.name;
                inst.shownDescription = variant.description;
            }
            // 狩猎规格不随机，直接引用模板；读档时同样从 def 取回
            inst.hunt = def.hunt;

            // 驻留计时：未配则默认 2 周期（1200 游戏秒）
            inst.hasResident = true;
            inst.residentRemaining = def.GetResidentSeconds();
            inst.residentEndsAt = nowSeconds + inst.residentRemaining;

            return inst;
        }

        // 根级效果槽的剩余（游戏秒）：有到期时刻就实时算，否则退回存盘值
        public float GetDurationRemaining()
        {
            if (triggerEffectEndsAt > 0f)
            {
                float r = triggerEffectEndsAt - EventSystem.GetGameTime();
                return r > 0f ? r : 0f;
            }
            return remainingDuration > 0f ? remainingDuration : 0f;
        }

        // 决策效果槽的剩余（游戏秒）
        public float GetChoiceEffectRemaining()
        {
            if (choiceEffectEndsAt > 0f)
            {
                float r = choiceEffectEndsAt - EventSystem.GetGameTime();
                return r > 0f ? r : 0f;
            }
            return choiceEffectRemaining > 0f ? choiceEffectRemaining : 0f;
        }

        // 剩余驻留时间（游戏秒）：有到期时刻就实时算，否则退回存盘值
        public float GetResidentRemaining()
        {
            if (residentEndsAt > 0f)
            {
                float r = residentEndsAt - EventSystem.GetGameTime();
                return r > 0f ? r : 0f;
            }
            return residentRemaining > 0f ? residentRemaining : 0f;
        }

        // 按配置随机一个根级效果时长（秒）；未配 ⇒ 0
        public static float RollDuration(EventDef def)
        {
            if (def == null || def.duration == null || def.duration.max <= 0f) return 0f;
            float lo = Mathf.Min(def.duration.min, def.duration.max);
            float hi = Mathf.Max(def.duration.min, def.duration.max);
            return UnityEngine.Random.Range(lo, hi);
        }

        public string ShortDesc()
        {
            return (templateId ?? "?") + "[" + state + "]" + (instanceId ?? "");
        }
    }
}
