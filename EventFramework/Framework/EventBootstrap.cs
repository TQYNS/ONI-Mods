// EventBootstrap.cs — 事件框架 · 【启动编排】

using System;
using UnityEngine;

namespace EventFramework
{
    public static class EventBootstrap
    {
        // handler 是否已注册
        private static bool _handlersDone;
        // ⚠ _runtimeDone 故意【不复用】：StartRuntime 每次进游戏（新存档 / 读档）都要跑 ——

        // 阶段 ①：处理器注册
        public static void RegisterHandlers()
        {
            if (_handlersDone) return;
            _handlersDone = true;

            try
            {
                Debug.Log("[MoreFood/Event] ── 阶段① 注册处理器（Db.Initialize 之后）");
                EventHandlers.RegisterAll();
                EventNotifyStyles.EnsureBuiltins();
                EventCustomNotifyRegistry.EnsureBuiltins();   // ★ 修复：状态行外观（含灾星倒计时）也提前注册
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 阶段① 异常: " + e);
            }
        }

        // 阶段 ②：运行时
        public static void StartRuntime()
        {
            try
            {
                Debug.Log("[MoreFood/Event] ── 阶段② 启动运行时（Game.OnSpawn 之后）");
                LoadOrderResolver.Resolve();          // 三档分类 + 拓扑（事件与奖励池共用）
                // 奖励池由内容 Mod 自管；框架只读事件 YAML 的内联奖励（C09）
                EventSystem.Init();          // 每次进游戏都重跑（内部已做重入清理）
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 阶段② 异常: " + e);
            }
        }

        // 会话切换清理：退档 / 进档各清一次；叙事者闹钟必须一起停，否则退档后仍有僵尸闹钟
        public static void SessionReset(string reason)
        {
            try
            {
                EventNarratorDirector.Suspend(reason);
                EventAudioTrigger.Shutdown();
                EventSignal.Clear();
                EventMediaPlayer.Clear();
                Debug.Log("[MoreFood/Event] 音频会话清理完成（" + (reason ?? "-") + "）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] SessionReset 异常: " + e);
            }
        }

        // 换局 / 强制重置（调试用）
        public static void ResetAll(string reason = null)
        {
            try
            {
                Debug.Log("[MoreFood/Event] ResetAll reason=" + (reason ?? "-"));

                EventNarratorDirector.Deactivate();
                EventNotifier.Clear();
                EventPanel.CloseCurrent("ResetAll");
                EventPanel.ClearQueue();      // ★v3.2
                EventSystem.Reset();

                _handlersDone = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] ResetAll 异常: " + e);
            }
        }

        // 调试入口：一键把开局平均间隔改成 N 秒（验证用）
        public static void SetTestInterval(float seconds)
        {
            try
            {
                var n = EventNarratorDirector.CurrentOrDefault;
                if (n != null) n.I0 = seconds;
                EventNarratorDirector.RestartRunning();
                Debug.Log("[MoreFood/Event] 开局平均间隔已改为 " + seconds + "s，调度已重排");
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] SetTestInterval 异常: " + e); }
        }
    }
}
