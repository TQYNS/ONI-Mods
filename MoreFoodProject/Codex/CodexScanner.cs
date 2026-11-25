// CodexScanner.cs — 让 MoreFood 的物品（食物 / 饮品 / 原料 / 植物）自动获得「原版样式」百科条目

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class CodexScanner
    {
        private static MethodInfo _foodDesc;   // CodexEntryGenerator.GenerateFoodDescriptionContainers(FoodInfo, List<ContentContainer>)
        private static MethodInfo _recipe;     // CodexEntryGenerator.GenerateRecipeContainers(Tag, List<ContentContainer>)

        // 物品显示名 → 条目 id（供图书按钮 / ChangeArticle 兜底）。
        private static readonly Dictionary<string, string> ItemNameToId =
            new Dictionary<string, string>(StringComparer.Ordinal);

        // 是否已做过诊断
        private static bool _diagnosed;

        // 诊断日志只打一次。
        public static bool Diagnosed
        {
            get { return _diagnosed; }
            set { _diagnosed = value; }
        }

        // 入口：CodexCacheInit 之后一次性把本 mod 全部物品补进百科
        public static void BuildAllEntries()
        {
            ItemNameToId.Clear();

            try { BuildFoodEntries(); }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 食物百科异常"); }

            try { BuildNonFoodItemEntries(); }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 物品百科异常"); }

            try { BuildPlantEntries(); }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 植物百科异常"); }

            try { BuildExtraItemEntries(); }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 物品补扫异常"); }
        }

        // ① 食物（原版已生成则跳过）
        public static void BuildFoodEntries()
        {
            int made = 0, exists = 0, failed = 0;
            List<EdiblesManager.FoodInfo> foods = AllOurFoodInfos();
            for (int i = 0; i < foods.Count; i++)
            {
                EdiblesManager.FoodInfo fi = foods[i];
                if (fi == null || string.IsNullOrEmpty(fi.Id))
                {
                    continue;
                }
                string id = CodexCache.FormatLinkID(fi.Id);
                if (!string.IsNullOrEmpty(fi.Name) && !ItemNameToId.ContainsKey(fi.Name))
                {
                    ItemNameToId[fi.Name] = id;
                }
                if (CodexCache.entries != null && CodexCache.entries.ContainsKey(id))
                {
                    exists++;   // 原版（或我们上次）已建，跳过
                    continue;
                }
                try
                {
                    if (BuildOne(fi)) made++;
                    else failed++;
                }
                catch (Exception e)
                {
                    failed++;
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 食物百科 {0} 生成失败", fi.Id);
                }
            }
            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] ★ 食物百科：新建 " + made + " 条，已存在 " + exists + " 条，跳过 " + failed
                      + " 条（共扫描 " + foods.Count + " 种食物，名→id 映射 " + ItemNameToId.Count + " 条）");
        }

        // ② 非食物物品（饮品 / 原料 / 其他）
        public static void BuildNonFoodItemEntries()
        {
            // 1) id → 描述（原料/食物配置里的 desc，字符串）
            var idToDesc = new Dictionary<string, string>(StringComparer.Ordinal);
            var order = new List<string>();

            CollectDefIds(typeof(FoodConfigs), idToDesc, order);
            CollectDefIds(typeof(IngredientConfigs), idToDesc, order);

            // 2) 饮品：id → effectId（DrinkData.effectId）
            var idToEffect = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Drinks.ItemTable != null)
            {
                foreach (KeyValuePair<Tag, DrinkData> kv in Drinks.ItemTable)
                {
                    if (kv.Key == null || string.IsNullOrEmpty(kv.Key.Name)) continue;
                    string id = kv.Key.Name;
                    if (kv.Value != null && !string.IsNullOrEmpty(kv.Value.effectId))
                    {
                        idToEffect[id] = kv.Value.effectId;
                    }
                    if (!idToDesc.ContainsKey(id))
                    {
                        idToDesc[id] = null;
                        order.Add(id);
                    }
                }
            }

            int made = 0, exists = 0, failed = 0, withEffect = 0;
            for (int i = 0; i < order.Count; i++)
            {
                string id = order[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (IngredientConfigs.IsIconOnlyItem(id)) continue;   // 纯图标没有实体，不建百科条目

                string linkId = CodexCache.FormatLinkID(id);
                string effectId;
                idToEffect.TryGetValue(id, out effectId);

                if (CodexCache.entries != null && CodexCache.entries.ContainsKey(linkId))
                {
                    exists++;
                    RegisterItemName(id);   // 仍然登记 名→id（供图书按钮 / ChangeArticle 兜底）
                    continue;
                }

                string desc;
                idToDesc.TryGetValue(id, out desc);

                try
                {
                    if (BuildGenericItem(id, CodexEntryGenerator.FOOD_CATEGORY_ID, effectId, desc))
                    {
                        made++;
                        if (!string.IsNullOrEmpty(effectId)) withEffect++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 物品百科 {0} 生成失败", id);
                }
            }

            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] ★ 物品百科：新建 " + made + " 条（其中带效果 " + withEffect
                      + " 条），已存在 " + exists + " 条，跳过 " + failed + " 条（候选 " + order.Count + " 个）");
        }

        // ③ 植物（ModPlants.All 目前为空，逻辑保留）
        public static void BuildPlantEntries()
        {
            ModPlants.PlantDef[] all = ModPlants.All;
            if (all == null || all.Length == 0) return;

            string category = CodexCache.FormatLinkID("PLANTS");
            int made = 0;
            for (int i = 0; i < all.Length; i++)
            {
                ModPlants.PlantDef d = all[i];
                if (d == null || string.IsNullOrEmpty(d.id)) continue;
                if (CodexCache.entries != null && CodexCache.entries.ContainsKey(CodexCache.FormatLinkID(d.id))) continue;
                try
                {
                    if (BuildGenericItem(d.id, category, null, d.desc)) made++;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 植物百科 {0} 生成失败", d.id);
                }
            }
            if (made > 0)
            {
                MFLog.Info(LogCat.Codex, "[MoreFood/Codex] ★ 植物百科：新建 " + made + " 条");
            }
        }

        // ④ 补扫：本 Mod 的其它物品（火箭模块 / 弹药 / 卷轴等）
        public static void BuildExtraItemEntries()
        {
            var cand = new List<string[]>();                       // {id, name, desc}
            var seen = new HashSet<string>(StringComparer.Ordinal);

            CollectSpaceItems(cand, seen);

            string[] modTags = { "ammo", "ModuleItem", "MoreFoodDrinkItem",
                                 "IndustrialIngredient", "SpaceScroll", "RecipeScroll" };
            try
            {
                var prefabs = Assets.Prefabs;
                if (prefabs != null)
                {
                    for (int i = 0; i < prefabs.Count; i++)
                    {
                        var kpid = prefabs[i];
                        if (kpid == null) continue;
                        bool ours = false;
                        for (int t = 0; t < modTags.Length; t++)
                            if (kpid.HasTag(new Tag(modTags[t]))) { ours = true; break; }
                        if (!ours) continue;
                        string id = kpid.PrefabTag.Name;
                        if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                        cand.Add(new string[] { id, null, null });
                    }
                }
            }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 物品补扫(tag) 异常"); }

            string cat = CodexCache.FormatLinkID("EQUIPMENT");
            string category = (CodexCache.entries != null && CodexCache.entries.ContainsKey(cat))
                ? cat : CodexEntryGenerator.FOOD_CATEGORY_ID;

            var missing = new List<string>();
            int made = 0, exists = 0;
            for (int i = 0; i < cand.Count; i++)
            {
                string id = cand[i][0];
                if (string.IsNullOrEmpty(id)) continue;
                if (CodexCache.entries != null && CodexCache.entries.ContainsKey(CodexCache.FormatLinkID(id)))
                {
                    exists++;
                    continue;
                }
                missing.Add(id);
                try
                {
                    if (BuildGenericItem(id, category, null, cand[i][2])) made++;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 物品补扫 {0} 生成失败", id);
                }
            }

            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] ★ 物品补扫：候选 " + cand.Count + " 个，已存在 " + exists
                      + "，缺百科 " + missing.Count + " 条 → 新建 " + made + " 条"
                      + (missing.Count > 0 ? "；缺项：" + string.Join(",", missing.ToArray()) : ""));
        }

        // 反射 Space.AmmoItems / Space.ModuleItems 的 static Items（string[][]：id/name/desc/...）
        private static void CollectSpaceItems(List<string[]> cand, HashSet<string> seen)
        {
            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int a = 0; a < asms.Length; a++)
                {
                    var asm = asms[a];
                    if (asm == null) continue;
                    GatherTable(asm, "Space.AmmoItems", cand, seen);
                    GatherTable(asm, "Space.ModuleItems", cand, seen);
                }
            }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] Space 物品表反射异常"); }
        }

        private static void GatherTable(System.Reflection.Assembly asm, string typeName,
                                        List<string[]> cand, HashSet<string> seen)
        {
            try
            {
                Type t = asm.GetType(typeName, false);
                if (t == null) return;
                var f = t.GetField("Items", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var rows = f != null ? f.GetValue(null) as string[][] : null;
                if (rows == null) return;
                for (int i = 0; i < rows.Length; i++)
                {
                    var r = rows[i];
                    if (r == null || r.Length < 1 || string.IsNullOrEmpty(r[0])) continue;
                    if (!seen.Add(r[0])) continue;
                    cand.Add(new string[] { r[0], r.Length > 1 ? r[1] : null, r.Length > 2 ? r[2] : null });
                }
            }
            catch { /* expected: 反射收集候选失败，跳过该行 */ }
        }

        // 通用物品条目：照原版 GenerateFoodEntries 的步骤，全部复用原版控件
        internal static bool BuildGenericItem(string id, string categoryId, string effectId, string descOverride)
        {
            GameObject prefab = Assets.GetPrefab(id);
            if (prefab == null)
            {
                Debug.LogWarning("[MoreFood/Codex] 物品 prefab 尚不可用，跳过：" + id);
                return false;
            }

            KPrefabID kpid = prefab.GetComponent<KPrefabID>();
            if (kpid != null && (kpid.HasTag(GameTags.DeprecatedContent) || kpid.HasTag(GameTags.IncubatableEgg)
                                  || kpid.HasTag(GameTags.Seed) || kpid.HasTag(GameTags.CropSeed)))
            {
                return false;
            }

            string name = ItemDisplayName(prefab, id);

            string desc = descOverride;
            InfoDescription info = prefab.GetComponent<InfoDescription>();
            if (info != null && !string.IsNullOrEmpty(info.description))
            {
                desc = info.description;
            }

            Sprite sprite = null;
            try { sprite = Def.GetUISprite(new Tag(id), "ui", false).first; }
            catch { /* expected: 无 UI 图标，使用占位 */ sprite = null; }

            var containers = new List<ContentContainer>();

            // 1) 标题（public）
            CodexEntryGenerator.GenerateTitleContainers(name, containers);

            // 2) 图片（public）
            if (sprite != null)
            {
                CodexEntryGenerator.GenerateImageContainers(sprite, containers);
            }

            // 3) 描述（原版 generic 描述段）
            if (!string.IsNullOrEmpty(desc))
            {
                var w = new List<ICodexWidget>();
                w.Add(new CodexText(desc, CodexTextStyle.Body, null));
                w.Add(new CodexSpacer());
                containers.Add(new ContentContainer(w, ContentContainer.ContentLayout.Vertical));
            }

            // 4) 效果（buff）—— 仿原版效果段
            if (!string.IsNullOrEmpty(effectId))
            {
                AppendEffectSection(effectId, containers);
            }

            // 5) 配方（原版为 private，反射调用；= 原版配方面板：图标 + 数量 + 制造机）
            EnsureReflection();
            if (_recipe != null)
            {
                try { _recipe.Invoke(null, new object[] { id.ToTag(), containers }); }
                catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 配方生成失败 {0}", id); }
            }

            // 6) 制造于 / 用于（public）
            try { CodexEntryGenerator_Elements.GenerateMadeAndUsedContainers(id.ToTag(), containers); }
            catch { /* expected: 可选用途容器生成失败，跳过 */ }

            CodexEntry entry = new CodexEntry(categoryId, containers, name);
            entry.name = name;
            entry.icon = sprite;
            entry.parentId = categoryId;
            CodexCache.AddEntry(id, entry);

            if (!string.IsNullOrEmpty(name) && !ItemNameToId.ContainsKey(name))
            {
                ItemNameToId[name] = CodexCache.FormatLinkID(id);
            }
            return true;
        }

        // 按原版 GenerateFoodEntries 的步骤，为单个食物组装完整条目。
        private static bool BuildOne(EdiblesManager.FoodInfo food)
        {
            GameObject prefab = Assets.GetPrefab(food.Id);
            if (prefab == null)
            {
                Debug.LogWarning("[MoreFood/Codex] 食物 prefab 尚不可用，跳过：" + food.Id);
                return false;
            }
            KPrefabID kpid = prefab.GetComponent<KPrefabID>();
            if (kpid != null && (kpid.HasTag(GameTags.DeprecatedContent) || kpid.HasTag(GameTags.IncubatableEgg)
                                  || kpid.HasTag(GameTags.Seed) || kpid.HasTag(GameTags.CropSeed)))
            {
                return false;
            }

            var containers = new List<ContentContainer>();

            // 1) 标题（public）
            CodexEntryGenerator.GenerateTitleContainers(food.Name, containers);

            // 2) 图片（public）
            Sprite sprite = null;
            try { sprite = Def.GetUISprite(food.ConsumableId, "ui", false).first; }
            catch { /* expected: 无 UI 图标，使用占位 */ sprite = null; }
            if (sprite != null)
            {
                CodexEntryGenerator.GenerateImageContainers(sprite, containers);
            }

            // 3) 描述（卡路里/品质/腐败/效果）—— 原版为 private，反射调用
            EnsureReflection();
            if (_foodDesc != null)
            {
                try { _foodDesc.Invoke(null, new object[] { food, containers }); }
                catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 食物描述生成失败 {0}", food.Id); }
            }

            // 4) 配方 —— 原版为 private，反射调用
            if (_recipe != null)
            {
                try { _recipe.Invoke(null, new object[] { food.ConsumableId.ToTag(), containers }); }
                catch { /* expected: 可选配方生成失败，跳过 */ }
            }

            // 5) 制造于/用于（public）
            try { CodexEntryGenerator_Elements.GenerateMadeAndUsedContainers(food.ConsumableId.ToTag(), containers); }
            catch { /* expected: 可选用途容器生成失败，跳过 */ }

            CodexEntry entry = new CodexEntry(CodexEntryGenerator.FOOD_CATEGORY_ID, containers, food.Name);
            entry.name = food.Name;
            entry.icon = sprite;
            entry.parentId = CodexEntryGenerator.FOOD_CATEGORY_ID;
            CodexCache.AddEntry(food.Id, entry);
            return true;
        }

        // 效果段（buff）—— 仿 GenerateFoodDescriptionContainers 的效果部分
        private static void AppendEffectSection(string effectId, List<ContentContainer> containers)
        {
            string name = null;
            string desc = null;
            List<string> lines = new List<string>();

            BuffDef buff = BuffConfig.Get(effectId);
            if (buff != null)
            {
                name = buff.name;
                desc = buff.description;
                for (int i = 0; i < buff.changes.Count; i++)
                {
                    lines.Add(buff.changes[i].Format());
                }
                string behavior = buff.BehaviorLine();
                if (!string.IsNullOrEmpty(behavior)) lines.Add(behavior);
            }
            else
            {
                // 退路：取游戏已注册的 Effect
                Klei.AI.Effect eff = null;
                try
                {
                    if (Db.Get() != null && Db.Get().effects != null && Db.Get().effects.Exists(effectId))
                    {
                        eff = Db.Get().effects.Get(effectId);
                    }
                }
                catch { /* expected: 效果条目不存在，跳过 */ }
                if (eff == null)
                {
                    return;
                }
                name = eff.Name;
                desc = eff.description;
                if (eff.SelfModifiers != null)
                {
                    for (int i = 0; i < eff.SelfModifiers.Count; i++)
                    {
                        Klei.AI.AttributeModifier m = eff.SelfModifiers[i];
                        string an = string.IsNullOrEmpty(m.GetName()) ? m.AttributeId : m.GetName();
                        lines.Add(an + " " + m.GetFormattedString());
                    }
                }
            }

            if (string.IsNullOrEmpty(name)) return;

            bool linkable = EnsureEffectSubEntry(effectId, name, desc, lines);

            var w = new List<ICodexWidget>();
            w.Add(new CodexText(STRINGS.CODEX.HEADERS.FOODEFFECTS + ":", CodexTextStyle.Body, null));

            // 统一口径：优先走 buff tooltip 工厂（其首行=名字），否则用同款布局兜底（名字优先）
            string tipText;
            if (!CountedBuffDisplay.TryBuildTooltip(effectId, null, out tipText) || string.IsNullOrEmpty(tipText))
            {
                StringBuilder fb = new StringBuilder();
                fb.Append(name);
                if (!string.IsNullOrEmpty(desc)) fb.Append("\n").Append(desc);
                for (int i = 0; i < lines.Count; i++)
                {
                    fb.Append("\n    • ").Append(lines[i]);
                }
                tipText = fb.ToString();
            }

            string label = name;
            if (linkable)
            {
                string linkId = CodexEntryGenerator.FOOD_EFFECTS_ENTRY_ID + "::" + effectId.ToUpperInvariant();
                label = STRINGS.UI.FormatAsLink(name, linkId);
            }
            w.Add(new CodexTextWithTooltip("    • " + label, tipText, CodexTextStyle.Body));
            w.Add(new CodexSpacer());

            containers.Add(new ContentContainer(w, ContentContainer.ContentLayout.Vertical));
        }

        // 把一个 SubEntry（FOOD_EFFECTS::ID）挂进原版「食物效果」条目，使效果链接可点开。
        private static bool EnsureEffectSubEntry(string effectId, string name, string desc, List<string> lines)
        {
            try
            {
                if (CodexCache.entries == null) return false;

                CodexEntry fx;
                if (!CodexCache.entries.TryGetValue(CodexEntryGenerator.FOOD_EFFECTS_ENTRY_ID, out fx) || fx == null)
                {
                    return false;
                }
                if (fx.subEntries == null) return false;

                string subId = CodexEntryGenerator.FOOD_EFFECTS_ENTRY_ID + "::" + effectId.ToUpperInvariant();
                for (int i = 0; i < fx.subEntries.Count; i++)
                {
                    SubEntry s = fx.subEntries[i];
                    if (s != null && !string.IsNullOrEmpty(s.id)
                        && string.Equals(s.id, subId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;   // 已存在
                    }
                }

                var w = new List<ICodexWidget>();
                w.Add(new CodexText(name, CodexTextStyle.Title, null));
                if (!string.IsNullOrEmpty(desc)) w.Add(new CodexText(desc, CodexTextStyle.Body, null));
                for (int i = 0; i < lines.Count; i++)
                {
                    w.Add(new CodexText("    • " + lines[i], CodexTextStyle.Body, null));
                }
                w.Add(new CodexSpacer());

                SubEntry sub = new SubEntry(subId, CodexEntryGenerator.FOOD_EFFECTS_ENTRY_ID,
                    new List<ContentContainer> { new ContentContainer(w, ContentContainer.ContentLayout.Vertical) },
                    name);
                fx.subEntries.Add(sub);
                return true;
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 效果子条目 {0} 注册失败", effectId);
                return false;
            }
        }

        // 辅助
        public static bool TryGetItemId(string displayName, out string id)
        {
            id = null;
            if (string.IsNullOrEmpty(displayName)) return false;
            if (ItemNameToId.TryGetValue(displayName, out id)) return true;
            string trimmed = displayName.Trim();
            if (trimmed != displayName && ItemNameToId.TryGetValue(trimmed, out id)) return true;
            id = null;
            return false;
        }

        private static void RegisterItemName(string id)
        {
            GameObject prefab = Assets.GetPrefab(id);
            if (prefab == null) return;
            string name = ItemDisplayName(prefab, id);
            if (!string.IsNullOrEmpty(name) && !ItemNameToId.ContainsKey(name))
            {
                ItemNameToId[name] = CodexCache.FormatLinkID(id);
            }
        }

        private static string ItemDisplayName(GameObject prefab, string fallbackId)
        {
            try
            {
                KSelectable sel = prefab.GetComponent<KSelectable>();
                if (sel != null)
                {
                    string n = sel.GetProperName();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch { /* expected: 显示名探测失败，返回标识 */ }
            return fallbackId;
        }

        private static void EnsureReflection()
        {
            if (_foodDesc == null)
            {
                _foodDesc = AccessTools.Method(typeof(CodexEntryGenerator), "GenerateFoodDescriptionContainers",
                    new Type[] { typeof(EdiblesManager.FoodInfo), typeof(List<ContentContainer>) });
            }
            if (_recipe == null)
            {
                _recipe = AccessTools.Method(typeof(CodexEntryGenerator), "GenerateRecipeContainers",
                    new Type[] { typeof(Tag), typeof(List<ContentContainer>) });
            }
        }

        // 反射读某个 IMultiEntityConfig 的 private static Def[] _defs，取 id/desc。
        private static void CollectDefIds(Type configType, Dictionary<string, string> idToDesc, List<string> order)
        {
            if (configType == null) return;
            FieldInfo f = configType.GetField("_defs", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return;

            Array arr = null;
            try { arr = f.GetValue(null) as Array; } catch { /* expected: 反射取静态表失败，跳过 */ }
            if (arr == null) return;

            for (int i = 0; i < arr.Length; i++)
            {
                object item = null;
                try { item = arr.GetValue(i); } catch { /* expected: 反射逐项取值失败，跳过该项 */ }
                if (item == null) continue;

                Type dt = item.GetType();
                string id = ReadFieldString(dt, item, "id");
                if (string.IsNullOrEmpty(id)) continue;
                if (idToDesc.ContainsKey(id)) continue;

                string desc = ReadFieldString(dt, item, "desc");
                idToDesc[id] = desc;
                order.Add(id);
            }
        }

        private static string ReadFieldString(Type t, object boxed, string field)
        {
            try
            {
                FieldInfo fi = t.GetField(field);
                if (fi == null) return null;
                return fi.GetValue(boxed) as string;
            }
            catch { /* expected: 反射读字段失败，按无值处理 */ return null; }
        }

        // 反射收集本程序集内所有 public static FoodInfo 字段（ModFoods + 饮品等）。
        private static List<EdiblesManager.FoodInfo> AllOurFoodInfos()
        {
            var result = new List<EdiblesManager.FoodInfo>();
            Assembly asm = typeof(CodexScanner).Assembly;
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            if (types == null) return result;

            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t == null) continue;
                FieldInfo[] fields;
                try { fields = t.GetFields(BindingFlags.Public | BindingFlags.Static); }
                catch { continue; }
                for (int k = 0; k < fields.Length; k++)
                {
                    if (fields[k].FieldType != typeof(EdiblesManager.FoodInfo)) continue;
                    try
                    {
                        var fi = fields[k].GetValue(null) as EdiblesManager.FoodInfo;
                        if (fi != null) result.Add(fi);
                    }
                    catch { /* expected: 反射取值失败，跳过该字段 */ }
                }
            }
            return result;
        }
    }

    // 百科初始化末尾：分类 + 原版条目均就绪，且此时 mod prefab 已注册 → 补全物件百科。
    [HarmonyPatch(typeof(CodexCache), "CodexCacheInit")]
    public static class CodexScanner_Init_Patch
    {
        public static void Postfix()
        {
            try
            {
                CodexScanner.BuildAllEntries();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:CodexScanner_Init_Patch", e); }
        }
    }

    // 原版信息面板「图书」按钮兜底：
    [HarmonyPatch(typeof(DetailsScreen), "CodexEntryButton_GetCodexId")]
    public static class CodexScanner_CodexButton_Patch
    {
        // 补丁：详情屏取描述文本后补进百科条目链接（点了能跳百科）
        public static void Postfix(DetailsScreen __instance, ref string __result)
        {
            try
            {
            if (__instance == null)
            {
                return;
            }
            GameObject target = __instance.target;
            if (target == null)
            {
                return;
            }

            KSelectable sel = target.GetComponent<KSelectable>();
            string proper = sel != null ? sel.GetProperName() : null;

            if (!CodexScanner.Diagnosed)
            {
                CodexScanner.Diagnosed = true;
                string pid = "?";
                try
                {
                    KPrefabID kp = target.GetComponent<KPrefabID>();
                    if (kp != null && kp.PrefabTag.IsValid)
                    {
                        pid = kp.PrefabTag.Name;
                    }
                }
                catch { /* expected: 预制体标签解析失败，保持原值 */ }
                MFLog.Verbose(LogCat.Codex, "[MoreFood/Codex] 诊断 图书按钮：name='" + proper + "' prefabId='" + pid
                          + "' 原版算出id='" + (__result ?? "") + "'");
            }

            if (!string.IsNullOrEmpty(__result))
            {
                return;   // 原版已经命中
            }

            string id;
            if (!CodexScanner.TryGetItemId(proper, out id))
            {
                return;
            }
            if (CodexCache.entries != null && CodexCache.entries.ContainsKey(id))
            {
                __result = id;
            }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:CodexScanner_CodexButton_Patch", e); }
        }
    }

    // 配方 / 用途面板的图片点击修复：
    [HarmonyPatch(typeof(CodexScreen), "ChangeArticle")]
    public static class CodexScanner_ChangeArticle_Patch
    {
        // Harmony 补丁：Prefix（参数 id：标识 id）
        public static void Prefix(ref string id)
        {
            try
            {
            if (string.IsNullOrEmpty(id)) return;
            if (CodexCache.entries == null) return;

            if (CodexCache.entries.ContainsKey(id)) return;   // 合法条目 id，放行

            string mapped;
            if (CodexScanner.TryGetItemId(id, out mapped) && CodexCache.entries.ContainsKey(mapped))
            {
                id = mapped;
                return;
            }

            // 兜底：名称 → 格式化 id（纯名称正好等于某条目 id 的少见情形）
            string fid = CodexCache.FormatLinkID(id);
            if (CodexCache.entries.ContainsKey(fid))
            {
                id = fid;
            }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:CodexScanner_ChangeArticle_Patch", e); }
        }
    }
}
