// ImprovArtwork.cs — 行为示例 2「即兴演出2」：N 次艺术品装饰瞬完 + 绕过艺术技能前置

using System;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

namespace MoreFood_KFC
{
    public static class ImprovArtwork
    {
        // 启动：接下来 count 次艺术品装饰瞬间完成 + 无视艺术技能前置
        public static void Start(GameObject go, int count)
        {
            WorkRush.Start(go, WorkRushKind.Artwork, count);
        }

        // 清零（摘 buff 请用 BuffManager.Remove）；只清 Artwork 这一类，不动「即兴演出」
        public static void Stop(GameObject go)
        {
            WorkRush.Stop(go, WorkRushKind.Artwork);
        }

        // 取该小人身上「即兴演出2」还剩几次可瞬完（没有 tracker 或已用完 = 0）
        public static int Remaining(GameObject go)
        {
            return WorkRush.Remaining(go, WorkRushKind.Artwork);
        }

        // 是否处于"即兴演出2 生效中"（还艺术技能前置是否该放行的判据）
        public static bool Active(GameObject go)
        {
            return WorkRush.IsActive(go, WorkRushKind.Artwork);
        }
    }

    // 技能前置绕过：包装 ChorePreconditions.instance.HasSkillPerk
    [HarmonyPatch(typeof(ChorePreconditions), MethodType.Constructor, new Type[0])]
    public static class ImprovArtwork_SkillPerkBypass_Patch
    {
        // 补丁是否已安装
        private static bool _installed;

        // 补丁：ChorePreconditions 构造后替换 HasSkillPerk —— 让「即兴演出2」期间无视艺术技能前置（仅该 buff 生效且还剩次数时放行）
        public static void Postfix(ChorePreconditions __instance)
        {
            try
            {
            if (_installed || __instance == null) return;
            _installed = true;

            try
            {
                Chore.Precondition original = __instance.HasSkillPerk;

                Chore.Precondition wrapped = default(Chore.Precondition);
                wrapped.id = original.id;
                wrapped.description = original.description;
                wrapped.sortOrder = original.sortOrder;
                wrapped.canExecuteOnAnyThread = original.canExecuteOnAnyThread;
                wrapped.fn = delegate(ref Chore.Precondition.Context context, object data)
                {
                    // ⚠ 整体 try/catch：这个委托挂在 chore 的前置条件里，
                    try
                    {
                        // 只在"艺术类技能前置 + 小人身上有即兴演出2 且没次数用完"时放行
                        if (IsArtPerk(data) && context.consumerState != null
                            && ImprovArtwork.Active(context.consumerState.gameObject))
                            return true;
                    }
                    catch (Exception e)
                    {
                        MFLog.Exc(LogCat.Buff, e, "[MoreFood/Artwork] 技能前置判定异常（已忽略）");
                    }

                    // 其余情况原样透传，不碰任何别的技能前置
                    return original.fn(ref context, data);
                };

                __instance.HasSkillPerk = wrapped;
                MFLog.Info(LogCat.Buff, "[MoreFood/Artwork] 已装上「艺术技能前置」绕过（只对即兴演出2 生效）");
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Buff, e, "[MoreFood/Artwork] 安装技能前置绕过失败");
            }
            }
            catch (System.Exception ex) { MFLog.HotFail("patch:ImprovArtwork_SkillPerkBypass_Patch", ex); }
        }

        // 只认艺术类技能 perk，别的技能前置一律不动
        private static bool IsArtPerk(object data)
        {
            // ⚠ SkillPerk 的完整名字是 Database.SkillPerk（在 Database 命名空间下），
            var perk = data as Database.SkillPerk;
            if (perk == null) return false;   // 传 HashedString / string 的情况不拦

            var perks = Db.Get() != null ? Db.Get().SkillPerks : null;
            if (perks == null) return false;

            return perk == perks.CanArt
                || perk == perks.CanArtUgly
                || perk == perks.CanArtOkay
                || perk == perks.CanArtGreat;
        }
    }
}
