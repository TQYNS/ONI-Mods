using System;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    public class DrinkRestEfficiencyMonitor : KMonoBehaviour, ISim1000ms
    {
        // 施加的属性修正（缓引用）
        private AttributeModifier modifier;
        // 体力变化速率属性实例
        private AttributeInstance staminaDelta;
        // 是否已结算过恢复（防重复）
        private bool healed;

        // Unity 回调：prefab 初始化
        protected override void OnPrefabInit()
        {
            try
            {
            base.OnPrefabInit();
            this.autoRegisterSimRender = false;
            }
            catch (System.Exception e) { MFLog.HotFail("prefab:DrinkRestEfficiencyMonitor", e); }
        }

        // Unity 回调：实例生成
        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            SimAndRenderScheduler.instance.Add(this, false);
            UpdateModifier();
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:DrinkRestEfficiencyMonitor", e); }
        }

        // Unity 回调：清理
        protected override void OnCleanUp()
        {
            try
            {
            SimAndRenderScheduler.instance.Remove(this);
            RemoveModifier();
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:DrinkRestEfficiencyMonitor", e); }
        }

        // 每秒：按「休息效率」效果修正体力恢复速度（宿醉 -180% 等）
        public void Sim1000ms(float dt)
        {
            try
            {
            if (!healed)
            {
                healed = true;
                DrinkEffectHelpers.SelfHeal(this.gameObject);
            }
            UpdateModifier();
            }
            catch (System.Exception e) { MFLog.HotFail("sim:DrinkRestEfficiencyMonitor", e); }
        }

        private void UpdateModifier()
        {
            bool sleeping = DrinkEffectHelpers.HasEffect(this.gameObject, "Sleep")
                         || DrinkEffectHelpers.HasEffect(this.gameObject, "FloorSleep")
                         || DrinkEffectHelpers.HasEffect(this.gameObject, "NarcolepticSleep");
            float penalty = sleeping ? DrinkEffectHelpers.GetRestPenalty(this.gameObject) : 0f;

            if (penalty <= 0f)
            {
                RemoveModifier();
                return;
            }

            if (staminaDelta == null)
            {
                Attributes attributes = this.GetAttributes();
                if (attributes != null) staminaDelta = attributes.Get("StaminaDelta");
            }
            if (staminaDelta == null) return;

            if (modifier != null && Mathf.Approximately(modifier.Value, -penalty)) return;

            RemoveModifier();
            modifier = new AttributeModifier("StaminaDelta", -penalty, "饮品休息效率", true);
            staminaDelta.Add(modifier);
        }

        private void RemoveModifier()
        {
            if (modifier != null && staminaDelta != null) staminaDelta.Remove(modifier);
            modifier = null;
        }
    }

    [HarmonyPatch(typeof(MinionConfig), "CreatePrefab", new Type[0])]
    public static class DrinkRestEfficiency_MinionPrefab_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(GameObject __result)
        {
            try
            {
                if (__result != null) __result.AddOrGet<DrinkRestEfficiencyMonitor>();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkRestEfficiency_MinionPrefab_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(BionicMinionConfig), "CreatePrefab", new Type[0])]
    public static class DrinkRestEfficiency_BionicPrefab_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(GameObject __result)
        {
            try
            {
                if (__result != null) __result.AddOrGet<DrinkRestEfficiencyMonitor>();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkRestEfficiency_BionicPrefab_Patch", e); }
        }
    }
}
