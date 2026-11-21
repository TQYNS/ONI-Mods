using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // ★ 问题15：怪物百科「自动化工厂」

    internal interface ICodexGenerator
    {
        void Build();
    }

    internal static class CodexFactory
    {
        private static readonly List<ICodexGenerator> generators = new List<ICodexGenerator>();
        private static bool registered;

        static CodexFactory()
        {
            try
            {
                generators.Add(new MonsterCodexGenerator());
            }
            catch (System.Exception e) { MFLog.Exc(LogCat.Codex, e, "[Space] 百科工厂静态初始化失败"); }
        }

        internal static void Add(ICodexGenerator gen)
        {
            if (gen != null)
            {
                generators.Add(gen);
            }
        }

        /// <summary>运行时自动挂载：把所有生成器跑一遍（幂等，重复调用无副作用）。</summary>
        internal static void EnsureRegistered()
        {
            if (registered)
            {
                return;
            }
            registered = true;
            for (int i = 0; i < generators.Count; i++)
            {
                try
                {
                    generators[i].Build();
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] 百科生成失败");
                }
            }
        }

        internal static void Reset()
        {
            registered = false;
        }
    }

    // 挂在原版百科初始化末尾：此时分类 + 其它条目已就绪，可安全 AddEntry。
    [HarmonyPatch(typeof(CodexCache), "CodexCacheInit")]
    internal static class CodexCacheInitPatch
    {
        private static void Postfix()
        {
            try
            {
                CodexFactory.Reset();
                CodexFactory.EnsureRegistered();
            }
            catch (System.Exception e) { MFLog.HotFail("patch:CodexCacheInitPatch", e); }
        }
    }

    /// <summary>怪物百科生成器：遍历 CombatDb.Monsters，按 yaml 字段自动组装条目。</summary>
    internal sealed class MonsterCodexGenerator : ICodexGenerator
    {
        internal const string IdPrefix = "MONSTER_";

        /// <summary>怪物 ID → 百科 linkID（供 UI.FormatAsLink 使用）。</summary>
        internal static string LinkId(string monsterId)
        {
            return IdPrefix + (monsterId == null ? "" : monsterId.ToUpperInvariant());
        }

        public void Build()
        {
            if (CombatDb.Monsters == null)
            {
                Debug.LogWarning("[Space] 百科工厂：CombatDb.Monsters 为空（时序？）");
                return;
            }
            // Game.OnSpawn 早于 CodexCacheInit 时 entries 仍为 null；
            // 此时 AddEntry 会 NRE 并走 LogError（LogCatcher 会把它变成崩溃弹窗）。
            // 直接跳过，等 CodexCacheInit Postfix 在百科字典建好后再生成。
            if (CodexCache.entries == null)
            {
                MFLog.Info(LogCat.Space, "[Space] 百科工厂：CodexCache 尚未初始化，延后到 CodexCacheInit 再生成");
                return;
            }
            int made = 0;
            for (int i = 0; i < CombatDb.Monsters.Count; i++)
            {
                CombatMonsterDef m = CombatDb.Monsters[i];
                if (m == null || string.IsNullOrEmpty(m.id))
                {
                    continue;
                }
                string id = LinkId(m.id);
                if (CodexCache.entries != null && CodexCache.entries.ContainsKey(CodexCache.FormatLinkID(id)))
                {
                    continue;
                }
                try
                {
                    BuildOne(m, id);
                    made++;
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] 怪物百科 " + m.id + " 生成失败");
                }
            }
            MFLog.Info(LogCat.Space, "[Space] ★ 百科工厂：生成 " + made + " 条怪物百科（共 " + CombatDb.Monsters.Count + " 只，linkID 前缀 " + IdPrefix + "）");
            // 条目已全部落进 CodexCache，此时建分类才能复用现成条目；分类内部不会再 AddEntry
            try { MonsterCodexCategory.Build(); }
            catch (Exception e) { MFLog.Exc(LogCat.Codex, e, "[Space] 怪物百科分类生成失败"); }
        }

        private void BuildOne(CombatMonsterDef m, string id)
        {
            List<ICodexWidget> widgets = new List<ICodexWidget>();
            widgets.Add(new CodexText(m.name + "  " + m.tier + " 级", CodexTextStyle.Title, null));
            if (!string.IsNullOrEmpty(m.description))
            {
                widgets.Add(new CodexText(m.description, CodexTextStyle.Body, null));
            }
            widgets.Add(new CodexDividerLine());
            widgets.Add(new CodexText("生命值：" + Mathf.Round(m.hp), CodexTextStyle.Body, null));
            widgets.Add(new CodexText("装甲：" + Mathf.Round(m.armor), CodexTextStyle.Body, null));
            widgets.Add(new CodexText("护甲（防御）：" + Mathf.Round(m.defense), CodexTextStyle.Body, null));
            widgets.Add(new CodexText("攻击：" + Mathf.Round(m.attack)
                + "（穿甲 " + Mathf.Round(m.armorPenetration) + "）", CodexTextStyle.Body, null));
            widgets.Add(new CodexText("攻击间隔：" + m.attackInterval.ToString("0.#") + " 秒", CodexTextStyle.Body, null));
            widgets.Add(new CodexText("攻击射程：" + m.attackRange + " 格（警戒 " + m.alertRange + " 格）", CodexTextStyle.Body, null));
            widgets.Add(new CodexText("阵营：" + (m.hostile ? "敌对（主动开火）" : "中立（需下令）"), CodexTextStyle.Body, null));
            widgets.Add(new CodexText("可逃跑：" + (m.canFlee ? "是（低于 " + Mathf.RoundToInt(m.fleeHpPct * 100f) + "% 血）" : "否"), CodexTextStyle.Body, null));

            AddLootSection(widgets, m);   // ★ 问题3：掉落物

            List<ContentContainer> content = new List<ContentContainer>();
            content.Add(new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical));

            // CodexEntry(category, titleKey, contentContainers)；titleKey 用运行时名（不依赖 STRINGS）
            CodexEntry entry = new CodexEntry("CREATURES", m.name, content);
            entry.name = m.name;
            entry.parentId = "CREATURES";
            // ★ 图标 = 怪物自己的 UI 贴图（anim/<id> 的 ui 帧）。怪物美术本来就只有一帧 ui，
            //   以前借原版长途导弹当图标 ⇒ 20 只怪全是导弹，还会刷 "Missing prefab: MONSTERxxx"。
            Sprite icon = MonsterIcon(m.anim);
            if (icon != null)
            {
                if (entryIconField == null)
                {
                    entryIconField = AccessTools.Field(typeof(CodexEntry), "_icon");
                }
                if (entryIconField != null)
                {
                    entryIconField.SetValue(entry, icon);
                }
            }
            CodexCache.AddEntry(id, entry);
        }

        private static FieldInfo entryIconField;

        // 取怪物 kanim 的 ui 帧；取不到就返回 null（图鉴照常建，不设图标）
        private static Sprite MonsterIcon(string anim)
        {
            try
            {
                if (string.IsNullOrEmpty(anim)) return null;
                KAnimFile file = Assets.GetAnim(anim);
                if (file == null) return null;
                return Def.GetUISpriteFromMultiObjectAnim(file, "ui", false, "");
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>★ 问题3：把 yaml 的掉落物（tag + kg 区间 + 概率）写进百科。</summary>
        private static void AddLootSection(List<ICodexWidget> widgets, CombatMonsterDef m)
        {
            if (m.loot == null || m.loot.Count == 0)
            {
                return;
            }
            widgets.Add(new CodexDividerLine());
            widgets.Add(new CodexText("掉落物", CodexTextStyle.Title, null));

            // 有真实图标的条目置顶，取不到图标的纯文本条目沉底
            var withIcon = new List<ICodexWidget>();
            var plain = new List<ICodexWidget>();
            for (int i = 0; i < m.loot.Count; i++)
            {
                CombatLootEntry le = m.loot[i];
                if (le == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(le.group))
                {
                    AddPoolLootLine(withIcon, plain, le);
                    continue;
                }
                if (string.IsNullOrEmpty(le.item))
                {
                    continue;
                }
                // ★ 百科设定：ID 未注册（既非 prefab 也非元素）→ 跳过该条 + 短日志
                if (!ItemExists(le.item))
                {
                    if (InvalidItemLogged.Add(le.item))
                    {
                        Debug.LogWarning("[Space] 百科掉落物：「" + le.item + "」未定义/未加载（怪物 " + m.id + "）→ 已跳过");
                    }
                    continue;
                }
                string amount = Mathf.Approximately(le.min, le.max)
                    ? le.min.ToString("0.##") + " 千克"
                    : le.min.ToString("0.##") + "~" + le.max.ToString("0.##") + " 千克";
                string chance = le.chance >= 1f ? "" : "（" + Mathf.RoundToInt(le.chance * 100f) + "%）";
                string label = ItemName(le.item) + "：" + amount + chance;
                AddLootLine(withIcon, plain, label, le.item);
            }
            widgets.AddRange(withIcon);
            widgets.AddRange(plain);

            string field = (m.lootField != null && !string.IsNullOrEmpty(m.lootField.name))
                ? m.lootField.name : (m.name + "残骸带");
            widgets.Add(new CodexText("击杀后残骸落在「" + field + "」小行星带，可派火箭前往打捞。", CodexTextStyle.Body, null));
        }

        private static readonly HashSet<string> InvalidItemLogged = new HashSet<string>(StringComparer.Ordinal);

        // 占位 unknown 不算「有贴图」；有图行走 CodexLabelWithIcon，无图行走纯文本
        private static void AddLootLine(List<ICodexWidget> withIcon, List<ICodexWidget> plain,
            string label, string itemId)
        {
            Tuple<Sprite, Color> icon = RealIcon(itemId);
            if (icon != null)
            {
                withIcon.Add(new CodexLabelWithIcon(label, CodexTextStyle.Body, icon));
            }
            else
            {
                plain.Add(new CodexText(label, CodexTextStyle.Body, null));
            }
        }

        private static Tuple<Sprite, Color> RealIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            try
            {
                Tuple<Sprite, Color> icon = Def.GetUISprite(new Tag(itemId), "ui", false);
                if (icon != null && CodexCategoryUtil.IsRealIcon(icon.first, CodexCategoryUtil.UnknownIcon()))
                {
                    return icon;
                }
            }
            catch { /* expected: 取不到图标按纯文本处理 */ }
            return null;
        }

        // 池条目只显示「随机 N 种 · 池名 + 区间 + 概率」，不展开候选清单（避免百科构建时扫描）
        private static void AddPoolLootLine(List<ICodexWidget> withIcon, List<ICodexWidget> plain,
            CombatLootEntry le)
        {
            int lo = le.picksLo;
            int hi = le.picksHi;
            if (lo <= 0)
            {
                AsteroidBeltPool.GroupPicks(le.group, out lo, out hi);   // yaml 没写 picks → 用组默认
            }
            if (lo <= 0)
            {
                lo = 1;
            }
            if (hi <= 0)
            {
                hi = lo;
            }
            if (hi < lo)
            {
                int t = lo;
                lo = hi;
                hi = t;
            }
            string kinds = lo == hi ? lo.ToString() : lo + "~" + hi;
            string unit = UnitOf(le.group);
            string amount = !le.hasRange ? "按该池档位"
                : (Mathf.Approximately(le.min, le.max)
                    ? le.min.ToString("0.##") + " " + unit
                    : le.min.ToString("0.##") + "~" + le.max.ToString("0.##") + " " + unit);
            string chance = le.chance >= 1f ? "" : "（" + Mathf.RoundToInt(le.chance * 100f) + "%）";
            string label = GroupLabel(le.group) + "：随机 " + kinds + " 种 · 每种 " + amount + chance;

            string sample = AsteroidBeltPool.FirstCandidate(le.group);
            AddLootLine(withIcon, plain, label, sample);
        }

        // 离散物品（弹药/模块/卷轴）按件数显示，其余按质量
        private static string UnitOf(string group)
        {
            switch ((group ?? "").Trim().ToLowerInvariant())
            {
                case "ammo": return "发";
                case "module":
                case "artifact":
                case "recipe":
                case "scroll_good":
                case "legendary": return "个";
                default: return "千克";
            }
        }

        private static string GroupLabel(string group)
        {
            switch ((group ?? "").Trim().ToLowerInvariant())
            {
                case "food": return "食物池";
                case "drink": return "饮品池";
                case "ore": return "原矿池";
                case "refined_metal": return "精炼金属池";
                case "mid_material": return "人造/稀有材料池";
                case "high_tier_material": return "高档材料池";
                case "manufactured_material": return "人造材料池";
                case "rare_material": return "稀有材料池";
                case "artifact": return "工艺品池";
                case "ammo": return "弹药池";
                case "module": return "火箭模块池";
                case "recipe": return "配方卷轴池";
                case "canned": return "罐头池";
                case "legendary": return "传奇物品池";
                case "scroll_good": return "精良任务卷轴";
                default: return string.IsNullOrEmpty(group) ? "随机池" : group;
            }
        }

        /// <summary>掉落物是否真实存在（prefab 或元素）。</summary>
        private static bool ItemExists(string item)
        {
            if (string.IsNullOrEmpty(item))
            {
                return false;
            }
            if (IngredientConfigs.IsIconOnlyItem(item))
            {
                return false;   // 纯图标条目不进百科掉落段
            }
            try
            {
                if (Assets.GetPrefab(new Tag(item)) != null)
                {
                    return true;
                }
            }
            catch { /* expected: prefab 探测失败按不存在处理，继续查元素 */ }
            try
            {
                if (ElementLoader.GetElement(new Tag(item)) != null)
                {
                    return true;
                }
            }
            catch { /* expected: 元素探测失败按不存在处理 */ }
            return false;
        }

        /// <summary>把掉落物 tag/prefabId 转成显示名（元素本地化名 → prefab 显示名 → tag 名 → 原串）。</summary>
        private static string ItemName(string item)
        {
            // 1) 真元素（硫 / 黑曜石等）→ 本地化元素名
            try
            {
                Element el = ElementLoader.GetElement(new Tag(item));
                if (el != null && !string.IsNullOrEmpty(el.name))
                {
                    return el.name;
                }
            }
            catch { /* expected: 元素名读取失败，回退到 prefab 显示名 */ }

            // 2) prefab → 复用交易侧统一显示名入口（KPrefabID 本地化名）；本 Mod 物品的元素统一是占位的「遗传生物软泥」，绝不能拿元素名当物品名
            try
            {
                string n = TradeUtil.DisplayName(item);
                if (!string.IsNullOrEmpty(n) && !string.Equals(n, item, StringComparison.Ordinal))
                {
                    return n;
                }
            }
            catch { /* expected: prefab 名读取失败，回退到 tag 显示名 */ }

            // 3) tag 显示名
            try
            {
                string n2 = TagManager.GetProperName(new Tag(item), false);
                if (!string.IsNullOrEmpty(n2) && !string.Equals(n2, item, StringComparison.Ordinal))
                {
                    return n2;
                }
            }
            catch { /* expected: tag 显示名读取失败，回退原串 */ }

            return item;
        }
    }

    // ★ 问题1：原版信息面板的「图书」小图标
    [HarmonyPatch(typeof(DetailsScreen), "CodexEntryButton_GetCodexId")]
    internal static class MonsterCodexButtonPatch
    {
        private static void Postfix(DetailsScreen __instance, ref string __result)
        {
            try
            {
            if (!string.IsNullOrEmpty(__result) || __instance == null)
            {
                return;
            }
            GameObject target = __instance.target;
            if (target == null)
            {
                return;
            }
            SpaceMonster sm = target.GetComponent<SpaceMonster>();
            if (sm == null || string.IsNullOrEmpty(sm.defId))
            {
                return;
            }
            string id = CodexCache.FormatLinkID(MonsterCodexGenerator.LinkId(sm.defId));
            if (CodexCache.entries != null && CodexCache.entries.ContainsKey(id))
            {
                __result = id;
            }
            }
            catch (System.Exception e) { MFLog.HotFail("patch:MonsterCodexButtonPatch", e); }
        }
    }
}
