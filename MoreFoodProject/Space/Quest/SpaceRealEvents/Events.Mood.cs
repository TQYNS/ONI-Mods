// 用途：情绪与食物：偷吃、快乐、恐惧与负面特质

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
        private static List<Edible> WorldFoods()
        {
            List<Edible> list = new List<Edible>();
            try
            {
                WorldContainer world = SpaceRealEventRuntime.MainWorld();
                if (world == null || world.worldInventory == null)
                {
                    return list;
                }
                ICollection<Pickupable> items = world.worldInventory.GetPickupables(GameTags.Edible, true);
                if (items == null)
                {
                    return list;
                }
                foreach (Pickupable p in items)
                {
                    if (p == null)
                    {
                        continue;
                    }
                    KPrefabID kpid = p.KPrefabID;
                    if (kpid != null && kpid.HasTag(GameTags.StoredPrivate))
                    {
                        continue;
                    }
                    Edible e = p.GetComponent<Edible>();
                    if (e != null)
                    {
                        list.Add(e);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 偷吃：读世界食物清单失败");
            }
            return list;
        }

        private static void SneakEating(IEventContext inst)
        {
            try
            {
                List<Edible> list = WorldFoods();
                float total = 0f;
                for (int i = 0; i < list.Count; i++)
                {
                    total += Mathf.Max(0f, list[i].Calories);
                }
                float need = 1000f * 1000f;
                if (total < need)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 偷吃：现有食物 " + (total / 1000f).ToString("0") + " kcal < 1000，条件不满足（前置条件本应拦下）");
                    return;
                }
                float remain = need;
                float removed = 0f;
                for (int i = 0; i < list.Count && remain > 0f; i++)
                {
                    Edible ed = list[i];
                    if (ed == null)
                    {
                        continue;
                    }
                    float c = Mathf.Max(0f, ed.Calories);
                    if (c <= 0f)
                    {
                        continue;
                    }
                    float take = Mathf.Min(c, remain);
                    ed.Calories = c - take;
                    MFLog.Verbose(LogCat.Event,"[Space] 偷吃：" + ed.GetProperName() + " " + (c / 1000f).ToString("0") + " → "
                        + ((c - take) / 1000f).ToString("0") + " kcal");
                    if (ed.Calories <= 0.01f)
                    {
                        Util.KDestroyGameObject(ed.gameObject);
                    }
                    remain -= take;
                    removed += take;
                }
                MFLog.Verbose(LogCat.Event,"[Space] ★ 偷吃：本星球食物 " + (total / 1000f).ToString("0") + " → "
                    + ((total - removed) / 1000f).ToString("0") + " kcal（扣除 "
                    + (removed / 1000f).ToString("0") + "，食物条目 " + list.Count + " 个）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 偷吃执行失败");
            }
        }

        // 喜悦候选：心情余量≥10、有 JoyBehaviourMonitor、不在 overjoyed；前置与本体共用判定
        private static List<GameObject> CollectJoyCandidates(out int busy, out string detail)
        {
            List<GameObject> ok = new List<GameObject>();
            busy = 0;
            detail = "";
            try
            {
                List<GameObject> dupes = LiveDupes(false);
                int noMon = 0;
                int lowMorale = 0;
                int noAttr = 0;
                float bestSurplus = float.MinValue;
                for (int i = 0; i < dupes.Count; i++)
                {
                    GameObject go = dupes[i];
                    AttributeInstance qol = Db.Get().Attributes.QualityOfLife.Lookup(go);
                    AttributeInstance exp = Db.Get().Attributes.QualityOfLifeExpectation.Lookup(go);
                    if (qol == null || exp == null)
                    {
                        noAttr++;
                        continue;
                    }
                    float surplus = qol.GetTotalValue() - exp.GetTotalValue();
                    if (surplus > bestSurplus) bestSurplus = surplus;
                    if (surplus < 10f)
                    {
                        lowMorale++;
                        continue;
                    }
                    JoyBehaviourMonitor.Instance mon = go.GetSMI<JoyBehaviourMonitor.Instance>();
                    if (mon == null)
                    {
                        noMon++;
                        continue;
                    }
                    if (mon.IsInsideState(mon.sm.overjoyed))
                    {
                        busy++;
                        continue;
                    }
                    ok.Add(go);
                }
                detail = "（候选 " + dupes.Count + "，心情不足 " + lowMorale + "，缺 JoyBehaviourMonitor "
                    + noMon + "，缺属性 " + noAttr + "，已在反应中 " + busy + "，最高心情余量 "
                    + (bestSurplus == float.MinValue ? "-" : bestSurplus.ToString("0.0")) + "）";
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 喜悦反应候选扫描失败");
            }
            return ok;
        }

        // 无可触发喜悦反应的复制人时返回 false，走框架失败路线
        private static bool HappyMoodPrecondition(IEventContext inst)
        {
            int busy;
            string detail;
            List<GameObject> ok = CollectJoyCandidates(out busy, out detail);
            if (ok != null && ok.Count > 0) return true;

            MFLog.Verbose(LogCat.Event,"[Space] 心情愉悦：前置条件不通过 —— 没有可触发喜悦反应的复制人" + detail
                + " ⇒ 跳过本轮抽取");
            return false;
        }

        private static void HappyMood(IEventContext inst)
        {
            try
            {
                int busy;
                string detail;
                List<GameObject> ok = CollectJoyCandidates(out busy, out detail);
                if (ok.Count == 0)
                {
                    // 正常情况下到不了这里（前置条件已拦下）；保留兜底，避免空引用
                    MFLog.Verbose(LogCat.Event,"[Space] 心情愉悦：没有可触发喜悦反应的复制人" + detail
                        + "（前置条件本应拦下）");
                    return;
                }
                GameObject dupe = ok[UnityEngine.Random.Range(0, ok.Count)];
                JoyBehaviourMonitor.Instance smi = dupe.GetSMI<JoyBehaviourMonitor.Instance>();
                smi.GoToOverjoyed();
                SpaceRealEventRuntime.TrackJoy(dupe);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 心情愉悦：" + dupe.GetProperName() + " 触发喜悦反应（overjoyed 状态）");
                inst.SetNoticeArg("target", dupe.GetProperName());
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 心情愉悦执行失败");
            }
        }

        private static readonly HashSet<string> negativeTraitIds = new HashSet<string>();
        private static bool negativeTraitIdsLoaded;

        private static void LoadNegativeTraitIds()
        {
            if (negativeTraitIdsLoaded)
            {
                return;
            }
            negativeTraitIdsLoaded = true;
            AddTraitIdsFromPool("BADTRAITS");
            AddTraitIdsFromPool("BIONICBUGTRAITS");
            MFLog.Verbose(LogCat.Event,"[Space] 克服恐惧：可移除负面特质池 " + negativeTraitIds.Count + " 种");
        }

        private static void AddTraitIdsFromPool(string fieldName)
        {
            try
            {
                FieldInfo fi = AccessTools.Field(typeof(DUPLICANTSTATS), fieldName);
                if (fi == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 克服恐惧：找不到 DUPLICANTSTATS." + fieldName);
                    return;
                }
                IEnumerable pool = fi.GetValue(null) as IEnumerable;
                if (pool == null)
                {
                    return;
                }
                foreach (object item in pool)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    FieldInfo idField = AccessTools.Field(item.GetType(), "id");
                    string id = idField != null ? idField.GetValue(item) as string : null;
                    if (!string.IsNullOrEmpty(id))
                    {
                        negativeTraitIds.Add(id);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 克服恐惧：读取特质池 " + fieldName + " 失败");
            }
        }

        private static void OvercomeFear(IEventContext inst)
        {
            try
            {
                LoadNegativeTraitIds();
                List<GameObject> dupes = LiveDupes(false);
                List<GameObject> owners = new List<GameObject>();
                List<Klei.AI.Trait> traits = new List<Klei.AI.Trait>();
                for (int i = 0; i < dupes.Count; i++)
                {
                    Klei.AI.Traits t = dupes[i].GetComponent<Klei.AI.Traits>();
                    if (t == null || t.TraitList == null)
                    {
                        continue;
                    }
                    for (int k = 0; k < t.TraitList.Count; k++)
                    {
                        Klei.AI.Trait trait = t.TraitList[k];
                        if (trait == null || string.IsNullOrEmpty(trait.Id) || trait.Id == "None")
                        {
                            continue;
                        }
                        if (!negativeTraitIds.Contains(trait.Id))
                        {
                            continue;
                        }
                        owners.Add(dupes[i]);
                        traits.Add(trait);
                    }
                }
                if (traits.Count == 0)
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 克服恐惧：没有可移除的负面特质（前置条件本应拦下）");
                    return;
                }
                int idx = UnityEngine.Random.Range(0, traits.Count);
                GameObject dupe = owners[idx];
                Klei.AI.Trait pick = traits[idx];
                Klei.AI.Traits mgr = dupe.GetComponent<Klei.AI.Traits>();
                string tname = pick.GetName();
                mgr.Remove(pick);
                MFLog.Verbose(LogCat.Event,"[Space] ★ 克服恐惧：" + dupe.GetProperName() + " 移除负面特质 " + pick.Id
                    + "（" + tname + "）");
                inst.SetNoticeArg("target", dupe.GetProperName());
                inst.SetNoticeArg("trait", tname);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 克服恐惧执行失败");
            }
        }
    }
}
