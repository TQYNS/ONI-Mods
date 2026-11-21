// EventHandlers.cs — 事件框架 · 【代码端处理器集中注册】

using UnityEngine;
using EventFramework.Content;

namespace EventFramework
{
    public static class EventHandlers
    {
        // 是否已注册
        private static bool _registered;

// 注册全部事件 handler（幂等）
        public static void RegisterAll()
        {
            if (_registered)
            {
                Debug.Log("[MoreFood/Event] 处理器已注册过，跳过（幂等）");
                return;
            }
            _registered = true;

            int before = EventHandlerRegistry.Count;

            // 事件处理器清单（加新事件在这里加一行）
            SafeRegister(Events_YamlFunctions.Register);

            // 将来：

            Debug.Log("[MoreFood/Event] 处理器注册完成：本次新增 "
                      + (EventHandlerRegistry.Count - before) + " 个，累计 "
                      + EventHandlerRegistry.Count + " 个");
        }

        private static void SafeRegister(System.Action register)
        {
            if (register == null) return;
            try { register(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MoreFood/Event] 处理器注册失败（已跳过该事件）: " + e);
            }
        }

        public static void Reset() { _registered = false; }
    }
}
