// 用途：增益持久化：存档快照、恢复与属性修改

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
    internal static partial class SpaceRealEventRuntime
    {
        // 持续型 buff 落盘：id + 数值 + 剩余时间；只记 6 个跨时间事件
        private static readonly string[] PersistentBuffIds = new string[]
        {
            SpaceRealEvents.GravityKey,
            SpaceRealEvents.FertileKey,
            SpaceRealEvents.SporeKey,
            SpaceRealEvents.MagneticKey,
            SpaceRealEvents.DrySeasonKey,
            SpaceRealEvents.CosmicKey,
        };

        // 外部复位期间禁止落盘：buff 是被强制清掉的，落盘会清空存档快照
        private static bool suppressPersist;
        // 落盘条数变化时才打日志（BeginBuff/EndBuff 调用频繁，避免刷屏）
        private static int lastPersistCount = -1;

        internal static void PersistBuffs()
        {
            try
            {
                if (suppressPersist)
                {
                    return;
                }
                EventSaveData data = EventSaveData.Attach();
                if (data == null || data.buffId == null)
                {
                    return;
                }
                data.buffId.Clear();
                data.buffValue.Clear();
                data.buffRemain.Clear();
                float now = Now();
                for (int i = 0; i < buffs.Count; i++)
                {
                    RealBuff b = buffs[i];
                    if (b == null)
                    {
                        continue;
                    }
                    bool keep = false;
                    for (int k = 0; k < PersistentBuffIds.Length; k++)
                    {
                        if (PersistentBuffIds[k] == b.id)
                        {
                            keep = true;
                            break;
                        }
                    }
                    if (!keep)
                    {
                        continue;
                    }
                    float rem = b.endsAt - now;
                    if (rem <= 0f)
                    {
                        continue;
                    }
                    data.buffId.Add(b.id);
                    data.buffValue.Add(b.value);
                    data.buffRemain.Add(rem);
                }
                if (lastPersistCount != data.buffId.Count)
                {
                    lastPersistCount = data.buffId.Count;
                    MFLog.Verbose(LogCat.Event,"[Space] 持续 buff 落盘快照：" + data.buffId.Count + " 项");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc("space:persist", LogCat.Event, e, "[Space] 持续 buff 落盘失败");
            }
        }

        // 存档补写：按存档那刻重算剩余时间，保证读档倒计时准确
        internal static void PersistBuffsOnSave()
        {
            PersistBuffs();
            EventSaveData d = EventSaveData.Attach();
            int n = (d != null && d.buffId != null) ? d.buffId.Count : 0;
            MFLog.Verbose(LogCat.Event,"[Space] 存档补写持续 buff 快照：" + n + " 项");
        }

        internal static void EndBuffById(string id, string why, bool notify)
        {
            RealBuff b = FindBuff(id);
            if (b == null)
            {
                return;
            }
            EndBuff(b, why, notify);
            buffs.Remove(b);
        }

        internal static bool AddAttrMod(RealBuff b, GameObject go, string attrId, float value, bool multiplier)
        {
            if (b == null || go == null || string.IsNullOrEmpty(attrId))
            {
                return false;
            }
            try
            {
                Klei.AI.Attributes attrs = go.GetAttributes();
                if (attrs == null)
                {
                    return false;
                }
                if (attrs.Get(attrId) == null)
                {
                    return false;
                }
                Klei.AI.AttributeModifier mod = new Klei.AI.AttributeModifier(attrId, value, b.name, multiplier, false, false);
                attrs.Add(mod);
                if (string.IsNullOrEmpty(b.attrId))
                {
                    b.attrId = attrId;
                    b.attrMultiplier = multiplier;
                }
                RealAttrBinding bind = new RealAttrBinding();
                bind.go = go;
                bind.mod = mod;
                b.attrs.Add(bind);
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 事件属性加成失败（" + b.id + " @ " + go.name + "）");
                return false;
            }
        }

        private static void EndBuff(RealBuff b, string why, bool notify)
        {
            if (b == null)
            {
                return;
            }
            try
            {
                for (int i = 0; i < b.attrs.Count; i++)
                {
                    RealAttrBinding bind = b.attrs[i];
                    if (bind == null || bind.go == null || bind.mod == null)
                    {
                        continue;
                    }
                    try
                    {
                        Klei.AI.Attributes attrs = bind.go.GetAttributes();
                        if (attrs != null)
                        {
                            attrs.Remove(bind.mod);
                        }
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Event, e, "[Space] 事件属性回滚失败（" + b.id + "）");
                    }
                }
                if (b.kind == "dry")
                {
                    for (int i = 0; i < b.attrs.Count; i++)
                    {
                        if (b.attrs[i] != null)
                        {
                            SpaceRealEvents.RefreshIrrigation(b.attrs[i].go);
                        }
                    }
                }
                b.attrs.Clear();
                // ★ 摘掉补挂登记（buff 已结束，后续新目标不再挂它）
                SpaceRealEventBuffHub.Unwatch(b);
                // ★ 孢子云：还原被放大的 DiseaseDropper 配置（含共享 Def 场景，只还原一次）
                if (b.droppers.Count > 0)
                {
                    for (int i = 0; i < b.droppers.Count; i++)
                    {
                        RealDropperBinding db = b.droppers[i];
                        if (db == null || db.def == null)
                        {
                            continue;
                        }
                        db.def.singleEmitQuantity = db.singleEmitQuantity;
                        db.def.averageEmitPerSecond = db.averageEmitPerSecond;
                    }
                    b.droppers.Clear();
                }
                if (b.germs.Count > 0)
                {
                    for (int i = 0; i < b.germs.Count; i++)
                    {
                        RealGermBackup gb = b.germs[i];
                        if (gb == null || gb.disease == null || gb.info == null)
                        {
                            continue;
                        }
                        Array.Copy(gb.info, gb.disease.elemGrowthInfo, gb.info.Length);
                    }
                    b.germs.Clear();
                    SpaceRealEvents.PushDiseaseTable();
                }
                for (int i = 0; i < b.stations.Count; i++)
                {
                    SpaceStation st = b.stations[i];
                    if (st == null)
                    {
                        continue;
                    }
                    try
                    {
                        MFLog.Verbose(LogCat.Event,"[Space] 临时站点到期销毁 " + st.stationId + " @" + SpaceCfg.Pos(st.Location));
                        Util.KDestroyGameObject(st.gameObject);
                    }
                    catch (Exception e)
                    {
                        MFLog.Verbose(LogCat.Event, e, "[Space] 临时站点销毁失败");
                    }
                }
                b.stations.Clear();
                if (b.kind == "magnetic")
                {
                    MagneticStormMul = 1f;
                }
                else if (b.kind == "cosmic")
                {
                    CosmicRadMul = 1f;
                }
                MFLog.Verbose(LogCat.Event,"[Space] 真实事件结束 " + b.id + "（" + why + "）");
                if (notify)
                {
                    try
                    {
                        TradeUi.Notify(b.name + " 已结束", "异象消退，效果与数值已恢复原状");
                    }
                    catch (Exception)
                    {
                        // expected: 结束提示属可选 UI，通知失败不影响事件收尾与数值恢复
                    }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 事件收尾异常（" + b.id + "）");
            }
            finally
            {
                PersistBuffs();
            }
        }
    }
}
