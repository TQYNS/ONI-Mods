using System;
using System.Collections.Generic;

namespace EventFramework.Distribution.Contracts
{
    // 事件文案变体：同一条事件配多组「名字 + 正文」，创建实例时抽一组并冻结
    public sealed class EventVariantDef
    {
        public string name { get; set; }
        public string description { get; set; }
    }

    // 狩猎任务规格（yaml 的 hunt: 段）：宿主按此挑怪、定投放数量与期限奖励
    public sealed class HuntSpec
    {
        public List<string> creature_tiers { get; set; } = new List<string>();
        public int creature_count { get; set; } = 1;
        public int duration_cycles { get; set; } = 8;
        public int reward_rep { get; set; } = 12;
        public int reward_currency_min { get; set; } = 0;
        public int reward_currency_max { get; set; } = 0;
    }

    // 事件上下文：宿主处理器能看到的全部信息，只含基元，不暴露实现程序集的任何类型
    public interface IEventContext
    {
        string InstanceId { get; }
        string TemplateId { get; }
        string DefId { get; }
        int DefOptionCount { get; }
        int ChosenIndex { get; }
        string ChosenOptionId { get; }
        bool HasDuration { get; }
        float DurationRemaining { get; }

        // 本实例冻结的文案（variants 抽中的那一组；未配 variants 时回落根级 name/description）
        string ShownName { get; }
        string ShownDescription { get; }

        // 本实例的狩猎规格（yaml 的 hunt: 段；未配返回 null）
        HuntSpec Hunt { get; }

        // 读事件 yaml 里的数值字段（未定义返回 fallback）
        float GetDefNumber(string key, float fallback);

        void SetNoticeArg(string key, string value);
        // 读取当前实例已冻结的参数；不存在时返回 fallback，绝不重新生成
        string GetNoticeArg(string key, string fallback);
        void SetNoticeVariant(string key);
    }

    // 代码端事件处理器：字段与框架内部同名结构一一对应，宿主只填自己要用的那几个
    public sealed class EventHandler
    {
        public System.Action<IEventContext> onTrigger;
        public System.Action<IEventContext> onStart;
        public System.Action<IEventContext> onEnd;
        public Dictionary<string, System.Action<IEventContext>> options;
        public System.Action install;
        public System.Action uninstall;
    }

    // 实现程序集在激活时注入的桥：内容 Mod 只通过这些方法回调框架，框架不反向引用宿主类型
    public interface IEventBridge
    {
        void RegisterHandler(string key, EventHandler handler);

        void RegisterCondition(string key, Func<IEventContext, bool> fn, string[] dependencies);

        void RegisterHook(string key, System.Action<IEventContext> fn);

        // 事件 → 世界实体 / 星图目标：实体 id 由内容 Mod 自己取，框架只保存
        void BindTargets(string instanceId, int defVersion, int[] entityIds);

        bool Jump(string instanceId);

        // 当前所有进行中事件（宿主只读遍历用）
        IEventContext[] ActiveContexts();

        void ClosePanels(string reason);

        // 内容 Mod 的星图跳转实现（按 instanceId 返回是否已跳转）
        void SetStarmapJump(Func<string, bool> jump);

        // 把物品投递到打印仓（找不到打印门 / id 无效 ⇒ false，由调用方决定后续）
        // quantity：资源按 kg，食物/小动物按件数（与 CarePackageInfo 一致，由游戏解释）
        bool Deliver(string itemId, float quantity);
    }
}
