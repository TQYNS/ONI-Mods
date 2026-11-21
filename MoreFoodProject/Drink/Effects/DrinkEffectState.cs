using System;
using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class DrinkEffectState : KMonoBehaviour
    {
        [Serialize] public List<string> suppressedTraits = new List<string>();
        [Serialize] public List<string> addedTraits = new List<string>();
        [Serialize] public List<string> grantedSkills = new List<string>();
        [Serialize] public List<string> curedTraits = new List<string>();
        [Serialize] public List<string> joySwaps = new List<string>();
        // 茅台「超级生产力」命中标记：true 期间瞬间完成工作概率按 30% 计算
        [Serialize] public bool moutaiSuperBoost;

        // Unity 回调：实例生成
        protected override void OnSpawn()
        {
            try
            {
            base.OnSpawn();
            if (suppressedTraits == null) suppressedTraits = new List<string>();
            if (addedTraits == null) addedTraits = new List<string>();
            if (grantedSkills == null) grantedSkills = new List<string>();
            if (curedTraits == null) curedTraits = new List<string>();
            if (joySwaps == null) joySwaps = new List<string>();
            }
            catch (System.Exception e) { MFLog.HotFail("spawn:DrinkEffectState", e); }
        }

        // 拼存档键：buffId + 值（形如 "<buffId>|<value>"）
        public static string MakeKey(string buffId, string value)
        {
            return (buffId ?? "") + "|" + (value ?? "");
        }

        // 从存档键里取出值部分
        public static string ValueOf(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            int cut = entry.IndexOf('|');
            return cut >= 0 && cut + 1 < entry.Length ? entry.Substring(cut + 1) : null;
        }

        // 从存档键里取出 buffId 部分
        public static string BuffIdOf(string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            int cut = entry.IndexOf('|');
            return cut > 0 ? entry.Substring(0, cut) : null;
        }

        // 往存档列表里加一条（按"键"去重）
        public void AddUnique(List<string> list, string buffId, string value)
        {
            if (list == null) return;
            string key = MakeKey(buffId, value);
            if (!list.Contains(key)) list.Add(key);
        }

        // 从存档列表里按 buffId + 值移除一条
        public void RemoveEntry(List<string> list, string buffId, string value)
        {
            if (list == null) return;
            list.Remove(MakeKey(buffId, value));
        }

        // 取出该 buffId 名下的全部条目并从列表移除（回收用）
        public List<string> TakeByPrefix(List<string> list, string buffId)
        {
            var taken = new List<string>();
            if (list == null) return taken;
            string prefix = (buffId ?? "") + "|";
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null && list[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    taken.Add(list[i]);
                    list.RemoveAt(i);
                }
            }
            return taken;
        }
    }

    [HarmonyPatch(typeof(MinionConfig), "CreatePrefab", new Type[0])]
    public static class DrinkEffectState_MinionPrefab_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(GameObject __result)
        {
            try
            {
                if (__result != null) __result.AddOrGet<DrinkEffectState>();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkEffectState_MinionPrefab_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(BionicMinionConfig), "CreatePrefab", new Type[0])]
    public static class DrinkEffectState_BionicPrefab_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(GameObject __result)
        {
            try
            {
                if (__result != null) __result.AddOrGet<DrinkEffectState>();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkEffectState_BionicPrefab_Patch", e); }
        }
    }
}
