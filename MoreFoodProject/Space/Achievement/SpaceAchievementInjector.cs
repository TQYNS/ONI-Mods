using System;
using System.Collections;
using System.Collections.Generic;
using Database;
using System.Reflection;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class SpaceAchievementInjector
    {
        private static readonly string[] Ids = SpaceAchievements.AllIds;

        private static FieldInfo dictField;

        private static IDictionary GetDict(ColonyAchievementTracker tracker)
        {
            if (tracker == null)
            {
                return null;
            }
            if (dictField == null)
            {
                dictField = typeof(ColonyAchievementTracker).GetField("achievements",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            return dictField != null ? dictField.GetValue(tracker) as IDictionary : null;
        }

        // 只看追踪器里的成功标志，避免运行期静态集合跨存档残留造成漏判
        internal static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id) || SaveGame.Instance == null)
            {
                return false;
            }
            IDictionary dict = GetDict(SaveGame.Instance.ColonyAchievementTracker);
            if (dict == null || !dict.Contains(id))
            {
                return false;
            }
            ColonyAchievementStatus st = dict[id] as ColonyAchievementStatus;
            return st != null && st.success;
        }

        internal static void ClearEntries(ColonyAchievementTracker tracker)
        {
            if (tracker == null)
            {
                return;
            }
            IDictionary dict = GetDict(tracker);
            if (dict == null)
            {
                return;
            }
            int removed = 0;
            for (int i = 0; i < Ids.Length; i++)
            {
                if (dict.Contains(Ids[i]))
                {
                    dict.Remove(Ids[i]);
                    removed++;
                }
            }
            if (removed > 0)
            {
                MFLog.Info(LogCat.Space, "[Space] 读档前清掉成就字典里预填的 " + removed
                    + " 个自定义条目（否则原版反序列化会因重复键判定存档损坏）");
            }
        }

        internal static void EnsureEntries()
        {
            if (SaveGame.Instance == null)
            {
                return;
            }
            EnsureEntries(SaveGame.Instance.ColonyAchievementTracker);
        }

        internal static void EnsureEntries(ColonyAchievementTracker tracker)
        {
            try
            {
                IDictionary dict = GetDict(tracker);
                if (dict == null)
                {
                    return;
                }
                int added = 0;
                for (int i = 0; i < Ids.Length; i++)
                {
                    string id = Ids[i];
                    if (dict.Contains(id))
                    {
                        ColonyAchievementStatus have = dict[id] as ColonyAchievementStatus;
                        if (have != null && have.success)
                        {
                            SpaceAchievements.SyncUnlockedRuntime(id, true);
                        }
                        continue;
                    }
                    ColonyAchievement ach = FindAchievement(id);
                    if (ach == null)
                    {
                        continue;
                    }
                    dict[id] = new ColonyAchievementStatus(id);
                    added++;
                }
                if (added > 0)
                {
                    MFLog.Info(LogCat.Space, "[Space] 成就追踪器已补条目 " + added + " 个（字典共 " + dict.Count + " 项）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 补成就追踪器条目失败");
            }
        }

        internal static void MarkSuccess(ColonyAchievementTracker tracker, string id)
        {
            try
            {
                if (tracker == null || string.IsNullOrEmpty(id))
                {
                    return;
                }
                IDictionary dict = GetDict(tracker);
                if (dict == null)
                {
                    return;
                }
                if (!dict.Contains(id))
                {
                    EnsureEntries(tracker);
                }
                ColonyAchievementStatus status = dict.Contains(id)
                    ? dict[id] as ColonyAchievementStatus : null;
                if (status == null)
                {
                    return;
                }
                status.success = true;
                status.failed = false;
                SpaceAchievements.MarkUnlockedRuntime(id);

                FieldInfo displayField = typeof(ColonyAchievementTracker).GetField(
                    "completedAchievementsToDisplay",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                List<string> display = displayField != null
                    ? displayField.GetValue(tracker) as List<string> : null;
                if (display != null && !display.Contains(id))
                {
                    display.Add(id);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 写成就完成状态失败 " + id);
            }
        }

        private static ColonyAchievement FindAchievement(string id)
        {
            try
            {
                IList all = Db.Get().ColonyAchievements.resources;
                for (int i = 0; i < all.Count; i++)
                {
                    ColonyAchievement ca = all[i] as ColonyAchievement;
                    if (ca != null && ca.Id == id)
                    {
                        return ca;
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 查成就失败 " + id);
            }
            return null;
        }
    }
}
