// DebugHotkeyGuard.cs — 数字输入期间屏蔽游戏调试快捷键（退格默认绑定 Action.DebugToggle，会与输入框抢键）
using System;
using HarmonyLib;

namespace MoreFood_KFC
{
    [HarmonyPatch(typeof(DebugHandler), "OnKeyDown")]
    public static class DebugHandler_OnKeyDown_Patch
    {
        // Harmony 补丁：Prefix（无参）——输入框编辑中跳过调试应答，返回 true 表示继续原逻辑
        public static bool Prefix()
        {
            try
            {
                return !TradeUi.NumberBox.IsEditing;
            }
            catch (Exception e) { MFLog.HotFail("patch:DebugHotkeyGuard", e); return true; }
        }
    }
}
