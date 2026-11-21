// 怪物百科分类：仿饮品分类，首页插在饮品之后、复用已生成的怪物条目、封面三只怪物等比合成。

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static class MonsterCodexCategory
    {
        internal static readonly string CategoryId = CodexCache.FormatLinkID("MoreFood_Monsters");

        private const string DisplayName = "怪物";
        private const string HomeId = "HOME";

        // 分类名带 link，面包屑与导航里的「怪物」才可点击跳回本分类
        private static readonly string LinkedName = STRINGS.UI.FormatAsLink(DisplayName, CategoryId);

        // 与食物卡同款三点斜向排布：左上猪人、右中邪天翁、左下座狼
        // 只给目标高度，宽度由精灵自身宽高比推导，绝不拉伸
        private static readonly CodexCategoryUtil.CoverPart[] CoverParts =
        {
            new CodexCategoryUtil.CoverPart("猪人", 0.29f, 0.74f, 0.44f, 8f),
            new CodexCategoryUtil.CoverPart("邪天翁", 0.69f, 0.53f, 0.50f, 24f),
            new CodexCategoryUtil.CoverPart("座狼", 0.31f, 0.30f, 0.52f, -20f)
        };

        // 三只封面怪物各自的专属 kanim，取图路径与怪物百科条目完全一致
        private static readonly Dictionary<string, string> CoverAnims =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "猪人", "pigman_kanim" },
                { "邪天翁", "malbatross_kanim" },
                { "座狼", "warg_kanim" }
            };

        // 本局怪物条目的 linkID，供中途加入的条目判重
        private static readonly HashSet<string> MonsterLinkIds = new HashSet<string>(StringComparer.Ordinal);
        private static bool _built;

        // 怪物条目由 MonsterCodexGenerator 建好后紧接着调用，不与它重复 AddEntry
        internal static void Build()
        {
            _built = false;
            RefreshLinkIds();

            if (CodexCache.entries == null)
            {
                MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 怪物分类跳过：CodexCache 未就绪");
                return;
            }
            if (CodexCache.entries.ContainsKey(CategoryId))
            {
                _built = true;
                return;
            }
            List<CombatMonsterDef> monsters = CombatDb.Monsters;
            if (monsters == null || monsters.Count == 0)
            {
                MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 怪物分类跳过：怪物表未就绪");
                return;
            }

            var entries = new Dictionary<string, CodexEntry>(StringComparer.Ordinal);
            int reused = 0, pending = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                CombatMonsterDef m = monsters[i];
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                string linkId = CodexCache.FormatLinkID(MonsterCodexGenerator.LinkId(m.id));
                MonsterLinkIds.Add(linkId);

                CodexEntry entry;
                if (!CodexCache.entries.TryGetValue(linkId, out entry))
                {
                    pending++;   // 条目还没生成：等 AddEntry 事件增量补挂，绝不自己建
                    continue;
                }
                entry.parentId = CategoryId;
                entry.category = CategoryId;
                entries[linkId] = entry;
                reused++;
            }

            if (entries.Count == 0)
            {
                MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 怪物分类跳过：本轮没有可复用的怪物条目");
                return;
            }

            Sprite icon = ResolveCardIcon();
            CategoryEntry category = CodexEntryGenerator.GenerateCategoryEntry(
                CategoryId, LinkedName, entries, icon, true, true, null);
            CodexEntryGenerator.PopulateCategoryEntries(new List<CategoryEntry> { category }, null);
            InsertHomeCard(category, icon);
            _built = true;

            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] 怪物分类：扫描 {0} 只，复用 {1} 条，待补挂 {2} 条",
                       monsters.Count, reused, pending);
        }

        // 早退分支也要留住 id 表，否则重复进游戏时增量补挂会认不出后补的怪物条目
        private static void RefreshLinkIds()
        {
            MonsterLinkIds.Clear();
            List<CombatMonsterDef> monsters = CombatDb.Monsters;
            if (monsters == null) return;
            for (int i = 0; i < monsters.Count; i++)
            {
                CombatMonsterDef m = monsters[i];
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                MonsterLinkIds.Add(CodexCache.FormatLinkID(MonsterCodexGenerator.LinkId(m.id)));
            }
        }

        // 封面优先用三只怪物合成图，合成失败才退回单只图标
        private static Sprite ResolveCardIcon()
        {
            Sprite cover = CodexCategoryUtil.BuildCompositeCover("monster", CoverParts, ResolveMonsterIcon);
            if (cover != null) return cover;

            Sprite unknown = CodexCategoryUtil.UnknownIcon();
            for (int i = 0; i < CoverParts.Length; i++)
            {
                try
                {
                    Sprite icon = ResolveMonsterIcon(CoverParts[i]);
                    if (CodexCategoryUtil.IsRealIcon(icon, unknown)) return icon;
                }
                catch (System.Exception e)
                {
                    MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 怪物图标读取失败 {0}", CoverParts[i].Id);
                }
            }
            return unknown;
        }

        // 怪物美术只有一帧 ui，取法与 MonsterCodexFactory 的条目图标一致
        private static Sprite ResolveMonsterIcon(CodexCategoryUtil.CoverPart part)
        {
            string anim;
            if (!CoverAnims.TryGetValue(part.Id, out anim)) return null;
            KAnimFile file = Assets.GetAnim(anim);
            if (file == null) return null;
            return Def.GetUISpriteFromMultiObjectAnim(file, "ui", false, "");
        }

        // 复用原版大图标控件，把卡片插在首页饮品卡右侧；饮品卡尚未插入时先放食物卡右侧
        private static void InsertHomeCard(CategoryEntry category, Sprite icon)
        {
            try
            {
                CodexEntry home;
                if (!CodexCache.entries.TryGetValue(HomeId, out home))
                {
                    MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 首页分类缺失，怪物卡未挂");
                    return;
                }
                CategoryEntry homeCategory = home as CategoryEntry;
                ContentContainer grid = homeCategory != null
                    ? CodexCategoryUtil.FindLargeIconGrid(home.contentContainers) : null;
                if (homeCategory == null || grid == null)
                {
                    MFLog.Warn(LogCat.Codex, "[MoreFood/Codex] 首页卡片容器缺失，怪物卡未挂");
                    return;
                }

                if (!homeCategory.entriesInCategory.Contains(category))
                {
                    homeCategory.entriesInCategory.Add(category);
                }

                Sprite cardIcon = icon != null ? icon : CodexCategoryUtil.UnknownIcon();
                var card = new CodexLabelWithLargeIcon(LinkedName, CodexTextStyle.BodyWhite,
                    new Tuple<Sprite, Color>(cardIcon, Color.white), CategoryId);

                int insertAt = -1;
                for (int i = 0; i < grid.content.Count; i++)
                {
                    CodexLabelWithLargeIcon w = grid.content[i] as CodexLabelWithLargeIcon;
                    if (w != null && string.Equals(w.linkID, DrinkCodexCategory.CategoryId,
                                                   StringComparison.Ordinal))
                    {
                        insertAt = i + 1;
                        break;
                    }
                }
                if (insertAt < 0)
                {
                    // 饮品卡稍后插到食物卡右侧时，会把这张卡挤到它后面，顺序仍是食物 / 饮品 / 怪物
                    for (int i = 0; i < grid.content.Count; i++)
                    {
                        CodexLabelWithLargeIcon w = grid.content[i] as CodexLabelWithLargeIcon;
                        if (w != null && string.Equals(w.linkID, CodexEntryGenerator.FOOD_CATEGORY_ID,
                                                       StringComparison.Ordinal))
                        {
                            insertAt = i + 1;
                            break;
                        }
                    }
                }

                if (insertAt >= 0)
                {
                    grid.content.Insert(insertAt, card);
                }
                else
                {
                    grid.content.Add(card);
                    MFLog.Warn("codex:monster:noAnchorCard", LogCat.Codex,
                               "[MoreFood/Codex] 未找到饮品卡或食物卡，怪物卡已追加到首页末尾");
                }
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.Codex, e, "[MoreFood/Codex] 首页怪物卡插入失败");
            }
        }

        // 仿原版 AddEntry 的 parentId 归集：后补的怪物条目同步进分类页，不重扫
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

            bool isMonster = MonsterLinkIds.Contains(linkId)
                             || string.Equals(entry.parentId, CategoryId, StringComparison.Ordinal);
            if (!isMonster) return;

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
            MFLog.Info(LogCat.Codex, "[MoreFood/Codex] 怪物分类增量补挂：{0}", linkId);
        }
    }

    // 中途加入的怪物条目按原版 parentId 归集口径补进分类页
    [HarmonyPatch(typeof(CodexCache), "AddEntry")]
    public static class MonsterCodex_AddEntry_Patch
    {
        public static void Postfix(string id, CodexEntry entry)
        {
            try { MonsterCodexCategory.OnEntryAdded(id, entry); }
            catch (System.Exception e) { MFLog.HotFail("patch:MonsterCodex_AddEntry_Patch", e); }
        }
    }
}
