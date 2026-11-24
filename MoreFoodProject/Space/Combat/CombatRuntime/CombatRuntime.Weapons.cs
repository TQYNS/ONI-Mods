// 用途：火箭武器：开火、追击、弹药选择与目标判定

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        // 火箭开火（§12.5~12.9）

        private static void TickRocketWeapons(Clustercraft craft, RocketCombatState st, float now)
        {
            try
            {
            RocketLoadout lo = RocketLoadout.Get(craft);
            if (lo == null || lo.weaponModuleIds.Count == 0)
            {
                return;
            }
            MonsterRuntime target = FindByUid(st.orderedTargetUid);
            if (target == null || !target.IsAlive)
            {
                return;
            }
            int d = SpaceCfg.Distance(craft.Location, target.Cell);
            // ★ Bug2：不再改写 selector 目的地（攻击不影响"选择目的地"）。

            Dictionary<string, float> dict = RocketAmmoDictionary.Build(craft);
            WarnAmmoWhitelistOnce(craft, dict);

            List<int> queue = new List<int>();
            for (int i = 0; i < lo.weaponModuleIds.Count; i++)
            {
                ModuleDef wdef = CombatDb.FindModule(lo.weaponModuleIds[i]);
                if (wdef == null || wdef.slotType != "weapon")
                {
                    continue;
                }
                if (i < lo.weaponAttackUntil.Count && lo.weaponAttackUntil[i] > now)
                {
                    continue;
                }
                AmmoDef ammo = ResolveCurrentAmmo(lo, i, wdef, dict);
                if (ammo == null)
                {
                    continue;
                }
                if (d > WeaponRange(craft, wdef, ammo))
                {
                    continue;
                }
                queue.Add(i);
            }
            queue.Sort(delegate(int a, int b)
            {
                float ua = a < lo.weaponAttackUntil.Count ? lo.weaponAttackUntil[a] : 0f;
                float ub = b < lo.weaponAttackUntil.Count ? lo.weaponAttackUntil[b] : 0f;
                int c = ua.CompareTo(ub);
                if (c != 0)
                {
                    return c;
                }
                c = a.CompareTo(b);
                if (c != 0)
                {
                    return c;
                }
                return string.CompareOrdinal(lo.weaponModuleIds[a], lo.weaponModuleIds[b]);
            });

            int attacks = 0;
            int cap = Mathf.Max(1, CombatDb.Shared.maxAttacksPerSecond * CombatDb.Shared.tickSeconds);
            for (int q = 0; q < queue.Count; q++)
            {
                if (!target.IsAlive)
                {
                    break;
                }
                int i = queue[q];
                ModuleDef wdef = CombatDb.FindModule(lo.weaponModuleIds[i]);
                if (wdef == null)
                {
                    continue;
                }
                AmmoDef ammo = ResolveCurrentAmmo(lo, i, wdef, dict);
                if (ammo == null)
                {
                    continue;
                }
                if (d > WeaponRange(craft, wdef, ammo))
                {
                    continue;
                }
                float available;
                if (!dict.TryGetValue(ammo.prefabId, out available))
                {
                    continue;
                }
                float actualKg = Mathf.Min(ammo.shotMassKg, available);
                if (actualKg <= 0f)
                {
                    continue;
                }
                FireOnce(craft, st, wdef, ammo, target, actualKg);
                dict[ammo.prefabId] = Mathf.Max(0f, available - actualKg);
                ConsumeAmmo(craft, ammo, actualKg);
                float interval = Mathf.Max(ammo.attackInterval / Mathf.Max(0.01f, wdef.stats.fireRateMul),
                    CombatDb.Shared.minAttackInterval);
                if (i < lo.weaponAttackUntil.Count)
                {
                    lo.weaponAttackUntil[i] = now + interval;
                }
                st.lastAttackAt = now;
                st.inCombat = true;
                attacks++;
                if (attacks >= cap)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 本 Tick 攻击结算数达上限 " + cap + "（max_attacks_per_second 保险丝）");
                    break;
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:CombatRuntime", e); }
        }

        private static void MaintainChase(Clustercraft craft, MonsterRuntime target, int dist, float baseRange)
        {
            try
            {
                ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
                if (selector == null)
                {
                    return;
                }
                if (dist > baseRange)
                {
                    if (selector.GetDestination() != target.Cell)
                    {
                        selector.SetDestination(target.Cell);   // 追踪移动目标
                    }
                }
                else if (!selector.IsAtDestination())
                {
                    selector.SetDestination(craft.Location);    // 进射程 → 停车
                }
            }
            catch (Exception e)
            {
                MFLog.Exc("combat:chase", LogCat.Combat, e, "[Space] 攻击追击移动失败（目标格保持不变，下次 tick 重试）");
            }
        }

        internal static void StopCraft(Clustercraft craft)
        {
            try
            {
                ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
                if (selector == null || selector.IsAtDestination())
                {
                    return;
                }
                // 只在"当前目的地是本 mod 的攻击目标（怪物格）"时才收车。
                if (!IsMonsterDestination(selector.GetDestination()))
                {
                    return;
                }
                selector.SetDestination(craft.Location);
            }
            catch (Exception e)
            {
                MFLog.Exc("combat:stop", LogCat.Combat, e, "[Space] 停车失败（火箭会继续飞向目标格）");
            }
        }

        /// <summary>目的地格是否停着本 mod 的怪物（= 我们下的攻击/追击指令）。</summary>
        private static bool IsMonsterDestination(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return false;
            }
            List<ClusterGridEntity> list;
            if (!grid.cellContents.TryGetValue(cell, out list) || list == null)
            {
                return false;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is SpaceMonster)
                {
                    return true;
                }
            }
            return false;
        }

        private static void WarnAmmoWhitelistOnce(Clustercraft craft, Dictionary<string, float> dict)
        {
            int id = craft.GetInstanceID();
            if (dict.Count > 0 || warnedAmmoWhitelist.Contains(id))
            {
                return;
            }
            warnedAmmoWhitelist.Add(id);
            // 货舱里有物品但一件都不在弹药白名单 → 提示一次
            List<RocketHold> holds = RocketInventory.GetHolds(craft);
            for (int i = 0; i < holds.Count; i++)
            {
                List<GameObject> items = holds[i].storage != null ? holds[i].storage.GetItems() : null;
                if (items != null && items.Count > 0)
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 火箭 " + craft.Name + " 货舱有货但都不是弹药（白名单 "
                        + string.Join(",", CombatDb.Shared.ammoSourcePrefabs) + " 或物品不在 ammo.yaml）");
                    return;
                }
            }
        }

        /// <summary>当前实际发射弹种：主弹（具体/auto）→ 备弹 → 无；不修改用户字段。</summary>
        private static AmmoDef ResolveCurrentAmmo(RocketLoadout lo, int weaponIndex,
            ModuleDef weapon, Dictionary<string, float> dict)
        {
            string primary = weaponIndex < lo.weaponPrimaryAmmoIds.Count
                ? lo.weaponPrimaryAmmoIds[weaponIndex] : null;
            if (!string.IsNullOrEmpty(primary) && primary != "auto")
            {
                AmmoDef a = CombatDb.FindAmmo(primary);
                if (a != null && a.caliber == weapon.caliber && StockOf(dict, a) > 0f)
                {
                    return a;
                }
            }
            else
            {
                AmmoDef auto = AutoPick(weapon, dict);
                if (auto != null)
                {
                    return auto;
                }
            }
            string backup = weaponIndex < lo.weaponBackupAmmoIds.Count
                ? lo.weaponBackupAmmoIds[weaponIndex] : null;
            if (!string.IsNullOrEmpty(backup))
            {
                AmmoDef b = CombatDb.FindAmmo(backup);
                if (b != null && b.caliber == weapon.caliber && StockOf(dict, b) > 0f)
                {
                    return b;
                }
            }
            if (!string.IsNullOrEmpty(primary) && primary != "auto")
            {
                AmmoDef auto = AutoPick(weapon, dict);
                if (auto != null)
                {
                    return auto;
                }
            }
            return null;
        }

        private static AmmoDef AutoPick(ModuleDef weapon, Dictionary<string, float> dict)
        {
            List<AmmoDef> pool = CombatDb.AmmoOfCaliber(weapon.caliber);
            if (pool == null || pool.Count == 0)
            {
                return null;
            }
            AmmoDef best = null;
            for (int i = 0; i < pool.Count; i++)
            {
                AmmoDef a = pool[i];
                if (StockOf(dict, a) <= 0f)
                {
                    continue;
                }
                if (best == null || a.damage > best.damage
                    || (a.damage == best.damage && TierRank(a.tier) > TierRank(best.tier)))
                {
                    best = a;
                }
            }
            return best;
        }

        private static float StockOf(Dictionary<string, float> dict, AmmoDef ammo)
        {
            if (ammo == null || string.IsNullOrEmpty(ammo.prefabId))
            {
                return 0f;
            }
            float kg;
            return dict.TryGetValue(ammo.prefabId, out kg) ? kg : 0f;
        }

        private static int TierRank(string tier)
        {
            switch (tier)
            {
                case "S": return 7;
                case "A": return 6;
                case "B": return 5;
                case "C": return 4;
                case "D": return 3;
                case "E": return 2;
                default: return 1;
            }
        }

        private static void FireOnce(Clustercraft craft, RocketCombatState st, ModuleDef weapon, AmmoDef ammo,
            MonsterRuntime target, float actualKg)
        {
            float massFactor = actualKg / Mathf.Max(0.0001f, ammo.shotMassKg);
            float pen = ammo.armorPenetration + (st != null ? st.armorPenAddCache : 0f);
            float armorMul = ammo.armorDamageMult * (st != null ? st.armorDamageMulCache : 1f);
            float weaponMul = weapon != null ? weapon.stats.damageMul : 1f;
            for (int p = 0; p < ammo.pellets; p++)
            {
                float dmg = ammo.damage * massFactor * weaponMul
                    * MoreFood_KFC.DrinkRocketIntegration.RocketAmmoDamageMultiplier(craft);
                DamageSource src = new DamageSource();
                src.damage = dmg;
                src.armorPenetration = pen;
                src.armorDamageMult = armorMul;
                src.attacker = craft;
                src.sourceKind = "ammo";
                CombatResolver.ApplyDamage(new MonsterDamageTarget(target), src);
            }
            // 游荡（Idle/Roam）中的怪物被攻击要立刻还手；
            bool idleLike = target.state == MonsterState.Idle || target.state == MonsterState.Roam;
            if ((target.aggroCraft == null || idleLike)
                && target.state != MonsterState.Flee && target.state != MonsterState.Leaving)
            {
                SetAggro(target, craft);
            }
            CombatBars.PushMonster(target);
            CombatFx.PlayRocket(craft, target.monster, ammo);
            MFLog.Verbose(LogCat.Combat,"[Space] " + craft.Name + " 用 " + (weapon != null ? weapon.name : "?") + " 开火（"
                + ammo.name + " ×" + ammo.pellets + "，" + actualKg.ToString("0.###") + "kg，伤害系数 "
                + massFactor.ToString("0.###") + "）→ " + target.def.name + " HP "
                + Mathf.Round(Mathf.Max(0f, target.hp)) + "/" + Mathf.Round(target.def.hp)
                + " 装甲 " + Mathf.Round(Mathf.Max(0f, target.armor)) + "/" + Mathf.Round(target.def.armor));
            if (target.hp <= 0f)
            {
                KillMonster(target, craft, TradeRegistry.Now(), "被 " + craft.Name + " 击毁");
            }
        }

        private static void ConsumeAmmo(Clustercraft craft, AmmoDef ammo, float actualKg)
        {
            if (string.IsNullOrEmpty(ammo.prefabId) || actualKg <= 0f)
            {
                return;
            }
            float taken = RocketInventory.Consume(craft, new Tag(ammo.prefabId), actualKg);
            if (taken + 0.0001f < actualKg)
            {
                MFLog.Warn(LogCat.Combat,"[Space] 弹药实际扣取不足：" + ammo.prefabId + " 需 " + actualKg
                    + "kg 实扣 " + taken + "kg（字典与实际库存不同源？）");
            }
        }

        // 命令 / 任务联动

        internal static void OrderAttack(Clustercraft craft, SpaceMonster monster)
        {
            if (craft == null || monster == null)
            {
                return;
            }
            MonsterRuntime rt = FindByInstance(monster);
            if (rt == null)
            {
                TradeUi.Notify("无法攻击", "目标不在战斗列表中");
                return;
            }
            RocketCombatState st = RocketCombatState.Ensure(craft);
            if (st == null)
            {
                return;
            }
            st.orderedTargetUid = rt.uid;
            rt.ordered = true;
            // 下令只锁目标，不置交战态；lastAttackAt 仅由实际开火(FireOnce)写入
            // ★ Bug2：不改写 selector 目的地 —— 攻击只设定目标，不干扰玩家"选择目的地"。
            MFLog.Verbose(LogCat.Combat,"[Space] " + craft.Name + " 接到攻击指令：" + rt.def.name + "（" + rt.defId + "）@"
                + SpaceCfg.Pos(rt.Cell) + "，进入射程即开火");
            RocketPanelScreen.RequestRebuild();   // 下令攻击 → 面板重建（出现交战区）
        }

        internal static bool HasAliveQuestMonster(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return false;
            }
            for (int i = 0; i < Monsters.Count; i++)
            {
                MonsterRuntime rt = Monsters[i];
                if (rt != null && rt.IsAlive && rt.questId == questId)
                {
                    return true;
                }
            }
            return false;
        }

    }
}
