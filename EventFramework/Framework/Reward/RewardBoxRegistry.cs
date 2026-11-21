using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class RewardBoxRegistry
    {
        // 本类日志前缀
        public const string LOG_TAG = "[MoreFood/Event][Reward]";

        private static readonly Dictionary<string, RewardBoxDef> _byFullId =
            new Dictionary<string, RewardBoxDef>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> _shortToFullId =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly List<string> _loadErrors = new List<string>();

        public static int Count { get { return _byFullId.Count; } }
        public static IEnumerable<RewardBoxDef> All { get { return _byFullId.Values; } }
        public static IEnumerable<string> AllFullIds { get { return _byFullId.Keys; } }
        public static List<string> LoadErrors { get { return _loadErrors; } }

        public static void Clear()
        {
            _byFullId.Clear();
            _shortToFullId.Clear();
            _loadErrors.Clear();
        }

        // 拼奖励池的全局唯一 id：<mod 前缀>.reward.<id>（无前缀时直接用 id）
        // 与事件的全 ID 同一套命名空间风格（<前缀>.event.<id>），跨 Mod 同名短 id 不冲突
        public static string MakeFullId(string modPrefix, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return string.IsNullOrEmpty(modPrefix) ? id : modPrefix + ".reward." + id;
        }

        // 注册奖励池定义（fullId 为空则忽略并告警）
        public static bool Add(RewardBoxDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.fullId))
            {
                Debug.LogWarning(LOG_TAG + " Add：fullId 为空，忽略（来自 "
                                 + (def != null ? def.sourceFile : "<null>") + "）");
                return false;
            }

            if (_byFullId.ContainsKey(def.fullId))
            {
                string msg = "池 id 重复: " + def.fullId + "（先=" + _byFullId[def.fullId].sourceFile
                             + "，后=" + def.sourceFile + "），保留先到的";
                Debug.LogWarning(LOG_TAG + " " + msg);
                _loadErrors.Add(msg);
                return false;
            }

            _byFullId[def.fullId] = def;
            RegisterShort(def);
            return true;
        }

        // 用补丁覆盖已注册的奖励池（缺 mode_target 视为非法补丁）
        public static bool Override(RewardBoxDef patch)
        {
            if (patch == null || string.IsNullOrEmpty(patch.mode_target))
            {
                Debug.LogWarning(LOG_TAG + " 补丁缺少 mode_target: "
                               + (patch != null ? patch.sourceFile : "<null>"));
                return false;
            }

            RewardBoxDef target;
            if (!_byFullId.TryGetValue(patch.mode_target, out target))
            {
                Debug.LogWarning(LOG_TAG + " 补丁目标不存在: " + patch.mode_target
                               + "（来自 " + patch.sourceFile + "）");
                return false;
            }

            patch.fullId = target.fullId;
            patch.sourceMod = target.sourceMod;
            patch.sourceFile = target.sourceFile;
            patch.lastPatchedBy = patch.lastPatchedBy ?? patch.sourceMod;
            patch.lastPatchedFrom = patch.lastPatchedFrom ?? patch.sourceFile;

            _byFullId[target.fullId] = patch;
            RegisterShort(patch);

            Debug.Log(LOG_TAG + " 已覆盖池 '" + target.fullId + "'（补丁来自 "
                      + patch.lastPatchedBy + "：" + patch.lastPatchedFrom + "）");
            return true;
        }

        // keyOrFullId：keyorfull id
        public static RewardBoxDef Get(string keyOrFullId)
        {
            if (string.IsNullOrEmpty(keyOrFullId)) return null;

            RewardBoxDef def;
            if (_byFullId.TryGetValue(keyOrFullId, out def)) return def;

            string fullId;
            if (_shortToFullId.TryGetValue(keyOrFullId, out fullId)
                && _byFullId.TryGetValue(fullId, out def)) return def;

            return null;
        }

        // keyOrFullId：标识 id
        public static bool Has(string keyOrFullId)
        {
            return Get(keyOrFullId) != null;
        }

        public static string Describe()
        {
            return "共 " + _byFullId.Count + " 个奖励池";
        }

        private static void RegisterShort(RewardBoxDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;

            string existing;
            if (_shortToFullId.TryGetValue(def.id, out existing))
            {
                if (!string.Equals(existing, def.fullId, StringComparison.Ordinal))
                {
                    Debug.LogWarning(LOG_TAG + " 短 id 冲突: '" + def.id + "' 已指向 " + existing
                                   + "，后到者 " + def.fullId + " 只能用完整 id 引用");
                }
                return;
            }
            _shortToFullId[def.id] = def.fullId;
        }
    }
}
