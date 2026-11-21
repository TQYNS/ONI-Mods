// Init.cs — 模组入口（模板：由 ONI 代码生成器生成）

using HarmonyLib;
using EventFramework.Distribution.Contracts;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using UnityEngine;

namespace MoreFood_KFC
{
    public class vs_presetMod : UserMod2
    {
        // 模组入口：初始化 PLib、注册本地化、构造食物 / 饮品表，最后 PatchAll 装补丁
        public override void OnLoad(Harmony harmony)
        {
            // PLib 初始化
            PUtil.InitLibrary(true);

            // 日志门面：读设置页的日志级别（重启生效）
            MFLog.Init();

            // 供多 Mod 配置扫描排除自身：本地 combat/ 另有加载路径，重复解析会产生「重复定义」
            try { CombatDb.OwnModTitle = this.mod != null ? this.mod.title : null; }
            catch (System.Exception e) { MFLog.Exc(LogCat.General, e, "[MoreFood] 读取自身 Mod 标题失败"); }

            // 注册模组设置页（PLib 会在「模组」界面给本 Mod 挂「选项」按钮）
            // 失败只降级为「没有设置页」，绝不拖垮 OnLoad 的其余初始化
            try
            {
                new POptions().RegisterOptions(this, typeof(ModOptions));
                MFLog.Info(LogCat.UI, "[MoreFood/Options] 设置页已注册（ModOptions）");
            }
            catch (System.Exception e)
            {
                MFLog.Exc(LogCat.General, e, "[MoreFood/Options] 设置页注册失败，已跳过");
            }

            // 性能分析探针：开关在读档完成时应用（ModPerfProbe.RefreshFromOptions），加载期不装任何钩子

            // 注册本地化：自动读取 translations/<lang>.po
            new PLocalization().Register(GetType().Assembly);

            // 注册默认字符串（STRINGS.ITEMS.FOOD.*）
            LocString.CreateLocStringKeys(typeof(ModStrings.ITEMS), "STRINGS.");

            // ==[ZONE:MODE_FOOD_ONLY]BEGIN==
            // 初始化所有 FoodInfo（构造函数自动注册到全局食物列表）
            ModFoods.Init();
            // ==[ZONE:MODE_FOOD_ONLY]END==

            // 奖励池不能在这里加载：OnLoad 阶段本 Mod 的 loaded_content 还是 0，
            // 一扫描「启用的 Mod」就会把自己排除掉（见 OnAllModsLoaded）

            // 事件框架补丁由框架用固定 harmonyId 统一装配；宿主只登记星图跳转实现这一个数据
            EventFrameworkApi.SetStarmapJump(StarmapLocator.Jump);

            // 第三方兼容补丁（目标缺失 / 装配失败 / 运行异常都会回退到默认行为）
            RonivansLegacyCompat.Install(harmony);

            // Space 装载钩子（合并后唯一入口是 vs_presetMod）：保留原 SpaceMod.OnLoad 的初始化与手动补丁
            SpaceMod.OnLoad(harmony);

            // 尽早挂"prefab 注册即打饮品 tag"的钩子
            //   （必须早于任何 prefab 注册；Db.Initialize 里 RegisterDefaults 末尾还会整表扫一遍兜底）
            Drinks.InstallAutoTagHook();

            // 配方卷轴：先把注册表填好，RecipeScrollItems 建 prefab 时才拿得到清单。
            RecipeScrollRegistry.RegisterAll();

            // 不要在此手动 PatchAll：基类 UserMod2.OnLoad 内部就是 PatchAll(this.assembly)，再调一次会把每个 [HarmonyPatch] 装两遍（前缀在一次调用里执行两次）
            base.OnLoad(harmony);
        }
    }
}

