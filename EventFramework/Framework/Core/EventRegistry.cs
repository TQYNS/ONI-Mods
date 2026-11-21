// EventRegistry.cs — 事件框架 · 【模板注册表】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventRegistry
    {
        private static readonly Dictionary<string, EventDef> _byFullId =
            new Dictionary<string, EventDef>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> _shortToFullId =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly List<string> _loadErrors = new List<string>();

        // 内容版本号：注册表发生增删改时递增，供 UI 缓存判定
        private static int _version;
        public static int Version { get { return _version; } }

        public static int Count { get { return _byFullId.Count; } }
        public static IEnumerable<EventDef> All { get { return _byFullId.Values; } }
        public static IEnumerable<string> AllFullIds { get { return _byFullId.Keys; } }
        public static List<string> LoadErrors { get { return _loadErrors; } }

        // 全部事件的【短 id】（命名空间提醒 / 日志用；不含 mod 前缀）
        public static IEnumerable<string> AllIds
        {
            get
            {
                var ids = new List<string>(_byFullId.Count);
                foreach (var d in _byFullId.Values)
                    if (d != null && !string.IsNullOrEmpty(d.id)) ids.Add(d.id);
                return ids;
            }
        }

        public static void Clear()
        {
            _byFullId.Clear();
            _shortToFullId.Clear();
            _loadErrors.Clear();
            _version++;
        }

        // 登记
        public static string MakeFullId(string modTitle, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return string.IsNullOrEmpty(modTitle) ? id : modTitle + ".event." + id;
        }

        // 登记一个模板。id 重复时保留先到的并告警。
        public static bool Add(EventDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
            {
                Debug.LogWarning("[MoreFood/Event] EventRegistry.Add：id 为空，忽略（来自 "
                                 + (def != null ? def.sourceFile : "<null>") + "）");
                return false;
            }

            if (string.IsNullOrEmpty(def.fullId))
                def.fullId = MakeFullId(def.sourceMod, def.id);

            if (_byFullId.ContainsKey(def.fullId))
            {
                string msg = "事件 id 重复: " + def.fullId + "（先=" + _byFullId[def.fullId].sourceFile
                             + "，后=" + def.sourceFile + "），保留先到的";
                Debug.LogWarning("[MoreFood/Event] " + msg);
                _loadErrors.Add(msg);
                return false;
            }

            _byFullId[def.fullId] = def;
            RegisterShort(def);
            _version++;
            return true;
        }

 // 覆盖补丁：身份保持（fullId / sourceMod / sourceFile 用目标的），
        public static bool Override(EventDef patch)
        {
            if (patch == null || string.IsNullOrEmpty(patch.mode_target))
            {
                Debug.LogWarning("[MoreFood/Event] 补丁缺少 mode_target: "
                               + (patch != null ? patch.sourceFile : "<null>"));
                return false;
            }

            EventDef target;
            if (!_byFullId.TryGetValue(patch.mode_target, out target))
            {
                Debug.LogWarning("[MoreFood/Event] 补丁目标不存在: " + patch.mode_target
                               + "（来自 " + patch.sourceFile + "）");
                return false;
            }

            patch.lastPatchedBy = patch.sourceMod;
            patch.lastPatchedFrom = patch.sourceFile;

            patch.fullId = target.fullId;
            patch.sourceMod = target.sourceMod;
            patch.sourceFile = target.sourceFile;

            _byFullId[target.fullId] = patch;
            RegisterShort(patch);
            _version++;

            Debug.Log("[MoreFood/Event] 已覆盖事件 '" + target.fullId + "'（补丁来自 "
                      + patch.lastPatchedBy + "：" + patch.lastPatchedFrom + "）");
            return true;
        }

        // keyOrFullId：keyorfull id
        public static EventDef Get(string keyOrFullId)
        {
            if (string.IsNullOrEmpty(keyOrFullId)) return null;

            EventDef d;
            if (_byFullId.TryGetValue(keyOrFullId, out d)) return d;

            string fullId;
            if (_shortToFullId.TryGetValue(keyOrFullId, out fullId)
                && _byFullId.TryGetValue(fullId, out d)) return d;

            return null;
        }

        // keyOrFullId：标识 id
        public static bool Has(string keyOrFullId)
        {
            return Get(keyOrFullId) != null;
        }

        // 统计（启动日志用）
        public static string Describe()
        {
            int scheduled = 0, chained = 0;
            foreach (var d in _byFullId.Values)
            {
                if (d.trigger_mode == EventTriggerMode.scheduled) scheduled++;
                if (d.next_event != null && d.next_event.Count > 0) chained++;
            }
            return "共 " + _byFullId.Count + " 个事件（scheduled=" + scheduled
                   + " 带链式=" + chained + "）";
        }

        private static void RegisterShort(EventDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;

            string existing;
            if (_shortToFullId.TryGetValue(def.id, out existing))
            {
                if (!string.Equals(existing, def.fullId, StringComparison.Ordinal))
                {
                    Debug.LogWarning("[MoreFood/Event] 事件短 id 冲突: '" + def.id + "' 已指向 "
                                   + existing + "，后到者 " + def.fullId + " 只能用完整 id 引用");
                }
                return;
            }
            _shortToFullId[def.id] = def.fullId;
        }
    }
}
