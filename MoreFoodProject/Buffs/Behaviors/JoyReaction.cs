// JoyReaction.cs — 行为示例 3「灵感激励」：按概率立刻触发原版喜悦反应（无计数）

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class JoyReaction
    {
        // 反射缓存的 GoToOverjoyed（跨程序集访问不到嵌套类型时走这条路）
        private static MethodInfo _goToOverjoyed;
        // 反射是否已失败（失败不重试）
        private static bool _reflectFailed;

        // 「没有 monitor」警告只打一次
        private static bool _warnedNoMonitor;

        // 立刻触发一次喜悦反应
        public static void Trigger(GameObject go)
        {
            if (go == null) return;

            // 主路径：直接拿 SMI 调 GoToOverjoyed
            var smi = go.GetSMI<JoyBehaviourMonitor.Instance>();
            if (smi != null)
            {
                smi.GoToOverjoyed();
                MFLog.Verbose(LogCat.Buff, "[MoreFood/Joy] " + go.name + " 立刻触发了喜悦反应");
                return;
            }

            // 兜底：反射（万一嵌套类型跨程序集不可访问）
            if (TryTriggerByReflection(go)) return;

            if (!_warnedNoMonitor)
            {
                _warnedNoMonitor = true;
                Debug.LogWarning("[MoreFood/Joy] " + go.name
                                 + " 身上没有 JoyBehaviourMonitor，喜悦反应触发失败");
            }
        }

        private static bool TryTriggerByReflection(GameObject go)
        {
            if (_reflectFailed) return false;

            try
            {
                if (_goToOverjoyed == null)
                {
                    var t = AccessTools.TypeByName("JoyBehaviourMonitor+Instance");
                    if (t == null)
                    {
                        _reflectFailed = true;
                        Debug.LogWarning("[MoreFood/Joy] 找不到 JoyBehaviourMonitor+Instance 类型");
                        return false;
                    }
                    _goToOverjoyed = AccessTools.Method(t, "GoToOverjoyed");
                    if (_goToOverjoyed == null)
                    {
                        _reflectFailed = true;
                        Debug.LogWarning("[MoreFood/Joy] 找不到 GoToOverjoyed 方法");
                        return false;
                    }
                }

                // 从 StateMachineController 里翻出这个 monitor 实例
                var smc = go.GetComponent<StateMachineController>();
                if (smc == null) return false;

                foreach (var inst in smc)
                {
                    if (inst == null) continue;
                    if (inst.GetType() != _goToOverjoyed.DeclaringType) continue;
                    _goToOverjoyed.Invoke(inst, null);
                    MFLog.Verbose(LogCat.Buff, "[MoreFood/Joy] " + go.name + " 立刻触发了喜悦反应（反射路径）");
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                _reflectFailed = true;
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Joy] 反射触发喜悦反应失败");
                return false;
            }
        }
    }
}
