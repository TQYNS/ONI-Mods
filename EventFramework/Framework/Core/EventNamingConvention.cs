// EventNamingConvention.cs — 事件框架 · 【全局 ID 命名空间提醒】

using System.Collections.Generic;
using UnityEngine;

namespace EventFramework
{
    public static class EventNamingConvention
    {
        // 最多显示几个候选
        public const int MAX_SHOWN = 12;

        public static int Check()
        {
            var loose = new List<string>();

            Collect(loose, "事件id", EventRegistry.AllIds);
            Collect(loose, "handler", EventHandlerRegistry.AllKeys);
            Collect(loose, "前置条件", EventConditionRegistry.AllKeys);
            Collect(loose, "音频模板", EventAudioFactory.AllKeys);
            Collect(loose, "音频脚本", EventAudioScriptRegistry.AllKeys);
            Collect(loose, "行为函数", EventHookRegistry.AllKeys);
            Collect(loose, "音频开关键", AudioSwitch.AllDeclared());

            if (loose.Count == 0)
            {
                Debug.Log("[MoreFood/Event][命名] 全部全局 ID 都带命名空间");
                return 0;
            }

            int shown = loose.Count < MAX_SHOWN ? loose.Count : MAX_SHOWN;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append("、");
                sb.Append(loose[i]);
            }
            if (loose.Count > shown) sb.Append(" …");

            Debug.LogWarning("[MoreFood/Event][命名] " + loose.Count
                             + " 个全局 ID 没带命名空间（建议 <modid>.<域>.<名字>，"
                             + "如 eventfw.meteor.can_start）：" + sb);
            return loose.Count;
        }

        private static void Collect(List<string> into, string face, IEnumerable<string> keys)
        {
            if (keys == null) return;
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k)) continue;
                if (k.IndexOf('.') >= 0 || k.IndexOf(':') >= 0) continue;
                into.Add(face + "/" + k);
            }
        }
    }
}
