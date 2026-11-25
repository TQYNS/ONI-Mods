// IngredientConfigs.cs — 物品批量注册（IMultiEntityConfig）
// 新增物品：在 _defs 标记区内加一行 + Strings.cs 加字符串

using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace MoreFood_KFC{
    public class IngredientConfigs : IMultiEntityConfig
    {
        private struct Def
        {
            // 材料物品 ID（如 "flour"）
            public string id;
            // 材料动画名（kanim，不含路径）
            public string anim;
            // 材料显示名（ModStrings 取）
            public string name;
            // 材料描述文本
            public string desc;
            // 单份质量（kg）
            public float mass;
            // 碰撞箱宽（格）
            public float width;
            // 碰撞箱高（格）
            public float height;
            // 构成元素（Creature = 无元素属性）
            public SimHashes element;
            // 渲染图层（Grid.SceneLayer）
            public Grid.SceneLayer sceneLayer;
            // 物品菜单排序权重
            public int sortOrder;
            // 额外附加 Tag
            public List<Tag> tags;
            // 仅这些 DLC 可见（null = 全版本）
            public string[] requireDlc;
            // 这些 DLC 不可见（null = 不限）
            public string[] forbidDlc;
            // 是否按原版工艺品方式注册
            public bool artifact;
        }

        // 纯图标条目：只有 ui 贴图，不注册实体，也不进掉落 / 交易 / 百科
        private static readonly HashSet<string> IconOnlyIds = new HashSet<string> { "Fuel", "HonorPoint" };

        private static readonly Dictionary<string, string> IconOnlyAnims = new Dictionary<string, string>
        {
            { "Fuel", "fuel_kanim" },
            { "HonorPoint", "honorpoint_kanim" },
        };

        internal static bool IsIconOnlyItem(string id)
        {
            return !string.IsNullOrEmpty(id) && IconOnlyIds.Contains(id);
        }

        // 纯图标没有 prefab，直接从 kanim 的 ui 帧取图；注意本类有同名嵌套 Def，必须完全限定
        internal static Sprite IconOnlySprite(string id)
        {
            string anim;
            if (!IconOnlyAnims.TryGetValue(id, out anim)) return null;
            KAnimFile file = Assets.GetAnim(anim);
            if (file == null) return null;
            return global::Def.GetUISpriteFromMultiObjectAnim(file, "ui", false, "");
        }

        // 参数 id：物品 ID
        // 参数 anim：动画名，如 "flour_kanim"（不含路径）
        // 参数 name：显示名称
        // 参数 desc：描述
        // 参数 mass：单份质量 (kg)，默认 1
        // 参数 width：碰撞箱宽度 (格)，默认 0.8
        // 参数 height：碰撞箱高度 (格)，默认 0.4
        // 参数 element：模拟元素，默认 SimHashes.Creature（无元素属性）
        // 参数 sceneLayer：渲染图层，默认 Grid.SceneLayer.Front
        // 参数 sortOrder：UI 排序权重，默认 0
        // 参数 tags：额外标签，默认 null
        // 参数 requireDlc：需要的 DLC 数组，null=全版本。如 DlcManager.EXPANSION1
        // 参数 forbidDlc：禁止的 DLC 数组，null=不限。如 DlcManager.EXPANSION1
        // 参数 artifact：是否按原版工艺品方式注册
        private static Def Item(
            string id,
            string anim,
            string name,
            string desc,
            float mass = 1f,
            float width = 0.8f,
            float height = 0.4f,
            SimHashes element = SimHashes.Creature,
            Grid.SceneLayer sceneLayer = Grid.SceneLayer.Front,
            int sortOrder = 0,
            List<Tag> tags = null,
            string[] requireDlc = null,
            string[] forbidDlc = null,
            bool artifact = false)
        {
            var t = tags ?? new List<Tag>();

            return new Def
            {
                id = id,
                anim = anim,
                name = name,
                desc = desc,
                mass = mass,
                width = width,
                height = height,
                element = element,
                sceneLayer = sceneLayer,
                sortOrder = sortOrder,
                tags = t,
                requireDlc = requireDlc,
                forbidDlc = forbidDlc,
                artifact = artifact,
            };
        }

        private static readonly Def[] _defs =
        {
            // ==[ZONE:DEFS]BEGIN==
// ==[ITEM:Flour:DEF]BEGIN==
            Item(
                id: "Flour",
                anim: "flour_kanim",
                name: ModStrings.ITEMS.FOOD.FLOUR.NAME,
                desc: ModStrings.ITEMS.FOOD.FLOUR.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Flour:DEF]END==
// ==[ITEM:BoneSoup:DEF]BEGIN==
            Item(
                id: "BoneSoup",
                anim: "bonesoup_kanim",
                name: ModStrings.ITEMS.FOOD.BONESOUP.NAME,
                desc: ModStrings.ITEMS.FOOD.BONESOUP.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BoneSoup:DEF]END==
// ==[ITEM:ClassicCola:DEF]BEGIN==
            Item(
                id: "ClassicCola",
                anim: "classiccola_kanim",
                name: ModStrings.ITEMS.FOOD.CLASSICCOLA.NAME,
                desc: ModStrings.ITEMS.FOOD.CLASSICCOLA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:ClassicCola:DEF]END==
// ==[ITEM:ZeroSugarCola:DEF]BEGIN==
            Item(
                id: "ZeroSugarCola",
                anim: "zerosugarcola_kanim",
                name: ModStrings.ITEMS.FOOD.ZEROSUGARCOLA.NAME,
                desc: ModStrings.ITEMS.FOOD.ZEROSUGARCOLA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:ZeroSugarCola:DEF]END==
// ==[ITEM:DeerAntler:DEF]BEGIN==
            Item(
                id: "DeerAntler",
                anim: "deerantler_kanim",
                name: ModStrings.ITEMS.FOOD.DEERANTLER.NAME,
                desc: ModStrings.ITEMS.FOOD.DEERANTLER.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:DeerAntler:DEF]END==
// ==[ITEM:FishBone:DEF]BEGIN==
            Item(
                id: "FishBone",
                anim: "fishbone_kanim",
                name: ModStrings.ITEMS.FOOD.FISHBONE.NAME,
                desc: ModStrings.ITEMS.FOOD.FISHBONE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:FishBone:DEF]END==
// ==[ITEM:LargeBone:DEF]BEGIN==
            Item(
                id: "LargeBone",
                anim: "largebone_kanim",
                name: ModStrings.ITEMS.FOOD.LARGEBONE.NAME,
                desc: ModStrings.ITEMS.FOOD.LARGEBONE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:LargeBone:DEF]END==
// ==[ITEM:CuminPowder:DEF]BEGIN==
            Item(
                id: "CuminPowder",
                anim: "cuminpowder_kanim",
                name: ModStrings.ITEMS.FOOD.CUMINPOWDER.NAME,
                desc: ModStrings.ITEMS.FOOD.CUMINPOWDER.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CuminPowder:DEF]END==
// ==[ITEM:SoySauce:DEF]BEGIN==
            Item(
                id: "SoySauce",
                anim: "soysauce_kanim",
                name: ModStrings.ITEMS.FOOD.SOYSAUCE.NAME,
                desc: ModStrings.ITEMS.FOOD.SOYSAUCE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SoySauce:DEF]END==
// ==[ITEM:SantosBean:DEF]BEGIN==
            Item(
                id: "SantosBean",
                anim: "santosbean_kanim",
                name: ModStrings.ITEMS.FOOD.SANTOSBEAN.NAME,
                desc: ModStrings.ITEMS.FOOD.SANTOSBEAN.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:SantosBean:DEF]END==
// ==[ITEM:YirgacheffeBean:DEF]BEGIN==
            Item(
                id: "YirgacheffeBean",
                anim: "yirgacheffebean_kanim",
                name: ModStrings.ITEMS.FOOD.YIRGACHEFFEBEAN.NAME,
                desc: ModStrings.ITEMS.FOOD.YIRGACHEFFEBEAN.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:YirgacheffeBean:DEF]END==
// ==[ITEM:WhiteWhaleVodka:DEF]BEGIN==
            Item(
                id: "WhiteWhaleVodka",
                anim: "whitewhalevodka_kanim",
                name: ModStrings.ITEMS.FOOD.WHITEWHALEVODKA.NAME,
                desc: ModStrings.ITEMS.FOOD.WHITEWHALEVODKA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:WhiteWhaleVodka:DEF]END==
// ==[ITEM:RedWine:DEF]BEGIN==
            Item(
                id: "RedWine",
                anim: "redwine_kanim",
                name: ModStrings.ITEMS.FOOD.REDWINE.NAME,
                desc: ModStrings.ITEMS.FOOD.REDWINE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:RedWine:DEF]END==
// ==[ITEM:Brandy:DEF]BEGIN==
            Item(
                id: "Brandy",
                anim: "brandy_kanim",
                name: ModStrings.ITEMS.FOOD.BRANDY.NAME,
                desc: ModStrings.ITEMS.FOOD.BRANDY.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Brandy:DEF]END==
// ==[ITEM:PureWheatBeer:DEF]BEGIN==
            Item(
                id: "PureWheatBeer",
                anim: "purewheatbeer_kanim",
                name: ModStrings.ITEMS.FOOD.PUREWHEATBEER.NAME,
                desc: ModStrings.ITEMS.FOOD.PUREWHEATBEER.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:PureWheatBeer:DEF]END==
// ==[ITEM:GruitAle:DEF]BEGIN==
            Item(
                id: "GruitAle",
                anim: "gruitale_kanim",
                name: ModStrings.ITEMS.FOOD.GRUITALE.NAME,
                desc: ModStrings.ITEMS.FOOD.GRUITALE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:GruitAle:DEF]END==
// ==[ITEM:Rum:DEF]BEGIN==
            Item(
                id: "Rum",
                anim: "rum_kanim",
                name: ModStrings.ITEMS.FOOD.RUM.NAME,
                desc: ModStrings.ITEMS.FOOD.RUM.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Rum:DEF]END==
// ==[ITEM:Moutai:DEF]BEGIN==
            Item(
                id: "Moutai",
                anim: "moutai_kanim",
                name: ModStrings.ITEMS.FOOD.MOUTAI.NAME,
                desc: ModStrings.ITEMS.FOOD.MOUTAI.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Moutai:DEF]END==
// ==[ITEM:RiceWine:DEF]BEGIN==
            Item(
                id: "RiceWine",
                anim: "ricewine_kanim",
                name: ModStrings.ITEMS.FOOD.RICEWINE.NAME,
                desc: ModStrings.ITEMS.FOOD.RICEWINE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:RiceWine:DEF]END==
// ==[ITEM:MunichBeer:DEF]BEGIN==
            Item(
                id: "MunichBeer",
                anim: "munichbeer_kanim",
                name: ModStrings.ITEMS.FOOD.MUNICHBEER.NAME,
                desc: ModStrings.ITEMS.FOOD.MUNICHBEER.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:MunichBeer:DEF]END==
// ==[ITEM:LimeCitrusWine:DEF]BEGIN==
            Item(
                id: "LimeCitrusWine",
                anim: "limecitruswine_kanim",
                name: ModStrings.ITEMS.FOOD.LIMECITRUSWINE.NAME,
                desc: ModStrings.ITEMS.FOOD.LIMECITRUSWINE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:LimeCitrusWine:DEF]END==
// ==[ITEM:BlueberrySparklingWine:DEF]BEGIN==
            Item(
                id: "BlueberrySparklingWine",
                anim: "blueberrysparklingwine_kanim",
                name: ModStrings.ITEMS.FOOD.BLUEBERRYSPARKLINGWINE.NAME,
                desc: ModStrings.ITEMS.FOOD.BLUEBERRYSPARKLINGWINE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BlueberrySparklingWine:DEF]END==
// ==[ITEM:IcedCoffee:DEF]BEGIN==
            Item(
                id: "IcedCoffee",
                anim: "icedcoffee_kanim",
                name: ModStrings.ITEMS.FOOD.ICEDCOFFEE.NAME,
                desc: ModStrings.ITEMS.FOOD.ICEDCOFFEE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:IcedCoffee:DEF]END==
// ==[ITEM:Espresso:DEF]BEGIN==
            Item(
                id: "Espresso",
                anim: "espresso_kanim",
                name: ModStrings.ITEMS.FOOD.ESPRESSO.NAME,
                desc: ModStrings.ITEMS.FOOD.ESPRESSO.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Espresso:DEF]END==
// ==[ITEM:RetroEspresso:DEF]BEGIN==
            Item(
                id: "RetroEspresso",
                anim: "retroespresso_kanim",
                name: ModStrings.ITEMS.FOOD.RETROESPRESSO.NAME,
                desc: ModStrings.ITEMS.FOOD.RETROESPRESSO.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:RetroEspresso:DEF]END==
// ==[ITEM:CoffeeDoubleSkinJelly:DEF]BEGIN==
            Item(
                id: "CoffeeDoubleSkinJelly",
                anim: "coffeedoubleskinjelly_kanim",
                name: ModStrings.ITEMS.FOOD.COFFEEDOUBLESKINJELLY.NAME,
                desc: ModStrings.ITEMS.FOOD.COFFEEDOUBLESKINJELLY.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:CoffeeDoubleSkinJelly:DEF]END==
// ==[ITEM:Mocha:DEF]BEGIN==
            Item(
                id: "Mocha",
                anim: "mocha_kanim",
                name: ModStrings.ITEMS.FOOD.MOCHA.NAME,
                desc: ModStrings.ITEMS.FOOD.MOCHA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Mocha:DEF]END==
// ==[ITEM:PineappleBeer:DEF]BEGIN==
            Item(
                id: "PineappleBeer",
                anim: "pineapplebeer_kanim",
                name: ModStrings.ITEMS.FOOD.PINEAPPLEBEER.NAME,
                desc: ModStrings.ITEMS.FOOD.PINEAPPLEBEER.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:PineappleBeer:DEF]END==
// ==[ITEM:OrangeSoda:DEF]BEGIN==
            Item(
                id: "OrangeSoda",
                anim: "orangesoda_kanim",
                name: ModStrings.ITEMS.FOOD.ORANGESODA.NAME,
                desc: ModStrings.ITEMS.FOOD.ORANGESODA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:OrangeSoda:DEF]END==
// ==[ITEM:BlackcurrantSoda:DEF]BEGIN==
            Item(
                id: "BlackcurrantSoda",
                anim: "blackcurrantsoda_kanim",
                name: ModStrings.ITEMS.FOOD.BLACKCURRANTSODA.NAME,
                desc: ModStrings.ITEMS.FOOD.BLACKCURRANTSODA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BlackcurrantSoda:DEF]END==
// ==[ITEM:BlueberrySoda:DEF]BEGIN==
            Item(
                id: "BlueberrySoda",
                anim: "blueberrysoda_kanim",
                name: ModStrings.ITEMS.FOOD.BLUEBERRYSODA.NAME,
                desc: ModStrings.ITEMS.FOOD.BLUEBERRYSODA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:BlueberrySoda:DEF]END==
// ==[ITEM:WatermelonSoda:DEF]BEGIN==
            Item(
                id: "WatermelonSoda",
                anim: "watermelonsoda_kanim",
                name: ModStrings.ITEMS.FOOD.WATERMELONSODA.NAME,
                desc: ModStrings.ITEMS.FOOD.WATERMELONSODA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:WatermelonSoda:DEF]END==
// ==[ITEM:XiMeiSoda:DEF]BEGIN==
            Item(
                id: "XiMeiSoda",
                anim: "ximeisoda_kanim",
                name: ModStrings.ITEMS.FOOD.XIMEISODA.NAME,
                desc: ModStrings.ITEMS.FOOD.XIMEISODA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:XiMeiSoda:DEF]END==
// ==[ITEM:MangoSoda:DEF]BEGIN==
            Item(
                id: "MangoSoda",
                anim: "mangosoda_kanim",
                name: ModStrings.ITEMS.FOOD.MANGOSODA.NAME,
                desc: ModStrings.ITEMS.FOOD.MANGOSODA.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:MangoSoda:DEF]END==
// ==[ITEM:Koji:DEF]BEGIN==
            Item(
                id: "Koji",
                anim: "koji_kanim",
                name: ModStrings.ITEMS.FOOD.KOJI.NAME,
                desc: ModStrings.ITEMS.FOOD.KOJI.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Koji:DEF]END==
// ==[ITEM:FreshMilk:DEF]BEGIN==
            Item(
                id: "FreshMilk",
                anim: "freshmilk_kanim",
                name: ModStrings.ITEMS.FOOD.FRESHMILK.NAME,
                desc: ModStrings.ITEMS.FOOD.FRESHMILK.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:FreshMilk:DEF]END==
// ==[ITEM:Chocolate:DEF]BEGIN==
            Item(
                id: "Chocolate",
                anim: "chocolate_kanim",
                name: ModStrings.ITEMS.FOOD.CHOCOLATE.NAME,
                desc: ModStrings.ITEMS.FOOD.CHOCOLATE.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialIngredient },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Chocolate:DEF]END==
// ==[ITEM:Fuel:DEF]BEGIN==
            Item(
                id: "Fuel",
                anim: "fuel_kanim",
                name: ModStrings.OBJECT.FUEL.NAME,
                desc: ModStrings.OBJECT.FUEL.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialProduct },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:Fuel:DEF]END==
// ==[ITEM:StarMapIntel:DEF]BEGIN==
            Item(
                id: "StarMapIntel",
                anim: "intel_kanim",
                name: ModStrings.OBJECT.STARMAPINTEL.NAME,
                desc: ModStrings.OBJECT.STARMAPINTEL.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialProduct },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:StarMapIntel:DEF]END==
// ==[ITEM:HonorPoint:DEF]BEGIN==
            Item(
                id: "HonorPoint",
                anim: "honorpoint_kanim",
                name: ModStrings.OBJECT.HONORPOINT.NAME,
                desc: ModStrings.OBJECT.HONORPOINT.DESC,
                mass: 1.0f,
                width: 0.8f,
                height: 0.4f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Front,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.IndustrialProduct },
                requireDlc: null,
                forbidDlc: null),
// ==[ITEM:HonorPoint:DEF]END==
// ==[ITEM:FuelCell:DEF]BEGIN==
            Item(
                id: "FuelCell",
                anim: "fuelcell_kanim",
                name: ModStrings.OBJECT.FUELCELL.NAME,
                desc: ModStrings.OBJECT.FUELCELL.DESC,
                mass: 1.0f,
                width: 1.0f,
                height: 1.0f,
                element: SimHashes.Creature,
                sceneLayer: Grid.SceneLayer.Ore,
                sortOrder: 0,
                tags: new List<Tag> { GameTags.MiscPickupable },
                requireDlc: null,
                forbidDlc: null,
                artifact: true),
// ==[ITEM:FuelCell:DEF]END==
            // ==[ZONE:DEFS]END==
        };

        public List<GameObject> CreatePrefabs()
        {
            var list = new List<GameObject>(_defs.Length);
            foreach (var d in _defs)
            {
                if (IsIconOnlyItem(d.id)) continue;   // 纯图标不注册实体，ui 由 IconOnlySprite 直取
                list.Add(Create(d));
            }
            return list;
        }

        // 标准散落物创建的唯一入口。
        //   IngredientConfigs 的 37 个原料 / FoodConfigs 之外的散落物、
        //   以及 Space 的模块 / 弹药物品统一走这里，保证
        //   sceneLayer、碰撞形状与尺寸、EntitySplitter 完全一致。
        //   标准口径写死为 Front 层 + RECTANGLE 0.8×0.4 + EntitySplitter，
        //   仅 id/名/描述/动画/tags 与尺寸类参数可覆盖。
        // 禁止开放 collisionShape / unitMass / EntitySplitter 开关：
        //     《战斗.md》§3.7 已实证 CIRCLE 0.35 会把 transform 抬高 0.36 格造成
        //     散落物悬空，卷轴曾因「保持旧行为」回退该参数而复发。碰撞形状一律
        //     RECTANGLE，任何散落物都不得例外。
        internal static GameObject CreateStandard(
            string id, string name, string desc, KAnimFile anim, List<Tag> tags,
            float mass = 1f,
            SimHashes element = SimHashes.Creature,
            Grid.SceneLayer sceneLayer = Grid.SceneLayer.Front,
            float width = 0.8f,
            float height = 0.4f,
            int sortOrder = 0,
            string[] requireDlc = null,
            string[] forbidDlc = null)
        {
            GameObject go = EntityTemplates.CreateLooseEntity(
                id: id,
                name: name,
                desc: desc,
                mass: mass,
                unitMass: false,
                anim: anim,
                initialAnim: "object",
                sceneLayer: sceneLayer,
                collisionShape: EntityTemplates.CollisionShape.RECTANGLE,
                width: width,
                height: height,
                isPickupable: true,
                sortOrder: sortOrder,
                element: element,
                additionalTags: tags);

            KPrefabID kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                kpid.requiredDlcIds = requireDlc;
                kpid.forbiddenDlcIds = forbidDlc;
            }

            go.AddOrGet<EntitySplitter>();

            return go;
        }

        private static GameObject Create(Def d)
        {
            if (d.artifact)
            {
                return CreateArtifact(
                    d.id, d.name, d.desc, Assets.GetAnim(d.anim), d.tags,
                    d.mass, d.element, d.width, d.height,
                    d.requireDlc, d.forbidDlc);
            }
            GameObject go = CreateStandard(
                d.id, d.name, d.desc, Assets.GetAnim(d.anim), d.tags,
                d.mass, d.element, d.sceneLayer, d.width, d.height,
                d.sortOrder, d.requireDlc, d.forbidDlc);
            if (d.id == StarMapIntel.PrefabId)
            {
                StarMapIntel.AttachLore(go);   // 星图数据：阅读即解析，消耗 1 份
            }
            return go;
        }

        // 仿原版 ArtifactConfig.CreateArtifact：工艺品散落物 + 展示组件 + 工艺品 Tag
        internal static GameObject CreateArtifact(
            string id, string name, string desc, KAnimFile anim, List<Tag> tags,
            float mass = 1f,
            SimHashes element = SimHashes.Creature,
            float width = 1f,
            float height = 1f,
            string[] requireDlc = null,
            string[] forbidDlc = null)
        {
            GameObject go = EntityTemplates.CreateLooseEntity(
                id: id,
                name: name,
                desc: desc,
                mass: mass,
                unitMass: true,
                anim: anim,
                initialAnim: "object",
                sceneLayer: Grid.SceneLayer.Ore,
                collisionShape: EntityTemplates.CollisionShape.RECTANGLE,
                width: width,
                height: height,
                isPickupable: true,
                sortOrder: SORTORDER.ARTIFACTS,
                element: element,
                additionalTags: tags);

            go.AddOrGet<OccupyArea>().SetCellOffsets(EntityTemplates.GenerateOffsets(1, 1));
            DecorProvider decor = go.AddOrGet<DecorProvider>();
            decor.SetValues(DECOR.SPACEARTIFACT.TIER0.decorValues);
            decor.overrideName = go.GetProperName();
            SpaceArtifact art = go.AddOrGet<SpaceArtifact>();
            art.SetUIAnim("ui");
            art.SetArtifactTier(DECOR.SPACEARTIFACT.TIER0);
            art.uniqueAnimNameFragment = "object";
            art.artifactType = ArtifactType.Any;
            go.AddOrGet<KSelectable>();
            Pickupable pickup = go.GetComponent<Pickupable>();
            if (pickup != null) pickup.deleteOffGrid = false;
            KPrefabID kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                kpid.requiredDlcIds = requireDlc;
                kpid.forbiddenDlcIds = forbidDlc;
                kpid.AddTag(GameTags.PedestalDisplayable, false);
                kpid.AddTag(GameTags.Artifact, false);
                kpid.AddTag(GameTags.Ornament, false);

                // ArtifactTier / ArtifactType 是普通引用类型字段，Unity 实例化时不复制，
                // 实例上会是 null → SpaceArtifact.GetEffectDescriptions() 里
                // artifactTier.payloadDropChance 空引用（点开详情即崩）。
                // 原版 ArtifactConfig.CreateArtifact 用 prefabSpawnFn 在每次 spawn 时补一次。
                kpid.prefabSpawnFn += delegate (GameObject instance)
                {
                    SpaceArtifact inst = instance.GetComponent<SpaceArtifact>();
                    if (inst != null)
                    {
                        inst.SetArtifactTier(DECOR.SPACEARTIFACT.TIER0);
                        inst.artifactType = ArtifactType.Any;
                    }
                };
            }
            return go;
        }

        public void OnPrefabInit(GameObject inst) { }
        public void OnSpawn(GameObject inst) { }
    }
}
