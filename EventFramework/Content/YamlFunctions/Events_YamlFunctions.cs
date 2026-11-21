// Events_YamlFunctions.cs — 事件框架 · 【yaml 调用的自定义行为函数 · 集中收纳】

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EventFramework.Content
{
    public static class Events_YamlFunctions
    {
        // 键名清单（改键只改这一处，yaml 那边同步）

        // ---- 测试事件 test_event.yaml ----
        public const string HANDLER_TEST = "TestEvent";
        // 测试预条件名
        public const string COND_TEST = "TestPrecondition";
        // 测试音效 key（注册进音频库）
        public const string AUDIO_TEST = "eventfw.test.audio";
        // 测试事件根节点「开始」钩子名（yaml hooks.root_start）
        public const string HOOK_TEST_ROOT_START = "eventfw.test.root_start";
        // 测试事件根节点「结束」钩子名（yaml hooks.root_end）
        public const string HOOK_TEST_ROOT_END = "eventfw.test.root_end";
        // 测试选项「开始」钩子名（yaml hooks.choice_start）
        public const string HOOK_TEST_CHOICE_START = "eventfw.test.choice_start";
        // 测试选项「结束」钩子名（yaml hooks.choice_end）
        public const string HOOK_TEST_CHOICE_END = "eventfw.test.choice_end";
        // 测试选项 A 的 id
        public const string OPT_TEST_A = "TestOptionA";
        // 测试选项 B 的 id
        public const string OPT_TEST_B = "TestOptionB";
        // 测试选项选后发出的信号名
        public const string OPT_TEST_SIGNAL = "eventfw.test.raise_signal";
        // 测试自定义信号名（EventSignal 的 Raise/Subscribe 用它）
        public const string SIG_TEST_PING = "eventfw.sig.test_ping";

        // ---- 链式故事 story_*.yaml ----
        public const string HANDLER_STORY = "StoryEvent";
        // 剧情预条件名
        public const string COND_STORY = "StoryPrecondition";
        // 剧情事件根节点「开始」钩子名
        public const string HOOK_STORY_START = "eventfw.story.root_start";
        // 剧情事件根节点「结束」钩子名
        public const string HOOK_STORY_END = "eventfw.story.root_end";
        // 剧情选项 id（选它推进剧情）
        public const string OPT_STORY_PICK = "StoryOption";

        // 集中注册
        public static void Register()
        {
            EventHandlerRegistry.Register(HANDLER_TEST, new EventHandler
            {
                onTrigger = Test_OnTrigger,
                onStart = Test_OnStart,
                onEnd = Test_OnEnd,
                options = new Dictionary<string, System.Action<EventInstance>>
                {
                    [OPT_TEST_A] = Test_OptionA,
                    [OPT_TEST_B] = Test_OptionB,
                    [OPT_TEST_SIGNAL] = Test_RaiseSignal,
                },
            });

            EventHandlerRegistry.Register(HANDLER_STORY, new EventHandler
            {
                onTrigger = Story_OnTrigger,
                onStart = Story_OnStart,
                onEnd = Story_OnEnd,
                options = new Dictionary<string, System.Action<EventInstance>>
                {
                    [OPT_STORY_PICK] = Story_Option,
                },
            });

            EventConditionRegistry.Register(COND_TEST, Test_YamlPrecondition, new string[0]);
            EventConditionRegistry.Register(COND_STORY, Story_YamlPrecondition, new string[0]);

            EventAudioScriptRegistry.Register(AUDIO_TEST, Test_OnAudioScript);

            EventHookRegistry.Register(HOOK_TEST_ROOT_START, Test_OnRootStart);
            EventHookRegistry.Register(HOOK_TEST_ROOT_END, Test_OnRootEnd);
            EventHookRegistry.Register(HOOK_TEST_CHOICE_START, Test_OnChoiceStart);
            EventHookRegistry.Register(HOOK_TEST_CHOICE_END, Test_OnChoiceEnd);

            EventHookRegistry.Register(HOOK_STORY_START, Story_OnRootStart);
            EventHookRegistry.Register(HOOK_STORY_END, Story_OnRootEnd);

            Debug.Log("[MoreFood/Event] yaml 行为函数已集中注册（" + HANDLER_TEST + " / " + HANDLER_STORY + "）");
        }

        // 测试事件
        private static bool Test_Precondition(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] TestEvent.Precondition 通过（" + inst.templateId + "）");
            return true;
        }

        private static bool Test_YamlPrecondition(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] yaml 前置条件 " + COND_TEST + " 通过（"
                      + inst.templateId + " cycle=" + inst.startCycle + "）");
            return true;
        }

        private static void Test_OnRootStart(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ▶ " + HOOK_TEST_ROOT_START + " 被调用: " + inst.templateId);
        }

        private static void Test_OnRootEnd(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ⏹ " + HOOK_TEST_ROOT_END + " 被调用: " + inst.templateId);
        }

        private static void Test_OnChoiceStart(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ▶ " + HOOK_TEST_CHOICE_START + " 被调用: " + inst.templateId
                      + " 选中项=" + inst.chosenIndex);
        }

        private static void Test_OnChoiceEnd(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ⏹ " + HOOK_TEST_CHOICE_END + " 被调用: " + inst.templateId
                      + " 选中项=" + inst.chosenIndex);
        }

        private static void Test_OnAudioScript(EventAudioContext ctx)
        {
            Debug.Log("[MoreFood/Event][Yaml] 音频脚本 " + AUDIO_TEST + " slot=" + ctx.Slot
                      + " 事件=" + (ctx.Def != null ? ctx.Def.id : "?"));

            ctx.Play("eventfw.ui.mouseover");
            ctx.Play("eventfw.ui.back", 2f);
        }

        private static void Test_OnTrigger(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ★ " + HANDLER_TEST + ".onTrigger: " + inst.instanceId
                      + " cycle=" + inst.startCycle + " 参与者=" + inst.participantIds.Count);
        }

        private static void Test_OnStart(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] " + HANDLER_TEST + ".onStart: " + inst.instanceId
                      + " 持续=" + (inst.hasDuration ? inst.GetDurationRemaining().ToString("F1") + "s" : "无"));
        }

        private static void Test_OnEnd(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] " + HANDLER_TEST + ".onEnd: " + inst.instanceId
                      + " 玩家选择数=" + inst.playerChoices.Count);
        }

        private static void Test_OptionA(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] " + OPT_TEST_A + "（" + inst.instanceId + "）");
        }

        private static void Test_OptionB(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] " + OPT_TEST_B + "（" + inst.instanceId + "）");
        }

        private static void Test_RaiseSignal(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] 发自定义信号 " + SIG_TEST_PING + "（" + inst.instanceId + "）");
            EventSignal.Raise(SIG_TEST_PING);
        }

        // 链式故事
        private static bool Story_Precondition(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] StoryEvent.Precondition 通过（" + inst.templateId + "）");
            return true;
        }

        private static bool Story_YamlPrecondition(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] yaml 前置条件 " + COND_STORY + " 通过（"
                      + inst.templateId + " cycle=" + inst.startCycle + "）");
            return true;
        }

        private static void Story_OnRootStart(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ▶ 故事节点开始：" + inst.templateId
                      + "（" + HOOK_STORY_START + "）");
        }

        private static void Story_OnRootEnd(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ⏹ 故事节点结束：" + inst.templateId
                      + " 选中项=" + inst.chosenIndex
                      + " 走保底=" + inst.chosenByDefault
                      + "（" + HOOK_STORY_END + "）");
        }

        private static void Story_OnTrigger(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] ★ 故事节点触发：" + inst.templateId
                      + " " + inst.instanceId + " cycle=" + inst.startCycle);
        }

        private static void Story_OnStart(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] 故事节点 onStart：" + inst.templateId
                      + " 持续=" + (inst.hasDuration ? inst.GetDurationRemaining().ToString("F1") + "s" : "无"));
        }

        private static void Story_OnEnd(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] 故事节点 onEnd：" + inst.templateId
                      + " 玩家选择数=" + inst.playerChoices.Count);
        }

        private static void Story_Option(EventInstance inst)
        {
            Debug.Log("[MoreFood/Event][Yaml] " + OPT_STORY_PICK + "：" + inst.templateId
                      + " 选项 " + inst.chosenIndex);
        }
    }
}
