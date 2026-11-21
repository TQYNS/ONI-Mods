// 用途：事件计时器：驱动真实事件的秒级节拍

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
    public class SpaceRealEventTicker : KMonoBehaviour
    {
        private void Update()
        {
            try
            {
            SpaceRealEventRuntime.Tick();
            }
            catch (System.Exception e) { MFLog.HotFail("update:SpaceRealEvents", e); }
        }
    }
}
