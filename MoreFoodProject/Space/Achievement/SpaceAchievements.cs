using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Database;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreFood_KFC
{
    internal static class SpaceAchievements
    {
        internal const string HunterFirstContactId = "SpaceHunterFirstContact";
        internal const string FirstTradeId = "SpaceFirstTrade";
        internal const string TearTakeoverId = "SpaceTearTakeover";
        internal const string WealthId = "SpaceWealthy";
        internal const string LegendId = "SpaceLegendAdventurer";

        internal const float WealthThreshold = 10000f;

        // 自定义成就 id 的唯一来源：追踪器、界面与胜利拦截都引用它
        internal static readonly string[] AllIds = new string[]
        {
            HunterFirstContactId, FirstTradeId, TearTakeoverId, WealthId, LegendId
        };

        internal static bool IsOurAchievement(string id)
        {
            return Array.IndexOf(AllIds, id) >= 0;
        }

        // 换档时清空运行期解锁集合，读档后由 EnsureEntries 按存档 success 填回
        internal static void ResetForSession()
        {
            unlockedRuntime.Clear();
        }

        // 里程碑成就：只在状态变化的既有入口按当前星币 / 声望补判，不挂 Tick
        internal static void CheckMilestones()
        {
            try
            {
                if (!registered || SaveGame.Instance == null || SaveGame.Instance.ColonyAchievementTracker == null)
                {
                    return;
                }
                if (!SpaceAchievementInjector.IsUnlocked(WealthId) && Find(WealthId) != null
                    && TradeRegistry.Currency >= WealthThreshold)
                {
                    Unlock(WealthId, "家财万贯");
                }
                if (!SpaceAchievementInjector.IsUnlocked(LegendId) && Find(LegendId) != null
                    && TradeRegistry.IsMaxRank())
                {
                    Unlock(LegendId, "传奇冒险家");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 成就里程碑检查失败（不影响交易）");
            }
        }

// 是否已注册成就表
        private static bool registered;
        private static readonly List<ColonyAchievement> ours = new List<ColonyAchievement>();
        private static readonly HashSet<string> unlockedRuntime = new HashSet<string>();

        internal static bool IsUnlockedRuntime(string id)
        {
            return !string.IsNullOrEmpty(id) && unlockedRuntime.Contains(id);
        }

        internal static void MarkUnlockedRuntime(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                unlockedRuntime.Add(id);
            }
        }

        internal static void SyncUnlockedRuntime(string id, bool success)
        {
            if (string.IsNullOrEmpty(id) || !success)
            {
                return;
            }
            unlockedRuntime.Add(id);
        }

        internal static void Register()
        {
            if (registered)
            {
                return;
            }
            registered = true;
            try
            {
                Add(HunterFirstContactId, "星际来客", "初次发现美食猎人协会",
                    "在星图上找到并接触美食猎人协会空间站，拿到协会的启动资金。",
                    "在星图上找到并接触美食猎人协会空间站", false);
                Add(FirstTradeId, "第一桶金", "完成第一笔星图贸易",
                    "与星图上的任意贸易站完成一次买入或卖出。",
                    "与任意贸易站完成一次买入或卖出", false);
                Add(TearTakeoverId, "穿过裂口的人", "接管时间裂口",
                    "把火箭与舱内复制人一起送进时间裂口，而不是让它被吞掉。",
                    "把火箭与舱内复制人一起送进时间裂口", false);
                Add(WealthId, "家财万贯", "最高持有 10000 星币",
                    "让星币余额达到 10000，成为星图间最富有的冒险家。",
                    "星币持有量达到 10000", false);
                Add(LegendId, "传奇冒险家", "协会声望达到 S",
                    "把美食猎人协会的声望推到最高的 S 级。",
                    "协会声望达到 S 级", false);
                MFLog.Info(LogCat.Space,"[Space] 自定义成就已注入原版成就库（Db.Initialize 之后）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 注入自定义成就失败（游戏可继续）");
            }
        }

        private static void Add(string id, string name, string description, string messageBody,
            string requirementText, bool victory)
        {
            ColonyAchievement a = new ColonyAchievement();
            a.Disabled = false;
            SetMember(a, "Id", id);
            SetMember(a, "Name", name);
            SetMember(a, "description", description);
            SetMember(a, "messageTitle", name);
            SetMember(a, "messageBody", messageBody);
            SetMember(a, "isVictoryCondition", victory);
            SetMember(a, "platformAchievementId", "");
            SetMember(a, "icon", "rocket");
            SetMember(a, "clusterTag", null);
            SetMember(a, "shortVideoName", "");
            SetMember(a, "loopVideoName", "");
            SetMember(a, "dlcIdFrom", "");
            SetMember(a, "requiredDlcIds", new string[0]);
            SetMember(a, "forbiddenDlcIds", new string[0]);
            List<ColonyAchievementRequirement> reqs = new List<ColonyAchievementRequirement>();
            reqs.Add(new SpaceAchievementRequirement(id, requirementText, description));
            SetMember(a, "requirementChecklist", reqs);
            Db.Get().ColonyAchievements.Add(a);
            ours.Add(a);
        }

        internal static bool IsVictory(string id)
        {
            ColonyAchievement a = Find(id);
            return a != null && a.isVictoryCondition;
        }

        internal static ColonyAchievement Find(string id)
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

        private static void SetMember(object target, string field, object value)
        {
            if (target == null || field == null)
            {
                return;
            }
            Type t = target.GetType();
            while (t != null)
            {
                FieldInfo f = t.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    try
                    {
                        f.SetValue(target, value);
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Space, e, "[Space] 成就字段 " + field + " 赋值失败");
                    }
                    return;
                }
                t = t.BaseType;
            }
            Debug.LogWarning("[Space] 成就字段不存在（跳过）：" + field);
        }

        internal static void Unlock(string id, string displayName)
        {
            try
            {
                // Unlock 必须幂等：成交每次都调它，重复写会让成就通知反复推送（见 交易.md §7.4 P-34）
                if (string.IsNullOrEmpty(id) || IsUnlockedRuntime(id) || SpaceAchievementInjector.IsUnlocked(id))
                {
                    return;
                }
                if (SaveGame.Instance != null && SaveGame.Instance.ColonyAchievementTracker != null)
                {
                    System.Reflection.MethodInfo m = typeof(ColonyAchievementTracker).GetMethod(
                        "IsAchievementUnlocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    bool known = false;
                    if (m != null)
                    {
                        try
                        {
                            System.Collections.IList all = Db.Get().ColonyAchievements.resources;
                            for (int i = 0; i < all.Count; i++)
                            {
                                ColonyAchievement ca = all[i] as ColonyAchievement;
                                if (ca != null && ca.Id == id)
                                {
                                    known = true;
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // expected: 遍历成就库失败时按「不在原版库」处理，只本地记账加通知
                        }
                    }
                    if (!known)
                    {
                        Debug.LogWarning("[Space] 成就 " + id + " 不在原版库里（跳过追踪器调用，避免 LogError 崩溃），只记账");
                        MarkUnlockedRuntime(id);
                        TradeUi.Notify("成就达成", displayName);
                        return;
                    }
                    SpaceAchievementInjector.MarkSuccess(SaveGame.Instance.ColonyAchievementTracker, id);
                    if (IsVictory(id))
                    {
                        Debug.LogWarning("[Space] 成就 " + id + " 按胜利级分类：跳过原版胜利结算（结算序列为空会崩），只记账并通知");
                    }
                    else
                    {
                        System.Reflection.MethodInfo t = typeof(ColonyAchievementTracker).GetMethod(
                            "TriggerNewAchievementCompleted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (t != null)
                        {
                            t.Invoke(SaveGame.Instance.ColonyAchievementTracker,
                                new object[] { id, Game.Instance != null ? Game.Instance.gameObject : null });
                        }
                    }
                    try
                    {
                        RetireColonyUtility.SaveColonySummaryData();
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Space, e, "[Space] 写殖民地成就摘要失败（不影响游戏）");
                    }
                }
                MarkUnlockedRuntime(id);   // 无存档/追踪器缺失时也记一次，避免同会话反复推送
                MFLog.Info(LogCat.Space,"[Space] ★ 成就达成：" + displayName + "（" + id + "）");
                TradeUi.Notify("成就达成", displayName);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 成就调用失败（只记账）成就 " + id);
            }
        }
    }

    internal sealed class SpaceAchievementRequirement : VictoryColonyAchievementRequirement
    {
        private readonly string id;
        private readonly string label;
        private readonly string detail;

        internal SpaceAchievementRequirement(string id, string label, string detail)
        {
            this.id = id;
            this.label = label;
            this.detail = detail;
        }

        public override string Name()
        {
            return label;
        }

        public override string Description()
        {
            return detail;
        }

        public override string GetProgress(bool complete)
        {
            return label;
        }

        public override bool Success()
        {
            return SpaceAchievements.IsUnlockedRuntime(id);
        }
    }

    [HarmonyPatch(typeof(KScrollRect), "SetSmoothAutoScrollTarget")]
    internal static class SmoothScrollClampPatch
    {
private static void Prefix(ref float normalizedVerticalPos)
        {
            try
            {
                if (normalizedVerticalPos < 0f)
                {
                    MFLog.Info(LogCat.Space,"[Space] 成就列表自动滚动目标 " + normalizedVerticalPos.ToString("0.000")
                        + " 越界（底部条目的原版公式会算出负值），已钳到 0，避免无限下拉");
                    normalizedVerticalPos = 0f;
                }
                else if (normalizedVerticalPos > 1f)
                {
                    normalizedVerticalPos = 1f;
                }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:SmoothScrollClampPatch", e); }
        }
    }

    [HarmonyPatch(typeof(ColonyAchievementTracker), "TriggerNewAchievementCompleted")]
    internal static class VictoryTriggerGuardPatch
    {
        private static bool Prefix(string achievement)
        {
            try
            {
                if (string.IsNullOrEmpty(achievement))
                {
                    return true;
                }
                if (!SpaceAchievements.IsOurAchievement(achievement))
                {
                    return true;
                }
                if (!SpaceAchievements.IsVictory(achievement))
                {
                    return true;
                }
                Debug.LogWarning("[Space] 拦截胜利级成就 " + achievement
                    + " 的原版胜利结算触发（未填 victorySequence，会 NRE），改为只记账");
                return false;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 胜利结算拦截异常（交回原版）成就 " + achievement);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(RetiredColonyInfoScreen), "LoadColony")]
    internal static class AchievementScreenScrollPatch
    {
        private static void Postfix(RetiredColonyInfoScreen __instance)
        {
            try
            {
                Type t = typeof(RetiredColonyInfoScreen);
                FieldInfo entriesField = t.GetField("achievementEntries",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                IDictionary entries = entriesField != null ? entriesField.GetValue(__instance) as IDictionary : null;
                if (entries == null || entries.Count == 0)
                {
                    return;
                }
                foreach (DictionaryEntry kv in entries)
                {
                    GameObject go = kv.Value as GameObject;
                    if (go == null || go.transform.parent == null)
                    {
                        continue;
                    }
                    RectTransform rt = go.transform.parent as RectTransform;
                    if (rt != null)
                    {
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                    }
                    break;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 成就列表重排失败（不影响原版）");
            }
        }
    }

    [HarmonyPatch(typeof(RetiredColonyInfoScreen), "PopulateAchievements")]
    internal static class AchievementScreenPatch
    {
        private static readonly string[] Ids = SpaceAchievements.AllIds;

        private static void Postfix(RetiredColonyInfoScreen __instance)
        {
            try
            {
                Type t = typeof(RetiredColonyInfoScreen);
                FieldInfo entriesField = t.GetField("achievementEntries",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                IDictionary entries = entriesField != null ? entriesField.GetValue(__instance) as IDictionary : null;
                if (entries == null)
                {
                    Debug.LogWarning("[Space] 成就界面：取不到条目表，无法补自定义成就");
                    return;
                }
                int added = 0;
                for (int i = 0; i < Ids.Length; i++)
                {
                    string id = Ids[i];
                    if (entries.Contains(id))
                    {
                        continue;
                    }
                    ColonyAchievement ach = FindAchievement(id);
                    GameObject go = ach != null ? MakeEntry(t, __instance, ach) : null;
                    if (go == null)
                    {
                        continue;
                    }
                    entries[id] = go;
                    added++;
                }
                if (added > 0)
                {
                    GameObject container = GetContainer(t, __instance);
                    if (container != null)
                    {
                        RectTransform rt = container.GetComponent<RectTransform>();
                        if (rt != null)
                        {
                            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                        }
                    }
                    MethodInfo update = t.GetMethod("UpdateAchievementData",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (update != null)
                    {
                        update.Invoke(__instance, new object[] { null, null });
                    }
                }
                int present = 0;
                int hidden = 0;
                for (int i = 0; i < Ids.Length; i++)
                {
                    if (!entries.Contains(Ids[i]))
                    {
                        continue;
                    }
                    present++;
                    GameObject go = entries[Ids[i]] as GameObject;
                    if (go == null)
                    {
                        continue;
                    }
                    if (!go.activeSelf)
                    {
                        hidden++;
                    }
                    AchievementWidget w = go.GetComponent<AchievementWidget>();
                    ColonyAchievementStatus st = GetStatus(Ids[i]);
                    if (w != null && st != null)
                    {
                        w.ShowProgress(st);
                    }
                }
                MFLog.Info(LogCat.Space,"[Space] 成就界面自检：共 " + entries.Count + " 条，自定义 " + present + "/" + Ids.Length
                    + "（隐藏 " + hidden + "），本次补 " + added + " 条；SaveLoader="
                    + (SaveLoader.Instance != null ? "在场" : "为空"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 成就界面补条目失败（不影响原版界面）");
            }
        }

        private static ColonyAchievementStatus GetStatus(string id)
        {
            try
            {
                if (SaveGame.Instance != null && SaveGame.Instance.ColonyAchievementTracker != null)
                {
                    ColonyAchievementStatus st;
                    if (SaveGame.Instance.ColonyAchievementTracker.achievements.TryGetValue(id, out st) && st != null)
                    {
                        return st;
                    }
                }
                return new ColonyAchievementStatus(id);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Space, e, "[Space] 取成就状态失败 " + id);
                return null;
            }
        }

        private static GameObject GetContainer(Type screenType, RetiredColonyInfoScreen screen)
        {
            FieldInfo containerField = screenType.GetField("achievementsContainer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return containerField != null ? containerField.GetValue(screen) as GameObject : null;
        }

        private static GameObject MakeEntry(Type screenType, RetiredColonyInfoScreen screen, ColonyAchievement ach)
        {
            FieldInfo prefabField = screenType.GetField(
                ach.isVictoryCondition ? "victoryAchievementsPrefab" : "achievementsPrefab",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            GameObject prefab = prefabField != null ? prefabField.GetValue(screen) as GameObject : null;
            GameObject container = GetContainer(screenType, screen);
            if (prefab == null || container == null)
            {
                Debug.LogWarning("[Space] 成就界面：容器或条目 prefab 为空，无法补条目");
                return null;
            }
            GameObject go = Util.KInstantiateUI(prefab, container, true);
            if (go == null)
            {
                return null;
            }
            if (ach.isVictoryCondition)
            {
                go.transform.SetAsFirstSibling();
            }
            HierarchyReferences href = go.GetComponent<HierarchyReferences>();
            if (href != null)
            {
                LocText name = href.TryGetReference<LocText>("nameLabel");
                if (name != null)
                {
                    name.SetText(ach.Name);
                }
                LocText desc = href.TryGetReference<LocText>("descriptionLabel");
                if (desc != null)
                {
                    desc.SetText(ach.description);
                }
                Image icon = href.TryGetReference<Image>("icon");
                if (icon != null)
                {
                    Sprite sp = string.IsNullOrEmpty(ach.icon) ? null : Assets.GetSprite(ach.icon);
                    if (sp == null && !string.IsNullOrEmpty(ach.Name))
                    {
                        sp = Assets.GetSprite(ach.Name);
                    }
                    if (sp == null)
                    {
                        sp = Assets.GetSprite("check");
                    }
                    icon.sprite = sp;
                }
                KImage dlc = href.TryGetReference<KImage>("dlc_overlay");
                if (dlc != null)
                {
                    dlc.gameObject.SetActive(false);
                }
            }
            MultiToggle toggle = go.GetComponent<MultiToggle>();
            if (toggle != null)
            {
                toggle.ChangeState(2);
            }
            AchievementWidget widget = go.GetComponent<AchievementWidget>();
            if (widget != null)
            {
                widget.dlcIdFrom = ach.dlcIdFrom;
            }
            return go;
        }

        private static ColonyAchievement FindAchievement(string id)
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
            return null;
        }
    }
}
