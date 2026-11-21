// EventSystemPatches.cs — 事件框架 · 【所有 Harmony 补丁集中在这里】

using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using EventFramework.Distribution.Contracts;
using EventFramework.Distribution.Patches;

namespace EventFramework
{
    public static class EventSystemPatches
    {
        // 旧入口：补丁统一由分发层用固定 harmonyId 装配，这里只接受该 id，避免双份 owner
        public static void Install(Harmony harmony)
        {
            try
            {
                if (harmony == null) return;
                if (harmony.Id != EventFrameworkApi.FixedHarmonyId)
                {
                    Debug.LogWarning("[MoreFood/Event] 忽略旧入口装配请求（owner=" + harmony.Id
                        + "）：补丁由框架用 " + EventFrameworkApi.FixedHarmonyId + " 统一装配");
                    return;
                }
                LifecyclePatches.Install(harmony);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 旧入口装配异常: " + e);
            }
        }

        // ① Db.Initialize 之后 → 注册处理器
        public static void Postfix_Db_Initialize()
        {
            try { EventBootstrap.RegisterHandlers(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] Db.Initialize Postfix 异常: " + e); }
        }

        // ②a Game.OnPrefabInit 之后 → 兜底尝试挂存档载体
        public static void Postfix_Game_OnPrefabInit(Game __instance)
        {
            // ★ 进档（新档 / 读档）先清一次音频残留（与 Game.StopBE 的退档清理对称）
            try { EventBootstrap.SessionReset("Game.OnPrefabInit"); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] Game.OnPrefabInit 会话清理异常: " + e); }

            try { EventSaveBridge.Install(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] Game.OnPrefabInit Postfix 异常: " + e); }
        }

        // ②c SaveGame.OnPrefabInit 之后 → ★ 挂存档载体（本次修正的关键）
        public static void Postfix_SaveGame_OnPrefabInit(SaveGame __instance)
        {
            try { EventSaveBridge.InstallOnSaveGame(__instance); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] SaveGame.OnPrefabInit Postfix 异常: " + e); }
        }

        // ②d SaveLoadRoot.LoadInternal 之后 → ★ 精确的"读档完成"时刻
        public static void Postfix_SaveLoadRoot_LoadInternal(GameObject gameObject)
        {
            try
            {
                if (gameObject == null) return;
                if (gameObject.GetComponent<SaveGame>() == null) return;
                EventSaveBridge.OnSaveGameDeserialized();
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] SaveLoadRoot.LoadInternal Postfix 异常: " + e); }
        }

        // ②b Game.OnSpawn 之后 → 启动运行时
        public static void Postfix_Game_OnSpawn()
        {
            try { EventBootstrap.StartRuntime(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] Game.OnSpawn Postfix 异常: " + e); }
        }

        // ③ ManagementMenu.OnPrefabInit 之后 → 往管理菜单挂 3 个 tab
        public static void Postfix_ManagementMenu_OnPrefabInit(ManagementMenu __instance)
        {
            try { EventMenuTabs.Mount(__instance); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] ManagementMenu.OnPrefabInit Postfix 异常: " + e); }
        }

        // Game.StopBE（离开存档 / 换档 / 退主菜单）→ 停声 + 清缓存
        public static void Prefix_Game_StopBE()
        {
            try { EventBootstrap.SessionReset("Game.StopBE"); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] Game.StopBE Prefix 异常: " + e); }
        }

        // AudioMixer.SetUserVolume（用户拉音量滑块）→ 实时刷新自建 channel
        public static void Postfix_AudioMixer_SetUserVolume()
        {
            try { EventAudioVolume.UpdateAllLive(); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event] SetUserVolume Postfix 异常: " + e); }
        }
    }
}
