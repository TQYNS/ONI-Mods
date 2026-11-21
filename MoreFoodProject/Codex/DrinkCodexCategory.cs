// 饮品百科分类：仿原版「食物」分类，首页插卡、按 Tag 一次扫描、条目增量补挂。

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class DrinkCodexCategory
    {
        internal static readonly string CategoryId = CodexCache.FormatLinkID("MoreFood_Drinks");

        private const string DisplayName = "饮品";
        private const string HomeId = "HOME";

        // 分类名带 link，面包屑与导航里的「饮品」才可点击跳回本分类
        private static readonly string LinkedName = STRINGS.UI.FormatAsLink(DisplayName, CategoryId);

        // 与 codexIconFood 同款的三点斜向排布：左上冰咖啡、右中白兰地、左下朗姆酒
        // 只给目标高度，宽度由精灵自身宽高比推导，绝不拉伸
        private static readonly CodexCategoryUtil.CoverPart[] CoverParts =
        {
            new CodexCategoryUtil.CoverPart("IcedCoffee", 0.29f, 0.74f, 0.44f, 8f),
            new CodexCategoryUtil.CoverPart("Brandy", 0.69f, 0.53f, 0.50f, 24f),
            new CodexCategoryUtil.CoverPart("Rum", 0.31f, 0.30f, 0.52f, -20f)
        };

        // 本局扫描到的饮品条目 id，供中途加入的条目判重
        private static readonly HashSet<string> DrinkLinkIds = new HashSet<string>(StringComparer.Ordinal);
        private static bool _built;

        // 与 CodexCacheInit 同一时机建立分类与首页卡片，开窗不再扫描
        internal static void Build()
        {
            _built = false;
            DrinkLinkIds.Clear();

            if (CodexCache.entries == null)
            {
                MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 饮品分类跳过：CodexCache 未就绪");
                return;
            }
            if (CodexCache.entries.ContainsKey(CategoryId))
            {
                _built = true;
                return;
            }

            List<string> ids = ScanDrinkIds();
            var entries = new Dictionary<string, CodexEntry>(StringComparer.Ordinal);
            int made = 0, reused = 0, skipped = 0;

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                string linkId = CodexCache.FormatLinkID(id);
                DrinkLinkIds.Add(linkId);

                CodexEntry entry;
                if (!CodexCache.entries.TryGetValue(linkId, out entry))
                {
                    try
                    {
                        if (CodexScanner.BuildGenericItem(id, CategoryId, null, null))
                        {
                            made++;
                            CodexCache.entries.TryGetValue(linkId, out entry);
                        }
                    }
                    catch (System.Exception e)
                    {
                        MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 饮品百科 {0} 生成失败", id);
                    }
                }
                else
                {
                    reused++;
                }

                if (entry == null)
                {
                    skipped++;
                    continue;
                }

                // 复用条目改挂到饮品分类，面包屑才会显示「索引 > 饮品 > xxx」
                entry.parentId = CategoryId;
                entry.category = CategoryId;
                entries[linkId] = entry;
            }

            Sprite icon = ResolveCardIcon(ids);
            CategoryEntry category = CodexEntryGenerator.GenerateCategoryEntry(
                CategoryId, LinkedName, entries, icon, true, true, null);
            CodexEntryGenerator.PopulateCategoryEntries(new List<CategoryEntry> { category }, null);
            InsertHomeCard(category, icon);
            _built = true;

            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] 饮品分类：扫描 {0} 款，新建 {1} 条，复用 {2} 条，跳过 {3} 条",
                       ids.Count, made, reused, skipped);
        }

        // 全表扫一遍带饮品 Tag 的 prefab，Tag 未就绪时退回饮品注册表
        private static List<string> ScanDrinkIds()
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                var prefabs = Assets.Prefabs;
                if (prefabs != null)
                {
                    for (int i = 0; i < prefabs.Count; i++)
                    {
                        var kpid = prefabs[i];
                        if (kpid == null || !kpid.HasTag(Drinks.DrinkItemTag)) continue;
                        if (kpid.HasTag(GameTags.DeprecatedContent) || kpid.HasTag(GameTags.IncubatableEgg)
                            || kpid.HasTag(GameTags.Seed) || kpid.HasTag(GameTags.CropSeed)) continue;
                        Tag tag = kpid.PrefabTag;
                        if (tag == null || string.IsNullOrEmpty(tag.Name)) continue;
                        if (seen.Add(tag.Name)) ids.Add(tag.Name);
                    }
                }
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 饮品 Tag 扫描失败");
            }

            if (ids.Count == 0)
            {
                try
                {
                    if (Drinks.ItemTable != null)
                    {
                        foreach (KeyValuePair<Tag, DrinkData> kv in Drinks.ItemTable)
                        {
                            if (kv.Key == null || string.IsNullOrEmpty(kv.Key.Name)) continue;
                            if (seen.Add(kv.Key.Name)) ids.Add(kv.Key.Name);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 饮品注册表兜底失败");
                }
                MFLog.Warn("codex:drink:tagEmpty", LogCat.Codex,
                           "[MoreFood/Codex] 未扫到带饮品 Tag 的 prefab，已退回饮品注册表（{0} 款）", ids.Count);
            }
            return ids;
        }

        // 取三款饮品 ui 精灵，交给共享合成器按原比例叠成封面
        private static Sprite BuildDrinkCover()
        {
            return CodexCategoryUtil.BuildCompositeCover("drink", CoverParts, DrinkIconResolver);
        }

        private static Sprite DrinkIconResolver(CodexCategoryUtil.CoverPart part)
        {
            return Def.GetUISprite(new Tag(part.Id), "ui", false).first;
        }

        // 封面优先用三款饮品合成图，合成失败才退回单款图标
        private static Sprite ResolveCardIcon(List<string> ids)
        {
            Sprite cover = BuildDrinkCover();
            if (cover != null) return cover;

            Sprite unknown = CodexCategoryUtil.UnknownIcon();
            for (int i = 0; i < ids.Count; i++)
            {
                try
                {
                    Sprite icon = Def.GetUISprite(new Tag(ids[i]), "ui", false).first;
                    if (CodexCategoryUtil.IsRealIcon(icon, unknown)) return icon;
                }
                catch (System.Exception e)
                {
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 饮品图标读取失败 {0}", ids[i]);
                }
            }
            return unknown;
        }

        // 复用原版大图标控件，把卡片插在首页食物卡右侧
        private static void InsertHomeCard(CategoryEntry category, Sprite icon)
        {
            try
            {
                CodexEntry home;
                if (!CodexCache.entries.TryGetValue(HomeId, out home))
                {
                    MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 首页分类缺失，饮品卡未挂");
                    return;
                }
                CategoryEntry homeCategory = home as CategoryEntry;
                ContentContainer grid = homeCategory != null ? CodexCategoryUtil.FindLargeIconGrid(home.contentContainers) : null;
                if (homeCategory == null || grid == null)
                {
                    MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 首页卡片容器缺失，饮品卡未挂");
                    return;
                }

                if (!homeCategory.entriesInCategory.Contains(category))
                {
                    homeCategory.entriesInCategory.Add(category);
                }

                Sprite cardIcon = icon != null ? icon : CodexCategoryUtil.UnknownIcon();
                var card = new CodexLabelWithLargeIcon(LinkedName, CodexTextStyle.BodyWhite,
                    new Tuple<Sprite, Color>(cardIcon, Color.white), CategoryId);

                int foodIdx = -1;
                for (int i = 0; i < grid.content.Count; i++)
                {
                    CodexLabelWithLargeIcon w = grid.content[i] as CodexLabelWithLargeIcon;
                    if (w != null && string.Equals(w.linkID, CodexEntryGenerator.FOOD_CATEGORY_ID,
                                                   StringComparison.Ordinal))
                    {
                        foodIdx = i;
                        break;
                    }
                }

                if (foodIdx >= 0)
                {
                    grid.content.Insert(foodIdx + 1, card);
                }
                else
                {
                    grid.content.Add(card);
                    MFLog.Warn("codex:drink:noFoodCard", LogCat.Codex,
                               "[MoreFood/Codex] 未找到食物卡，饮品卡已追加到首页末尾");
                }
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 首页饮品卡插入失败");
            }
        }

        // 仿原版 AddEntry 的 parentId 归集：后补的饮品条目同步进分类页，不重扫
        internal static void OnEntryAdded(string id, CodexEntry entry)
        {
            if (!_built || entry == null || CodexCache.entries == null) return;

            CodexEntry catEntry;
            if (!CodexCache.entries.TryGetValue(CategoryId, out catEntry)) return;
            CategoryEntry category = catEntry as CategoryEntry;
            if (category == null) return;

            string linkId = entry.id;
            if (string.IsNullOrEmpty(linkId)) linkId = CodexCache.FormatLinkID(id ?? "");
            if (string.IsNullOrEmpty(linkId)) return;

            bool isDrink = string.Equals(entry.parentId, CategoryId, StringComparison.Ordinal)
                           || DrinkLinkIds.Contains(linkId)
                           || IsDrinkPrefab(id);
            if (!isDrink) return;

            // 后补条目同样改挂到饮品分类，面包屑才与首页卡片一致
            entry.parentId = CategoryId;
            entry.category = CategoryId;

            ContentContainer grid = CodexCategoryUtil.FindLargeIconGrid(category.contentContainers);
            if (grid == null) return;
            for (int i = 0; i < grid.content.Count; i++)
            {
                CodexLabelWithLargeIcon w = grid.content[i] as CodexLabelWithLargeIcon;
                if (w != null && string.Equals(w.linkID, linkId, StringComparison.Ordinal)) return;
            }

            if (!category.entriesInCategory.Contains(entry)) category.entriesInCategory.Add(entry);
            Sprite icon = entry.icon != null ? entry.icon : CodexCategoryUtil.UnknownIcon();
            grid.content.Add(new CodexLabelWithLargeIcon(
                string.IsNullOrEmpty(entry.name) ? linkId : entry.name, CodexTextStyle.BodyWhite,
                new Tuple<Sprite, Color>(icon, entry.iconColor), linkId));
            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] 饮品分类增量补挂：{0}", linkId);
        }

        // 单次查 prefab 的饮品 Tag，负担随 AddEntry 次数而非帧数增长
        private static bool IsDrinkPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            GameObject prefab = Assets.GetPrefab(id);
            if (prefab == null) return false;
            KPrefabID kpid = prefab.GetComponent<KPrefabID>();
            return kpid != null && kpid.HasTag(Drinks.DrinkItemTag);
        }
    }

    // 百科初始化末尾：原版分类与首页卡片均已就绪，此处补建饮品分类
    [HarmonyPatch(typeof(CodexCache), "CodexCacheInit")]
    public static class DrinkCodex_Init_Patch
    {
        public static void Postfix()
        {
            try { DrinkCodexCategory.Build(); }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkCodex_Init_Patch", e); }
        }
    }

    // 中途加入的饮品条目按原版 parentId 归集口径补进分类页
    [HarmonyPatch(typeof(CodexCache), "AddEntry")]
    public static class DrinkCodex_AddEntry_Patch
    {
        public static void Postfix(string id, CodexEntry entry)
        {
            try { DrinkCodexCategory.OnEntryAdded(id, entry); }
            catch (System.Exception e) { MFLog.HotFail("patch:DrinkCodex_AddEntry_Patch", e); }
        }
    }
}
