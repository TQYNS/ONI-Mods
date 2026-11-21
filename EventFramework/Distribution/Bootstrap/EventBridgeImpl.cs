using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;

namespace EventFramework.Distribution.Bootstrap
{
    // 把宿主处理器看到的只读上下文包装到框架内部实例上；实例为空时保持空，宿主原有空判语义不变
    internal sealed class EventContextAdapter : IEventContext
    {
        private readonly EventInstance _inst;

        internal EventContextAdapter(EventInstance inst)
        {
            _inst = inst;
        }

        public string InstanceId
        {
            get { return _inst == null ? null : _inst.instanceId; }
        }

        public string TemplateId
        {
            get { return _inst == null ? null : _inst.templateId; }
        }

        public string DefId
        {
            get { return _inst == null || _inst.def == null ? null : _inst.def.id; }
        }

        public int DefOptionCount
        {
            get { return _inst == null || _inst.def == null || _inst.def.options == null ? 0 : _inst.def.options.Count; }
        }

        public int ChosenIndex
        {
            get { return _inst == null ? -1 : _inst.chosenIndex; }
        }

        public string ChosenOptionId
        {
            get
            {
                try
                {
                    if (_inst == null || _inst.def == null || _inst.def.options == null) return null;
                    int idx = _inst.chosenIndex;
                    if (idx < 0 || idx >= _inst.def.options.Count) return null;
                    EventOption opt = _inst.def.options[idx];
                    return opt == null ? null : opt.id;
                }
                catch (Exception e)
                {
                    DistributionLog.Warn("读取已选选项失败：" + e);
                    return null;
                }
            }
        }

        public string ShownName
        {
            get { return _inst == null ? null : _inst.GetShownName(); }
        }

        public string ShownDescription
        {
            get { return _inst == null ? null : _inst.GetShownDescription(); }
        }

        public HuntSpec Hunt
        {
            get { return _inst == null ? null : _inst.hunt; }
        }

        public float GetDefNumber(string key, float fallback)
        {
            try
            {
                if (_inst == null || _inst.def == null || string.IsNullOrEmpty(key)) return fallback;
                if (key == "impactor_delay_min_cycles") return _inst.def.impactor_delay_min_cycles;
                if (key == "impactor_delay_max_cycles") return _inst.def.impactor_delay_max_cycles;
                return fallback;
            }
            catch (Exception e)
            {
                DistributionLog.Warn("读取事件数值失败 " + key + "：" + e);
                return fallback;
            }
        }

        public bool HasDuration
        {
            get { return _inst != null && _inst.hasDuration; }
        }

        public float DurationRemaining
        {
            get { return _inst == null ? 0f : _inst.GetDurationRemaining(); }
        }

        public void SetNoticeArg(string key, string value)
        {
            if (_inst != null) _inst.SetNoticeArg(key, value);
        }

        public string GetNoticeArg(string key, string fallback)
        {
            if (_inst == null || _inst.NoticeArgs == null || key == null) return fallback;
            string value;
            return _inst.NoticeArgs.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetNoticeVariant(string key)
        {
            if (_inst != null) _inst.SetNoticeVariant(key);
        }

        internal static IEventContext Wrap(EventInstance inst)
        {
            return inst == null ? null : new EventContextAdapter(inst);
        }
    }

    // 契约层注册桥的实现：把 Contracts 的处理器翻译成框架内部结构
    internal sealed class EventBridgeImpl : IEventBridge
    {
        public void RegisterHandler(string key, Contracts.EventHandler handler)
        {
            try
            {
                if (handler == null) return;

                EventHandler inner = new EventHandler();
                if (handler.onTrigger != null)
                {
                    Action<IEventContext> cb = handler.onTrigger;
                    inner.onTrigger = inst => Invoke(cb, inst);
                }
                if (handler.onStart != null)
                {
                    Action<IEventContext> cb = handler.onStart;
                    inner.onStart = inst => Invoke(cb, inst);
                }
                if (handler.onEnd != null)
                {
                    Action<IEventContext> cb = handler.onEnd;
                    inner.onEnd = inst => Invoke(cb, inst);
                }
                if (handler.options != null)
                {
                    inner.options = new Dictionary<string, Action<EventInstance>>();
                    foreach (KeyValuePair<string, Action<IEventContext>> kv in handler.options)
                    {
                        if (kv.Value == null) continue;
                        Action<IEventContext> cb = kv.Value;
                        inner.options[kv.Key] = inst => Invoke(cb, inst);
                    }
                }
                inner.install = handler.install;
                inner.uninstall = handler.uninstall;

                EventHandlerRegistry.Register(key, inner);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("桥接事件处理器失败 " + key + "：" + e);
            }
        }

        public void RegisterCondition(string key, Func<IEventContext, bool> fn, string[] dependencies)
        {
            try
            {
                if (fn == null) return;
                Func<IEventContext, bool> cb = fn;
                EventConditionRegistry.Register(key, inst =>
                {
                    try
                    {
                        return cb(EventContextAdapter.Wrap(inst));
                    }
                    catch (Exception e)
                    {
                        DistributionLog.Warn("前置条件求值异常 " + key + "：" + e);
                        return false;
                    }
                }, dependencies);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("桥接前置条件失败 " + key + "：" + e);
            }
        }

        public void RegisterHook(string key, Action<IEventContext> fn)
        {
            try
            {
                if (fn == null) return;
                Action<IEventContext> cb = fn;
                EventHookRegistry.Register(key, inst => Invoke(cb, inst));
            }
            catch (Exception e)
            {
                DistributionLog.Warn("桥接行为函数失败 " + key + "：" + e);
            }
        }

        public void BindTargets(string instanceId, int defVersion, int[] entityIds)
        {
            try
            {
                EventLocator.BindRaw(instanceId, defVersion, entityIds);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("绑定跳转目标失败 " + instanceId + "：" + e);
            }
        }

        public IEventContext[] ActiveContexts()
        {
            try
            {
                IList<EventInstance> list = EventSystem.ActiveInstances;
                if (list == null || list.Count == 0) return new IEventContext[0];
                List<IEventContext> outp = new List<IEventContext>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    IEventContext c = EventContextAdapter.Wrap(list[i]);
                    if (c != null) outp.Add(c);
                }
                return outp.ToArray();
            }
            catch (Exception e)
            {
                DistributionLog.Warn("取进行中事件失败：" + e);
                return new IEventContext[0];
            }
        }

        public bool Jump(string instanceId)
        {
            try
            {
                return EventLocator.Jump(instanceId);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("跳转失败 " + instanceId + "：" + e);
                return false;
            }
        }

        public void ClosePanels(string reason)
        {
            try
            {
                EventPanel.CloseCurrent(reason);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("关闭事件面板失败：" + e);
            }
        }

        public void SetStarmapJump(Func<string, bool> jump)
        {
            try
            {
                EventLocator.StarmapJump = jump;
            }
            catch (Exception e)
            {
                DistributionLog.Warn("注册星图跳转失败：" + e);
            }
        }

        // 投递到打印仓：原样转发，不做任何数值加工
        // （第二个参数对资源是 kg、对小动物/食物是件数，由 CarePackageInfo 自己解释）
        public bool Deliver(string itemId, float quantity)
        {
            try
            {
                return PodDelivery.TryDeliver(itemId, quantity);
            }
            catch (Exception e)
            {
                DistributionLog.Warn("投递失败 " + itemId + "：" + e);
                return false;
            }
        }

        private static void Invoke(Action<IEventContext> cb, EventInstance inst)
        {
            try
            {
                cb(EventContextAdapter.Wrap(inst));
            }
            catch (Exception e)
            {
                DistributionLog.Warn("宿主事件处理器异常：" + e);
            }
        }
    }
}
