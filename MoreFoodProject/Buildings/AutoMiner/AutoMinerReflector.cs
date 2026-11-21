// Buildings/AutoMiner/AutoMinerReflector.cs — 原生 AutoMiner 私有字段的统一反射入口（静态缓存）

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class AutoMinerReflector
    {
        private static readonly FieldInfo DigCellField = AccessTools.Field(typeof(AutoMiner), "dig_cell");
        private static readonly FieldInfo RotationCompleteField = AccessTools.Field(typeof(AutoMiner), "rotation_complete");
        private static readonly FieldInfo ArmGoField = AccessTools.Field(typeof(AutoMiner), "arm_go");
        private static readonly FieldInfo ArmAnimCtrlField = AccessTools.Field(typeof(AutoMiner), "arm_anim_ctrl");

        internal static int GetDigCell(AutoMiner miner)
        {
            if (DigCellField == null) return Grid.InvalidCell;
            object v = DigCellField.GetValue(miner);
            return v is int ? (int)v : Grid.InvalidCell;
        }

        internal static void SetDigCell(AutoMiner miner, int cell)
        {
            if (DigCellField != null) DigCellField.SetValue(miner, cell);
        }

        internal static bool GetRotationComplete(AutoMiner miner)
        {
            if (RotationCompleteField == null) return false;
            object v = RotationCompleteField.GetValue(miner);
            return v is bool && (bool)v;
        }

        internal static void SetRotationComplete(AutoMiner miner, bool value)
        {
            if (RotationCompleteField != null) RotationCompleteField.SetValue(miner, value);
        }

        // 同步激光 clip 到当前目标格；原生 UpdateDig 被采集 Prefix 跳过后必须手动同步（详见生态.md §6.2）
        internal static void SyncLaserClip(AutoMiner miner, int cell)
        {
            if (miner == null || !Grid.IsValidCell(cell)) return;
            if (ArmGoField == null || ArmAnimCtrlField == null) return;
            try
            {
                var armGo = ArmGoField.GetValue(miner) as GameObject;
                var armCtrl = ArmAnimCtrlField.GetValue(miner) as KBatchedAnimController;
                if (armGo == null || armCtrl == null) return;
                Vector3 target = Grid.CellToPosCCC(cell, Grid.SceneLayer.FXFront2);
                target.z = 0f;
                Vector3 armPos = armGo.transform.position;   // 等同原生 GetPosition()
                armPos.z = 0f;
                float distSq = (target - armPos).sqrMagnitude;
                armCtrl.GetBatchInstanceData().SetClipRadius(armPos.x, armPos.y, distSq, true);
            }
            catch (Exception e) { MFLog.HotFail("autominer.laserclip", e); }
        }
    }
}
