// EventNarratorSelection.cs — 事件框架 · 【开局选叙事者：屏幕接线】；详见《事件.md》§23.1

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EventFramework
{
    // 只做「哪块屏、什么时候开面板、选完怎么推进流程」；界面本体在 EventNarratorPanel
    public static class EventNarratorSelection
    {
        // 内置叙事者 id（注册表为空时的兜底）
        public static readonly string[] IDS =
        {
            EventNarratorRegistry.CASSANDRA,
            EventNarratorRegistry.RANDY,
            EventNarratorRegistry.PHOEBE,
        };

        // 三位叙事者的显示名（与 IDS 对应）
        public static readonly string[] NAMES = { "卡桑德拉", "兰迪", "菲比" };

        // 面板标题
        public const string NARRATOR_TITLE = "选择叙事者";
        // 面板左下说明
        public const string NARRATOR_DESC = "叙事者决定事件出现的节奏与风格。本页仅预览参数，选择结果在进入殖民地后生效。";

        // 前端选中的叙事者（进新殖民地时消费）
        public static string Pending { get; private set; }

        // 从存档读到的叙事者 id（待应用）
        private static string _fromSave;
        // 已打过的警告（只打一次）
        private static bool _warned;
        // 已捕获的分类屏实例
        private static ClusterCategorySelectionScreen _screen;

        public static void SetFromSave(string id) { _fromSave = id; }

        public static void ResetForSession() { _fromSave = null; _warned = false; }

        // 补丁失败时给一个可用身份，保证原版流程仍能把这一局开起来
        internal static void SetPendingFallback()
        {
            string id = EventNarratorDirector.PickNarratorId();
            Pending = string.IsNullOrEmpty(id) ? EventNarratorRegistry.CASSANDRA : id;
        }

        public static string ResolveForSession()
        {
            if (!string.IsNullOrEmpty(_fromSave)) return _fromSave;
            return Pending;
        }

        public static bool IsPendingFromFrontEnd { get { return !string.IsNullOrEmpty(Pending); } }

        public static void TakePending() { Pending = null; }

        // 新一轮开局：清掉上一局残留的选择与面板参数（否则第一次进分类屏就会自动弹出叙事者页）
        internal static void ResetPending()
        {
            Pending = null;
            EventNarratorRegistry.ResetParameters();
        }

        // 记下这次分类屏实例（面板挂它的子节点，随屏销毁）
        internal static void Capture(ClusterCategorySelectionScreen screen)
        {
            if (screen == null) return;
            if (ReferenceEquals(_screen, screen)) return;
            _screen = screen;
            Debug.Log("[MoreFood/Narr] 已捕获分类选择屏");
        }

        // 打开叙事者面板（盖在分类屏上，不改原屏任何子节点）
        internal static void EnterNarratorMode(ClusterCategorySelectionScreen screen)
        {
            if (screen == null) return;
            Capture(screen);
            EventNarratorPanel.Show(screen.transform);
        }

        // 关闭叙事者面板
        internal static void ExitNarratorMode(ClusterCategorySelectionScreen screen)
        {
            EventNarratorPanel.Hide();
        }

        // 从下一步「返回」时：分类已定、叙事者也选过 ⇒ 直接把叙事者页摊回来
        internal static void ReopenIfChosen(ClusterCategorySelectionScreen screen)
        {
            if (screen == null) return;
            if (string.IsNullOrEmpty(Pending)) return;
            EnterNarratorMode(screen);
        }

        // 「继续」：记住选择并推进新游戏流程
        internal static void CommitChoice(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                Pending = id;
                Debug.Log("[MoreFood/Narr] 已选定叙事者 = " + id);
            }
            AdvanceFlow("继续");
        }

        // 把玩家选的星图分类写进原版设置（写失败只告警）
        internal static void SetCategory(ProcGen.ClusterLayout.ClusterCategory category)
        {
            try { DestinationSelectPanel.ChosenClusterCategorySetting = (int)category; }
            catch (Exception e) { Warn("记录分类失败: " + e); }
        }

        // 推进新游戏流程的下一步
        internal static void AdvanceFlow(string reason)
        {
            try
            {
                var flow = FindFlow();
                if (flow == null) { Warn("找不到 NewGameFlow（" + reason + "）"); return; }

                var mi = typeof(NewGameFlow).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null) { Warn("NewGameFlow.Next 找不到"); return; }

                mi.Invoke(flow, null);
                Debug.Log("[MoreFood/Narr] 流程推进：" + reason);
            }
            catch (Exception e) { Warn("流程前进失败（" + reason + "）: " + e); }
        }

        internal static NewGameFlow FindFlow()
        {
            try { return UnityEngine.Object.FindObjectOfType<NewGameFlow>(); }
            catch (Exception e) { Warn("查找 NewGameFlow 失败: " + e); return null; }
        }

        private static void Warn(string msg)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("[MoreFood/Narr] " + msg);
        }
    }

    // 点分类卡：先记分类，再开叙事者面板（原本版方法不再执行）
    public static class EventNarratorSelection_Patch
    {
        public static bool Prefix(ClusterCategorySelectionScreen __instance,
                                  ProcGen.ClusterLayout.ClusterCategory __0)
        {
            try
            {
                EventNarratorSelection.SetCategory(__0);
                EventNarratorSelection.EnterNarratorMode(__instance);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Narr] 叙事者页补丁失败，回退原版: " + e);
                EventWarnOnce.Warn("narr/click_fallback",
                    "[MoreFood/Narr] 叙事者选择页已降级为原版分类屏，本局使用默认叙事者");
                EventNarratorSelection.SetPendingFallback();
                EventNarratorPanel.Hide();
                return true;
            }
        }
    }

    // 屏生成后：捕获实例；已选过叙事者（从下一步「返回」回来）则直接回到叙事者页
    public static class EventNarratorSelection_SpawnPatch
    {
        public static void Postfix(ClusterCategorySelectionScreen __instance)
        {
            try
            {
                if (__instance == null) return;
                // 模板实例不过 OnSpawn 也不该挂面板：只认真实的活动实例
                if (!__instance.gameObject.activeInHierarchy) return;

                EventNarratorSelection.Capture(__instance);
                EventNarratorSelection.ReopenIfChosen(__instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Narr] 分类屏补丁异常: " + e);
            }
        }
    }

    // 新开局入口：清掉上一局遗留的叙事者选择
    public static class EventNarratorSelection_BeginFlowPatch
    {
        public static void Postfix()
        {
            try { EventNarratorSelection.ResetPending(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Narr] 开局清理异常: " + e); }
        }
    }
}
