using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    // 紧急脱离（§13 + K9/K12）

    internal static class RocketEmergencyExit
    {
        internal static bool CanTriggerActive(Clustercraft craft)
        {
            RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
            if (st == null)
            {
                return false;
            }
            float now = TradeRegistry.Now();
            if (st.emergencyCooldownUntil > now)
            {
                return false;
            }
            return st.Hp <= st.totalHpCache * CombatDb.Shared.emergencyTriggerHpPct;
        }

        internal static bool IsCoolingDown(Clustercraft craft)
        {
            RocketCombatState st = craft != null ? craft.GetComponent<RocketCombatState>() : null;
            return st != null && st.emergencyCooldownUntil > TradeRegistry.Now();
        }

        // 被动触发的调用点在 RocketCombatState.TakeHit（受击时判定，不做 Tick 轮询）；按 §13.6「下次受击回到情况 B」语义，只有受击才尝试

        internal static bool TryTrigger(Clustercraft craft, bool passive)
        {
            try
            {
                if (craft == null)
                {
                    return false;
                }
                RocketCombatState st = RocketCombatState.Ensure(craft);
                if (st == null)
                {
                    return false;
                }
                CombatSharedCfg sh = CombatDb.Shared;
                float now = TradeRegistry.Now();
                bool coolingDown = st.emergencyCooldownUntil > now;

                if (!passive)
                {
                    if (coolingDown)
                    {
                        TradeUi.Notify("紧急脱离不可用", "冷却中（还剩 "
                            + Mathf.CeilToInt((st.emergencyCooldownUntil - now) / 600f) + " 周期）");
                        return false;
                    }
                    if (st.Hp > st.totalHpCache * sh.emergencyTriggerHpPct)
                    {
                        TradeUi.Notify("紧急脱离不可用", "HP 需低于 "
                            + Mathf.Round(sh.emergencyTriggerHpPct * 100f) + "% 才能主动脱离");
                        return false;
                    }
                }
                else
                {
                    if (!sh.passiveOnMinHp)
                    {
                        return false;
                    }
                }

                float lossPct;
                if (coolingDown)
                {
                    lossPct = sh.cooldownLossPct;   // 情况 C：丢光
                }
                else if (passive)
                {
                    lossPct = UnityEngine.Random.Range(sh.passiveLossMin, sh.passiveLossMax);
                }
                else
                {
                    lossPct = UnityEngine.Random.Range(sh.activeLossMin, sh.activeLossMax);
                }

                AxialI from = craft.Location;
                DropCargoAsLootField(craft, lossPct);
                AxialI to = GetHomeSystemCell();
                Teleport(craft, to);
                st.emergencyCooldownUntil = now + sh.cooldownCycles * 600f;
                st.emergencyUsed++;
                st.orderedTargetUid = 0;
                PlayWarpFx(from);
                CombatUi.RefreshBar();
                if (GameScheduler.Instance != null)
                {
                    GameScheduler.Instance.Schedule("SpaceBarRefresh",
                        Mathf.Max(1f, sh.cooldownCycles * 600f),
                        delegate(object o) { CombatUi.RefreshBar(); }, null, null);
                }
                string tag = passive ? (coolingDown ? "被动·冷却中（丢光）" : "被动") : "主动";
                MFLog.Info(LogCat.Space, "[Space] ★ 紧急脱离（" + tag + "）：" + craft.Name + " "
                    + SpaceCfg.Pos(from) + " → 母星系 " + SpaceCfg.Pos(to)
                    + "，丢失货物 " + Mathf.Round(lossPct * 100f) + "%，冷却 "
                    + sh.cooldownCycles + " 周期");
                EmergencyExitEvent ev = new EmergencyExitEvent();
                ev.craftId = craft.Name;
                ev.fromCell = from;
                ev.toCell = to;
                ev.passive = passive;
                ev.coolingDown = coolingDown;
                ev.lossPct = lossPct;
                SpaceCombatBus.PublishEmergencyExit(ev);
                TradeUi.Notify("紧急脱离", craft.Name + "（" + tag + "）丢失 "
                    + Mathf.Round(lossPct * 100f) + "% 货物，已传送回母星系");
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 紧急脱离失败");
                return false;
            }
        }

        // 内部

        private static bool IsAmmoSource(RocketHold hold)
        {
            return hold != null && RocketAmmoDictionary.IsAmmoSource(hold.prefabId);
        }

        /// <summary>只丢弹药白名单货舱里的货（漏洞48），按各货舱各物品质量比例扣。</summary>
        private static void DropCargoAsLootField(Clustercraft craft, float pct)
        {
            if (pct <= 0f || !CombatDb.Shared.dropAsLootField)
            {
                return;
            }
            Dictionary<string, float> drops = new Dictionary<string, float>();
            List<RocketHold> holds = RocketInventory.GetHolds(craft);
            for (int i = 0; i < holds.Count; i++)
            {
                RocketHold hold = holds[i];
                if (!IsAmmoSource(hold) || hold.storage == null)
                {
                    continue;
                }
                List<GameObject> items = hold.storage.GetItems();
                if (items == null || items.Count == 0)
                {
                    continue;
                }
                // 快照物品的 tag（ConsumeIgnoringDisease 按 tag 扣）
                HashSet<string> tags = new HashSet<string>();
                for (int k = 0; k < items.Count; k++)
                {
                    GameObject item = items[k];
                    if (item == null)
                    {
                        continue;
                    }
                    KPrefabID pid = item.GetComponent<KPrefabID>();
                    if (pid != null)
                    {
                        tags.Add(pid.PrefabTag.Name);
                    }
                }
                foreach (string tagName in tags)
                {
                    float have = hold.storage.GetAmountAvailable(new Tag(tagName));
                    if (have <= 0.0001f)
                    {
                        continue;
                    }
                    float want = have * pct;
                    if (want <= 0.0001f)
                    {
                        continue;
                    }
                    hold.storage.ConsumeIgnoringDisease(new Tag(tagName), want);   // void：可用量刚核对过，视为足额
                    float acc;
                    drops.TryGetValue(tagName, out acc);
                    drops[tagName] = acc + want;
                }
            }
            if (drops.Count == 0)
            {
                MFLog.Warn(LogCat.Space, "[Space] 紧急脱离：白名单货舱没有可丢的货（传送与冷却照常，漏洞54）");
                return;
            }
            List<string> items2 = new List<string>();
            List<float> masses = new List<float>();
            List<int> counts = new List<int>();
            foreach (KeyValuePair<string, float> kv in drops)
            {
                if (kv.Value <= 0f)
                {
                    continue;
                }
                items2.Add(kv.Key);
                masses.Add(kv.Value);
                counts.Add(1);
            }
            CombatRuntime.SpawnLootFieldRaw(craft.Location, items2, masses, counts,
                CombatDb.Shared.lootFieldSourceTag, "紧急丢弃的货物");
        }

        private static AxialI GetHomeSystemCell()
        {
            ClusterManager cm = ClusterManager.Instance;
            if (cm == null)
            {
                return default(AxialI);
            }
            WorldContainer start = cm.GetStartWorld();
            ClusterGridEntity startEntity = start != null ? start.GetComponent<ClusterGridEntity>() : null;
            if (startEntity == null)
            {
                return default(AxialI);
            }
            AxialI startCell = startEntity.Location;
            int radius = CombatDb.Shared.homeSearchRadius;
            for (int r = 1; r <= radius; r++)
            {
                List<AxialI> ring = CellsAtRing(startCell, r);
                for (int i = 0; i < ring.Count; i++)
                {
                    if (IsFree(ring[i]))
                    {
                        return ring[i];
                    }
                }
            }
            Debug.LogWarning("[Space] 母星系 " + radius + " 环内没有空位，原地传送（漏洞61）");
            return startCell;
        }

        private static List<AxialI> CellsAtRing(AxialI center, int radius)
        {
            List<AxialI> list = new List<AxialI>();
            try
            {
                List<AxialI> all = AxialUtil.GetAllPointsWithinRadius(center, radius);
                if (all == null)
                {
                    return list;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    AxialI rel = SpaceCfg.Mk(all[i].Q - center.Q, all[i].R - center.R);
                    if (SpaceCfg.Ring(rel) == radius)
                    {
                        list.Add(all[i]);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 环枚举失败");
            }
            return list;
        }

        private static bool IsFree(AxialI cell)
        {
            ClusterGrid grid = ClusterGrid.Instance;
            if (grid == null || !grid.IsValidCell(cell))
            {
                return false;
            }
            List<ClusterGridEntity> list;
            if (grid.cellContents.TryGetValue(cell, out list) && list != null && list.Count > 0)
            {
                return false;   // 有任何实体都算占用（含原版星球/POI/裂缝，§13.3）
            }
            return true;
        }

        private static void Teleport(Clustercraft craft, AxialI target)
        {
            try
            {
                ClusterDestinationSelector selector = craft.GetComponent<ClusterDestinationSelector>();
                if (selector != null)
                {
                    selector.SetDestination(target);
                }
                craft.Location = target;
                ClusterTraveler traveler = craft.GetComponent<ClusterTraveler>();
                if (traveler != null)
                {
                    // MarkPathDirty 是私有方法（dnSpy 实证）→ 反射调用，让在途路径立即作废重算
                    System.Reflection.MethodInfo m = typeof(ClusterTraveler).GetMethod("MarkPathDirty",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic);
                    if (m != null)
                    {
                        m.Invoke(traveler, null);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 紧急脱离传送失败");
            }
        }

        private static void PlayWarpFx(AxialI at)
        {
            try
            {
                KMonoBehaviour.PlaySound(CombatDb.Shared.warpSound);
            }
            catch (Exception)
            {
                // expected: 音效缺失/播放失败时静音，下面的脱离光效与传送照常执行
            }
            // 视觉：用光团在脱离点闪一下（warp_anim 完整播放留待美术资源，K5 占位）
            try
            {
                SpaceZones z = StarMapVisuals.Zones;
                float hexW = StarMapVisuals.GetHexWidth(ClusterMapScreen.Instance, at);
                CombatFx.PlayBurst(at, new Color(0.55f, 0.75f, 1f, 1f), Mathf.Max(1f, hexW) * 0.30f);
            }
            catch (Exception)
            {
                // expected: 脱离点光效播不出时只少个视觉提示，传送与航线重算已先行完成
            }
        }
    }
}
