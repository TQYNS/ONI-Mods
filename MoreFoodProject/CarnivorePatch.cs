using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Database;

namespace MoreFood_KFC
{
    // 让自定义食物计入「肉食」成就（EatkCalFromMeatByCycle100）。
    // 原理：成就白名单是 Database.ColonyAchievements 构造函数里写死的 List<string>，
    // 在构造完成后把它补长即可（判定与 UI 进度条都读同一个列表）。
    [HarmonyPatch(typeof(Database.ColonyAchievements),
                  MethodType.Constructor, new Type[] { typeof(ResourceSet) })]
    public static class Patch_Carnivore_ExtraFoods
    {
        // 要计入肉食成就的自定义食物 FoodInfo.Id（= 食物预制体 ID）。
        // 行尾带 ==[MEAT:ID]== 手术刀标记的行由代码生成器维护（勾选「计入肉食成就」时增、
        // 取消勾选时删、改名时重写）；没有标记的行是手写条目，生成器只读不删。
        // 手写条目照抄 "ID", 的写法即可，不要加标记。
        public static readonly List<string> ExtraMeatIds = new List<string>
        {
            "SauceBraisedRibs",   // ==[MEAT:SauceBraisedRibs]== 酱汁排骨
            "SteamedEggWithMeat",   // ==[MEAT:SteamedEggWithMeat]== 肉沫蒸蛋
            "GrilledMeatPlatter",   // ==[MEAT:GrilledMeatPlatter]== 烧烤拼盘
            "ChickenWithMushroom",   // ==[MEAT:ChickenWithMushroom]== 小鸡炖蘑菇
            "GrilledSquidSkewer",   // ==[MEAT:GrilledSquidSkewer]== 鱿鱼串
            "CannedMeat",   // ==[MEAT:CannedMeat]== 鲜肉罐头
            "CannedSeafood",   // ==[MEAT:CannedSeafood]== 海鲜罐头
            "CrabMeatBalls",   // ==[MEAT:CrabMeatBalls]== 蟹肉球
            "SteamedCrabBalls",   // ==[MEAT:SteamedCrabBalls]== 清蒸蟹球
            "StirFriedHairyCrab",   // ==[MEAT:StirFriedHairyCrab]== 爆炒大闸蟹
            "CrispyRoastChicken",   // ==[MEAT:CrispyRoastChicken]== 脆皮烤鸡
            "CrazyThursday",   // ==[MEAT:CrazyThursday]== 疯狂星期四
            "BeefBurger",   // ==[MEAT:BeefBurger]== 汉堡
            "BraisedPorkRiceBowl",   // ==[MEAT:BraisedPorkRiceBowl]== 红烧肉盖饭
            "DeerAntlerTonicSoup",   // ==[MEAT:DeerAntlerTonicSoup]== 鹿茸大补汤
            "BraisedBeefInSoySauce",   // ==[MEAT:BraisedBeefInSoySauce]== 酱牛肉
            "StewedBeef",   // ==[MEAT:StewedBeef]== 炖牛肉
            "TomahawkSteak",   // ==[MEAT:TomahawkSteak]== 战斧牛排
            "BraisedSoftshellTurtle",   // ==[MEAT:BraisedSoftshellTurtle]== 红烧甲鱼
            "SoftshellTurtleMeat",   // ==[MEAT:SoftshellTurtleMeat]== 甲鱼肉
            "BeefMeat",   // ==[MEAT:BeefMeat]== 牛肉
            "CrabClaw",   // ==[MEAT:CrabClaw]== 蟹钳
            "CrabLeg",   // ==[MEAT:CrabLeg]== 蟹腿
            "ElephantTrunk",   // ==[MEAT:ElephantTrunk]== 象鼻
            "Barnacle",   // ==[MEAT:Barnacle]== 藤壶
            "LargeHaunch",   // ==[MEAT:LargeHaunch]== 大肉
            "LongPork",   // ==[MEAT:LongPork]== 长猪肉
            "Drumstick",   // ==[MEAT:Drumstick]== 鸡腿
            "HaunchSoup",   // ==[MEAT:HaunchSoup]== 大肉汤
            "FigStuffedTrunk",   // ==[MEAT:FigStuffedTrunk]== 无花果酿象鼻
            "Perogies",   // ==[MEAT:Perogies]== 波兰水饺
            "SeafoodGumbo",   // ==[MEAT:SeafoodGumbo]== 海鲜杂烩
            "TurkeyDinner",   // ==[MEAT:TurkeyDinner]== 火鸡正餐
            "RoastedWings",   // ==[MEAT:RoastedWings]== 烤翅
            "BarnacleNoodles",   // ==[MEAT:BarnacleNoodles]== 藤壶细面
            "EelSushi",   // ==[MEAT:EelSushi]== 鳗鱼寿司
            "LobsterDinner",   // ==[MEAT:LobsterDinner]== 龙虾正餐
        };

        private static readonly AccessTools.FieldRef<EatXCaloriesFromY, List<string>> FromFoodTypeRef =
            AccessTools.FieldRefAccess<EatXCaloriesFromY, List<string>>("fromFoodType");

        // 补丁：原版成就定义初始化后，把本 mod 的肉食类物品追加进「肉食成就」的判定清单
        public static void Postfix(Database.ColonyAchievements __instance)
        {
            try
            {
                AddToRequirement(__instance.EatkCalFromMeatByCycle100, ExtraMeatIds);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:Patch_Carnivore_ExtraFoods", e); }
        }

        private static void AddToRequirement(ColonyAchievement achievement, List<string> extraIds)
        {
            if (achievement == null || achievement.requirementChecklist == null) return;

            foreach (ColonyAchievementRequirement req in achievement.requirementChecklist)
            {
                if (!(req is EatXCaloriesFromY eatReq)) continue;
                List<string> list = FromFoodTypeRef(eatReq);
                if (list == null) continue;
                foreach (string id in extraIds)
                    if (!string.IsNullOrEmpty(id) && !list.Contains(id))
                        list.Add(id);   // 幂等：Db 若被重建也不会重复加
            }
        }
    }
}
