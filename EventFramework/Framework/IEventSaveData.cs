// IEventSaveData.cs — 事件框架 · 【存档数据接口】；宿主实现（类型名/程序集不变 => 旧档兼容）

using System.Collections.Generic;

namespace EventFramework
{
    // 框架只认此接口；属性名与宿主 IEventSaveData 的 [Serialize] 字段一一对应
    public interface IEventSaveData
    {
        List<string> instId { get; set; }
        List<string> instTemplate { get; set; }
        List<int> instStartCycle { get; set; }
        List<float> instRemaining { get; set; }
        List<int> instState { get; set; }
        List<float> instResident { get; set; }
        List<int> instChoicePhase { get; set; }
        List<bool> instTriggerEffectDone { get; set; }
        List<string> instVisited { get; set; }
        // 每个实例的通知占位参数（"键=值;键=值"，已转义）——读档后状态行 tooltip 才能复原 {pct} 等内容
        List<string> instNoticeArgs { get; set; }
        // 每个实例冻结的文案（variants 抽中的那一组；空 = 没配变体）
        List<string> instShownName { get; set; }
        List<string> instShownDesc { get; set; }
        List<float> instChoiceEffect { get; set; }
        List<bool> instChoiceEffectDone { get; set; }
        List<int> instChosenIndex { get; set; }
        List<bool> instChosenByDefault { get; set; }
        List<bool> instBranchDecided { get; set; }
        List<string> instBranchNext { get; set; }
        List<float> instBranchAt { get; set; }
        List<string> lastTriggerId { get; set; }
        List<float> lastTriggerTime { get; set; }
        List<string> cooldownId { get; set; }
        List<float> cooldownAt { get; set; }
        List<string> branchNextId { get; set; }
        List<float> branchAtTime { get; set; }
        List<string> branchVisited { get; set; }
        List<bool> branchWaiting { get; set; }
        string narratorId { get; set; }
        // 当局叙事者：下次闹钟的绝对游戏秒 + 三项可调参数（0 = 旧档缺字段，走默认开局）
        float narrNextDueAt { get; set; }
        float narrI0 { get; set; }
        float narrW { get; set; }
        float narrR { get; set; }
        // 调度器槽闹钟（"槽名 + 下次到点绝对游戏秒" 并行表；旧档为空 = 读档按满间隔重排）
        List<string> slotName { get; set; }
        List<float> slotNextDueAt { get; set; }
        List<string> histTriggered { get; set; }        // 兼容字段：只有键（老档按 1 次）
        List<string> histTriggeredKeys { get; set; }    // 事件 id（新）
        List<int> histTriggeredCounts { get; set; }     // 对应次数，与 Keys 平行
        List<string> histChosen { get; set; }
        List<string> presetPromoted { get; set; }      // 预制池已降级的 id
        List<string> histInstId { get; set; }
        List<string> histTemplate { get; set; }
        List<string> histName { get; set; }
        List<int> histCycle { get; set; }
        List<string> histOutcome { get; set; }
        List<int> histChoice { get; set; }
        List<bool> histKey { get; set; }
        List<string> histTip { get; set; }
        List<string> locInstId { get; set; }
        List<string> locQueue { get; set; }
        List<int> locVersion { get; set; }

        void ResetAll();
        string Describe();
        int GetInt(List<string> keys, List<int> vals, string key, int dflt);
        void SetInt(List<string> keys, List<int> vals, string key, int val);
    }
}
