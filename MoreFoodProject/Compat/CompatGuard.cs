// CompatGuard.cs — 兼容补丁的第三方存在性检测（只按程序集名/类型名字符串判断，不引用对方程序集）

using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 第三方 Mod 存在性守卫：不加载、不依赖对方 DLL，仅在需要时按名字探测
    internal static class CompatGuard
    {
        private const string RonivansAssembly = "RonivansLegacy_ChemicalProcessing";
        private const string RonivansLocUtilType = "UtilLibs.LocalisationUtil";

        private static int _ronivansState;   // 0=未知 1=存在 -1=不存在

        // Ronivans Legacy - Industrial Revolution 是否已加载（结果缓存，避免重复遍历程序集）
        internal static bool HasRonivansLegacy()
        {
            if (_ronivansState != 0) return _ronivansState > 0;
            _ronivansState = DetectRonivans() ? 1 : -1;
            return _ronivansState > 0;
        }

        private static bool DetectRonivans()
        {
            try
            {
                if (AccessTools.TypeByName(RonivansLocUtilType) != null) return true;

                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var a = asms[i];
                    if (a == null) continue;
                    string n;
                    try { n = a.GetName().Name; } catch { continue; }
                    if (string.Equals(n, RonivansAssembly, StringComparison.Ordinal)) return true;
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Patch, e, "[MoreFood/Compat] 探测 Ronivans Legacy 失败（按不存在处理）");
            }
            return false;
        }
    }
}