using System;
using HarmonyLib;
using EventFramework.Distribution.Contracts;

namespace EventFramework.Distribution.Patches
{
    // 8 个生命周期补丁的手动注册表：固定 harmonyId 下逐条 Patch，不走 PatchAll
    internal static class LifecyclePatches
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
                    DistributionLog.Warn("生命周期补丁装配收到空 harmony");
                    return 0;
                }
                _done = true;

                Type handler = typeof(EventSystemPatches);
                int ok = 0;
                int miss = 0;

                if (TryPatch(harmony, handler, "Db", "Initialize",
                             postfix: "Postfix_Db_Initialize")) ok++; else miss++;

                if (TryPatch(harmony, handler, "Game", "OnPrefabInit",
                             postfix: "Postfix_Game_OnPrefabInit")) ok++; else miss++;

                if (TryPatch(harmony, handler, "Game", "OnSpawn",
                             postfix: "Postfix_Game_OnSpawn")) ok++; else miss++;

                // 存档载体挂载点：SaveGame 的 GameObject 才带 SaveLoadRoot
                if (TryPatch(harmony, handler, "SaveGame", "OnPrefabInit",
                             postfix: "Postfix_SaveGame_OnPrefabInit")) ok++; else miss++;

                // 读档完成时刻
                if (TryPatch(harmony, handler, "SaveLoadRoot", "LoadInternal",
                             postfix: "Postfix_SaveLoadRoot_LoadInternal")) ok++; else miss++;

                // 管理菜单挂 tab
                if (TryPatch(harmony, handler, "ManagementMenu", "OnPrefabInit",
                             postfix: "Postfix_ManagementMenu_OnPrefabInit")) ok++; else miss++;

                // 音频层：退档清理
                if (TryPatch(harmony, handler, "Game", "StopBE",
                             prefix: "Prefix_Game_StopBE")) ok++; else miss++;

                // 音频层：音量跟随
                if (TryPatch(harmony, handler, "AudioMixer", "SetUserVolume",
                             postfix: "Postfix_AudioMixer_SetUserVolume")) ok++; else miss++;

                _ok = ok;
                _miss = miss;
            }
            catch (Exception e)
            {
                DistributionLog.Warn("生命周期补丁装配失败：" + e);
            }
            return _ok;
        }

        // 逐条注册：目标类型/方法找不到只告警跳过，不影响其它补丁
        private static bool TryPatch(Harmony harmony, Type handlerType, string typeName, string methodName,
                                     string postfix = null, string prefix = null)
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

                var method = AccessTools.Method(target, methodName);
                if (method == null)
                {
                    DistributionLog.Warn("找不到方法 " + tag + "，跳过");
                    return false;
                }

                var pf = Resolve(handlerType, postfix, tag);
                var px = Resolve(handlerType, prefix, tag);
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

        private static HarmonyMethod Resolve(Type handlerType, string name, string tag)
        {
            if (name == null) return null;
            var m = AccessTools.Method(handlerType, name);
            if (m == null)
            {
                DistributionLog.Warn("找不到补丁方法 " + handlerType.Name + "." + name + "（" + tag + "）");
                return null;
            }
            return new HarmonyMethod(m);
        }
    }
}
