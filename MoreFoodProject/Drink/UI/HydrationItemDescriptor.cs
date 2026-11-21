// 用途：给带饮水值档案的物品（饮品或食物）在详情面板加「饮水 / 效果」栏。
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 详情面板描述符：取数只认 ConsumeRegistry，不依赖饮品行为表。
    public class HydrationItemDescriptor : KMonoBehaviour, IGameObjectEffectDescriptor
    {
        public void CollectDescriptors(GameObject go, List<Descriptor> result)
        {
            if (go == null || result == null) return;

            var kp = go.GetComponent<KPrefabID>();
            if (kp == null) return;

            ConsumeProfile data = ConsumeRegistry.Find(kp.PrefabTag);
            if (data == null) return;

            if (data.hydrationPerKg > 0f)
            {
                float serve = 1f;
                if (data.isDrink)
                {
                    serve = Mathf.Min(data.maxServeMass, 1f);
                }
                else
                {
                    var pe = go.GetComponent<PrimaryElement>();
                    if (pe != null && pe.MassPerUnit > 0f) serve = pe.MassPerUnit;
                }
                Add(result, "饮水 +" + Mathf.RoundToInt(data.hydrationPerKg * serve),
                    data.isDrink ? "饮用后回复复制人的饮水需求" : "食用后回复复制人的饮水需求");
            }

            if (data.isDrink && !Mathf.Approximately(data.moraleBonus, 0f))
            {
                Add(result, "士气 " + data.moraleBonus.ToString("+0.#;-0.#"),
                    "饮用后的士气加成");
            }

            if (!string.IsNullOrEmpty(data.effectId))
            {
                var def = BuffConfig.Get(data.effectId);
                string name = def != null ? def.name : data.effectId;

                string detail;
                if (!CountedBuffDisplay.TryBuildTooltip(data.effectId, go, out detail))
                {
                    detail = def != null ? def.Summary() : name;
                    if (def != null)
                    {
                        string line = def.BehaviorLine();
                        if (!string.IsNullOrEmpty(line)) detail += "\n" + line;
                    }
                }

                Add(result, "效果：" + name, detail);
            }
        }

        private static void Add(List<Descriptor> list, string text, string tooltip)
        {
            Descriptor d = default(Descriptor);
            d.SetupDescriptor(text, string.IsNullOrEmpty(tooltip) ? text : tooltip,
                              Descriptor.DescriptorType.Effect);
            list.Add(d);
        }

        // 挂载：只给已登记饮水值档案的 prefab 挂组件。
        public static void AttachIfProfile(GameObject go)
        {
            if (go == null) return;
            var kp = go.GetComponent<KPrefabID>();
            if (kp == null) return;
            if (ConsumeRegistry.Find(kp.PrefabTag) == null) return;
            go.AddOrGet<HydrationItemDescriptor>();
            MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 已给 " + kp.PrefabTag.Name + " 挂「饮水」栏提供者");
        }
    }

    [HarmonyPatch(typeof(FoodConfigs), "CreatePrefabs")]
    public static class HydrationDescriptor_FoodPrefabs_Patch
    {
        public static void Postfix(List<GameObject> __result)
        {
            try
            {
                if (__result == null) return;
                for (int i = 0; i < __result.Count; i++)
                {
                    HydrationDescriptorBootstrap.Track(__result[i]);
                    HydrationItemDescriptor.AttachIfProfile(__result[i]);
                }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:HydrationDescriptor_FoodPrefabs_Patch", e); }
        }
    }

    [HarmonyPatch(typeof(IngredientConfigs), "CreatePrefabs")]
    public static class HydrationDescriptor_IngredientPrefabs_Patch
    {
        public static void Postfix(List<GameObject> __result)
        {
            try
            {
                if (__result == null) return;
                for (int i = 0; i < __result.Count; i++)
                {
                    HydrationDescriptorBootstrap.Track(__result[i]);
                    HydrationItemDescriptor.AttachIfProfile(__result[i]);
                }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:HydrationDescriptor_IngredientPrefabs_Patch", e); }
        }
    }

    // 时序兜底：yaml 晚于 prefab 注册时补挂。
    public static class HydrationDescriptorBootstrap
    {
        private static readonly List<GameObject> _pending = new List<GameObject>();

        public static void Track(GameObject go)
        {
            if (go == null) return;
            if (_pending.Contains(go)) return;
            _pending.Add(go);
        }

        public static void RetryAttachAll()
        {
            int n = 0;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var go = _pending[i];
                if (go == null) { _pending.RemoveAt(i); continue; }
                if (go.GetComponent<HydrationItemDescriptor>() != null) { _pending.RemoveAt(i); continue; }
                var kp = go.GetComponent<KPrefabID>();
                if (kp == null || ConsumeRegistry.Find(kp.PrefabTag) == null) continue;
                HydrationItemDescriptor.AttachIfProfile(go);
                n++;
            }
            if (n > 0) MFLog.Info(LogCat.Drink, "[MoreFood/Drink] 补挂「饮水」栏提供者 " + n + " 个");
        }
    }
}
