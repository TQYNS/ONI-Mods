// RonivansLegacy_Compat.cs — 兼容补丁：在第三方本地化之后再做一次词条缺口收口；定案见 星图.md P-23

using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class RonivansLegacyCompat
    {
        private const string TAG = "[MoreFood/Compat]";

        private static bool _installed;   // 是否已尝试装配
        private static bool _disabled;    // 熔断：修复失败后彻底回退默认行为

        // 手动装配：特性式 [HarmonyPatch] 在目标缺失时会让 PatchAll 抛异常、拖垮整个 Mod 装载
        internal static void Install(Harmony harmony)
        {
            if (_installed) return;
            _installed = true;

            try
            {
                // 通用缺口修复交框架收口（框架侧自带同一时机的补丁，此处为兼容场景的显式触发）
                LocalGapRepair.Install(harmony);

                var t = AccessTools.TypeByName("Localization");
                if (t == null) { Debug.LogWarning(TAG + " 找不到 Localization，跳过兼容补丁"); return; }

                var m = AccessTools.Method(t, "Initialize");
                if (m == null) { Debug.LogWarning(TAG + " 找不到 Localization.Initialize，跳过兼容补丁"); return; }

                var pi = AccessTools.Method(typeof(RonivansLegacyCompat), nameof(Postfix));
                if (pi == null) { Debug.LogWarning(TAG + " 找不到自身 Postfix，跳过兼容补丁"); return; }

                harmony.Patch(m, postfix: new HarmonyMethod(pi));
                MFLog.Info(LogCat.General, TAG + " 兼容补丁已挂：Localization.Initialize");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, TAG + " 兼容补丁安装失败（已放弃，等同无补丁）");
            }
        }

        // 在对方之后执行：Priority.Last 由该方法上的特性被 HarmonyMethod 读取
        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (_disabled) return;

            try
            {
                if (!CompatGuard.HasRonivansLegacy()) return;   // 对方不存在 ⇒ 等同无补丁

                int n = LocalGapRepair.RepairNow();
                if (n > 0) MFLog.Info(LogCat.General, TAG + " Ronivans Legacy 在场：已回填词条 " + n + " 条（修复顶部栏 Missing）");
            }
            catch (Exception e)
            {
                _disabled = true;                                // 熔断：只尝试一次，之后回退默认行为
                MFLog.Exc(LogCat.Patch, e, TAG + " 词条修复异常，已熔断（回退默认行为，不影响游戏）");
            }
        }
    }
}