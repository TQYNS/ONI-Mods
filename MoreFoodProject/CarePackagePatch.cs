using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace MoreFood_KFC
{

    // 1) 工厂：一个打印仓条目的统一构造入口
    //    - 把 Func<bool> 门槛的样板收在这里，注册表只写「要什么、多少个、几周期起」
    //    - cycleGate 缺省 1（原代码 CYCLE_GATE），想"开局就能刷"传 0

    public static class CarePackageFactory
    {
        // 造一条补给包裹注册项：数量 quantity，从第 cycleGate 个周期起可供应
        public static CarePackageInfo Make(string id, float quantity, int cycleGate = 1)
        {
            return new CarePackageInfo(id, quantity,
                () => GameClock.Instance.GetCycle() >= cycleGate);
        }
    }


    // 2) 注册表：要进打印仓的条目都写在这里
    //    —— 想加东西只改这一个 List，不用碰下面的 Patch
    //    —— id 必须是已注册的 prefab / 物品 ID（食物、蛋、生物幼崽都行）
    //    —— 数量为 float；门槛为周期数（委托每次刷新补给列表时重新求值）

    public static class CarePackageRegistry
    {
        // 要进打印仓的条目（食物 / 物品写自己的 ID，植物写 seedId —— 只有种子进补给）。
        // 行尾带 ==[CARE:ID]== 手术刀标记的行由代码生成器维护（勾选「补给」时增、
        // 取消勾选时删、改数量/周期/改名时重写那一行）；没有标记的行是手写条目，
        // 生成器只读不删。手写条目照抄 CarePackageFactory.Make("ID", 数量f, 周期)
        // 的写法即可，不要加标记。
        public static readonly List<CarePackageInfo> Entries = new List<CarePackageInfo>
        {
            CarePackageFactory.Make("SauceBraisedRibs", 3.0f, 10),   // ==[CARE:SauceBraisedRibs]== 酱汁排骨
            CarePackageFactory.Make("SteamedEggWithMeat", 3.0f, 10),   // ==[CARE:SteamedEggWithMeat]== 肉沫蒸蛋
            CarePackageFactory.Make("GrilledMeatPlatter", 3.0f, 10),   // ==[CARE:GrilledMeatPlatter]== 烧烤拼盘
            CarePackageFactory.Make("MixedVegetableStew", 3.0f, 10),   // ==[CARE:MixedVegetableStew]== 蔬菜杂烩
            CarePackageFactory.Make("ChickenWithMushroom", 2.0f, 10),   // ==[CARE:ChickenWithMushroom]== 小鸡炖蘑菇
            CarePackageFactory.Make("GrilledSquidSkewer", 3.0f, 10),   // ==[CARE:GrilledSquidSkewer]== 鱿鱼串
            CarePackageFactory.Make("CannedMeat", 5.0f, 10),   // ==[CARE:CannedMeat]== 鲜肉罐头
            CarePackageFactory.Make("CannedSeafood", 5.0f, 10),   // ==[CARE:CannedSeafood]== 海鲜罐头
            CarePackageFactory.Make("CannedGrain", 5.0f, 10),   // ==[CARE:CannedGrain]== 杂粮罐头
            CarePackageFactory.Make("CannedFruit", 5.0f, 10),   // ==[CARE:CannedFruit]== 水果罐头
            CarePackageFactory.Make("CookedRice", 3.0f, 10),   // ==[CARE:CookedRice]== 米饭
            CarePackageFactory.Make("Flour", 3.0f, 10),   // ==[CARE:Flour]== 面粉
            CarePackageFactory.Make("BoneSoup", 3.0f, 10),   // ==[CARE:BoneSoup]== 大骨汤
            CarePackageFactory.Make("CrabMeatBalls", 5.0f, 10),   // ==[CARE:CrabMeatBalls]== 蟹肉球
            CarePackageFactory.Make("SteamedCrabBalls", 3.0f, 10),   // ==[CARE:SteamedCrabBalls]== 清蒸蟹球
            CarePackageFactory.Make("StirFriedHairyCrab", 3.0f, 10),   // ==[CARE:StirFriedHairyCrab]== 爆炒大闸蟹
            CarePackageFactory.Make("CrispyRoastChicken", 3.0f, 10),   // ==[CARE:CrispyRoastChicken]== 脆皮烤鸡
            CarePackageFactory.Make("CrazyThursday", 1.0f, 30),   // ==[CARE:CrazyThursday]== 疯狂星期四
            CarePackageFactory.Make("BeefBurger", 2.0f, 1),   // ==[CARE:BeefBurger]== 汉堡
            CarePackageFactory.Make("BraisedPorkRiceBowl", 2.0f, 10),   // ==[CARE:BraisedPorkRiceBowl]== 红烧肉盖饭
            CarePackageFactory.Make("DeerAntlerTonicSoup", 3.0f, 30),   // ==[CARE:DeerAntlerTonicSoup]== 鹿茸大补汤
            CarePackageFactory.Make("BraisedBeefInSoySauce", 3.0f, 10),   // ==[CARE:BraisedBeefInSoySauce]== 酱牛肉
            CarePackageFactory.Make("StewedBeef", 3.0f, 30),   // ==[CARE:StewedBeef]== 炖牛肉
            CarePackageFactory.Make("TomahawkSteak", 3.0f, 30),   // ==[CARE:TomahawkSteak]== 战斧牛排
            CarePackageFactory.Make("BraisedSoftshellTurtle", 3.0f, 10),   // ==[CARE:BraisedSoftshellTurtle]== 红烧甲鱼
            CarePackageFactory.Make("Pudding", 5.0f, 1),   // ==[CARE:Pudding]== 香草奶油布丁
            CarePackageFactory.Make("ClassicCola", 3.0f, 10),   // ==[CARE:ClassicCola]== 可乐
            CarePackageFactory.Make("ZeroSugarCola", 3.0f, 10),   // ==[CARE:ZeroSugarCola]== 无糖可乐
            CarePackageFactory.Make("SoyMilk", 6.0f, 10),   // ==[CARE:SoyMilk]== 豆奶
            CarePackageFactory.Make("SoftshellTurtleMeat", 3.0f, 1),   // ==[CARE:SoftshellTurtleMeat]== 甲鱼肉
            CarePackageFactory.Make("DeerAntler", 3.0f, 30),   // ==[CARE:DeerAntler]== 鹿茸
            CarePackageFactory.Make("BeefMeat", 3.0f, 10),   // ==[CARE:BeefMeat]== 牛肉
            CarePackageFactory.Make("CrabClaw", 8.0f, 10),   // ==[CARE:CrabClaw]== 蟹钳
            CarePackageFactory.Make("CrabLeg", 6.0f, 10),   // ==[CARE:CrabLeg]== 蟹腿
            CarePackageFactory.Make("FishBone", 3.0f, 10),   // ==[CARE:FishBone]== 鱼骨
            CarePackageFactory.Make("LargeBone", 3.0f, 10),   // ==[CARE:LargeBone]== 大骨
            CarePackageFactory.Make("CuminPowder", 1.0f, 10),   // ==[CARE:CuminPowder]== 孜然
            CarePackageFactory.Make("SoySauce", 1.0f, 10),   // ==[CARE:SoySauce]== 酱油
            CarePackageFactory.Make("Pineapple", 8.0f, 1),   // ==[CARE:Pineapple]== 菠萝
            CarePackageFactory.Make("NapaCabbage", 8.0f, 1),   // ==[CARE:NapaCabbage]== 白菜
            CarePackageFactory.Make("SantosBean", 3.0f, 10),   // ==[CARE:SantosBean]== 桑托豆
            CarePackageFactory.Make("YirgacheffeBean", 3.0f, 10),   // ==[CARE:YirgacheffeBean]== 耶加豆
            CarePackageFactory.Make("WhiteWhaleVodka", 3.0f, 10),   // ==[CARE:WhiteWhaleVodka]== 白鲸伏特加
            CarePackageFactory.Make("RedWine", 3.0f, 10),   // ==[CARE:RedWine]== 葡萄酒
            CarePackageFactory.Make("Brandy", 3.0f, 10),   // ==[CARE:Brandy]== 白兰地
            CarePackageFactory.Make("PureWheatBeer", 3.0f, 10),   // ==[CARE:PureWheatBeer]== 纯麦啤酒
            CarePackageFactory.Make("GruitAle", 3.0f, 10),   // ==[CARE:GruitAle]== 格鲁特麦酒
            CarePackageFactory.Make("Rum", 3.0f, 10),   // ==[CARE:Rum]== 朗姆酒
            CarePackageFactory.Make("Moutai", 3.0f, 10),   // ==[CARE:Moutai]== 茅台酒
            CarePackageFactory.Make("RiceWine", 3.0f, 10),   // ==[CARE:RiceWine]== 米酒
            CarePackageFactory.Make("MunichBeer", 3.0f, 10),   // ==[CARE:MunichBeer]== 慕尼黑啤酒
            CarePackageFactory.Make("LimeCitrusWine", 3.0f, 10),   // ==[CARE:LimeCitrusWine]== 青柠柑橘酒
            CarePackageFactory.Make("BlueberrySparklingWine", 3.0f, 10),   // ==[CARE:BlueberrySparklingWine]== 蓝莓气泡酒
            CarePackageFactory.Make("IcedCoffee", 3.0f, 10),   // ==[CARE:IcedCoffee]== 冰咖啡
            CarePackageFactory.Make("Espresso", 3.0f, 10),   // ==[CARE:Espresso]== 浓缩咖啡
            CarePackageFactory.Make("RetroEspresso", 3.0f, 10),   // ==[CARE:RetroEspresso]== 复古浓缩咖啡
            CarePackageFactory.Make("CoffeeDoubleSkinJelly", 3.0f, 10),   // ==[CARE:CoffeeDoubleSkinJelly]== 咖啡双皮冻
            CarePackageFactory.Make("Mocha", 3.0f, 10),   // ==[CARE:Mocha]== 摩卡
            CarePackageFactory.Make("PineappleBeer", 3.0f, 10),   // ==[CARE:PineappleBeer]== 菠萝啤
            CarePackageFactory.Make("OrangeSoda", 3.0f, 10),   // ==[CARE:OrangeSoda]== 橙味汽水
            CarePackageFactory.Make("BlackcurrantSoda", 3.0f, 10),   // ==[CARE:BlackcurrantSoda]== 黑加仑汽水
            CarePackageFactory.Make("BlueberrySoda", 3.0f, 10),   // ==[CARE:BlueberrySoda]== 蓝莓汽水
            CarePackageFactory.Make("WatermelonSoda", 3.0f, 10),   // ==[CARE:WatermelonSoda]== 西瓜汽水
            CarePackageFactory.Make("XiMeiSoda", 3.0f, 10),   // ==[CARE:XiMeiSoda]== 西莓汽水
            CarePackageFactory.Make("MangoSoda", 3.0f, 10),   // ==[CARE:MangoSoda]== 芒果汽水
            CarePackageFactory.Make("Orange", 3.0f, 10),   // ==[CARE:Orange]== 橙子
            CarePackageFactory.Make("BlackCurrant", 3.0f, 10),   // ==[CARE:BlackCurrant]== 黑加仑
            CarePackageFactory.Make("Blueberry", 3.0f, 10),   // ==[CARE:Blueberry]== 蓝莓
            CarePackageFactory.Make("Grape", 3.0f, 10),   // ==[CARE:Grape]== 葡萄
            CarePackageFactory.Make("Watermelon", 3.0f, 10),   // ==[CARE:Watermelon]== 西瓜
            CarePackageFactory.Make("Prune", 3.0f, 10),   // ==[CARE:Prune]== 西梅
            CarePackageFactory.Make("Koji", 3.0f, 10),   // ==[CARE:Koji]== 酒曲
            CarePackageFactory.Make("FreshMilk", 3.0f, 10),   // ==[CARE:FreshMilk]== 鲜奶
            CarePackageFactory.Make("Chocolate", 3.0f, 10),   // ==[CARE:Chocolate]== 巧克力
            CarePackageFactory.Make("Mango", 3.0f, 10),   // ==[CARE:Mango]== 芒果
            CarePackageFactory.Make("Potato", 12.0f, 1),   // ==[CARE:Potato]== 土豆
            CarePackageFactory.Make("Onion", 12.0f, 1),   // ==[CARE:Onion]== 洋葱
            CarePackageFactory.Make("Eggplant", 12.0f, 1),   // ==[CARE:Eggplant]== 茄子
            CarePackageFactory.Make("Banana", 10.0f, 1),   // ==[CARE:Banana]== 香蕉
            CarePackageFactory.Make("Fig", 11.0f, 1),   // ==[CARE:Fig]== 无花果
            CarePackageFactory.Make("DragonFruit", 12.0f, 1),   // ==[CARE:DragonFruit]== 火龙果
            CarePackageFactory.Make("Tomato", 12.0f, 1),   // ==[CARE:Tomato]== 西红柿
            CarePackageFactory.Make("Honey", 8.0f, 10),   // ==[CARE:Honey]== 蜂蜜
            CarePackageFactory.Make("Butter", 6.0f, 10),   // ==[CARE:Butter]== 黄油
            CarePackageFactory.Make("ElephantTrunk", 6.0f, 10),   // ==[CARE:ElephantTrunk]== 象鼻
            CarePackageFactory.Make("Barnacle", 8.0f, 10),   // ==[CARE:Barnacle]== 藤壶
            CarePackageFactory.Make("LargeHaunch", 4.0f, 10),   // ==[CARE:LargeHaunch]== 大肉
            CarePackageFactory.Make("LongPork", 5.0f, 10),   // ==[CARE:LongPork]== 长猪肉
            CarePackageFactory.Make("Drumstick", 6.0f, 10),   // ==[CARE:Drumstick]== 鸡腿
            CarePackageFactory.Make("FrozenBananaDaiquiri", 4.0f, 10),   // ==[CARE:FrozenBananaDaiquiri]== 冰香蕉冻唇蜜
            CarePackageFactory.Make("PumpkinCake", 3.0f, 10),   // ==[CARE:PumpkinCake]== 南瓜饼
            CarePackageFactory.Make("HaunchSoup", 3.0f, 10),   // ==[CARE:HaunchSoup]== 大肉汤
            CarePackageFactory.Make("CreamyMashedPotato", 3.0f, 10),   // ==[CARE:CreamyMashedPotato]== 奶油土豆泥
            CarePackageFactory.Make("FigStuffedTrunk", 2.0f, 30),   // ==[CARE:FigStuffedTrunk]== 无花果酿象鼻
            CarePackageFactory.Make("Perogies", 3.0f, 10),   // ==[CARE:Perogies]== 波兰水饺
            CarePackageFactory.Make("SeafoodGumbo", 2.0f, 10),   // ==[CARE:SeafoodGumbo]== 海鲜杂烩
            CarePackageFactory.Make("TurkeyDinner", 2.0f, 30),   // ==[CARE:TurkeyDinner]== 火鸡正餐
            CarePackageFactory.Make("RoastedWings", 3.0f, 10),   // ==[CARE:RoastedWings]== 烤翅
            CarePackageFactory.Make("PotatoTornado", 3.0f, 10),   // ==[CARE:PotatoTornado]== 花式回旋块茎
            CarePackageFactory.Make("FluffyPotatoSouffle", 3.0f, 10),   // ==[CARE:FluffyPotatoSouffle]== 蓬松土豆蛋奶酥
            CarePackageFactory.Make("VegStinger", 4.0f, 10),   // ==[CARE:VegStinger]== 蔬菜鸡尾酒
            CarePackageFactory.Make("BarnacleNoodles", 3.0f, 10),   // ==[CARE:BarnacleNoodles]== 藤壶细面
            CarePackageFactory.Make("WatermelonPopsicle", 4.0f, 10),   // ==[CARE:WatermelonPopsicle]== 西瓜冰棍
            CarePackageFactory.Make("BananaJelly", 4.0f, 10),   // ==[CARE:BananaJelly]== 香蕉冻
            CarePackageFactory.Make("EelSushi", 2.0f, 10),   // ==[CARE:EelSushi]== 鳗鱼寿司
            CarePackageFactory.Make("LobsterDinner", 2.0f, 30),   // ==[CARE:LobsterDinner]== 龙虾正餐


            // CarePackageFactory.Make("food3", 4f, 50),   // 第 50 周期起 ×4
        };
    }


    // 3) 唯一的 Harmony 补丁：把注册表整个塞进 Immigration.carePackages
    //    - carePackages 是私有字段，用反射拿
    // - 按 id 判重：ConfigureCarePackages 会被多个 Mod 的补丁各调一次，必须幂等
    //      同一个 id 只留一条（先到先得），避免列表里堆重复项

    [HarmonyPatch(typeof(Immigration), "ConfigureCarePackages")]
    public static class Patch_CarePackage_Registry
    {
        static void Postfix(Immigration __instance)
        {
            try
            {
                var list = (List<CarePackageInfo>)typeof(Immigration)
                    .GetField("carePackages",
                        System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(__instance);
                if (list == null) return;

                foreach (var entry in CarePackageRegistry.Entries)
                    if (!list.Exists(x => x.id == entry.id))
                        list.Add(entry);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:Patch_CarePackage_Registry", e); }
        }
    }
}
