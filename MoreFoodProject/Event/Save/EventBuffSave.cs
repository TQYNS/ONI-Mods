// EventBuffSave.cs — buff 快照的挂载与存盘补写（宿主自管两个 Harmony 补丁）；时机与根因见 事件.md §24.25

using System;
using HarmonyLib;

namespace MoreFood_KFC
{
    // 挂载：SaveGame 对象一出现就把快照载体挂上
    [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
    internal static class EventBuffSaveMountPatch
    {
        private static void Postfix()
        {
            try
            {
                EventSaveData data = EventSaveData.Attach();
                MFLog.Info(LogCat.Save, "[MoreFood/Save] buff 快照载体已挂上 SaveGame 对象："
                          + (data != null ? "ok" : "失败"));
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Save, e, "[MoreFood/Save] buff 快照载体挂载异常（照常保存）");
            }
        }
    }

    // 补写：序列化之前重算快照，保证读档倒计时准确
    [HarmonyPatch(typeof(SaveLoader), "Save", new Type[] { typeof(string), typeof(bool), typeof(bool) })]
    internal static class EventBuffSaveCapturePatch
    {
        private static void Prefix()
        {
            try
            {
                SpaceRealEventRuntime.PersistBuffsOnSave();
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Save, e, "[MoreFood/Save] buff 快照补写失败（照常保存）");
            }
        }
    }
}
