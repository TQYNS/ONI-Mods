// 用途：事件绑定数据：属性/掉落/病菌备份、增益与货运行

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
    internal sealed class RealAttrBinding
    {
        internal GameObject go;
        internal Klei.AI.AttributeModifier mod;
    }

    // 孢子云：被放大的 DiseaseDropper 配置（含原值，供结束时还原）
    internal sealed class RealDropperBinding
    {
        internal DiseaseDropper.Def def;
        internal int singleEmitQuantity;
        internal int averageEmitPerSecond;
    }

    internal sealed class RealGermBackup
    {
        internal Disease disease;
        internal ElemGrowthInfo[] info;
    }

    internal sealed class RealBuff
    {
        internal string id;
        internal string name;
        internal string kind;
        internal float endsAt;
        internal float value;
        internal string attrId;
        internal bool attrMultiplier;
        internal string owner;
        internal readonly List<RealAttrBinding> attrs = new List<RealAttrBinding>();
        internal readonly List<RealGermBackup> germs = new List<RealGermBackup>();
        internal readonly List<SpaceStation> stations = new List<SpaceStation>();
        internal readonly List<RealDropperBinding> droppers = new List<RealDropperBinding>();
        // 已挂载目标（Unity 实例 id）由 BuffTargetHub 维护：去重 + 重复回调兜底
        internal readonly HashSet<int> boundIds = new HashSet<int>();
    }

    internal sealed class RealFreighterRun
    {
        internal string stationId;
        internal SpaceStation station;
        internal int phase;
        internal AxialI target;
        internal AxialI exit;
        internal float nextStepAt;
        internal float dockUntil;
        internal int steps;
        internal float hardStopAt;
    }
}
