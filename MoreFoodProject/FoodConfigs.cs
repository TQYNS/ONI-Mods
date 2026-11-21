// FoodConfigs.cs — 食物批量注册（IMultiEntityConfig）
using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace MoreFood_KFC{
    public class FoodConfigs : IMultiEntityConfig
    {
        private struct Def
        {
            // 食物物品 ID（prefab 名与存档键都用它）
            public string id;
            // 物品动画名（kanim）
            public string anim;
            // 显示名（ModStrings 取）
            public string name;
            // 物品描述文本
            public string desc;
            // 饮食数值（卡路里/品质/腐坏时间等，EdiblesManager.FoodInfo）
            public EdiblesManager.FoodInfo foodInfo;
            // 单份质量（kg）
            public float mass;
            // 碰撞箱宽（格）
            public float width;
            // 碰撞箱高（格）
            public float height;
            // 构成元素（SimHashes；Creature = 无元素属性）
            public SimHashes element;
            // 渲染图层（Grid.SceneLayer）
            public Grid.SceneLayer sceneLayer;
            // 建造/物品菜单里的排序权重
            public int sortOrder;
            // 额外附加 Tag
            public List<Tag> tags;
            // 仅在列出的 DLC 下出现（null = 不限）
            public string[] requireDlc;
            // 列出的 DLC 下不出现（null = 不限）
            public string[] forbidDlc;
        }

        private static Def Make(
            string id,
            string anim,
            string name,
            string desc,
            EdiblesManager.FoodInfo foodInfo,
            float mass = 1f,
            float width = 0.8f,
            float height = 0.4f,
            SimHashes element = SimHashes.Creature,
            Grid.SceneLayer sceneLayer = Grid.SceneLayer.Front,
            int sortOrder = 0,
            List<Tag> tags = null,
            string[] requireDlc = null,
            string[] forbidDlc = null)
        {
            return new Def
            {
                id = id,
                anim = anim,
                name = name,
                desc = desc,
                foodInfo = foodInfo,
                mass = mass,
                width = width,
                height = height,
                element = element,
                sceneLayer = sceneLayer,
                sortOrder = sortOrder,
                tags = tags,
                requireDlc = requireDlc,
                forbidDlc = forbidDlc,
            };
        }

        private static readonly Def[] _defs =
        {
            // ==[ZONE:DEFS]BEGIN==
// ==[ITEM:SauceBraisedRibs:DEF]BEGIN==
            Make(
                id: "SauceBraisedRibs",
                anim: "saucebraisedribs_kanim",
                name: ModStrings.ITEMS.FOOD.SAUCEBRAISEDRIBS.NAME,
                desc: ModStrings.ITEMS.FOOD.SAUCEBRAISEDRIBS.DESC,
                foodInfo: ModFoods.SAUCEBRAISEDRIBS,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SauceBraisedRibs:DEF]END==
// ==[ITEM:SteamedEggWithMeat:DEF]BEGIN==
            Make(
                id: "SteamedEggWithMeat",
                anim: "steamedeggwithmeat_kanim",
                name: ModStrings.ITEMS.FOOD.STEAMEDEGGWITHMEAT.NAME,
                desc: ModStrings.ITEMS.FOOD.STEAMEDEGGWITHMEAT.DESC,
                foodInfo: ModFoods.STEAMEDEGGWITHMEAT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SteamedEggWithMeat:DEF]END==
// ==[ITEM:GrilledMeatPlatter:DEF]BEGIN==
            Make(
                id: "GrilledMeatPlatter",
                anim: "grilledmeatplatter_kanim",
                name: ModStrings.ITEMS.FOOD.GRILLEDMEATPLATTER.NAME,
                desc: ModStrings.ITEMS.FOOD.GRILLEDMEATPLATTER.DESC,
                foodInfo: ModFoods.GRILLEDMEATPLATTER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:GrilledMeatPlatter:DEF]END==
// ==[ITEM:MixedVegetableStew:DEF]BEGIN==
            Make(
                id: "MixedVegetableStew",
                anim: "mixedvegetablestew_kanim",
                name: ModStrings.ITEMS.FOOD.MIXEDVEGETABLESTEW.NAME,
                desc: ModStrings.ITEMS.FOOD.MIXEDVEGETABLESTEW.DESC,
                foodInfo: ModFoods.MIXEDVEGETABLESTEW,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:MixedVegetableStew:DEF]END==
// ==[ITEM:ChickenWithMushroom:DEF]BEGIN==
            Make(
                id: "ChickenWithMushroom",
                anim: "chickenwithmushroom_kanim",
                name: ModStrings.ITEMS.FOOD.CHICKENWITHMUSHROOM.NAME,
                desc: ModStrings.ITEMS.FOOD.CHICKENWITHMUSHROOM.DESC,
                foodInfo: ModFoods.CHICKENWITHMUSHROOM,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:ChickenWithMushroom:DEF]END==
// ==[ITEM:GrilledSquidSkewer:DEF]BEGIN==
            Make(
                id: "GrilledSquidSkewer",
                anim: "grilledsquidskewer_kanim",
                name: ModStrings.ITEMS.FOOD.GRILLEDSQUIDSKEWER.NAME,
                desc: ModStrings.ITEMS.FOOD.GRILLEDSQUIDSKEWER.DESC,
                foodInfo: ModFoods.GRILLEDSQUIDSKEWER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:GrilledSquidSkewer:DEF]END==
// ==[ITEM:CannedMeat:DEF]BEGIN==
            Make(
                id: "CannedMeat",
                anim: "cannedmeat_kanim",
                name: ModStrings.ITEMS.FOOD.CANNEDMEAT.NAME,
                desc: ModStrings.ITEMS.FOOD.CANNEDMEAT.DESC,
                foodInfo: ModFoods.CANNEDMEAT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CannedMeat:DEF]END==
// ==[ITEM:CannedSeafood:DEF]BEGIN==
            Make(
                id: "CannedSeafood",
                anim: "cannedseafood_kanim",
                name: ModStrings.ITEMS.FOOD.CANNEDSEAFOOD.NAME,
                desc: ModStrings.ITEMS.FOOD.CANNEDSEAFOOD.DESC,
                foodInfo: ModFoods.CANNEDSEAFOOD,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CannedSeafood:DEF]END==
// ==[ITEM:CannedGrain:DEF]BEGIN==
            Make(
                id: "CannedGrain",
                anim: "cannedgrain_kanim",
                name: ModStrings.ITEMS.FOOD.CANNEDGRAIN.NAME,
                desc: ModStrings.ITEMS.FOOD.CANNEDGRAIN.DESC,
                foodInfo: ModFoods.CANNEDGRAIN,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CannedGrain:DEF]END==
// ==[ITEM:CannedFruit:DEF]BEGIN==
            Make(
                id: "CannedFruit",
                anim: "cannedfruit_kanim",
                name: ModStrings.ITEMS.FOOD.CANNEDFRUIT.NAME,
                desc: ModStrings.ITEMS.FOOD.CANNEDFRUIT.DESC,
                foodInfo: ModFoods.CANNEDFRUIT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CannedFruit:DEF]END==
// ==[ITEM:CookedRice:DEF]BEGIN==
            Make(
                id: "CookedRice",
                anim: "cookedrice_kanim",
                name: ModStrings.ITEMS.FOOD.COOKEDRICE.NAME,
                desc: ModStrings.ITEMS.FOOD.COOKEDRICE.DESC,
                foodInfo: ModFoods.COOKEDRICE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CookedRice:DEF]END==
// ==[ITEM:CrabMeatBalls:DEF]BEGIN==
            Make(
                id: "CrabMeatBalls",
                anim: "crabmeatballs_kanim",
                name: ModStrings.ITEMS.FOOD.CRABMEATBALLS.NAME,
                desc: ModStrings.ITEMS.FOOD.CRABMEATBALLS.DESC,
                foodInfo: ModFoods.CRABMEATBALLS,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CrabMeatBalls:DEF]END==
// ==[ITEM:SteamedCrabBalls:DEF]BEGIN==
            Make(
                id: "SteamedCrabBalls",
                anim: "steamedcrabballs_kanim",
                name: ModStrings.ITEMS.FOOD.STEAMEDCRABBALLS.NAME,
                desc: ModStrings.ITEMS.FOOD.STEAMEDCRABBALLS.DESC,
                foodInfo: ModFoods.STEAMEDCRABBALLS,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SteamedCrabBalls:DEF]END==
// ==[ITEM:StirFriedHairyCrab:DEF]BEGIN==
            Make(
                id: "StirFriedHairyCrab",
                anim: "stirfriedhairycrab_kanim",
                name: ModStrings.ITEMS.FOOD.STIRFRIEDHAIRYCRAB.NAME,
                desc: ModStrings.ITEMS.FOOD.STIRFRIEDHAIRYCRAB.DESC,
                foodInfo: ModFoods.STIRFRIEDHAIRYCRAB,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:StirFriedHairyCrab:DEF]END==
// ==[ITEM:CrispyRoastChicken:DEF]BEGIN==
            Make(
                id: "CrispyRoastChicken",
                anim: "crispyroastchicken_kanim",
                name: ModStrings.ITEMS.FOOD.CRISPYROASTCHICKEN.NAME,
                desc: ModStrings.ITEMS.FOOD.CRISPYROASTCHICKEN.DESC,
                foodInfo: ModFoods.CRISPYROASTCHICKEN,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CrispyRoastChicken:DEF]END==
// ==[ITEM:CrazyThursday:DEF]BEGIN==
            Make(
                id: "CrazyThursday",
                anim: "crazythursday_kanim",
                name: ModStrings.ITEMS.FOOD.CRAZYTHURSDAY.NAME,
                desc: ModStrings.ITEMS.FOOD.CRAZYTHURSDAY.DESC,
                foodInfo: ModFoods.CRAZYTHURSDAY,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CrazyThursday:DEF]END==
// ==[ITEM:BeefBurger:DEF]BEGIN==
            Make(
                id: "BeefBurger",
                anim: "beefburger_kanim",
                name: ModStrings.ITEMS.FOOD.BEEFBURGER.NAME,
                desc: ModStrings.ITEMS.FOOD.BEEFBURGER.DESC,
                foodInfo: ModFoods.BEEFBURGER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BeefBurger:DEF]END==
// ==[ITEM:BraisedPorkRiceBowl:DEF]BEGIN==
            Make(
                id: "BraisedPorkRiceBowl",
                anim: "braisedporkricebowl_kanim",
                name: ModStrings.ITEMS.FOOD.BRAISEDPORKRICEBOWL.NAME,
                desc: ModStrings.ITEMS.FOOD.BRAISEDPORKRICEBOWL.DESC,
                foodInfo: ModFoods.BRAISEDPORKRICEBOWL,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BraisedPorkRiceBowl:DEF]END==
// ==[ITEM:DeerAntlerTonicSoup:DEF]BEGIN==
            Make(
                id: "DeerAntlerTonicSoup",
                anim: "deerantlertonicsoup_kanim",
                name: ModStrings.ITEMS.FOOD.DEERANTLERTONICSOUP.NAME,
                desc: ModStrings.ITEMS.FOOD.DEERANTLERTONICSOUP.DESC,
                foodInfo: ModFoods.DEERANTLERTONICSOUP,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:DeerAntlerTonicSoup:DEF]END==
// ==[ITEM:BraisedBeefInSoySauce:DEF]BEGIN==
            Make(
                id: "BraisedBeefInSoySauce",
                anim: "braisedbeefinsoysauce_kanim",
                name: ModStrings.ITEMS.FOOD.BRAISEDBEEFINSOYSAUCE.NAME,
                desc: ModStrings.ITEMS.FOOD.BRAISEDBEEFINSOYSAUCE.DESC,
                foodInfo: ModFoods.BRAISEDBEEFINSOYSAUCE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BraisedBeefInSoySauce:DEF]END==
// ==[ITEM:StewedBeef:DEF]BEGIN==
            Make(
                id: "StewedBeef",
                anim: "stewedbeef_kanim",
                name: ModStrings.ITEMS.FOOD.STEWEDBEEF.NAME,
                desc: ModStrings.ITEMS.FOOD.STEWEDBEEF.DESC,
                foodInfo: ModFoods.STEWEDBEEF,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:StewedBeef:DEF]END==
// ==[ITEM:TomahawkSteak:DEF]BEGIN==
            Make(
                id: "TomahawkSteak",
                anim: "tomahawksteak_kanim",
                name: ModStrings.ITEMS.FOOD.TOMAHAWKSTEAK.NAME,
                desc: ModStrings.ITEMS.FOOD.TOMAHAWKSTEAK.DESC,
                foodInfo: ModFoods.TOMAHAWKSTEAK,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:TomahawkSteak:DEF]END==
// ==[ITEM:BraisedSoftshellTurtle:DEF]BEGIN==
            Make(
                id: "BraisedSoftshellTurtle",
                anim: "braisedsoftshellturtle_kanim",
                name: ModStrings.ITEMS.FOOD.BRAISEDSOFTSHELLTURTLE.NAME,
                desc: ModStrings.ITEMS.FOOD.BRAISEDSOFTSHELLTURTLE.DESC,
                foodInfo: ModFoods.BRAISEDSOFTSHELLTURTLE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BraisedSoftshellTurtle:DEF]END==
// ==[ITEM:Pudding:DEF]BEGIN==
            Make(
                id: "Pudding",
                anim: "pudding_kanim",
                name: ModStrings.ITEMS.FOOD.PUDDING.NAME,
                desc: ModStrings.ITEMS.FOOD.PUDDING.DESC,
                foodInfo: ModFoods.PUDDING,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Pudding:DEF]END==
// ==[ITEM:SoyMilk:DEF]BEGIN==
            Make(
                id: "SoyMilk",
                anim: "soymilk_kanim",
                name: ModStrings.ITEMS.FOOD.SOYMILK.NAME,
                desc: ModStrings.ITEMS.FOOD.SOYMILK.DESC,
                foodInfo: ModFoods.SOYMILK,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SoyMilk:DEF]END==
// ==[ITEM:SoftshellTurtleMeat:DEF]BEGIN==
            Make(
                id: "SoftshellTurtleMeat",
                anim: "softshellturtlemeat_kanim",
                name: ModStrings.ITEMS.FOOD.SOFTSHELLTURTLEMEAT.NAME,
                desc: ModStrings.ITEMS.FOOD.SOFTSHELLTURTLEMEAT.DESC,
                foodInfo: ModFoods.SOFTSHELLTURTLEMEAT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SoftshellTurtleMeat:DEF]END==
// ==[ITEM:BeefMeat:DEF]BEGIN==
            Make(
                id: "BeefMeat",
                anim: "beefmeat_kanim",
                name: ModStrings.ITEMS.FOOD.BEEFMEAT.NAME,
                desc: ModStrings.ITEMS.FOOD.BEEFMEAT.DESC,
                foodInfo: ModFoods.BEEFMEAT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BeefMeat:DEF]END==
// ==[ITEM:CrabClaw:DEF]BEGIN==
            Make(
                id: "CrabClaw",
                anim: "crabclaw_kanim",
                name: ModStrings.ITEMS.FOOD.CRABCLAW.NAME,
                desc: ModStrings.ITEMS.FOOD.CRABCLAW.DESC,
                foodInfo: ModFoods.CRABCLAW,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CrabClaw:DEF]END==
// ==[ITEM:CrabLeg:DEF]BEGIN==
            Make(
                id: "CrabLeg",
                anim: "crableg_kanim",
                name: ModStrings.ITEMS.FOOD.CRABLEG.NAME,
                desc: ModStrings.ITEMS.FOOD.CRABLEG.DESC,
                foodInfo: ModFoods.CRABLEG,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CrabLeg:DEF]END==
// ==[ITEM:Pineapple:DEF]BEGIN==
            Make(
                id: "Pineapple",
                anim: "pineapple_kanim",
                name: ModStrings.ITEMS.FOOD.PINEAPPLE.NAME,
                desc: ModStrings.ITEMS.FOOD.PINEAPPLE.DESC,
                foodInfo: ModFoods.PINEAPPLE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Pineapple:DEF]END==
// ==[ITEM:NapaCabbage:DEF]BEGIN==
            Make(
                id: "NapaCabbage",
                anim: "napacabbage_kanim",
                name: ModStrings.ITEMS.FOOD.NAPACABBAGE.NAME,
                desc: ModStrings.ITEMS.FOOD.NAPACABBAGE.DESC,
                foodInfo: ModFoods.NAPACABBAGE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:NapaCabbage:DEF]END==
// ==[ITEM:Orange:DEF]BEGIN==
            Make(
                id: "Orange",
                anim: "orange_kanim",
                name: ModStrings.ITEMS.FOOD.ORANGE.NAME,
                desc: ModStrings.ITEMS.FOOD.ORANGE.DESC,
                foodInfo: ModFoods.ORANGE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Orange:DEF]END==
// ==[ITEM:BlackCurrant:DEF]BEGIN==
            Make(
                id: "BlackCurrant",
                anim: "blackcurrant_kanim",
                name: ModStrings.ITEMS.FOOD.BLACKCURRANT.NAME,
                desc: ModStrings.ITEMS.FOOD.BLACKCURRANT.DESC,
                foodInfo: ModFoods.BLACKCURRANT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BlackCurrant:DEF]END==
// ==[ITEM:Blueberry:DEF]BEGIN==
            Make(
                id: "Blueberry",
                anim: "blueberry_kanim",
                name: ModStrings.ITEMS.FOOD.BLUEBERRY.NAME,
                desc: ModStrings.ITEMS.FOOD.BLUEBERRY.DESC,
                foodInfo: ModFoods.BLUEBERRY,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Blueberry:DEF]END==
// ==[ITEM:Grape:DEF]BEGIN==
            Make(
                id: "Grape",
                anim: "grape_kanim",
                name: ModStrings.ITEMS.FOOD.GRAPE.NAME,
                desc: ModStrings.ITEMS.FOOD.GRAPE.DESC,
                foodInfo: ModFoods.GRAPE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Grape:DEF]END==
// ==[ITEM:Watermelon:DEF]BEGIN==
            Make(
                id: "Watermelon",
                anim: "watermelon_kanim",
                name: ModStrings.ITEMS.FOOD.WATERMELON.NAME,
                desc: ModStrings.ITEMS.FOOD.WATERMELON.DESC,
                foodInfo: ModFoods.WATERMELON,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Watermelon:DEF]END==
// ==[ITEM:Prune:DEF]BEGIN==
            Make(
                id: "Prune",
                anim: "prune_kanim",
                name: ModStrings.ITEMS.FOOD.PRUNE.NAME,
                desc: ModStrings.ITEMS.FOOD.PRUNE.DESC,
                foodInfo: ModFoods.PRUNE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Prune:DEF]END==
// ==[ITEM:Mango:DEF]BEGIN==
            Make(
                id: "Mango",
                anim: "mango_kanim",
                name: ModStrings.ITEMS.FOOD.MANGO.NAME,
                desc: ModStrings.ITEMS.FOOD.MANGO.DESC,
                foodInfo: ModFoods.MANGO,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Mango:DEF]END==
// ==[ITEM:Potato:DEF]BEGIN==
            Make(
                id: "Potato",
                anim: "potato_kanim",
                name: ModStrings.ITEMS.FOOD.POTATO.NAME,
                desc: ModStrings.ITEMS.FOOD.POTATO.DESC,
                foodInfo: ModFoods.POTATO,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Potato:DEF]END==
// ==[ITEM:Onion:DEF]BEGIN==
            Make(
                id: "Onion",
                anim: "onion_kanim",
                name: ModStrings.ITEMS.FOOD.ONION.NAME,
                desc: ModStrings.ITEMS.FOOD.ONION.DESC,
                foodInfo: ModFoods.ONION,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Onion:DEF]END==
// ==[ITEM:Eggplant:DEF]BEGIN==
            Make(
                id: "Eggplant",
                anim: "eggplant_kanim",
                name: ModStrings.ITEMS.FOOD.EGGPLANT.NAME,
                desc: ModStrings.ITEMS.FOOD.EGGPLANT.DESC,
                foodInfo: ModFoods.EGGPLANT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Eggplant:DEF]END==
// ==[ITEM:Banana:DEF]BEGIN==
            Make(
                id: "Banana",
                anim: "banana_kanim",
                name: ModStrings.ITEMS.FOOD.BANANA.NAME,
                desc: ModStrings.ITEMS.FOOD.BANANA.DESC,
                foodInfo: ModFoods.BANANA,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Banana:DEF]END==
// ==[ITEM:Fig:DEF]BEGIN==
            Make(
                id: "Fig",
                anim: "fig_kanim",
                name: ModStrings.ITEMS.FOOD.FIG.NAME,
                desc: ModStrings.ITEMS.FOOD.FIG.DESC,
                foodInfo: ModFoods.FIG,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Fig:DEF]END==
// ==[ITEM:DragonFruit:DEF]BEGIN==
            Make(
                id: "DragonFruit",
                anim: "dragonfruit_kanim",
                name: ModStrings.ITEMS.FOOD.DRAGONFRUIT.NAME,
                desc: ModStrings.ITEMS.FOOD.DRAGONFRUIT.DESC,
                foodInfo: ModFoods.DRAGONFRUIT,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:DragonFruit:DEF]END==
// ==[ITEM:Tomato:DEF]BEGIN==
            Make(
                id: "Tomato",
                anim: "tomato_kanim",
                name: ModStrings.ITEMS.FOOD.TOMATO.NAME,
                desc: ModStrings.ITEMS.FOOD.TOMATO.DESC,
                foodInfo: ModFoods.TOMATO,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Tomato:DEF]END==
// ==[ITEM:Honey:DEF]BEGIN==
            Make(
                id: "Honey",
                anim: "honey_kanim",
                name: ModStrings.ITEMS.FOOD.HONEY.NAME,
                desc: ModStrings.ITEMS.FOOD.HONEY.DESC,
                foodInfo: ModFoods.HONEY,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Honey:DEF]END==
// ==[ITEM:Butter:DEF]BEGIN==
            Make(
                id: "Butter",
                anim: "butter_kanim",
                name: ModStrings.ITEMS.FOOD.BUTTER.NAME,
                desc: ModStrings.ITEMS.FOOD.BUTTER.DESC,
                foodInfo: ModFoods.BUTTER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Butter:DEF]END==
// ==[ITEM:ElephantTrunk:DEF]BEGIN==
            Make(
                id: "ElephantTrunk",
                anim: "elephanttrunk_kanim",
                name: ModStrings.ITEMS.FOOD.ELEPHANTTRUNK.NAME,
                desc: ModStrings.ITEMS.FOOD.ELEPHANTTRUNK.DESC,
                foodInfo: ModFoods.ELEPHANTTRUNK,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:ElephantTrunk:DEF]END==
// ==[ITEM:Barnacle:DEF]BEGIN==
            Make(
                id: "Barnacle",
                anim: "barnacle_kanim",
                name: ModStrings.ITEMS.FOOD.BARNACLE.NAME,
                desc: ModStrings.ITEMS.FOOD.BARNACLE.DESC,
                foodInfo: ModFoods.BARNACLE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Barnacle:DEF]END==
// ==[ITEM:LargeHaunch:DEF]BEGIN==
            Make(
                id: "LargeHaunch",
                anim: "largehaunch_kanim",
                name: ModStrings.ITEMS.FOOD.LARGEHAUNCH.NAME,
                desc: ModStrings.ITEMS.FOOD.LARGEHAUNCH.DESC,
                foodInfo: ModFoods.LARGEHAUNCH,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:LargeHaunch:DEF]END==
// ==[ITEM:LongPork:DEF]BEGIN==
            Make(
                id: "LongPork",
                anim: "longpork_kanim",
                name: ModStrings.ITEMS.FOOD.LONGPORK.NAME,
                desc: ModStrings.ITEMS.FOOD.LONGPORK.DESC,
                foodInfo: ModFoods.LONGPORK,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:LongPork:DEF]END==
// ==[ITEM:Drumstick:DEF]BEGIN==
            Make(
                id: "Drumstick",
                anim: "drumstick_kanim",
                name: ModStrings.ITEMS.FOOD.DRUMSTICK.NAME,
                desc: ModStrings.ITEMS.FOOD.DRUMSTICK.DESC,
                foodInfo: ModFoods.DRUMSTICK,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Drumstick:DEF]END==
// ==[ITEM:FrozenBananaDaiquiri:DEF]BEGIN==
            Make(
                id: "FrozenBananaDaiquiri",
                anim: "frozenbananadaiquiri_kanim",
                name: ModStrings.ITEMS.FOOD.FROZENBANANADAIQUIRI.NAME,
                desc: ModStrings.ITEMS.FOOD.FROZENBANANADAIQUIRI.DESC,
                foodInfo: ModFoods.FROZENBANANADAIQUIRI,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:FrozenBananaDaiquiri:DEF]END==
// ==[ITEM:PumpkinCake:DEF]BEGIN==
            Make(
                id: "PumpkinCake",
                anim: "pumpkincake_kanim",
                name: ModStrings.ITEMS.FOOD.PUMPKINCAKE.NAME,
                desc: ModStrings.ITEMS.FOOD.PUMPKINCAKE.DESC,
                foodInfo: ModFoods.PUMPKINCAKE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:PumpkinCake:DEF]END==
// ==[ITEM:HaunchSoup:DEF]BEGIN==
            Make(
                id: "HaunchSoup",
                anim: "haunchsoup_kanim",
                name: ModStrings.ITEMS.FOOD.HAUNCHSOUP.NAME,
                desc: ModStrings.ITEMS.FOOD.HAUNCHSOUP.DESC,
                foodInfo: ModFoods.HAUNCHSOUP,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:HaunchSoup:DEF]END==
// ==[ITEM:CreamyMashedPotato:DEF]BEGIN==
            Make(
                id: "CreamyMashedPotato",
                anim: "creamymashedpotato_kanim",
                name: ModStrings.ITEMS.FOOD.CREAMYMASHEDPOTATO.NAME,
                desc: ModStrings.ITEMS.FOOD.CREAMYMASHEDPOTATO.DESC,
                foodInfo: ModFoods.CREAMYMASHEDPOTATO,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CreamyMashedPotato:DEF]END==
// ==[ITEM:FigStuffedTrunk:DEF]BEGIN==
            Make(
                id: "FigStuffedTrunk",
                anim: "figstuffedtrunk_kanim",
                name: ModStrings.ITEMS.FOOD.FIGSTUFFEDTRUNK.NAME,
                desc: ModStrings.ITEMS.FOOD.FIGSTUFFEDTRUNK.DESC,
                foodInfo: ModFoods.FIGSTUFFEDTRUNK,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:FigStuffedTrunk:DEF]END==
// ==[ITEM:Perogies:DEF]BEGIN==
            Make(
                id: "Perogies",
                anim: "perogies_kanim",
                name: ModStrings.ITEMS.FOOD.PEROGIES.NAME,
                desc: ModStrings.ITEMS.FOOD.PEROGIES.DESC,
                foodInfo: ModFoods.PEROGIES,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Perogies:DEF]END==
// ==[ITEM:SeafoodGumbo:DEF]BEGIN==
            Make(
                id: "SeafoodGumbo",
                anim: "seafoodgumbo_kanim",
                name: ModStrings.ITEMS.FOOD.SEAFOODGUMBO.NAME,
                desc: ModStrings.ITEMS.FOOD.SEAFOODGUMBO.DESC,
                foodInfo: ModFoods.SEAFOODGUMBO,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SeafoodGumbo:DEF]END==
// ==[ITEM:TurkeyDinner:DEF]BEGIN==
            Make(
                id: "TurkeyDinner",
                anim: "turkeydinner_kanim",
                name: ModStrings.ITEMS.FOOD.TURKEYDINNER.NAME,
                desc: ModStrings.ITEMS.FOOD.TURKEYDINNER.DESC,
                foodInfo: ModFoods.TURKEYDINNER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:TurkeyDinner:DEF]END==
// ==[ITEM:RoastedWings:DEF]BEGIN==
            Make(
                id: "RoastedWings",
                anim: "roastedwings_kanim",
                name: ModStrings.ITEMS.FOOD.ROASTEDWINGS.NAME,
                desc: ModStrings.ITEMS.FOOD.ROASTEDWINGS.DESC,
                foodInfo: ModFoods.ROASTEDWINGS,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:RoastedWings:DEF]END==
// ==[ITEM:PotatoTornado:DEF]BEGIN==
            Make(
                id: "PotatoTornado",
                anim: "potatotornado_kanim",
                name: ModStrings.ITEMS.FOOD.POTATOTORNADO.NAME,
                desc: ModStrings.ITEMS.FOOD.POTATOTORNADO.DESC,
                foodInfo: ModFoods.POTATOTORNADO,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:PotatoTornado:DEF]END==
// ==[ITEM:FluffyPotatoSouffle:DEF]BEGIN==
            Make(
                id: "FluffyPotatoSouffle",
                anim: "fluffypotatosouffle_kanim",
                name: ModStrings.ITEMS.FOOD.FLUFFYPOTATOSOUFFLE.NAME,
                desc: ModStrings.ITEMS.FOOD.FLUFFYPOTATOSOUFFLE.DESC,
                foodInfo: ModFoods.FLUFFYPOTATOSOUFFLE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:FluffyPotatoSouffle:DEF]END==
// ==[ITEM:VegStinger:DEF]BEGIN==
            Make(
                id: "VegStinger",
                anim: "vegstinger_kanim",
                name: ModStrings.ITEMS.FOOD.VEGSTINGER.NAME,
                desc: ModStrings.ITEMS.FOOD.VEGSTINGER.DESC,
                foodInfo: ModFoods.VEGSTINGER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:VegStinger:DEF]END==
// ==[ITEM:BarnacleNoodles:DEF]BEGIN==
            Make(
                id: "BarnacleNoodles",
                anim: "barnaclenoodles_kanim",
                name: ModStrings.ITEMS.FOOD.BARNACLENOODLES.NAME,
                desc: ModStrings.ITEMS.FOOD.BARNACLENOODLES.DESC,
                foodInfo: ModFoods.BARNACLENOODLES,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BarnacleNoodles:DEF]END==
// ==[ITEM:WatermelonPopsicle:DEF]BEGIN==
            Make(
                id: "WatermelonPopsicle",
                anim: "watermelonpopsicle_kanim",
                name: ModStrings.ITEMS.FOOD.WATERMELONPOPSICLE.NAME,
                desc: ModStrings.ITEMS.FOOD.WATERMELONPOPSICLE.DESC,
                foodInfo: ModFoods.WATERMELONPOPSICLE,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:WatermelonPopsicle:DEF]END==
// ==[ITEM:BananaJelly:DEF]BEGIN==
            Make(
                id: "BananaJelly",
                anim: "bananajelly_kanim",
                name: ModStrings.ITEMS.FOOD.BANANAJELLY.NAME,
                desc: ModStrings.ITEMS.FOOD.BANANAJELLY.DESC,
                foodInfo: ModFoods.BANANAJELLY,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BananaJelly:DEF]END==
// ==[ITEM:EelSushi:DEF]BEGIN==
            Make(
                id: "EelSushi",
                anim: "eelsushi_kanim",
                name: ModStrings.ITEMS.FOOD.EELSUSHI.NAME,
                desc: ModStrings.ITEMS.FOOD.EELSUSHI.DESC,
                foodInfo: ModFoods.EELSUSHI,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:EelSushi:DEF]END==
// ==[ITEM:LobsterDinner:DEF]BEGIN==
            Make(
                id: "LobsterDinner",
                anim: "lobsterdinner_kanim",
                name: ModStrings.ITEMS.FOOD.LOBSTERDINNER.NAME,
                desc: ModStrings.ITEMS.FOOD.LOBSTERDINNER.DESC,
                foodInfo: ModFoods.LOBSTERDINNER,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: null,
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:LobsterDinner:DEF]END==
            // ==[ZONE:DEFS]END==
        };

        public List<GameObject> CreatePrefabs()
        {
            var list = new List<GameObject>(_defs.Length);
            foreach (var d in _defs)
            {
                list.Add(Create(d));
            }
            return list;
        }

        private static GameObject Create(Def d)
        {
            GameObject go = EntityTemplates.CreateLooseEntity(
                id: d.id,
                name: d.name,
                desc: d.desc,
                mass: d.mass,
                unitMass: false,
                anim: Assets.GetAnim(d.anim),
                initialAnim: "object",
                sceneLayer: d.sceneLayer,
                collisionShape: EntityTemplates.CollisionShape.RECTANGLE,
                width: d.width,
                height: d.height,
                isPickupable: true,
                sortOrder: d.sortOrder,
                element: d.element,
                additionalTags: d.tags);

            // 挂载食物组件
            go = EntityTemplates.ExtendEntityToFood(go, d.foodInfo);

            KPrefabID kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                kpid.requiredDlcIds = d.requireDlc;
                kpid.forbiddenDlcIds = d.forbidDlc;
            }

            go.AddOrGet<EntitySplitter>();
            ModFoods.RegisterFoodId(d.id);
            return go;
        }

        public void OnPrefabInit(GameObject inst) { }
        public void OnSpawn(GameObject inst) { }
    }
}
