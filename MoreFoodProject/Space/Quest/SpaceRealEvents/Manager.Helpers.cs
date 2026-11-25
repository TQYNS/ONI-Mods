// 用途：事件通用助手：技能名、角色名、复制人/植物与被动钩子

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
        // 原版字符串表缺失时的占位（Strings.Get 未命中会返回该类串）
        private static bool MissingPlaceholder(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            if (s.StartsWith("<missing", StringComparison.OrdinalIgnoreCase)) return true;
            if (s.StartsWith("MISSING.", StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(s, "Missing", StringComparison.OrdinalIgnoreCase);
        }

        // 技能显示名：技能组名取不到时，退回技能自身的名称（避免显示 BionicsC2 这类 id）
        internal static string SkillDisplayName(string skillId)
        {
            Database.Skill s = Db.Get().Skills.Get(skillId);
            if (s == null) return skillId;
            string n = s.Name;
            if (!string.IsNullOrEmpty(n) && (n == skillId || MissingPlaceholder(n))) n = null;
            return string.IsNullOrEmpty(n) ? skillId : n;
        }

        internal static string SkillGroupNameOf(string skillId)
        {
            Database.Skill skill = Db.Get().Skills.Get(skillId);
            if (skill != null && !string.IsNullOrEmpty(skill.skillGroup))
            {
                string key = "STRINGS.DUPLICANTS.SKILLGROUPS." + skill.skillGroup.ToUpper() + ".NAME";
                string text = Strings.Get(key);
                // Strings.Get 未命中返回 Missing 占位串，必须当作拿不到处理
                if (string.IsNullOrEmpty(text) || text == key || MissingPlaceholder(text))
                {
                    return "";
                }
                if (text.EndsWith("者", StringComparison.Ordinal))
                {
                    text = text.Substring(0, text.Length - 1);
                }
                return MissingPlaceholder(text) ? "" : text;
            }
            return "";
        }

        private static string PrefabIdOf(GameObject go)
        {
            KPrefabID pid = go.GetComponent<KPrefabID>();
            if (pid != null && !string.IsNullOrEmpty(pid.PrefabTag.Name))
            {
                return pid.PrefabTag.Name;
            }
            return go != null ? go.name : null;
        }

        internal static string ProperNameOf(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return prefabId;
            }
            GameObject prefab = Assets.TryGetPrefab(new Tag(prefabId));
            if (prefab != null)
            {
                KPrefabID pid = prefab.GetComponent<KPrefabID>();
                if (pid != null)
                {
                    string proper = pid.GetProperName();
                    if (!string.IsNullOrEmpty(proper))
                    {
                        return proper;
                    }
                }
            }
            return prefabId;
        }

        private static bool IsBionic(GameObject go)
        {
            return go != null && go.GetComponent<BionicUpgradesMonitor>() != null;
        }

        internal static List<GameObject> LiveDupes(bool excludeBionic)
        {
            List<GameObject> list = new List<GameObject>();
            try
            {
                if (Components.LiveMinionIdentities == null)
                {
                    return list;
                }
                foreach (MinionIdentity id in Components.LiveMinionIdentities)
                {
                    if (id == null)
                    {
                        continue;
                    }
                    GameObject go = id.gameObject;
                    if (go == null || !SpaceRealEventRuntime.IsInActiveWorld(go))
                    {
                        continue;
                    }
                    if (excludeBionic && IsBionic(go))
                    {
                        continue;
                    }
                    list.Add(go);
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 取复制人列表失败");
            }
            return list;
        }

        // 修复：直接订阅 OnAdd/OnRemove（Cmps.Register 会重放全体订阅者 → 重复键刷屏）
        private static bool passiveHooksInstalled;

        internal static void SetUpPassiveHooks()
        {
            try
            {
                if (Components.LiveMinionIdentities == null)
                {
                    return;
                }
                Components.LiveMinionIdentities.OnAdd -= OnMinionAdded;
                Components.LiveMinionIdentities.OnAdd += OnMinionAdded;
                Components.LiveMinionIdentities.OnRemove -= OnMinionRemoved;
                Components.LiveMinionIdentities.OnRemove += OnMinionRemoved;
                if (!passiveHooksInstalled)
                {
                    passiveHooksInstalled = true;
                    foreach (MinionIdentity id in Components.LiveMinionIdentities)
                    {
                        try { OnMinionAdded(id); }
                        catch (Exception) { /* expected: 回调内部已按 Warn 记录，此处隔离单个复制人不阻断回放 */ }
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 订阅复制人增删事件失败");
            }
        }

        private static void OnMinionAdded(MinionIdentity id)
        {
            try
            {
                if (id == null)
                {
                    return;
                }
                GameObject go = id.gameObject;
                if (go == null || !SpaceRealEventRuntime.IsInActiveWorld(go))
                {
                    return;
                }
                RealBuff b = SpaceRealEventRuntime.FindBuff(GravityKey);
                if (b == null)
                {
                    return;
                }
                // 走统一通道：解析 / 校验 / 去重全在 hub 内，策略仍是 GravityActions
                if (SpaceRealEventBuffHub.Attach(b, go, GravityActions))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 重力异常：补挂新加入的复制人 " + go.GetProperName());
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 重力异常补挂失败");
            }
        }

        private static void OnMinionRemoved(MinionIdentity id)
        {
            try
            {
                RealBuff b = SpaceRealEventRuntime.FindBuff(GravityKey);
                if (b == null)
                {
                    return;
                }
                for (int i = b.attrs.Count - 1; i >= 0; i--)
                {
                    RealAttrBinding bind = b.attrs[i];
                    if (bind == null || bind.go == null)
                    {
                        b.attrs.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 重力异常清理失败");
            }
        }

        internal static void OnPlantPlanted(GameObject plant)
        {
            try
            {
                if (plant == null || !SpaceRealEventRuntime.IsInActiveWorld(plant))
                {
                    return;
                }
                RealBuff fertile = SpaceRealEventRuntime.FindBuff(FertileKey);
                if (fertile != null && SpaceRealEventBuffHub.Attach(fertile, plant, FertileActions))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 丰饶季：补挂事件期间新种下的作物 " + plant.GetProperName());
                }
                RealBuff dry = SpaceRealEventRuntime.FindBuff(DrySeasonKey);
                if (dry != null && SpaceRealEventBuffHub.Attach(dry, plant, DrySeasonActions))
                {
                    MFLog.Verbose(LogCat.Event,"[Space] 旱季：补挂事件期间新种下的作物 " + plant.GetProperName());
                }
            }
            catch (Exception e)
            {
                MFLog.Verbose(LogCat.Event, e, "[Space] 新种作物补挂失败");
            }
        }
    }
}
