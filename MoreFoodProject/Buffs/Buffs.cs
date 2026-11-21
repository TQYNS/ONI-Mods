// Buffs.cs — 所有实际 buff 的汇总注册处（工厂定义在 BuffConfig.cs、行为实现在 Behaviors/；新增 buff 只改本文件，统一按 id 引用）

using UnityEngine;

namespace MoreFood_KFC
{
    public static class Buffs
    {
        // buff id（供外部按 id 引用）

        // 脱水（水位低于 DrinkDesireMonitor.DEHYDRATED_AT）
        public const string DehydratedId = "DrinkDehydrated";

        // 即兴演出 —— 建造/挖掘瞬完（暂无饮品挂载）
        public const string ImprovPerformanceId = "DrinkImprovPerformance";

        // 崩溃艺术家 —— 艺术瞬完 + 无视技能前置（香草奶油布丁挂载，暂无饮品挂载）
        public const string ImprovArtworkId = "DrinkImprovArtwork";

        // 增益行配色（金）
        private static readonly SpanStyle AccentStyle = new SpanStyle().Color("#FFD700").Bold();

        // 全部 buff 的注册（幂等，可重复调用）
        public static void Register()
        {
            if (BuffConfig.Exists(DehydratedId)) return;

            // 缺水惩罚：脱水
            BuffConfig.Add(DehydratedId, ModStrings.BUFFS.DEHYDRATED_NAME, ModStrings.BUFFS.DEHYDRATED_DESC, true)
                .Add(StatIds.Athletics, -2f)
                .Add(StatIds.Morale, -3f)
                .AddDrainAccel(StatIds.Stamina, 0.30f);

            // 即兴演出：数值 士气 +1（必定生效）；行为 接下来 3 次建造/挖掘瞬完（实现在 Behaviors/ImprovPerformance.cs，瞬完复用 WorkRush）
            BuffDef improvPerformance = BuffConfig.Add(
                ImprovPerformanceId, ModStrings.BUFFS.IMPROVPERF_NAME,
                ModStrings.BUFFS.IMPROVPERF_DESC, false);
            improvPerformance.duration = 600f;
            improvPerformance
                .Add(StatIds.Morale, 1f)                                  // ← 数值部分：必定生效
                .Special("rush", 0.50f,
                         go => { ImprovPerformance.Start(go, 3); return true; },
                         SpecialTrigger.Apply, summary: "瞬完 3 次建造/挖掘")
                .OnTick((go, def, dt) =>                                   // ← 次数用完自摘（只在行为生效中才摘）
                {
                    if (ImprovPerformance.Active(go) && ImprovPerformance.Remaining(go) <= 0)
                        BuffManager.Remove(go, ImprovPerformanceId);
                })
                .OnExpire((go, def) => ImprovPerformance.Stop(go));        // ← buff 结束（含 duration 到期）就停行为
            CountedBuffDisplay.Register(ImprovPerformanceId, b => b
                .Row(r => r.Label("建造/挖掘瞬完", AccentStyle).Span("50% 触发"))
                .Row(r => r.Label("剩余次数", AccentStyle)
                           .Span(go => WorkRush.Remaining(go, WorkRushKind.BuildOrDig).ToString())));

            // 崩溃艺术家：数值 运动 +1、艺术 +3（必定生效）；行为 接下来 3 次艺术品装饰瞬完且无视艺术技能前置（实现在 Behaviors/ImprovArtwork.cs）
            BuffDef improvArtwork = BuffConfig.Add(
                ImprovArtworkId, ModStrings.BUFFS.IMPROVART_NAME,
                ModStrings.BUFFS.IMPROVART_DESC, false);
            improvArtwork.duration = 600f;
            improvArtwork
                .Add(StatIds.Athletics, 1f)                                // ← 数值部分：必定生效
                .Add(StatIds.Art, 3f)                                      // ← 数值部分：必定生效
                .Special("rush", 0.40f,
                         go => { ImprovArtwork.Start(go, 3); return true; },
                         SpecialTrigger.Apply, summary: "瞬完 3 次艺术装饰")
                .OnTick((go, def, dt) =>                                    // ← 次数用完自摘（只在行为生效中才摘）
                {
                    if (ImprovArtwork.Active(go) && ImprovArtwork.Remaining(go) <= 0)
                        BuffManager.Remove(go, ImprovArtworkId);
                })
                .OnExpire((go, def) => ImprovArtwork.Stop(go));             // ← buff 结束（含 duration 到期）就停行为
            CountedBuffDisplay.Register(ImprovArtworkId, b => b
                .Row(r => r.Label("艺术品装饰瞬完", AccentStyle).Span("40% 触发"))
                .Row(r => r.Label("剩余次数", AccentStyle)
                           .Span(go => WorkRush.Remaining(go, WorkRushKind.Artwork).ToString())));
        }
    }
}
