using System;
using HarmonyLib;
using EventFramework.Distribution.Contracts;

namespace EventFramework.Distribution.Bootstrap
{
    // 赢家入口，Hub 按这个固定类型全名取实例
    public sealed class FrameworkBootstrap : IFrameworkBootstrap
    {
        public Version Ver
        {
            get
            {
                try
                {
                    return typeof(FrameworkBootstrap).Assembly.GetName().Version;
                }
                catch (Exception e)
                {
                    DistributionLog.Warn("读取实现版本失败：" + e);
                    return new Version(0, 0, 0, 0);
                }
            }
        }

        public void Activate(Harmony harmony)
        {
            try
            {
                FrameworkActivation.Run(harmony);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("Activate 失败：" + e);
            }
        }

        public bool Trigger(string id, string reason)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    DistributionLog.WarnOnce("trigger-empty", "触发被忽略：事件 id 为空");
                    return false;
                }
                return EventSystem.Trigger(id, reason, null);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("触发失败：" + id + " → " + e);
                return false;
            }
        }

        public bool TriggerOption(string id, string optionId, string reason)
        {
            try
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(optionId))
                {
                    DistributionLog.WarnOnce("trigger-opt-empty", "触发并选项被忽略：id / optionId 为空");
                    return false;
                }
                return EventSystem.TriggerOption(id, optionId, reason);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("触发并选项失败：" + id + "#" + optionId + " → " + e);
                return false;
            }
        }

        public string[] EventIds(string pool)
        {
            try
            {
                return EventSystem.EventIds(pool);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("取池事件清单失败：" + pool + " → " + e);
                return new string[0];
            }
        }
    }
}
