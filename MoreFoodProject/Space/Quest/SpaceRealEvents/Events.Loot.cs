// 用途：拾荒类事件：失落物流、海盗残骸与战利品工厂

using System;
using EventFramework.Distribution.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSerialization;
using Klei.AI;
using Klei.AI.DiseaseGrowthRules;
using STRINGS;
using TUNING;
using UnityEngine;
using MoreFood_KFC;

namespace MoreFood_KFC
{
    internal static partial class SpaceRealEvents
    {
        private static void LostLogistics(IEventContext inst)
        {
            try
            {
                List<string> items = new List<string>();
                List<float> masses = new List<float>();
                List<int> counts = new List<int>();

                List<string> foodPicks = PickDistinct(PickModFoods(), 2);
                for (int i = 0; i < foodPicks.Count; i++)
                {
                    AddLoot(items, masses, counts, foodPicks[i], UnityEngine.Random.Range(80f, 200f), 1);
                }
                if (foodPicks.Count < 2)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星际遗失物流箱：本 Mod 食物不足两种（现取 " + foodPicks.Count + " 种）");
                }

                List<string> rarePicks = PickDistinct(
                    PickTagged("ManufacturedMaterial", "RareMaterials"), UnityEngine.Random.Range(1, 3));
                for (int i = 0; i < rarePicks.Count; i++)
                {
                    AddLoot(items, masses, counts, rarePicks[i], UnityEngine.Random.Range(1000f, 3000f), 1);
                }
                if (rarePicks.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星际遗失物流箱：没有可用稀有资源（ManufacturedMaterial/RareMaterials），跳过该项");
                }

                string artifact = PickArtifactPrefab();
                if (!string.IsNullOrEmpty(artifact))
                {
                    AddLoot(items, masses, counts, artifact, 1f, 1);
                }
                else
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星际遗失物流箱：没有可用工艺品 prefab，跳过该项");
                }

                string drink = PickModDrink();
                if (!string.IsNullOrEmpty(drink))
                {
                    AddLoot(items, masses, counts, drink, UnityEngine.Random.Range(50f, 150f), 1);
                }
                else
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星际遗失物流箱：没有可抽取的本 Mod 饮品，跳过该项");
                }

                if (items.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星际遗失物流箱：没有可用奖励 prefab");
                    return;
                }
                LootFieldFactory.Drop("lost_logistics", "星际遗失物流箱", items, masses, counts, inst.InstanceId);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 星际遗失物流箱执行失败");
            }
        }

        private static List<string> PickDistinct(List<string> pool, int count)
        {
            List<string> result = new List<string>();
            if (pool == null || count <= 0)
            {
                return result;
            }
            List<string> copy = new List<string>(pool);
            int n = Mathf.Min(count, copy.Count);
            for (int i = 0; i < n; i++)
            {
                int idx = UnityEngine.Random.Range(0, copy.Count);
                result.Add(copy[idx]);
                copy.RemoveAt(idx);
            }
            return result;
        }

        private static List<string> PickTagged(params string[] tagNames)
        {
            try
            {
                return SpaceLootPools.Get(SpaceLootPools.KeyOf(tagNames), tagNames);
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 取标签物品池失败");
                return new List<string>();
            }
        }

        private static string PickAmmo()
        {
            try
            {
                List<AmmoDef> all = CombatDb.Ammo;
                if (all == null)
                {
                    return null;
                }
                List<string> pool = new List<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    AmmoDef def = all[i];
                    if (def == null || string.IsNullOrEmpty(def.prefabId))
                    {
                        continue;
                    }
                    if (Assets.TryGetPrefab(new Tag(def.prefabId)) == null)
                    {
                        continue;
                    }
                    if (!pool.Contains(def.prefabId))
                    {
                        pool.Add(def.prefabId);
                    }
                }
                if (pool.Count == 0)
                {
                    return null;
                }
                return pool[UnityEngine.Random.Range(0, pool.Count)];
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 取弹药清单失败");
                return null;
            }
        }

        private static float AmmoMass(string prefabId)
        {
            AmmoDef def = CombatDb.FindAmmoByPrefab(prefabId);
            if (def != null && def.caliber == "large")
            {
                return UnityEngine.Random.Range(6f, 18f);
            }
            return UnityEngine.Random.Range(12f, 38f);
        }

        private static List<string> PickModFoods()
        {
            List<string> result = new List<string>();
            try
            {
                List<string> all = MoreFood_KFC.ModFoods.CollectFoodIds();
                if (all == null)
                {
                    return result;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    string id = all[i];
                    if (string.IsNullOrEmpty(id) || result.Contains(id))
                    {
                        continue;
                    }
                    if (Assets.TryGetPrefab(new Tag(id)) == null)
                    {
                        continue;
                    }
                    result.Add(id);
                }
                MFLog.Verbose(LogCat.Event,"[Space] 星际遗失物流箱：本 Mod 可抽取食物 " + result.Count + " 种");
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 取本 Mod 食物清单失败");
            }
            return result;
        }

        private static string PickModDrink()
        {
            try
            {
                List<MoreFood_KFC.Drinks.DrinkEntry> list = MoreFood_KFC.Drinks.CollectItems();
                if (list == null || list.Count == 0)
                {
                    return null;
                }
                List<string> ids = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    MoreFood_KFC.Drinks.DrinkEntry entry = list[i];
                    if (entry == null || string.IsNullOrEmpty(entry.id))
                    {
                        continue;
                    }
                    if (ids.Contains(entry.id))
                    {
                        continue;
                    }
                    if (Assets.TryGetPrefab(new Tag(entry.id)) == null)
                    {
                        continue;
                    }
                    ids.Add(entry.id);
                }
                if (ids.Count == 0)
                {
                    return null;
                }
                MFLog.Verbose(LogCat.Event,"[Space] 星际遗失物流箱：本 Mod 可抽取饮品 " + ids.Count + " 种");
                return ids[UnityEngine.Random.Range(0, ids.Count)];
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 取本 Mod 饮品清单失败");
                return null;
            }
        }

        private static string PickArtifactPrefab()
        {
            try
            {
                List<GameObject> list = Assets.GetPrefabsWithTag(new Tag("Artifact"));
                if (list == null || list.Count == 0)
                {
                    return null;
                }
                GameObject go = list[UnityEngine.Random.Range(0, list.Count)];
                return PrefabIdOf(go);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void PirateWreck(IEventContext inst)
        {
            try
            {
                string[][] variants = new string[][]
                {
                    new string[] { "pirate_wreck", "海盗船坠毁" },
                    new string[] { "battle_debris", "战斗残骸" },
                    new string[] { "war_ruins", "星际大战遗址" }
                };
                string[] pick = variants[UnityEngine.Random.Range(0, variants.Length)];
                List<string> items = new List<string>();
                List<float> masses = new List<float>();
                List<int> counts = new List<int>();
                string[] normal =
                {
                    "ModuleItem_weapon_small", "ModuleItem_module_penetrator_1",
                    "ModuleItem_module_speed_1", "ModuleItem_module_radar_1", "ModuleItem_module_repair_1"
                };
                string[] master =
                {
                    "ModuleItem_weapon_medium", "ModuleItem_module_penetrator_2",
                    "ModuleItem_module_speed_2", "ModuleItem_module_radar_2", "ModuleItem_module_repair_2"
                };
                string[] legendary =
                {
                    "ModuleItem_weapon_large", "ModuleItem_module_penetrator_3",
                    "ModuleItem_module_speed_3", "ModuleItem_module_radar_3", "ModuleItem_module_repair_3"
                };
                int moduleSlots = UnityEngine.Random.Range(1, 3);
                int rareRoom = moduleSlots - 1;
                int rareTaken = 0;
                if (UnityEngine.Random.value < 0.15f && rareRoom > 0)
                {
                    AddLoot(items, masses, counts, master[UnityEngine.Random.Range(0, master.Length)], 1f,
                        UnityEngine.Random.Range(1, 3));
                    rareRoom--;
                    rareTaken++;
                }
                if (UnityEngine.Random.value < 0.05f && rareRoom > 0)
                {
                    AddLoot(items, masses, counts, legendary[UnityEngine.Random.Range(0, legendary.Length)], 1f, 1);
                    rareRoom--;
                    rareTaken++;
                }
                List<string> modulePicks = PickDistinct(new List<string>(normal), moduleSlots - rareTaken);
                for (int i = 0; i < modulePicks.Count; i++)
                {
                    AddLoot(items, masses, counts, modulePicks[i], 1f, 1);
                }
                if (UnityEngine.Random.value < 0.05f)
                {
                    string[] tiers = MoreFood_KFC.ScrollItems.Tiers;
                    string scroll = MoreFood_KFC.ScrollItems.PrefabId(tiers[UnityEngine.Random.Range(0, tiers.Length)]);
                    AddLoot(items, masses, counts, scroll, 1f, 1);
                }

                string ammo = PickAmmo();
                if (!string.IsNullOrEmpty(ammo))
                {
                    AddLoot(items, masses, counts, ammo, AmmoMass(ammo), 1);
                }
                else
                {
                    MFLog.Warn(LogCat.Event,"[Space] 海盗船坠毁：没有可用弹药 prefab，跳过该项");
                }

                List<string> metalPicks = PickDistinct(PickTagged("RefinedMetal"), UnityEngine.Random.Range(1, 3));
                for (int i = 0; i < metalPicks.Count; i++)
                {
                    AddLoot(items, masses, counts, metalPicks[i], UnityEngine.Random.Range(1500f, 3000f), 1);
                }
                if (metalPicks.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 海盗船坠毁：没有可用精炼金属（RefinedMetal），跳过该项");
                }

                if (items.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 海盗船坠毁：没有可用奖励 prefab");
                    return;
                }
                LootFieldFactory.Drop(pick[0], pick[1], items, masses, counts);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 海盗船坠毁执行失败");
            }
        }

        // 打捞点工厂：一行创建星图打捞点（星图小行星带 + 星图跳转注册）。物流箱复用根通知 instanceId 作为跳转 id，不再发冗余的第二封「（打捞点）」通知
        internal static class LootFieldFactory
        {
            private static int lootNotifySeq;

            // jumpId 非空 ⇒ 用该 id 注册星图跳转（物流箱传事件 instanceId，使根通知点击即跳星图）；为空则自动生成并附「（打捞点）」通知
            internal static bool Drop(string tag, string label, List<string> items, List<float> masses, List<int> counts, string jumpId = null)
            {
                try
                {
                    if (items == null || items.Count == 0)
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 打捞点：" + label + " 没有可发放的奖励，已跳过");
                        return false;
                    }
                    AxialI planet = SpaceRealEventRuntime.HomePlanetCell();
                    AxialI cell = SpaceRealEventRuntime.FindFreeCellNear(planet, 3, 9);
                    if (!SpaceRealEventRuntime.CellOk(cell))
                    {
                        cell = SpaceRealEventRuntime.FindFreeCellNear(SpaceCfg.Mk(0, 0), 2, 12);
                    }
                    if (!SpaceRealEventRuntime.CellOk(cell))
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 打捞点：" + label + " 找不到星图空位");
                        return false;
                    }
                    BeltKind kind = tag == "lost_logistics" ? BeltKind.LostLogistics
                        : (tag == "battle_debris" ? BeltKind.BattleDebris
                        : (tag == "war_ruins" ? BeltKind.WarRuins : BeltKind.PirateWreck));
                    BeltStyle style = AsteroidBeltFactory.RollStyle(kind);
                    BeltBuilder builder = AsteroidBeltFactory.Create(label, style);
                    int added = AsteroidBeltFactory.Feed(builder, items, masses, counts);
                    if (added == 0)
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 打捞点：" + label + " 奖励清单无效（全部为 0 质量），已跳过");
                        return false;
                    }
                    SpaceAsteroidBelt belt = builder.At(cell);
                    if (belt == null)
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 打捞点：" + label + " 生成失败（见上一条日志）");
                        return false;
                    }
                    string useJumpId = string.IsNullOrEmpty(jumpId) ? "space_lootfield_" + tag + "_" + (++lootNotifySeq) : jumpId;
                    StarmapLocator.Register(useJumpId, belt.Location);
                    // 物流箱：根通知已说明货箱位置，不再发冗余的第二封通知，跳转复用根通知
                    if (tag != "lost_logistics")
                    {
                        TradeUi.Notify(label + "（打捞点）", "星图上出现一处可打捞的残骸，派火箭抵达即可回收物资。", useJumpId, "blue");
                    }
                    return true;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Event, e, "[Space] 打捞点生成失败（" + label + "）");
                    return false;
                }
            }
        }

        private static void AddLoot(List<string> items, List<float> masses, List<int> counts,
            string id, float mass, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0)
            {
                return;
            }
            GameObject prefab = Assets.TryGetPrefab(new Tag(id));
            if (prefab == null)
            {
                MFLog.Warn(LogCat.Event,"[Space] 打捞点奖励物品不存在，已跳过：" + id);
                return;
            }
            if (prefab.GetComponent<Pickupable>() == null || prefab.GetComponent<Building>() != null)
            {
                MFLog.Warn(LogCat.Event,"[Space] 打捞点奖励不是可拾取物品，已跳过：" + id);
                return;
            }
            items.Add(id);
            masses.Add(mass);
            counts.Add(count);
        }
    }
}
