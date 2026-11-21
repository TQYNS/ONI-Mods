// EventSignal.cs — 事件框架 · 【作者自定义信号总线】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventSignal
    {
        private static readonly Dictionary<string, List<System.Action<object>>> _map =
            new Dictionary<string, List<System.Action<object>>>(StringComparer.Ordinal);

        // 信号重入最大深度（防环）
        public const int MAX_RAISE_DEPTH = 8;
        // 当前重入深度
        private static int _raiseDepth;

        public static int Count { get { return _map.Count; } }

        // 统计某个信号名当前已注册的订阅者数量（0 = 没人订阅）
        public static int SubscriberCount(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            List<System.Action<object>> list;
            if (!_map.TryGetValue(name, out list) || list == null) return 0;
            return list.Count;
        }

        // 订阅 / 退订（与 Raise 严格配对；停止音频时必须退订）
        public static bool Subscribe(string name, System.Action<object> cb)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[MoreFood/Event][Signal] Subscribe：信号名为空，忽略");
                return false;
            }
            if (cb == null) return false;

            List<System.Action<object>> list;
            if (!_map.TryGetValue(name, out list))
            {
                list = new List<System.Action<object>>();
                _map[name] = list;
            }
            if (!list.Contains(cb)) list.Add(cb);
            // 有人订阅了 ⇒ 清掉"没人订阅"的提醒记录：将来再次变成无人订阅时还能提醒
            EventWarnOnce.Reset("signal:nosub:" + name);
            return true;
        }

        // 取消订阅：把回调从该信号名的订阅表摘掉（幂等）
        public static void Unsubscribe(string name, System.Action<object> cb)
        {
            if (string.IsNullOrEmpty(name) || cb == null) return;

            List<System.Action<object>> list;
            if (!_map.TryGetValue(name, out list) || list == null) return;

            list.Remove(cb);
            if (list.Count == 0) _map.Remove(name);
        }

        // 发信号
        public static void Raise(string name, object data = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[MoreFood/Event][Signal] Raise：信号名为空，忽略");
                return;
            }

            if (name.IndexOf('.') < 0 && name.IndexOf(':') < 0)
                EventWarnOnce.Warn("signal:ns:" + name, "[MoreFood/Event][Signal] 信号名 '" + name
                                 + "' 没带命名空间（建议 <modid>.sig.<名字>），有和原版事件名撞车的风险");

            List<System.Action<object>> list;
            if (!_map.TryGetValue(name, out list) || list == null || list.Count == 0)
            {
                EventWarnOnce.Warn("signal:nosub:" + name, "[MoreFood/Event][Signal] 信号 '" + name
                                   + "' 发出时没有任何订阅者 —— 检查是否有音频在等它，或名字拼错");
                return;
            }

            // 嵌套 Raise 深度守卫：A 的订阅者 Raise B、B 的订阅者又 Raise A ⇒ 会无限递归
            if (_raiseDepth >= MAX_RAISE_DEPTH)
            {
                Debug.LogWarning("[MoreFood/Event][Signal] 信号嵌套 Raise 超过 " + MAX_RAISE_DEPTH
                               + " 层（疑似成环）→ 本次 '" + name + "' 不再分发");
                return;
            }

            _raiseDepth++;
            try
            {
                var snapshot = list.ToArray();   // 回调里可能退订，先快照
                for (int i = 0; i < snapshot.Length; i++)
                {
                    try { snapshot[i](data); }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[MoreFood/Event][Signal] 信号 '" + name + "' 的订阅者抛异常（已忽略）: " + e);
                    }
                }
            }
            finally { _raiseDepth--; }
        }

        // 该信号名是否还有订阅者
        public static bool HasSubscriber(string name)
        {
            return SubscriberCount(name) > 0;
        }

        public static void Clear()
        {
            _map.Clear();
            EventWarnOnce.ResetPrefix("signal:");
        }

        // 信号名是否"作者自定义"（带 '.' 或 ':'）；否则按原版 GameHashes 名解析
        public static bool IsCustomName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf('.') >= 0 || name.IndexOf(':') >= 0;
        }
    }
}
