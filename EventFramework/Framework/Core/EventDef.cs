// EventDef.cs — 事件框架 · 【数据定义层】：YAML → 内存的映射结构

using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;

namespace EventFramework
{
    // 枚举
    public enum EventTriggerMode { scheduled, branch_only }
    //   scheduled   = 进抽取池（也可被父级调取 / 手动触发）

    // ★同 id 被"承诺"再次调取时的策略（见《事件框架规划.md》§17.4）；allow = 允许并存，直接新建一份
    public enum EventDuplicatePolicy { reject, allow }
    public enum EventNotifyLevel { info, warning, critical, hidden }

 // 事件极性：叙事者按它配权重（卡桑德拉会随周期提高负面权重）。
    public enum EventPolarity { positive, neutral, negative }

    // 子块（全部属性）
    public class EventTimeBlock
    {
        // 时长/门槛一律用【秒】（1 周期 = 600 秒）
        public float start { get; set; } = 600f;             // 开局 600 秒后才可能被抽到（原 1 周期）
    }

    public class EventLocationBlock
    {
        // 限定小行星（"any" = 不限）
        public string world_id { get; set; } = "any";
    }

    // ★ 统一的【预检条件块】：所有条件都在这里，不再有第二套语义。
    //   A 历史（框架自动记录） / C 游戏世界（O(1) 字典） / D 自定义函数 / E 并发互斥
    //   全部 AND；任一不满足 ⇒ 抽取阶段拦下（跳过本轮，不推通知）
    public class EventConditionBlock
    {
        // ══ A. 历史（事件/选项发生过没有、发生了几次）══
        public List<string> require_events { get; set; } = new List<string>();       // 全部 ≥1 次
        public List<string> require_any_events { get; set; } = new List<string>();   // 至少一个 ≥1 次
        public List<string> forbid_events { get; set; } = new List<string>();        // 均 0 次
        public List<ChoiceRef> require_choices { get; set; } = new List<ChoiceRef>();// 必须选过
        public List<ChoiceRef> forbid_choices { get; set; } = new List<ChoiceRef>(); // 必须没选过
        public List<EventCountCond> require_event_counts { get; set; } = new List<EventCountCond>(); // 次数阈值

        // ══ C. 游戏世界（原版增量字典，O(1)）══
        public List<BuildingCond> require_buildings { get; set; } = new List<BuildingCond>();  // 已建成数量
        public List<string> require_research { get; set; } = new List<string>();               // 已完成的科技

        // ══ D. 自定义函数（EventConditionRegistry 注册的键）══
        public string function { get; set; } = null;

        // ══ E. 并发互斥（运行态：对方正在跑就不能同时跑）══
        public List<string> mutex_running { get; set; } = new List<string>();        // 原 mutex
        public List<string> mutex_groups { get; set; } = new List<string>();         // 原 mutex_groups
    }

    // 事件触发次数阈值
    //   key: [e1]                       —— 单事件
    //   events: [a, b, c]               —— 多事件合计
    //   op: >= <= > < == !=             —— 比较符（空 = ==）
    public class EventCountCond
    {
        public string key { get; set; } = null;
        public List<string> events { get; set; } = null;
        public string op { get; set; } = ">=";
        public int value { get; set; } = 1;
    }

    // 建筑存在数量阈值（prefabId → BuildingInventory.BuildingCount）
    public class BuildingCond
    {
        public string id { get; set; } = null;
        public int min { get; set; } = 1;
    }

    public class EventRepeatBlock
    {
        // 只触发一次（触发后不再抽取）
        public bool one_shot { get; set; } = false;
        public float min_interval { get; set; } = 12000f;      // ★v2：单位【秒】（原 20 周期）
        public float max_interval { get; set; } = 24000f;      // 原 40 周期
    }

    public class EventDurationBlock
    {
        public float min { get; set; } = 0f;                 // ★v2：单位【秒】（原 1.0 周期）
        public float max { get; set; } = 0f;                  // 默认一次性：不配置 duration 即无持续期
    }

    // 通知/状态行的【分支变体】：同一事件按运行分支选用不同的正文模板（可只覆盖其中一项）
    public class EventNotifyVariant
    {
        // 通知正文变体；不写 ⇒ 回落 notification.tooltip
        public string tooltip { get; set; } = null;
        // 状态行正文变体；不写 ⇒ 回落 notification.status_tooltip
        public string status_tooltip { get; set; } = null;
    }

    public class EventNotificationBlock
    {
        // 自定义常驻状态文本
        public string status_custom { get; set; } = null;
        // 通知等级（决定图标与配色）
        public EventNotifyLevel level { get; set; } = EventNotifyLevel.info;
        public string tier { get; set; } = null;               // ★v3.2 通知外观档位（purple/yellow/blue/red/alert）；不写 ⇒ 按 level 推断
        // 根级通知正文（支持富文本 / [id] 贴图 / {占位符}）；不写 ⇒ 回落 description
        public string tooltip { get; set; } = null;
        public string status_tier { get; set; } = null;        // ★v3.2 常驻状态行的档位；不写 ⇒ 跟 tier 同档

 // 推送开关（统一语义：true = 推）
        public bool notify { get; set; } = true;

        // 常驻状态推不推（不写 = false —— 默认不推，要显式配 true）
        public bool status { get; set; } = false;

        // 状态行文本（可选）：支持富文本 + [id] + {name}；不写 ⇒ 用档位模板（事件名）
        public string status_text { get; set; } = null;
        // 常驻状态行正文；不写 ⇒ 用 status_text
        public string status_tooltip { get; set; } = null;

        // ★ 分支变体表（键名自定，无数量上限）：运行时由 EventInstance.notifyVariant 选择。
        //   未命中 ⇒ 回落上面的 tooltip / status_tooltip（不报错）。
        public Dictionary<string, EventNotifyVariant> variants { get; set; } = null;

 // 排序位置：**每条通知/状态单独控**（不写 ⇒ 用档位的，再没有 ⇒ 用默认）
        public NotificationType? sort { get; set; } = null;              // 事件通知
        public NotificationType? status_sort { get; set; } = null;       // 常驻状态
        // 弹窗时暂停游戏
        public bool pause_game { get; set; } = false;

        // 是否允许"超时未选择 → 强制弹窗"。
        public bool show_panel { get; set; } = true;

        // 驻留到期是否强制弹窗
        public bool resident_popup { get; set; } = false;

        // ★ 只通知不弹窗：为真且该事件【没有任何选项】时，点击事件通知不弹窗（极简事件，如气候变换）。默认 false。
        public bool notify_only { get; set; } = false;
    }

    public class EventMediaOnTrigger
    {
        public string sound { get; set; } = null;              // 音频模板键
        // 音乐模板键
        public string music { get; set; } = null;
        // 语音模板键
        public string voice { get; set; } = null;
        public string script { get; set; } = null;             // 音频脚本键（静态槽位播完后调）
    }

    public class EventMediaOnEnd
    {
        // 音频模板键
        public string sound { get; set; } = null;
        // 音乐模板键
        public string music { get; set; } = null;
        // 音频脚本键
        public string script { get; set; } = null;
    }

    public class EventMediaOnChoice
    {
        // 音频模板键
        public string sound { get; set; } = null;
        // 音乐模板键
        public string music { get; set; } = null;
        // 音频脚本键
        public string script { get; set; } = null;
    }

    public class EventMediaBlock
    {
        public EventMediaOnTrigger on_trigger { get; set; } = new EventMediaOnTrigger();
        public EventMediaOnChoice on_choice { get; set; } = new EventMediaOnChoice();
        public EventMediaOnEnd on_end { get; set; } = new EventMediaOnEnd();
    }

    public class EventAiBlock
    {
        // 是否启用 AI 叙事块（唯一被读取的字段；其余生成参数未实现，已移除）
        public bool enabled { get; set; } = false;
    }

    public class EventDebugBlock
    {
        // 是否打详细日志
        public bool verbose_log { get; set; } = false;

        // ★ 验证用：> 0 时，调度器不再按 cycle_min/max 计算，
        public float test_interval_seconds { get; set; } = 0f;
    }

    // 选项 / 链式（列表元素）
    public class EventOption
    {
        // ---- 已有 ----
        public string id { get; set; } = null;                 // 选项身份（日志/调试用）
        public string label { get; set; } = null;              // LocString key 或直接写文本
        // 悬浮提示文本
        public string tooltip { get; set; } = null;
        public string action { get; set; } = null;             // 代码端函数键（空 = 点了只关窗口，即"退出"）
        // ---- 选项级通知（选项被触发那一刻推的一条通知）----
        public string notice_name { get; set; } = null;        // 标题；不写 ⇒ 不推选项级通知
        public string notice_tooltip { get; set; } = null;     // 正文；支持富文本 / [id] / 占位符
        public string notice_tier { get; set; } = null;        // 外观档位

 // ---- 决策层字段（见《事件框架规划.md》§16.4 / §16.7 / §16.8）----
        public float weight { get; set; } = -1f;               // 兜底随机权重；-1 = 未配（视为 1）；<=0 = 不参与兜底
        public bool is_default { get; set; } = false;          // 默认项：没表态时优先走它（允许多配）
        public float duration { get; set; } = -1f;             // 秒：选了它之后【决策效果】持续多久；<=0 = 无决策效果
        public string start_fn { get; set; } = null;           // 决策效果开始钩子（函数键）
        public string end_fn { get; set; } = null;             // 决策效果结束钩子（到点自动调）
        public List<object> next_event { get; set; } = null;   // 分支列表：元素为 "id" 或 [id] 或 [id, 权重]
        public float next_delay_min { get; set; } = 0f;        // 秒：分支最早触发（从表态时刻起算）
        // 秒：分支最晚触发（从表态时刻起算）
        public float next_delay_max { get; set; } = 0f;
        public bool next_allow_repeat { get; set; } = false;   // ★v3.2：允许回到本链已访问节点

 // 新增：提交材料 + 奖励下发 + 警告开关
//   语义见《提交奖励施工文档》§5 / §7

        // 提交材料：[[id, kg], [id, kg], ...]
        public List<object> submit_materials { get; set; } = null;

        // 奖励下发：[[id, kg], [id, kg], ...]
        public List<object> grant_rewards { get; set; } = null;

 // 信息栏文案覆盖：写了就用它，不写走"物品名 ×量"自动拼接。
        public string submit_label { get; set; } = null;
        // 奖励栏文案覆盖（不写则按"物品名 ×量"自动拼）
        public string grant_label { get; set; } = null;
        // 奖励栏自定义图标；与 grant_label 可用于纯展示，不触发实际发奖
        public string grant_icon { get; set; } = null;

 // 该选项的"决策效果"状态行（与根级同一套语义：true = 推）
        public bool status { get; set; } = false;
        // 状态行文本覆盖
        public string status_text { get; set; } = null;
        // 常驻状态行正文；不写 ⇒ 用 status_text
        public string status_tooltip { get; set; } = null;

 // 警告开关：
        public bool Warning { get; set; } = true;

 // 提交材料的预编译结果（启动时由 EventHistoryScanner 填）。
        [System.NonSerialized] public ItemParseResult submit_compiled = null;

 // 奖励的预编译结果（启动时由 EventHistoryScanner 填）。
        [System.NonSerialized] public ItemParseResult reward_compiled = null;

        // 本选项是否配了提交 / 奖励（用于 ApplyChoice 里快速跳过无效果的选项）
        public bool HasItemEffect
        {
            get
            {
                return (submit_materials != null && submit_materials.Count > 0)
                    || (grant_rewards != null && grant_rewards.Count > 0);
            }
        }

        // 选项级：条件（决定"该选项是否可用 / 是否出现"），与根级共用同一结构
        public EventConditionBlock condition { get; set; } = new EventConditionBlock();

        // 预编译条件（启动时填）
        public EventCondition compiled = null;
    }

    // 解析后的分支（next_event 的元素）
    public class EventBranch
    {
        // 下一个事件 id
        public string next = null;
        // 抽取权重
        public float weight = 1f;
        public string owner = null;    // 出错时打日志用（"MeteorShower" / "MeteorShower.options[1]"）
    }

    // EventDef —— 模板本体（YAML → 内存，不存盘）
    public class EventDef
    {
        // ---- 标识 ----
        public string id { get; set; } = null;                                  // 事件唯一 id（YAML 的 id）
        public string name { get; set; } = null;               // LocString key 或直接文本
        public string description { get; set; } = null;                         // 面板上显示的说明文字
        public string ai_summary { get; set; } = null;                          // 给 AI 桥接 / 调试用的一句话摘要
        // ★ 文案变体：同一条事件多组「名字 + 正文」，实例创建时等概率抽一组并冻结
        //   与根级 name/description 并存时以 variants 为准（根级仅作兜底）
        public List<EventVariantDef> variants { get; set; } = null;
        // ★ 狩猎规格：由宿主（内容 Mod）读取，框架只做读取与校验，不解释玩法
        public HuntSpec hunt { get; set; } = null;
        public string handler { get; set; } = null;                             // 代码端处理器键（EventHandlerRegistry 注册）
        public string pool { get; set; } = null;                                // ★ 事件池类型（YAML: pool:），未写 = normal
        public int version { get; set; } = 1;                                   // 事件模板版本号

        public EventTriggerMode trigger_mode { get; set; } = EventTriggerMode.scheduled;   // 触发方式（默认 scheduled = 定时抽取）

 // 事件极性（正面/中立/负面）—— 叙事者的抽取权重按它分档
        public EventPolarity polarity { get; set; } = EventPolarity.neutral;    // 事件极性（决定叙事者抽取权重分档）

        // ---- 各域 ----
        public EventTimeBlock time { get; set; } = new EventTimeBlock();
        public EventLocationBlock location { get; set; } = new EventLocationBlock();
        public EventConditionBlock condition { get; set; } = new EventConditionBlock();
        public EventRepeatBlock repeat { get; set; } = new EventRepeatBlock();
        public EventDurationBlock duration { get; set; } = new EventDurationBlock();

        // ---- ★ 驻留时间（秒）：事件等玩家做选择等多久。与 duration 完全解耦 ----
        public float resident_time { get; set; } = -1f;                         // 驻留秒数；<= 0 ⇒ 用默认 0.5 周期

 // ---- 根级：事件本体的开始/结束钩子 + 默认走向（见 §16.5 / §16.8）----
        public string start_fn { get; set; } = null;           // 本体效果开始钩子（函数键）
        public string end_fn { get; set; } = null;             // 本体效果结束钩子（duration 到点自动调）
        public List<object> next_event { get; set; } = null;   // 默认走向：没人表态时接的下一个事件（分支列表）
        public float next_delay_min { get; set; } = 0f;        // 秒
        public float next_delay_max { get; set; } = 0f;                         // 默认走向的下一次延迟上限（秒）

        // 同 id 被承诺再次调取时的策略（默认 reject = 丢弃）
        public EventDuplicatePolicy on_duplicate { get; set; } = EventDuplicatePolicy.reject;   // 同 id 再次承诺时的策略（默认 reject = 丢弃，不续期）

        // 是否允许弹出面板（交互 / 回顾）。通知点击与历史点击两条路径共用此口径，避免行为不一致。
        //   无通知块 ⇒ 允许；
        //   明确的“无弹窗”= show_panel=false 或 notify_only（且没有可交互选项）⇒ 不允许。
        public bool AllowsPanel()
        {
            if (notification == null) return true;
            bool hasOptions = options != null && options.Count > 0;
            if (!hasOptions && (notification.notify_only || !notification.show_panel)) return false;
            return true;
        }

        // 灾星（LargeImpactor）撞击倒计时（周期）；不配 ⇒ 80~200 周期
        public float impactor_delay_min_cycles { get; set; } = 80f;
        public float impactor_delay_max_cycles { get; set; } = 200f;

        // 取一次倒计时时长（秒）：区间内随机，1 周期 = 600 秒
        public float RollImpactorDelaySeconds()
        {
            float lo = UnityEngine.Mathf.Min(impactor_delay_min_cycles, impactor_delay_max_cycles);
            float hi = UnityEngine.Mathf.Max(impactor_delay_min_cycles, impactor_delay_max_cycles);
            if (hi <= 0f) return 0f;
            if (lo <= 0f) lo = hi;
            return UnityEngine.Random.Range(lo, hi) * 600f;
        }

        // 本事件的 next_event 分支是否允许回到本链已访问过的节点（表达合法循环）
        public bool next_allow_repeat { get; set; } = false;                    // next_event 分支是否允许回到已访问节点（表达合法循环）
        public EventNotificationBlock notification { get; set; } = new EventNotificationBlock();
        public EventMediaBlock media { get; set; } = new EventMediaBlock();
        public EventAiBlock ai { get; set; } = new EventAiBlock();
        public EventDebugBlock debug { get; set; } = new EventDebugBlock();

 // ：面板渲染选项时是否检查"该选项的分支目标当前可触发"（默认开）
        public bool check_branch_availability { get; set; } = true;             // 渲染选项时是否检查分支目标当前可触发

        // 允许同 id 事件与自身并发（默认 false）。
        public bool allow_self_concurrent { get; set; } = false;                // 允许同 id 事件与自身并发

        // ---- 列表 ----
        // （并发互斥已迁到 condition.mutex_running / condition.mutex_groups）
        public List<EventOption> options { get; set; } = new List<EventOption>();

        // ---- 优先级 ----
        public int priority { get; set; } = 100;                                // 优先级（数值越大越先弹）

        // ---- 关键节点事件：进「历史」页（策划在 yaml 里手标 key_event: true）----
        public bool key_event { get; set; } = false;                            // 关键节点事件（true ⇒ 进「历史」页）

        // ---- 其它 ----
        // 辅助（非 YAML 字段，运行时填充；字段形式即可，YamlDotNet 不碰它）
        public string sourceFile = null;                                        // 该事件来自哪个 yaml 文件（日志用）

 // 覆盖（补丁）+ 身份（fullId）
        public string mode { get; set; } = null;                                // 覆盖模式（补丁用），配合 mode_target
        public string mode_target { get; set; } = null;                         // 覆盖目标：被替换掉的事件 id（补丁用）

        [System.NonSerialized] public string fullId = null;
        [System.NonSerialized] public string sourceMod = null;
        [System.NonSerialized] public string sourceRawText = null;
        [System.NonSerialized] public string lastPatchedBy = null;
        [System.NonSerialized] public string lastPatchedFrom = null;

 // 预编译条件（启动时填；不是 yaml 字段 ⇒ 用字段形式，YamlDotNet 不会碰它）
        public EventCondition compiled = null;                                  // 预编译条件（启动时填，运行期判定用）

        // 解析 next_event 列表（根级 / 选项级共用）。
        public static List<EventBranch> ParseBranches(List<object> raw, string owner)
        {
            return ParseBranches(raw, owner, false);
        }

        // quiet = true 时不打警告（运行时的高频校验用；启动校验仍用带日志的版本）
        public static List<EventBranch> ParseBranches(List<object> raw, string owner, bool quiet)
        {
            var result = new List<EventBranch>();
            if (raw == null) return result;

            for (int i = 0; i < raw.Count; i++)
            {
                object item = raw[i];

                // 形式一：裸 id 字符串
                string id = item as string;
                if (id != null)
                {
                    AddBranch(result, id, 1f, owner, i, quiet);
                    continue;
                }

                // 形式二：[id] 或 [id, 权重]
                var sub = item as System.Collections.IList;
                if (sub != null)
                {
                    if (sub.Count == 0) { Warn(owner, i, "空元素", quiet); continue; }
                    string sid = sub[0] as string;
                    if (string.IsNullOrEmpty(sid)) { Warn(owner, i, "第一个元素不是事件 id", quiet); continue; }

                    float w = 1f;
                    if (sub.Count >= 2 && sub[1] != null)
                    {
                        if (!float.TryParse(sub[1].ToString(), out w))
                        { Warn(owner, i, "权重解析失败", quiet); continue; }
                        if (w <= 0f) { Warn(owner, i, "权重 <= 0", quiet); continue; }
                    }
                    AddBranch(result, sid, w, owner, i, quiet);
                    continue;
                }

                // 其它（数字 / null / 映射）
                Warn(owner, i, "既不是事件 id，也不是 [id, 权重]；不支持 [id, 数字] 这种顶层写法（请写成 [[id, 数字]]）", quiet);
            }
            return result;
        }

        private static void AddBranch(List<EventBranch> into, string id, float weight, string owner, int idx, bool quiet)
        {
            if (string.IsNullOrEmpty(id)) { Warn(owner, idx, "事件 id 为空", quiet); return; }
            if (!EventRegistry.Has(id)) { Warn(owner, idx, "事件 '" + id + "' 不存在（未加载）", quiet); return; }

            var b = new EventBranch { next = id, weight = weight, owner = owner };
            into.Add(b);
        }

        private static void Warn(string owner, int idx, string why, bool quiet)
        {
            if (quiet) return;
            Debug.LogWarning("[MoreFood/Event] next_event 第 " + idx + " 项无效（" + owner + "）：" + why);
        }

        // 未配 resident_time 时的默认驻留：0.5 周期 = 300 游戏秒（1 周期 = 600 秒）
        public const float RESIDENT_DEFAULT_SECONDS = 300f;                     // 驻留时间默认值（0.5 周期 = 300 秒）

        // 本事件的驻留秒数（未配则取默认 0.5 周期）
        public float GetResidentSeconds()
        {
            return resident_time > 0f ? resident_time : RESIDENT_DEFAULT_SECONDS;
        }

        // 是否有任何选项配了兜底信息（is_default 或显式 weight）
        public bool HasFallbackInfo
        {
            get
            {
                if (options == null) return false;
                for (int i = 0; i < options.Count; i++)
                {
                    var o = options[i];
                    if (o == null) continue;
                    if (o.is_default || o.weight >= 0f) return true;
                }
                return false;
            }
        }

        // 取显示名（LocString key → 实际文本；非 key 则原样返回）
        public string GetNameText()
        {
            return ResolveText(name, id);
        }

        // 等概率抽一组可用变体；没有可用变体返回 null
        public EventVariantDef PickVariant()
        {
            if (variants == null || variants.Count == 0) return null;
            int count = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                EventVariantDef v = variants[i];
                if (v != null && !string.IsNullOrEmpty(v.name) && !string.IsNullOrEmpty(v.description)) count++;
            }
            if (count == 0) return null;
            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < variants.Count; i++)
            {
                EventVariantDef v = variants[i];
                if (v == null || string.IsNullOrEmpty(v.name) || string.IsNullOrEmpty(v.description)) continue;
                if (pick-- == 0) return v;
            }
            return null;
        }

        public string GetDescText()
        {
            return ResolveText(description, "");
        }

        // 宽容解析：只对"看起来像字符串表 key"的值走 LocString，
        public static string ResolveText(string raw, string fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            if (!LooksLikeKey(raw)) return raw;

            try
            {
                string s = new LocString(raw).text;
                if (!string.IsNullOrEmpty(s) && s != raw) return s;
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("def:text", "[Def] 词条解析异常 → 用原始文本", e);
            }
            return fallback;
        }

        private static bool LooksLikeKey(string s)
        {
            return s.StartsWith("STRINGS.") || s.StartsWith("MISSING.");
        }
    }
}
