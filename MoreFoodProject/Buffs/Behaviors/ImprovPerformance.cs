// ImprovPerformance.cs — 行为示例 1「即兴演出」：BuildOrDig 类瞬完的语义化 API（机制在 WorkRush.cs）

using System;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class ImprovPerformance
    {
        // 启动：接下来 count 次建造/挖掘瞬间完成
        public static void Start(GameObject go, int count)
        {
            WorkRush.Start(go, WorkRushKind.BuildOrDig, count);
        }

        // 清零（摘 buff 请用 BuffManager.Remove）；只清 BuildOrDig 这一类，不动「即兴演出2」
        public static void Stop(GameObject go)
        {
            WorkRush.Stop(go, WorkRushKind.BuildOrDig);
        }

        // 取该小人身上「即兴演出」还剩几次可瞬完（没有 tracker 或已用完 = 0）
        public static int Remaining(GameObject go)
        {
            return WorkRush.Remaining(go, WorkRushKind.BuildOrDig);
        }

        // 判断「即兴演出」行为当前是否还在生效（剩余次数 > 0）
        public static bool Active(GameObject go)
        {
            return WorkRush.IsActive(go, WorkRushKind.BuildOrDig);
        }
    }
}
