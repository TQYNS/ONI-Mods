// 用途：临时空间站：生成、补货与星图生物

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
        private static SpaceStation SpawnTempStation(string idPrefix, string label, float buffSeconds)
        {
            try
            {
                AxialI planet = SpaceRealEventRuntime.HomePlanetCell();
                AxialI cell = SpaceRealEventRuntime.FindFreeCellNear(planet, 3, 9);
                if (!SpaceRealEventRuntime.CellOk(cell))
                {
                    MFLog.Warn(LogCat.Event,"[Space] 临时站点：星图上找不到空位");
                    return null;
                }
                string id = idPrefix + "_" + UnityEngine.Random.Range(1000, 9999);
                SpaceStation st = SpawnTempStationAt(cell, id, label, buffSeconds, false);
                if (st == null)
                {
                    return null;
                }
                SpaceReveal.Reveal(cell);
                StarMapVisuals.RefreshNow();
                RealBuff b = SpaceRealEventRuntime.BeginBuff(id, label, "tempstation", buffSeconds, 0f);
                b.stations.Add(st);
                return st;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 临时站点生成失败（选点建站）");
                return null;
            }
        }

        private static SpaceStation SpawnTempStationAt(AxialI cell, string id, string label, float seconds,
            bool asShip)
        {
            try
            {
                SpaceTestRuntime.RegisterPrefabs();
                GameObject prefab = Assets.TryGetPrefab(new Tag(SpaceCfg.StationPrefabId));
                if (prefab == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 临时站点：prefab " + SpaceCfg.StationPrefabId + " 不存在");
                    return null;
                }
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    return null;
                }
                SpaceStation st = go.GetComponent<SpaceStation>();
                if (st == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 临时站点缺少 SpaceStation 组件");
                    return null;
                }
                st.stationId = id;
                st.displayName = label;
                st.kind = "main";
                st.isShip = asShip;
                if (asShip)
                {
                    ClusterDestinationSelector selector = go.AddOrGet<ClusterDestinationSelector>();
                    selector.assignable = false;
                    selector.canNavigateFogOfWar = true;
                    go.AddOrGet<ClusterTraveler>();
                }
                if (asShip)
                {
                    st.animFile = "rocket01_kanim";
                    st.animState = "idle_loop";
                }
                ClusterGridEntity entity = go.GetComponent<ClusterGridEntity>();
                if (entity == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 临时站点缺少 ClusterGridEntity");
                    return null;
                }
                entity.Location = cell;
                go.SetActive(true);
                st.ApplyIdentity();
                FillStock(id, label, seconds, !asShip);
                MFLog.Verbose(LogCat.Event,"[Space] 临时站点已生成 " + id + "（" + label + "）@" + SpaceCfg.Pos(cell));
                return st;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 临时站点生成失败（实例化建站）");
                return null;
            }
        }

        private static void FillStock(string stationId, string label, float seconds, bool rich)
        {
            try
            {
                StationDef def = TradeDb.FindStation(stationId);
                if (def == null)
                {
                    def = new StationDef();
                    def.id = stationId;
                    def.name = label;
                    def.k = 0.2f;
                }
                StationCatalog cat = TradeCatalog.Catalog(def);
                TradeStationState st = TradeRegistry.EnsureState(stationId);
                st.shelf.Clear();
                if (cat == null || cat.sellList.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 临时站点：可卖池为空（贸易配置未就绪），货架留空");
                }
                else
                {
                    int want = rich ? UnityEngine.Random.Range(30, 61) : UnityEngine.Random.Range(24, 39);
                    int legendaryWant = rich ? UnityEngine.Random.Range(3, 6) : 0;
                    List<TradeItemValue> pool = new List<TradeItemValue>(cat.sellList);
                    HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
                    if (rich)
                    {
                        List<TradeItemValue> legend;
                        if (cat.sellByRarity.TryGetValue(TradeRarity.Legendary, out legend) && legend != null)
                        {
                            List<TradeItemValue> legPool = new List<TradeItemValue>(legend);
                            for (int i = 0; i < legendaryWant && legPool.Count > 0; i++)
                            {
                                int idx = UnityEngine.Random.Range(0, legPool.Count);
                                TradeItemValue v = legPool[idx];
                                legPool.RemoveAt(idx);
                                if (v == null || string.IsNullOrEmpty(v.id) || used.Contains(v.id))
                                {
                                    continue;
                                }
                                AddStockEntry(st, v);
                                used.Add(v.id);
                            }
                        }
                        string scroll = PickGuaranteedScroll(cat);
                        if (!string.IsNullOrEmpty(scroll) && used.Add(scroll))
                        {
                            TradeItemValue sv = FindSell(cat, scroll);
                            if (sv != null)
                            {
                                AddStockEntry(st, sv);
                            }
                        }
                    }
                    for (int i = 0; i < want && pool.Count > 0 && st.shelf.Count < want; i++)
                    {
                        int idx = UnityEngine.Random.Range(0, pool.Count);
                        TradeItemValue v = pool[idx];
                        pool.RemoveAt(idx);
                        if (v == null || string.IsNullOrEmpty(v.id))
                        {
                            continue;
                        }
                        if (used.Contains(v.id))
                        {
                            continue;
                        }
                        used.Add(v.id);
                        AddStockEntry(st, v);
                    }
                }
                st.currency = (rich ? UnityEngine.Random.Range(6000f, 9000f)
                    : UnityEngine.Random.Range(3000f, 5000f)) * ModOptions.Current.TraderCreditsMultiplier;
                st.cooldownEnd = SpaceRealEventRuntime.Now() + Mathf.Max(60f, seconds) + 600f;
                MFLog.Verbose(LogCat.Event,"[Space] 临时站点货架就绪：" + st.shelf.Count + " 件商品，星币 "
                    + st.currency.ToString("0") + "（" + stationId + (rich ? "，富集传奇/卷轴" : "") + "）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 临时站点装货失败");
            }
        }

        private static void AddStockEntry(TradeStationState st, TradeItemValue v)
        {
            TradeStockEntry e = new TradeStockEntry();
            e.id = v.id;
            e.value = v;
            float lo = Mathf.Max(1f, v.stockMin);
            float hi = Mathf.Max(lo + 1f, v.stockMax);
            e.stock = Mathf.Max(1f, Mathf.Round(UnityEngine.Random.Range(lo, hi)
                * ModOptions.Current.StockMultiplier));
            st.shelf.Add(e);
        }

        private static TradeItemValue FindSell(StationCatalog cat, string id)
        {
            if (cat == null || string.IsNullOrEmpty(id))
            {
                return null;
            }
            TradeItemValue v;
            return cat.sell.TryGetValue(id, out v) ? v : null;
        }

        private static string PickGuaranteedScroll(StationCatalog cat)
        {
            if (cat == null || cat.sell == null)
            {
                return null;
            }
            string[] ids = { "SpaceScrollLegendary", "SpaceScrollGood", "SpaceScrollCommon" };
            for (int i = 0; i < ids.Length; i++)
            {
                if (cat.sell.ContainsKey(ids[i]))
                {
                    return ids[i];
                }
            }
            return null;
        }

        private static void SpawnStarmapCreature(string tag)
        {
            try
            {
                AxialI planet = SpaceRealEventRuntime.HomePlanetCell();
                AxialI cell = SpaceRealEventRuntime.FindFreeCellNear(planet, 2, 8);
                if (!SpaceRealEventRuntime.CellOk(cell))
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星图生物：找不到空位");
                    return;
                }
                if (CombatDb.Monsters.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星图生物：怪物配置为空");
                    return;
                }
                CombatRuntime.Init();
                CombatMonsterDef def = CombatDb.Monsters[UnityEngine.Random.Range(0, CombatDb.Monsters.Count)];
                string defId = def.id;
                if (CombatRuntime.SpawnMonster(defId, cell, tag, "main") == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 星图生物生成返回空（" + defId + "）");
                    return;
                }
                SpaceReveal.Reveal(cell);
                StarMapVisuals.RefreshNow();
                MFLog.Verbose(LogCat.Event,"[Space] ★ 星图生物已生成 " + defId + " @" + SpaceCfg.Pos(cell));
                TradeUi.Notify("星图生物", "一只"
                    + (string.IsNullOrEmpty(def.name) ? "未知生物" : def.name)
                    + "出现在星图上，正在附近空域游荡。");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 星图生物生成失败");
            }
        }
    }
}
