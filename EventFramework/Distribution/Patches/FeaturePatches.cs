using System;
using HarmonyLib;
using EventFramework.Distribution.Contracts;

namespace EventFramework.Distribution.Patches
{
    // 8 个特性补丁的手动注册表：类上已无 [HarmonyPatch]，由这里逐条 Patch
    internal static class FeaturePatches
    {
        internal const int Expected = 8;

        private static bool _done;
        private static int _ok;
        private static int _miss;

        // 本次（或首次）装配跳过的条数，供汇总日志使用
        internal static int Skipped { get { return _miss; } }

        internal static int Install(Harmony harmony)
        {
            if (_done) return _ok;
            try
            {
                if (harmony == null)
                {
                    DistributionLog.Warn("特性补丁装配收到空 harmony");
                    return 0;
                }
                _done = true;

                int ok = 0;
                int miss = 0;

                // 通知屏外观
                if (TryPatch(harmony, "NotificationScreen", "AddNotification",
                             typeof(NotificationScreen_Style_Patch), postfix: "Postfix")) ok++; else miss++;

                // 通知清理探针
                if (TryPatch(harmony, "Notification", "Clear",
                             typeof(Notification_Clear_Probe), postfix: "Postfix")) ok++; else miss++;

                // 叙事者选择屏：点分类卡
                if (TryPatch(harmony, "ClusterCategorySelectionScreen", "OnClickOption",
                             typeof(EventNarratorSelection_Patch), prefix: "Prefix")) ok++; else miss++;

                // 叙事者选择屏：生成后捕获实例
                if (TryPatch(harmony, "ClusterCategorySelectionScreen", "OnSpawn",
                             typeof(EventNarratorSelection_SpawnPatch), postfix: "Postfix")) ok++; else miss++;

                // 新开局入口：清残留
                if (TryPatch(harmony, "NewGameFlow", "BeginFlow",
                             typeof(EventNarratorSelection_BeginFlowPatch), postfix: "Postfix")) ok++; else miss++;

                // ESC 优先关面板
                if (TryPatch(harmony, "RootMenu", "OnKeyDown",
                             typeof(EventPanelEscPriorityPatch), prefix: "Prefix")) ok++; else miss++;

                // 星图格子射线拦截
                if (TryPatch(harmony, "ClusterMapHex", "IsRaycastLocationValid",
                             typeof(ClusterMapHexBlockerPatch), prefix: "Prefix")) ok++; else miss++;

                // 星图打开时收起事件历史屏（目标方法有重载，必须带参数类型）
                if (TryPatch(harmony, "ClusterMapScreen", "OnShow",
                             typeof(ClusterMapScreen_OnShow_CloseHistoryPatch), postfix: "Postfix",
                             args: new[] { typeof(bool) })) ok++; else miss++;

                _ok = ok;
                _miss = miss;
            }
            catch (Exception e)
            {
                DistributionLog.Warn("特性补丁装配失败：" + e);
            }
            return _ok;
        }

        // 逐条注册：目标类型/方法找不到只告警跳过，不影响其它补丁
        private static bool TryPatch(Harmony harmony, string typeName, string methodName,
                                     Type patchType, string postfix = null, string prefix = null,
                                     Type[] args = null)
        {
            string tag = typeName + "." + methodName;
            try
            {
                Type target = AccessTools.TypeByName(typeName);
                if (target == null)
                {
                    DistributionLog.Warn("找不到类型 " + typeName + "，跳过补丁 " + tag);
                    return false;
                }

                var method = AccessTools.Method(target, methodName, args);
                if (method == null)
                {
                    DistributionLog.Warn("找不到方法 " + tag + "，跳过");
                    return false;
                }

                var pf = Resolve(patchType, postfix, tag);
                var px = Resolve(patchType, prefix, tag);
                if ((postfix != null && pf == null) || (prefix != null && px == null)) return false;

                harmony.Patch(method, prefix: px, postfix: pf);
                DistributionLog.Info("已打补丁 " + tag);
                return true;
            }
            catch (Exception e)
            {
                DistributionLog.Warn("打补丁失败 " + tag + "：" + e);
                return false;
            }
        }

        private static HarmonyMethod Resolve(Type patchType, string name, string tag)
        {
            if (name == null) return null;
            var m = AccessTools.Method(patchType, name);
            if (m == null)
            {
                DistributionLog.Warn("找不到补丁方法 " + patchType.Name + "." + name + "（" + tag + "）");
                return null;
            }
            return new HarmonyMethod(m);
        }
    }
}
