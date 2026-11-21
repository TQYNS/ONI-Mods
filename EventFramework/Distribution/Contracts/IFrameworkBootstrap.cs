using System;
using HarmonyLib;

namespace EventFramework.Distribution.Contracts
{
    // 赢家实现的引导入口，Hub 只依赖本接口
    public interface IFrameworkBootstrap
    {
        // 仅用于激活后的日志核对，不参与择优
        Version Ver { get; }

        void Activate(Harmony harmony);

        bool Trigger(string id, string reason);

        // 触发并立即代玩家选定某个选项（卷轴「阅读即接取」这类主动凭证用）；
        // 走的是与面板点击完全相同的 ApplyChoice 路径，只是跳过等待。
        bool TriggerOption(string id, string optionId, string reason);

        // 某池里全部 scheduled 事件的 id（只读清单，供调试面板按 id 触发）
        string[] EventIds(string pool);
    }
}
