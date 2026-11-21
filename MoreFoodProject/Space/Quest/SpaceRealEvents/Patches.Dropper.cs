// 用途：投掷器补丁：事件开始时的掉落检查

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
    // 读档时机由框架 OnRestored 提供，不硬挂 Harmony（签名变动会禁用 mod）

    // 孢子云补挂：被动订阅不扫描；手动挂载避免 PatchAll 失败禁用 mod
    internal static class SpaceRealEventsDropperStartPatch
    {
        private static void Postfix(DiseaseDropper.Instance __instance)
        {
            try
            {
                if (__instance == null || !SpaceRealEventBuffHub.AnyWatch(BuffChannel.Droppers))
                {
                    return;
                }
                GameObject go = __instance.gameObject;
                if (go == null || !SpaceRealEvents.IsSporePrefab(go))
                {
                    return;
                }
                SpaceRealEventBuffHub.OnTargetAppeared(go, BuffChannel.Droppers);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 产病菌植物补挂异常");
            }
        }
    }
}
