using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    // 战斗存档载体（§17 + K4/K11）

    /// <summary>★★★ 2026-10-06【探针定案】存档数据宿主实体。
    internal sealed class SpaceSaveHost : KMonoBehaviour
    {
        internal const string HostPrefabId = "SpaceSaveHost";
        private static GameObject prefab;

        /// <summary>★★ 载入完成闸：站点实体等"随档对象"在【载入途中】就会调
        internal static bool LoadFinished;

        internal static void RegisterPrefab()
        {
            if (prefab != null)
            {
                return;
            }
            GameObject go = EntityTemplates.CreateEntity(HostPrefabId, "SpaceSaveHost", true);
            SaveLoadRoot slr = go.AddOrGet<SaveLoadRoot>();
            slr.SetRegistered(true);              // ★ 必须显式注册（C-09 的 default 反向：我们的怪要 false，这里要 true）
            go.AddOrGet<SpaceSaveHost>();
            go.AddOrGet<TradeSaveData>();
            go.AddOrGet<CombatSaveData>();
            go.SetActive(false);
            Assets.AddPrefab(go.GetComponent<KPrefabID>());
            prefab = go;
            MFLog.Info(LogCat.Save,"[Space] 注册 prefab " + HostPrefabId + "（存档宿主，随档保存）");
        }

        /// <summary>数据量打分：多个宿主时挑"正主"（空壳=0）。</summary>
        private int DataScore()
        {
            int n = 0;
            TradeSaveData t = GetComponent<TradeSaveData>();
            if (t != null)
            {
                n += t.stationIds.Count + t.questActive.Count + t.delivered.Count + t.monsterIds.Count;
            }
            CombatSaveData c = GetComponent<CombatSaveData>();
            if (c != null)
            {
                n += c.combatMonsterIds.Count;
                if (!string.IsNullOrEmpty(c.debugMark))
                {
                    n += 1;
                }
            }
            return n;
        }

        /// <summary>场景里是否已有存档宿主实体（排除 prefab 资产）。
        internal static bool HasSceneHost()
        {
            SpaceSaveHost[] all = Resources.FindObjectsOfTypeAll<SpaceSaveHost>();
            for (int i = 0; i < all.Length; i++)
            {
                SpaceSaveHost h = all[i];
                if (h != null && h.gameObject != null && h.gameObject != prefab
                    && h.gameObject.scene.IsValid())
                {
                    return true;
                }
            }
            return false;
        }

        internal static SpaceSaveHost FindOrCreate()
        {
            // ① 先扫【所有】宿主（含未激活；排除 prefab 资产），选数据最多的那个
            SpaceSaveHost[] all = Resources.FindObjectsOfTypeAll<SpaceSaveHost>();
            SpaceSaveHost best = null;
            int bestScore = -1;
            for (int i = 0; i < all.Length; i++)
            {
                SpaceSaveHost h = all[i];
                if (h == null || h.gameObject == null || h.gameObject == prefab
                    || !h.gameObject.scene.IsValid())
                {
                    continue;
                }
                int score = h.DataScore();
                if (score > bestScore)
                {
                    bestScore = score;
                    best = h;
                }
            }
            if (best != null)
            {
                // 清掉多余的空壳宿主（旧档里被"载入中途"建出来的那种）
                for (int i = 0; i < all.Length; i++)
                {
                    SpaceSaveHost h = all[i];
                    if (h == null || h == best || h.gameObject == null || h.gameObject == prefab
                        || !h.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    if (h.DataScore() > 0)
                    {
                        continue;
                    }
                    MFLog.Info(LogCat.Save,"[Space] 清理空壳存档宿主 " + h.gameObject.name + "#" + h.gameObject.GetInstanceID());
                    Util.KDestroyGameObject(h.gameObject);
                }
                return best;
            }
            // ② 载入完成前不建（否则建出来的必然是空壳，还会被写进档）
            if (!LoadFinished)
            {
                return null;
            }
            if (prefab == null)
            {
                RegisterPrefab();
            }
            if (prefab == null)
            {
                return null;
            }
            GameObject go = Util.KInstantiate(prefab, null, null);
            if (go == null)
            {
                return null;
            }
            go.SetActive(true);
            SpaceSaveHost host = go.GetComponent<SpaceSaveHost>();
            MFLog.Info(LogCat.Save,"[Space] 新建存档宿主实体（档里没有 ⇒ 首次创建；此后随档保存）");
            return host;
        }
    }

    // ★★ 真根因（反编译实证）：KSerialization.SerializationTemplate 对没有
    internal sealed class CombatSaveData : KMonoBehaviour, ISaveLoadable
    {
        // 怪物（K4：我方权威；原版持久化已 SetRegistered(false) 关掉）
        [Serialize]
        public string debugMark = "";
        // 子图小行星带是否已铺过（每档一次）
        [Serialize]
        public bool beltSpawned;
        // 外层异常残骸带是否已做过一次性清理（见 星图.md P-33）
        [Serialize]
        public bool outerBeltsPurged;
        [Serialize]
        public List<string> combatMonsterIds = new List<string>();
        [Serialize]
        public List<int> combatMonsterUids = new List<int>();
        [Serialize]
        public List<int> combatMonsterHp = new List<int>();
        [Serialize]
        public List<int> combatMonsterArmor = new List<int>();
        [Serialize]
        public List<int> combatMonsterCellQ = new List<int>();
        [Serialize]
        public List<int> combatMonsterCellR = new List<int>();
        [Serialize]
        public List<string> combatMonsterRegion = new List<string>();
        [Serialize]
        public List<string> combatMonsterState = new List<string>();
        [Serialize]
        public List<string> combatMonsterQuestId = new List<string>();
        [Serialize]
        public List<int> combatMonsterOrdered = new List<int>();
        [Serialize]
        public List<int> combatMonsterFleeUsed = new List<int>();
        [Serialize]
        public List<string> combatMonsterAggroName = new List<string>();

        // 怪物闹钟（GameClock 绝对秒，§17.3）
        [Serialize]
        public List<float> monsterIdleUntil = new List<float>();
        [Serialize]
        public List<float> monsterRegenUntil = new List<float>();
        [Serialize]
        public List<float> monsterAttackUntil = new List<float>();
        [Serialize]
        public List<float> monsterDeaggroUntil = new List<float>();
        // ★ 离场闹钟 + 逃跑窗口（K18）：不带存档就会读档后闹钟断裂
        [Serialize]
        public List<float> monsterLeaveAt = new List<float>();
        [Serialize]
        public List<float> monsterFleeUntil = new List<float>();

        // 抽取器闹钟
        [Serialize]
        public float spawnerMainNextAt;
        [Serialize]
        public float spawnerSubNextAt;
        // ★ 暂停态（名额满 → 闹钟停表；空位由通知恢复）：不带存档就会读档后闹钟断裂
        [Serialize]
        public bool spawnerMainPaused;
        [Serialize]
        public float spawnerMainRemain;
        [Serialize]
        public bool spawnerSubPaused;
        [Serialize]
        public float spawnerSubRemain;

        // 一次性小行星带（K7/K11：条目按 field 连续存放，lootFieldGroups = 每带条目数）
        [Serialize]
        public List<int> lootFieldCellQ = new List<int>();
        [Serialize]
        public List<int> lootFieldCellR = new List<int>();
        [Serialize]
        public List<string> lootFieldSourceTags = new List<string>();
        [Serialize]
        public List<string> lootFieldLabels = new List<string>();
        [Serialize]
        public List<int> lootFieldGroups = new List<int>();
        [Serialize]
        public List<string> lootFieldItemIds = new List<string>();
        [Serialize]
        public List<float> lootFieldMasses = new List<float>();
        [Serialize]
        public List<int> lootFieldCounts = new List<int>();

        private static GameObject host;   // ★ 当前宿主（= SpaceSaveHost 实体）
        // ★★ 实测"写进 Clone 的载体、存档里却没有数据" ⇒ 被序列化的对象
        internal static readonly List<CombatSaveData> AllCarriers = new List<CombatSaveData>();
        private static bool mirroring;

        /// <summary>★ 2026-10-06：同 TradeSaveData.AttachTo —— 存档组件必须挂在
        internal static CombatSaveData Attach()
        {
            SpaceSaveHost hostEntity = SpaceSaveHost.FindOrCreate();
            if (hostEntity == null)
            {
                MFLog.Info(LogCat.Save,"[SAVE-TRACE] L3 Attach(战斗)：FindOrCreate=null");
                return null;
            }
            host = hostEntity.gameObject;
            MFLog.Info(LogCat.Save,"[SAVE-TRACE] L3 Attach(战斗)：宿主=" + host.name + "#" + host.GetInstanceID());
            CombatSaveData mine = host.AddOrGet<CombatSaveData>();
            if (mine != null && !AllCarriers.Contains(mine))
            {
                AllCarriers.Add(mine);
            }
            return mine;
        }

        // ★★★ 全链路打点：KSerialization 写完本组件字段后 / 读完本组件字段后回调这里。
        [OnSerialized]
        public void TraceOnSerialized()
        {
            MFLog.Info(LogCat.Save,"[SAVE-TRACE] S4 序列化通知(战斗)：怪物=" + combatMonsterIds.Count
                + " 探针=" + debugMark + " 载体#" + GetInstanceID()
                + "@" + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "?"));
        }

        [OnDeserialized]
        public void TraceOnDeserialized()
        {
            MFLog.Info(LogCat.Save,"[SAVE-TRACE] L4 反序列化通知(战斗)：怪物=" + combatMonsterIds.Count
                + " 探针=" + debugMark + " 载体#" + GetInstanceID()
                + "@" + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "?"));
        }

        internal void Capture()
        {
            if (!SpaceSaveHost.LoadFinished)
            {
                MFLog.Info(LogCat.Save,"[SAVE-TRACE] S2 Capture(战斗) 被闸掉：LoadFinished=false");
                return;
            }
            CombatRuntime.CaptureTo(this);
            debugMark = "SPACEDBG-" + (GameClock.Instance != null ? GameClock.Instance.GetCycle().ToString() : "?")
                + "-M" + combatMonsterIds.Count;
            MFLog.Info(LogCat.Save,"[SAVE-TRACE] S3 捕获落点(战斗)：怪物=" + combatMonsterIds.Count + " 探针="
                + debugMark + " 载体#" + GetInstanceID() + "@"
                + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "(null)"));
            MFLog.Info(LogCat.Save,"[Space] 战斗存档捕获：怪物 " + combatMonsterIds.Count + " 只，小行星带 "
                + lootFieldCellQ.Count + " 个（载体实例 " + GetInstanceID() + "，宿主="
                + (gameObject != null ? gameObject.name + "#" + gameObject.GetInstanceID() : "(null)") + "）");
            // ★ 镜像：把同一份数据写进所有挂过载体的实例（谁被序列化都不落空）
            if (!mirroring)
            {
                mirroring = true;
                try
                {
                    for (int i = 0; i < AllCarriers.Count; i++)
                    {
                        CombatSaveData c = AllCarriers[i];
                        if (c != null && c != this)
                        {
                            CombatRuntime.CaptureTo(c);
                            MFLog.Info(LogCat.Save,"[Space] 战斗存档镜像捕获 → 宿主="
                                + (c.gameObject != null ? c.gameObject.name + "#" + c.gameObject.GetInstanceID() : "(null)"));
                        }
                    }
                }
                finally
                {
                    mirroring = false;
                }
            }
        }

        internal void Restore()
        {
            CombatRuntime.RestoreFrom(this);
        }
    }
}
