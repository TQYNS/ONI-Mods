using System;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 战斗调试按钮（正式装配 UI 完成前的测试入口）

    [HarmonyPatch(typeof(Clustercraft), "OnSpawn")]
    internal static class CombatTestButtonPatch
    {
        private static void Postfix(Clustercraft __instance)
        {
            try
            {
                __instance.gameObject.AddOrGet<CombatTestAmmoButton>();
                __instance.gameObject.AddOrGet<CombatTestEquipButton>();
                __instance.gameObject.AddOrGet<CombatTestResetButton>();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 战斗调试按钮挂载失败");
            }
        }
    }

    public class CombatTestAmmoButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "调试：补给弹药"; }
        }

        public override bool SidescreenEnabled()
        {
            return SandboxDebugButtons.Visible;
        }

        public override string SidescreenButtonTooltip
        {
            get { return "把 combat/ammo.yaml 里的每种弹药各补 20 个（1kg/个）进这艘火箭的货舱（测试用）。"; }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 30;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                Clustercraft craft = GetComponent<Clustercraft>();
                if (craft == null)
                {
                    return;
                }
                int ok = 0;
                int missing = 0;
                for (int i = 0; i < CombatDb.Ammo.Count; i++)
                {
                    AmmoDef a = CombatDb.Ammo[i];
                    if (string.IsNullOrEmpty(a.prefabId))
                    {
                        continue;
                    }
                    if (Assets.TryGetPrefab(new Tag(a.prefabId)) == null)
                    {
                        missing++;
                        continue;
                    }
                    for (int k = 0; k < 20; k++)
                    {
                        RocketInventory.Grant(craft, new Tag(a.prefabId), 1f, 300f);
                    }
                    ok++;
                }
                TradeUi.Notify("调试补给", "已补 " + ok + " 种弹药各 20 个"
                    + (missing > 0 ? "（" + missing + " 种 prefab 缺失，看日志）" : ""));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试补给弹药失败");
            }
        }
    }

    public class RocketAssemblyButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "装配"; }
        }

        public override string SidescreenButtonTooltip
        {
            get { return "打开火箭装配面板（改装需要本控制台）。"; }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 25;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                WorldContainer w = ClusterManager.Instance.GetWorld(ClusterUtil.GetMyWorldId(this));
                Clustercraft craft = w != null && w.gameObject != null
                    ? w.gameObject.GetComponent<Clustercraft>() : null;
                if (craft == null)
                {
                    TradeUi.Notify("装配", "找不到控制台所属的火箭");
                    return;
                }
                RocketPanelScreen screen = RocketPanelScreen.GetOrCreate();
                if (screen == null)
                {
                    return;
                }
                // ★ 问题2：用 Open()（overlay 显示）代替 Activate()（PushScreen 会顶替星图活动屏 → 背景跳转）
                screen.Open(craft);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 打开火箭装配面板失败");
            }
        }
    }

    public class CombatTestEquipButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "调试：发放模块"; }
        }

        public override bool SidescreenEnabled()
        {
            return SandboxDebugButtons.Visible;
        }

        public override string SidescreenButtonTooltip
        {
            get { return "把 15 种模块各 2 个放进这艘火箭的货舱，并尝试装上大型导弹发射器（测试用）。"; }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 31;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                Clustercraft craft = GetComponent<Clustercraft>();
                if (craft == null)
                {
                    return;
                }
                int granted = 0;
                for (int i = 0; i < CombatDb.Modules.Count; i++)
                {
                    ModuleDef def = CombatDb.Modules[i];
                    if (def == null || string.IsNullOrEmpty(def.prefabId))
                    {
                        continue;
                    }
                    if (Assets.TryGetPrefab(new Tag(def.prefabId)) == null)
                    {
                        continue;
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        RocketInventory.Grant(craft, new Tag(def.prefabId), 1f, 300f);
                    }
                    granted++;
                }
                string reason = null;
                RocketLoadout lo = RocketLoadout.Ensure(craft);
                string testId = "weapon_large";
                bool equipped = lo != null && lo.Equip(craft, testId, out reason);
                TradeUi.Notify("调试模块",
                    "已发放 " + granted + " 种模块各 2 个"
                    + (equipped ? "；已试装 " + testId : "；试装失败：" + (reason ?? "未知原因")));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试发放模块失败");
            }
        }
    }

    public class CombatTestResetButton : SpaceBeaconButton
    {
        public override string SidescreenButtonText
        {
            get { return "调试：重置状态"; }
        }

        public override bool SidescreenEnabled()
        {
            return SandboxDebugButtons.Visible;
        }

        public override string SidescreenButtonTooltip
        {
            get { return "立刻恢复火箭血量、补满燃料、重置跃迁冷却（测试用）。"; }
        }

        public override int ButtonSideScreenSortOrder()
        {
            return 32;
        }

        public override void OnSidescreenButtonPressed()
        {
            try
            {
                Clustercraft craft = GetComponent<Clustercraft>();
                if (craft == null)
                {
                    return;
                }
                RocketCombatState st = RocketCombatState.Ensure(craft);
                if (st != null)
                {
                    st.pendingDamage = 0f;
                    st.totalHpCache = RocketStats.MaxHp(craft);
                    st.armor = st.maxArmor;   // ★ 第十七轮-2：重置时同时补满装甲值
                    st.warpCooldownUntil = 0f;
                }
                int tanks = FillFuel(craft);
                CombatUi.RefreshBar();
                TradeUi.Notify("调试重置", "火箭状态已重置：血量恢复、燃料已补"
                    + (tanks > 0 ? "（" + tanks + " 个燃料罐）" : "（未找到燃料罐/引擎）")
                    + "、跃迁冷却已清零");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试重置状态失败");
            }
        }

        private static int FillFuel(Clustercraft craft)
        {
            if (craft == null || craft.ModuleInterface == null)
            {
                return 0;
            }
            RocketEngineCluster engine = craft.ModuleInterface.GetEngine();
            if (engine == null)
            {
                return 0;
            }
            Tag fuelTag = engine.fuelTag;
            GameObject prefab = Assets.TryGetPrefab(fuelTag);
            if (prefab == null)
            {
                Debug.LogWarning("[Space] 调试重置：找不到燃料 prefab " + fuelTag.Name);
                return 0;
            }
            int filled = 0;
            System.Collections.Generic.IList<Ref<RocketModuleCluster>> modules =
                craft.ModuleInterface.ClusterModules;
            if (modules == null)
            {
                return 0;
            }
            for (int i = 0; i < modules.Count; i++)
            {
                RocketModuleCluster module = modules[i].Get();
                if (module == null)
                {
                    continue;
                }
                IFuelTank tank = module.GetComponent<IFuelTank>();
                Storage storage = module.GetComponent<Storage>();
                if (tank == null || storage == null)
                {
                    continue;
                }
                float room = storage.Capacity() - storage.MassStored();
                if (room <= 0.01f)
                {
                    continue;
                }
                GameObject go = Util.KInstantiate(prefab, null, null);
                if (go == null)
                {
                    continue;
                }
                PrimaryElement pe = go.GetComponent<PrimaryElement>();
                if (pe != null)
                {
                    pe.Mass = room;
                }
                go.SetActive(true);
                storage.Store(go, true, false, true, false);
                filled++;
            }
            return filled;
        }
    }
}
