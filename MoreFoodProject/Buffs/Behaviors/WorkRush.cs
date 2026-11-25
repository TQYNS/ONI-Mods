// WorkRush.cs — 「瞬间完成工作」通用机制：加枚举 + Matches 一行即可扩展；每种 kind 各一份计数，互不覆盖

using System;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace MoreFood_KFC
{
// 瞬完哪一类工作（新增一类 = 加枚举值 + 在 Matches 里加一行判断 + 在 WorkRushTracker 里加一个计数字段）
    public enum WorkRushKind
    {
// 建造 + 挖掘（两类工作共用一个计数）
        BuildOrDig,
// 艺术品装饰（Artable）
        Artwork,
    }

    // 计数载体（存盘）：每个 kind 一份计数，互不覆盖；字段必须 public 可写（KSerialization 靠反射赋值）
    public class WorkRushTracker : KMonoBehaviour
    {
        // 旧字段：v5.0 及更早的存档里只有这一份状态，现仅作迁移来源，新逻辑不再写入
        [Serialize] public int remaining;
        [Serialize] public int kindRaw;

        // 新字段：按 kind 各一份
        [Serialize] public int remainingBuildOrDig;
        [Serialize] public int remainingArtwork;

        // 迁移标记（0 = 还没迁移过；迁一次就置 1，避免重复搬）
        [Serialize] public int migrated;

        // 旧版语义的 kind（只给迁移读，新代码不写）
        public WorkRushKind kind
        {
            get { return (WorkRushKind)kindRaw; }
            set { kindRaw = (int)value; }
        }

        public int Get(WorkRushKind k)
        {
            return k == WorkRushKind.Artwork ? remainingArtwork : remainingBuildOrDig;
        }

        public void Set(WorkRushKind k, int v)
        {
            if (k == WorkRushKind.Artwork) remainingArtwork = v;
            else remainingBuildOrDig = v;
        }

        // 老档迁移：旧版唯一 remaining 搬到对应 kind 后清旧值；新档只置标记，幂等
        internal void MigrateLegacyOnce()
        {
            if (migrated != 0) return;
            migrated = 1;
            if (remaining <= 0) return;

            WorkRushKind old = (WorkRushKind)kindRaw;
            if (old == WorkRushKind.Artwork)
                remainingArtwork = Mathf.Max(remainingArtwork, remaining);
            else
                remainingBuildOrDig = Mathf.Max(remainingBuildOrDig, remaining);

            remaining = 0;   // 清掉，防止下次又被搬一次
        }
    }

    public static class WorkRush
    {
        // 给目标挂上某一类瞬完的计数（只覆盖这一类，不影响另一类）
        public static void Start(GameObject go, WorkRushKind kind, int count)
        {
            if (go == null) return;
            var tracker = go.AddOrGet<WorkRushTracker>();
            tracker.MigrateLegacyOnce();
            int v = Mathf.Max(0, count);
            tracker.Set(kind, v);
            MFLog.Verbose(LogCat.Buff, "[MoreFood/Rush] " + go.name + " 获得「" + kind + "」瞬完，剩余 " + v + " 次");
        }

        // 停掉某一类瞬完行为（只清这一类）
        public static void Stop(GameObject go, WorkRushKind kind)
        {
            if (go == null) return;
            var tracker = go.GetComponent<WorkRushTracker>();
            if (tracker == null) return;
            tracker.MigrateLegacyOnce();
            tracker.Set(kind, 0);
        }

        // 取该小人某一类还剩几次（没有 tracker 或已用完 = 0）
        public static int Remaining(GameObject go, WorkRushKind kind)
        {
            if (go == null) return 0;
            var tracker = go.GetComponent<WorkRushTracker>();
            if (tracker == null) return 0;
            tracker.MigrateLegacyOnce();
            return tracker.Get(kind);
        }

// 目标身上是否有「指定类型且还没用完」的瞬完
        public static bool IsActive(GameObject go, WorkRushKind kind)
        {
            if (go == null) return false;
            var tracker = go.GetComponent<WorkRushTracker>();
            if (tracker == null) return false;
            tracker.MigrateLegacyOnce();
            return tracker.Get(kind) > 0;
        }

// 这类工作是否属于某个瞬完类型
        internal static bool Matches(Workable workable, WorkRushKind kind)
        {
            if (workable == null) return false;
            switch (kind)
            {
                case WorkRushKind.BuildOrDig:
                    return workable is Constructable || workable is Diggable;
                case WorkRushKind.Artwork:
                    return workable is Artable;
            }
            return false;
        }

        // 瞬完判定：按工作类型反推 kind 再看剩余次数，两类瞬完可同时存在、各扣各的
        internal static void TryRush(Workable workable, WorkerBase worker)
        {
            // ⚠ 整体 try/catch：这个补丁挂在 Workable.OnWorkTick 上，
            try
            {
                if (workable == null || worker == null) return;

                WorkRushKind kind;
                if (Matches(workable, WorkRushKind.BuildOrDig)) kind = WorkRushKind.BuildOrDig;
                else if (Matches(workable, WorkRushKind.Artwork)) kind = WorkRushKind.Artwork;
                else return;   // 与瞬完无关的工作，直接走原逻辑

                var tracker = worker.GetComponent<WorkRushTracker>();
                if (tracker == null) return;
                tracker.MigrateLegacyOnce();

                int left = tracker.Get(kind);
                if (left <= 0) return;

                // 已经完成了就不要再计一次（override 若调了 base 会二次进入）
                if (workable.WorkTimeRemaining <= 0f) return;

                workable.WorkTimeRemaining = 0f;   // 本 tick 直接完成
                tracker.Set(kind, left - 1);

                MFLog.Verbose(LogCat.Buff, "[MoreFood/Rush] " + worker.name + " 瞬完 "
                          + workable.GetType().Name + "（" + kind + "），剩余 " + (left - 1) + " 次");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Rush] 瞬完判定异常（已忽略，不影响工作）");
            }
        }
    }

    // 补丁 1：基类 —— 覆盖 Constructable / Artable 等没有 override 的
    [HarmonyPatch(typeof(Workable), "OnWorkTick")]
    public static class WorkRush_WorkableTick_Patch
    {
        // 补丁：Workable.OnWorkTick 后判瞬完 —— 覆盖 Constructable / Artable 等【没有 override】的工作
        public static void Postfix(Workable __instance, WorkerBase worker)
        {
            try
            {
                WorkRush.TryRush(__instance, worker);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WorkRush_WorkableTick_Patch", e); }
        }
    }

    // 补丁 2：挖掘 —— Diggable 有自己的 override，基类补丁拦不到
    [HarmonyPatch(typeof(Diggable), "OnWorkTick")]
    public static class WorkRush_DiggableTick_Patch
    {
        // 补丁：Diggable.OnWorkTick 后判瞬完 —— Diggable 有自己的 override，基类补丁拦不到，必须单独打
        public static void Postfix(Diggable __instance, WorkerBase worker)
        {
            try
            {
                WorkRush.TryRush(__instance, worker);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:WorkRush_DiggableTick_Patch", e); }
        }
    }

// 补丁 3：预挂持久组件到小人 prefab
    [HarmonyPatch(typeof(MinionConfig), "CreatePrefab", new Type[0])]
    public static class MinionConfig_PersistentComponents_Patch
    {
        // Harmony 补丁：Postfix（参数 __result：结果）
        public static void Postfix(GameObject __result)
        {
            if (__result == null) return;
            try
            {
                // 用 AddOrGet：幂等，如果 prefab 上已经有了就直接复用
                __result.AddOrGet<WorkRushTracker>();
                __result.AddOrGet<BuffBehaviorState>();
                MFLog.Info(LogCat.Buff, "[MoreFood/Rush] 已为小人 prefab 预挂 "
                          + "WorkRushTracker + BuffBehaviorState（存读安全）");
            }
            catch (Exception e)
            {
                // 预挂失败不影响游戏；只是这两个组件的字段读档时会丢
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Rush] 预挂持久组件到 prefab 失败");
            }
        }
    }
}
