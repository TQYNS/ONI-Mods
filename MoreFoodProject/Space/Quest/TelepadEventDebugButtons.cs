using System;
using System.Collections.Generic;
using EventFramework.Distribution.Contracts;
using HarmonyLib;
using UnityEngine;

namespace MoreFood_KFC
{
    // 打印仓上的事件调试按钮：跟随沙盒；按下 = 让叙事者真抽，走完整事件流程
    internal abstract class TelepadEventDebugButton : KMonoBehaviour, ISidescreenButtonControl
    {
        public abstract string SidescreenButtonText { get; }
        public abstract string SidescreenButtonTooltip { get; }
        public virtual string SidescreenTitle { get { return "事件调试"; } }

        public bool SidescreenEnabled() { return SandboxDebugButtons.Visible; }
        public bool SidescreenButtonInteractable() { return SandboxDebugButtons.Visible; }
        public int HorizontalGroupID() { return -1; }
        public int ButtonSideScreenSortOrder() { return 0; }
        public void SetButtonTextOverride(ButtonMenuTextOverride textOverride) { }
        public abstract void OnSidescreenButtonPressed();
    }

    internal sealed class TelepadEventRandomButton : TelepadEventDebugButton
    {
        public override string SidescreenButtonText { get { return "调试：抽取事件（随机）"; } }

        public override string SidescreenButtonTooltip
        {
            get { return "让当前叙事者按正常规则真抽一次，走完整流程（通知 / 面板 / 存档）。"; }
        }

        public override void OnSidescreenButtonPressed() { EventDebugDraw.RollOne(); }
    }

    internal sealed class TelepadEventAllButton : TelepadEventDebugButton
    {
        public override string SidescreenButtonText { get { return "调试：抽取事件（全部）"; } }

        public override string SidescreenButtonTooltip
        {
            get { return "把常规池里全部 scheduled 事件逐个真抽一次，每个都走完整流程。"; }
        }

        public override void OnSidescreenButtonPressed() { EventDebugDraw.RollAll(); }
    }

    internal sealed class TelepadMonsterRefreshButton : TelepadEventDebugButton
    {
        public override string SidescreenTitle { get { return "怪物调试"; } }
        public override string SidescreenButtonText { get { return "调试：刷新怪物"; } }

        public override string SidescreenButtonTooltip
        {
            get { return "按原有刷怪规则（区域 / 边缘格 / 休眠与离场闹钟）把怪物表里每一种各刷一只，逐只错开；已在场的不移除。"; }
        }

        public override void OnSidescreenButtonPressed() { MonsterDebugTools.RefreshAll(); }
    }

    internal sealed class TelepadMonsterKillAllButton : TelepadEventDebugButton
    {
        public override string SidescreenTitle { get { return "怪物调试"; } }
        public override string SidescreenButtonText { get { return "调试：全部击杀"; } }

        public override string SidescreenButtonTooltip
        {
            get { return "把在场全部星图怪物按玩家击杀路线逐个结算：掉落定死并生成残骸带、任务按击杀结算（无归属火箭）。"; }
        }

        public override void OnSidescreenButtonPressed() { MonsterDebugTools.KillAll(); }
    }

    internal static class EventDebugDraw
    {
        // 调试按钮只走门面：按 id 触发，不再自建 EventDef / 不再直接问叙事者
        // （取舍：门面按 id 触发不做"当前是否可抽"的预筛，前置条件不满足时日志会写"被拒"）
        private const string DebugPool = "normal";

        // 随机：从该池的事件清单里随机取一个
        internal static void RollOne()
        {
            try
            {
                string[] ids = EventFrameworkApi.EventIds(DebugPool);
                if (ids == null || ids.Length == 0)
                {
                    MFLog.Warn(LogCat.Event,"[Space] 调试抽取（随机）：池 " + DebugPool + " 还没有事件清单（yaml 未加载完？）");
                    return;
                }
                string id = ids[UnityEngine.Random.Range(0, ids.Length)];
                bool ok = EventFrameworkApi.Trigger(id, "debug");
                MFLog.Info(LogCat.Event,"[Space] 调试抽取（随机）→ " + id + " 触发=" + (ok ? "成功" : "被拒"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 调试抽取（随机）异常");
            }
        }

        private static readonly Queue<string> _queue = new Queue<string>();
        private static bool _running;

        // 全部：逐帧错开触发（同帧会被单帧去重拦掉）
        internal static void RollAll()
        {
            try
            {
                _queue.Clear();
                string[] ids = EventFrameworkApi.EventIds(DebugPool);
                for (int i = 0; i < ids.Length; i++)
                {
                    if (!string.IsNullOrEmpty(ids[i])) _queue.Enqueue(ids[i]);
                }
                MFLog.Info(LogCat.Event,"[Space] 调试抽取（全部）：待抽 " + _queue.Count + " 个");
                if (!_running)
                {
                    _running = true;
                    Step();
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 调试抽取（全部）异常");
            }
        }

        private static void Step()
        {
            if (_queue.Count == 0)
            {
                _running = false;
                MFLog.Info(LogCat.Event,"[Space] 调试抽取（全部）完成");
                return;
            }
            string id = _queue.Dequeue();
            try
            {
                bool ok = EventFrameworkApi.Trigger(id, "debug_all");
                MFLog.Info(LogCat.Event,"[Space] 调试抽取（全部）→ " + id + " 触发=" + (ok ? "成功" : "被拒"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 调试抽取异常 " + (id ?? "?"));
            }
            try
            {
                GameScheduler.Instance.Schedule("EventDebugNext", 0.1f,
                    new Action<object>(delegate(object data) { Step(); }), null, null);
            }
            catch (Exception e)
            {
                _running = false;
                MFLog.Exc(LogCat.Event, e, "[Space] 调试抽取（全部）排下一拍失败");
            }
        }
    }

    internal static class MonsterDebugTools
    {
        private static readonly Queue<CombatMonsterDef> _queue = new Queue<CombatMonsterDef>();
        private static int _token;
        private static int _spawned;
        private static int _total;

        // 刷新：怪物表里每种刷一只，逐帧错开（同帧刷完会全挤在一起）
        internal static void RefreshAll()
        {
            try
            {
                _queue.Clear();
                for (int i = 0; i < CombatDb.Monsters.Count; i++)
                {
                    CombatMonsterDef def = CombatDb.Monsters[i];
                    if (def != null) _queue.Enqueue(def);
                }
                _total = _queue.Count;
                _spawned = 0;
                CombatRuntime.DebugResetSpawnReserved();
                if (StarMapVisuals.Zones == null || !StarMapVisuals.Zones.valid)
                {
                    SpaceTestRuntime.RebuildZones();   // 分区未算（刚读档）→ 先补算，否则一只都刷不出
                }
                MFLog.Info(LogCat.Combat,"[Space] 调试刷新怪物：待刷 " + _total + " 种");
                if (_total == 0)
                {
                    TradeUi.Notify("调试刷新怪物", "怪物表为空（combat/monsters 未加载？）");
                    return;
                }
                Step(++_token);
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试刷新怪物异常");
            }
        }

        private static void Step(int token)
        {
            if (token != _token)
            {
                return;
            }
            if (_queue.Count == 0)
            {
                MFLog.Info(LogCat.Combat,"[Space] 调试刷新怪物完成：" + _spawned + "/" + _total + " 只");
                TradeUi.Notify("调试刷新怪物", "已刷出 " + _spawned + "/" + _total + " 只（没刷出的看日志：边缘格被占满）");
                return;
            }
            CombatMonsterDef def = _queue.Dequeue();
            try
            {
                if (CombatRuntime.DebugSpawnOne(def) != null)
                {
                    _spawned++;
                }
                else
                {
                    MFLog.Warn(LogCat.Combat,"[Space] 调试刷新怪物：刷不出 " + def.id);
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试刷新怪物异常 " + (def != null ? def.id : "?"));
            }
            try
            {
                GameScheduler.Instance.Schedule("MonsterDebugNext", 0.1f,
                    new Action<object>(delegate(object data) { Step(token); }), null, null);
            }
            catch (Exception e)
            {
                _token++;
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试刷新怪物排下一拍失败");
            }
        }

        // 击杀：走玩家击杀路线（掉落定死 + 生成残骸带 + 广播任务结算）
        internal static void KillAll()
        {
            try
            {
                int n = CombatRuntime.DebugKillAll();
                MFLog.Info(LogCat.Combat,"[Space] 调试全部击杀：结算 " + n + " 只");
                TradeUi.Notify("调试全部击杀", n > 0 ? ("已击杀 " + n + " 只，战利品留在残骸带") : "场上没有怪物");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Combat, e, "[Space] 调试全部击杀异常");
            }
        }
    }

    [HarmonyPatch(typeof(Telepad), "OnSpawn")]
    internal static class TelepadEventDebugMountPatch
    {
        private static void Postfix(Telepad __instance)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.GetComponent<TelepadEventRandomButton>() == null)
                    __instance.gameObject.AddComponent<TelepadEventRandomButton>();
                if (__instance.GetComponent<TelepadEventAllButton>() == null)
                    __instance.gameObject.AddComponent<TelepadEventAllButton>();
                if (__instance.GetComponent<TelepadMonsterRefreshButton>() == null)
                    __instance.gameObject.AddComponent<TelepadMonsterRefreshButton>();
                if (__instance.GetComponent<TelepadMonsterKillAllButton>() == null)
                    __instance.gameObject.AddComponent<TelepadMonsterKillAllButton>();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Event, e, "[Space] 打印仓调试按钮挂载失败");
            }
        }
    }
}