// 用途：间歇泉事件：收集、生成、强化与喷发

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
        // 取源判定改由 IsGasSpawnable（按元素而非名称）完成

        // 判断未生成泉的元素是否为气体：读 prefab 的 MakeConfiguration，无需实例化
        private static bool IsGasSpawnable(string id)
        {
            GameObject prefab = Assets.GetPrefab(new Tag(id));
            GeyserConfigurator cfg = prefab != null ? prefab.GetComponent<GeyserConfigurator>() : null;
            if (cfg == null)
            {
                return false;
            }
            var conf = cfg.MakeConfiguration();
            if (conf == null)
            {
                return false;
            }
            Element e = ElementLoader.FindElementByHash(conf.GetElement());
            return e != null && e.IsGas;
        }

        // 选泉优先级逻辑已并入枚举阶段，不再需要单独排名

        // 间歇泉条目：同一个泉在世界里要么已实例化（live），要么仍在迷雾中未生成（spawn）
        private struct GeyserEntry
        {
            public int cell;
            public Geyser live;
            public WorldGenSpawner.Spawnable spawn;
        }

        // 单泉效果委托（返回是否生效，用于计数）
        private delegate bool GeyserEffect(Geyser g);

        // 枚举本星全部间歇泉（含迷雾未生成），按真实格位去重
        private static List<GeyserEntry> CollectGeysers(int worldId)
        {
            List<GeyserEntry> entries = new List<GeyserEntry>();
            HashSet<int> seen = new HashSet<int>();
            try
            {
                var registry = Components.Geysers;
                if (registry != null)
                {
                    foreach (int wid in registry.GetWorldsIds())
                    {
                        List<Geyser> items = registry.GetItems(wid);
                        if (items == null)
                        {
                            continue;
                        }
                        for (int i = 0; i < items.Count; i++)
                        {
                            Geyser g = items[i];
                            if (g == null)
                            {
                                continue;
                            }
                            int c = Grid.PosToCell(g);
                            if (!Grid.IsValidCell(c))
                            {
                                continue;
                            }
                            if (worldId >= 0 && Grid.WorldIdx[c] != worldId)
                            {
                                continue;
                            }
                            if (!seen.Add(c))
                            {
                                continue;
                            }
                            entries.Add(new GeyserEntry { cell = c, live = g, spawn = null });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 读取已生成间歇泉失败");
            }
            try
            {
                SaveGame sg = SaveGame.Instance;
                WorldGenSpawner spawner = sg != null ? sg.worldGenSpawner : null;
                if (spawner != null)
                {
                    List<WorldGenSpawner.Spawnable> unspawned = spawner.GeInfoOfUnspawnedWithType<Geyser>(worldId);
                    if (unspawned != null)
                    {
                        for (int i = 0; i < unspawned.Count; i++)
                        {
                            WorldGenSpawner.Spawnable s = unspawned[i];
                            if (s == null || s.spawnInfo == null)
                            {
                                continue;
                            }
                            int c = Grid.OffsetCell(0, s.spawnInfo.location_x, s.spawnInfo.location_y);
                            if (!Grid.IsValidCell(c))
                            {
                                continue;
                            }
                            if (!seen.Add(c))
                            {
                                continue;
                            }
                            entries.Add(new GeyserEntry { cell = c, live = null, spawn = s });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 读取未生成间歇泉失败");
            }
            int liveN = 0;
            int fogN = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].live != null) liveN++; else fogN++;
            }
            MFLog.Verbose(LogCat.Event,"[Space] 间歇泉枚举：worldId=" + worldId + " 共 " + entries.Count + " 个（已生成 " + liveN + " / 迷雾未生成 " + fogN + "）");
            return entries;
        }

        // 强制生成（TrySpawn 只查 world.IsDiscovered + prefab 存在，绕开 Grid.Spawnable 闸门）+ 揭示该泉所在迷雾
        private static bool SpawnAndRevealGeyser(GeyserEntry e)
        {
            if (e.spawn == null)
            {
                return false;
            }
            WorldGenSpawner.Spawnable s = e.spawn;
            string id = s.spawnInfo.id;
            MFLog.Verbose(LogCat.Event,"[Space] 强制生成+揭示间歇泉 " + id + " cell=" + s.cell + " real=" + e.cell
                + " loc=" + s.spawnInfo.location_x + "," + s.spawnInfo.location_y);
            s.TrySpawn();
            MFLog.Verbose(LogCat.Event,"[Space] 间歇泉 TrySpawn 后 isSpawned=" + s.isSpawned + " id=" + id);
            if (Grid.IsValidCell(e.cell))
            {
                Vector2I above = Grid.CellToXY(Grid.OffsetCell(e.cell, 0, 2));
                GridVisibility.Reveal(above.x, above.y, 6, 1f);
                Grid.Reveal(e.cell, 255, true);
                SimMessages.Dig(e.cell, -1, false, false);
            }
            return true;
        }

        // 按格位在场景中反查已实例化的间歇泉（TrySpawn 后 Geyser.OnSpawn 注册进 Components.Geysers 约延迟一帧）
        private static Geyser FindGeyserAtCell(int cell)
        {
            if (!Grid.IsValidCell(cell))
            {
                return null;
            }
            var registry = Components.Geysers;
            if (registry == null)
            {
                return null;
            }
            foreach (int wid in registry.GetWorldsIds())
            {
                List<Geyser> items = registry.GetItems(wid);
                if (items == null)
                {
                    continue;
                }
                for (int i = 0; i < items.Count; i++)
                {
                    Geyser g = items[i];
                    if (g != null && Grid.PosToCell(g) == cell)
                    {
                        return g;
                    }
                }
            }
            return null;
        }

        // 对迷雾中未生成的泉：先生成+揭示，延迟一帧后按格位取回真实实例并施加效果
        private static void ScheduleApplyGeyserEffect(IEventContext inst, List<GeyserEntry> pending, GeyserEffect effect, string label)
        {
            if (pending == null || pending.Count == 0 || GameScheduler.Instance == null || effect == null)
            {
                return;
            }
            List<int> cells = new List<int>();
            for (int i = 0; i < pending.Count; i++)
            {
                cells.Add(pending[i].cell);
            }
            IEventContext instRef = inst;
            string key = "SpaceGeyserFx_" + label;
            GameScheduler.Instance.Schedule(key, 0.5f, delegate(object o)
            {
                try
                {
                    int hit = 0;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        Geyser g = FindGeyserAtCell(cells[i]);
                        if (g != null && effect(g))
                        {
                            hit++;
                        }
                        else if (g == null)
                        {
                            MFLog.Warn(LogCat.Event,"[Space] " + label + " 延迟取泉失败 cell=" + cells[i]);
                        }
                    }
                    MFLog.Verbose(LogCat.Event,"[Space] ★ " + label + " 延迟生效：命中=" + hit + "/" + cells.Count);
                }
                catch (Exception ex)
                {
                    MFLog.Verbose(LogCat.Event, ex, "[Space] " + label + " 延迟生效失败");
                }
            });
        }

        // 地质异常 30% 分支：单个泉产量提升
        private static void BoostGeyser(Geyser g, IEventContext inst)
        {
            if (g == null)
            {
                return;
            }
            float pct = UnityEngine.Random.Range(0.15f, 0.30f);
            Geyser.GeyserModification mod = new Geyser.GeyserModification();
            mod.originID = "SpaceEventGeological";
            mod.massPerCycleModifier = pct;
            g.AddModification(mod);
            MFLog.Verbose(LogCat.Event,"[Space] ★ 地质异常（30% 分支）：" + g.GetProperName() + " 产量 +" + Mathf.Round(pct * 100f)
                + "%（massModificationMethod=" + Geyser.massModificationMethod + "）");
            if (inst != null)
            {
                // 走分支变体：yaml notification.variants.boost
                inst.SetNoticeVariant("boost");
                inst.SetNoticeArg("target", g.GetProperName());
                inst.SetNoticeArg("pct", "+" + Mathf.Round(pct * 100f) + "%");
            }
        }

        private static bool IsGasGeyser(Geyser g)
        {
            Element e = ElementLoader.FindElementByHash(GeyserElement(g));
            return e != null && e.IsGas;
        }

        private static int CountGasGeysers(List<Geyser> all)
        {
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsGasGeyser(all[i]))
                {
                    n++;
                }
            }
            return n;
        }

        private static List<Geyser> WorldGeysers()
        {
            List<Geyser> list = new List<Geyser>();
            try
            {
                ClusterManager cm = ClusterManager.Instance;
                if (cm == null)
                {
                    return list;
                }
                int worldId = SpaceRealEventRuntime.MainWorldId();
                if (worldId < 0) worldId = cm.activeWorldId;

                // ① 从各世界层级内取（含非活跃实例；以 WorldContainer 为根 ⇒ 天然排除 prefab 模板，避免 typeId=0x0 崩）
                HashSet<Geyser> seen = new HashSet<Geyser>();
                int worldCount = 0;
                if (cm.WorldContainers != null)
                {
                    for (int i = 0; i < cm.WorldContainers.Count; i++)
                    {
                        WorldContainer wc = cm.WorldContainers[i];
                        if (wc == null || wc.gameObject == null) continue;
                        Geyser[] found = wc.gameObject.GetComponentsInChildren<Geyser>(true);
                        if (found == null) continue;
                        for (int k = 0; k < found.Length; k++)
                        {
                            Geyser g = found[k];
                            if (g != null && seen.Add(g)) { list.Add(g); worldCount++; }
                        }
                    }
                }

                // ② 全量兜底（含 prefab 模板；后面的 ValidGeyser 会剔除 typeId=0x0 的模板，不会崩）
                UnityEngine.Object[] raw = Resources.FindObjectsOfTypeAll(typeof(Geyser));
                int rawCount = raw != null ? raw.Length : 0;
                for (int i = 0; i < rawCount; i++)
                {
                    Geyser g = raw[i] as Geyser;
                    if (g != null && seen.Add(g)) list.Add(g);
                }

                // ③ 原版登记表兜底（CmpsByWorld：含 worldId 归属可疑、场景扫描漏掉的对象）
                int regCount = 0;
                try
                {
                    var registry = Components.Geysers;
                    if (registry != null)
                    {
                        foreach (int wid in registry.GetWorldsIds())
                        {
                            List<Geyser> items = registry.GetItems(wid);
                            if (items == null) continue;
                            for (int i = 0; i < items.Count; i++)
                            {
                                Geyser g = items[i];
                                if (g != null && seen.Add(g)) { list.Add(g); regCount++; }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    MFLog.Verbose(LogCat.Event, e, "[Space] 间歇泉登记表读取失败");
                }

                // ③ 剔除模板/未配置对象（typeId=0x0 会让原版 FindType 直接 LogError ⇒ 崩），再按【真实格位所属世界】过滤
                List<Geyser> hit = new List<Geyser>();
                int invalid = 0;
                int skippedCell = 0;
                int skippedWorld = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    Geyser g = list[i];
                    if (g == null) continue;
                    int cell = Grid.PosToCell(g);
                    bool valid = ValidGeyser(g);
                    bool cellOk = Grid.IsValidCell(cell);
                    int idx = cellOk ? Grid.WorldIdx[cell] : -1;
                    if (valid || i < 12)
                    {
                        MFLog.Verbose(LogCat.Event,"[Space] 泉候选#" + i + " name=" + g.gameObject.name + " valid=" + valid
                            + " cell=" + cell + " worldIdx=" + idx + " myWorld=" + g.GetMyWorldId()
                            + " active=" + g.gameObject.activeInHierarchy + " 位置=" + g.transform.position
                            + " 父=" + (g.transform.parent != null ? g.transform.parent.name : "-"));
                    }
                    if (!valid)
                    {
                        invalid++;
                        continue;
                    }
                    if (!cellOk)
                    {
                        skippedCell++;
                        continue;
                    }
                    if (worldId >= 0 && idx != worldId)
                    {
                        skippedWorld++;
                        MFLog.Verbose(LogCat.Event,"[Space] 泉候选#" + i + " 因世界不符被剔除：worldIdx=" + idx
                            + " ≠ worldId=" + worldId + " name=" + g.gameObject.name);
                        continue;
                    }
                    hit.Add(g);
                }
                MFLog.Verbose(LogCat.Event,"[Space] 间歇泉扫描：worldId=" + worldId + " activeWorld=" + cm.activeWorldId
                          + " 世界=" + worldCount + " 全部=" + rawCount + " 登记=" + regCount
                          + " 无效=" + invalid + " 非本世界=" + skippedWorld + " 格无效=" + skippedCell
                          + " 命中=" + hit.Count);
                return hit;
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 取间歇泉列表失败");
            }
            return list;
        }

        // 模板/未配置的 Geyser（configuration.typeId = 0x0）：一旦读 geyserType 会触发原版 FindType 的 LogError，LogCatcher 视为致命 ⇒ 崩
        private static bool ValidGeyser(Geyser g)
        {
            if (g == null || g.configuration == null) return false;
            return g.configuration.typeId != HashedString.Invalid;
        }

        private static SimHashes GeyserElement(Geyser g)
        {
            if (!ValidGeyser(g))
            {
                return SimHashes.Vacuum;
            }
            return g.configuration.GetElement();
        }

        private static bool ForceErupt(Geyser g)
        {
            if (g == null || !ValidGeyser(g))
            {
                return false;
            }
            try
            {
                float shift = g.timeShift;
                if (g.RemainingActiveTime() <= 0f)
                {
                    shift += g.RemainingDormantTime() + 1f;
                    g.AlterTime(shift, true);
                }
                float nonErupt = g.RemainingNonEruptTime();
                if (nonErupt > 0f)
                {
                    g.AlterTime(shift + nonErupt + 1f, true);
                }
                bool erupting = g.RemainingEruptTime() > 0f;
                MFLog.Verbose(LogCat.Event,"[Space] 间歇泉强制喷发 " + g.GetProperName() + " 元素=" + GeyserElement(g)
                    + " 喷发中=" + erupting);
                return erupting;
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 间歇泉强制喷发失败");
                return false;
            }
        }
    }
}
