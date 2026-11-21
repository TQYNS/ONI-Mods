// BuffConfig.cs — 通用 Buff 工厂/总表：链式 Add 定义 buff、全局按 id 引用（用法示例 / 属性 id 规则 / 显示格式化）

using System;
using System.Collections.Generic;
using KSerialization;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
// 一条数值改动：改哪个属性（attributeId）+ 改多少（value）+ 是否按百分比
    public class StatChange
    {
        // 目标属性 id（如 StatIds.Athletics）
        public readonly string attributeId;
        // 改动数值（百分比时为比例，0.2 = +20%）
        public readonly float value;
        // 是否按百分比
        public readonly bool isPercent;

        public StatChange(string attributeId, float value, bool isPercent)
        {
            this.attributeId = attributeId;
            this.value = value;
            this.isPercent = isPercent;
        }

        public override string ToString()
        {
            return attributeId + (isPercent ? " " + (value * 100f).ToString("+0.#;-0.#") + "%"
                                            : " " + value.ToString("+0.#;-0.#"));
        }

        // 本地化显示，如「运动 -3」「体力消耗 +20%」
        public string Format()
        {
            try
            {
                string s;

                // 1) XMax / XMin
                if (attributeId.EndsWith("Max", StringComparison.Ordinal) && attributeId.Length > 3
                    && TryFormatBound(attributeId.Substring(0, attributeId.Length - 3), "上限", out s))
                    return s;
                if (attributeId.EndsWith("Min", StringComparison.Ordinal) && attributeId.Length > 3
                    && TryFormatBound(attributeId.Substring(0, attributeId.Length - 3), "下限", out s))
                    return s;

                // 2) XDelta + 百分比 → 表述成"消耗速度"
                if (isPercent && attributeId.EndsWith("Delta", StringComparison.Ordinal)
                    && attributeId.Length > 5)
                {
                    string baseName = AmountName(attributeId.Substring(0, attributeId.Length - 5));
                    if (baseName != null)
                    {
                        float drainPct = -value * 100f;   // 负的百分比修正 = 消耗加快
                        return baseName + "消耗 " + drainPct.ToString("+0.#;-0.#") + "%";
                    }
                }

                // 3) 其余交给 AttributeModifier 自带格式化
                var m = new AttributeModifier(attributeId, value, (string)null, isPercent);
                string n = m.GetName();
                string v = m.GetFormattedString();
                return (string.IsNullOrEmpty(n) ? attributeId : n) + " " + v;
            }
            catch (Exception)
            {
                return ToString();
            }
        }

        private bool TryFormatBound(string amountId, string label, out string result)
        {
            result = null;
            string baseName = AmountName(amountId);
            if (baseName == null) return false;
            result = baseName + label + " " + value.ToString("+0.#;-0.#");
            return true;
        }

        // 取 Amount 的本地化名字；取不到返回 null（让上层回退）。
        private static string AmountName(string amountId)
        {
            if (Db.Get() == null || Db.Get().Amounts == null) return null;
            var amount = Db.Get().Amounts.TryGet(amountId);
            if (amount == null) return null;
            return string.IsNullOrEmpty(amount.Name) ? amountId : amount.Name;
        }
    }

    // 属性 id 常量：能力/抗性类名字取自反编译 Database.Attributes（44 个 public 字段），可直接用；Amount 派生的 id 还有 XMin / XMax / XDelta
    public static class StatIds
    {
        // 能力类 Attribute
        public const string Construction = "Construction";   // 建造
        public const string Digging      = "Digging";        // 挖掘  ⚠ 不是 Excavation
        public const string Machinery    = "Machinery";      // 机械（操作）
        public const string Athletics    = "Athletics";      // 运动
        public const string Learning     = "Learning";       // 学习
        public const string Cooking      = "Cooking";        // 烹饪
        public const string Caring       = "Caring";         // 照料（医疗）
        public const string Strength     = "Strength";       // 力量（搬运）
        public const string Art          = "Art";            // 艺术
        public const string Botanist     = "Botanist";       // 植物学
        public const string Ranching     = "Ranching";       // 畜牧
        public const string LifeSupport  = "LifeSupport";    // 生命维持
        public const string Toggle       = "Toggle";         // 开关
        public const string PowerTinker  = "PowerTinker";    // 电力调校
        public const string FarmTinker   = "FarmTinker";     // 农业调校
        public const string SpaceNavigation = "SpaceNavigation"; // 太空导航
        public const string MachinerySpeed  = "MachinerySpeed";  // 机械速度

        // 抗性 / 阈值
        public const string RadiationResistance = "RadiationResistance"; // 辐射抗性
        public const string RadiationRecovery   = "RadiationRecovery";   // 辐射恢复
        public const string GermResistance      = "GermResistance";      // 病菌抗性
        public const string Immunity            = "Immunity";            // 免疫力
        public const string Insulation          = "Insulation";          // 隔热
        public const string ThermalConductivityBarrier = "ThermalConductivityBarrier"; // 热传导屏障
        public const string ScoldingThreshold   = "ScoldingThreshold";   // 冻伤阈值（越高越耐寒）
        public const string ScaldingThreshold   = "ScaldingThreshold";   // 烫伤阈值（越高越耐热）
        public const string RoomTemperaturePreference = "RoomTemperaturePreference"; // 适宜室温
        public const string Sneezyness          = "Sneezyness";          // 打喷嚏

        // 效率 / 其它
        public const string CarryAmount          = "CarryAmount";          // 搬运量
        public const string ToiletEfficiency     = "ToiletEfficiency";     // 厕所效率
        public const string TransitTubeTravelSpeed = "TransitTubeTravelSpeed"; // 管道速度
        public const string DiseaseCureSpeed     = "DiseaseCureSpeed";     // 治病速度
        public const string DoctoredLevel        = "DoctoredLevel";        // 医疗等级
        public const string AirConsumptionRate   = "AirConsumptionRate";   // 耗氧速率
        // 水下移动耗时上限
        public const string MaxUnderwaterTravelCost = "MaxUnderwaterTravelCost";
        public const string Luminescence         = "Luminescence";         // 发光
        public const string GeneratorOutput      = "GeneratorOutput";      // 发电机输出
        public const string FoodQuality          = "FoodQuality";          // 食物品质
        public const string BionicBoosterSlots   = "BionicBoosterSlots";   // 仿生人插件槽
        // 仿生人电池数量上限
        public const string BionicBatteryCountCapacity = "BionicBatteryCountCapacity";

        // 士气 / 期望
        public const string Morale            = "QualityOfLife";              // 士气
        public const string MoraleExpectation = "QualityOfLifeExpectation";   // 士气期望
        public const string DecorExpectation  = "DecorExpectation";           // 装饰期望
        public const string FoodExpectation   = "FoodExpectation";            // 食物期望

        // Amount 本体（改上下限用 XMax / XMin）
        public const string Stamina     = "Stamina";      // 体力
        public const string Stress      = "Stress";       // 压力
        public const string Bladder     = "Bladder";      // 膀胱
        public const string HitPoints   = "HitPoints";    // 生命值
        public const string Breath      = "Breath";       // 呼吸
        public const string Temperature = "Temperature";  // 体温
        public const string Calories    = "Calories";     // 卡路里
        public const string Decor       = "Decor";        // 装饰

        // 变化速率（Amount 的 XDelta）
        public const string StaminaDelta   = "StaminaDelta";
        // 压力变化速率
        public const string StressDelta    = "StressDelta";
        // 膀胱变化速率
        public const string BladderDelta   = "BladderDelta";
        // 生命值变化速率
        public const string HitPointsDelta = "HitPointsDelta";
        // 呼吸变化速率
        public const string BreathDelta    = "BreathDelta";
        // 卡路里变化速率
        public const string CaloriesDelta  = "CaloriesDelta";
    }

    // 特殊效果的触发时机：Apply = 挂上 buff 时；Drink = 饮用结算时
    public enum SpecialTrigger
    {
        Apply,
        Drink,
    }

    // 一条特殊效果：独立概率、独立冷却；数值改动与它互不影响
    public class BuffSpecial
    {
        // 本 buff 内唯一（和 buffId 一起组成冷却存档键）
        public readonly string key;
        // 触发概率（0~1，1 = 必定）
        public readonly float chance;
        // 冷却周期；0 = 用设置页的全局值
        public readonly int cooldownCycles;
        // 触发时机
        public readonly SpecialTrigger trigger;
        // 实际效果；返回 true 表示真的生效（只有生效才开始冷却）
        public readonly Func<GameObject, bool> action;
        // 展示用简述
        public readonly string summary;

        public BuffSpecial(string key, float chance, int cooldownCycles,
                           SpecialTrigger trigger, Func<GameObject, bool> action, string summary)
        {
            this.key = key;
            this.chance = Mathf.Clamp01(chance);
            this.cooldownCycles = cooldownCycles < 0 ? 0 : cooldownCycles;
            this.trigger = trigger;
            this.action = action;
            this.summary = summary;
        }
    }

// 一个 buff / 惩罚的定义：一组数值改动 + 展示信息 + 可选特殊效果
    public class BuffDef
    {
        // buff id（全局唯一，按它引用）
        public readonly string id;
        // 显示名
        public string name;
        // 描述
        public string description;
        // 是否负面（决定 UI 分类与配色）
        public bool isBad;
        // 是否显示在小人的"修正"列表里
        public bool showInUI = true;
        // 持续时间，0 = 永久（由调用方自己挂/摘）
        public float duration = 0f;

        public readonly List<StatChange> changes = new List<StatChange>();

        // 特殊效果（自定义函数）：每条独立判定、独立冷却；数值改动不受它影响
        public readonly List<BuffSpecial> specials = new List<BuffSpecial>();

        // 挂上时触发（可空）
        public Action<GameObject, BuffDef> onApply;
        // 摘掉时触发（可空）
        public Action<GameObject, BuffDef> onExpire;
        // 每秒触发（可空）；写了会自动给目标挂一个 BuffTicker
        public Action<GameObject, BuffDef, float> onTick;

        public BuffDef(string id, string name, string description, bool isBad = false)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.isBad = isBad;
        }

        // 直接加减（加法）。
        public BuffDef Add(string attributeId, float value)
        {
            changes.Add(new StatChange(attributeId, value, false));
            return this;
        }

        // 百分比修正（乘法）。
        public BuffDef AddPercent(string attributeId, float fraction)
        {
            changes.Add(new StatChange(attributeId, fraction, true));
            return this;
        }

        // 变化速率加速：AddDrainAccel(StatIds.Stamina, 0.20f) = 体力消耗加快 20%
        public BuffDef AddDrainAccel(string amountId, float fraction)
        {
            changes.Add(new StatChange(amountId + "Delta", -Mathf.Abs(fraction), true));
            return this;
        }

        // 变化速率减速
        public BuffDef AddDrainSlow(string amountId, float fraction)
        {
            changes.Add(new StatChange(amountId + "Delta", Mathf.Abs(fraction), true));
            return this;
        }

        // 改某个 Amount 的当前值上限（XMax）
        public BuffDef AddMax(string amountId, float delta)
        {
            changes.Add(new StatChange(amountId + "Max", delta, false));
            return this;
        }

        // 可选行为 —— 链式末尾追加，不写也完全没问题

        // 挂上时执行。例（即兴演出的雏形）：
        public BuffDef OnApply(Action<GameObject, BuffDef> cb) { onApply = cb; return this; }

        // 摘掉时执行，适合清理计数/临时组件
        public BuffDef OnExpire(Action<GameObject, BuffDef> cb) { onExpire = cb; return this; }

        // 每秒执行，适合"还剩 N 次"这类倒计时/计数逻辑
        public BuffDef OnTick(Action<GameObject, BuffDef, float> cb) { onTick = cb; return this; }

        // 追加一条特殊效果（例：.Special("trait", 0.6f, go => ..., summary: "暂时获得「简单口味」")）
        public BuffDef Special(string key, float chance, Func<GameObject, bool> action,
                               SpecialTrigger trigger = SpecialTrigger.Drink,
                               int cooldownCycles = 0, string summary = null)
        {
            if (string.IsNullOrEmpty(key) || action == null)
            {
                MFLog.Warn(LogCat.Buff, "[MoreFood/Buff] 「" + id + "」的特殊效果缺少 key/action，已忽略");
                return this;
            }
            for (int i = 0; i < specials.Count; i++)
            {
                if (specials[i].key != key) continue;
                MFLog.Warn(LogCat.Buff, "[MoreFood/Buff] 「" + id + "」的特殊效果 key 重复：" + key + "（已忽略）");
                return this;
            }
            specials.Add(new BuffSpecial(key, chance, cooldownCycles, trigger, action, summary));
            return this;
        }

        // 必定触发（100%）的特殊效果
        public BuffDef Special(string key, Func<GameObject, bool> action,
                               SpecialTrigger trigger = SpecialTrigger.Drink,
                               int cooldownCycles = 0, string summary = null)
        {
            return Special(key, 1f, action, trigger, cooldownCycles, summary);
        }

        // 是否挂了特殊效果
        public bool HasSpecial
        {
            get { return specials.Count > 0; }
        }

        // 该特殊效果实际使用的冷却周期（自身写了就用自身，否则取设置页全局值）
        public static int EffectiveCooldownCycles(BuffSpecial s)
        {
            if (s == null) return 0;
            if (s.cooldownCycles > 0) return s.cooldownCycles;
            return Mathf.Max(1, ModOptions.Current.SpecialEffectCooldownCycles);
        }

        // 特殊效果展示行（每条一行：概率 + 简述；冷却周期不对玩家展示）
        public string BehaviorLine()
        {
            if (specials.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < specials.Count; i++)
            {
                BuffSpecial s = specials[i];
                if (string.IsNullOrEmpty(s.summary)) continue;
                if (sb.Length > 0) sb.Append('\n');
                if (s.chance >= 1f) sb.Append("必定触发：");
                else sb.Append("触发概率 ").Append((s.chance * 100f).ToString("0.#")).Append("%：");
                sb.Append(s.summary);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // 多行摘要，用于详情面板「效果」栏的悬浮提示：每个数值改动单独一行（"\n    • " 前缀，风格同原版 Effect.CreateTooltip）
        public string Summary()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(name);
            for (int i = 0; i < changes.Count; i++)
                sb.Append("\n    • ").Append(changes[i].Format());
            return sb.ToString();
        }

        // 转成游戏用的 Effect 对象
        public Effect Build()
        {
            Effect e = new Effect(id, name, description, duration, showInUI, false, isBad);
            foreach (var c in changes)
                e.Add(new AttributeModifier(c.attributeId, c.value, description, c.isPercent));
            return e;
        }
    }

    // Buff 工厂 + 总表。
    public static class BuffConfig
    {
        private static readonly Dictionary<string, BuffDef> _defs = new Dictionary<string, BuffDef>();
        private static readonly List<string> _order = new List<string>();

        // 新建并注册一个 buff，返回定义以便链式追加改动。重复 id 会覆盖定义。
        public static BuffDef Add(string id, string name, string description, bool isBad = false)
        {
            var def = new BuffDef(id, name, description, isBad);
            Register(def);
            return def;
        }

        // 注册一个已构造好的定义（同样支持链式的那种写法）
        public static void Register(BuffDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            if (!_defs.ContainsKey(def.id)) _order.Add(def.id);
            _defs[def.id] = def;
        }

        // 按 id 取回注册的 buff 定义（供展示 / 引用）；未注册返回 null
        public static BuffDef Get(string id)
        {
            BuffDef d;
            return (id != null && _defs.TryGetValue(id, out d)) ? d : null;
        }

        // 是否存在（参数 id：标识 id）
        public static bool Exists(string id)
        {
            return id != null && _defs.ContainsKey(id);
        }

        public static IEnumerable<BuffDef> All
        {
            get
            {
                for (int i = 0; i < _order.Count; i++)
                {
                    // 用 TryGetValue 而不是索引器：万一有别处动过表，也不会抛 KeyNotFound
                    BuffDef d;
                    if (_defs.TryGetValue(_order[i], out d) && d != null) yield return d;
                }
            }
        }

// 把表里所有定义写进 Db.Get().effects，之后各 buff 就能按 id 挂到小人身上
        public static void BuildAll()
        {
            if (Db.Get() == null || Db.Get().effects == null)
            {
                Debug.LogWarning("[MoreFood/Buff] Db.effects 不可用，Buff 表未构建");
                return;
            }
            foreach (var d in All)
            {
                // 逐个 try/catch：一个 buff 定义有问题不该拖垮其他 buff 甚至整个 Db 初始化
                try
                {
                    if (Db.Get().effects.Exists(d.id)) continue;
                    Db.Get().effects.Add(d.Build());
                    MFLog.Info(LogCat.Buff,"[MoreFood/Buff] 注册 Effect '" + d.id + "'："
                              + string.Join("，", System.Linq.Enumerable.Select(d.changes, c => c.ToString())));
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] 注册 Effect '" + d.id + "' 失败（已跳过）");
                }
            }
        }
    }

    // Buff 挂/摘的统一入口。
    public static class BuffManager
    {
        // 挂 buff。返回 false = 未注册 / 目标没有 Effects 组件 / Db 里还没有这个 Effect。
        public static bool Apply(GameObject target, string buffId, bool shouldSave = true)
        {
            if (target == null || string.IsNullOrEmpty(buffId)) return false;

            var def = BuffConfig.Get(buffId);
            if (def == null)
            {
                Debug.LogWarning("[MoreFood/Buff] 未注册的 buff: " + buffId);
                return false;
            }

            var effects = target.GetComponent<Effects>();
            if (effects == null) return false;

            bool already = effects.HasEffect(buffId);
            if (!already)
            {
                if (Db.Get() == null || Db.Get().effects == null || !Db.Get().effects.Exists(buffId))
                {
                    Debug.LogWarning("[MoreFood/Buff] Db 里还没有 Effect '" + buffId
                                     + "'（是不是漏了 BuffConfig.BuildAll()？）");
                    return false;
                }
                effects.Add(buffId, shouldSave);
            }

            // 数值改动（Add / AddPercent / …）：走上面 effects.Add 就生效，一定生效

            // 有 tick / 到期清理 / 特殊效果的 buff 才需要状态组件与 ticker
            if (def.onTick != null || def.onExpire != null || def.HasSpecial)
                BuffBehaviorState.Bind(target);
            if (def.onTick != null) target.AddOrGet<BuffTicker>();

            // 挂上时触发的特殊效果（每条独立判定、独立冷却）
            TriggerSpecials(target, buffId, SpecialTrigger.Apply);

            if (def.onApply != null)
            {
                try { def.onApply(target, def); }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] onApply 异常 " + buffId);
                }
            }
            return true;
        }

        // 触发某 buff 在指定时机下的全部特殊效果：冷却就绪 → 掷骰 → 实际生效 → 开始冷却
        public static void TriggerSpecials(GameObject target, string buffId, SpecialTrigger trigger)
        {
            if (target == null || string.IsNullOrEmpty(buffId)) return;

            var def = BuffConfig.Get(buffId);
            if (def == null || def.specials.Count == 0) return;

            float now = BuffBehaviorState.NowSeconds();
            for (int i = 0; i < def.specials.Count; i++)
            {
                BuffSpecial spec = def.specials[i];
                if (spec.trigger != trigger || spec.action == null) continue;

                string cdKey = buffId + "#" + spec.key;
                if (!BuffBehaviorState.SpecialReady(target, cdKey, now))
                {
                    MFLog.Verbose(LogCat.Buff, "[MoreFood/Buff] " + target.name + " 的「" + def.name
                                  + "·" + spec.key + "」冷却中，跳过");
                    continue;
                }
                if (spec.chance < 1f && UnityEngine.Random.value >= spec.chance) continue;

                bool done = false;
                try { done = spec.action(target); }
                catch (Exception e) { MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] 特殊效果异常 " + cdKey); }
                if (!done) continue;

                int cycles = BuffDef.EffectiveCooldownCycles(spec);
                BuffBehaviorState.MarkSpecial(target, cdKey, cycles, now);
                MFLog.Info(LogCat.Buff, "[MoreFood/Buff] " + target.name + " 触发「" + def.name
                           + "·" + spec.key + "」，进入 " + cycles + " 周期冷却");
            }
        }

// 摘掉目标身上的 buff（幂等；不存在则无事发生）
        public static void Remove(GameObject target, string buffId)
        {
            if (target == null || string.IsNullOrEmpty(buffId)) return;

            var effects = target.GetComponent<Effects>();
            if (effects == null || !effects.HasEffect(buffId)) return;

            effects.Remove(buffId);   // 会 Trigger(-1157678353, effect)
        }

        // 判断该目标身上当前是否挂着指定 buff 的 Effect
        public static bool Has(GameObject target, string buffId)
        {
            if (target == null || string.IsNullOrEmpty(buffId)) return false;
            var effects = target.GetComponent<Effects>();
            return effects != null && effects.HasEffect(buffId);
        }
    }

    // onTick 驱动。BuffManager.Apply 会按需自动挂到目标身上。
    public class BuffTicker : KMonoBehaviour, ISim1000ms
    {
        // Unity 回调：prefab 初始化
        protected override void OnPrefabInit()
        {
            try
            {
                base.OnPrefabInit();
                this.autoRegisterSimRender = false;   // 手动注册，避免被注册两次
            }
            catch (System.Exception e) { MFLog.HotFail("prefab:BuffTicker", e); }
        }

        // Unity 回调：实例生成
        protected override void OnSpawn()
        {
            try
            {
                base.OnSpawn();
                SimAndRenderScheduler.instance.Add(this, false);
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:BuffTicker", e); }
        }

        // Unity 回调：清理
        protected override void OnCleanUp()
        {
            try
            {
                SimAndRenderScheduler.instance.Remove(this);
                base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:BuffTicker", e); }
        }

        // 每秒驱动：身上仍挂着该 buff 的，逐个调 buff 定义的 onTick
        public void Sim1000ms(float dt)
        {
            try
            {
            var effects = this.GetComponent<Effects>();
            if (effects == null) return;

            foreach (var def in BuffConfig.All)
            {
                if (def.onTick == null) continue;
                if (!effects.HasEffect(def.id)) continue;
                try { def.onTick(this.gameObject, def, dt); }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] onTick 异常 " + def.id);
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("sim:BuffTicker", e); }
        }
    }

    // 特殊效果冷却状态 + buff 结束事件的中转
    public class BuffBehaviorState : KMonoBehaviour
    {
        // 冷却表：条目 = "<buffId>#<specialKey>|<到期游戏秒>"；冷却不随 buff 到期清除
        [Serialize] private List<string> _specialCd = new List<string>();
        // 是否已订阅「效果被移除」事件（运行时 AddOrGet 不走 OnSpawn，需要自己兜一次）
        private bool _bound;

        // Unity 回调：实例生成
        protected override void OnSpawn()
        {
            try
            {
                base.OnSpawn();
                BindEvent();
                ReArmAfterLoad();
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:BuffBehaviorState", e); }
        }

        // 订阅 buff 结束事件（幂等）
        private void BindEvent()
        {
            if (_bound) return;
            _bound = true;
            this.Subscribe(-1157678353, new Action<object>(this.OnEffectRemoved));
        }

        // 确保目标身上有状态组件且已订阅结束事件
        public static void Bind(GameObject go)
        {
            if (go == null) return;
            var s = go.AddOrGet<BuffBehaviorState>();
            if (s != null) s.BindEvent();
        }

        // 当前游戏秒（无 GameClock 时按 0）
        public static float NowSeconds()
        {
            return GameClock.Instance != null ? GameClock.Instance.GetTime() : 0f;
        }

        // 该特殊效果是否已过冷却（顺手清掉已过期的条目）
        public static bool SpecialReady(GameObject go, string cdKey, float now)
        {
            if (go == null || string.IsNullOrEmpty(cdKey)) return true;
            var s = go.GetComponent<BuffBehaviorState>();
            if (s == null || s._specialCd == null) return true;

            for (int i = s._specialCd.Count - 1; i >= 0; i--)
            {
                string entry = s._specialCd[i];
                if (entry == null) { s._specialCd.RemoveAt(i); continue; }

                int until = UntilOf(entry);
                if (KeyOf(entry) == cdKey)
                {
                    if (now < until) return false;
                    s._specialCd.RemoveAt(i);   // 已到期：删掉，留给 MarkSpecial 重新写入
                    return true;
                }
                if (now >= until) s._specialCd.RemoveAt(i);
            }
            return true;
        }

        // 成功触发后开始冷却
        public static void MarkSpecial(GameObject go, string cdKey, int cycles, float now)
        {
            if (go == null || string.IsNullOrEmpty(cdKey) || cycles <= 0) return;
            Bind(go);
            var s = go.GetComponent<BuffBehaviorState>();
            if (s == null) return;
            if (s._specialCd == null) s._specialCd = new List<string>();

            for (int i = s._specialCd.Count - 1; i >= 0; i--)
            {
                if (KeyOf(s._specialCd[i]) == cdKey) s._specialCd.RemoveAt(i);
            }
            s._specialCd.Add(cdKey + "|" + (int)(now + cycles * 600f));
        }

        // 拆冷却条目：键部分
        private static string KeyOf(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            int cut = entry.IndexOf('|');
            return cut > 0 ? entry.Substring(0, cut) : null;
        }

        // 拆冷却条目：到期秒部分
        private static int UntilOf(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return 0;
            int cut = entry.IndexOf('|');
            int v;
            if (cut < 0 || cut + 1 >= entry.Length) return 0;
            return int.TryParse(entry.Substring(cut + 1), out v) ? v : 0;
        }

        // 读档后自愈：清坏条目，给仍在身上的 tick 类 buff 补 ticker
        private void ReArmAfterLoad()
        {
            try
            {
                if (_specialCd == null) _specialCd = new List<string>();

                // 清掉坏条目与已过期的冷却，避免读档后列表只增不减
                float now = NowSeconds();
                for (int i = _specialCd.Count - 1; i >= 0; i--)
                {
                    string entry = _specialCd[i];
                    if (KeyOf(entry) == null || now >= UntilOf(entry)) _specialCd.RemoveAt(i);
                }

                var effects = this.GetComponent<Effects>();
                if (effects == null) return;

                foreach (var def in BuffConfig.All)
                {
                    if (def.onTick == null) continue;
                    if (!effects.HasEffect(def.id)) continue;
                    this.gameObject.AddOrGet<BuffTicker>();
                    break;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] 读档重建行为状态失败（已忽略）");
            }
        }

        // 任意效果被移除：若是本 Mod 注册的 buff，跑一次到期清理
        private void OnEffectRemoved(object data)
        {
            var effect = data as Effect;
            if (effect == null) return;

            string id = effect.Id;
            var def = BuffConfig.Get(id);
            if (def == null || def.onExpire == null) return;

            try { def.onExpire(this.gameObject, def); }
            catch (Exception e) { MFLog.Exc(LogCat.Buff, e, "[MoreFood/Buff] onExpire 异常 " + id); }
            MFLog.Verbose(LogCat.Buff, "[MoreFood/Buff] " + this.gameObject.name + " 的「"
                      + def.name + "」生命周期回调已执行（跟随 buff 结束）");
        }
    }
}
