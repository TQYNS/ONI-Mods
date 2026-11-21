// EventFrameworkSaveData.cs — 事件框架 · 存档载体：框架自己挂到 SaveGame，只装事件状态与叙事者 4 标量（见 叙事者.md §5.2）

using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace EventFramework
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class EventFrameworkSaveData : KMonoBehaviour, IEventSaveData
    {
        // 1) 活跃事件实例（并行 List 扁平化）
        [Serialize] public List<string> instId = new List<string>();
        [Serialize] public List<string> instTemplate = new List<string>();
        [Serialize] public List<int> instStartCycle = new List<int>();
        [Serialize] public List<float> instRemaining = new List<float>();
        [Serialize] public List<int> instState = new List<int>();

        // ---- 驻留计时 + 两个计时器的完成标志 ----
        [Serialize] public List<float> instResident = new List<float>();
        [Serialize] public List<int> instChoicePhase = new List<int>();
        [Serialize] public List<bool> instTriggerEffectDone = new List<bool>();
        [Serialize] public List<string> instVisited = new List<string>();   // "a|b|c"

        // ---- 通知占位参数（"键=值;键=值"，已转义）----
        [Serialize] public List<string> instNoticeArgs = new List<string>();

        // ---- 冻结文案（variants 抽中的那一组；空 = 该事件没配变体）----
        [Serialize] public List<string> instShownName = new List<string>();
        [Serialize] public List<string> instShownDesc = new List<string>();

        // ---- 决策效果槽（根级/决策两层效果）----
        [Serialize] public List<float> instChoiceEffect = new List<float>();
        [Serialize] public List<bool> instChoiceEffectDone = new List<bool>();

        // ---- 决策结果 + 已决定的分支 ----
        [Serialize] public List<int> instChosenIndex = new List<int>();
        [Serialize] public List<bool> instChosenByDefault = new List<bool>();
        [Serialize] public List<bool> instBranchDecided = new List<bool>();
        [Serialize] public List<string> instBranchNext = new List<string>();
        [Serialize] public List<float> instBranchAt = new List<float>();

        // 2) 上次触发时刻（eventId → 游戏秒）
        [Serialize] public List<string> lastTriggerId = new List<string>();
        [Serialize] public List<float> lastTriggerTime = new List<float>();
        // 冷却结束时刻（成功执行时 roll 的 [min,max]，冷却期间不重掷）
        [Serialize] public List<string> cooldownId = new List<string>();
        [Serialize] public List<float> cooldownAt = new List<float>();

        // 3) 待兑现的分支（并行 List）
        [Serialize] public List<string> branchNextId = new List<string>();
        [Serialize] public List<float> branchAtTime = new List<float>();
        [Serialize] public List<string> branchVisited = new List<string>();   // "id1|id2|id3"
        [Serialize] public List<bool> branchWaiting = new List<bool>();

        // 3b) 事件历史（次数表 + 选项组合表）与当前叙事者 id
        [Serialize] public string narratorId = "";

        // 当局叙事者：下次闹钟绝对游戏秒 + 三项可调参数（0 = 旧档缺字段，走默认开局）
        [Serialize] public float narrNextDueAt = 0f;
        [Serialize] public float narrI0 = 0f;
        [Serialize] public float narrW = 0f;
        [Serialize] public float narrR = 0f;

        // 3c) 调度器槽闹钟（槽名 + 下次到点绝对游戏秒；读档续订用，旧档为空）
        [Serialize] public List<string> slotName = new List<string>();
        [Serialize] public List<float> slotNextDueAt = new List<float>();

        [Serialize] public List<string> histTriggered = new List<string>();        // 兼容字段：只有键
        [Serialize] public List<string> histTriggeredKeys = new List<string>();    // 事件 id
        [Serialize] public List<int> histTriggeredCounts = new List<int>();        // 对应次数
        [Serialize] public List<string> histChosen = new List<string>();

        // 4) 预制池：已降级到常规池的事件 id（不再被预制队列扫描；不可逆）
        [Serialize] public List<string> presetPromoted = new List<string>();

        // 5) 事件历史摘要（「事件」页 50 条 + 「历史」页关键 100 条）
        [Serialize] public List<string> histInstId = new List<string>();
        [Serialize] public List<string> histTemplate = new List<string>();
        [Serialize] public List<string> histName = new List<string>();
        [Serialize] public List<int> histCycle = new List<int>();
        [Serialize] public List<string> histOutcome = new List<string>();
        [Serialize] public List<int> histChoice = new List<int>();
        [Serialize] public List<bool> histKey = new List<bool>();
        [Serialize] public List<string> histTip = new List<string>();   // 与通知同源的正文（老档为空）

        // 6) 事件定位器（并行表：instId / "id|id|..." / 绑定时的模板版本号）
        [Serialize] public List<string> locInstId = new List<string>();
        [Serialize] public List<string> locQueue = new List<string>();
        [Serialize] public List<int> locVersion = new List<int>();

        // 工具：取/写 键值对（避免到处写查找循环）
        public int GetInt(List<string> keys, List<int> vals, string key, int dflt)
        {
            if (keys == null || vals == null) return dflt;
            int i = keys.IndexOf(key);
            return (i >= 0 && i < vals.Count) ? vals[i] : dflt;
        }

        // 往平行数组里写一个键值（键已存在则覆盖，否则追加）
        public void SetInt(List<string> keys, List<int> vals, string key, int val)
        {
            if (keys == null || vals == null) return;
            int i = keys.IndexOf(key);
            if (i >= 0) { if (i < vals.Count) vals[i] = val; }
            else { keys.Add(key); vals.Add(val); }
        }

        // ResetAll 必须清空【所有】[Serialize] 列表
        List<string> IEventSaveData.instId { get { return instId; } set { instId = value; } }
        List<string> IEventSaveData.instTemplate { get { return instTemplate; } set { instTemplate = value; } }
        List<int> IEventSaveData.instStartCycle { get { return instStartCycle; } set { instStartCycle = value; } }
        List<float> IEventSaveData.instRemaining { get { return instRemaining; } set { instRemaining = value; } }
        List<int> IEventSaveData.instState { get { return instState; } set { instState = value; } }
        List<float> IEventSaveData.instResident { get { return instResident; } set { instResident = value; } }
        List<int> IEventSaveData.instChoicePhase { get { return instChoicePhase; } set { instChoicePhase = value; } }
        List<bool> IEventSaveData.instTriggerEffectDone { get { return instTriggerEffectDone; } set { instTriggerEffectDone = value; } }
        List<string> IEventSaveData.instVisited { get { return instVisited; } set { instVisited = value; } }
        List<string> IEventSaveData.instNoticeArgs { get { return instNoticeArgs; } set { instNoticeArgs = value; } }
        List<string> IEventSaveData.instShownName { get { return instShownName; } set { instShownName = value; } }
        List<string> IEventSaveData.instShownDesc { get { return instShownDesc; } set { instShownDesc = value; } }
        List<float> IEventSaveData.instChoiceEffect { get { return instChoiceEffect; } set { instChoiceEffect = value; } }
        List<bool> IEventSaveData.instChoiceEffectDone { get { return instChoiceEffectDone; } set { instChoiceEffectDone = value; } }
        List<int> IEventSaveData.instChosenIndex { get { return instChosenIndex; } set { instChosenIndex = value; } }
        List<bool> IEventSaveData.instChosenByDefault { get { return instChosenByDefault; } set { instChosenByDefault = value; } }
        List<bool> IEventSaveData.instBranchDecided { get { return instBranchDecided; } set { instBranchDecided = value; } }
        List<string> IEventSaveData.instBranchNext { get { return instBranchNext; } set { instBranchNext = value; } }
        List<float> IEventSaveData.instBranchAt { get { return instBranchAt; } set { instBranchAt = value; } }
        List<string> IEventSaveData.lastTriggerId { get { return lastTriggerId; } set { lastTriggerId = value; } }
        List<float> IEventSaveData.lastTriggerTime { get { return lastTriggerTime; } set { lastTriggerTime = value; } }
        List<string> IEventSaveData.cooldownId { get { return cooldownId; } set { cooldownId = value; } }
        List<float> IEventSaveData.cooldownAt { get { return cooldownAt; } set { cooldownAt = value; } }
        List<string> IEventSaveData.branchNextId { get { return branchNextId; } set { branchNextId = value; } }
        List<float> IEventSaveData.branchAtTime { get { return branchAtTime; } set { branchAtTime = value; } }
        List<string> IEventSaveData.branchVisited { get { return branchVisited; } set { branchVisited = value; } }
        List<bool> IEventSaveData.branchWaiting { get { return branchWaiting; } set { branchWaiting = value; } }
        string IEventSaveData.narratorId { get { return narratorId; } set { narratorId = value; } }
        float IEventSaveData.narrNextDueAt { get { return narrNextDueAt; } set { narrNextDueAt = value; } }
        float IEventSaveData.narrI0 { get { return narrI0; } set { narrI0 = value; } }
        float IEventSaveData.narrW { get { return narrW; } set { narrW = value; } }
        float IEventSaveData.narrR { get { return narrR; } set { narrR = value; } }
        List<string> IEventSaveData.slotName { get { return slotName; } set { slotName = value; } }
        List<float> IEventSaveData.slotNextDueAt { get { return slotNextDueAt; } set { slotNextDueAt = value; } }
        List<string> IEventSaveData.histTriggered { get { return histTriggered; } set { histTriggered = value; } }
        List<string> IEventSaveData.histTriggeredKeys { get { return histTriggeredKeys; } set { histTriggeredKeys = value; } }
        List<int> IEventSaveData.histTriggeredCounts { get { return histTriggeredCounts; } set { histTriggeredCounts = value; } }
        List<string> IEventSaveData.histChosen { get { return histChosen; } set { histChosen = value; } }
        List<string> IEventSaveData.presetPromoted { get { return presetPromoted; } set { presetPromoted = value; } }
        List<string> IEventSaveData.histInstId { get { return histInstId; } set { histInstId = value; } }
        List<string> IEventSaveData.histTemplate { get { return histTemplate; } set { histTemplate = value; } }
        List<string> IEventSaveData.histName { get { return histName; } set { histName = value; } }
        List<int> IEventSaveData.histCycle { get { return histCycle; } set { histCycle = value; } }
        List<string> IEventSaveData.histOutcome { get { return histOutcome; } set { histOutcome = value; } }
        List<int> IEventSaveData.histChoice { get { return histChoice; } set { histChoice = value; } }
        List<bool> IEventSaveData.histKey { get { return histKey; } set { histKey = value; } }
        List<string> IEventSaveData.histTip { get { return histTip; } set { histTip = value; } }
        List<string> IEventSaveData.locInstId { get { return locInstId; } set { locInstId = value; } }
        List<string> IEventSaveData.locQueue { get { return locQueue; } set { locQueue = value; } }
        List<int> IEventSaveData.locVersion { get { return locVersion; } set { locVersion = value; } }

        public void ResetAll()
        {
            // ---- 活跃实例 ----
            instId.Clear(); instTemplate.Clear();
            instStartCycle.Clear(); instRemaining.Clear();
            instState.Clear(); instResident.Clear();
            instChoicePhase.Clear(); instTriggerEffectDone.Clear();
            instVisited.Clear();
            instNoticeArgs.Clear();
            instShownName.Clear();
            instShownDesc.Clear();
            instChoiceEffect.Clear();
            instChoiceEffectDone.Clear();
            instChosenIndex.Clear();
            instChosenByDefault.Clear();
            instBranchDecided.Clear();
            instBranchNext.Clear();
            instBranchAt.Clear();

            // ---- 上次触发时刻 ----
            lastTriggerId.Clear(); lastTriggerTime.Clear();
            cooldownId.Clear(); cooldownAt.Clear();

            // ---- 待兑现的分支 ----
            branchNextId.Clear(); branchAtTime.Clear();
            branchVisited.Clear(); branchWaiting.Clear();

            // ---- 事件历史 ----
            histTriggered.Clear(); histChosen.Clear();
            histTriggeredKeys.Clear(); histTriggeredCounts.Clear();

            narratorId = "";
            narrNextDueAt = 0f;
            narrI0 = 0f;
            narrW = 0f;
            narrR = 0f;
            slotName.Clear(); slotNextDueAt.Clear();
            presetPromoted.Clear();

            // ---- 历史摘要 ----
            histInstId.Clear(); histTemplate.Clear();
            histName.Clear(); histCycle.Clear();
            histOutcome.Clear(); histChoice.Clear();
            histKey.Clear();
            histTip.Clear();

            // ---- 事件定位器 ----
            locInstId.Clear(); locQueue.Clear(); locVersion.Clear();
        }

        public string Describe()
        {
            return "instances=" + instId.Count + " lastTriggered=" + lastTriggerId.Count
                 + " branches=" + branchNextId.Count + " presetPromoted=" + presetPromoted.Count;
        }
    }
}
