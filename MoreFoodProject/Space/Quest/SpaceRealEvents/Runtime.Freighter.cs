// 用途：货船运行：航次、到站、残骸与临时空间站清扫

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
    internal static partial class SpaceRealEventRuntime
    {
        internal static void AddRun(RealFreighterRun run)
        {
            if (run != null)
            {
                runs.Add(run);
            }
        }

        private static void KillRun(RealFreighterRun run, string why)
        {
            if (run == null)
            {
                return;
            }
            try
            {
                if (run.station != null)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 太空货船离场销毁 " + run.stationId + "（" + why + "）");
                    Util.KDestroyGameObject(run.station.gameObject);
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 太空货船销毁失败");
            }
            run.station = null;
        }

        private static void TickFreighters()
        {
            try
            {
            if (runs.Count == 0)
            {
                return;
            }
            float now = Now();
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                if (i >= runs.Count)
                {
                    continue;
                }
                RealFreighterRun run = runs[i];
                if (run == null || run.station == null)
                {
                    runs.RemoveAt(i);
                    continue;
                }
                if (run.hardStopAt > 0f && now >= run.hardStopAt)
                {
                    KillRun(run, "超时");
                    runs.RemoveAt(i);
                    continue;
                }
                if (run.phase == 0)
                {
                    if (now < run.nextStepAt)
                    {
                        continue;
                    }
                    if (SpaceCfg.Distance(run.station.Location, run.target) <= 0)
                    {
                        run.phase = 1;
                        run.dockUntil = now + run.dockUntil;
                        run.nextStepAt = 0f;
                        try
                        {
                            ClusterDestinationSelector sel = run.station.GetComponent<ClusterDestinationSelector>();
                            if (sel != null)
                            {
                                sel.SetDestination(run.station.Location);
                            }
                        }
                        catch (Exception)
                        {
                            // expected: 目标选择器缺失或设置失败，货船按坐标继续停靠流程
                        }
                        MFLog.Verbose(LogCat.Event,"[Space] ★ 太空货船已停靠 @" + SpaceCfg.Pos(run.station.Location)
                            + "，停靠 " + Mathf.Round(run.dockUntil - now).ToString("0") + " 秒");
                        try
                        {
                            TradeUi.Notify("太空货船", "货船已停靠在你的星球轨道，火箭开到相邻一格即可交易", null, "blue");
                        }
                        catch (Exception)
                        {
                            // expected: 停靠提示属可选 UI，通知失败不影响货船运行
                        }
                        continue;
                    }
                    if (run.station.GetComponent<ClusterTraveler>() != null)
                    {
                        continue;
                    }
                    int dir = StepToward(run.station.Location, run.target);
                    if (dir < 0)
                    {
                        run.nextStepAt = now + 2f;
                        continue;
                    }
                    AxialI next = SpaceCfg.Neighbor(run.station.Location, dir);
                    try
                    {
                        run.station.Location = next;
                        run.steps++;
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Event, e, "[Space] 太空货船移动失败");
                    }
                    run.nextStepAt = now + 2f;
                }
                else if (run.phase == 1)
                {
                    if (now < run.dockUntil)
                    {
                        continue;
                    }
                    run.phase = 2;
                    run.nextStepAt = now;
                    run.exit = FindEdgeCell();
                    try
                    {
                        ClusterDestinationSelector sel = run.station.GetComponent<ClusterDestinationSelector>();
                        if (sel != null && CellOk(run.exit))
                        {
                            sel.SetDestination(run.exit);
                        }
                    }
                    catch (Exception)
                    {
                        // expected: 目标选择器缺失或设置失败，货船按坐标继续飞离
                    }
                    MFLog.Verbose(LogCat.Event,"[Space] 太空货船停靠结束，开始飞离 @" + SpaceCfg.Pos(run.station.Location));
                    try
                    {
                        TradeUi.Notify("太空货船", "货船已完成补给，正在飞往星图边缘", null, "blue");
                    }
                    catch (Exception)
                    {
                        // expected: 飞离提示属可选 UI，通知失败不影响货船运行
                    }
                }
                else
                {
                    if (now < run.nextStepAt)
                    {
                        continue;
                    }
                    if (!CellOk(run.exit) || SpaceCfg.Distance(run.station.Location, run.exit) <= 0)
                    {
                        KillRun(run, "抵达星图边缘");
                        runs.RemoveAt(i);
                        continue;
                    }
                    if (run.station.GetComponent<ClusterTraveler>() != null)
                    {
                        continue;
                    }
                    int dir = StepToward(run.station.Location, run.exit);
                    if (dir < 0)
                    {
                        KillRun(run, "无路可走");
                        runs.RemoveAt(i);
                        continue;
                    }
                    try
                    {
                        run.station.Location = SpaceCfg.Neighbor(run.station.Location, dir);
                    }
                    catch (Exception e)
                    {
                        MFLog.Verbose(LogCat.Event, e, "[Space] 太空货船离场移动失败");
                    }
                    run.nextStepAt = now + 2f;
                }
            }
            }
            catch (System.Exception e) { MFLog.HotFail("tick:SpaceRealEvents", e); }
        }

        private static void ProbeImpactorBySecond()
        {
            int second = Mathf.RoundToInt(Now());
            if (second == lastProbeSecond)
            {
                return;
            }
            lastProbeSecond = second;
            SpaceRealEvents.ProbeImpactor();
        }

        internal static bool IsFreighterCell(AxialI cell)
        {
            for (int i = 0; i < runs.Count; i++)
            {
                RealFreighterRun run = runs[i];
                if (run != null && run.station != null && run.station.Location == cell)
                {
                    return true;
                }
            }
            for (int i = 0; i < buffs.Count; i++)
            {
                RealBuff b = buffs[i];
                if (b == null)
                {
                    continue;
                }
                for (int k = 0; k < b.stations.Count; k++)
                {
                    SpaceStation st = b.stations[k];
                    if (st == null || st.Location != cell || string.IsNullOrEmpty(st.stationId))
                    {
                        continue;
                    }
                    if (st.stationId.StartsWith(SpaceRealEvents.TempStationPrefix, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void SweepTempStationsNow()
        {
            tempSwept = false;
            SweepOrphanTempStations();
        }

        private static void SweepOrphanTempStations()
        {
            if (tempSwept)
            {
                return;
            }
            tempSwept = true;
            try
            {
                SpaceStation[] all = Resources.FindObjectsOfTypeAll<SpaceStation>();
                int killed = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    SpaceStation st = all[i];
                    if (st == null || st.gameObject == null || !st.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    string id = st.stationId;
                    if (string.IsNullOrEmpty(id)
                        || !id.StartsWith(SpaceRealEvents.TempStationPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    bool live = false;
                    for (int k = 0; k < runs.Count && !live; k++)
                    {
                        if (runs[k] != null && runs[k].station == st)
                        {
                            live = true;
                        }
                    }
                    for (int k = 0; k < buffs.Count && !live; k++)
                    {
                        if (buffs[k] != null && buffs[k].stations.Contains(st))
                        {
                            live = true;
                        }
                    }
                    if (live)
                    {
                        continue;
                    }
                    MFLog.Verbose(LogCat.Event,"[Space] 清理上一会话遗留的临时站点 " + id);
                    Util.KDestroyGameObject(st.gameObject);
                    killed++;
                }
                if (killed > 0)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 临时站点清理完成：" + killed + " 个");
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 临时站点清理异常");
            }
        }
    }
}
