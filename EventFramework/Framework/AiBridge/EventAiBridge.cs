// EventAiBridge.cs — 事件框架 · 【AI 桥接（空壳）】

using UnityEngine;

namespace EventFramework
{
    public static class EventAiBridge
    {
        // 是否打印桥接日志（默认关，避免刷屏；调试时置 true）
        public static bool Verbose = false;

        // 回调：OnEventTriggered（参数 inst：目标实例、def：定义）
        public static void OnEventTriggered(EventInstance inst, EventDef def)
        {
            try
            {
                if (inst == null) return;
                if (def != null && def.ai != null && !def.ai.enabled) return;

                if (Verbose)
                    Debug.Log("[MoreFood/Event][AI] (v1 空壳) OnEventTriggered "
                              + inst.templateId
                              + " summary=" + (def != null ? def.ai_summary : "-"));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AI] OnEventTriggered 异常（已忽略）: " + e);
            }
        }

        // 回调：OnEventEnded（参数 inst：目标实例、def：定义）
        public static void OnEventEnded(EventInstance inst, EventDef def)
        {
            try
            {
                if (inst == null) return;
                if (Verbose)
                    Debug.Log("[MoreFood/Event][AI] (v1 空壳) OnEventEnded " + inst.templateId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AI] OnEventEnded 异常（已忽略）: " + e);
            }
        }

        // 回调：OnPlayerChoice（参数 inst：目标实例、optionIndex：下标）
        public static void OnPlayerChoice(EventInstance inst, int optionIndex)
        {
            try
            {
                if (inst == null) return;
                if (Verbose)
                    Debug.Log("[MoreFood/Event][AI] (v1 空壳) OnPlayerChoice "
                              + inst.templateId + " idx=" + optionIndex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][AI] OnPlayerChoice 异常（已忽略）: " + e);
            }
        }
    }
}
