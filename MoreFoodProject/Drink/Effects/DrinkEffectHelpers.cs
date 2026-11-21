using System;
using System.Collections.Generic;
using Database;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class DrinkEffectHelpers
    {
        // 宿醉 buff 的 ID（各饮品效果按它挂上/查询）
        public const string HangoverId = "DrinkHangover";
        // 咖啡豆士气 buff 的 ID（在 DrinkEffectBehaviorRegistry 注册为自建 buff）
        public const string BeanMoraleId = "DrinkBeanMorale";
        // 芒果饮品附加 buff 的 ID（同上，自建 buff）
        public const string MangoCarryId = "DrinkMangoCarry";

        // 耐热辅助 buff 的 ID（原版 RefreshingTouch，靠 OnTick 续期）
        public const string HeatImmunityEffect = "RefreshingTouch";
        // 耐寒+防湿足辅助 buff 的 ID（原版 WarmTouch）
        public const string ColdWetImmunityEffect = "WarmTouch";
        // 抑制过敏的辅助 buff 的 ID（原版 HistamineSuppression）
        public const string AllergySuppressionEffect = "HistamineSuppression";

        // effects
        public static bool HasEffect(GameObject go, string id)
        {
            if (go == null || string.IsNullOrEmpty(id)) return false;
            Effects fx = go.GetComponent<Effects>();
            return fx != null && fx.HasEffect(id);
        }

        // 确保目标身上挂着指定 Effect；返回 true = 这次真的加上了（已存在返回 false）
        public static bool EnsureEffect(GameObject go, string id)
        {
            if (go == null || string.IsNullOrEmpty(id)) return false;
            if (Db.Get() == null || Db.Get().effects == null || !Db.Get().effects.Exists(id)) return false;
            Effects fx = go.GetComponent<Effects>();
            if (fx == null || fx.HasEffect(id)) return false;
            fx.Add(id, true);
            return true;
        }

        // 概率判定：chance >= 1 恒真；返回是否命中
        public static bool Roll(float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return UnityEngine.Random.value < chance;
        }

        // traits
        public static Traits GetTraits(GameObject go)
        {
            return go != null ? go.GetComponent<Traits>() : null;
        }

        // 目标身上是否有指定特质（Trait）
        public static bool HasTrait(GameObject go, string traitId)
        {
            Traits traits = GetTraits(go);
            return traits != null && traits.HasTrait(traitId);
        }

        // 给目标加一个特质；已有时返回 false（幂等）
        public static bool AddTrait(GameObject go, string traitId)
        {
            Traits traits = GetTraits(go);
            if (traits == null || string.IsNullOrEmpty(traitId)) return false;
            if (traits.HasTrait(traitId)) return false;
            if (Db.Get() == null || Db.Get().traits == null) return false;
            Trait trait = Db.Get().traits.TryGet(traitId);
            if (trait == null) return false;
            traits.Add(trait);
            return true;
        }

        // 从目标身上移除指定特质
        public static bool RemoveTrait(GameObject go, string traitId)
        {
            Traits traits = GetTraits(go);
            if (traits == null || string.IsNullOrEmpty(traitId) || !traits.HasTrait(traitId)) return false;
            if (Db.Get() == null || Db.Get().traits == null) return false;
            Trait trait = Db.Get().traits.TryGet(traitId);
            if (trait == null) return false;
            traits.Remove(trait);
            return true;
        }

        // 临时借一个特质给目标（记在 buff 名下，buff 结束由 RemoveAddedTraits 收回）；返回 true = 这次真的加上了
        public static bool AddTemporaryTrait(GameObject go, string buffId, string traitId)
        {
            if (!AddTrait(go, traitId)) return false;
            DrinkEffectState state = go.AddOrGet<DrinkEffectState>();
            state.AddUnique(state.addedTraits, buffId, traitId);
            return true;
        }

        // 收回该 buff 临时借出的全部特质
        public static void RemoveAddedTraits(GameObject go, string buffId)
        {
            DrinkEffectState state = go.GetComponent<DrinkEffectState>();
            if (state == null) return;
            List<string> entries = state.TakeByPrefix(state.addedTraits, buffId);
            for (int i = 0; i < entries.Count; i++)
            {
                string traitId = DrinkEffectState.ValueOf(entries[i]);
                if (string.IsNullOrEmpty(traitId)) continue;
                RemoveTraitAndComponent(go, traitId);
            }
        }

        // 临时压制某特质（记录下来，buff 结束时还原）；返回 true = 这次真的压制了
        public static bool SuppressTrait(GameObject go, string buffId, string traitId)
        {
            if (!HasTrait(go, traitId)) return false;
            if (!RemoveTrait(go, traitId)) return false;
            DrinkEffectState state = go.AddOrGet<DrinkEffectState>();
            state.AddUnique(state.suppressedTraits, buffId, traitId);
            return true;
        }

        // 还原该 buff 压制过的全部特质
        public static void RestoreSuppressedTraits(GameObject go, string buffId)
        {
            DrinkEffectState state = go.GetComponent<DrinkEffectState>();
            if (state == null) return;
            List<string> entries = state.TakeByPrefix(state.suppressedTraits, buffId);
            for (int i = 0; i < entries.Count; i++)
            {
                string traitId = DrinkEffectState.ValueOf(entries[i]);
                if (string.IsNullOrEmpty(traitId)) continue;
                if (state.curedTraits != null && state.curedTraits.Contains(traitId)) continue;
                if (!HasTrait(go, traitId)) AddTrait(go, traitId);
            }
        }

        // 治愈某特质：从目标身上移除它；返回 true = 这次真的治愈了
        public static bool CureTrait(GameObject go, string buffId, string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return false;
            if (!RemoveTrait(go, traitId)) return false;

            DrinkEffectState state = go.AddOrGet<DrinkEffectState>();

            if (!state.curedTraits.Contains(traitId)) state.curedTraits.Add(traitId);

            // 同一 buff / 其它 buff 的抑制记录都撤掉，避免到期时又把它加回来
            if (state.suppressedTraits != null)
            {
                for (int i = state.suppressedTraits.Count - 1; i >= 0; i--)
                {
                    if (DrinkEffectState.ValueOf(state.suppressedTraits[i]) == traitId)
                        state.suppressedTraits.RemoveAt(i);
                }
            }
            return true;
        }

        // skills
        // 临时授予技能；返回 true = 这次真的授予了
        public static bool GrantTemporarySkill(GameObject go, string buffId, string skillId)
        {
            MinionResume resume = go != null ? go.GetComponent<MinionResume>() : null;
            if (resume == null || string.IsNullOrEmpty(skillId)) return false;
            if (resume.HasMasteredSkill(skillId) || resume.HasBeenGrantedSkill(skillId)) return false;

            resume.GrantSkill(skillId);
            DrinkEffectState state = go.AddOrGet<DrinkEffectState>();
            state.AddUnique(state.grantedSkills, buffId, skillId);
            return true;
        }

        // 收回该 buff 临时授予的技能
        public static void UngrantTemporarySkills(GameObject go, string buffId)
        {
            DrinkEffectState state = go.GetComponent<DrinkEffectState>();
            if (state == null) return;
            MinionResume resume = go.GetComponent<MinionResume>();
            List<string> entries = state.TakeByPrefix(state.grantedSkills, buffId);
            for (int i = 0; i < entries.Count; i++)
            {
                string skillId = DrinkEffectState.ValueOf(entries[i]);
                if (string.IsNullOrEmpty(skillId)) continue;
                if (resume != null && resume.HasBeenGrantedSkill(skillId))
                    resume.UngrantSkill(skillId);
            }
        }

        // one-time amounts
        public static void RestoreStamina(GameObject go, float fractionOfMax)
        {
            AmountInstance inst = LookupAmount(go, Db.Get() != null ? Db.Get().Amounts.Stamina : null);
            if (inst == null || fractionOfMax <= 0f) return;
            inst.ApplyDelta(inst.GetMax() * fractionOfMax);
        }

        // amount 单位是 kcal（界面值）；内部换算成原始卡路里。
        public static void AddCalories(GameObject go, float kcal)
        {
            AmountInstance inst = LookupAmount(go, Db.Get() != null ? Db.Get().Amounts.Calories : null);
            if (inst == null || kcal <= 0f) return;
            inst.ApplyDelta(kcal * 1000f);
        }

        // 给目标加生命值（按当前生命上限夹取）
        public static void AddHitPoints(GameObject go, float amount)
        {
            if (go == null || amount <= 0f) return;
            Health health = go.GetComponent<Health>();
            if (health != null && health.GetAmountInstance != null)
            {
                health.GetAmountInstance.ApplyDelta(amount);
                return;
            }
            AmountInstance inst = LookupAmount(go, Db.Get() != null ? Db.Get().Amounts.HitPoints : null);
            if (inst != null) inst.ApplyDelta(amount);
        }

        private static AmountInstance LookupAmount(GameObject go, Amount amount)
        {
            if (go == null || amount == null) return null;
            return amount.Lookup(go);
        }

        // joy
        public static void TriggerJoy(GameObject go)
        {
            JoyReaction.Trigger(go);
        }

        // 触发一次「轻曲哼唱者」表现（独奏乐器效果）
        public static void TriggerCatchyTune(GameObject go)
        {
            if (go == null) return;
            try { go.Trigger((int)GameHashes.CatchyTune, null); }
            catch (Exception e) { MFLog.Exc(LogCat.Buff, e, "[MoreFood/Drink][Effect] CatchyTune 触发失败"); }
        }

        // immunity
        public const string ThermalHeatId = "DrinkThermalImmunityHeat";
        // 本 Mod 自建的耐寒免疫 buff ID
        public const string ThermalColdId = "DrinkThermalImmunityCold";

        // 确保目标获得耐热免疫（辅助效果，靠 OnTick 续期）
        public static void EnsureHeatImmunity(GameObject go)
        {
            EnsureEffect(go, HeatImmunityEffect);
            EnsureEffect(go, ThermalHeatId);
        }

        // 确保目标获得耐寒 + 防湿足免疫（辅助效果，600s 续期）
        public static void EnsureColdWetImmunity(GameObject go)
        {
            EnsureEffect(go, ColdWetImmunityEffect);
            EnsureEffect(go, ThermalColdId);
        }

        // 确保目标获得抗过敏压制（持续抗敏药效果）
        public static void EnsureAllergySuppression(GameObject go)
        {
            EnsureEffect(go, AllergySuppressionEffect);
        }

        // 治愈过敏；返回 true = 这次真的治愈了一例
        public static bool CureAllergies(GameObject go)
        {
            if (go == null) return false;
            try
            {
                Klei.AI.Sicknesses sicknesses = go.GetSicknesses();
                if (sicknesses == null) return false;
                SicknessInstance inst = sicknesses.Get("Allergies");
                if (inst == null) return false;
                inst.Cure();
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Drink][Effect] 治愈过敏失败");
                return false;
            }
        }

        // learning
        public static float GetLearningMultiplier(GameObject go)
        {
            float m = 1f;
            if (HasEffect(go, "DrinkEffect_IcedCoffee")) m += 0.25f;
            if (HasEffect(go, "DrinkEffect_Espresso")) m += 0.50f;
            if (HasEffect(go, "DrinkEffect_RetroEspresso")) m += 0.55f;
            if (HasEffect(go, "DrinkEffect_CoffeeDoubleSkinJelly")) m += 0.45f;
            if (HasEffect(go, "DrinkEffect_Mocha")) m += 0.35f;
            return m;
        }

        // rest efficiency
        public static float GetRestPenalty(GameObject go)
        {
            float p = 0f;
            if (HasEffect(go, "DrinkEffect_IcedCoffee")) p += 0.90f;
            if (HasEffect(go, "DrinkEffect_Espresso")) p += 2.10f;
            if (HasEffect(go, "DrinkEffect_RetroEspresso")) p += 2.30f;
            if (HasEffect(go, "DrinkEffect_CoffeeDoubleSkinJelly")) p += 0.90f;
            if (HasEffect(go, "DrinkEffect_Mocha")) p += 1.30f;
            if (HasEffect(go, HangoverId)) p += 1.80f;
            return Mathf.Min(p, 5f);
        }

        // insight（顿悟）：随机一项可提升属性 +1；返回 true = 这次真的提升了
        public static bool TryInsight(GameObject go)
        {
            if (go == null) return false;

            AttributeLevels levels = go.GetComponent<AttributeLevels>();
            if (levels == null) return false;

            List<Klei.AI.Attribute> candidates = CollectInsightAttributes(go, levels);
            if (candidates.Count == 0) return false;

            Klei.AI.Attribute chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            AttributeLevel level = levels.GetAttributeLevel(chosen.Id);
            if (level == null || level.level >= levels.maxAttributeLevel) return false;

            levels.SetLevel(chosen.Id, level.level + 1);

            MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink][Effect] " + go.name + " 顿悟："
                + chosen.Id + " 等级 " + level.level + " → " + (level.level + 1));
            return true;
        }

        private static List<Klei.AI.Attribute> CollectInsightAttributes(GameObject go, AttributeLevels levels)
        {
            var result = new List<Klei.AI.Attribute>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            MinionResume resume = go != null ? go.GetComponent<MinionResume>() : null;
            if (resume != null && resume.AptitudeBySkillGroup != null)
            {
                foreach (KeyValuePair<HashedString, float> kv in resume.AptitudeBySkillGroup)
                {
                    if (kv.Value <= 0f) continue;
                    SkillGroup group = null;
                    try { group = Db.Get().SkillGroups.TryGet(kv.Key); }
                    catch { /* expected: 技能组查不到时跳过该项天赋，全查不到则退回遍历全部属性 */ }
                    if (group == null || group.relevantAttributes == null) continue;
                    for (int i = 0; i < group.relevantAttributes.Count; i++)
                        AddInsightCandidate(result, seen, levels, group.relevantAttributes[i]);
                }
            }

            if (result.Count == 0)
            {
                foreach (AttributeLevel level in levels)
                {
                    if (level == null || level.attribute == null) continue;
                    AddInsightCandidate(result, seen, levels, level.attribute.Attribute);
                }
            }
            return result;
        }

        private static void AddInsightCandidate(List<Klei.AI.Attribute> result, HashSet<string> seen,
                                                AttributeLevels levels, Klei.AI.Attribute attr)
        {
            if (attr == null || string.IsNullOrEmpty(attr.Id) || seen.Contains(attr.Id)) return;
            AttributeLevel level = levels.GetAttributeLevel(attr.Id);
            if (level == null || level.level >= levels.maxAttributeLevel) return;
            seen.Add(attr.Id);
            result.Add(attr);
        }

        // 读档自愈
        public static void SelfHeal(GameObject go)
        {
            if (go == null) return;
            DrinkEffectState state = go.GetComponent<DrinkEffectState>();
            if (state == null) return;
            Effects fx = go.GetComponent<Effects>();

            if (state.suppressedTraits != null)
            {
                for (int i = state.suppressedTraits.Count - 1; i >= 0; i--)
                {
                    string entry = state.suppressedTraits[i];
                    string buffId = DrinkEffectState.BuffIdOf(entry);
                    if (string.IsNullOrEmpty(buffId)) { state.suppressedTraits.RemoveAt(i); continue; }
                    if (fx != null && fx.HasEffect(buffId)) continue;

                    string traitId = DrinkEffectState.ValueOf(entry);
                    state.suppressedTraits.RemoveAt(i);
                    if (!string.IsNullOrEmpty(traitId)
                        && !state.curedTraits.Contains(traitId)
                        && !HasTrait(go, traitId))
                    {
                        AddTrait(go, traitId);
                    }
                }
            }

            if (state.addedTraits != null)
            {
                for (int i = state.addedTraits.Count - 1; i >= 0; i--)
                {
                    string entry = state.addedTraits[i];
                    string buffId = DrinkEffectState.BuffIdOf(entry);
                    if (string.IsNullOrEmpty(buffId)) { state.addedTraits.RemoveAt(i); continue; }
                    if (fx != null && fx.HasEffect(buffId)) continue;

                    string traitId = DrinkEffectState.ValueOf(entry);
                    state.addedTraits.RemoveAt(i);
                    if (!string.IsNullOrEmpty(traitId)) RemoveTraitAndComponent(go, traitId);
                }
            }

            if (state.grantedSkills != null)
            {
                MinionResume resume = go.GetComponent<MinionResume>();
                for (int i = state.grantedSkills.Count - 1; i >= 0; i--)
                {
                    string entry = state.grantedSkills[i];
                    string buffId = DrinkEffectState.BuffIdOf(entry);
                    if (string.IsNullOrEmpty(buffId)) { state.grantedSkills.RemoveAt(i); continue; }
                    if (fx != null && fx.HasEffect(buffId)) continue;

                    string skillId = DrinkEffectState.ValueOf(entry);
                    state.grantedSkills.RemoveAt(i);
                    if (!string.IsNullOrEmpty(skillId) && resume != null && resume.HasBeenGrantedSkill(skillId))
                        resume.UngrantSkill(skillId);
                }
            }
        }

        private static void RemoveTraitAndComponent(GameObject go, string traitId)
        {
            RemoveTrait(go, traitId);
            if (traitId == "RadiationEater")
            {
                RadiationEater eater = go.GetComponent<RadiationEater>();
                if (eater != null) UnityEngine.Object.Destroy(eater);
            }
        }

        // 豆类菜肴
        public static void ApplyBeanMorale(GameObject go)
        {
            if (go == null) return;
            Effects fx = go.GetComponent<Effects>();
            if (fx != null) fx.Add(BeanMoraleId, true);
        }
    }
}
