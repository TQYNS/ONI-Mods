// 用途：基因洗牌：增益登记、应用与提示

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
    internal static class SpaceGeneShuffle
    {
        private const float BonusMultiplier = 1.2f;

        private static readonly Dictionary<string, string> boosted =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static bool ensured;

        internal static void Ensure()
        {
            if (ensured)
            {
                return;
            }
            try
            {
                if (Db.Get() == null || Db.Get().traits == null)
                {
                    return;
                }
                FieldInfo fi = AccessTools.Field(typeof(DUPLICANTSTATS), "GENESHUFFLERTRAITS");
                IEnumerable pool = fi != null ? fi.GetValue(null) as IEnumerable : null;
                if (pool == null)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 神经振荡器：读不到 DUPLICANTSTATS.GENESHUFFLERTRAITS，超频奖励不可用");
                    return;
                }
                foreach (object item in pool)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    FieldInfo idField = AccessTools.Field(item.GetType(), "id");
                    string baseId = idField != null ? idField.GetValue(item) as string : null;
                    if (string.IsNullOrEmpty(baseId))
                    {
                        continue;
                    }
                    Trait baseTrait = Db.Get().traits.TryGet(baseId);
                    if (baseTrait == null)
                    {
                        MFLog.Warn(LogCat.Event,"[Space] 神经振荡器：找不到原版特质 " + baseId);
                        continue;
                    }
                    string boostedId = "SpaceGeneBoost_" + baseId;
                    Trait trait = Db.Get().traits.TryGet(boostedId);
                    if (trait == null)
                    {
                        trait = Db.Get().CreateTrait(boostedId, baseTrait.Name, baseTrait.description,
                            null, true, null, baseTrait.PositiveTrait, true);
                        for (int i = 0; i < baseTrait.SelfModifiers.Count; i++)
                        {
                            AttributeModifier m = baseTrait.SelfModifiers[i];
                            trait.Add(new AttributeModifier(m.AttributeId, m.Value * BonusMultiplier,
                                baseTrait.Name, m.IsMultiplier, false, false));
                        }
                    }
                    boosted[baseId] = boostedId;
                    MFLog.Verbose(LogCat.Event,"[Space] 神经振荡器超频特质 " + baseId + " → " + boostedId
                        + "（修饰符 " + baseTrait.SelfModifiers.Count + " 条 ×" + BonusMultiplier + "）");
                }
                MFLog.Verbose(LogCat.Event,"[Space] 神经振荡器超频奖励已就绪：" + boosted.Count + " 种特质");
                if (boosted.Count > 0)
                {
                    ensured = true;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神经振荡器超频特质创建失败");
            }
        }

        internal static void SaveBonus()
        {
            try
            {
                TradeSaveData save = TradeSaveData.Attach();
                if (save != null)
                {
                    save.Capture();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神经振荡器超频奖励写入存档失败");
            }
        }

        internal static bool TryApply(GeneShuffler shuffler, WorkerBase worker)
        {
            if (shuffler == null || worker == null)
            {
                return false;
            }
            Ensure();
            if (boosted.Count == 0)
            {
                return false;
            }
            Traits traits = worker.GetComponent<Traits>();
            if (traits == null)
            {
                return false;
            }
            List<string> available = new List<string>();
            foreach (KeyValuePair<string, string> kv in boosted)
            {
                if (!traits.HasTrait(kv.Key) && !traits.HasTrait(kv.Value))
                {
                    available.Add(kv.Key);
                }
            }
            if (available.Count == 0)
            {
                ShowDialog(false, worker, null);
                return true;
            }
            string basePick = available[UnityEngine.Random.Range(0, available.Count)];
            bool boostedPick = SpaceRealEventRuntime.GeneShuffleBonus;
            string traitId = basePick;
            if (boostedPick && boosted.ContainsKey(basePick))
            {
                traitId = boosted[basePick];
            }
            Trait trait = Db.Get().traits.TryGet(traitId);
            if (trait == null)
            {
                return false;
            }
            traits.Add(trait);
            if (boostedPick)
            {
                SpaceRealEventRuntime.GeneShuffleBonus = false;
                SaveBonus();
                MFLog.Verbose(LogCat.Event,"[Space] ★ 神经振荡器：超频奖励已消耗，" + worker.GetProperName()
                    + " 获得强化特质 " + traitId);
            }
            else
            {
                MFLog.Verbose(LogCat.Event,"[Space] ★ 神经振荡器：" + worker.GetProperName() + " 获得特质 " + traitId);
            }
            ShowDialog(true, worker, trait);
            MethodInfo setConsumed = AccessTools.Method(typeof(GeneShuffler), "SetConsumed",
                new Type[] { typeof(bool) }, null);
            if (setConsumed != null)
            {
                setConsumed.Invoke(shuffler, new object[] { true });
            }
            else
            {
                shuffler.IsConsumed = true;
            }
            return true;
        }

        private static void ShowDialog(bool success, WorkerBase worker, Trait trait)
        {
            try
            {
                InfoDialogScreen screen = (InfoDialogScreen)GameScreenManager.Instance.StartScreen(
                    ScreenPrefabs.Instance.InfoDialogScreen.gameObject,
                    GameScreenManager.Instance.ssOverlayCanvas.gameObject,
                    GameScreenManager.UIRenderTarget.ScreenSpaceOverlay);
                string text = success
                    ? string.Format(UI.GENESHUFFLERMESSAGE.BODY_SUCCESS, worker.GetProperName(), trait.GetName(),
                        trait.GetTooltip())
                    : string.Format(UI.GENESHUFFLERMESSAGE.BODY_FAILURE, worker.GetProperName());
                screen.SetHeader(UI.GENESHUFFLERMESSAGE.HEADER).AddPlainText(text).AddDefaultOK(false);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神经振荡器结果弹窗失败");
            }
        }
    }

    [HarmonyPatch(typeof(GeneShuffler), "ApplyRandomTrait")]
    internal static class SpaceGeneShuffleTraitPatch
    {
        private static bool Prefix(GeneShuffler __instance, WorkerBase worker)
        {
            try
            {
                return !SpaceGeneShuffle.TryApply(__instance, worker);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 神经振荡器特质替换失败（回退原版）");
                return true;
            }
        }
    }
}
