// FrameworkSaveManager.cs — 事件框架 · 【存档挂载 + 读写】；框架自管，宿主零接线
//
// 与拆分前的宿主 EventSaveManager 一一对应，区别只有两点：
//   1) 载体换成框架自带的 EventFrameworkSaveData，由框架自己挂到 SaveGame 对象；
//   2) 不再有「宿主快照补写」回调（buff 快照归宿主自己的载体）。

using System;
using UnityEngine;

namespace EventFramework
{
    internal static class FrameworkSaveManager
    {
        // 静态构造：把自己挂上存档桥（EventSystem / 定位器只认桥，不认具体载体）
        static FrameworkSaveManager()
        {
            try
            {
                EventSaveBridge.Bind(
                    delegate { return (IEventSaveData)_data; },
                    InstallOnSaveGame, Install, Restore, Capture, ClearAll, OnSaveGameDeserialized);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Save] 框架存档桥自绑失败（事件状态将不落盘）: " + e);
            }
        }

        // 显式入口：确保静态构造已跑（Activate 时调一次，早于任何存档读写）
        internal static bool Ensure() { return Ready; }

        // 存档载体（null = 未挂载）
        private static EventFrameworkSaveData _data;
        // 载体的宿主对象
        private static GameObject _host;
        // 是否已反序列化
        private static bool _deserialized;
        // 是否已尝试挂载（失败不重试）
        private static bool _installTried;

        // 是否已挂上存档载体（未挂上时框架仍可运行，只是不落盘）
        internal static bool Ready { get { return _data != null; } }

        internal static IEventSaveData Data { get { return _data; } }

        // 挂载
        internal static void InstallOnSaveGame(SaveGame sg)
        {
            try
            {
                if (sg == null) { Debug.LogWarning("[MoreFood/Event][Save] SaveGame 为 null，跳过挂载"); return; }

                var go = sg.gameObject;
                if (go == null) { Debug.LogWarning("[MoreFood/Event][Save] SaveGame.gameObject 为 null，跳过挂载"); return; }

                // 换了宿主（读档 → 回主菜单 → 再开新档）⇒ 状态整体重置
                if (_host != null && _host != go)
                {
                    _data = null;
                    _deserialized = false;
                    Debug.Log("[MoreFood/Event][Save] 宿主已更换，存档载体状态重置");
                }
                _host = go;

                if (_data == null) _data = go.GetComponent<EventFrameworkSaveData>();
                if (_data == null) _data = go.AddComponent<EventFrameworkSaveData>();

                if (_data == null)
                {
                    Debug.LogWarning("[MoreFood/Event][Save] EventFrameworkSaveData 挂载失败");
                    return;
                }

                Debug.Log("[MoreFood/Event][Save] 框架存档载体已挂上 SaveGame 对象：宿主=" + go.name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Save] InstallOnSaveGame 异常（框架继续运行）: " + e);
            }
        }

        // 兜底：拿不到 SaveGame 引用时，自己去找 SaveGame.Instance
        internal static void Install()
        {
            if (_data != null) return;
            if (_installTried) return;
            _installTried = true;

            try
            {
                var sg = SaveGame.Instance;
                if (sg != null) InstallOnSaveGame(sg);
                else Debug.Log("[MoreFood/Event][Save] SaveGame.Instance 尚为 null（等它的 OnPrefabInit）");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Save] Install 异常（框架继续运行）: " + e);
            }
        }

        internal static void Uninstall()
        {
            _data = null;
            _host = null;
            _deserialized = false;
            _installTried = false;
        }

        // 读
        internal static void OnSaveGameDeserialized()
        {
            _deserialized = true;
            Debug.Log("[MoreFood/Event][Save] SaveGame 反序列化完成 → 标记 _deserialized=true"
                      + "（真正的读档由 EventSystem.Init 里的 Restore 执行，此时 yaml 已加载）");
        }

        // 从存档载体恢复到运行态。
        internal static void Restore()
        {
            if (_data == null) return;

            if (!_deserialized)
            {
                Debug.Log("[MoreFood/Event][Save] 反序列化尚未发生，本次不读档（等 LoadInternal）");
                return;
            }

            try
            {
                Debug.Log("[MoreFood/Event][Save] 读档内容：" + _data.Describe());
                EventSystem.ReadFromSave(_data);
                EventLocator.LoadFromSave();   // 定位器队列读档重建（版本脏标记）
            }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Save] Restore 异常: " + e); }
        }

        // 运行态 → 载体（宿主快照补写已拆走，这里只写框架自己的状态）
        internal static void Capture()
        {
            if (_data == null) return;
            try { EventSystem.WriteToSave(_data); }
            catch (Exception e) { Debug.LogWarning("[MoreFood/Event][Save] Capture 异常: " + e); }
        }

        // 换局/清档：清空载体内容
        internal static void ClearAll()
        {
            try
            {
                if (_data != null) _data.ResetAll();
            }
            catch (Exception e)
            {
                // 换局清理失败会导致上一局状态残留，必须留痕
                Debug.LogWarning("[MoreFood/Event][Save] 换局清理异常（旧状态可能残留）: " + e);
            }
            EventLocator.Clear();   // 定位器随换局清空
        }
    }
}
