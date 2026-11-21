// 用途：叙事类事件：驯化、顿悟、广播、货运与科技

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
        private static void AnimalTaming(IEventContext inst)
        {
            try
            {
                List<GameObject> wild = new List<GameObject>();
                List<string> species = new List<string>();
                if (Components.Brains != null)
                {
                    foreach (Brain brain in Components.Brains)
                    {
                        if (brain == null)
                        {
                            continue;
                        }
                        GameObject go = brain.gameObject;
                        if (go == null || !SpaceRealEventRuntime.IsInActiveWorld(go))
                        {
                            continue;
                        }
                        AmountInstance w = Db.Get().Amounts.Wildness.Lookup(go);
                        if (w == null || w.value <= 0f)
                        {
                            continue;
                        }
                        string pid = PrefabIdOf(go);
                        if (string.IsNullOrEmpty(pid))
                        {
                            continue;
                        }
                        wild.Add(go);
                        if (!species.Contains(pid))
                        {
                            species.Add(pid);
                        }
                    }
                }
                if (wild.Count == 0 || species.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 动物驯化：本星球没有野生生物（前置条件本应拦下）");
                    return;
                }
                string pick = species[UnityEngine.Random.Range(0, species.Count)];
                List<GameObject> same = new List<GameObject>();
                for (int i = 0; i < wild.Count; i++)
                {
                    if (PrefabIdOf(wild[i]) == pick)
                    {
                        same.Add(wild[i]);
                    }
                }
                int want = UnityEngine.Random.Range(1, 4);
                int tamed = 0;
                List<GameObject> tamedGos = new List<GameObject>();
                for (int i = 0; i < same.Count && tamed < want; i++)
                {
                    AmountInstance w = Db.Get().Amounts.Wildness.Lookup(same[i]);
                    if (w == null)
                    {
                        continue;
                    }
                    w.SetValue(0f);
                    tamedGos.Add(same[i]);
                    WildnessMonitor.Instance wm = same[i].GetSMI<WildnessMonitor.Instance>();
                    if (wm != null)
                    {
                        wm.DebugTame();
                    }
                    else
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 动物驯化：目标缺少 WildnessMonitor，只能把野性归零");
                    }
                    tamed++;
                }
                string pickName = ProperNameOf(pick);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 动物驯化：" + pickName + "（" + pick + "）驯化 " + tamed
                    + " 只（野生候选 " + same.Count + " 只）");
                // 一行启用事件定位器（点击通知/历史 → 跳转目标）：只交实体编号，框架不碰 GameObject
                {
                    var ids = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < tamedGos.Count; i++)
                    {
                        var go = tamedGos[i];
                        if (go == null) continue;
                        var kpid = go.GetComponent<KPrefabID>();
                        if (kpid != null) ids.Add(kpid.InstanceID);
                    }
                    EventFrameworkApi.BindTargets(inst != null ? inst.InstanceId : null, 1, ids.ToArray());
                }
                inst.SetNoticeArg("target", pickName);
                inst.SetNoticeArg("count", tamed.ToString());
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 动物驯化执行失败");
            }
        }

        private static void Epiphany(IEventContext inst)
        {
            try
            {
                List<GameObject> dupes = LiveDupes(true);
                if (dupes.Count == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 醍醐灌顶：没有可用的复制人（仿生人已排除；前置条件本应拦下）");
                    return;
                }
                GameObject dupe = dupes[UnityEngine.Random.Range(0, dupes.Count)];
                MinionResume resume = dupe.GetComponent<MinionResume>();
                if (resume == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 醍醐灌顶：目标缺少 MinionResume");
                    return;
                }
                List<string> candidates = new List<string>();
                Database.Skills skills = Db.Get().Skills;
                for (int i = 0; i < skills.Count; i++)
                {
                    Database.Skill s = skills[i];
                    if (s == null || string.IsNullOrEmpty(s.Id))
                    {
                        continue;
                    }
                    if (resume.HasMasteredSkill(s.Id))
                    {
                        continue;
                    }
                    if (!resume.IsAbleToLearnSkill(s.Id))
                    {
                        continue;
                    }
                    candidates.Add(s.Id);
                }
                string who = dupe.GetProperName();
                if (candidates.Count == 0)
                {
                    resume.ForceAddSkillPoint();
                    MFLog.Verbose(LogCat.Event,"[Space] ★ 醍醐灌顶：" + who + " 已无可用技能 → 补 1 技能点（当前 "
                        + resume.AvailableSkillpoints + "）");
                    inst.SetNoticeArg("target", who);
                    inst.SetNoticeArg("skill", "全部技能");
                    return;
                }
                string skillId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                int before = resume.SkillsMastered;
                resume.MasterSkill(skillId);
                string groupName = SkillGroupNameOf(skillId);
                string skillName = string.IsNullOrEmpty(groupName) ? SkillDisplayName(skillId) : groupName;
                MFLog.Verbose(LogCat.Event,"[Space] ★ 醍醐灌顶：" + who + " 掌握技能 " + skillId
                    + "（" + skillName + "，已掌握 " + before + " → " + resume.SkillsMastered + " 级）");
                inst.SetNoticeArg("target", who);
                inst.SetNoticeArg("skill", skillName);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 醍醐灌顶执行失败");
            }
        }

        private static void AlienBroadcast(IEventContext inst)
        {
            try
            {
                MFLog.Verbose(LogCat.Event,"[Space] 外星广播事件已触发，等待玩家在面板选择【破译】或【漠视】");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 外星广播触发失败");
            }
        }

        private static void DecodeBroadcast(IEventContext inst)
        {
            try
            {
                float roll = UnityEngine.Random.value;
                float alienSeconds = UnityEngine.Random.Range(9000f, 18000f);
                if (roll < 0.7f)
                {
                    SpaceStation st = SpawnTempStation("evt_temp_alien", "未知信号中继站", alienSeconds);
                    if (st != null)
                    {
                        MFLog.Verbose(LogCat.Event,"[Space] ★ 外星广播·破译：揭示临时可交易空间站 @" + SpaceCfg.Pos(st.Location));
                        TradeUi.Notify("外星广播·破译", "坐标解密成功：星图上出现一座临时中继站（约 "
                            + (alienSeconds / 600f).ToString("0.0") + " 周期可交易）");
                    }
                    else
                    {
                        TradeUi.Notify("外星广播·破译", "坐标解密失败：星图上找不到可用的空位，信号就此中断");
                    }
                }
                else
                {
                    int before = 0;
                    if (CombatRuntime.Monsters != null)
                    {
                        before = CombatRuntime.Monsters.Count;
                    }
                    SpawnStarmapCreature("evt_temp_alien_beast");
                    if (CombatRuntime.Monsters == null || CombatRuntime.Monsters.Count <= before)
                    {
                        TradeUi.Notify("外星广播·破译", "坐标指向一只星图生物，但它没能现身，信号就此中断");
                    }
                }
                DecodeTechProgress();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 外星广播·破译执行失败");
            }
        }

        private static void IgnoreBroadcast(IEventContext inst)
        {
            try
            {
                MFLog.Verbose(LogCat.Event,"[Space] 外星广播·漠视：玩家选择无视信号，事件结束");
                TradeUi.Notify("外星广播·漠视", "信号渐渐消失在背景噪声里，什么也没有发生");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 外星广播·漠视执行失败");
            }
        }

        private static void DecodeTechProgress()
        {
            try
            {
                Research r = Research.Instance;
                if (r == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 外星广播：研究系统不可用，科技进度奖励跳过");
                    return;
                }
                TechInstance active = r.GetActiveResearch();
                if (active == null || active.tech == null || active.tech.costsByResearchTypeID == null)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 外星广播：当前没有进行中的研究，科技进度奖励跳过");
                    return;
                }
                bool instant = UnityEngine.Random.value < 0.05f;
                float pct = instant ? 1f : UnityEngine.Random.Range(0.2f, 0.5f);
                float total = 0f;
                foreach (KeyValuePair<string, float> kv in active.tech.costsByResearchTypeID)
                {
                    float have;
                    active.progressInventory.PointsByTypeID.TryGetValue(kv.Key, out have);
                    float lack = Mathf.Max(0f, kv.Value - have);
                    float add = lack * pct;
                    if (add <= 0f)
                    {
                        continue;
                    }
                    r.AddResearchPoints(kv.Key, add);
                    total += add;
                }
                MFLog.Verbose(LogCat.Event,"[Space] ★ 外星广播·破译：科技 " + active.tech.Id + " 进度 +" + total.ToString("0.0")
                    + "（" + Mathf.Round(pct * 100f) + "%" + (instant ? "，直接完成档" : "") + "）");
                string techName = string.IsNullOrEmpty(active.tech.Name) ? "当前科技" : active.tech.Name;
                TradeUi.Notify("外星广播·破译", "破译成果转入研究：" + techName
                    + " 进度 +" + Mathf.Round(pct * 100f) + "%");
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 外星广播·科技进度失败");
            }
        }

        private static void SpaceFreighter(IEventContext inst)
        {
            try
            {
                AxialI planet = SpaceRealEventRuntime.HomePlanetCell();
                if (!SpaceRealEventRuntime.CellOk(planet))
                {
                    MFLog.Warn(LogCat.Event,"[Space] 太空货船：星图未就绪（拿不到母星位置）");
                    TradeUi.Notify("太空货船", "星图尚未就绪，货船无法进港", null, "blue");
                    return;
                }
                AxialI spawn = SpaceRealEventRuntime.FindFreeCellNear(planet, 5, 10);
                if (!SpaceRealEventRuntime.CellOk(spawn))
                {
                    MFLog.Warn(LogCat.Event,"[Space] 太空货船：母星 5~10 格外找不到空位");
                    TradeUi.Notify("太空货船", "轨道附近没有空位，货船擦肩而过", null, "blue");
                    return;
                }
                AxialI dock = SpaceRealEventRuntime.FindFreeCellNear(planet, 1, 3);
                if (!SpaceRealEventRuntime.CellOk(dock))
                {
                    dock = spawn;
                }
                float dockSeconds = UnityEngine.Random.Range(1800f, 3000f);
                string id = TempStationPrefix + "freighter_" + UnityEngine.Random.Range(1000, 9999);
                SpaceStation st = SpawnTempStationAt(spawn, id, "星际货船", dockSeconds + 3600f, true);
                if (st == null)
                {
                    return;
                }
                ClusterDestinationSelector selector = st.GetComponent<ClusterDestinationSelector>();
                if (selector != null)
                {
                    selector.SetDestination(dock);
                }
                RealFreighterRun run = new RealFreighterRun();
                run.stationId = id;
                run.station = st;
                run.phase = 0;
                run.target = dock;
                run.dockUntil = dockSeconds;
                run.nextStepAt = 0f;
                run.hardStopAt = SpaceRealEventRuntime.Now() + dockSeconds + 7200f;
                SpaceRealEventRuntime.AddRun(run);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 太空货船经过：生成于 " + SpaceCfg.Pos(spawn) + "，目标轨道 "
                    + SpaceCfg.Pos(dock) + "，停靠 " + Mathf.Round(dockSeconds) + " 秒");
                TradeUi.Notify("太空货船", "一艘星际货船正驶入殖民地轨道，抵达后可与其交易，"
                    + "停靠约 3~5 周期。", null, "blue");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 太空货船执行失败");
            }
        }
    }
}
