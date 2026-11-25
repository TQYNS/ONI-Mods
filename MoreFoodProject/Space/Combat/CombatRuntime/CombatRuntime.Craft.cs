// 用途：战舰查询与推进：节拍入口、速度、区域判定、移动与射程

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatRuntime
    {
        // 主 Tick（低频有界，K6）

        internal static void Tick(float now)
        {
            if (!inited)
            {
                Init();
                if (!inited)
                {
                    return;
                }
            }
            try
            {
                CombatFx.BeginFrame();
                PruneCrafts();
                foreach (Clustercraft seen in Components.Clustercrafts)
                {
                    OnCraftMoved(seen);
                }
                for (int i = Monsters.Count - 1; i >= 0; i--)
                {
                    if (i >= Monsters.Count)
                    {
                        continue;
                    }
                    MonsterRuntime rt = Monsters[i];
                    if (rt == null)
                    {
                        RemoveMonsterAt(i);
                        continue;
                    }
                    if (rt.monster == null)
                    {
                        // 实体异常销毁也按离场广播，避免任务目标被静默移除后只能等待超时
                        EscapeMonster(rt);
                        continue;
                    }
                    if (rt.hp <= 0f)
                    {
                        KillMonster(rt, null, now, "already-dead");
                        continue;
                    }
                    TickMonster(rt, now);
                }
                for (int i = Crafts.Count - 1; i >= 0; i--)
                {
                    Clustercraft craft = Crafts[i];
                    if (craft == null)
                    {
                        Crafts.RemoveAt(i);
                        continue;
                    }
                    RocketCombatState st = craft.GetComponent<RocketCombatState>();
                    if (st == null)
                    {
                        continue;
                    }
                    st.SyncIfNeeded();
                    RocketLoadout lo = RocketLoadout.Get(craft);
                    if (lo != null)
                    {
                        lo.SyncIfNeeded();
                    }
                    st.TickCombat(now);
                    if (!st.inCombat)
                    {
                        st.TickRegen();
                    }
                    TickRocketWeapons(craft, st, now);
                }
                TickLootFields();
                SpaceReveal.RetryPending();
            }
            catch (Exception e)
            {
                // 热路径：首错带完整堆栈，之后同 key 静默，避免每 tick 刷屏
                MFLog.Exc("combat:tick", LogCat.Combat, e, "[Space] 战斗 Tick 异常（本帧剩余战斗逻辑跳过）");
            }
        }

        // 火箭战斗速度系数（Π utility.speed_mul）。
        internal static float CraftSpeedMul(Clustercraft craft)
        {
            try
            {
                RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
                return st != null ? Mathf.Max(0.1f, st.speedMulCache) : 1f;
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        // 由 SpaceTick 的 craft 遍历每秒喂进来（不额外全图扫描，K6）。
        internal static void OnCraftSeen(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            if (!Crafts.Contains(craft))
            {
                Crafts.Add(craft);
            }
        }

        // 每 Tick 对比 lastCell，变化 → 警戒扫描（§7.3）。
        internal static void OnCraftMoved(Clustercraft craft)
        {
            if (craft == null)
            {
                return;
            }
            OnCraftSeen(craft);
            int id = craft.GetInstanceID();
            AxialI last;
            if (lastCellByCraft.TryGetValue(id, out last) && last == craft.Location)
            {
                return;
            }
            lastCellByCraft[id] = craft.Location;
            AlertOnCraftMoved(craft);
            CombatBars.OnCraftMoved(craft);   // 迷你条：随移动重挂（事件驱动，复用本钩子）
            // 设计变更：不再在这里刷新按钮排。交易/加油的可用态一律在「显示前」现算、
            //   「点击时」再校验，不依赖位置事件（事件漏一次就会永久显示错误）。
        }

        private static void PruneCrafts()
        {
            for (int i = Crafts.Count - 1; i >= 0; i--)
            {
                if (Crafts[i] == null)
                {
                    Crafts.RemoveAt(i);
                }
            }
        }

        // 不在星图或 HP=1 且无可用弹药时判定火箭脱战
        internal static bool ShouldDisengageFromCraft(Clustercraft craft)
        {
            if (craft == null)
            {
                return true;
            }
            // 在星球上视为不在星图飞行
            if (craft.Status == Clustercraft.CraftStatus.Grounded)
            {
                return true;
            }
            // HP 触底且无可用武器弹药
            RocketCombatState st = craft.GetComponent<RocketCombatState>();
            if (st != null && st.Hp <= CombatDb.Shared.rocketMinHp + 0.001f && !CanRocketFire(craft))
            {
                return true;
            }
            return false;
        }

        // 火箭能否开火：只看有无 weapon 与匹配且非空的弹药，不计 CD
        internal static bool CanRocketFire(Clustercraft craft)
        {
            if (craft == null)
            {
                return false;
            }
            if (craft.Status == Clustercraft.CraftStatus.Grounded)
            {
                return false;
            }
            try
            {
                RocketLoadout lo = RocketLoadout.Get(craft);
                if (lo == null || lo.weaponModuleIds.Count == 0)
                {
                    return false;
                }
                Dictionary<string, float> dict = RocketAmmoDictionary.Build(craft);
                for (int i = 0; i < lo.weaponModuleIds.Count; i++)
                {
                    ModuleDef wdef = CombatDb.FindModule(lo.weaponModuleIds[i]);
                    if (wdef == null || wdef.slotType != "weapon")
                    {
                        continue;
                    }
                    AmmoDef ammo = ResolveCurrentAmmo(lo, i, wdef, dict);
                    if (ammo != null && StockOf(dict, ammo) > 0f)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.HotFail("combat:canFire", e);
            }
            return false;
        }

        // 区域 / 格子工具

        internal static string RegionOf(AxialI cell)
        {
            SpaceZones z = StarMapVisuals.Zones;
            if (z != null && z.valid && z.IsSub(cell))
            {
                return "sub";
            }
            return "main";
        }

        internal static bool IsEdgeCell(AxialI cell, string region)
        {
            SpaceZones z = StarMapVisuals.Zones;
            if (z == null || !z.valid)
            {
                return false;
            }
            if (region == "sub")
            {
                AxialI rel = SpaceCfg.Mk(cell.Q - z.subCenter.Q, cell.R - z.subCenter.R);
                return SpaceCfg.Ring(rel) >= z.subRadius;
            }
            return SpaceCfg.Ring(cell) >= z.visibleEdgeRing;
        }

        internal static bool TryStep(MonsterRuntime rt, AxialI target)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || !grid.IsValidCell(target))
            {
                return false;
            }

            // 区域边界闸门（修复：怪物把可见外沿之外的虚空当可走区）
            SpaceZones z = StarMapVisuals.Zones;
            if (z == null || !z.valid)
            {
                return false;   // 分区未就绪 → 保守拒绝，等下个 Tick（原先会放行到任意格）
            }
            if (rt.region == "sub")
            {
                // 子图怪：只允许走到子图集内的格子
                if (!z.IsSub(target))
                {
                    return false;
                }
            }
            else
            {
                // 主图怪：不许走进子图，也不许越过可见外沿（IsVoid = Ring > visibleEdgeRing 且非子图）
                if (z.IsSub(target) || z.IsVoid(target))
                {
                    return false;
                }
            }

            List<ClusterGridEntity> list;
            if (grid.cellContents.TryGetValue(target, out list) && list != null && list.Count > 0)
            {
                return false;
            }
            return true;
        }

        internal static Clustercraft FindCraftAt(AxialI cell)
        {
            for (int i = 0; i < Crafts.Count; i++)
            {
                Clustercraft c = Crafts[i];
                if (c != null && c.Location == cell)
                {
                    return c;
                }
            }
            return null;
        }

        internal static Clustercraft FindCraftByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            for (int i = 0; i < Crafts.Count; i++)
            {
                Clustercraft c = Crafts[i];
                if (c != null && c.Name == name)
                {
                    return c;
                }
            }
            // Crafts 可能还没被喂过 —— 现场扫一遍网格（读档恢复时用，低频）
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null)
            {
                return null;
            }
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in grid.cellContents)
            {
                List<ClusterGridEntity> list = kv.Value;
                if (list == null)
                {
                    continue;
                }
                for (int j = 0; j < list.Count; j++)
                {
                    Clustercraft c = list[j] as Clustercraft;
                    if (c != null && c.Name == name)
                    {
                        OnCraftSeen(c);
                        return c;
                    }
                }
            }
            return null;
        }

        internal static MonsterRuntime FindByInstance(SpaceMonster monster)
        {
            if (monster == null)
            {
                return null;
            }
            MonsterRuntime rt;
            return byInstance.TryGetValue(monster.GetInstanceID(), out rt) ? rt : null;
        }

        internal static MonsterRuntime FindByUid(int uid)
        {
            if (uid <= 0)
            {
                return null;
            }
            for (int i = 0; i < Monsters.Count; i++)
            {
                if (Monsters[i] != null && Monsters[i].uid == uid)
                {
                    return Monsters[i];
                }
            }
            return null;
        }

        // 火箭追击用最大有效射程：基础 + 最佳武器 + 雷达 + 最佳弹药。
        internal static float AttackRangeOf(Clustercraft craft)
        {
            float baseRange = BaseAttackRange(craft);
            try
            {
                RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
                int radar = st != null ? st.radarRangeBonusCache : 0;
                int bestWeapon = 0;
                RocketLoadout lo = RocketLoadout.Get(craft);
                if (lo != null)
                {
                    for (int i = 0; i < lo.weaponModuleIds.Count; i++)
                    {
                        ModuleDef def = CombatDb.FindModule(lo.weaponModuleIds[i]);
                        if (def != null && def.stats.rangeBonus > bestWeapon)
                        {
                            bestWeapon = def.stats.rangeBonus;
                        }
                    }
                }
                int bestAmmo = 0;
                for (int i = 0; i < CombatDb.Ammo.Count; i++)
                {
                    if (CombatDb.Ammo[i].rangeBonus > bestAmmo)
                    {
                        bestAmmo = CombatDb.Ammo[i].rangeBonus;
                    }
                }
                return baseRange + bestWeapon + radar + bestAmmo;
            }
            catch (Exception e)
            {
                MFLog.Exc("combat:range", LogCat.Combat, e, "[Space] 计算攻击距离失败（按基础射程继续）");
                return baseRange;
            }
        }

        internal static float WeaponRange(Clustercraft craft, ModuleDef weapon, AmmoDef ammo)
        {
            RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
            int radar = st != null ? st.radarRangeBonusCache : 0;
            float wr = weapon != null ? weapon.stats.rangeBonus : 0f;
            float ar = ammo != null ? ammo.rangeBonus : 0f;
            return BaseAttackRange(craft) + wr + radar + ar;
        }

        internal static float BaseAttackRange(Clustercraft craft)
        {
            float baseRange = 1f;
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
                        baseRange = rd.attackRange;
                        break;
                    }
                }
            }
            return baseRange;
        }
    }
}
