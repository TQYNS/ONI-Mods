// LanguageHelper.cs — 显示名多语言（po 覆盖不到静态化的显示名 → patch 查表）
// 参考 shark 项目 LanguageHelper.cs 的实测方案（中/韩/俄切换正常）

using HarmonyLib;

namespace MoreFood_KFC{
    public static class vs_presetNameHelper
    {
        // prefabId → po key（命名空间.树路径）映射
        internal static string GetDisplayName(string prefabId)
        {
            string key;
            switch (prefabId)
            {

                // ==[ZONE:CASES]BEGIN==
// ==[ITEM:SauceBraisedRibs:CASE]BEGIN==
                case "SauceBraisedRibs": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.SAUCEBRAISEDRIBS.NAME"; break;
// ==[ITEM:SauceBraisedRibs:CASE]END==
// ==[ITEM:SteamedEggWithMeat:CASE]BEGIN==
                case "SteamedEggWithMeat": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.STEAMEDEGGWITHMEAT.NAME"; break;
// ==[ITEM:SteamedEggWithMeat:CASE]END==
// ==[ITEM:GrilledMeatPlatter:CASE]BEGIN==
                case "GrilledMeatPlatter": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.GRILLEDMEATPLATTER.NAME"; break;
// ==[ITEM:GrilledMeatPlatter:CASE]END==
// ==[ITEM:MixedVegetableStew:CASE]BEGIN==
                case "MixedVegetableStew": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.MIXEDVEGETABLESTEW.NAME"; break;
// ==[ITEM:MixedVegetableStew:CASE]END==
// ==[ITEM:ChickenWithMushroom:CASE]BEGIN==
                case "ChickenWithMushroom": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CHICKENWITHMUSHROOM.NAME"; break;
// ==[ITEM:ChickenWithMushroom:CASE]END==
// ==[ITEM:GrilledSquidSkewer:CASE]BEGIN==
                case "GrilledSquidSkewer": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.GRILLEDSQUIDSKEWER.NAME"; break;
// ==[ITEM:GrilledSquidSkewer:CASE]END==
// ==[ITEM:CannedMeat:CASE]BEGIN==
                case "CannedMeat": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CANNEDMEAT.NAME"; break;
// ==[ITEM:CannedMeat:CASE]END==
// ==[ITEM:CannedSeafood:CASE]BEGIN==
                case "CannedSeafood": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CANNEDSEAFOOD.NAME"; break;
// ==[ITEM:CannedSeafood:CASE]END==
// ==[ITEM:CannedGrain:CASE]BEGIN==
                case "CannedGrain": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CANNEDGRAIN.NAME"; break;
// ==[ITEM:CannedGrain:CASE]END==
// ==[ITEM:CannedFruit:CASE]BEGIN==
                case "CannedFruit": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CANNEDFRUIT.NAME"; break;
// ==[ITEM:CannedFruit:CASE]END==
// ==[ITEM:CookedRice:CASE]BEGIN==
                case "CookedRice": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.COOKEDRICE.NAME"; break;
// ==[ITEM:CookedRice:CASE]END==
// ==[ITEM:Flour:CASE]BEGIN==
                case "Flour": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FLOUR.NAME"; break;
// ==[ITEM:Flour:CASE]END==
// ==[ITEM:BoneSoup:CASE]BEGIN==
                case "BoneSoup": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BONESOUP.NAME"; break;
// ==[ITEM:BoneSoup:CASE]END==
// ==[ITEM:CrabMeatBalls:CASE]BEGIN==
                case "CrabMeatBalls": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CRABMEATBALLS.NAME"; break;
// ==[ITEM:CrabMeatBalls:CASE]END==
// ==[ITEM:SteamedCrabBalls:CASE]BEGIN==
                case "SteamedCrabBalls": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.STEAMEDCRABBALLS.NAME"; break;
// ==[ITEM:SteamedCrabBalls:CASE]END==
// ==[ITEM:StirFriedHairyCrab:CASE]BEGIN==
                case "StirFriedHairyCrab": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.STIRFRIEDHAIRYCRAB.NAME"; break;
// ==[ITEM:StirFriedHairyCrab:CASE]END==
// ==[ITEM:CrispyRoastChicken:CASE]BEGIN==
                case "CrispyRoastChicken": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CRISPYROASTCHICKEN.NAME"; break;
// ==[ITEM:CrispyRoastChicken:CASE]END==
// ==[ITEM:CrazyThursday:CASE]BEGIN==
                case "CrazyThursday": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CRAZYTHURSDAY.NAME"; break;
// ==[ITEM:CrazyThursday:CASE]END==
// ==[ITEM:BeefBurger:CASE]BEGIN==
                case "BeefBurger": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BEEFBURGER.NAME"; break;
// ==[ITEM:BeefBurger:CASE]END==
// ==[ITEM:BraisedPorkRiceBowl:CASE]BEGIN==
                case "BraisedPorkRiceBowl": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BRAISEDPORKRICEBOWL.NAME"; break;
// ==[ITEM:BraisedPorkRiceBowl:CASE]END==
// ==[ITEM:DeerAntlerTonicSoup:CASE]BEGIN==
                case "DeerAntlerTonicSoup": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.DEERANTLERTONICSOUP.NAME"; break;
// ==[ITEM:DeerAntlerTonicSoup:CASE]END==
// ==[ITEM:BraisedBeefInSoySauce:CASE]BEGIN==
                case "BraisedBeefInSoySauce": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BRAISEDBEEFINSOYSAUCE.NAME"; break;
// ==[ITEM:BraisedBeefInSoySauce:CASE]END==
// ==[ITEM:StewedBeef:CASE]BEGIN==
                case "StewedBeef": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.STEWEDBEEF.NAME"; break;
// ==[ITEM:StewedBeef:CASE]END==
// ==[ITEM:TomahawkSteak:CASE]BEGIN==
                case "TomahawkSteak": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.TOMAHAWKSTEAK.NAME"; break;
// ==[ITEM:TomahawkSteak:CASE]END==
// ==[ITEM:BraisedSoftshellTurtle:CASE]BEGIN==
                case "BraisedSoftshellTurtle": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BRAISEDSOFTSHELLTURTLE.NAME"; break;
// ==[ITEM:BraisedSoftshellTurtle:CASE]END==
// ==[ITEM:Pudding:CASE]BEGIN==
                case "Pudding": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PUDDING.NAME"; break;
// ==[ITEM:Pudding:CASE]END==
// ==[ITEM:ClassicCola:CASE]BEGIN==
                case "ClassicCola": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CLASSICCOLA.NAME"; break;
// ==[ITEM:ClassicCola:CASE]END==
// ==[ITEM:ZeroSugarCola:CASE]BEGIN==
                case "ZeroSugarCola": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ZEROSUGARCOLA.NAME"; break;
// ==[ITEM:ZeroSugarCola:CASE]END==
// ==[ITEM:SoyMilk:CASE]BEGIN==
                case "SoyMilk": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.SOYMILK.NAME"; break;
// ==[ITEM:SoyMilk:CASE]END==
// ==[ITEM:SoftshellTurtleMeat:CASE]BEGIN==
                case "SoftshellTurtleMeat": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.SOFTSHELLTURTLEMEAT.NAME"; break;
// ==[ITEM:SoftshellTurtleMeat:CASE]END==
// ==[ITEM:DeerAntler:CASE]BEGIN==
                case "DeerAntler": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.DEERANTLER.NAME"; break;
// ==[ITEM:DeerAntler:CASE]END==
// ==[ITEM:BeefMeat:CASE]BEGIN==
                case "BeefMeat": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BEEFMEAT.NAME"; break;
// ==[ITEM:BeefMeat:CASE]END==
// ==[ITEM:CrabClaw:CASE]BEGIN==
                case "CrabClaw": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CRABCLAW.NAME"; break;
// ==[ITEM:CrabClaw:CASE]END==
// ==[ITEM:CrabLeg:CASE]BEGIN==
                case "CrabLeg": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CRABLEG.NAME"; break;
// ==[ITEM:CrabLeg:CASE]END==
// ==[ITEM:FishBone:CASE]BEGIN==
                case "FishBone": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FISHBONE.NAME"; break;
// ==[ITEM:FishBone:CASE]END==
// ==[ITEM:LargeBone:CASE]BEGIN==
                case "LargeBone": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.LARGEBONE.NAME"; break;
// ==[ITEM:LargeBone:CASE]END==
// ==[ITEM:CuminPowder:CASE]BEGIN==
                case "CuminPowder": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CUMINPOWDER.NAME"; break;
// ==[ITEM:CuminPowder:CASE]END==
// ==[ITEM:SoySauce:CASE]BEGIN==
                case "SoySauce": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.SOYSAUCE.NAME"; break;
// ==[ITEM:SoySauce:CASE]END==
// ==[ITEM:Pineapple:CASE]BEGIN==
                case "Pineapple": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PINEAPPLE.NAME"; break;
// ==[ITEM:Pineapple:CASE]END==
// ==[ITEM:NapaCabbage:CASE]BEGIN==
                case "NapaCabbage": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.NAPACABBAGE.NAME"; break;
// ==[ITEM:NapaCabbage:CASE]END==
// ==[ITEM:SantosBean:CASE]BEGIN==
                case "SantosBean": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.SANTOSBEAN.NAME"; break;
// ==[ITEM:SantosBean:CASE]END==
// ==[ITEM:YirgacheffeBean:CASE]BEGIN==
                case "YirgacheffeBean": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.YIRGACHEFFEBEAN.NAME"; break;
// ==[ITEM:YirgacheffeBean:CASE]END==
// ==[ITEM:WhiteWhaleVodka:CASE]BEGIN==
                case "WhiteWhaleVodka": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.WHITEWHALEVODKA.NAME"; break;
// ==[ITEM:WhiteWhaleVodka:CASE]END==
// ==[ITEM:RedWine:CASE]BEGIN==
                case "RedWine": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.REDWINE.NAME"; break;
// ==[ITEM:RedWine:CASE]END==
// ==[ITEM:Brandy:CASE]BEGIN==
                case "Brandy": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BRANDY.NAME"; break;
// ==[ITEM:Brandy:CASE]END==
// ==[ITEM:PureWheatBeer:CASE]BEGIN==
                case "PureWheatBeer": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PUREWHEATBEER.NAME"; break;
// ==[ITEM:PureWheatBeer:CASE]END==
// ==[ITEM:GruitAle:CASE]BEGIN==
                case "GruitAle": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.GRUITALE.NAME"; break;
// ==[ITEM:GruitAle:CASE]END==
// ==[ITEM:Rum:CASE]BEGIN==
                case "Rum": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.RUM.NAME"; break;
// ==[ITEM:Rum:CASE]END==
// ==[ITEM:Moutai:CASE]BEGIN==
                case "Moutai": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.MOUTAI.NAME"; break;
// ==[ITEM:Moutai:CASE]END==
// ==[ITEM:RiceWine:CASE]BEGIN==
                case "RiceWine": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.RICEWINE.NAME"; break;
// ==[ITEM:RiceWine:CASE]END==
// ==[ITEM:MunichBeer:CASE]BEGIN==
                case "MunichBeer": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.MUNICHBEER.NAME"; break;
// ==[ITEM:MunichBeer:CASE]END==
// ==[ITEM:LimeCitrusWine:CASE]BEGIN==
                case "LimeCitrusWine": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.LIMECITRUSWINE.NAME"; break;
// ==[ITEM:LimeCitrusWine:CASE]END==
// ==[ITEM:BlueberrySparklingWine:CASE]BEGIN==
                case "BlueberrySparklingWine": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BLUEBERRYSPARKLINGWINE.NAME"; break;
// ==[ITEM:BlueberrySparklingWine:CASE]END==
// ==[ITEM:IcedCoffee:CASE]BEGIN==
                case "IcedCoffee": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ICEDCOFFEE.NAME"; break;
// ==[ITEM:IcedCoffee:CASE]END==
// ==[ITEM:Espresso:CASE]BEGIN==
                case "Espresso": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ESPRESSO.NAME"; break;
// ==[ITEM:Espresso:CASE]END==
// ==[ITEM:RetroEspresso:CASE]BEGIN==
                case "RetroEspresso": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.RETROESPRESSO.NAME"; break;
// ==[ITEM:RetroEspresso:CASE]END==
// ==[ITEM:CoffeeDoubleSkinJelly:CASE]BEGIN==
                case "CoffeeDoubleSkinJelly": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.COFFEEDOUBLESKINJELLY.NAME"; break;
// ==[ITEM:CoffeeDoubleSkinJelly:CASE]END==
// ==[ITEM:Mocha:CASE]BEGIN==
                case "Mocha": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.MOCHA.NAME"; break;
// ==[ITEM:Mocha:CASE]END==
// ==[ITEM:PineappleBeer:CASE]BEGIN==
                case "PineappleBeer": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PINEAPPLEBEER.NAME"; break;
// ==[ITEM:PineappleBeer:CASE]END==
// ==[ITEM:OrangeSoda:CASE]BEGIN==
                case "OrangeSoda": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ORANGESODA.NAME"; break;
// ==[ITEM:OrangeSoda:CASE]END==
// ==[ITEM:BlackcurrantSoda:CASE]BEGIN==
                case "BlackcurrantSoda": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BLACKCURRANTSODA.NAME"; break;
// ==[ITEM:BlackcurrantSoda:CASE]END==
// ==[ITEM:BlueberrySoda:CASE]BEGIN==
                case "BlueberrySoda": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BLUEBERRYSODA.NAME"; break;
// ==[ITEM:BlueberrySoda:CASE]END==
// ==[ITEM:WatermelonSoda:CASE]BEGIN==
                case "WatermelonSoda": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.WATERMELONSODA.NAME"; break;
// ==[ITEM:WatermelonSoda:CASE]END==
// ==[ITEM:XiMeiSoda:CASE]BEGIN==
                case "XiMeiSoda": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.XIMEISODA.NAME"; break;
// ==[ITEM:XiMeiSoda:CASE]END==
// ==[ITEM:MangoSoda:CASE]BEGIN==
                case "MangoSoda": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.MANGOSODA.NAME"; break;
// ==[ITEM:MangoSoda:CASE]END==
// ==[ITEM:Orange:CASE]BEGIN==
                case "Orange": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ORANGE.NAME"; break;
// ==[ITEM:Orange:CASE]END==
// ==[ITEM:BlackCurrant:CASE]BEGIN==
                case "BlackCurrant": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BLACKCURRANT.NAME"; break;
// ==[ITEM:BlackCurrant:CASE]END==
// ==[ITEM:Blueberry:CASE]BEGIN==
                case "Blueberry": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BLUEBERRY.NAME"; break;
// ==[ITEM:Blueberry:CASE]END==
// ==[ITEM:Grape:CASE]BEGIN==
                case "Grape": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.GRAPE.NAME"; break;
// ==[ITEM:Grape:CASE]END==
// ==[ITEM:Watermelon:CASE]BEGIN==
                case "Watermelon": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.WATERMELON.NAME"; break;
// ==[ITEM:Watermelon:CASE]END==
// ==[ITEM:Prune:CASE]BEGIN==
                case "Prune": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PRUNE.NAME"; break;
// ==[ITEM:Prune:CASE]END==
// ==[ITEM:Koji:CASE]BEGIN==
                case "Koji": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.KOJI.NAME"; break;
// ==[ITEM:Koji:CASE]END==
// ==[ITEM:FreshMilk:CASE]BEGIN==
                case "FreshMilk": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FRESHMILK.NAME"; break;
// ==[ITEM:FreshMilk:CASE]END==
// ==[ITEM:Chocolate:CASE]BEGIN==
                case "Chocolate": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CHOCOLATE.NAME"; break;
// ==[ITEM:Chocolate:CASE]END==
// ==[ITEM:Mango:CASE]BEGIN==
                case "Mango": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.MANGO.NAME"; break;
// ==[ITEM:Mango:CASE]END==
// ==[ITEM:Fuel:CASE]BEGIN==
                case "Fuel": key = "MoreFood_KFC.ModStrings.OBJECT.FUEL.NAME"; break;
// ==[ITEM:Fuel:CASE]END==
// ==[ITEM:StarMapIntel:CASE]BEGIN==
                case "StarMapIntel": key = "MoreFood_KFC.ModStrings.OBJECT.STARMAPINTEL.NAME"; break;
// ==[ITEM:StarMapIntel:CASE]END==
// ==[ITEM:HonorPoint:CASE]BEGIN==
                case "HonorPoint": key = "MoreFood_KFC.ModStrings.OBJECT.HONORPOINT.NAME"; break;
// ==[ITEM:HonorPoint:CASE]END==
// ==[ITEM:FuelCell:CASE]BEGIN==
                case "FuelCell": key = "MoreFood_KFC.ModStrings.OBJECT.FUELCELL.NAME"; break;
// ==[ITEM:FuelCell:CASE]END==
// ==[ITEM:Potato:CASE]BEGIN==
                case "Potato": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.POTATO.NAME"; break;
// ==[ITEM:Potato:CASE]END==
// ==[ITEM:Onion:CASE]BEGIN==
                case "Onion": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ONION.NAME"; break;
// ==[ITEM:Onion:CASE]END==
// ==[ITEM:Eggplant:CASE]BEGIN==
                case "Eggplant": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.EGGPLANT.NAME"; break;
// ==[ITEM:Eggplant:CASE]END==
// ==[ITEM:Banana:CASE]BEGIN==
                case "Banana": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BANANA.NAME"; break;
// ==[ITEM:Banana:CASE]END==
// ==[ITEM:Fig:CASE]BEGIN==
                case "Fig": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FIG.NAME"; break;
// ==[ITEM:Fig:CASE]END==
// ==[ITEM:DragonFruit:CASE]BEGIN==
                case "DragonFruit": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.DRAGONFRUIT.NAME"; break;
// ==[ITEM:DragonFruit:CASE]END==
// ==[ITEM:Tomato:CASE]BEGIN==
                case "Tomato": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.TOMATO.NAME"; break;
// ==[ITEM:Tomato:CASE]END==
// ==[ITEM:Honey:CASE]BEGIN==
                case "Honey": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.HONEY.NAME"; break;
// ==[ITEM:Honey:CASE]END==
// ==[ITEM:Butter:CASE]BEGIN==
                case "Butter": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BUTTER.NAME"; break;
// ==[ITEM:Butter:CASE]END==
// ==[ITEM:ElephantTrunk:CASE]BEGIN==
                case "ElephantTrunk": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ELEPHANTTRUNK.NAME"; break;
// ==[ITEM:ElephantTrunk:CASE]END==
// ==[ITEM:Barnacle:CASE]BEGIN==
                case "Barnacle": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BARNACLE.NAME"; break;
// ==[ITEM:Barnacle:CASE]END==
// ==[ITEM:LargeHaunch:CASE]BEGIN==
                case "LargeHaunch": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.LARGEHAUNCH.NAME"; break;
// ==[ITEM:LargeHaunch:CASE]END==
// ==[ITEM:LongPork:CASE]BEGIN==
                case "LongPork": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.LONGPORK.NAME"; break;
// ==[ITEM:LongPork:CASE]END==
// ==[ITEM:Drumstick:CASE]BEGIN==
                case "Drumstick": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.DRUMSTICK.NAME"; break;
// ==[ITEM:Drumstick:CASE]END==
// ==[ITEM:FrozenBananaDaiquiri:CASE]BEGIN==
                case "FrozenBananaDaiquiri": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FROZENBANANADAIQUIRI.NAME"; break;
// ==[ITEM:FrozenBananaDaiquiri:CASE]END==
// ==[ITEM:PumpkinCake:CASE]BEGIN==
                case "PumpkinCake": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PUMPKINCAKE.NAME"; break;
// ==[ITEM:PumpkinCake:CASE]END==
// ==[ITEM:HaunchSoup:CASE]BEGIN==
                case "HaunchSoup": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.HAUNCHSOUP.NAME"; break;
// ==[ITEM:HaunchSoup:CASE]END==
// ==[ITEM:CreamyMashedPotato:CASE]BEGIN==
                case "CreamyMashedPotato": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.CREAMYMASHEDPOTATO.NAME"; break;
// ==[ITEM:CreamyMashedPotato:CASE]END==
// ==[ITEM:FigStuffedTrunk:CASE]BEGIN==
                case "FigStuffedTrunk": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FIGSTUFFEDTRUNK.NAME"; break;
// ==[ITEM:FigStuffedTrunk:CASE]END==
// ==[ITEM:Perogies:CASE]BEGIN==
                case "Perogies": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.PEROGIES.NAME"; break;
// ==[ITEM:Perogies:CASE]END==
// ==[ITEM:SeafoodGumbo:CASE]BEGIN==
                case "SeafoodGumbo": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.SEAFOODGUMBO.NAME"; break;
// ==[ITEM:SeafoodGumbo:CASE]END==
// ==[ITEM:TurkeyDinner:CASE]BEGIN==
                case "TurkeyDinner": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.TURKEYDINNER.NAME"; break;
// ==[ITEM:TurkeyDinner:CASE]END==
// ==[ITEM:RoastedWings:CASE]BEGIN==
                case "RoastedWings": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.ROASTEDWINGS.NAME"; break;
// ==[ITEM:RoastedWings:CASE]END==
// ==[ITEM:PotatoTornado:CASE]BEGIN==
                case "PotatoTornado": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.POTATOTORNADO.NAME"; break;
// ==[ITEM:PotatoTornado:CASE]END==
// ==[ITEM:FluffyPotatoSouffle:CASE]BEGIN==
                case "FluffyPotatoSouffle": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.FLUFFYPOTATOSOUFFLE.NAME"; break;
// ==[ITEM:FluffyPotatoSouffle:CASE]END==
// ==[ITEM:VegStinger:CASE]BEGIN==
                case "VegStinger": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.VEGSTINGER.NAME"; break;
// ==[ITEM:VegStinger:CASE]END==
// ==[ITEM:BarnacleNoodles:CASE]BEGIN==
                case "BarnacleNoodles": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BARNACLENOODLES.NAME"; break;
// ==[ITEM:BarnacleNoodles:CASE]END==
// ==[ITEM:WatermelonPopsicle:CASE]BEGIN==
                case "WatermelonPopsicle": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.WATERMELONPOPSICLE.NAME"; break;
// ==[ITEM:WatermelonPopsicle:CASE]END==
// ==[ITEM:BananaJelly:CASE]BEGIN==
                case "BananaJelly": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.BANANAJELLY.NAME"; break;
// ==[ITEM:BananaJelly:CASE]END==
// ==[ITEM:EelSushi:CASE]BEGIN==
                case "EelSushi": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.EELSUSHI.NAME"; break;
// ==[ITEM:EelSushi:CASE]END==
// ==[ITEM:LobsterDinner:CASE]BEGIN==
                case "LobsterDinner": key = "MoreFood_KFC.ModStrings.ITEMS.FOOD.LOBSTERDINNER.NAME"; break;
// ==[ITEM:LobsterDinner:CASE]END==
                // ==[ZONE:CASES]END==

                default: return null;
            }
            
            string s = Strings.Get(key);

            // Debug.Log($"[vs_preset] 尝试 key='{key}', 实际查到了 s='{s}'");

            if (s == null || s.StartsWith("MISSING")) return null;
            return s;
        }
    }

    // 选中信息卡 / 头顶显示名
    [HarmonyPatch(typeof(KSelectable), "GetName")]
    public static class vs_presetDisplayNamePatch
    {
        static void Postfix(KSelectable __instance, ref string __result)
        {
            try
            {
                var kp = __instance != null ? __instance.GetComponent<KPrefabID>() : null;
                if (kp == null) return;
                string name = vs_presetNameHelper.GetDisplayName(kp.PrefabTag.Name);
                if (name != null) __result = name;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:vs_presetDisplayNamePatch", e); }
        }
    }

    // 标签 / 提示（TagManager.ProperNames → 回退 tag.Name）
    [HarmonyPatch(typeof(TagManager), "GetProperName")]
    public static class vs_presetProperNamePatch
    {
        static void Postfix(Tag tag, ref string __result)
        {
            try
            {
                string name = vs_presetNameHelper.GetDisplayName(tag.Name);
                if (name != null) __result = name;
            }
            catch (System.Exception e) { MFLog.HotFail("patch:vs_presetProperNamePatch", e); }
        }
    }
}
