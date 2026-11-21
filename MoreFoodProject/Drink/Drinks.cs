// Drinks.cs — 饮水系统 · 【数据汇总层】：两张表 + 一行创建入口 + 所有实际条目

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class Drinks
    {
        // 两张数据表
        public static readonly Dictionary<Tag, DrinkData> ItemTable = new Dictionary<Tag, DrinkData>();

// 饮品建筑的数值表（Tag → 数值）
        public static readonly Dictionary<Tag, DrinkData> BuildingTable = new Dictionary<Tag, DrinkData>();

        // 饮品识别 Tag
        public static readonly Tag DrinkItemTag     = new Tag("MoreFoodDrinkItem");
        public static readonly Tag DrinkBuildingTag = new Tag("MoreFoodDrinkBuilding");

        public static readonly Tag DrinkWineTag   = new Tag("MoreFoodDrinkWine");
        public static readonly Tag DrinkCoffeeTag = new Tag("MoreFoodDrinkCoffee");
        public static readonly Tag DrinkSodaTag   = new Tag("MoreFoodDrinkSoda");
        public static readonly Tag DrinkOtherTag  = new Tag("MoreFoodDrinkOther");

        // 取饮品分类对应的 Tag（wine / coffee / soda / other）
        public static Tag GetCategoryTag(string category)
        {
            switch (string.IsNullOrEmpty(category) ? "" : category.Trim().ToLowerInvariant())
            {
                case "wine":   return DrinkWineTag;
                case "coffee": return DrinkCoffeeTag;
                case "soda":   return DrinkSodaTag;
                default:       return DrinkOtherTag;
            }
        }

        // 取该分类的默认上桌优先级（wine40 > coffee30 > soda20 > other15）
        public static int GetDefaultGarnishPriority(string category)
        {
            switch (string.IsNullOrEmpty(category) ? "" : category.Trim().ToLowerInvariant())
            {
                case "wine":   return 40;
                case "coffee": return 30;
                case "soda":   return 20;
                default:       return 15;
            }
        }

        // 创建入口：一行加一种饮品（返回实例，可继续改字段）
        public static readonly List<Tag> CreatedDrinkIds = new List<Tag>();

        // 登记一个由本 mod 创建的饮品 id（供"是否本 mod 饮品"判定）
        internal static void RegisterDrinkId(Tag tag)
        {
            if (!CreatedDrinkIds.Contains(tag)) CreatedDrinkIds.Add(tag);
        }

        public static DrinkData AddItem(string tag,
                                        float hydrationPerKg = 50f,
                                        float moraleBonus = 0f,
                                        string effectId = null,
                                        float maxServeMass = 2f,
                                        int priority = 0,
                                        int sort = 0)
        {
            var d = new DrinkData
            {
                hydrationPerKg = hydrationPerKg,
                moraleBonus = moraleBonus,
                effectId = effectId,
                maxServeMass = maxServeMass,
                priority = priority,
                sort = sort,
            };
            RegisterItem(tag, d);
            return d;
        }

        public static DrinkData AddBuilding(string tag,
                                            float hydrationPerKg = 50f,
                                            float moraleBonus = 0f,
                                            string effectId = null,
                                            float maxServeMass = 1f,
                                            int priority = 0,
                                            int sort = 0)
        {
            var d = new DrinkData
            {
                hydrationPerKg = hydrationPerKg,
                moraleBonus = moraleBonus,
                effectId = effectId,
                maxServeMass = maxServeMass,
                priority = priority,
                sort = sort,
            };
            BuildingTable[new Tag(tag)] = d;
            // ⚠ 建筑【不打】饮品识别 tag —— 用户明确：tag 与配置只针对 object 物品。
            return d;
        }

        // 底层注册（想直接塞一个构造好的 DrinkData 时用）
        public static void RegisterItem(string tag, DrinkData data)
        {
            if (data == null) return;
            data.isDrink = true;
            ItemTable[new Tag(tag)] = data;
            ConsumeRegistry.Register(new Tag(tag), data);
            EnsureDrinkTag(tag, DrinkItemTag, quiet: true);   // 注册期 prefab 未就绪，失败是预期
            RegisterDrinkId(new Tag(tag));
        }

        // 注册一个「饮品建筑」条目（Tag → 数值；供建筑来源查找）
        public static void RegisterBuilding(string tag, DrinkData data)
        {
            if (data == null) return;
            BuildingTable[new Tag(tag)] = data;
            // 建筑不打饮品识别 tag（同 AddBuilding）
        }

        // 自动打标（幂等+防重）：quiet 抑制「注册期 prefab 未就绪」的预期失败警告，否则每次启动刷 56 条噪音
        public static void EnsureDrinkTag(string prefabId, Tag drinkTag, bool quiet = false)
        {
            if (string.IsNullOrEmpty(prefabId)) return;

            try
            {
                var go = Assets.TryGetPrefab(new Tag(prefabId));
                if (go == null)
                {
                    if (!quiet)
                    {
                        Debug.LogWarning("[MoreFood/Drink][Tag] 找不到 prefab '" + prefabId
                                         + "'（可能还没注册 / id 拼错）→ 未能打 tag " + drinkTag);
                    }
                    return;
                }

                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null)
                {
                    Debug.LogWarning("[MoreFood/Drink][Tag] prefab '" + prefabId
                                     + "' 上没有 KPrefabID → 未能打 tag " + drinkTag);
                    return;
                }

                // ★ 防重：已经有了就不再加
                if (kpid.HasTag(drinkTag))
                {
                    MFLog.Info(LogCat.Drink,"[MoreFood/Drink][Tag] '" + prefabId + "' 已有 tag "
                              + drinkTag + "（跳过，防重）");
                    return;
                }

                kpid.AddTag(drinkTag, false);   // false = 运行时 tag，不写进存档
                MFLog.Info(LogCat.Drink,"[MoreFood/Drink][Tag] ★ 已给 '" + prefabId + "' 追加饮品识别 tag："
                          + drinkTag);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Tag] 给 '" + prefabId + "' 打 tag 异常");
            }
        }

        // RegisterDefaults/AddItem 发生在 mod 加载早期，那时 prefab 还没注册
        public static void InstallAutoTagHook()
        {
            if (autoTagHooked)
            {
                return;
            }
            autoTagHooked = true;
            try
            {
                Assets.RegisterOnAddPrefab(new Action<KPrefabID>(OnPrefabAdded));
                MFLog.Info(LogCat.Drink,"[MoreFood/Drink][Tag] 已挂 Assets.RegisterOnAddPrefab：饮品 prefab 注册时立刻打标");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Tag] 挂 RegisterOnAddPrefab 失败");
            }
        }

        // 是否已挂上「prefab 注册即打 tag」的钩子
        private static bool autoTagHooked;

        private static void OnPrefabAdded(KPrefabID kpid)
        {
            try
            {
                if (kpid == null)
                {
                    return;
                }
                DrinkData data;
                if (!ItemTable.TryGetValue(kpid.PrefabTag, out data))
                {
                    return;   // 表是饮品表；不是饮料就放过
                }
                EnsureDrinkTag(kpid.PrefabTag.Name, DrinkItemTag);
                if (data != null && data.garnish)
                    EnsureDrinkTag(kpid.PrefabTag.Name, GetCategoryTag(data.category));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Tag] 注册即打标异常");
            }
        }

        // 该 prefab 是否被标记为饮品（物品或建筑任一）
        public static bool IsDrinkPrefab(GameObject go)
        {
            if (go == null) return false;
            try
            {
                var kpid = go.GetComponent<KPrefabID>();
                if (kpid == null) return false;
                return kpid.HasTag(DrinkItemTag) || kpid.HasTag(DrinkBuildingTag);
            }
            catch { return false; }
        }

        // 统一饮品清单收集函数
        public class DrinkEntry
        {
            public Tag tag;            // 饮品 tag
            public string id;          // tag 字符串（UI 列 id 用）
            public string name;        // 显示名（优先取 prefab 的正式名）
            // 该饮品的数值
            public DrinkData data;
            public bool isBuilding;    // true = 建筑货源（饮水机之类）
        }

        // 其他 Mod 的额外饮品来源（返回的条目会被合并进 CollectAll）
        public static readonly List<Func<List<DrinkEntry>>> ExtraSources =
            new List<Func<List<DrinkEntry>>>();

        // ★ 收集饮品清单。
        public static List<DrinkEntry> CollectAll(bool includeBuildings = false,
                                                  bool includeExtra = true)
        {
            var list = new List<DrinkEntry>(16);

            try
            {
                foreach (var kv in ItemTable)
                    list.Add(MakeEntry(kv.Key, kv.Value, false));

                if (includeBuildings)
                {
                    foreach (var kv in BuildingTable)
                        list.Add(MakeEntry(kv.Key, kv.Value, true));
                }

                if (includeExtra)
                {
                    for (int i = 0; i < ExtraSources.Count; i++)
                    {
                        try
                        {
                            var extra = ExtraSources[i] != null ? ExtraSources[i]() : null;
                            if (extra != null) list.AddRange(extra);
                        }
                        catch (Exception e)
                        {
                            MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] ExtraSources[" + i + "] 异常");
                        }
                    }
                }

                // 排序：★ sort 升序（0 在前、1 在后，比大小）
                list.Sort(delegate (DrinkEntry a, DrinkEntry b)
                {
                    int sa = (a != null && a.data != null) ? a.data.sort : 0;
                    int sb = (b != null && b.data != null) ? b.data.sort : 0;
                    if (sa != sb) return sa.CompareTo(sb);
                    if (a.isBuilding != b.isBuilding) return a.isBuilding ? 1 : -1;
                    return string.CompareOrdinal(a.id ?? "", b.id ?? "");
                });
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink] CollectAll 异常");
            }

            return list;
        }

        // ★ 饮品物品清单（含食物类物品）—— 管制屏 / 图鉴用这个
        public static List<DrinkEntry> CollectItems()
        {
            return CollectAll(false, false);
        }

        // 建筑货源清单（饮水机之类）——【不要】混进饮品清单
        public static List<DrinkEntry> CollectBuildings()
        {
            var all = CollectAll(true, false);
            var list = new List<DrinkEntry>();
            for (int i = 0; i < all.Count; i++) if (all[i].isBuilding) list.Add(all[i]);
            return list;
        }

        private static DrinkEntry MakeEntry(Tag tag, DrinkData data, bool isBuilding)
        {
            string id = tag.ToString();
            string name = id;
            bool prefabFound = false;

            try
            {
                var go = Assets.TryGetPrefab(tag);
                if (go != null)
                {
                    prefabFound = true;
                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid != null)
                    {
                        string proper = kpid.GetProperName();
                        if (!string.IsNullOrEmpty(proper)) name = proper;
                    }
                }
            }
            catch { /* expected: 取 prefab/别名失败时退回用 tag 名作显示名，条目照常建表 */ }

            // ★★ 补打识别 tag（幂等）—— 解决时序问题：prefab 注册可能晚于建表，这里再确认补一次
            if (prefabFound && !isBuilding)
                EnsureDrinkTag(id, DrinkItemTag);

            return new DrinkEntry { tag = tag, id = id, name = name, data = data, isBuilding = isBuilding };
        }

        // 查询
        public static DrinkData FindItem(Tag tag)
        {
            DrinkData d;
            return ItemTable.TryGetValue(tag, out d) ? d : null;
        }

        // 按 Tag 查找饮品建筑条目；找不到返回 null
        public static DrinkData FindBuilding(Tag tag)
        {
            DrinkData d;
            return BuildingTable.TryGetValue(tag, out d) ? d : null;
        }

        // 士气加成 → 自动生成一个只改 QualityOfLife 的 buff
        private static readonly Dictionary<int, string> _moraleEffectIds = new Dictionary<int, string>();

        // 把 moraleBonus 转成一个只改 QualityOfLife 的 buff 并返回 id。
        public static string GetMoraleEffectId(float moraleBonus)
        {
            if (Mathf.Approximately(moraleBonus, 0f)) return null;

            int key = Mathf.RoundToInt(moraleBonus * 10f);   // 支持 0.1 的精度
            string id;
            if (_moraleEffectIds.TryGetValue(key, out id)) return id;

            float v = key / 10f;
            id = "DrinkMorale" + (v >= 0f ? "P" : "M") + Mathf.Abs(key).ToString();
            BuffConfig.Register(new BuffDef(id, "饮品士气", "饮品带来的士气加成", v < 0f)
                .Add(StatIds.Morale, v));
            _moraleEffectIds[key] = id;
            return id;
        }

        // 实际条目
        public static class Defaults
        {
            public const string ClassicCola = "ClassicCola";
            public const string ZeroSugarCola = "ZeroSugarCola";
            public const string WaterCooler = "WaterCooler";
        }

        // 注册实际饮品条目。其他 Mod 可以在之后继续 AddItem 追加。
        public static void RegisterDefaults()
        {
            ResetTablesForSession();
            RegisterDefaults_Inner();
            DrinkYamlLoader.LoadAll();
            // 打标改由 Assets.RegisterOnAddPrefab 回调（会补发已存在 prefab）；Db 期扫描命中恒 0，已删除
        }

        // 清空注册表，避免同进程新档残留上一局的条目。
        public static void ResetTablesForSession()
        {
            ItemTable.Clear();
            BuildingTable.Clear();
            CreatedDrinkIds.Clear();
            ConsumeRegistry.ResetForSession();
        }

        private static void RegisterDefaults_Inner()
        {
            // 饮品效果统一由 drink/*.yaml 提供，这里只登记饮品数据
            AddItem(Defaults.ClassicCola);
            AddItem(Defaults.ZeroSugarCola);

            // 建筑：只登记成"货源"，【不加任何 buff】。
            AddBuilding(Defaults.WaterCooler, maxServeMass: 2f);
        }
    }
}
