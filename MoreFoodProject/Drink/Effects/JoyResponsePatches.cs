using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class JoyResponseRecGate
    {
        // 强制让某个喜悦反应状态机立刻进入「狂喜」
        internal static bool Force(StateMachine.Instance smi)
        {
            try
            {
                if (smi == null) return false;
                return Force(smi.gameObject);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // 强制让某个复制人立刻进入狂喜（找到它的 JoyBehaviourMonitor 再 Force）
        internal static bool Force(GameObject go)
        {
            try
            {
                if (go == null) return false;
                DrinkEffectState st = go.GetComponent<DrinkEffectState>();
                return st != null && st.joySwaps != null && st.joySwaps.Count > 0;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(BalloonStandCellSensor), "IsAllowed")]
    internal static class JoyGateBalloonSensor
    {
        private static bool Prefix(BalloonStandCellSensor __instance, ref bool __result)
        {
            try
            {
                if (__instance == null || !JoyResponseRecGate.Force(__instance.gameObject)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateBalloonSensor", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BalloonArtistChore.StatesInstance), "HasBalloonStallCell")]
    internal static class JoyGateBalloonStallCell
    {
        private static bool Prefix(BalloonArtistChore.StatesInstance __instance, ref bool __result)
        {
            try
            {
                if (__instance == null || !JoyResponseRecGate.Force(__instance.gameObject)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateBalloonStallCell", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BalloonArtistChore.StatesInstance), "GetBalloonStallCell")]
    internal static class JoyGateBalloonStallWalkCell
    {
        private static bool Prefix(BalloonArtistChore.StatesInstance __instance, ref int __result)
        {
            try
            {
                if (__instance != null && JoyResponseRecGate.Force(__instance.gameObject))
                {
                    int walk;
                    int stand;
                    if (JoyStallSpot.TryPick(__instance.gameObject, out walk, out stand))
                    {
                        __result = walk;
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateBalloonStallWalkCell", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BalloonArtistChore.StatesInstance), "GetBalloonStallTargetCell")]
    internal static class JoyGateBalloonStandCell
    {
        private static bool Prefix(BalloonArtistChore.StatesInstance __instance, ref int __result)
        {
            try
            {
                if (__instance != null && JoyResponseRecGate.Force(__instance.gameObject))
                {
                    int walk;
                    int stand;
                    if (JoyStallSpot.TryPick(__instance.gameObject, out walk, out stand))
                    {
                        __result = stand;
                        return false;
                    }
                }
                return true;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateBalloonStandCell", e);
                return true;
            }
        }
    }

    internal static class JoyStallSpot
    {
        // 借出喜悦表现时挑一个可用落脚点（walkCell / standCell）
        internal static bool TryPick(GameObject dupe, out int walkCell, out int standCell)
        {
            walkCell = Grid.InvalidCell;
            standCell = Grid.InvalidCell;
            try
            {
                if (dupe == null) return false;
                int cell = Grid.PosToCell(dupe);
                if (!Grid.IsValidCell(cell)) return false;
                walkCell = cell;

                int right1 = Grid.CellRight(cell);
                int right2 = Grid.CellRight(right1);
                if (Usable(right1) && Usable(right2))
                {
                    standCell = right2;
                    return true;
                }
                int left1 = Grid.CellLeft(cell);
                int left2 = Grid.CellLeft(left1);
                if (Usable(left1) && Usable(left2))
                {
                    standCell = left2;
                    return true;
                }
                standCell = cell;
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool Usable(int cell)
        {
            if (!Grid.IsValidCell(cell)) return false;
            if (Grid.Solid[cell]) return false;
            int below = Grid.CellBelow(cell);
            return Grid.IsValidCell(below) && Grid.Solid[below];
        }
    }

    [HarmonyPatch(typeof(BalloonArtist.Instance), "IsRecTime")]
    internal static class JoyGateBalloonArtistSmi
    {
        private static bool Prefix(BalloonArtist.Instance __instance, ref bool __result)
        {
            try
            {
                if (!JoyResponseRecGate.Force(__instance)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateBalloonArtistSmi", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(BalloonArtistChore.StatesInstance), "IsRecTime")]
    internal static class JoyGateBalloonArtistChore
    {
        private static bool Prefix(BalloonArtistChore.StatesInstance __instance, ref bool __result)
        {
            try
            {
                if (!JoyResponseRecGate.Force(__instance)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateBalloonArtistChore", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(DataRainer.Instance), "IsRecTime")]
    internal static class JoyGateDataRainerSmi
    {
        private static bool Prefix(DataRainer.Instance __instance, ref bool __result)
        {
            try
            {
                if (!JoyResponseRecGate.Force(__instance)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateDataRainerSmi", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(DataRainerChore.StatesInstance), "IsRecTime")]
    internal static class JoyGateDataRainerChore
    {
        private static bool Prefix(DataRainerChore.StatesInstance __instance, ref bool __result)
        {
            try
            {
                if (!JoyResponseRecGate.Force(__instance)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateDataRainerChore", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(RoboDancer.Instance), "IsRecTime")]
    internal static class JoyGateRoboDancerSmi
    {
        private static bool Prefix(RoboDancer.Instance __instance, ref bool __result)
        {
            try
            {
                if (!JoyResponseRecGate.Force(__instance)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateRoboDancerSmi", e);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(RoboDancerChore.StatesInstance), "IsRecTime")]
    internal static class JoyGateRoboDancerChore
    {
        private static bool Prefix(RoboDancerChore.StatesInstance __instance, ref bool __result)
        {
            try
            {
                if (!JoyResponseRecGate.Force(__instance)) return true;
                __result = true;
                return false;
            }
            catch (System.Exception e)
            {
                MFLog.HotFail("patch:JoyGateRoboDancerChore", e);
                return true;
            }
        }
    }
}
