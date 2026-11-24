using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    // 火箭战斗状态（§8.1 / §11）

    [SerializationConfig(MemberSerialization.OptIn)]   // ★ KSerialization OptOut 只认 public 字段，必须 OptIn
    internal sealed class RocketCombatState : KMonoBehaviour
    {
        [Serialize]
        internal bool inCombat;
        [Serialize]
        internal float lastDamageAt;
        [Serialize]
        internal float lastAttackAt;
        [Serialize]
        internal float disengageTimer;          // 绝对时刻（now + disengage_seconds）
        [Serialize]
        internal float armor;
        [Serialize]
        internal float maxArmor;
        [Serialize]
        internal float totalHpCache;
        [Serialize]
        internal float pendingDamage;
        [Serialize]
        internal int orderedTargetUid;          // 攻击指令目标（怪物 uid，0 = 无）
        [Serialize]
        internal float emergencyCooldownUntil;
        [Serialize]
        internal int emergencyUsed;
        [Serialize]
        internal float warpCooldownUntil;

        // 运行期缓存（不入档）
        internal float defenseCache;
        internal float speedMulCache = 1f;
        internal float hpSpeedFactorCache = 1f;   // ★ 性能：速度血量系数缓存（HP 变化时更新，速度回调只读，禁每帧枚举模块）
        internal float armorPenAddCache;
        internal float armorDamageMulCache = 1f;
        internal int scoutingBonusCache;
        internal int radarRangeBonusCache;
        internal float hpRegenPctCache;
        internal float shieldRegenPctCache;
        private Clustercraft craft;
        private string lastStatus = "";
        private bool statSynced;
        private const float RegenPeriodSeconds = 60f;
        private float regenAccum;

        internal float Hp
        {
            get { return Mathf.Max(0f, totalHpCache - pendingDamage); }
        }

        internal Clustercraft Craft
        {
            get { return craft; }
        }

        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            SpaceLoadTrace.Set("RocketCombatState.OnSpawn 开始");
            craft = GetComponent<Clustercraft>();
            statSynced = false;
            if (CombatDb.built)
            {
                Recalc();
                statSynced = true;
            }
            if (craft != null)
            {
                craft.Subscribe((int)GameHashes.RocketModuleChanged, OnModuleChanged);
            }
            SpaceLoadTrace.Set("RocketCombatState.OnSpawn 完成");
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:RocketCombatState", e); }
        }

        /// <summary>Db 就绪后的延迟重算（读档时序兜底：craft 的 OnSpawn 可能早于 CombatDb.Build）。</summary>
        internal void SyncIfNeeded()
        {
            if (!statSynced && CombatDb.built)
            {
                Recalc();
                statSynced = true;
            }
        }

        protected override void OnCleanUp()
        {
            try
            {
            try
            {
                CombatBars.RemoveCraft(craft);
                if (craft != null)
                {
                    craft.Unsubscribe((int)GameHashes.RocketModuleChanged, OnModuleChanged);
                }
            }
            catch (Exception)
            {
                // expected: 销毁时移除血条/退订失败可忽略，craft 已拆、引用由 RemoveCraft 汇总裁决
            }
            base.OnCleanUp();
            }
            catch (System.Exception e) { MFLog.HotFail("cleanup:RocketCombatState", e); }
        }

        private void OnModuleChanged(object data)
        {
            try
            {
                Recalc();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 火箭模块变更后重算失败");
            }
        }

        /// <summary>重算 HP / 装甲 / 防御 / 速度 / 穿甲 / 雷达 / 再生（纯模块数值，不含等级）。</summary>
        internal void Recalc()
        {
            try
            {
                if (craft == null || !CombatDb.built)
                {
                    return;
                }
                float hpSum = 0f;
                List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
                for (int i = 0; i < mods.Count; i++)
                {
                    BuildingHP hp = mods[i].module != null ? mods[i].module.GetComponent<BuildingHP>() : null;
                    if (hp != null)
                    {
                        hpSum += hp.MaxHitPoints;
                    }
                }
                if (hpSum <= 0f)
                {
                    hpSum = 100f;
                }
                float baseArmor, baseDefense;
                BaseStats(out baseArmor, out baseDefense);
                float penAdd, armorMul, speedMul, hpRegen, shieldRegen;
                int radarRange, scouting;
                RocketLoadout loadout = RocketLoadout.Get(craft);
                if (loadout != null)
                {
                    loadout.SumStats(out penAdd, out armorMul, out speedMul,
                        out radarRange, out scouting, out hpRegen, out shieldRegen);
                }
                else
                {
                    penAdd = 0f;
                    armorMul = 1f;
                    speedMul = 1f;
                    radarRange = 0;
                    scouting = 0;
                    hpRegen = 0f;
                    shieldRegen = 0f;
                }
                totalHpCache = hpSum;
                maxArmor = Mathf.Max(0f, baseArmor);
                defenseCache = Mathf.Max(0f, baseDefense);
                speedMulCache = Mathf.Max(0.1f, speedMul);
                armorPenAddCache = penAdd;
                armorDamageMulCache = Mathf.Max(0.01f, armorMul);
                radarRangeBonusCache = radarRange;
                scoutingBonusCache = scouting;
                hpRegenPctCache = Mathf.Max(0f, hpRegen);
                shieldRegenPctCache = Mathf.Max(0f, shieldRegen);
                if (armor > maxArmor)
                {
                    armor = maxArmor;
                }
                if (pendingDamage > totalHpCache)
                {
                    pendingDamage = totalHpCache;
                }
                RefreshSpeedFactor();   // ★ 缓存速度血量系数（模块可能变了）
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 火箭战斗状态重算失败");
            }
        }

        /// <summary>★ 性能：重算速度血量系数缓存。仅在 HP 变化时调用（受击/落地结算/回血/模块变更），
        internal void RefreshSpeedFactor()
        {
            try
            {
                float curHpSum = 0f;
                float maxHpSum = 0f;
                List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
                for (int i = 0; i < mods.Count; i++)
                {
                    BuildingHP hp = mods[i].module != null ? mods[i].module.GetComponent<BuildingHP>() : null;
                    if (hp != null)
                    {
                        curHpSum += hp.HitPoints;
                        maxHpSum += hp.MaxHitPoints;
                    }
                }
                if (maxHpSum <= 0f)
                {
                    hpSpeedFactorCache = 1f;
                    return;
                }
                // 飞行中 pendingDamage 未结算 → 也要计入；落地后 HitPoints 已扣、pendingDamage=0
                float ratio = Mathf.Clamp01((curHpSum - pendingDamage) / maxHpSum);
                hpSpeedFactorCache = Mathf.Lerp(0.6f, 1f, ratio);
            }
            catch (Exception)
            {
                hpSpeedFactorCache = 1f;
            }
        }

        private void BaseStats(out float armor, out float defense)
        {
            armor = 0f;
            defense = 0f;
            KPrefabID pid = craft != null ? craft.GetComponent<KPrefabID>() : null;
            for (int i = 0; i < CombatDb.Rockets.Count; i++)
            {
                RocketDef rd = CombatDb.Rockets[i];
                if (rd.matchTags == null || rd.matchTags.Length == 0)
                {
                    continue;
                }
                for (int t = 0; t < rd.matchTags.Length; t++)
                {
                    if (pid != null && pid.HasTag(new Tag(rd.matchTags[t])))
                    {
                        armor = rd.armor;
                        defense = rd.defense;
                        break;
                    }
                }
            }
            // ★ 问题12：前锥提供基础护甲(200/400/800) + 防御(F/E/D)，取已装前锥里最高的一档。
            try
            {
                List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
                float bestArmor = 0f;
                float bestDef = 0f;
                for (int i = 0; i < mods.Count; i++)
                {
                    float a = SpaceCfg.NoseconeArmor(mods[i].prefabId);
                    if (a > bestArmor)
                    {
                        bestArmor = a;
                        bestDef = SpaceCfg.NoseconeDefense(mods[i].prefabId);
                    }
                }
                armor += bestArmor;
                defense += bestDef;
            }
            catch (Exception)
            {
                // expected: 前锥模块遍历失败时不计前锥护甲/防御，其余模块加成的装甲仍保留
            }
        }

        /// <summary>脱战恢复：每 60 秒一次性结算一大块（装甲/待结算伤害/模块修复）。</summary>
        internal void TickRegen()
        {
            try
            {
            if (inCombat)
            {
                regenAccum = 0f;
                return;
            }
            if (hpRegenPctCache <= 0f && shieldRegenPctCache <= 0f)
            {
                regenAccum = 0f;
                return;
            }
            float tickSeconds = Mathf.Max(1f, CombatDb.Shared.tickSeconds);
            regenAccum += tickSeconds;
            if (regenAccum < RegenPeriodSeconds)
            {
                return;
            }
            float batches = Mathf.Floor(regenAccum / RegenPeriodSeconds);
            regenAccum -= batches * RegenPeriodSeconds;
            float ratio = batches * RegenPeriodSeconds / tickSeconds;
            if (shieldRegenPctCache > 0f && maxArmor > 0f && armor < maxArmor)
            {
                armor = Mathf.Min(maxArmor, armor + maxArmor * shieldRegenPctCache * ratio);
            }
            bool grounded = craft != null && craft.Status == Clustercraft.CraftStatus.Grounded;
            if (hpRegenPctCache > 0f)
            {
                if (!grounded)
                {
                    pendingDamage = Mathf.Max(0f, pendingDamage - totalHpCache * hpRegenPctCache * ratio);
                }
                else
                {
                    List<RocketModuleInfo> mods = RocketInventory.GetModules(craft);
                    for (int i = 0; i < mods.Count; i++)
                    {
                        BuildingHP hp = mods[i].module != null
                            ? mods[i].module.GetComponent<BuildingHP>() : null;
                        if (hp != null && hp.NeedsRepairs)
                        {
                            hp.Repair(Mathf.Max(1,
                                Mathf.RoundToInt(hp.MaxHitPoints * hpRegenPctCache * ratio)));
                        }
                    }
                }
            }
            RefreshSpeedFactor();   // ★ 回血后更新速度血量系数缓存
            CombatBars.PushCraft(craft, this);
            CombatUi.RefreshBar();
            }
            catch (System.Exception e) { MFLog.HotFail("tick:RocketCombatState", e); }
        }

        /// <summary>受击入口（怪物打火箭）：公式结算 + 乘员同步（K9）。</summary>
        internal void TakeHit(float damage, float penetration, CombatMonsterDef byMonster, float now)
        {
            DamageSource src = new DamageSource();
            src.damage = damage;
            src.armorPenetration = penetration;
            src.armorDamageMult = 1f;
            src.attacker = byMonster;
            src.sourceKind = "monster";
            CombatResolver.Result r = CombatResolver.ApplyDamage(new RocketDamageTarget(this), src);
            lastDamageAt = now;
            CombatResolver.ApplyCrewSync(craft, this, r.hpDamage);
            MFLog.Verbose(LogCat.Space, "[Space] 火箭受击 -" + Mathf.Round(r.hpDamage) + "HP -" + Mathf.Round(r.armorLoss)
                + "装甲（HP " + Mathf.Round(Hp) + "/" + Mathf.Round(totalHpCache)
                + " 装甲 " + Mathf.Round(armor) + "/" + Mathf.Round(maxArmor) + "）");
            CombatBars.PushCraft(craft, this);   // 迷你条：受击后推送（不轮询）
            CombatUi.RefreshBar();
            // 被动紧急脱离：受击把 HP 压到最低值才触发（§13.6：下次受击回到情况 B；
            if (Hp <= CombatDb.Shared.rocketMinHp + 0.001f)
            {
                RocketEmergencyExit.TryTrigger(craft, true);
            }
            RefreshSpeedFactor();   // ★ 受击后更新速度血量系数缓存
        }

        /// <summary>每 Tick 的火箭侧状态：脱战判定 + 落地检测（§8.3/§11.2）。</summary>
        internal void TickCombat(float now)
        {
            try
            {
                if (craft == null)
                {
                    return;
                }
                TickDisengage(now);
                TickLanding();
            }
            catch (Exception e)
            {
                MFLog.HotFail("combat:RocketCombatState.TickCombat", e);
            }
        }

        /// <summary>清空火箭侧交战态（脱战/强制脱战共用，C-37：务必连指令目标与箭头一起清）。</summary>
        internal void ClearCombatState(string reason)
        {
            inCombat = false;
            disengageTimer = 0f;
            orderedTargetUid = 0;
            lastAttackAt = 0f;
            lastDamageAt = 0f;
            CombatBars.RemoveCraft(craft);   // 脱战 → 迷你条隐藏
            CombatRuntime.StopCraft(craft);  // 停掉本 mod 的攻击目的地
            CombatArrow.Hide();              // 收起自绘攻击箭头
            MFLog.Verbose(LogCat.Combat,"[Space] " + (craft != null ? craft.Name : "?") + " 清交战态（" + reason + "）");
        }

        private void TickDisengage(float now)
        {
            try
            {
            CombatSharedCfg sh = CombatDb.Shared;

            // 不在星图或 HP=1 且无法开火时两层一起清，立即解锁飞行与拆除
            if (CombatRuntime.ShouldDisengageFromCraft(craft))
            {
                if (inCombat || orderedTargetUid != 0 || lastAttackAt > 0f || lastDamageAt > 0f)
                {
                    string why = craft.Status == Clustercraft.CraftStatus.Grounded
                        ? "火箭不在星图(在星球上)" : "HP=1且无法开火";
                    CombatRuntime.ForceDisengageBothLayers(craft, now, why);
                }
                return;
            }

            // 目标在射程内且本火箭实际开火过才算交战，避免无弹药指令锁住状态
            bool hasTargetInRange = false;
            MonsterRuntime target = CombatRuntime.FindByUid(orderedTargetUid);
            if (target != null && target.IsAlive && target.monster != null && lastAttackAt > 0f)
            {
                int d = SpaceCfg.Distance(craft.Location, target.monster.Location);
                float range = CombatRuntime.AttackRangeOf(craft);
                if (d <= range * sh.disengageRangeMult)
                {
                    hasTargetInRange = true;
                }
            }
            bool recentlyDamaged = (now - lastDamageAt) < sh.combatMemorySeconds;
            bool recentlyAttacked = (now - lastAttackAt) < sh.combatMemorySeconds;

            if (hasTargetInRange || recentlyDamaged || recentlyAttacked)
            {
                bool wasCombat = inCombat;
                inCombat = true;
                disengageTimer = 0f;
                CombatBars.EnsureCraft(craft, this);   // 交战开始 → 迷你条出现（跟随战斗状态）
                if (!wasCombat)
                {
                    RocketPanelScreen.RequestRebuild();   // ★ 第十八轮-2：交战状态变化 → 面板重建一次（结构变化）
                }
                return;
            }
            if (disengageTimer <= 0f)
            {
                disengageTimer = now + sh.disengageSeconds;
                return;
            }
            if (now >= disengageTimer)
            {
                ClearCombatState("脱战计时到点");   // 复用统一清理
                RocketPanelScreen.RequestRebuild();  // 脱战重建面板
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:RocketCombatState", e); }
        }

        private void TickLanding()
        {
            try
            {
            string status = craft.Status.ToString();
            if (status == lastStatus)
            {
                return;
            }
            string prev = lastStatus;
            lastStatus = status;
            bool nowGrounded = status == "Grounded";
            bool wasGrounded = prev == "Grounded";
            if (nowGrounded && !wasGrounded)
            {
                SettleOnGround();
                // ★ 着陆后清残留目的地箭头（火箭已到达，selector 目的地若仍指向星球格会留箭头）
                ClearStaleDestinationArrow();
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:RocketCombatState", e); }
        }

        /// <summary>着陆后清残留路线（与 SetCraftStatus 补丁共用同一实现，幂等）。</summary>
        private void ClearStaleDestinationArrow()
        {
            StarMapPathCleanup.OnLanded(craft);
        }

        /// <summary>落地结算：按模块 HP 从高到低分摊，每模块保底 min_module_hp，模块不可摧毁。</summary>
        internal void SettleOnGround()
        {
            if (pendingDamage <= 0f)
            {
                pendingDamage = 0f;
                return;
            }
            if (!CombatDb.Shared.settleOnLand)
            {
                pendingDamage = 0f;
                return;
            }
            float dmg = pendingDamage;
            List<RocketModuleInfo> modules = RocketInventory.GetModules(craft);
            modules.Sort(delegate (RocketModuleInfo a, RocketModuleInfo b)
            {
                int ha = 0;
                int hb = 0;
                BuildingHP pa = a.module != null ? a.module.GetComponent<BuildingHP>() : null;
                BuildingHP pb = b.module != null ? b.module.GetComponent<BuildingHP>() : null;
                if (pa != null) ha = pa.HitPoints;
                if (pb != null) hb = pb.HitPoints;
                return hb.CompareTo(ha);
            });
            int minModuleHp = CombatDb.Shared.minModuleHp;
            int hits = 0;
            for (int i = 0; i < modules.Count && dmg > 0f; i++)
            {
                RocketModuleInfo info = modules[i];
                if (info.module == null)
                {
                    continue;
                }
                BuildingHP hp = info.module.GetComponent<BuildingHP>();
                if (hp == null || hp.invincible)
                {
                    continue;
                }
                int allow = hp.HitPoints - minModuleHp;
                if (allow <= 0)
                {
                    continue;
                }
                int take = Mathf.Min(allow, Mathf.RoundToInt(dmg));
                if (take > 0)
                {
                    hp.DoDamage(take);
                    dmg -= take;
                    hits++;
                }
            }
            if (dmg > 0f)
            {
                Debug.LogWarning("[Space] 火箭落地结算：还有 " + Mathf.Round(dmg)
                    + " 点伤害未分摊（模块不可摧毁，每模块保底 " + minModuleHp + " HP）");
            }
            if (CombatDb.Shared.armorRegenOnLand > 0f)
            {
                armor = Mathf.Min(maxArmor, armor + maxArmor * CombatDb.Shared.armorRegenOnLand);
            }
            MFLog.Info(LogCat.Space, "[Space] ★ 火箭落地结算：分摊 " + Mathf.Round(pendingDamage) + " 点伤害到 " + hits
                + " 个模块（模块保底 " + minModuleHp + " HP，落地不摧毁）");
            pendingDamage = 0f;
            RefreshSpeedFactor();   // ★ 落地结算后更新速度血量系数缓存
            CombatBars.PushCraft(craft, this);   // 结算后血量变化 → 推送
        }

        /// <summary>确保组件存在（模板注入失败时的兜底，会打警告）。</summary>
        internal static RocketCombatState Ensure(Clustercraft craft)
        {
            if (craft == null)
            {
                return null;
            }
            RocketCombatState st = craft.GetComponent<RocketCombatState>();
            if (st == null)
            {
                st = craft.gameObject.AddOrGet<RocketCombatState>();
                Debug.LogWarning("[Space] 火箭 " + craft.Name
                    + " 缺 RocketCombatState 组件（prefab 模板注入未生效？已运行时补挂，字段会丢一次）");
            }
            return st;
        }
    }
}
