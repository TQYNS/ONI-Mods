// DrinkConfig.cs — 饮水系统 · 工厂【机制层】
using HarmonyLib;
using Klei;
using Klei.AI;
using KSerialization;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public class DrinkData : ConsumeProfile
    {
        public DrinkData()
        {
            hydrationPerKg = 50f;
            maxServeMass = 2f;
            isDrink = true;
        }

        // ★ 饮水优先级（数值越大越优先）—— 影响【选哪个喝】。
        public int priority = 0;

        // ★ 显示排序（升序比较大小）—— 影响【在「饮品」管制表里排前面还是后面】。
        public int sort = 0;

        public string category = "other";        // wine / coffee / soda / other
        public bool garnish = false;             // 是否启用上桌调料（YAML 配置）
        public string garnishAnim = null;        // garnish_N_kanim
        // 上桌调料挂的符号槽
        public string garnishSymbol = "object";
        // 调料消耗速率
        public float garnishConsumeRate = 0.5f;
        // 调料优先级
        public int garnishPriority = 0;
        public float hangoverChance = 0f;        // 0~1，饮用时判定
    }

    // ★ 饮品选择算法 —— 独立函数，方便随时改
    public static class DrinkPriorityPicker
    {
        // 按优先级挑一个候选，返回下标；没有可用候选返回 -1。
        public static int PickIndex(int count, Func<int, int> priorityAt, Func<int, bool> isUsable = null)
        {
            if (count <= 0 || priorityAt == null) return -1;

            int best = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (isUsable != null && !isUsable(i)) continue;
                int p = priorityAt(i);
                if (p > best) best = p;
            }
            if (best == int.MinValue) return -1;

            // 蓄水池抽样：在最高档里等概率取一个
            int seen = 0, chosen = -1;
            for (int i = 0; i < count; i++)
            {
                if (isUsable != null && !isUsable(i)) continue;
                if (priorityAt(i) != best) continue;
                seen++;
                if (UnityEngine.Random.Range(0, seen) == 0) chosen = i;
            }
            return chosen;
        }
    }

    public abstract class DrinkTarget
    {
        // 目标实体
        public GameObject go;
        // 走过去的落点组件
        public KMonoBehaviour approachTarget;
        // 该饮品的数值
        public DrinkData data;
        // 现存量（kg）
        public float availableMass;
        // 货源显示名
        public string sourceName = "";

        // 饮品结算：饮水值走统一入口，士气/宿醉/特殊触发只属于饮品。
        protected static void Settle(GameObject worker, DrinkData data, float mass)
        {
            if (worker == null || data == null || mass <= 0f) return;

            HydrationSettlement.Apply(worker, data.hydrationPerKg, mass);

            var effects = worker.GetComponent<Effects>();
            if (effects == null) return;

            if (!string.IsNullOrEmpty(data.effectId) && Db.Get().effects.Exists(data.effectId))
            {
                if (BuffManager.Apply(worker, data.effectId))
                    MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink] " + worker.name + " 获得效果 " + data.effectId);
                DrinkEffectBehaviorRegistry.OnDrink(worker, data.effectId);
            }

            DrinkHangoverEffect.TryApply(worker, data);

            string moraleId = Drinks.GetMoraleEffectId(data.moraleBonus);
            if (moraleId != null)
            {
                BuffConfig.BuildAll();
                BuffManager.Apply(worker, moraleId);
            }
        }
    }

    public class ItemDrinkTarget : DrinkTarget
    {
        // 可搬运物品（瓶 / 罐）
        public Pickupable pickupable;
        // 在世界登记表里的条目
        public DrinkWorldRegistry.Entry entry;

        // 按质量直接结算（不含取水流程）—— 供上桌调料 / 饮水 chore 复用
        public void SettleFromMass(GameObject worker, float mass)
        {
            if (entry != null) { entry.reserver = null; entry = null; }
            Settle(worker, data, mass);
        }
    }


    // 【已废弃，仅记录，不删除】手压泵（LiquidPumpingStation）链路已移除，实现与踩坑记录见 文档整理/饮品.md §5.10.1/§6.3/§12.4/§16。

    public class BuildingDrinkTarget : DrinkTarget
    {
        // 饮水机
        public WaterCooler cooler;
        // 计划喝多少 kg
        public float wantedKg;

        // 饮水机取水消费回调：扣水量并结算饮水
        public float Consume(GameObject worker)
        {
            if (cooler == null || worker == null) return 0f;

            var storage = cooler.GetComponent<Storage>();
            if (storage == null) return 0f;

            Tag tag = cooler.ChosenBeverage;
            if (!tag.IsValid) return 0f;

            float wantKg = this.wantedKg > 0f ? this.wantedKg : WaterCooler.DRINK_MASS;
            float availableKg = storage.GetMassAvailable(tag);
            if (availableKg <= 0f)
            {
                MFLog.Warn(LogCat.Drink, "[MoreFood/Drink] 饮水机里没有 " + tag.Name + "，放弃本次喝水");
                return 0f;
            }
            float actualKg = Mathf.Min(wantKg, availableKg);

            float consumed;
            SimUtil.DiseaseInfo diseaseInfo;
            float temp;
            try
            {
                storage.ConsumeAndGetDisease(tag, actualKg,
                                             out consumed, out diseaseInfo, out temp);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] 从饮水机 ConsumeAndGetDisease 异常");
                return 0f;
            }
            if (consumed <= 0f) return 0f;

            try
            {
                var germSmi = worker.GetSMI<GermExposureMonitor.Instance>();
                if (germSmi != null)
                {
                    germSmi.TryInjectDisease(diseaseInfo.idx, diseaseInfo.count, tag,
                                              Sickness.InfectionVector.Digestion);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] 病菌注入异常（已忽略）");
            }

            try
            {
                var effects = worker.GetComponent<Effects>();
                if (effects != null)
                {
                    for (int i = 0; i < WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS.Length; i++)
                    {
                        var tuple = WaterCoolerConfig.BEVERAGE_CHOICE_OPTIONS[i];
                        if (tag == tuple.first && !string.IsNullOrEmpty(tuple.second))
                        {
                            effects.Add(tuple.second, true);
                            break;
                        }
                    }
                    effects.Remove("Thirsty");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] 施加饮品效果异常（已忽略）");
            }

            Settle(worker, data, consumed);

            try
            {
                var heatSmi = cooler.GetSMI<HeatImmunityProvider.Instance>();
                if (heatSmi != null)
                {
                    heatSmi.ApplyImmunityEffect(worker, false);
                    MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink] 已触发抗寒效果链（HeatImmunityProvider）");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] 触发抗寒效果异常（已忽略）");
            }

            MFLog.Verbose(LogCat.Drink, "[MoreFood/Drink] " + worker.name + " 从饮水机喝了 "
                      + consumed.ToString("F3") + "kg " + tag.Name
                      + "（want=" + wantKg.ToString("F3")
                      + ", available=" + availableKg.ToString("F3") + "）");

            return consumed;
        }
    }

    public interface IDrinkSource
    {
        int Priority { get; }
        string Name { get; }
        DrinkTarget TryFind(GameObject duplicant, int rotation);
    }

    public static class DrinkSourceRegistry
    {
        public static readonly List<IDrinkSource> Sources = new List<IDrinkSource>();

        // 找最优饮水目标：按来源优先级（物品 > 建筑 > 桶装水）逐个试，返回第一个可用目标
        public static DrinkTarget FindBest(GameObject duplicant, int rotation)
        {
            Sources.Sort(delegate (IDrinkSource a, IDrinkSource b) { return a.Priority.CompareTo(b.Priority); });
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i] == null) continue;
                DrinkTarget t = Sources[i].TryFind(duplicant, rotation);
                if (t != null) return t;
            }
            return null;
        }

        public static void RegisterDefaults()
        {
            if (Sources.Count > 0) return;
            Sources.Add(new ItemDrinkSource());
            Sources.Add(new BuildingDrinkSource());
            Sources.Add(new BottledWaterDrinkSource());
        }
    }

// 目标级短时排除（刚喝完的目标冷却若干秒）
    public static class DrinkExclusionRegistry
    {
        // 默认排除时长（秒）
        public const float DEFAULT_BACKOFF_SECONDS = 15f;

        private static readonly Dictionary<GameObject, float> _until
            = new Dictionary<GameObject, float>();

        // 把某个饮水目标临时排除若干秒（刚喝完的冷却，防连续占用）
        public static void ExcludeFor(GameObject go, float seconds)
        {
            if (go == null) return;
            try { _until[go] = Time.time + Mathf.Max(0.1f, seconds); }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] ExcludeFor 异常"); }
        }

        // 该目标当前是否还在排除冷却期内
        public static bool IsExcluded(GameObject go)
        {
            if (go == null) return false;
            float until;
            if (!_until.TryGetValue(go, out until)) return false;
            if (Time.time >= until)
            {
                _until.Remove(go);
                return false;
            }
            return true;
        }

        // ★ 性能修复：Cleanup 被"每复制人每秒"调用 → 全局节流为最多每秒一次
        private static float _lastCleanupAt = -1f;

        public static void Cleanup()
        {
            if (_until.Count == 0) return;
            float now = Time.time;
            if (now - _lastCleanupAt < 0.9f) return;
            _lastCleanupAt = now;
            List<GameObject> dead = null;
            foreach (var kv in _until)
            {
                if (kv.Key == null || now >= kv.Value)
                {
                    if (dead == null) dead = new List<GameObject>();
                    dead.Add(kv.Key);
                }
            }
            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++) _until.Remove(dead[i]);
            }
        }

        public static void Clear()
        {
            try { _until.Clear(); }
            catch (Exception e) { MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] Exclusion.Clear 异常"); }
        }
    }

    public class ItemDrinkSource : IDrinkSource
    {
        public int Priority { get { return 1; } }
        public string Name { get { return "饮品物品"; } }

        // 来源【饮品物品】：找最近的可携带饮品（受饮品管制过滤），无则返回 null
        public DrinkTarget TryFind(GameObject duplicant, int rotation)
        {
            var item = DrinkWorldRegistry.FindNearest(duplicant, rotation, tag =>
            {
                var d = Drinks.FindItem(tag);
                if (d == null) return null;

// 饮品管制接线点：「管制只限制小人不主动饮用该饮品」
                if (!DrinkRestriction.IsUsableBy(duplicant, tag.ToString()))
                {
                    DrinkRestriction.NoteSkipped(duplicant, tag.ToString());
                    return null;
                }

                return d;
            });
            if (item == null) return null;
            return new ItemDrinkTarget
            {
                go = item.go,
                approachTarget = item.pickupable,
                pickupable = item.pickupable,
                entry = item,
                data = item.data,
                availableMass = item.mass,
                sourceName = Name,
            };
        }
    }

    public class BuildingDrinkSource : IDrinkSource
    {
        public int Priority { get { return 2; } }
        public string Name { get { return "饮品建筑"; } }

        // 来源【饮品建筑】：找最近的饮水机且水量不低于最低值，无则返回 null
        public DrinkTarget TryFind(GameObject duplicant, int rotation)
        {
            var hit = DrinkBuildingRegistry.FindNearest(duplicant, rotation, d => d != null);
            if (hit == null) return null;

            var cooler = hit.go.GetComponent<WaterCooler>();
            if (cooler == null) return null;
            var storage = cooler.GetComponent<Storage>();
            if (storage == null) return null;
            Tag beverage = cooler.ChosenBeverage;
            if (!beverage.IsValid) return null;
            // 不调用原版 HasMinimumMass：它对 Milk/Ink 这类非元素 Tag 会 ElementLoader.GetElement(...).id 空引用。
            if (storage.GetMassAvailable(beverage) < WaterCooler.DRINK_MASS) return null;

            return new BuildingDrinkTarget
            {
                go = cooler.gameObject,
                approachTarget = cooler,
                cooler = cooler,
                data = hit.data,
                availableMass = WaterCooler.DRINK_MASS,
                sourceName = Name,
            };
        }
    }

    public class BottledWaterDrinkSource : IDrinkSource
    {
        public int Priority { get { return 3; } }
        public string Name { get { return "桶装水"; } }

        // 来源【桶装水】：最近的净水 / 污水瓶；两闸口都关（ModOptions.Live 实时值）时整级跳过
        public DrinkTarget TryFind(GameObject duplicant, int rotation)
        {
            var opt = ModOptions.Live;
            bool allowWater = opt.AllowBottledWater;
            bool allowDirty = opt.AllowBottledDirtyWater;
            if (!allowWater && !allowDirty) return null;

            DrinkWorldRegistry.Entry hit = null;
            if (allowWater)
                hit = DrinkWorldRegistry.FindNearest(duplicant, rotation, tag => null,
                    SimHashes.Water.CreateTag());
            if (hit == null && allowDirty)
                hit = DrinkWorldRegistry.FindNearest(duplicant, rotation, tag => null,
                    SimHashes.DirtyWater.CreateTag());
            if (hit == null) return null;

            // 货源名按实际元素区分，避免日志出现「货源=桶装水」却实际喝了污染水
            var pe = hit.pickupable != null ? hit.pickupable.PrimaryElement : null;
            bool dirty = pe != null && pe.ElementID == SimHashes.DirtyWater;

            return new ItemDrinkTarget
            {
                go = hit.go,
                approachTarget = hit.pickupable,
                pickupable = hit.pickupable,
                entry = hit,
                data = new DrinkData { hydrationPerKg = 50f, effectId = null, maxServeMass = 2f },
                availableMass = hit.mass,
                sourceName = dirty ? "桶装污染水" : Name,
            };
        }
    }

    public static class DrinkWorldRegistry
    {
        public class Entry
        {
            // 物品实体
            public GameObject go;
            // 可搬运组件
            public Pickupable pickupable;
            // 该饮品数值
            public DrinkData data;
            // 现存量（kg）
            public float mass;
            // 已预约的小人（null = 无人预约）
            public GameObject reserver;
        }

        private static readonly List<Entry> _items = new List<Entry>();
        private static readonly List<Entry> _bottled = new List<Entry>();
        private static readonly List<Entry> _scratch = new List<Entry>();

        // 登记一个可携带饮品（OnSpawn 调），供"物品来源"查找
        public static void Register(Pickupable p)
        {
            if (p == null) return;
            var kp = p.GetComponent<KPrefabID>();
            if (kp == null) return;

            Tag tag = kp.PrefabTag;
            var data = Drinks.FindItem(tag);
            if (data != null)
            {
                _items.Add(new Entry { go = p.gameObject, pickupable = p, data = data, mass = p.TotalAmount });
                return;
            }

            var pe = p.PrimaryElement;
            if (pe != null && (pe.ElementID == SimHashes.Water || pe.ElementID == SimHashes.DirtyWater))
            {
                _bottled.Add(new Entry { go = p.gameObject, pickupable = p, data = null, mass = p.TotalAmount });
            }
        }

        // 注销可携带饮品（OnCleanUp 调）
        public static void Unregister(Pickupable p)
        {
            if (p == null) return;
            _items.RemoveAll(e => e == null || e.go == null || e.pickupable == p);
            _bottled.RemoveAll(e => e == null || e.go == null || e.pickupable == p);
        }

        // 尝试把一个饮品物品预留给某个复制人（防两个小人抢同一个物品）
        public static bool TryReserve(GameObject item, GameObject duplicant)
        {
            if (item == null || duplicant == null) return false;
            for (int i = 0; i < _items.Count; i++)
            {
                var e = _items[i];
                if (e == null || e.go != item) continue;
                if (e.reserver != null && e.reserver != duplicant) return false;
                e.reserver = duplicant;
                return true;
            }
            for (int i = 0; i < _bottled.Count; i++)
            {
                var e = _bottled[i];
                if (e == null || e.go != item) continue;
                if (e.reserver != null && e.reserver != duplicant) return false;
                e.reserver = duplicant;
                return true;
            }
            return false;
        }

        // 释放物品预留（取消或完成取水时调）
        public static void Release(GameObject item, GameObject duplicant)
        {
            if (item == null || duplicant == null) return;
            for (int i = 0; i < _items.Count; i++)
            {
                var e = _items[i];
                if (e == null || e.go != item) continue;
                if (e.reserver == duplicant) e.reserver = null;
                return;
            }
            for (int i = 0; i < _bottled.Count; i++)
            {
                var e = _bottled[i];
                if (e == null || e.go != item) continue;
                if (e.reserver == duplicant) e.reserver = null;
                return;
            }
        }

        // 找最近的可携带饮品：用 tagMatcher 把 Tag 映射成 DrinkData 做过滤
        public static Entry FindNearest(GameObject duplicant, int rotation, Func<Tag, DrinkData> tagMatcher)
        {
            return FindNearest(duplicant, rotation, tagMatcher, null);
        }

        public static Entry FindNearest(GameObject duplicant, int rotation,
                                        Func<Tag, DrinkData> tagMatcher, Tag? elementTag)
        {
            if (duplicant == null) return null;
            _scratch.Clear();

            if (elementTag == null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    var e = _items[i];
                    if (e == null || e.go == null || e.pickupable == null) continue;
                    if (e.pickupable.storage != null) continue;
                    if (e.reserver != null && e.reserver != duplicant) continue;
                    // 被其它 fetch（例如餐桌配送）预订完的，不要再选
                    if (e.pickupable.UnreservedFetchAmount <= 0f) continue;
                    // ★ 新增：跳过已被短时排除的目标
                    if (DrinkExclusionRegistry.IsExcluded(e.go)) continue;
                    if (e.pickupable.KPrefabID != null)
                    {
                        var d = tagMatcher(e.pickupable.KPrefabID.PrefabTag);
                        if (d == null) continue;
                        e.data = d;
                    }
                    e.mass = e.pickupable.TotalAmount;
                    _scratch.Add(e);
                }
            }
            else
            {
                for (int i = 0; i < _bottled.Count; i++)
                {
                    var e = _bottled[i];
                    if (e == null || e.go == null || e.pickupable == null) continue;
                    if (e.pickupable.storage != null) continue;
                    if (e.reserver != null && e.reserver != duplicant) continue;
                    if (e.pickupable.UnreservedFetchAmount <= 0f) continue;
                    // ★ 新增：跳过已被短时排除的目标
                    if (DrinkExclusionRegistry.IsExcluded(e.go)) continue;
                    var pe = e.pickupable.PrimaryElement;
                    if (pe == null || pe.ElementID.CreateTag() != elementTag.Value) continue;
                    e.mass = e.pickupable.TotalAmount;
                    _scratch.Add(e);
                }
            }

            if (_scratch.Count == 0) return null;
            Vector3 pos = duplicant.transform.position;
            _scratch.Sort(delegate (Entry a, Entry b)
            {
                return (a.go.transform.position - pos).sqrMagnitude
                    .CompareTo((b.go.transform.position - pos).sqrMagnitude);
            });
            int idx = rotation % _scratch.Count;
            if (idx < 0) idx += _scratch.Count;
            return _scratch[idx];
        }
    }

    public static class DrinkBuildingRegistry
    {
        public class Entry
        {
            // 建筑实体
            public GameObject go;
            // 该饮品数值
            public DrinkData data;
        }

        private static readonly List<Entry> _buildings = new List<Entry>();
        private static readonly List<Entry> _scratch = new List<Entry>();

        // 登记一个"世界里的液体容器"（供桶装水来源查找）
        public static void Register(GameObject go)
        {
            if (go == null) return;
            var kp = go.GetComponent<KPrefabID>();
            if (kp == null) return;
            var data = Drinks.FindBuilding(kp.PrefabTag);
            if (data == null) return;
            if (_buildings.Exists(e => e.go == go)) return;
            _buildings.Add(new Entry { go = go, data = data });
        }

        // 注销液体容器
        public static void Unregister(GameObject go)
        {
            if (go == null) return;
            _buildings.RemoveAll(e => e == null || e.go == null || e.go == go);
        }

        // 找最近的液体容器：用 filter 过滤出可当饮品的水
        public static Entry FindNearest(GameObject duplicant, int rotation, Func<DrinkData, bool> filter)
        {
            if (duplicant == null) return null;
            _scratch.Clear();
            for (int i = 0; i < _buildings.Count; i++)
            {
                var e = _buildings[i];
                if (e == null || e.go == null) continue;
                if (filter != null && !filter(e.data)) continue;
                // ★ 新增：跳过已被短时排除的目标
                if (DrinkExclusionRegistry.IsExcluded(e.go)) continue;
                _scratch.Add(e);
            }
            if (_scratch.Count == 0) return null;

            Vector3 pos = duplicant.transform.position;
            _scratch.Sort(delegate (Entry a, Entry b)
            {
                return (a.go.transform.position - pos).sqrMagnitude
                    .CompareTo((b.go.transform.position - pos).sqrMagnitude);
            });
            int idx = rotation % _scratch.Count;
            if (idx < 0) idx += _scratch.Count;
            return _scratch[idx];
        }
    }

    [HarmonyPatch(typeof(Pickupable), "OnSpawn")]
    public static class DrinkConfig_PickupableSpawn_Patch
    {
        // 补丁：Pickupable OnSpawn 后登记进世界物品表（供饮品物品来源查找）
        public static void Postfix(Pickupable __instance)
        {
            try
            {
            DrinkWorldRegistry.Register(__instance);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkConfig", e); }
        }
    }

    [HarmonyPatch(typeof(Pickupable), "OnCleanUp")]
    public static class DrinkConfig_PickupableCleanup_Patch
    {
        // 补丁：Pickupable OnCleanUp 后从世界物品表移除
        public static void Postfix(Pickupable __instance)
        {
            try
            {
            DrinkWorldRegistry.Unregister(__instance);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkConfig", e); }
        }
    }

    [HarmonyPatch(typeof(Db), "Initialize")]
    public static class DrinkConfig_DbInitialize_Patch
    {
        public static void Postfix()
        {
            try
            {
                Drinks.RegisterDefaults();
                Buffs.Register();
                DrinkEffectBehaviorRegistry.EnsureInitialized();
                BuffConfig.BuildAll();
                DrinkGarnishRegistry.RegisterAll();
                DrinkSourceRegistry.RegisterDefaults();
                HydrationDescriptorBootstrap.RetryAttachAll();
                DrinkExclusionRegistry.Clear();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkConfig_DbInitialize_Patch", e); }
        }
    }

    // ★ 饮品专属「默认禁饮清单」——「默认」行（新复制人的默认值）用
    [SerializationConfig(MemberSerialization.OptIn)]
    public class DrinkDefaultsSaveData : KMonoBehaviour
    {
        [Serialize]
        public List<string> forbiddenDrinkTags = DrinkRestrictionDefaults.NewDefaultList();

        // 内置默认禁饮是否已播种（老档首次加载补一次，之后尊重玩家改动）
        [Serialize]
        private bool builtInDefaultsSeeded;

        // 取一次「补种机会」，只返回一次 true
        public bool TryTakeSeedSlot()
        {
            if (builtInDefaultsSeeded) return false;
            builtInDefaultsSeeded = true;
            return true;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            DrinkRestrictionDefaults.ApplyBuiltIns(this);
        }
    }

    public static class DrinkRestrictionDefaults
    {
        // 内置默认禁饮：新上架饮品默认不勾选（与注册 Tag 保持一致）
        public static readonly string[] BuiltInForbidden = { "BoneSoup" };

        public static List<string> NewDefaultList()
        {
            return new List<string>(BuiltInForbidden);
        }

        // 存档里的默认禁饮清单载体
        private static DrinkDefaultsSaveData _data;
        private static readonly List<string> _fallback = new List<string>(BuiltInForbidden);

        // 老档升级：首次加载时把内置默认禁饮补进「默认」行（之后不再补，避免冲玩家勾选）
        public static void ApplyBuiltIns(DrinkDefaultsSaveData data)
        {
            try
            {
                if (data == null || !data.TryTakeSeedSlot()) return;
                var l = data.forbiddenDrinkTags;
                if (l == null)
                {
                    l = NewDefaultList();
                    data.forbiddenDrinkTags = l;
                }
                for (int i = 0; i < BuiltInForbidden.Length; i++)
                {
                    if (!l.Contains(BuiltInForbidden[i])) l.Add(BuiltInForbidden[i]);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Restrict] 内置默认禁饮补种异常");
            }
        }

        // 读档挂载：把存档里的「禁用饮品集合」恢复进排除表
        public static void Mount(DrinkDefaultsSaveData data)
        {
            if (data != null) _data = data;
        }

        private static List<string> List
        {
            get { return _data != null ? _data.forbiddenDrinkTags : _fallback; }
        }

        public static int Count
        {
            get { var l = List; return l != null ? l.Count : 0; }
        }

        // 该 Tag 是否被玩家标记为「禁止饮用」
        public static bool IsForbidden(Tag tag)
        {
            try
            {
                var l = List;
                return l != null && l.Contains(tag.ToString());
            }
            catch { return false; }
        }

        // 设置 / 清除某个 Tag 的「禁止饮用」标记（会同步进存档）
        public static void SetForbidden(Tag tag, bool forbidden)
        {
            try
            {
                var l = List;
                if (l == null) return;
                string s = tag.ToString();
                if (forbidden)
                {
                    if (!l.Contains(s)) l.Add(s);
                }
                else if (l.Contains(s)) l.Remove(s);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Restrict] 默认禁饮清单写入异常");
            }
        }

        // 把禁饮集合写进存档载体（供序列化）
        public static void SeedInto(List<string> target)
        {
            try
            {
                if (target == null) return;
                var l = List;
                if (l == null) return;
                for (int i = 0; i < l.Count; i++)
                {
                    if (string.IsNullOrEmpty(l[i])) continue;
                    if (!target.Contains(l[i])) target.Add(l[i]);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Restrict] 默认禁饮播种异常");
            }
        }
    }

    [HarmonyPatch(typeof(Game), "OnPrefabInit")]
    public static class DrinkDefaults_Mount_Patch
    {
        // 补丁：游戏初始化完成后挂载禁饮集合的存档载体
        public static void Postfix(Game __instance)
        {
            try
            {
                if (__instance == null) return;
                var go = __instance.gameObject;
                var data = go.GetComponent<DrinkDefaultsSaveData>();
                if (data == null) data = go.AddComponent<DrinkDefaultsSaveData>();
                DrinkRestrictionDefaults.Mount(data);
                MFLog.Info(LogCat.Drink, "[MoreFood/Drink][Restrict] 饮品默认禁饮载体已挂上 Game（早于反序列化）：已有 "
                          + DrinkRestrictionDefaults.Count + " 项");
            }
            catch (Exception e)
            {
                MFLog.Warn(LogCat.Drink,"[MoreFood/Drink][Restrict] 挂饮品默认禁饮载体失败: " + e);
            }
        }
    }
}
