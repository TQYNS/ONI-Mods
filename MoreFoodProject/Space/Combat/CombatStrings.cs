// CombatStrings.cs — Space 战斗实物物品的本地化字符串（规范化，仿 MoreFood 的 Strings.cs）


namespace MoreFood_KFC
{
    public static class SpaceModStrings
    {
        public class ITEMS
        {
            // 弹药（7 种，对应 AmmoItems.cs）
            public class AMMO
            {
                public static class AMMOHEBASIC
                {
                    public static LocString NAME = "宇宙内爆破弹";
                    public static LocString DESC = "最低档通用弹，几乎无穿甲。";
                }
                public static class AMMOCRUISE
                {
                    public static LocString NAME = "巡航侦察弹";
                    public static LocString DESC = "小型远程侦察弹，装填快。";
                }
                public static class AMMOCLUSTER
                {
                    public static LocString NAME = "集束导弹";
                    public static LocString DESC = "一发 6 颗，每颗独立算穿甲。";
                }
                public static class AMMOTHERMOBARIC
                {
                    public static LocString NAME = "温压弹";
                    public static LocString DESC = "中型高单发，对低装甲高 HP 目标强。";
                }
                public static class AMMOLASERGUIDED
                {
                    public static LocString NAME = "激光制导弹";
                    public static LocString DESC = "中型稳定高穿甲。";
                }
                public static class AMMOPENETRATOR
                {
                    public static LocString NAME = "钻地弹";
                    public static LocString DESC = "大型破甲弹，对装甲 2 倍伤害。";
                }
                public static class AMMOICBM
                {
                    public static LocString NAME = "洲际导弹";
                    public static LocString DESC = "大型远程决战弹。";
                }
            }

            // 模块实物（15 种，对应 ModuleItems.cs）
            public class MODULE
            {
                public static class MODULEITEMWEAPONSMALL
                {
                    public static LocString NAME = "小型导弹发射器";
                    public static LocString DESC = "小型导弹发射器，射速快、单发低。";
                }
                public static class MODULEITEMWEAPONMEDIUM
                {
                    public static LocString NAME = "中型导弹发射器";
                    public static LocString DESC = "中型导弹发射器，均衡。";
                }
                public static class MODULEITEMWEAPONLARGE
                {
                    public static LocString NAME = "大型导弹发射器";
                    public static LocString DESC = "大型导弹发射器，单发高、射速慢。";
                }
                public static class MODULEITEMMODULEPENETRATOR1
                {
                    public static LocString NAME = "穿甲弹头";
                    public static LocString DESC = "全火箭穿甲 +5。";
                }
                public static class MODULEITEMMODULEPENETRATOR2
                {
                    public static LocString NAME = "高爆穿甲弹头";
                    public static LocString DESC = "全火箭穿甲 +12，装甲伤害 ×110%。";
                }
                public static class MODULEITEMMODULEPENETRATOR3
                {
                    public static LocString NAME = "湮灭穿甲弹头";
                    public static LocString DESC = "全火箭穿甲 +25，装甲伤害 ×120%。";
                }
                public static class MODULEITEMMODULESPEED1
                {
                    public static LocString NAME = "化学推进器";
                    public static LocString DESC = "全火箭速度 ×110%。";
                }
                public static class MODULEITEMMODULESPEED2
                {
                    public static LocString NAME = "离子推进器";
                    public static LocString DESC = "全火箭速度 ×120%。";
                }
                public static class MODULEITEMMODULESPEED3
                {
                    public static LocString NAME = "曲率引擎";
                    public static LocString DESC = "全火箭速度 ×130%。";
                }
                public static class MODULEITEMMODULERADAR1
                {
                    public static LocString NAME = "雷达阵列";
                    public static LocString DESC = "子图视野 +1。";
                }
                public static class MODULEITEMMODULERADAR2
                {
                    public static LocString NAME = "相控阵雷达";
                    public static LocString DESC = "子图视野 +2，射程 +1。";
                }
                public static class MODULEITEMMODULERADAR3
                {
                    public static LocString NAME = "量子探测阵列";
                    public static LocString DESC = "子图视野 +3，射程 +2。";
                }
                public static class MODULEITEMMODULEREPAIR1
                {
                    public static LocString NAME = "维修无人机";
                    public static LocString DESC = "脱战缓慢修复 HP 与护盾。";
                }
                public static class MODULEITEMMODULEREPAIR2
                {
                    public static LocString NAME = "自修复涂层";
                    public static LocString DESC = "脱战修复 HP 与护盾，速度更快。";
                }
                public static class MODULEITEMMODULEREPAIR3
                {
                    public static LocString NAME = "纳米再生模块";
                    public static LocString DESC = "脱战快速修复 HP 与护盾。";
                }
            }
        }
    }
}
