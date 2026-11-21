// ManagementMenuLocGuard.cs — 顶部栏整排 MISSING 的显示层兜底：按钮建好后全量补救（见 星图.md【P-23】）

using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(ManagementMenu), "OnPrefabInit")]
    internal static class ManagementMenuLocGuard
    {
        // 21:3x 降噪：_text 层已在 20:31 证伪（MISSING 时 0 个 _text 是坏的），
        // 它只是历史观测项，每次开局重复打一整行属噪音 ⇒ 每进程只留一行。
        private static bool _textSelfCheckLogged;

        [HarmonyPriority(Priority.First)]
        private static void Prefix()
        {
            try
            {
                int n = LocalGapRepair.RepairLocStringTexts();
                if (!_textSelfCheckLogged)
                {
                    _textSelfCheckLogged = true;
                    MFLog.Info(LogCat.UI, "[MoreFood/Loc] 管理菜单构造前 _text 层自检（非主防线，仅报一次）：扫描 "
                                        + LocalGapRepair.IndexedFields + " 个 LocString；回填 " + n
                                        + " 条（表 " + LocalGapRepair.LastFromTable
                                        + " / 快照 " + LocalGapRepair.LastFromSnapshot
                                        + "）；仍无可用文本 " + LocalGapRepair.LastUnrepairable + " 条"
                                        + (string.IsNullOrEmpty(LocalGapRepair.LastBadKeys)
                                           ? "" : "；坏键样例 " + LocalGapRepair.LastBadKeys));
                }
            }
            catch (Exception e)
            {
                // 绝不能因为自检失败挡住原版 UI 初始化
                MFLog.Exc(LogCat.Patch, e, "[MoreFood/Loc] 管理菜单词条自检异常（已忽略）");
            }
        }

        // ★ 20:4x 方向订正后的主防线：按钮已由 RefreshButtons 建好，
        //   此刻（以及之后的 LocText.Awake / SwapFont）才是 MISSING 真正落到显示层的位置。
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            try
            {
                int n = LocalGapRepair.RepairVisibleLocTexts();

                // 有修复 ⇒ 打完整行（保留「修复 N 条」与改回来的中文样例，这是唯一判据）
                if (n > 0)
                {
                    MFLog.Info(LogCat.UI, "[MoreFood/Loc] 管理菜单构造后显示层兜底：修复 " + n
                                        + " 条；" + LocalGapRepair.LastDisplayKeys);
                    return;
                }

                // 无修复 ⇒ 每局仍留一行精简汇总，证明补丁执行过；不再列 12 个空占位键
                MFLog.Info(LogCat.UI, "[MoreFood/Loc] 管理菜单构造后显示层兜底：修复 0 条（扫描 "
                                    + LocalGapRepair.LastDisplayScanned + " 个 LocText；本次 MISSING "
                                    + LocalGapRepair.LastDisplayBad + " 条；累计拦下 "
                                    + LocalGapRepair.DisplayBlockedTotal + " 条）"
                                    + (string.IsNullOrEmpty(LocalGapRepair.LastDisplayKeys)
                                       ? "" : "；" + LocalGapRepair.LastDisplayKeys));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, "[MoreFood/Loc] 管理菜单显示层兜底异常（已忽略）");
            }
        }
    }
}
