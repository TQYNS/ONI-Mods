using System;
using System.Collections.Generic;

namespace EventFramework.Distribution.Contracts
{
    // 内容 Mod 的唯一公开入口，门面不代劳任何能力调用
    public static class EventFrameworkApi
    {
        public const string FixedHarmonyId = "MoreFood.EventFramework";

        public static IFrameworkBootstrap Current
        {
            get
            {
                try
                {
                    return EventFrameworkHub.Winner;
                }
                catch (Exception e)
                {
                    DistributionLog.Warn("取赢家失败：" + e);
                    return null;
                }
            }
        }

        public static void Install(FrameworkCandidate c)
        {
            try
            {
                EventFrameworkHub.Register(c);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("Install 失败：" + e);
            }
        }

        public static bool Trigger(string id, string reason)
        {
            try
            {
                IFrameworkBootstrap w = EventFrameworkHub.Winner;
                if (w == null)
                {
                    DistributionLog.WarnOnce("trigger-no-winner", "未激活时触发被忽略：" + id);
                    return false;
                }
                return w.Trigger(id, reason);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("触发失败：" + id + " → " + e);
                return false;
            }
        }

        // 触发事件并立即选定某个选项（主动凭证类入口用：阅读即生效，不等面板）
        public static bool TriggerOption(string id, string optionId, string reason)
        {
            try
            {
                IFrameworkBootstrap w = EventFrameworkHub.Winner;
                if (w == null)
                {
                    DistributionLog.WarnOnce("trigger-no-winner", "未激活时触发被忽略：" + id);
                    return false;
                }
                return w.TriggerOption(id, optionId, reason);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("触发并选项失败：" + id + " → " + e);
                return false;
            }
        }

        // 某池里全部 scheduled 事件的 id（调试面板用；只读，不产生调度）
        public static string[] EventIds(string pool)
        {
            try
            {
                IFrameworkBootstrap w = EventFrameworkHub.Winner;
                if (w == null) return new string[0];
                return w.EventIds(pool) ?? new string[0];
            }
            catch (Exception e)
            {
                DistributionLog.Warn("取池事件清单失败：" + pool + " → " + e);
                return new string[0];
            }
        }

        private static IEventBridge Bridge;

        // 宿主在 OnLoad 就注册的跳转实现要缓存到桥注入后再交出去
        private static Func<string, bool> PendingStarmapJump;

        // 激活前发生的注册全部排队，激活后按序补投，避免宿主注册被静默丢弃
        private static readonly List<System.Action<IEventBridge>> Pending = new List<System.Action<IEventBridge>>();

        // 由实现程序集在 Activate 时注入；内容 Mod 不调用
        public static void SetBridge(IEventBridge bridge)
        {
            try
            {
                Bridge = bridge;
                if (bridge != null && Pending.Count > 0)
                {
                    System.Action<IEventBridge>[] queued = Pending.ToArray();
                    Pending.Clear();
                    for (int i = 0; i < queued.Length; i++)
                    {
                        try { queued[i](bridge); }
                        catch (Exception e) { DistributionLog.Warn("补投注册失败：" + e); }
                    }
                }
                Func<string, bool> pending = PendingStarmapJump;
                if (bridge != null && pending != null)
                {
                    PendingStarmapJump = null;
                    bridge.SetStarmapJump(pending);
                }
            }
            catch (Exception e)
            {
                DistributionLog.Warn("注入事件桥失败：" + e);
            }
        }

        // 注册代码端事件处理器与前置条件：未激活时只告警一次，不抛
        public static void RegisterHandler(string key, EventHandler handler)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null)
                {
                    Pending.Add(br => br.RegisterHandler(key, handler));
                    DistributionLog.Info("框架未激活，处理器注册已排队：" + key);
                    return;
                }
                b.RegisterHandler(key, handler);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("注册事件处理器失败 " + key + "：" + e);
            }
        }

        public static void RegisterCondition(string key, Func<IEventContext, bool> fn, string[] dependencies = null)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null)
                {
                    Pending.Add(br => br.RegisterCondition(key, fn, dependencies));
                    DistributionLog.Info("框架未激活，前置条件注册已排队：" + key);
                    return;
                }
                b.RegisterCondition(key, fn, dependencies);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("注册前置条件失败 " + key + "：" + e);
            }
        }

        public static void RegisterHook(string key, System.Action<IEventContext> fn)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null)
                {
                    Pending.Add(br => br.RegisterHook(key, fn));
                    DistributionLog.Info("框架未激活，行为函数注册已排队：" + key);
                    return;
                }
                b.RegisterHook(key, fn);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("注册行为函数失败 " + key + "：" + e);
            }
        }

        public static void BindTargets(string instanceId, int defVersion, int[] entityIds)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null) return;
                b.BindTargets(instanceId, defVersion, entityIds);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("绑定跳转目标失败 " + instanceId + "：" + e);
            }
        }

        public static IEventContext[] ActiveContexts()
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null) return new IEventContext[0];
                return b.ActiveContexts();
            }
            catch (Exception e)
            {
                DistributionLog.Warn("取进行中事件失败：" + e);
                return new IEventContext[0];
            }
        }

        public static bool Jump(string instanceId)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null) return false;
                return b.Jump(instanceId);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("跳转失败 " + instanceId + "：" + e);
                return false;
            }
        }

        // 把物品投递到打印仓：找不到门 / id 无效 ⇒ false（由调用方决定后续）
        // quantity：资源按 kg，食物/小动物按件数。一行调用，不需要自己找 Telepad
        public static bool Deliver(string itemId, float quantity)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null)
                {
                    DistributionLog.Warn("投递请求被忽略：框架尚未激活（itemId=" + itemId + "）");
                    return false;
                }
                return b.Deliver(itemId, quantity);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("投递失败 " + itemId + "：" + e);
                return false;
            }
        }

        public static void ClosePanels(string reason)        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null) return;
                b.ClosePanels(reason);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("关闭事件面板失败：" + e);
            }
        }

        public static void SetStarmapJump(Func<string, bool> jump)
        {
            try
            {
                IEventBridge b = Bridge;
                if (b == null)
                {
                    PendingStarmapJump = jump;
                    DistributionLog.Info("星图跳转实现已缓存，等框架激活后生效");
                    return;
                }
                b.SetStarmapJump(jump);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("注册星图跳转失败：" + e);
            }
        }

    }
}
