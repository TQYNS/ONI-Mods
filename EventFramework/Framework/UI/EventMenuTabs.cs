// EventMenuTabs.cs — 事件框架 · 【在管理菜单顶部挂 3 个 tab】

using System;
using EventFramework.Distribution.Contracts;
using UnityEngine;

namespace EventFramework
{
    public static class EventMenuTabs
    {
        // ⚠⚠ 这里【不能】用 static bool 做"已挂载"守卫！

        public static void Mount(ManagementMenu menu)
        {
            if (menu == null)
            {
                Debug.LogWarning("[MoreFood/Event][Tab] ManagementMenu 为 null，未挂载");
                return;
            }

            try
            {
                // 顺序 = 数组顺序（AddTabs 按序 Insert 到「饮食」之后）
                var specs = new[]
                {
                    // 「饮品」tab 已归宿主自挂（C09：能力内建 / 宿主自管，框架不再代劳）
                    new MenuTabRegistry.TabSpec
                    {
                        text    = "事件",
                        icon    = "OverviewUI_reports_icon",
                        tooltip = "事件历史汇总",
                        screen  = EventHistoryUI.GetOrCreate(),
                    },
                    // ⚠ 「图鉴」tab 暂缓
                };

                int n = MenuTabRegistry.AddTabs(menu, specs);
                Debug.Log("[MoreFood/Event][Tab] 挂载结束：新增 " + n + " 个 tab");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MoreFood/Event][Tab] Mount 失败（不影响原版菜单）: " + e);
            }
        }

        // 无需 Reset：Mount 每次都真实执行（幂等交给 AddTabs 的 ContainsText 检查）
    }
}
