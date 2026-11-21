// 用途：战斗数据加载：目录扫描、mod 覆盖与原始表缓存

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoreFood_KFC
{
    internal static partial class CombatDb
    {
        // CombatDb
        // 本 Mod 标题（由 Init 注入）：多 Mod 扫描时据此排除自身，本地配置另有独立加载路径
        internal static string OwnModTitle;

        internal static CombatSharedCfg Shared = new CombatSharedCfg();

        internal static readonly List<CombatMonsterDef> Monsters = new List<CombatMonsterDef>();
        internal static readonly List<AmmoDef> Ammo = new List<AmmoDef>();
        internal static readonly List<ModuleDef> Modules = new List<ModuleDef>();
        internal static readonly List<RocketComponentSlots> Components = new List<RocketComponentSlots>();
        internal static readonly List<RocketDef> Rockets = new List<RocketDef>();

        internal static SpawnerCfg SpawnMain = new SpawnerCfg { id = "main", region = "main" };
        internal static SpawnerCfg SpawnSub = new SpawnerCfg { id = "sub", region = "sub" };

        // 索引（K6：查字典优先）
        private static Dictionary<string, CombatMonsterDef> monsterById = new Dictionary<string, CombatMonsterDef>();
        private static Dictionary<string, AmmoDef> ammoById = new Dictionary<string, AmmoDef>();
        private static Dictionary<string, AmmoDef> ammoByPrefab = new Dictionary<string, AmmoDef>();
        private static Dictionary<string, AmmoDef> ammoByTag = new Dictionary<string, AmmoDef>();
        private static Dictionary<string, ModuleDef> moduleById = new Dictionary<string, ModuleDef>();
        private static Dictionary<string, ModuleDef> moduleByPrefab = new Dictionary<string, ModuleDef>();
        private static Dictionary<string, List<AmmoDef>> ammoByCaliber = new Dictionary<string, List<AmmoDef>>();
        private static Dictionary<string, RocketComponentSlots> componentByPrefab = new Dictionary<string, RocketComponentSlots>();

        internal static bool loaded;
        internal static bool overlaid;
        internal static bool built;

        // 原始暂存
        private static MonsterRaw rawMonsterDefault = new MonsterRaw();
        private static readonly List<MonsterRaw> rawMonsters = new List<MonsterRaw>();
        private static readonly List<AmmoRaw> rawAmmo = new List<AmmoRaw>();
        private static readonly List<ModuleRaw> rawModules = new List<ModuleRaw>();
        private static readonly List<RocketComponentRaw> rawComponents = new List<RocketComponentRaw>();
        private static readonly List<RocketDefRaw> rawRockets = new List<RocketDefRaw>();
        private static int lootFieldFileCount;

        internal static string RootDir
        {
            get
            {
                try
                {
                    return Path.Combine(Path.GetDirectoryName(typeof(CombatDb).Assembly.Location), "combat");
                }
                catch (Exception)
                {
                    return "combat";
                }
            }
        }

        // 本地加载

        internal static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            string root = RootDir;
            ContentDiag.Begin("combat", LogCat.Combat);

            CombatSharedRaw combat = LoadOne<CombatSharedRaw>(Path.Combine(root, "shared", "combat.yaml"));
            ApplyCombatShared(combat);
            ApplyMovementShared(LoadOne<MovementSharedRaw>(Path.Combine(root, "shared", "movement.yaml")));
            ApplyRegenShared(LoadOne<RegenSharedRaw>(Path.Combine(root, "shared", "regen.yaml")));
            ApplyLootShared(LoadOne<LootSharedRaw>(Path.Combine(root, "shared", "loot.yaml")));
            ApplyAmmoShared(LoadOne<AmmoSharedRaw>(Path.Combine(root, "shared", "ammo.yaml")));
            ApplyRocketShared(LoadOne<RocketSharedRaw>(Path.Combine(root, "shared", "rocket.yaml")));
            ApplyEmergencyShared(LoadOne<EmergencySharedRaw>(Path.Combine(root, "shared", "emergency.yaml")));
            ApplyFxShared(LoadOne<FxSharedRaw>(Path.Combine(root, "shared", "fx.yaml")));
            ApplyUiShared(LoadOne<UiSharedRaw>(Path.Combine(root, "shared", "ui.yaml")));
            ApplySaveShared(LoadOne<SaveSharedRaw>(Path.Combine(root, "shared", "save.yaml")));

            MonsterDefaultsFile defaults = LoadOne<MonsterDefaultsFile>(Path.Combine(root, "shared", "monsters.yaml"));
            if (defaults != null && defaults.monster_default != null)
            {
                rawMonsterDefault = defaults.monster_default;
            }

            SpawnControllerFile main = LoadOne<SpawnControllerFile>(Path.Combine(root, "spawn", "main.yaml"));
            if (main != null && main.spawn_controller != null)
            {
                SpawnMain = MaterializeSpawner(main.spawn_controller, SpawnMain);
            }
            SpawnControllerFile sub = LoadOne<SpawnControllerFile>(Path.Combine(root, "spawn", "sub.yaml"));
            if (sub != null && sub.spawn_controller != null)
            {
                SpawnSub = MaterializeSpawner(sub.spawn_controller, SpawnSub);
            }

            int monsterFiles = LoadDir<MonstersFileDef>(Path.Combine(root, "monsters"), delegate (MonstersFileDef f, string file)
            {
                AddMonsters(f != null ? f.monsters : null, file);
            });
            int ammoFiles = LoadDir<AmmoFileDef>(Path.Combine(root, "ammo.yaml"), delegate (AmmoFileDef f, string file)
            {
                AddAmmo(f != null ? f.ammo : null, file);
            });
            int moduleFiles = LoadDir<ModulesFileDef>(Path.Combine(root, "modules"), delegate (ModulesFileDef f, string file)
            {
                AddModules(f != null ? f.modules : null, file);
            });
            int rocketFiles = LoadRocketsDir(Path.Combine(root, "rockets"));
            LoadOne<LootFieldsFileDef>(Path.Combine(root, "loot", "fields.yaml"));
            if (File.Exists(Path.Combine(root, "loot", "fields.yaml")))
            {
                lootFieldFileCount = 1;
            }
            // 小行星带配置：加载期解析
            AsteroidBeltPool.Load(root);

            MFLog.Info(LogCat.Combat,"[Space] 战斗配置（本地）：monster 文件 " + monsterFiles + "（条目 " + rawMonsters.Count
                + "）ammo " + ammoFiles + "（" + rawAmmo.Count + "）module " + moduleFiles + "（" + rawModules.Count
                + "）rocket " + rocketFiles + " components " + rawComponents.Count + " rockets " + rawRockets.Count
                + " lootfields " + lootFieldFileCount);
            ContentDiag.End("combat");
        }

        // 怪物条目统一入口：缺 id 的直接跳过并说明，其余记下来源文件供后续诊断
        private static void AddMonsters(List<MonsterRaw> list, string file)
        {
            if (list == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                MonsterRaw m = list[i];
                if (m == null)
                {
                    ContentDiag.Skip("combat", file, "#" + (i + 1), "条目为空", "删掉这个空的 '- ' 项");
                    continue;
                }
                if (string.IsNullOrEmpty(m.id))
                {
                    ContentDiag.Skip("combat", file,
                        "#" + (i + 1) + (string.IsNullOrEmpty(m.name) ? "" : "「" + m.name + "」"),
                        "缺少 id", "补 id: <唯一短名>，刷怪与狩猎任务都靠它索引");
                    continue;
                }
                m.srcFile = file;
                rawMonsters.Add(m);
            }
        }

        // 弹药条目统一入口
        private static void AddAmmo(List<AmmoRaw> list, string file)
        {
            if (list == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                AmmoRaw a = list[i];
                if (a == null || string.IsNullOrEmpty(a.id))
                {
                    ContentDiag.Skip("combat", file, "#" + (i + 1), "弹药缺少 id", "补 id: <唯一短名>");
                    continue;
                }
                a.srcFile = file;
                rawAmmo.Add(a);
            }
        }

        // 模块条目统一入口
        private static void AddModules(List<ModuleRaw> list, string file)
        {
            if (list == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                ModuleRaw m = list[i];
                if (m == null || string.IsNullOrEmpty(m.id))
                {
                    ContentDiag.Skip("combat", file, "#" + (i + 1), "模块缺少 id", "补 id: <唯一短名>");
                    continue;
                }
                m.srcFile = file;
                rawModules.Add(m);
            }
        }

        private static int LoadRocketsDir(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return 0;
            }
            string[] files = Directory.GetFiles(dir, "*.yaml");
            int ok = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string text = ReadText(files[i]);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }
                ApplyOneRocketsFile(text, files[i]);
                ok++;
            }
            return ok;
        }

        // 其它 Mod 覆盖

        internal static void OverlayMods()
        {
            if (overlaid || !loaded)
            {
                return;
            }
            overlaid = true;
            string ownRoot = null;
            try
            {
                ownRoot = Path.GetDirectoryName(typeof(CombatDb).Assembly.Location);
            }
            catch (Exception e)
            {
                MFLog.Exc("combat:ownroot", LogCat.Combat, e,
                    "[Space] 取自身程序集目录失败 → 可能把本 Mod 的 combat/ 当其它 Mod 重复合并");
            }
            List<ModYamlFile> files;
            ContentDiag.Begin("combat-覆盖", LogCat.Combat);
            try
            {
                files = ModYamlScanner.Scan(new string[] { "combat" }, "combat", "combat-覆盖");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 多 Mod 战斗扫描失败（只用本地 combat/）");
                ContentDiag.End("combat-覆盖");
                return;
            }
            int applied = 0;
            for (int i = 0; i < files.Count; i++)
            {
                ModYamlFile file = files[i];
                if (file == null || string.IsNullOrEmpty(file.text) || string.IsNullOrEmpty(file.filePath)
                    || string.IsNullOrEmpty(file.modRoot))
                {
                    continue;
                }
                // 标题即自身：直接跳过（本地 combat/ 已由独立路径加载，再解析会重复定义）
                if (!string.IsNullOrEmpty(OwnModTitle)
                    && string.Equals(file.modTitle, OwnModTitle, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(ownRoot)
                    && string.Equals(file.modRoot.TrimEnd('\\', '/'), ownRoot.TrimEnd('\\', '/'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string rel;
                try
                {
                    rel = file.filePath.Substring(file.modRoot.Length).TrimStart('\\', '/');
                }
                catch (Exception)
                {
                    continue;
                }
                string[] parts = rel.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }
                string section = parts[1].ToLowerInvariant();
                string src = file.modTitle + "/" + rel;
                try
                {
                    if (section == "config.yaml" || section == "shared" || section == "spawn")
                    {
                        // 同一 Mod 的同一目录只提示一次，避免一个目录多个文件刷出十几条同义警告
                        MFLog.Warn("combatcfg:" + file.modTitle + "/" + section, LogCat.Combat,
                            "[Space] 忽略 " + file.modTitle + " 的 combat/" + section + "：系统配置只认本地一份");
                        ContentDiag.Skip("combat-覆盖", src, "", "系统配置（config/shared/spawn）只认本 Mod 一份，整目录被忽略",
                            "要改这些参数请直接改本 Mod 的 combat/ 同名文件");
                        continue;
                    }
                    if (section == "monsters")
                    {
                        MonstersFileDef one = ParseOne<MonstersFileDef>(file.text, src);
                        if (one != null && one.monsters != null)
                        {
                            AddMonsters(one.monsters, src);
                            applied++;
                        }
                    }
                    else if (section == "ammo.yaml")
                    {
                        AmmoFileDef one = ParseOne<AmmoFileDef>(file.text, src);
                        if (one != null && one.ammo != null)
                        {
                            AddAmmo(one.ammo, src);
                            applied++;
                        }
                    }
                    else if (section == "modules")
                    {
                        ModulesFileDef one = ParseOne<ModulesFileDef>(file.text, src);
                        if (one != null && one.modules != null)
                        {
                            AddModules(one.modules, src);
                            applied++;
                        }
                    }
                    else if (section == "rockets")
                    {
                        ApplyOneRocketsFile(file.text, src);
                        applied++;
                    }
                    else if (section == "loot")
                    {
                        applied++;   // 预留：小行星带自定义表
                    }
                    else
                    {
                        ContentDiag.Skip("combat-覆盖", src, "", "未识别的 combat 子目录 '" + section + "'，整份未加载",
                            "只认 monsters / ammo.yaml / modules / rockets / loot / shared / spawn / config.yaml");
                    }
                }
                catch (Exception e)
                {
                    MFLog.Exc(LogCat.Combat, e, "[Space] 战斗覆盖文件处理失败 {0}", src);
                    ContentDiag.File("combat-覆盖", src, "应用阶段抛异常：" + e.Message, "该文件后续条目未生效");
                }
            }
            MFLog.Info(LogCat.Combat,"[Space] 多 Mod 战斗扫描完成：其它 Mod 覆盖文件 " + applied + " 个");
            ContentDiag.End("combat-覆盖");
        }

        private static void ApplyOneRocketsFile(string text, string src)
        {
            // rockets/ 目录下可能是 components.yaml / rockets.yaml —— 先静默探测，再按命中的结构解析
            RocketComponentsFileDef components = YamlLoad.TryParse<RocketComponentsFileDef>(text);
            if (components != null && components.rocket_components != null
                && components.rocket_components.Count > 0)
            {
                rawComponents.AddRange(components.rocket_components);
                return;
            }
            RocketsFileDef rockets = YamlLoad.TryParse<RocketsFileDef>(text);
            if (rockets != null && rockets.rockets != null && rockets.rockets.Count > 0)
            {
                rawRockets.AddRange(rockets.rockets);
                return;
            }
            ContentDiag.File("combat-覆盖", src, "既不是 rocket_components 结构，也不是 rockets 结构",
                "顶层键写 rocket_components: [...] 或 rockets: [...]");
        }
    }
}
