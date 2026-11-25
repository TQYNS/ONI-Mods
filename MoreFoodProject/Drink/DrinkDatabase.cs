// DrinkDatabase.cs — 饮水系统 · 数据层（最小验证版 · 路线A）

using System;
using HarmonyLib;
using Klei;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    // 数据层主体
    public static class DrinkDatabase
    {
        // CreateAmount 会自动生成 HydrationMin / HydrationMax / HydrationDelta 三个属性
        public const string AMOUNT_ID    = "Hydration";
        // 饮水变化速率属性 id
        public const string DELTA_ATTR   = "HydrationDelta";
        // 饮水消耗效果 id
        public const string DRAIN_EFFECT = "HydrationDrain";

        public const float MAX_VALUE  = 100f;   // 上限
        public const float INIT_VALUE = 100f;   // 初始值
        public const float PER_CYCLE  = 50f;    // 每周期 -50
        public const float CYCLE_SECS = 600f;   // 原版 1 cycle = 600 秒

// 每秒衰减量 = 50 / 600（约 0.08333）
        public const float DRAIN_PER_SECOND = PER_CYCLE / CYCLE_SECS;

        // 注册好的 Amount 定义；未注册完成时为 null
        public static Amount Hydration { get; private set; }

        public static bool Ready { get { return Hydration != null; } }

        // 注册 Amount
        public static void RegisterAmount()
        {
            if (Db.Get() == null || Db.Get().Amounts == null)
            {
                Debug.LogWarning("[MoreFood/Drink] Db.Amounts 不可用，注册中止");
                return;
            }
            if (Db.Get().Amounts.Exists(AMOUNT_ID))
            {
                // Db 是跨存档共用的单例：已注册过就复用，不重复建
                Hydration = Db.Get().Amounts.Get(AMOUNT_ID);
                return;
            }

            // 参数：id, min, max, show_max, units, delta_threshold, show_in_ui,
            Hydration = Db.Get().Amounts.CreateAmount(
                AMOUNT_ID,            // id
                0f,                   // min      —— 下限
                MAX_VALUE,            // max      —— 上限
                false,                // show_max —— 不显示 "当前/上限"
                0,                    // units    —— 不套单位
                0.5f,                 // delta_threshold —— UI 趋势箭头阈值
                true,                 // show_in_ui
                "STRINGS.DUPLICANTS", // string_root（取不到就落占位符，下面手工覆盖）
                "ui_icon_liquid",     // uiSprite —— 原版 BionicOil 在用的、确认存在的图标
                null,                 // thoughtSprite
                null);                // uiFullColourSprite

            if (Hydration == null)
            {
                Debug.LogWarning("[MoreFood/Drink] CreateAmount 返回 null，Hydration 注册失败");
                return;
            }

            // CreateAmount 用 Strings.Get 取名，本项目拿不到字符串表 => 手工覆盖。
            Hydration.Name                = "饮水";
            Hydration.description         = "复制人的饮水需求，随时间下降，归零会脱水。";
            Hydration.minAttribute.Name   = "饮水下限";
            Hydration.maxAttribute.Name   = "饮水上限";
            Hydration.deltaAttribute.Name = "饮水变化率";

            // 显示格式：百分比 / 每周期。原版 Stamina / Stress 用的同一组参数。
            Hydration.SetDisplayer(new StandardAmountDisplayer(
                GameUtil.UnitClass.Percent,
                GameUtil.TimeSlice.PerCycle,
                null,
                GameUtil.IdentityDescriptorTense.Normal));

            MFLog.Info(LogCat.Drink, "[MoreFood/Drink] Hydration Amount 已注册");
        }

        // 注册衰减 Effect
        public static void RegisterDrainEffect()
        {
            if (Db.Get() == null || Db.Get().effects == null)
            {
                Debug.LogWarning("[MoreFood/Drink] Db.effects 不可用，Effect 注册中止");
                return;
            }
            if (Db.Get().effects.Exists(DRAIN_EFFECT)) return;

            // duration = 0        => 永不超时（不进 effectsThatExpire）
            var effect = new Effect(
                DRAIN_EFFECT,
                "水分消耗",
                "随时间自然流失水分（每周期 -" + PER_CYCLE + "）。",
                0f,     // duration
                true,   // show_in_ui
                false,  // trigger_floating_text
                false); // is_bad

            // 给 HydrationDelta 挂 -0.05；AmountInstance.BatchUpdate 会自动每 200ms 应用
            effect.Add(new AttributeModifier(
                DELTA_ATTR,
                -DRAIN_PER_SECOND,
                "随时间自然流失"));

            Db.Get().effects.Add(effect);
            MFLog.Info(LogCat.Drink, "[MoreFood/Drink] HydrationDrain Effect 已注册 (-" + DRAIN_PER_SECOND + "/s)");
        }

        // 判定：是不是仿生人（机器人）
        public static bool IsBionic(GameObject go)
        {
            if (go == null) return false;
            var identity = go.GetComponent<MinionIdentity>();
            return identity != null && identity.model == BionicMinionConfig.MODEL;
        }

        // 建 AmountInstance（必须在 OnPrefabInit 阶段调用）
        public static void AddHydrationAmount(GameObject duplicant)
        {
            if (duplicant == null || !Ready) return;
            if (IsBionic(duplicant)) return;   // 仿生人（机器人）不要饮水

            var amounts = duplicant.GetAmounts();
            if (amounts == null)
            {
                Debug.LogWarning("[MoreFood/Drink] " + duplicant.name + " 没有 Amounts 组件");
                return;
            }
            if (amounts.Has(Hydration)) return;   // 已存在（含读档恢复的情形）

            // ⚠ 不要改成 amounts.CreateInstance(Hydration) —— 基类是 stub，永远返回 null。见文件头【坑1】。
            var inst = new AmountInstance(Hydration, duplicant);
            inst.SetValue(INIT_VALUE);   // SetValue 会 Clamp 到 [min, max]
            amounts.Add(inst);           // Klei.AI.Amounts.Add 内部会调 inst.Activate()

            MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink] " + duplicant.name
                      + " 已挂 Hydration AmountInstance = " + inst.value);
        }

        // OnSpawn 阶段：Effect + 仿生人清理
        public static void AttachTo(GameObject duplicant)
        {
            if (duplicant == null) return;

            // 仿生人（机器人）不需要饮水：跳过 + 反向清理
            if (IsBionic(duplicant))
            {
                DetachFrom(duplicant);
                return;
            }

            if (!Ready)
            {
                Debug.LogWarning("[MoreFood/Drink] Hydration 尚未注册，跳过 " + duplicant.name);
                return;
            }

            // 兜底：正常路径已由 MinionModifiers.OnPrefabInit 建好，这里只在没建上时补
            AddHydrationAmount(duplicant);

            // 衰减 Effect
            var effects = duplicant.GetComponent<Effects>();
            if (effects == null)
            {
                Debug.LogWarning("[MoreFood/Drink] " + duplicant.name + " 没有 Effects 组件");
                return;
            }
            if (!effects.HasEffect(DRAIN_EFFECT))
            {
                effects.Add(DRAIN_EFFECT, true);   // should_save = true 才会进存档
            }
        }

        // 反向清理：给不该有饮水的实体（如仿生人）摘掉 AmountInstance 与 Effect
        public static void DetachFrom(GameObject duplicant)
        {
            if (duplicant == null) return;

            var effects = duplicant.GetComponent<Effects>();
            if (effects != null && effects.HasEffect(DRAIN_EFFECT))
            {
                effects.Remove(DRAIN_EFFECT);
                MFLog.Info(LogCat.Drink, "[MoreFood/Drink] " + duplicant.name + " 是仿生人，已移除 HydrationDrain");
            }

            if (!Ready) return;
            var amounts = duplicant.GetAmounts();
            if (amounts != null && amounts.Has(Hydration))
            {
                var inst = amounts.Get(Hydration);
                if (inst != null) amounts.Remove(inst);
            }
        }

        // 读取当前饮水值（调试用，-1 表示没有）
        public static float GetValue(GameObject duplicant)
        {
            if (!Ready || duplicant == null) return -1f;
            var inst = Hydration.Lookup(duplicant);
            return inst == null ? -1f : inst.value;
        }
    }

    // 补丁 1：Db 初始化后注册定义（必须是一级类，见文件头说明）
    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class DrinkDatabase_DbInitialize_Patch
    {
        public static void Postfix()
        {
            try
            {
                DrinkDatabase.RegisterAmount();
                DrinkDatabase.RegisterDrainEffect();
                // 饮品打标已挪进 Drinks.RegisterDefaults() 末尾（同一时机、不赌 Patch 顺序）
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkDatabase_DbInitialize_Patch", e); }
        }
    }

    // 补丁 2：主路径 —— 在 MinionModifiers.OnPrefabInit 建 AmountInstance
    [HarmonyPatch(typeof(MinionModifiers), "OnPrefabInit")]
    public static class DrinkDatabase_MinionModifiers_Patch
    {
        // 补丁：小人 MinionModifiers.OnPrefabInit 后挂 Hydration AmountInstance（必须早于反序列化，否则读档水量会被重置）
        public static void Postfix(MinionModifiers __instance)
        {
            try
            {
            if (__instance == null) return;
            DrinkDatabase.AddHydrationAmount(__instance.gameObject);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkDatabase", e); }
        }
    }

    // 补丁 3：OnSpawn —— 施加衰减 Effect + 仿生人（机器人）反向清理
    [HarmonyPatch(typeof(MinionIdentity), "OnSpawn")]
    public static class DrinkDatabase_MinionSpawn_Patch
    {
        // 补丁：小人 OnSpawn 后挂 HydrationDrain Effect（幂等）+ 仿生人接 DrinkDesireMonitor
        public static void Postfix(MinionIdentity __instance)
        {
            try
            {
            if (__instance == null) return;
            DrinkDatabase.AttachTo(__instance.gameObject);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkDatabase", e); }
        }
    }
}
