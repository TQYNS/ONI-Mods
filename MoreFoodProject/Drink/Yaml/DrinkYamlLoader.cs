using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Klei;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class DrinkYamlLoader
    {
        // 认作 drink yaml 的目录名
        public static readonly string[] DIR_NAMES = { "drink", "Drink", "DrinkYaml" };

        public static int LoadedFiles { get; private set; }
        public static int LoadedDrinks { get; private set; }
        public static int FailedFiles { get; private set; }

// 加载全部 drink yaml（返回加载的文件数）
        public static int LoadAll()
        {
            LoadedFiles = 0;
            LoadedDrinks = 0;
            FailedFiles = 0;

            string modRoot;
            ContentDiag.Begin("drink", LogCat.Drink);
            try { modRoot = Path.GetDirectoryName(typeof(DrinkYamlLoader).Assembly.Location); }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Yaml] 取 Mod 目录异常（饮品配置未加载）");
                ContentDiag.End("drink");
                return 0;
            }
            if (string.IsNullOrEmpty(modRoot))
            {
                MFLog.Warn(LogCat.Drink, "[MoreFood/Drink][Yaml] Mod 目录为空，跳过饮品 YAML");
                ContentDiag.End("drink");
                return 0;
            }

            List<string> files = CollectFiles(modRoot);
            for (int i = 0; i < files.Count; i++)
            {
                LoadOne(files[i]);
            }

            MFLog.Info(LogCat.Drink, "[MoreFood/Drink][Yaml] 扫描完成：文件 " + LoadedFiles
                + "，饮品 " + LoadedDrinks + "，失败 " + FailedFiles);
            ContentDiag.End("drink");
            return LoadedDrinks;
        }

        private static List<string> CollectFiles(string modRoot)
        {
            var files = new List<string>();
            var dirs = new List<string>();
            for (int i = 0; i < DIR_NAMES.Length; i++)
            {
                string dir;
                try { dir = Path.Combine(modRoot, DIR_NAMES[i]); }
                catch { continue; }
                if (!Directory.Exists(dir)) continue;
                if (dirs.Exists(d => string.Equals(d, dir, StringComparison.OrdinalIgnoreCase))) continue;
                dirs.Add(dir);
            }

            for (int i = 0; i < dirs.Count; i++)
            {
                string[] found;
                try { found = Directory.GetFiles(dirs[i], "*.*", SearchOption.AllDirectories); }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Yaml] 枚举目录失败 {0}", dirs[i]);
                    ContentDiag.File("drink", dirs[i], "目录枚举失败：" + e.Message, "检查目录权限或是否被占用");
                    continue;
                }
                Array.Sort(found, StringComparer.Ordinal);
                for (int k = 0; k < found.Length; k++)
                {
                    string ext = Path.GetExtension(found[k]);
                    if (!ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                     && !ext.Equals(".yml", StringComparison.OrdinalIgnoreCase)) continue;
                    files.Add(found[k]);
                }
            }
            return files;
        }

        private static void LoadOne(string file)
        {
            string text;
            try { text = File.ReadAllText(file, new UTF8Encoding(false)); }
            catch (Exception e)
            {
                FailedFiles++;
                MFLog.Exc(LogCat.Drink, e, "[MoreFood/Drink][Yaml] 读取失败 {0}", file);
                ContentDiag.File("drink", file, "读取失败：" + e.Message, "检查文件是否被占用、编码是否为 UTF-8");
                return;
            }

            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                FailedFiles++;
                ContentDiag.File("drink", file, "空文件或只有空白", "删掉该文件，或按文档补齐内容");
                return;
            }

            DrinkYamlFile doc = YamlLoad.Parse<DrinkYamlFile>(text, file, "drink");
            if (doc == null)
            {
                FailedFiles++;
                return;
            }
            if (doc.drinks == null)
            {
                ContentDiag.File("drink", file, "顶层缺少 drinks 段", "顶层键写 drinks: 并在下面按 '饮品id: {字段}' 写条目");
                return;
            }

            LoadedFiles++;
            foreach (KeyValuePair<string, DrinkYamlDef> kv in doc.drinks)
            {
                if (kv.Value == null || string.IsNullOrEmpty(kv.Key)) continue;
                Apply(kv.Key, kv.Value, file);
            }
        }

        private static void Apply(string key, DrinkYamlDef def, string file)
        {
            string id = string.IsNullOrEmpty(def.id) ? key : def.id;
            Tag tag = new Tag(id);
            DrinkData data = ConsumeRegistry.Find(tag) as DrinkData;
            if (data == null) data = new DrinkData();
            bool foodOnly = def.food_only == true;

            if (def.hydration_per_kg.HasValue) data.hydrationPerKg = def.hydration_per_kg.Value;
            if (def.morale_bonus.HasValue) data.moraleBonus = def.morale_bonus.Value;
            if (def.max_serve_mass.HasValue) data.maxServeMass = def.max_serve_mass.Value;
            if (def.priority.HasValue) data.priority = def.priority.Value;
            if (def.sort.HasValue) data.sort = def.sort.Value;

            if (!string.IsNullOrEmpty(def.category))
            {
                string norm = NormalizeCategory(def.category);
                if (norm != def.category.Trim().ToLowerInvariant())
                {
                    ContentDiag.Fix("drink", file, "「" + id + "」",
                        "category='" + def.category + "' 不认识", "other（可选 wine / coffee / soda / other）");
                }
                data.category = norm;
            }
            if (def.garnish.HasValue) data.garnish = def.garnish.Value;
            if (!string.IsNullOrEmpty(def.garnish_anims)) data.garnishAnim = NormalizeAnim(def.garnish_anims);
            if (!string.IsNullOrEmpty(def.garnish_symbol)) data.garnishSymbol = def.garnish_symbol;
            if (def.garnish_consume_rate.HasValue) data.garnishConsumeRate = def.garnish_consume_rate.Value;
            if (def.garnish_priority.HasValue) data.garnishPriority = def.garnish_priority.Value;
            else if (data.garnishPriority <= 0) data.garnishPriority = Drinks.GetDefaultGarnishPriority(data.category);
            if (def.hangover_chance.HasValue) data.hangoverChance = def.hangover_chance.Value;

            if (def.effect != null)
            {
                if (string.IsNullOrEmpty(def.effect.id))
                {
                    ContentDiag.Suspect("drink", file, "「" + id + "」", "effect 没有 id（不会登记进效果表）",
                        "补 effect.id: <效果唯一短名>");
                }
                DrinkEffectRegistry.Register(def.effect);
                if (!string.IsNullOrEmpty(def.effect.id)) data.effectId = def.effect.id;
            }

            if (foodOnly)
            {
                data.isDrink = false;
                data.garnish = false;
                if (!def.max_serve_mass.HasValue) data.maxServeMass = 1f;
                ConsumeRegistry.Register(tag, data);
            }
            else
            {
                Drinks.RegisterItem(id, data);
                if (data.garnish)
                {
                    // 注册期 prefab 尚未进入 Assets，失败是预期 → quiet，不刷 WARNING（真正打标靠 OnAddPrefab 回调）
                    Drinks.EnsureDrinkTag(id, Drinks.GetCategoryTag(data.category), quiet: true);
                }
            }

            LoadedDrinks++;
        }

        private static string NormalizeCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return "other";
            string c = category.Trim().ToLowerInvariant();
            if (c == "wine" || c == "coffee" || c == "soda" || c == "other") return c;
            return "other";
        }

        private static string NormalizeAnim(string anim)
        {
            if (string.IsNullOrEmpty(anim)) return null;
            anim = anim.Trim();
            if (!anim.EndsWith("_kanim", StringComparison.OrdinalIgnoreCase)) anim += "_kanim";
            return anim;
        }
    }
}
