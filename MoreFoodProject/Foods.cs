using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TUNING;
using UnityEngine;

namespace MoreFood_KFC{
    public static class ModFoods
    {
        // ==[ZONE:FOODS]BEGIN==
// ==[ITEM:SauceBraisedRibs:FOOD]BEGIN==

        // 酱汁排骨 — 食物属性

        public static readonly EdiblesManager.FoodInfo SAUCEBRAISEDRIBS =
            new EdiblesManager.FoodInfo(
                id: "SauceBraisedRibs",
                caloriesPerUnit: 2000000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:SauceBraisedRibs:FOOD]END==
// ==[ITEM:SteamedEggWithMeat:FOOD]BEGIN==

        // 肉沫蒸蛋 — 食物属性

        public static readonly EdiblesManager.FoodInfo STEAMEDEGGWITHMEAT =
            new EdiblesManager.FoodInfo(
                id: "SteamedEggWithMeat",
                caloriesPerUnit: 2400000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:SteamedEggWithMeat:FOOD]END==
// ==[ITEM:GrilledMeatPlatter:FOOD]BEGIN==

        // 烧烤拼盘 — 食物属性

        public static readonly EdiblesManager.FoodInfo GRILLEDMEATPLATTER =
            new EdiblesManager.FoodInfo(
                id: "GrilledMeatPlatter",
                caloriesPerUnit: 2400000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:GrilledMeatPlatter:FOOD]END==
// ==[ITEM:MixedVegetableStew:FOOD]BEGIN==

        // 蔬菜杂烩 — 食物属性

        public static readonly EdiblesManager.FoodInfo MIXEDVEGETABLESTEW =
            new EdiblesManager.FoodInfo(
                id: "MixedVegetableStew",
                caloriesPerUnit: 2400000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:MixedVegetableStew:FOOD]END==
// ==[ITEM:ChickenWithMushroom:FOOD]BEGIN==

        // 小鸡炖蘑菇 — 食物属性

        public static readonly EdiblesManager.FoodInfo CHICKENWITHMUSHROOM =
            new EdiblesManager.FoodInfo(
                id: "ChickenWithMushroom",
                caloriesPerUnit: 3200000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:ChickenWithMushroom:FOOD]END==
// ==[ITEM:GrilledSquidSkewer:FOOD]BEGIN==

        // 鱿鱼串 — 食物属性

        public static readonly EdiblesManager.FoodInfo GRILLEDSQUIDSKEWER =
            new EdiblesManager.FoodInfo(
                id: "GrilledSquidSkewer",
                caloriesPerUnit: 1600000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:GrilledSquidSkewer:FOOD]END==
// ==[ITEM:CannedMeat:FOOD]BEGIN==

        // 鲜肉罐头 — 食物属性

        public static readonly EdiblesManager.FoodInfo CANNEDMEAT =
            new EdiblesManager.FoodInfo(
                id: "CannedMeat",
                caloriesPerUnit: 1200000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: false);
// ==[ITEM:CannedMeat:FOOD]END==
// ==[ITEM:CannedSeafood:FOOD]BEGIN==

        // 海鲜罐头 — 食物属性

        public static readonly EdiblesManager.FoodInfo CANNEDSEAFOOD =
            new EdiblesManager.FoodInfo(
                id: "CannedSeafood",
                caloriesPerUnit: 1000000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: false);
// ==[ITEM:CannedSeafood:FOOD]END==
// ==[ITEM:CannedGrain:FOOD]BEGIN==

        // 杂粮罐头 — 食物属性

        public static readonly EdiblesManager.FoodInfo CANNEDGRAIN =
            new EdiblesManager.FoodInfo(
                id: "CannedGrain",
                caloriesPerUnit: 1000000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: false);
// ==[ITEM:CannedGrain:FOOD]END==
// ==[ITEM:CannedFruit:FOOD]BEGIN==

        // 水果罐头 — 食物属性

        public static readonly EdiblesManager.FoodInfo CANNEDFRUIT =
            new EdiblesManager.FoodInfo(
                id: "CannedFruit",
                caloriesPerUnit: 1000000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: false);
// ==[ITEM:CannedFruit:FOOD]END==
// ==[ITEM:CookedRice:FOOD]BEGIN==

        // 米饭 — 食物属性

        public static readonly EdiblesManager.FoodInfo COOKEDRICE =
            new EdiblesManager.FoodInfo(
                id: "CookedRice",
                caloriesPerUnit: 1200000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:CookedRice:FOOD]END==
// ==[ITEM:CrabMeatBalls:FOOD]BEGIN==

        // 蟹肉球 — 食物属性

        public static readonly EdiblesManager.FoodInfo CRABMEATBALLS =
            new EdiblesManager.FoodInfo(
                id: "CrabMeatBalls",
                caloriesPerUnit: 1800000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:CrabMeatBalls:FOOD]END==
// ==[ITEM:SteamedCrabBalls:FOOD]BEGIN==

        // 清蒸蟹球 — 食物属性

        public static readonly EdiblesManager.FoodInfo STEAMEDCRABBALLS =
            new EdiblesManager.FoodInfo(
                id: "SteamedCrabBalls",
                caloriesPerUnit: 2400000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:SteamedCrabBalls:FOOD]END==
// ==[ITEM:StirFriedHairyCrab:FOOD]BEGIN==

        // 爆炒大闸蟹 — 食物属性

        public static readonly EdiblesManager.FoodInfo STIRFRIEDHAIRYCRAB =
            new EdiblesManager.FoodInfo(
                id: "StirFriedHairyCrab",
                caloriesPerUnit: 2400000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:StirFriedHairyCrab:FOOD]END==
// ==[ITEM:CrispyRoastChicken:FOOD]BEGIN==

        // 脆皮烤鸡 — 食物属性

        public static readonly EdiblesManager.FoodInfo CRISPYROASTCHICKEN =
            new EdiblesManager.FoodInfo(
                id: "CrispyRoastChicken",
                caloriesPerUnit: 2000000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 6400.0f,
                can_rot: true);
// ==[ITEM:CrispyRoastChicken:FOOD]END==
// ==[ITEM:CrazyThursday:FOOD]BEGIN==

        // 疯狂星期四 — 食物属性

        public static readonly EdiblesManager.FoodInfo CRAZYTHURSDAY =
            new EdiblesManager.FoodInfo(
                id: "CrazyThursday",
                caloriesPerUnit: 5400000.0f,
                quality: 6,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 6400.0f,
                can_rot: true);
// ==[ITEM:CrazyThursday:FOOD]END==
// ==[ITEM:BeefBurger:FOOD]BEGIN==

        // 汉堡 — 食物属性

        public static readonly EdiblesManager.FoodInfo BEEFBURGER =
            new EdiblesManager.FoodInfo(
                id: "BeefBurger",
                caloriesPerUnit: 2400000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BeefBurger:FOOD]END==
// ==[ITEM:BraisedPorkRiceBowl:FOOD]BEGIN==

        // 红烧肉盖饭 — 食物属性

        public static readonly EdiblesManager.FoodInfo BRAISEDPORKRICEBOWL =
            new EdiblesManager.FoodInfo(
                id: "BraisedPorkRiceBowl",
                caloriesPerUnit: 2600000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BraisedPorkRiceBowl:FOOD]END==
// ==[ITEM:DeerAntlerTonicSoup:FOOD]BEGIN==

        // 鹿茸大补汤 — 食物属性

        public static readonly EdiblesManager.FoodInfo DEERANTLERTONICSOUP =
            new EdiblesManager.FoodInfo(
                id: "DeerAntlerTonicSoup",
                caloriesPerUnit: 1200000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:DeerAntlerTonicSoup:FOOD]END==
// ==[ITEM:BraisedBeefInSoySauce:FOOD]BEGIN==

        // 酱牛肉 — 食物属性

        public static readonly EdiblesManager.FoodInfo BRAISEDBEEFINSOYSAUCE =
            new EdiblesManager.FoodInfo(
                id: "BraisedBeefInSoySauce",
                caloriesPerUnit: 1200000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BraisedBeefInSoySauce:FOOD]END==
// ==[ITEM:StewedBeef:FOOD]BEGIN==

        // 炖牛肉 — 食物属性

        public static readonly EdiblesManager.FoodInfo STEWEDBEEF =
            new EdiblesManager.FoodInfo(
                id: "StewedBeef",
                caloriesPerUnit: 2400000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:StewedBeef:FOOD]END==
// ==[ITEM:TomahawkSteak:FOOD]BEGIN==

        // 战斧牛排 — 食物属性

        public static readonly EdiblesManager.FoodInfo TOMAHAWKSTEAK =
            new EdiblesManager.FoodInfo(
                id: "TomahawkSteak",
                caloriesPerUnit: 1200000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:TomahawkSteak:FOOD]END==
// ==[ITEM:BraisedSoftshellTurtle:FOOD]BEGIN==

        // 红烧甲鱼 — 食物属性

        public static readonly EdiblesManager.FoodInfo BRAISEDSOFTSHELLTURTLE =
            new EdiblesManager.FoodInfo(
                id: "BraisedSoftshellTurtle",
                caloriesPerUnit: 1200000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BraisedSoftshellTurtle:FOOD]END==
// ==[ITEM:Pudding:FOOD]BEGIN==

        // 香草奶油布丁 — 食物属性

        public static readonly EdiblesManager.FoodInfo PUDDING =
            new EdiblesManager.FoodInfo(
                id: "Pudding",
                caloriesPerUnit: 1200000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:Pudding:FOOD]END==
// ==[ITEM:SoyMilk:FOOD]BEGIN==

        // 豆奶 — 食物属性

        public static readonly EdiblesManager.FoodInfo SOYMILK =
            new EdiblesManager.FoodInfo(
                id: "SoyMilk",
                caloriesPerUnit: 600000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:SoyMilk:FOOD]END==
// ==[ITEM:SoftshellTurtleMeat:FOOD]BEGIN==

        // 甲鱼肉 — 食物属性

        public static readonly EdiblesManager.FoodInfo SOFTSHELLTURTLEMEAT =
            new EdiblesManager.FoodInfo(
                id: "SoftshellTurtleMeat",
                caloriesPerUnit: 1200000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:SoftshellTurtleMeat:FOOD]END==
// ==[ITEM:BeefMeat:FOOD]BEGIN==

        // 牛肉 — 食物属性

        public static readonly EdiblesManager.FoodInfo BEEFMEAT =
            new EdiblesManager.FoodInfo(
                id: "BeefMeat",
                caloriesPerUnit: 1200000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BeefMeat:FOOD]END==
// ==[ITEM:CrabClaw:FOOD]BEGIN==

        // 蟹钳 — 食物属性

        public static readonly EdiblesManager.FoodInfo CRABCLAW =
            new EdiblesManager.FoodInfo(
                id: "CrabClaw",
                caloriesPerUnit: 600000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:CrabClaw:FOOD]END==
// ==[ITEM:CrabLeg:FOOD]BEGIN==

        // 蟹腿 — 食物属性

        public static readonly EdiblesManager.FoodInfo CRABLEG =
            new EdiblesManager.FoodInfo(
                id: "CrabLeg",
                caloriesPerUnit: 350000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:CrabLeg:FOOD]END==
// ==[ITEM:Pineapple:FOOD]BEGIN==

        // 菠萝 — 食物属性

        public static readonly EdiblesManager.FoodInfo PINEAPPLE =
            new EdiblesManager.FoodInfo(
                id: "Pineapple",
                caloriesPerUnit: 400000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Pineapple:FOOD]END==
// ==[ITEM:NapaCabbage:FOOD]BEGIN==

        // 白菜 — 食物属性

        public static readonly EdiblesManager.FoodInfo NAPACABBAGE =
            new EdiblesManager.FoodInfo(
                id: "NapaCabbage",
                caloriesPerUnit: 400000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:NapaCabbage:FOOD]END==
// ==[ITEM:Orange:FOOD]BEGIN==

        // 橙子 — 食物属性

        public static readonly EdiblesManager.FoodInfo ORANGE =
            new EdiblesManager.FoodInfo(
                id: "Orange",
                caloriesPerUnit: 400000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Orange:FOOD]END==
// ==[ITEM:BlackCurrant:FOOD]BEGIN==

        // 黑加仑 — 食物属性

        public static readonly EdiblesManager.FoodInfo BLACKCURRANT =
            new EdiblesManager.FoodInfo(
                id: "BlackCurrant",
                caloriesPerUnit: 325000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BlackCurrant:FOOD]END==
// ==[ITEM:Blueberry:FOOD]BEGIN==

        // 蓝莓 — 食物属性

        public static readonly EdiblesManager.FoodInfo BLUEBERRY =
            new EdiblesManager.FoodInfo(
                id: "Blueberry",
                caloriesPerUnit: 600000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Blueberry:FOOD]END==
// ==[ITEM:Grape:FOOD]BEGIN==

        // 葡萄 — 食物属性

        public static readonly EdiblesManager.FoodInfo GRAPE =
            new EdiblesManager.FoodInfo(
                id: "Grape",
                caloriesPerUnit: 400000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Grape:FOOD]END==
// ==[ITEM:Watermelon:FOOD]BEGIN==

        // 西瓜 — 食物属性

        public static readonly EdiblesManager.FoodInfo WATERMELON =
            new EdiblesManager.FoodInfo(
                id: "Watermelon",
                caloriesPerUnit: 250000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Watermelon:FOOD]END==
// ==[ITEM:Prune:FOOD]BEGIN==

        // 西梅 — 食物属性

        public static readonly EdiblesManager.FoodInfo PRUNE =
            new EdiblesManager.FoodInfo(
                id: "Prune",
                caloriesPerUnit: 800000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Prune:FOOD]END==
// ==[ITEM:Mango:FOOD]BEGIN==

        // 芒果 — 食物属性

        public static readonly EdiblesManager.FoodInfo MANGO =
            new EdiblesManager.FoodInfo(
                id: "Mango",
                caloriesPerUnit: 600000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Mango:FOOD]END==
// ==[ITEM:Potato:FOOD]BEGIN==

        // 土豆 — 食物属性

        public static readonly EdiblesManager.FoodInfo POTATO =
            new EdiblesManager.FoodInfo(
                id: "Potato",
                caloriesPerUnit: 400000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Potato:FOOD]END==
// ==[ITEM:Onion:FOOD]BEGIN==

        // 洋葱 — 食物属性

        public static readonly EdiblesManager.FoodInfo ONION =
            new EdiblesManager.FoodInfo(
                id: "Onion",
                caloriesPerUnit: 400000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Onion:FOOD]END==
// ==[ITEM:Eggplant:FOOD]BEGIN==

        // 茄子 — 食物属性

        public static readonly EdiblesManager.FoodInfo EGGPLANT =
            new EdiblesManager.FoodInfo(
                id: "Eggplant",
                caloriesPerUnit: 400000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Eggplant:FOOD]END==
// ==[ITEM:Banana:FOOD]BEGIN==

        // 香蕉 — 食物属性

        public static readonly EdiblesManager.FoodInfo BANANA =
            new EdiblesManager.FoodInfo(
                id: "Banana",
                caloriesPerUnit: 500000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Banana:FOOD]END==
// ==[ITEM:Fig:FOOD]BEGIN==

        // 无花果 — 食物属性

        public static readonly EdiblesManager.FoodInfo FIG =
            new EdiblesManager.FoodInfo(
                id: "Fig",
                caloriesPerUnit: 450000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Fig:FOOD]END==
// ==[ITEM:DragonFruit:FOOD]BEGIN==

        // 火龙果 — 食物属性

        public static readonly EdiblesManager.FoodInfo DRAGONFRUIT =
            new EdiblesManager.FoodInfo(
                id: "DragonFruit",
                caloriesPerUnit: 400000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:DragonFruit:FOOD]END==
// ==[ITEM:Tomato:FOOD]BEGIN==

        // 西红柿 — 食物属性

        public static readonly EdiblesManager.FoodInfo TOMATO =
            new EdiblesManager.FoodInfo(
                id: "Tomato",
                caloriesPerUnit: 400000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Tomato:FOOD]END==
// ==[ITEM:Honey:FOOD]BEGIN==

        // 蜂蜜 — 食物属性

        public static readonly EdiblesManager.FoodInfo HONEY =
            new EdiblesManager.FoodInfo(
                id: "Honey",
                caloriesPerUnit: 600000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:Honey:FOOD]END==
// ==[ITEM:Butter:FOOD]BEGIN==

        // 黄油 — 食物属性

        public static readonly EdiblesManager.FoodInfo BUTTER =
            new EdiblesManager.FoodInfo(
                id: "Butter",
                caloriesPerUnit: 800000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:Butter:FOOD]END==
// ==[ITEM:ElephantTrunk:FOOD]BEGIN==

        // 象鼻 — 食物属性

        public static readonly EdiblesManager.FoodInfo ELEPHANTTRUNK =
            new EdiblesManager.FoodInfo(
                id: "ElephantTrunk",
                caloriesPerUnit: 900000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:ElephantTrunk:FOOD]END==
// ==[ITEM:Barnacle:FOOD]BEGIN==

        // 藤壶 — 食物属性

        public static readonly EdiblesManager.FoodInfo BARNACLE =
            new EdiblesManager.FoodInfo(
                id: "Barnacle",
                caloriesPerUnit: 600000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Barnacle:FOOD]END==
// ==[ITEM:LargeHaunch:FOOD]BEGIN==

        // 大肉 — 食物属性

        public static readonly EdiblesManager.FoodInfo LARGEHAUNCH =
            new EdiblesManager.FoodInfo(
                id: "LargeHaunch",
                caloriesPerUnit: 1200000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:LargeHaunch:FOOD]END==
// ==[ITEM:LongPork:FOOD]BEGIN==

        // 长猪肉 — 食物属性

        public static readonly EdiblesManager.FoodInfo LONGPORK =
            new EdiblesManager.FoodInfo(
                id: "LongPork",
                caloriesPerUnit: 1000000.0f,
                quality: 1,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:LongPork:FOOD]END==
// ==[ITEM:Drumstick:FOOD]BEGIN==

        // 鸡腿 — 食物属性

        public static readonly EdiblesManager.FoodInfo DRUMSTICK =
            new EdiblesManager.FoodInfo(
                id: "Drumstick",
                caloriesPerUnit: 800000.0f,
                quality: 2,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Drumstick:FOOD]END==
// ==[ITEM:FrozenBananaDaiquiri:FOOD]BEGIN==

        // 冰香蕉冻唇蜜 — 食物属性

        public static readonly EdiblesManager.FoodInfo FROZENBANANADAIQUIRI =
            new EdiblesManager.FoodInfo(
                id: "FrozenBananaDaiquiri",
                caloriesPerUnit: 1200000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:FrozenBananaDaiquiri:FOOD]END==
// ==[ITEM:PumpkinCake:FOOD]BEGIN==

        // 南瓜饼 — 食物属性

        public static readonly EdiblesManager.FoodInfo PUMPKINCAKE =
            new EdiblesManager.FoodInfo(
                id: "PumpkinCake",
                caloriesPerUnit: 4800000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:PumpkinCake:FOOD]END==
// ==[ITEM:HaunchSoup:FOOD]BEGIN==

        // 大肉汤 — 食物属性

        public static readonly EdiblesManager.FoodInfo HAUNCHSOUP =
            new EdiblesManager.FoodInfo(
                id: "HaunchSoup",
                caloriesPerUnit: 2400000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:HaunchSoup:FOOD]END==
// ==[ITEM:CreamyMashedPotato:FOOD]BEGIN==

        // 奶油土豆泥 — 食物属性

        public static readonly EdiblesManager.FoodInfo CREAMYMASHEDPOTATO =
            new EdiblesManager.FoodInfo(
                id: "CreamyMashedPotato",
                caloriesPerUnit: 1800000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:CreamyMashedPotato:FOOD]END==
// ==[ITEM:FigStuffedTrunk:FOOD]BEGIN==

        // 无花果酿象鼻 — 食物属性

        public static readonly EdiblesManager.FoodInfo FIGSTUFFEDTRUNK =
            new EdiblesManager.FoodInfo(
                id: "FigStuffedTrunk",
                caloriesPerUnit: 2800000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:FigStuffedTrunk:FOOD]END==
// ==[ITEM:Perogies:FOOD]BEGIN==

        // 波兰水饺 — 食物属性

        public static readonly EdiblesManager.FoodInfo PEROGIES =
            new EdiblesManager.FoodInfo(
                id: "Perogies",
                caloriesPerUnit: 3200000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:Perogies:FOOD]END==
// ==[ITEM:SeafoodGumbo:FOOD]BEGIN==

        // 海鲜杂烩 — 食物属性

        public static readonly EdiblesManager.FoodInfo SEAFOODGUMBO =
            new EdiblesManager.FoodInfo(
                id: "SeafoodGumbo",
                caloriesPerUnit: 2400000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:SeafoodGumbo:FOOD]END==
// ==[ITEM:TurkeyDinner:FOOD]BEGIN==

        // 火鸡正餐 — 食物属性

        public static readonly EdiblesManager.FoodInfo TURKEYDINNER =
            new EdiblesManager.FoodInfo(
                id: "TurkeyDinner",
                caloriesPerUnit: 3200000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:TurkeyDinner:FOOD]END==
// ==[ITEM:RoastedWings:FOOD]BEGIN==

        // 烤翅 — 食物属性

        public static readonly EdiblesManager.FoodInfo ROASTEDWINGS =
            new EdiblesManager.FoodInfo(
                id: "RoastedWings",
                caloriesPerUnit: 1200000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:RoastedWings:FOOD]END==
// ==[ITEM:PotatoTornado:FOOD]BEGIN==

        // 花式回旋块茎 — 食物属性

        public static readonly EdiblesManager.FoodInfo POTATOTORNADO =
            new EdiblesManager.FoodInfo(
                id: "PotatoTornado",
                caloriesPerUnit: 1400000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:PotatoTornado:FOOD]END==
// ==[ITEM:FluffyPotatoSouffle:FOOD]BEGIN==

        // 蓬松土豆蛋奶酥 — 食物属性

        public static readonly EdiblesManager.FoodInfo FLUFFYPOTATOSOUFFLE =
            new EdiblesManager.FoodInfo(
                id: "FluffyPotatoSouffle",
                caloriesPerUnit: 2000000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:FluffyPotatoSouffle:FOOD]END==
// ==[ITEM:VegStinger:FOOD]BEGIN==

        // 蔬菜鸡尾酒 — 食物属性

        public static readonly EdiblesManager.FoodInfo VEGSTINGER =
            new EdiblesManager.FoodInfo(
                id: "VegStinger",
                caloriesPerUnit: 1200000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:VegStinger:FOOD]END==
// ==[ITEM:BarnacleNoodles:FOOD]BEGIN==

        // 藤壶细面 — 食物属性

        public static readonly EdiblesManager.FoodInfo BARNACLENOODLES =
            new EdiblesManager.FoodInfo(
                id: "BarnacleNoodles",
                caloriesPerUnit: 2800000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:BarnacleNoodles:FOOD]END==
// ==[ITEM:WatermelonPopsicle:FOOD]BEGIN==

        // 西瓜冰棍 — 食物属性

        public static readonly EdiblesManager.FoodInfo WATERMELONPOPSICLE =
            new EdiblesManager.FoodInfo(
                id: "WatermelonPopsicle",
                caloriesPerUnit: 1200000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:WatermelonPopsicle:FOOD]END==
// ==[ITEM:BananaJelly:FOOD]BEGIN==

        // 香蕉冻 — 食物属性

        public static readonly EdiblesManager.FoodInfo BANANAJELLY =
            new EdiblesManager.FoodInfo(
                id: "BananaJelly",
                caloriesPerUnit: 800000.0f,
                quality: 3,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 9600.0f,
                can_rot: true);
// ==[ITEM:BananaJelly:FOOD]END==
// ==[ITEM:EelSushi:FOOD]BEGIN==

        // 鳗鱼寿司 — 食物属性

        public static readonly EdiblesManager.FoodInfo EELSUSHI =
            new EdiblesManager.FoodInfo(
                id: "EelSushi",
                caloriesPerUnit: 2400000.0f,
                quality: 4,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:EelSushi:FOOD]END==
// ==[ITEM:LobsterDinner:FOOD]BEGIN==

        // 龙虾正餐 — 食物属性

        public static readonly EdiblesManager.FoodInfo LOBSTERDINNER =
            new EdiblesManager.FoodInfo(
                id: "LobsterDinner",
                caloriesPerUnit: 2800000.0f,
                quality: 5,
                preserveTemperatue: 255.14999999999998f,
                rotTemperature: 277.15f,
                spoilTime: 4800.0f,
                can_rot: true);
// ==[ITEM:LobsterDinner:FOOD]END==
        // ==[ZONE:FOODS]END==

        public static void Init()
        {
            // ==[ZONE:FOODS_INIT]BEGIN==
// ==[ITEM:SauceBraisedRibs:INIT]BEGIN==
            _ = SAUCEBRAISEDRIBS;
// ==[ITEM:SauceBraisedRibs:INIT]END==
// ==[ITEM:SteamedEggWithMeat:INIT]BEGIN==
            _ = STEAMEDEGGWITHMEAT;
// ==[ITEM:SteamedEggWithMeat:INIT]END==
// ==[ITEM:GrilledMeatPlatter:INIT]BEGIN==
            _ = GRILLEDMEATPLATTER;
// ==[ITEM:GrilledMeatPlatter:INIT]END==
// ==[ITEM:MixedVegetableStew:INIT]BEGIN==
            _ = MIXEDVEGETABLESTEW;
// ==[ITEM:MixedVegetableStew:INIT]END==
// ==[ITEM:ChickenWithMushroom:INIT]BEGIN==
            _ = CHICKENWITHMUSHROOM;
// ==[ITEM:ChickenWithMushroom:INIT]END==
// ==[ITEM:GrilledSquidSkewer:INIT]BEGIN==
            _ = GRILLEDSQUIDSKEWER;
// ==[ITEM:GrilledSquidSkewer:INIT]END==
// ==[ITEM:CannedMeat:INIT]BEGIN==
            _ = CANNEDMEAT;
// ==[ITEM:CannedMeat:INIT]END==
// ==[ITEM:CannedSeafood:INIT]BEGIN==
            _ = CANNEDSEAFOOD;
// ==[ITEM:CannedSeafood:INIT]END==
// ==[ITEM:CannedGrain:INIT]BEGIN==
            _ = CANNEDGRAIN;
// ==[ITEM:CannedGrain:INIT]END==
// ==[ITEM:CannedFruit:INIT]BEGIN==
            _ = CANNEDFRUIT;
// ==[ITEM:CannedFruit:INIT]END==
// ==[ITEM:CookedRice:INIT]BEGIN==
            _ = COOKEDRICE;
// ==[ITEM:CookedRice:INIT]END==
// ==[ITEM:CrabMeatBalls:INIT]BEGIN==
            _ = CRABMEATBALLS;
// ==[ITEM:CrabMeatBalls:INIT]END==
// ==[ITEM:SteamedCrabBalls:INIT]BEGIN==
            _ = STEAMEDCRABBALLS;
// ==[ITEM:SteamedCrabBalls:INIT]END==
// ==[ITEM:StirFriedHairyCrab:INIT]BEGIN==
            _ = STIRFRIEDHAIRYCRAB;
// ==[ITEM:StirFriedHairyCrab:INIT]END==
// ==[ITEM:CrispyRoastChicken:INIT]BEGIN==
            _ = CRISPYROASTCHICKEN;
// ==[ITEM:CrispyRoastChicken:INIT]END==
// ==[ITEM:CrazyThursday:INIT]BEGIN==
            _ = CRAZYTHURSDAY;
// ==[ITEM:CrazyThursday:INIT]END==
// ==[ITEM:BeefBurger:INIT]BEGIN==
            _ = BEEFBURGER;
// ==[ITEM:BeefBurger:INIT]END==
// ==[ITEM:BraisedPorkRiceBowl:INIT]BEGIN==
            _ = BRAISEDPORKRICEBOWL;
// ==[ITEM:BraisedPorkRiceBowl:INIT]END==
// ==[ITEM:DeerAntlerTonicSoup:INIT]BEGIN==
            _ = DEERANTLERTONICSOUP;
// ==[ITEM:DeerAntlerTonicSoup:INIT]END==
// ==[ITEM:BraisedBeefInSoySauce:INIT]BEGIN==
            _ = BRAISEDBEEFINSOYSAUCE;
// ==[ITEM:BraisedBeefInSoySauce:INIT]END==
// ==[ITEM:StewedBeef:INIT]BEGIN==
            _ = STEWEDBEEF;
// ==[ITEM:StewedBeef:INIT]END==
// ==[ITEM:TomahawkSteak:INIT]BEGIN==
            _ = TOMAHAWKSTEAK;
// ==[ITEM:TomahawkSteak:INIT]END==
// ==[ITEM:BraisedSoftshellTurtle:INIT]BEGIN==
            _ = BRAISEDSOFTSHELLTURTLE;
// ==[ITEM:BraisedSoftshellTurtle:INIT]END==
// ==[ITEM:Pudding:INIT]BEGIN==
            _ = PUDDING;
// ==[ITEM:Pudding:INIT]END==
// ==[ITEM:SoyMilk:INIT]BEGIN==
            _ = SOYMILK;
// ==[ITEM:SoyMilk:INIT]END==
// ==[ITEM:SoftshellTurtleMeat:INIT]BEGIN==
            _ = SOFTSHELLTURTLEMEAT;
// ==[ITEM:SoftshellTurtleMeat:INIT]END==
// ==[ITEM:BeefMeat:INIT]BEGIN==
            _ = BEEFMEAT;
// ==[ITEM:BeefMeat:INIT]END==
// ==[ITEM:CrabClaw:INIT]BEGIN==
            _ = CRABCLAW;
// ==[ITEM:CrabClaw:INIT]END==
// ==[ITEM:CrabLeg:INIT]BEGIN==
            _ = CRABLEG;
// ==[ITEM:CrabLeg:INIT]END==
// ==[ITEM:Pineapple:INIT]BEGIN==
            _ = PINEAPPLE;
// ==[ITEM:Pineapple:INIT]END==
// ==[ITEM:NapaCabbage:INIT]BEGIN==
            _ = NAPACABBAGE;
// ==[ITEM:NapaCabbage:INIT]END==
// ==[ITEM:Orange:INIT]BEGIN==
            _ = ORANGE;
// ==[ITEM:Orange:INIT]END==
// ==[ITEM:BlackCurrant:INIT]BEGIN==
            _ = BLACKCURRANT;
// ==[ITEM:BlackCurrant:INIT]END==
// ==[ITEM:Blueberry:INIT]BEGIN==
            _ = BLUEBERRY;
// ==[ITEM:Blueberry:INIT]END==
// ==[ITEM:Grape:INIT]BEGIN==
            _ = GRAPE;
// ==[ITEM:Grape:INIT]END==
// ==[ITEM:Watermelon:INIT]BEGIN==
            _ = WATERMELON;
// ==[ITEM:Watermelon:INIT]END==
// ==[ITEM:Prune:INIT]BEGIN==
            _ = PRUNE;
// ==[ITEM:Prune:INIT]END==
// ==[ITEM:Mango:INIT]BEGIN==
            _ = MANGO;
// ==[ITEM:Mango:INIT]END==
// ==[ITEM:Potato:INIT]BEGIN==
            _ = POTATO;
// ==[ITEM:Potato:INIT]END==
// ==[ITEM:Onion:INIT]BEGIN==
            _ = ONION;
// ==[ITEM:Onion:INIT]END==
// ==[ITEM:Eggplant:INIT]BEGIN==
            _ = EGGPLANT;
// ==[ITEM:Eggplant:INIT]END==
// ==[ITEM:Banana:INIT]BEGIN==
            _ = BANANA;
// ==[ITEM:Banana:INIT]END==
// ==[ITEM:Fig:INIT]BEGIN==
            _ = FIG;
// ==[ITEM:Fig:INIT]END==
// ==[ITEM:DragonFruit:INIT]BEGIN==
            _ = DRAGONFRUIT;
// ==[ITEM:DragonFruit:INIT]END==
// ==[ITEM:Tomato:INIT]BEGIN==
            _ = TOMATO;
// ==[ITEM:Tomato:INIT]END==
// ==[ITEM:Honey:INIT]BEGIN==
            _ = HONEY;
// ==[ITEM:Honey:INIT]END==
// ==[ITEM:Butter:INIT]BEGIN==
            _ = BUTTER;
// ==[ITEM:Butter:INIT]END==
// ==[ITEM:ElephantTrunk:INIT]BEGIN==
            _ = ELEPHANTTRUNK;
// ==[ITEM:ElephantTrunk:INIT]END==
// ==[ITEM:Barnacle:INIT]BEGIN==
            _ = BARNACLE;
// ==[ITEM:Barnacle:INIT]END==
// ==[ITEM:LargeHaunch:INIT]BEGIN==
            _ = LARGEHAUNCH;
// ==[ITEM:LargeHaunch:INIT]END==
// ==[ITEM:LongPork:INIT]BEGIN==
            _ = LONGPORK;
// ==[ITEM:LongPork:INIT]END==
// ==[ITEM:Drumstick:INIT]BEGIN==
            _ = DRUMSTICK;
// ==[ITEM:Drumstick:INIT]END==
// ==[ITEM:FrozenBananaDaiquiri:INIT]BEGIN==
            _ = FROZENBANANADAIQUIRI;
// ==[ITEM:FrozenBananaDaiquiri:INIT]END==
// ==[ITEM:PumpkinCake:INIT]BEGIN==
            _ = PUMPKINCAKE;
// ==[ITEM:PumpkinCake:INIT]END==
// ==[ITEM:HaunchSoup:INIT]BEGIN==
            _ = HAUNCHSOUP;
// ==[ITEM:HaunchSoup:INIT]END==
// ==[ITEM:CreamyMashedPotato:INIT]BEGIN==
            _ = CREAMYMASHEDPOTATO;
// ==[ITEM:CreamyMashedPotato:INIT]END==
// ==[ITEM:FigStuffedTrunk:INIT]BEGIN==
            _ = FIGSTUFFEDTRUNK;
// ==[ITEM:FigStuffedTrunk:INIT]END==
// ==[ITEM:Perogies:INIT]BEGIN==
            _ = PEROGIES;
// ==[ITEM:Perogies:INIT]END==
// ==[ITEM:SeafoodGumbo:INIT]BEGIN==
            _ = SEAFOODGUMBO;
// ==[ITEM:SeafoodGumbo:INIT]END==
// ==[ITEM:TurkeyDinner:INIT]BEGIN==
            _ = TURKEYDINNER;
// ==[ITEM:TurkeyDinner:INIT]END==
// ==[ITEM:RoastedWings:INIT]BEGIN==
            _ = ROASTEDWINGS;
// ==[ITEM:RoastedWings:INIT]END==
// ==[ITEM:PotatoTornado:INIT]BEGIN==
            _ = POTATOTORNADO;
// ==[ITEM:PotatoTornado:INIT]END==
// ==[ITEM:FluffyPotatoSouffle:INIT]BEGIN==
            _ = FLUFFYPOTATOSOUFFLE;
// ==[ITEM:FluffyPotatoSouffle:INIT]END==
// ==[ITEM:VegStinger:INIT]BEGIN==
            _ = VEGSTINGER;
// ==[ITEM:VegStinger:INIT]END==
// ==[ITEM:BarnacleNoodles:INIT]BEGIN==
            _ = BARNACLENOODLES;
// ==[ITEM:BarnacleNoodles:INIT]END==
// ==[ITEM:WatermelonPopsicle:INIT]BEGIN==
            _ = WATERMELONPOPSICLE;
// ==[ITEM:WatermelonPopsicle:INIT]END==
// ==[ITEM:BananaJelly:INIT]BEGIN==
            _ = BANANAJELLY;
// ==[ITEM:BananaJelly:INIT]END==
// ==[ITEM:EelSushi:INIT]BEGIN==
            _ = EELSUSHI;
// ==[ITEM:EelSushi:INIT]END==
// ==[ITEM:LobsterDinner:INIT]BEGIN==
            _ = LOBSTERDINNER;
// ==[ITEM:LobsterDinner:INIT]END==
            // ==[ZONE:FOODS_INIT]END==

            int count = typeof(ModFoods).GetFields(
                BindingFlags.Public | BindingFlags.Static).Length;
            MFLog.Info(LogCat.Food, "[vs_preset] 已注册 " + count + " 种食物属性");
        }

        // 进食管制 · 默认勾选


        //   问题：本 Mod 的食物没有【显式】进入"进食管制"的默认勾选，
        //         只靠"碰巧不在 ConsumableConsumer.forbiddenTagSet 里"。
        //         某些情况下（集合被别处填充）就会表现为默认全不勾选。
        //   修复：小人 ConsumableConsumer 初始化时，把本 Mod 的所有食物
        //         从它的禁食集合里移除 → 默认必定勾选。
        //   ⚠ 时机必须是 OnPrefabInit（反序列化【之前】）：
        //       读档时 Klei 会用存档里的 forbiddenTagSet 覆盖我们设的初值，
        //       所以【玩家手动改过的勾选状态不会被冲掉】。
        //       若改到 OnSpawn（反序列化之后）就会每次读档都覆盖玩家的设置。

        // 本 Mod 创建的食物 prefab id 总表（工厂创建时登记，自动去重）

        // id：标识 id
        public static readonly List<string> CreatedFoodIds = new List<string>();
        internal static void RegisterFoodId(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!CreatedFoodIds.Contains(id)) CreatedFoodIds.Add(id);
        }

        private static void SyncFoodIdsFromFields()
        {
            try
            {
                var fields = typeof(ModFoods).GetFields(BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < fields.Length; i++)
                {
                    try
                    {
                        var fi = fields[i].GetValue(null) as EdiblesManager.FoodInfo;
                        if (fi != null) RegisterFoodId(fi.Id);
                    }
                    catch { /* expected: 单个字段反射取值失败时跳过该字段，其余食物 id 仍照常登记 */ }
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Food, e, "[MoreFood/Food] CollectFoodIds 异常");
            }
        }

        // 本 Mod 的食物 id 清单（去重；工厂尚未登记时用反射兜底）
        public static List<string> CollectFoodIds()
        {
            if (CreatedFoodIds.Count == 0) SyncFoodIdsFromFields();
            return CreatedFoodIds;
        }

        // 把本 Mod 的所有食物从该小人的禁食集合里移除（= 默认勾选）
        public static void EnsureDefaultPermitted(ConsumableConsumer cc)
        {
            if (cc == null || cc.forbiddenTagSet == null) return;
            try
            {
                int n = 0;
                var ids = CollectFoodIds();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (cc.forbiddenTagSet.Remove(new Tag(ids[i]))) n++;
                }
                if (n > 0)
                {
                    try { cc.consumableRulesChanged.Signal(); } catch { /* expected: 信号通知失败时进食许可已改好，仅 UI 未即时刷新 */ }
                    MFLog.Info(LogCat.Food, "[MoreFood/Food] 已为小人恢复 " + n + " 种食物的默认进食许可");
                }
            }
            catch (Exception e)
            {
                MFLog.Exc(LogCat.Food, e, "[MoreFood/Food] EnsureDefaultPermitted 异常");
            }
        }
    }


    // Patch：小人 ConsumableConsumer 初始化时套用默认进食许可
    //   时机 = OnPrefabInit（早于反序列化 → 不冲掉玩家存档里的改动）

    [HarmonyPatch(typeof(ConsumableConsumer), "OnPrefabInit")]
    public static class ConsumableConsumer_DefaultPermit_Patch
    {
        // 补丁：复制人 ConsumableConsumer 初始化后，把本 mod 食物加入默认可食用清单
        public static void Postfix(ConsumableConsumer __instance)
        {
            try
            {
            ModFoods.EnsureDefaultPermitted(__instance);
            }
            catch (System.Exception e) { MFLog.HotFail("patch:Foods", e); }
        }
    }
}
