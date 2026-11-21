using System;
using System.Reflection;
using UnityEngine;

namespace EventFramework
{
    public static class PodDelivery
    {
        // 本类日志前缀
        public const string LOG_TAG = "[MoreFood/Event][Item]";

        // 打印门实例缓存
        private static Telepad _pad;
        // 反射缓存的「关闭传送门信号」字段
        private static FieldInfo _closePortalSignal;
        // 反射缓存的信号触发方法
        private static MethodInfo _signalTrigger;
        // 是否已探测过动画字段
        private static bool _animProbed;

        public static Telepad FindTelepad()
        {
            if (_pad != null) return _pad;

            try
            {
                var all = Components.Telepads.Items;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == null) continue;
                    _pad = all[i];
                    break;
                }
            }
            catch (Exception e)
            {
                Distribution.Contracts.DistributionLog.ExcOnce("pod:find", "[Pod] 查找打印门失败 → 本次不派送", e);
                _pad = null;
            }

            return _pad;
        }

        public static void ClearCache()
        {
            _pad = null;
        }

        // 尝试把物品投递到打印舱（找不到打印舱返回 false）
        public static bool TryDeliver(string itemId, float mass)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            var pad = FindTelepad();
            if (pad == null) return false;

            GameObject spawned = null;
            try
            {
                Vector3 pos = Grid.CellToPosCBC(Grid.PosToCell(pad), Grid.SceneLayer.Move);
                var info = new CarePackageInfo(itemId, mass, null);
                spawned = info.Deliver(pos);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(LOG_TAG + " 打印门派送 '" + itemId + "' 异常: " + e);
                return false;
            }

            if (spawned == null) return false;

            PlayPortalAnim(pad);
            return true;
        }

        // 播放打印门的传送门动画（原版 OnAcceptDelivery 里那一句）。
        private static void PlayPortalAnim(Telepad pad)
        {
            try
            {
                if (!_animProbed)
                {
                    _animProbed = true;

                    object smi = null;
                    var smiProp = typeof(Telepad).GetProperty("smi",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (smiProp != null) smi = smiProp.GetValue(pad, null);

                    if (smi != null)
                    {
                        var smProp = smi.GetType().GetProperty("sm",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        object sm = smProp != null ? smProp.GetValue(smi, null) : null;
                        if (sm != null)
                        {
                            _closePortalSignal = sm.GetType().GetField("closePortal",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (_closePortalSignal != null)
                            {
                                _signalTrigger = _closePortalSignal.FieldType.GetMethod(
                                    "Trigger", new[] { smi.GetType() })
                                    ?? _closePortalSignal.FieldType.GetMethod(
                                        "Trigger", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            }
                        }
                    }

                    if (_closePortalSignal == null || _signalTrigger == null)
                        Debug.LogWarning(LOG_TAG + " 打印门动画信号取不到（版本差异）→ 只派送物品，不播动画");
                }

                if (_closePortalSignal == null || _signalTrigger == null) return;

                var smiProp2 = typeof(Telepad).GetProperty("smi",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object smiNow = smiProp2 != null ? smiProp2.GetValue(pad, null) : null;
                if (smiNow == null) return;

                var smProp2 = smiNow.GetType().GetProperty("sm",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object smNow = smProp2 != null ? smProp2.GetValue(smiNow, null) : null;
                if (smNow == null) return;

                object signal = _closePortalSignal.GetValue(smNow);
                if (signal == null) return;

                var args = _signalTrigger.GetParameters().Length == 1 ? new object[] { smiNow } : null;
                _signalTrigger.Invoke(signal, args);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(LOG_TAG + " 打印门动画播放失败（忽略）: " + e);
            }
        }
    }
}
