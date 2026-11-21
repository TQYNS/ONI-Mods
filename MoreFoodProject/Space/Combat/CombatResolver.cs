using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 伤害结算（§11.2/K9）

    internal struct DamageSource
    {
        public float damage;
        public float armorPenetration;
        public float armorDamageMult;   // 对装甲伤害倍率（ammo.armor_damage_mult，默认 1）
        public object attacker;
        public string sourceKind;
    }

    internal interface IDamageTarget
    {
        float CurrentArmor { get; set; }
        float CurrentHp { get; set; }
        float Defense { get; }
        float MaxArmor { get; }
        float MaxHp { get; }
        void OnArmorBroken(float overflow);
        void OnHpChanged(float delta);
    }

    internal static class CombatResolver
    {
        internal struct Result
        {
            public float hpDamage;
            public float armorLoss;
            public float overflow;
        }

        /// <summary>唯一伤害公式（§10.2 逐字）。</summary>
        internal static Result ApplyDamage(IDamageTarget target, DamageSource src)
        {
            Result r = new Result();
            if (target == null || src.damage <= 0f)
            {
                return r;
            }
            float p = src.armorPenetration;
            if (p <= 0f)
            {
                p = CombatDb.Shared.armorMinPenetration;
            }
            if (p <= 0f)
            {
                p = 0.001f;
            }
            float d = Mathf.Max(0f, target.Defense);
            float x = src.damage;
            float hpDmg;
            float armorDmg;
            if (p > d)
            {
                hpDmg = x * (p - d) / p;
                armorDmg = x - hpDmg;
            }
            else
            {
                hpDmg = 0f;
                armorDmg = x;
            }
            float mult = src.armorDamageMult > 0f ? src.armorDamageMult : 1f;
            armorDmg *= mult;

            float a = Mathf.Max(0f, target.CurrentArmor);
            float actualLoss = Mathf.Min(a, armorDmg);
            float overflow = armorDmg - actualLoss;
            target.CurrentArmor = a - actualLoss;
            if (target.CurrentArmor <= 0f && overflow > 0f && CombatDb.Shared.armorOverflowToHp)
            {
                hpDmg += overflow;
                target.OnArmorBroken(overflow);
            }
            if (hpDmg > 0f)
            {
                target.CurrentHp = Mathf.Max(0f, target.CurrentHp - hpDmg);
                target.OnHpChanged(-hpDmg);
            }
            r.hpDamage = hpDmg;
            r.armorLoss = actualLoss;
            r.overflow = overflow;
            return r;
        }

        /// <summary>K9：乘员同步——火箭 HP% 低于阈值时，把这次命中伤害的一部分分给舱内乘员，乘员血最多降到地板上。</summary>
        internal static void ApplyCrewSync(Clustercraft craft, RocketCombatState st, float hitDamage)
        {
            try
            {
                if (craft == null || st == null || hitDamage <= 0f || st.totalHpCache <= 0f)
                {
                    return;
                }
                float hpRatio = (st.totalHpCache - st.pendingDamage) / st.totalHpCache;
                if (hpRatio >= CombatDb.Shared.crewSyncHpPct)
                {
                    return;
                }
                WorldContainer w = RocketInventory.GetInterior(craft);
                if (w == null)
                {
                    return;
                }
                List<MinionIdentity> minions = Components.LiveMinionIdentities.GetWorldItems(w.id, false);
                if (minions == null || minions.Count == 0)
                {
                    return;
                }
                float each = hitDamage * CombatDb.Shared.crewDamageRatio / minions.Count;
                int hurt = 0;
                for (int i = 0; i < minions.Count; i++)
                {
                    MinionIdentity m = minions[i];
                    if (m == null)
                    {
                        continue;
                    }
                    Health h = m.GetComponent<Health>();
                    if (h == null)
                    {
                        continue;
                    }
                    float floor = h.maxHitPoints * CombatDb.Shared.crewFloorHpPct;
                    float allow = Mathf.Max(0f, h.hitPoints - floor);
                    float deal = Mathf.Min(each, allow);
                    if (deal > 0f)
                    {
                        h.Damage(deal);
                        hurt++;
                    }
                }
                if (hurt > 0)
                {
                    MFLog.Warn(LogCat.Space, "[Space] 火箭重伤（HP " + Mathf.Round(hpRatio * 100f) + "% < "
                        + Mathf.Round(CombatDb.Shared.crewSyncHpPct * 100f) + "%）：舱内 " + hurt
                        + " 名乘员各受约 " + Mathf.Round(each) + " 点伤害（血线地板 "
                        + Mathf.Round(CombatDb.Shared.crewFloorHpPct * 100f) + "%，不致死）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 乘员同步伤害失败");
            }
        }
    }

    // 两个实现（§10.3 / §10.4）

    internal sealed class RocketDamageTarget : IDamageTarget
    {
        private readonly RocketCombatState st;

        internal RocketDamageTarget(RocketCombatState state)
        {
            st = state;
        }

        public float CurrentArmor
        {
            get { return st.armor; }
            set { st.armor = Mathf.Max(0f, value); }
        }

        public float CurrentHp
        {
            get { return st.totalHpCache - st.pendingDamage; }
            set
            {
                // 火箭最低 HP = rocket_min_hp（漏洞28）：pendingDamage 不许把 HP 压到地板以下
                float floorHp = Mathf.Min(CombatDb.Shared.rocketMinHp, st.totalHpCache);
                float v = Mathf.Max(value, floorHp);
                st.pendingDamage = Mathf.Clamp(st.totalHpCache - v, 0f, st.totalHpCache);
            }
        }

        public float Defense
        {
            get { return st.defenseCache; }
        }

        public float MaxArmor
        {
            get { return st.maxArmor; }
        }

        public float MaxHp
        {
            get { return st.totalHpCache; }
        }

        public void OnArmorBroken(float overflow)
        {
        }

        public void OnHpChanged(float delta)
        {
        }
    }

    internal sealed class MonsterDamageTarget : IDamageTarget
    {
        private readonly MonsterRuntime rt;

        internal MonsterDamageTarget(MonsterRuntime runtime)
        {
            rt = runtime;
        }

        public float CurrentArmor
        {
            get { return rt.armor; }
            set
            {
                rt.armor = Mathf.Max(0f, value);
                if (rt.monster != null)
                {
                    rt.monster.armor = rt.armor;
                }
            }
        }

        public float CurrentHp
        {
            get { return rt.hp; }
            set
            {
                rt.hp = Mathf.Max(0f, value);
                if (rt.monster != null)
                {
                    rt.monster.hp = rt.hp;
                }
            }
        }

        public float Defense
        {
            get { return rt.def != null ? rt.def.defense : 0f; }
        }

        public float MaxArmor
        {
            get { return rt.def != null ? rt.def.armor : 0f; }
        }

        public float MaxHp
        {
            get { return rt.def != null ? rt.def.hp : 100f; }
        }

        public void OnArmorBroken(float overflow)
        {
        }

        public void OnHpChanged(float delta)
        {
        }
    }
}
