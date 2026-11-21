// EventSaveBridge.cs — 事件框架 · 【存档桥】；框架内部自绑（FrameworkSaveManager），宿主不再注入

using System;
using UnityEngine;

namespace EventFramework
{
    // 框架侧存档入口：由 FrameworkSaveManager 在静态构造里自绑（宿主已无注入点）
    public static class EventSaveBridge
    {
        private static System.Func<IEventSaveData> _getData;
        private static System.Action<SaveGame> _installOnSaveGame;
        private static System.Action _install;
        private static System.Action _restore;
        private static System.Action _capture;
        private static System.Action _clearAll;
        private static System.Action _onDeserialized;

        // 宿主是否已绑定
        public static bool Ready { get; private set; }

        // 存档载体（null = 未挂载 / 未绑定）
        public static IEventSaveData Data { get { return _getData != null ? _getData() : null; } }

        internal static void Bind(System.Func<IEventSaveData> getData, System.Action<SaveGame> installOnSaveGame,
                                System.Action install, System.Action restore, System.Action capture,
                                System.Action clearAll, System.Action onDeserialized)
        {
            _getData = getData;
            _installOnSaveGame = installOnSaveGame;
            _install = install;
            _restore = restore;
            _capture = capture;
            _clearAll = clearAll;
            _onDeserialized = onDeserialized;
            Ready = true;
        }

        public static void InstallOnSaveGame(SaveGame sg) { if (_installOnSaveGame != null) _installOnSaveGame(sg); }
        public static void Install() { if (_install != null) _install(); }
        public static void Restore() { if (_restore != null) _restore(); }
        public static void Capture() { if (_capture != null) _capture(); }
        public static void ClearAll() { if (_clearAll != null) _clearAll(); }
        public static void OnSaveGameDeserialized() { if (_onDeserialized != null) _onDeserialized(); }
    }
}