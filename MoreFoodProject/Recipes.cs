// Recipes.cs — 配方注册（配方创建器生成 + 可手动编辑）
// 每个建筑一个注册方法（ZONE:RECIPES 内）+ 一个 Harmony 补丁（ZONE:PATCHES 内）
// 手动添加配方：在该建筑注册方法内加一行 Brew(...).In(...).Out(...).Build()

using System.Collections.Generic;
using HarmonyLib;

namespace MoreFood_KFC
{
    public static class ModRecipes
    {
        // ==[ZONE:RECIPES]BEGIN==

// ==[ITEM:RB:GourmetCookingStation:REG]BEGIN==
        // ════ GourmetCookingStation 配方（24 条）════
        public static void RegisterGourmetCookingStation()
        {
            // 嫩肋排剁段，下锅煎至两面焦黄。锅里留油，下糖炒出糖色，排骨倒入翻匀上色。加酱油大骨汤，大火烧开转小火慢烧。烧到肉烂脱骨，汤汁收浓，稠厚挂勺。浇在热米饭上，酱红油亮。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.SAUCEBRAISEDRIBS.DESC, 8)
                .In("SmokedDinosaurMeat", 0.12f)  // ==[ERR:0]==
                .In("CookedRice", 1.0f)  // ==[ERR:0]==
                .In("SoySauce", 0.1f)  // ==[ERR:0]==
                .Out("SauceBraisedRibs", 1.0f)  // ==[ERR:0]==
                .Build();
            // 鸡蛋打散，兑温水，过筛撇沫。上笼旺火蒸到凝脂。肉末下锅煸炒，逼出油，加酱油葱姜，炒到粒粒酥香。连油带肉浇在蛋羹上，油汁渗进蛋里，淋香油，撒葱花。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.STEAMEDEGGWITHMEAT.DESC, 8)
                .In("Meat", 0.5f)  // ==[ERR:0]==
                .In("RawEgg", 1.0f)  // ==[ERR:0]==
                .In("Tallow", 0.1f)  // ==[ERR:0]==
                .Out("SteamedEggWithMeat", 1.0f)  // ==[ERR:0]==
                .Build();
            // 鸡肉斩块，蘑菇温水泡发，泡菇水留着，锅里下油，鸡块倒入煸炒，炒到皮边出油。蘑菇下锅同炒，倒泡菇水，添大骨汤没过。大火烧开，转小火慢炖。炖到鸡肉脱骨，蘑菇吸饱汤，汤色转浓。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.CHICKENWITHMUSHROOM.DESC, 8)
                .In("CrispyRoastChicken", 1.0f)  // ==[ERR:0]==
                .In("Mushroom", 0.5f)  // ==[ERR:0]==
                .In("BoneSoup", 0.3f)  // ==[ERR:0]==
                .Out("ChickenWithMushroom", 1.0f)  // ==[ERR:0]==
                .Build();
            // 谷粒挑净，淘洗几遍，去杂去瘪。入锅，加水没过一指。大火烧开，水面翻滚，转小火慢焖。米粒在锅里一点点胀开，淀粉化成甜味，米香慢慢聚拢。焖到米粒饱满透亮，粒粒分明，不粘不连，软糯弹牙。米香清甜，回味绵长。
            Brew("GourmetCookingStation", 30.0f, ModStrings.RECIPES.COOKEDRICE.DESC, 9)
                .In(new Tag[] { "ColdWheatSeed", "FernFood", "BasicPlantFood" },
                    new float[] { 3.0f, 3.0f, 2.0f })  // ==[ERR:0, 0, 0]==
                .Out("CookedRice", 1.0f)  // ==[ERR:0]==
                .Build();
            // 大骨或鱼骨敲开，冷水下锅，加姜片葱段。大火烧开，撇净浮沫，转小火慢滚。骨中胶质与油脂一点点溶入汤中，汤色由清转浓，鲜味慢慢析出。滚到汤色醇厚，胶质稠滑，汤面浮着油花。滤去骨渣，汤清味浓，鲜香醇和，脂香温润。
            Brew("GourmetCookingStation", 100.0f, ModStrings.RECIPES.BONESOUP.DESC, 9)
                .In(new Tag[] { "FishBone", "LargeBone" },
                    new float[] { 10.0f, 10.0f })  // ==[ERR:0, 0]==
                .In("Water", 1.0f)  // ==[ERR:0]==
                .Out("BoneSoup", 5.0f)  // ==[ERR:0]==
                .Build();
            // 蟹肉球码进碗里，兑入大骨汤，汤没过一半，上笼旺火蒸，火候掐准。蟹球慢慢定型，鲜味凝住不散，蒸到通体雪白饱满，汤清味浓。原汤清亮见底，汤色透亮，鲜气扑鼻。汤鲜味醇，蟹香清甜，嫩滑回甘，鲜味绵长。
            Brew("GourmetCookingStation", 35.0f, ModStrings.RECIPES.STEAMEDCRABBALLS.DESC, 10)
                .In("CrabMeatBalls", 1.0f)  // ==[ERR:0]==
                .In("BoneSoup", 0.3f)  // ==[ERR:0]==
                .Out("SteamedCrabBalls", 1.0f)  // ==[ERR:0]==
                .Build();
            // 蟹肉球拍上干粉，油锅烧滚，辣椒段下锅爆香，蟹肉球倒入，大火翻匀，翻炒均匀，炒到表面焦黄。淋醋，沿锅边一炝，酸香窜起。加酱汁，大火收浓，裹住蟹球，红亮油润。辣香扑鼻，酸鲜开胃，鲜辣回甜，锅气十足。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.STIRFRIEDHAIRYCRAB.DESC, 10)
                .In("CrabMeatBalls", 1.0f)  // ==[ERR:0]==
                .In("SpiceNut", 1.0f)  // ==[ERR:0]==
                .In("Tallow", 0.1f)  // ==[ERR:0]==
                .Out("StirFriedHairyCrab", 1.0f)  // ==[ERR:0]==
                .Build();
            // 汉堡上锅烙热，汉堡胚烤到微焦，肉饼煎出油，煎到两面焦黄，果薯条下锅复炸，炸到金黄酥脆，外脆里糯，捞出沥油。冰镇可乐开盖，气泡翻涌，凉气直冒。三样拼一盘，汉堡夹好，薯条堆边，可乐放旁。肉香四溢，薯香扑鼻，冰爽解腻。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.CRAZYTHURSDAY.DESC, 10)
                .In("BeefBurger", 1.0f)  // ==[ERR:0]==
                .In("FriesCarrot", 0.5f)  // ==[ERR:0]==
                .In(new Tag[] { "ClassicCola", "ZeroSugarCola" },
                    new float[] { 1.0f, 1.0f })  // ==[ERR:0, 0]==
                .Out("CrazyThursday", 1.0f)  // ==[ERR:0]==
                .Build();
            // 冰霜面包入炉烤热，烤到表面微焦，边缘起酥，肉饼下锅煎，煎到两面焦黄，油脂逼出，西红柿洗净切片，酱汁抹匀。面包剖开，码上西红柿，放上肉饼，夹紧。面包焦香，肉饼多汁，西红柿酸甜，酱香浓郁，咸鲜适口。
            Brew("GourmetCookingStation", 25.0f, ModStrings.RECIPES.BEEFBURGER.DESC, 10)
                .In("ColdWheatBread", 1.0f)  // ==[ERR:0]==
                .In("Meat", 0.5f)  // ==[ERR:0]==
                .Out("BeefBurger", 1.0f)  // ==[ERR:0]==
                .Build();
            // 五花肉切块，冷水下锅焯透，捞出控水，锅里煸出肥油，姜片葱段一炝，糖色炒红，加酱油大骨汤，转小火慢炖，炖到肉皮软糯起胶，肥肉透亮，汤汁收浓。米饭盛碗，连肉带汁往上一浇，酱香浓郁，肉香扑鼻，咸甜适口。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.BRAISEDPORKRICEBOWL.DESC, 10)
                .In("CookedMeat", 0.25f)  // ==[ERR:0]==
                .In("CookedRice", 1.0f)  // ==[ERR:0]==
                .Out("BraisedPorkRiceBowl", 1.0f)  // ==[ERR:0]==
                .Build();
            // 鹿茸切片，温水泡软，甲鱼宰净剁块，冷水下锅焯透去腥，砂锅添大骨汤，甲鱼鹿茸同下，加姜片枸杞，大火烧开，转小火慢炖，炖到汤色转浓，甲鱼肉酥烂脱骨，鹿茸味全化进汤里，撒盐调味，汤醇味厚，鲜香浓郁，回味醇和。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.DEERANTLERTONICSOUP.DESC, 10)
                .In("DeerAntler", 1.0f)  // ==[ERR:0]==
                .In("BoneSoup", 1.0f)  // ==[ERR:0]==
                .In("SoftshellTurtleMeat", 1.0f)  // ==[ERR:0]==
                .Out("DeerAntlerTonicSoup", 1.0f)  // ==[ERR:0]==
                .Build();
            // 牛腱子洗净，冷水下锅焯透，撇净浮沫，锅里下酱油盐，加葱姜香料和干辣椒，添水烧开，牛腱下锅，小火慢慢酱，汤面浮着油花，酱到肉色红亮，筷子扎透，切成薄片，筋花透亮，酱香浓郁，咸鲜微辣，肉香扑鼻。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.BRAISEDBEEFINSOYSAUCE.DESC, 10)
                .In("BeefMeat", 1.0f)  // ==[ERR:0]==
                .In("SoySauce", 0.2f)  // ==[ERR:0]==
                .In("Salt", 0.1f)  // ==[ERR:0]==
                .Out("BraisedBeefInSoySauce", 1.0f)  // ==[ERR:0]==
                .Build();
            // 牛肉切块，冷水下锅焯透，撇净浮沫，捞出控水，锅里下油，姜片葱段爆香，牛肉块倒入煸炒，炒到表面收紧，加酱油上色，添大骨汤没过，大火烧开，转小火慢炖，炖到肉烂，下羽叶果薯，炖到薯块软糯，汤汁收浓，肉香浓郁，咸鲜醇厚，薯香清甜。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.STEWEDBEEF.DESC, 10)
                .In("BeefMeat", 1.0f)  // ==[ERR:0]==
                .In("BoneSoup", 0.3f)  // ==[ERR:0]==
                .In("Carrot", 0.025f)  // ==[ERR:0]==
                .Out("StewedBeef", 1.0f)  // ==[ERR:0]==
                .Build();
            // 甲鱼宰净，剁成大块，滚水焯透去腥，锅里下油，姜片葱段爆香，甲鱼块下锅煸炒，加酱油糖酒，添大骨汤没过，小火慢慢炖，炖到裙边软糯，肉烂脱骨不散，汤汁收浓，裙边透亮，酱色红亮，酱香浓郁，咸鲜醇厚，胶质挂唇。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.BRAISEDSOFTSHELLTURTLE.DESC, 10)
                .In("SoftshellTurtleMeat", 1.0f)  // ==[ERR:0]==
                .In("Tallow", 0.1f)  // ==[ERR:0]==
                .In("SpiceNut", 1.0f)  // ==[ERR:0]==
                .Out("BraisedSoftshellTurtle", 1.0f)  // ==[ERR:0]==
                .Build();
            // 豆奶入锅加糖，小火慢煮，煮到糖化，豆奶微温。鸡蛋磕碗里打散，搅到蛋液均匀，兑入温豆奶，边倒边搅，搅匀过筛两遍，去筋去沫，倒进模具，上笼小火蒸，蒸到凝住不晃，蛋香豆香交融，甜润细腻，软嫩滑口。
            Brew("GourmetCookingStation", 30.0f, ModStrings.RECIPES.PUDDING.DESC, 14)
                .In("SoyMilk", 1.0f)  // ==[ERR:0]==
                .In("RawEgg", 0.3f)  // ==[ERR:0]==
                .In("Sucrose", 0.5f)  // ==[ERR:0]==
                .Out("Pudding", 1.0f)  // ==[ERR:0]==
                .Build();
            // 香蕉剥皮切段，冻到硬实，压成细泥。蜂蜜入锅小火化开，搅成蜜浆，晾到温凉。拌进蕉泥，兑入碎冰，慢搅到冰沙细密，融成一体。入杯堆出尖角，杯壁挂霜，插一片冻蕉。蕉香浓甜，凉气透齿，入口即化。
            Brew("GourmetCookingStation", 32.0f, ModStrings.RECIPES.FROZENBANANADAIQUIRI.DESC, 18)
                .In("Banana", 1.0f)  // ==[ERR:0]==
                .In("Honey", 0.5f)  // ==[ERR:0]==
                .In("Ice", 1.0f)  // ==[ERR:0]==
                .Out("FrozenBananaDaiquiri", 1.0f)  // ==[ERR:0]==
                .Build();
            // 大块腿肉入锅，大火逼出浮沫，撇净捞出。砂锅添大骨汤，肉块下锅，动物油脂一并入锅。大火催开，转小火慢煨，汤面始终微沸。肉纤维一点点松开，油脂化进汤里，汤色由清转白。煨到筷子一扎即透，撇去浮油。汤浓味厚，肉酥不散。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.HAUNCHSOUP.DESC, 18)
                .In("LargeHaunch", 1.5f)  // ==[ERR:0]==
                .In("BoneSoup", 0.5f)  // ==[ERR:0]==
                .In("Tallow", 0.2f)  // ==[ERR:0]==
                .Out("HaunchSoup", 1.0f)  // ==[ERR:0]==
                .Build();
            // 象鼻收拾干净，滚水焯透，捞出晾凉，顺长剖开不切断。无花果切碎，塞进剖口，压实抹平。码进砂锅，添汤没过一半，大火烧开转小火慢煨。果皮一点点化开，蜜汁渗进肉里，胶皮渐软。煨到筷子能透，汤汁收浓挂勺，起锅切段。甜咸交织，胶质挂唇。
            Brew("GourmetCookingStation", 60.0f, ModStrings.RECIPES.FIGSTUFFEDTRUNK.DESC, 18)
                .In("ElephantTrunk", 2.0f)  // ==[ERR:0]==
                .In("Fig", 2.0f)  // ==[ERR:0]==
                .Out("FigStuffedTrunk", 1.0f)  // ==[ERR:0]==
                .Build();
            // 面粉兑水，揉成柔韧的面团，醒到光滑。菜叶焯水挤干，剁碎，与肉末同拌成馅，顺一个方向搅上劲。擀薄扣出圆皮，包进馅心，捏紧花边。上笼旺火足汽蒸到皮透，取出沥干。平锅刷薄油，饺子码入，小火煎到底面微焦。外韧里鲜，汁水锁住。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.PEROGIES.DESC, 18)
                .In("Flour", 0.5f)  // ==[ERR:0]==
                .In(new Tag[] { "Lettuce", "Mushroom" },
                    new float[] { 3.0f, 0.5f })  // ==[ERR:0, 0]==
                .In("Meat", 1.0f)  // ==[ERR:0]==
                .Out("Perogies", 1.0f)  // ==[ERR:0]==
                .Build();
            // 鱼片切块，或取生壳肉挑净碎壳，海生菜撕成小片。主料先下锅，大火一滚即转小火，添大骨汤没过，汤面保持微沸，鲜味一层层析出。海生菜最后撒入，断生即起，汤色转浓。撇净浮沫，稍收汁。鱼嫩贝甜，海味层叠，汤稠味厚。
            Brew("GourmetCookingStation", 50.0f, ModStrings.RECIPES.SEAFOODGUMBO.DESC, 18)
                .In(new Tag[] { "FishMeat", "ShellfishMeat" },
                    new float[] { 1.0f, 1.0f })  // ==[ERR:0, 0]==
                .In(new Tag[] { "Lettuce", "Mushroom" },
                    new float[] { 3.0f, 0.5f })  // ==[ERR:0, 0]==
                .In("BoneSoup", 0.3f)  // ==[ERR:0]==
                .Out("SeafoodGumbo", 1.0f)  // ==[ERR:0]==
                .Build();
            // 土豆蒸透压泥，过筛到无颗粒。黄油化开，拌进薯泥。蛋清蛋黄分开，蛋清打到立尖，分次切进薯糊，轻翻不消泡。模具抹油，倒入八分满，抹平表面，入炉中火慢烤。面糊一点点鼓起，表层转金，不晃不塌。出炉即上桌，轻盈蓬松，蛋奶香浓。
            Brew("GourmetCookingStation", 45.0f, ModStrings.RECIPES.FLUFFYPOTATOSOUFFLE.DESC, 18)
                .In("Potato", 1.0f)  // ==[ERR:0]==
                .In("RawEgg", 0.75f)  // ==[ERR:0]==
                .In("Butter", 0.25f)  // ==[ERR:0]==
                .Out("FluffyPotatoSouffle", 1.0f)  // ==[ERR:0]==
                .Build();
            // 西红柿烫皮去籽，或取海生菜撕净、蘑菇去蒂切片。料入锅小火煮开，撇净浮沫，细纱滤过两遍，汁色清亮。晾到凉透，沿杯壁缓缓冲下，轻搅几圈。辛香先起，酸甜随后，凉气收尾。
            Brew("GourmetCookingStation", 32.0f, ModStrings.RECIPES.VEGSTINGER.DESC, 18)
                .In(new Tag[] { "Tomato", "Lettuce", "Mushroom" },
                    new float[] { 3.0f, 3.0f, 0.5f })  // ==[ERR:0, 0, 0]==
                .In("Water", 0.5f)  // ==[ERR:0]==
                .Out("VegStinger", 1.0f)  // ==[ERR:0]==
                .Build();
            // 藤壶撬开取肉，挑净碎壳，顺纹撕成细丝。面粉兑水揉成硬团，擀薄切细，抖散下锅，滚两滚捞起过凉。藤壶丝下锅一煸，添水烧滚，连汤收浓。面细而滑，盛碗浇卤。汤清见底，鲜味全在里头。
            Brew("GourmetCookingStation", 40.0f, ModStrings.RECIPES.BARNACLENOODLES.DESC, 18)
                .In("Barnacle", 2.0f)  // ==[ERR:0]==
                .In("Flour", 1.0f)  // ==[ERR:0]==
                .Out("BarnacleNoodles", 1.0f)  // ==[ERR:0]==
                .Build();
            // 西瓜剖开取瓤，去籽压汁，细纱滤清。瓜汁入锅小火熬到微稠，晾到温凉，灌入模具，插好木签。低温凝冻，静置到冻实不晃。脱模取出，表面结一层薄霜，咬开冰晶细密。瓜香清甜，凉气透齿。
            Brew("GourmetCookingStation", 25.0f, ModStrings.RECIPES.WATERMELONPOPSICLE.DESC, 18)
                .In("Watermelon", 4.0f)  // ==[ERR:0]==
                .In("Ice", 1.0f)  // ==[ERR:0]==
                .Out("WatermelonPopsicle", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:GourmetCookingStation:REG]END==
// ==[ITEM:RB:Smoker:REG]BEGIN==
        // ════ Smoker 配方（4 条，配方创建器生成）════
        public static void RegisterSmoker()
        {
            // 肉切块，肥瘦相间，串成串。炭火升起，肉串上架，翻烤。油脂滴落，滋滋作响，边缘微焦。撒孜然辣椒面，再翻烤。海生菜洗净，蘑菇切片，上架烤软。肉串与菜蔬同摆一盘，荤素相间。炭火气窜进肉里，焦香扑鼻。
            Brew("Smoker", 180.0f, ModStrings.RECIPES.GRILLEDMEATPLATTER.DESC, 8)
                .In(new Tag[] { "Lettuce", "Mushroom" },
                    new float[] { 1.0f, 0.166667f })  // ==[ERR:0, 0.0008]==
                .In("CookedMeat", 0.25f)  // ==[ERR:0]==
                .Out("GrilledMeatPlatter", 1.0f)  // ==[ERR:0]==
                .Build();
            // 鱿鱼收拾干净，切成条，串成串。胡椒盐酒腌上，去腥入味。熏炉烧热，鱿鱼串上架，慢熏。鱿鱼卷边，边缘焦褐，油脂渗出。翻面再熏，撒胡椒粉。熏到外皮微脆，内里弹韧，焦香扑鼻。
            Brew("Smoker", 120.0f, ModStrings.RECIPES.GRILLEDSQUIDSKEWER.DESC, 8)
                .In("SquidMeat", 2.0f)  // ==[ERR:0]==
                .In("CuminPowder", 0.1f)  // ==[ERR:0]==
                .Out("GrilledSquidSkewer", 1.0f)  // ==[ERR:0]==
                .Build();
            // 肉切块，加辣椒、醋、糖，抓匀腌透，腌到入味，肉纤维松开。挂起风干，皮面收紧。入炉熏烤，热力逼出油脂，皮面起泡，由白转金再转红。翻面再烤，烤到皮脆肉嫩，油脂滴落，烟熏气渗进肉里。焦香四溢，辣香扑鼻。
            Brew("Smoker", 300.0f, ModStrings.RECIPES.CRISPYROASTCHICKEN.DESC, 10)
                .In("Meat", 1.0f)  // ==[ERR:0]==
                .In("SpiceNut", 1.0f)  // ==[ERR:0]==
                .In("Sucrose", 0.5f)  // ==[ERR:0]==
                .Out("CrispyRoastChicken", 1.0f)  // ==[ERR:0]==
                .Build();
            // 牛排修净肥边，盐和黑胡椒抹匀，静置回温到室温，铁锅烧到冒烟，下牛排，大火封煎，一面焦褐，翻面再煎，边缘脂肪逼出油，转小火，加黄油迷迭香，舀热油淋面，反复淋，切面汁水锁住，外焦里嫩，肉香浓郁，脂香醇厚。
            Brew("Smoker", 600.0f, ModStrings.RECIPES.TOMAHAWKSTEAK.DESC, 10)
                .In("BeefMeat", 1.0f)  // ==[ERR:0]==
                .In("CuminPowder", 0.1f)  // ==[ERR:0]==
                .In("Tallow", 0.1f)  // ==[ERR:0]==
                .Out("TomahawkSteak", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:Smoker:REG]END==
// ==[ITEM:RB:FoodDehydrator:REG]BEGIN==
        // ════ FoodDehydrator 配方（4 条，配方创建器生成）════
        public static void RegisterFoodDehydrator()
        {
            // 肉挑肥瘦相间的，切大块，冷水下锅，大骨汤没过，加姜片葱段。大火烧开，撇净血沫，转小火慢煨。肉纤维一点点松开，油脂化进汤里，汤色由清转浓，收至挂勺。肉烂而不散，筷子一扎即透，汤汁稠厚，咸鲜脂香。
            Brew("FoodDehydrator", 50.0f, ModStrings.RECIPES.CANNEDMEAT.DESC, 12)
                .In("Meat", 0.75f)  // ==[ERR:0]==
                .In("BoneSoup", 0.3f)  // ==[ERR:0]==
                .In("Steel", 0.01f)  // ==[ERR:0]==
                .Out("CannedMeat", 1.0f)  // ==[ERR:0]==
                .Build();
            // 鱼剔骨取肉，片成厚片。大骨汤烧至微沸，鱼片下锅，一烫即白。撇去浮沫，汤清见底，鲜味全留在汤里。汤汁收浓，鱼鲜更纯，咸里透出回甘。
            Brew("FoodDehydrator", 50.0f, ModStrings.RECIPES.CANNEDSEAFOOD.DESC, 13)
                .In("FishMeat", 1.0f)  // ==[ERR:0]==
                .In("BoneSoup", 0.3f)  // ==[ERR:0]==
                .In("Steel", 0.01f)  // ==[ERR:0]==
                .Out("CannedSeafood", 1.0f)  // ==[ERR:0]==
                .Build();
            // 米饭蒸透，粒粒分明，摊开晾一晾。蘑菇切丁，下锅煸炒，煸出汁水。海生菜撕碎，下锅同炒，断生即起。米饭倒入，大火翻匀，让菌菇汁裹住每粒米。海菜带出咸鲜，淋一勺大骨汤，翻炒收汁。米粒油亮，菌菇香渗进饭里。
            Brew("FoodDehydrator", 50.0f, ModStrings.RECIPES.CANNEDGRAIN.DESC, 12)
                .In("CookedRice", 0.5f)  // ==[ERR:0]==
                .In(new Tag[] { "Mushroom", "Lettuce" },
                    new float[] { 0.166667f, 1.0f })  // ==[ERR:0.0008, 0]==
                .In("Steel", 0.01f)  // ==[ERR:0]==
                .Out("CannedGrain", 1.0f)  // ==[ERR:0]==
                .Build();
            // 果子去皮切块，蔗糖撒上，静置到汁水渗出。糖和果汁一起下锅，小火熬到浓稠，果块半透明。糖渍透亮，果肉柔韧，酸甜汁浓，甜而不腻。
            Brew("FoodDehydrator", 50.0f, ModStrings.RECIPES.CANNEDFRUIT.DESC, 12)
                .In(new Tag[] { "SwampDelights", "IceCavesForagePlant", "Pineapple" },
                    new float[] { 0.535714f, 1.5f, 3.0f })  // ==[ERR:-0.00064, 0, 0]==
                .In("Sucrose", 0.1f)  // ==[ERR:0]==
                .In("Steel", 0.01f)  // ==[ERR:0]==
                .Out("CannedFruit", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:FoodDehydrator:REG]END==
// ==[ITEM:RB:MilkPress:REG]BEGIN==
        // ════ MilkPress 配方（3 条，配方创建器生成）════
        public static void RegisterMilkPress()
        {
            // 谷粒挑净，去杂去瘪，上磨碾磨。磨盘慢转，粗壳脱落，粒芯碾成细粉。过细筛，筛出粗粒，回磨再碾。反复碾筛，粉出得匀。粉质细密且松散，手捻无颗粒，指间滑爽。过筛几遍，粉细如尘，白净蓬松，麦香清甜。
            Brew("MilkPress", 100.0f, ModStrings.RECIPES.FLOUR.DESC, 9)
                .In(new Tag[] { "ColdWheatSeed", "FernFood", "BasicPlantFood" },
                    new float[] { 3.0f, 3.0f, 2.0f })  // ==[ERR:0, 0, 0]==
                .Out("Flour", 1.0f)  // ==[ERR:0]==
                .Build();
            // 小吃豆挑净，冷水泡发，上磨磨浆，过筛滤去豆渣。豆浆入锅，大火烧开，撇净浮沫，转小火慢煮，加糖调味。浆色乳白，豆香清甜，口感顺滑，醇厚温和。
            Brew("MilkPress", 25.0f, ModStrings.RECIPES.SOYMILK.DESC, 6)
                .In("BeanPlantSeed", 1.0f)  // ==[ERR:0]==
                .In("Water", 1.0f)  // ==[ERR:0]==
                .Out("SoyMilk", 1.0f)  // ==[ERR:0]==
                .Build();
            // 火椒粒、辣椒拣净晾干，盐入锅小火炒热，下火椒粒和辣椒焙至椒皮微焦、烟熏辛香四溢，起锅摊凉，入植物粉碎机打成细粉，过筛去粗梗，装罐密封。
            Brew("MilkPress", 100.0f, ModStrings.RECIPES.CUMINPOWDER.DESC, 8)
                .In("SpiceNut", 1.0f)  // ==[ERR:0]==
                .In("Salt", 0.5f)  // ==[ERR:0]==
                .Out("CuminPowder", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:MilkPress:REG]END==
// ==[ITEM:RB:MicrobeMusher:REG]BEGIN==
        // ════ MicrobeMusher 配方（1 条，配方创建器生成）════
        public static void RegisterMicrobeMusher()
        {
            // 蟹肉拆出，挑净碎壳，拌蛋清淀粉，顺一个方向摔打上劲。团成球，码在盘里，垫姜丝。上笼蒸，旺火足汽，火候掐准。蒸到蟹球白嫩定型，鲜味凝住，原汤清亮。蟹球饱满，嫩而不散，鲜味干净。淋一勺原汤，鲜味更足。
            Brew("MicrobeMusher", 25.0f, ModStrings.RECIPES.CRABMEATBALLS.DESC, 9)
                .In(new Tag[] { "CrabLeg", "CrabClaw" },
                    new float[] { 3.0f, 1.75f })  // ==[ERR:0, 0]==
                .In("Flour", 0.5f)  // ==[ERR:0]==
                .Out("CrabMeatBalls", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:MicrobeMusher:REG]END==
// ==[ITEM:RB:SodaFountain:REG]BEGIN==
        // ════ SodaFountain 配方（9 条，配方创建器生成）════
        public static void RegisterSodaFountain()
        {
            // 冰可乐在手，整个夏天都是你的！
            Brew("SodaFountain", 25.0f, ModStrings.RECIPES.CLASSICCOLA.DESC, 6)
                .In(new Tag[] { "Water", "Ice" },
                    new float[] { 1.0f, 1.0f })  // ==[ERR:0, 0]==
                .In("CarbonDioxide", 0.3f)  // ==[ERR:0]==
                .In("Sucrose", 0.1f)  // ==[ERR:0]==
                .Out("ClassicCola", 1.0f)  // ==[ERR:0]==
                .Build();
            // 4℃冰镇，劲爽无糖，再来一瓶！
            Brew("SodaFountain", 25.0f, ModStrings.RECIPES.ZEROSUGARCOLA.DESC, 6)
                .In(new Tag[] { "Water", "Ice" },
                    new float[] { 1.0f, 1.0f })  // ==[ERR:0, 0]==
                .In("CarbonDioxide", 1.0f)  // ==[ERR:0]==
                .Out("ZeroSugarCola", 1.0f)  // ==[ERR:0]==
                .Build();
            // 菠萝去皮取肉，压汁滤清，入水调匀。甜酸试味，入罐充气，封口静置。果香清甜，气泡细密。色浅香清，菠萝甜爽。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.PINEAPPLEBEER.DESC, 9)
                .In("Pineapple", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("PineappleBeer", 1.0f)  // ==[ERR:0]==
                .Build();
            // 橙子剖开取汁，果皮浸出香气，汁与皮香入水调匀。甜酸试味，入罐充气，封口静置。橙香明快，气泡细密。色橙香明，甜酸清爽。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.ORANGESODA.DESC, 9)
                .In("Orange", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("OrangeSoda", 1.0f)  // ==[ERR:0]==
                .Build();
            // 黑加仑拣净压汁，滤去果渣，入水调匀。甜酸试味，入罐充气，封口静置。果香浓郁，气泡细密。色深香浓，浆果酸爽。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.BLACKCURRANTSODA.DESC, 9)
                .In("BlackCurrant", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("BlackcurrantSoda", 1.0f)  // ==[ERR:0]==
                .Build();
            // 蓝莓拣净压汁，滤去果渣，入水调匀。甜酸试味，入罐充气，封口静置。蓝莓香清雅，气泡细密。色淡香雅，蓝莓清甜。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.BLUEBERRYSODA.DESC, 9)
                .In("Blueberry", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("BlueberrySoda", 1.0f)  // ==[ERR:0]==
                .Build();
            // 西瓜剖开取瓤，压汁滤清，入水调匀。甜酸试味，入罐充气，封口静置。瓜香清甜，气泡细密。色浅香清，瓜甜清凉。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.WATERMELONSODA.DESC, 9)
                .In("Watermelon", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("WatermelonSoda", 1.0f)  // ==[ERR:0]==
                .Build();
            // 西莓拣净压汁，滤去果渣，入水调匀。甜酸试味，入罐充气，封口静置。果香浓郁，气泡细密。色深香浓，果酸清甜。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.XIMEISODA.DESC, 9)
                .In("Prune", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("XiMeiSoda", 1.0f)  // ==[ERR:0]==
                .Build();
            // 芒果去皮取肉，压汁滤清，入水调匀。甜酸试味，入罐充气，封口静置。芒果香浓，气泡细密。色金黄亮，芒果浓甜。
            Brew("SodaFountain", 32.0f, ModStrings.RECIPES.MANGOSODA.DESC, 9)
                .In("Mango", 0.4f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("CarbonDioxide", 0.4f)  // ==[ERR:0]==
                .Out("MangoSoda", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:SodaFountain:REG]END==
// ==[ITEM:RB:ChemicalRefinery:REG]BEGIN==
        // ════ ChemicalRefinery 配方（10 条，配方创建器生成）════
        public static void RegisterChemicalRefinery()
        {
            // 小吃豆洗净浸泡，蒸透摊凉，拌曲制醅，入酿造桶，加盐水搅匀，日晒夜露，定期翻搅，待酱醅成熟，压榨取汁，滤清沉淀，取红褐清液，装坛陈放。
            Brew("ChemicalRefinery", 1800.0f, ModStrings.RECIPES.SOYSAUCE.DESC, 8)
                .In("BeanPlantSeed", 5.0f)  // ==[ERR:0]==
                .In("Salt", 1.5f)  // ==[ERR:0]==
                .In("Water", 3.5f)  // ==[ERR:0]==
                .Out("SoySauce", 6.0f)  // ==[ERR:0]==
                .Build();
            // 乙醇缓缓入水，慢搅至匀，细纱滤过。静置候清，酒液澄澈如泉，气息凛冽而纯。封存待稳，色澈香冷。
            Brew("ChemicalRefinery", 53.0f, ModStrings.RECIPES.WHITEWHALEVODKA.DESC, 11)
                .In("Ethanol", 1.0f)  // ==[ERR:0]==
                .In("Water", 1.5f)  // ==[ERR:0]==
                .Out("WhiteWhaleVodka", 2.5f)  // ==[ERR:0]==
                .Build();
            // 葡萄粒粒手选，去梗破皮，汁液入瓮。撒蔗糖轻拌，候其自然发酵，果香渐起。滤去沉渣，入桶陈放。酒色渐深，果味渐圆，色深香馥。
            Brew("ChemicalRefinery", 28.0f, ModStrings.RECIPES.REDWINE.DESC, 11)
                .In("Grape", 1.2f)  // ==[ERR:0]==
                .In("Sucrose", 0.2f)  // ==[ERR:0]==
                .Out("RedWine", 1.0f)  // ==[ERR:0]==
                .Build();
            // 葡萄酒入铜釜，文火慢蒸，酒气升腾。凝而成液，入橡木桶静养，木香渐入。果甜渐柔，出桶调香，滤清。色琥珀润，木香果甜。
            Brew("ChemicalRefinery", 25.0f, ModStrings.RECIPES.BRANDY.DESC, 11)
                .In("RedWine", 5.0f)  // ==[ERR:0]==
                .In("Water", 0.5f)  // ==[ERR:0]==
                .Out("Brandy", 1.2f)  // ==[ERR:0]==
                .Build();
            Brew("ChemicalRefinery", 142.0f, ModStrings.RECIPES.PUREWHEATBEER.DESC, 11)
                .In("ColdWheatSeed", 1.0f)  // ==[ERR:0]==
                .In("Water", 4.0f)  // ==[ERR:0]==
                .Out("PureWheatBeer", 4.5f)  // ==[ERR:0]==
                .Build();
            // 小麦淘净，火辣粒拣净，同入锅糖化。麦汁滤出，煮沸凉却，入罐发酵。草本香与麦香交缠，熟成后滤清。色沉香厚，草本辛香。
            Brew("ChemicalRefinery", 113.0f, ModStrings.RECIPES.GRUITALE.DESC, 11)
                .In("ColdWheatSeed", 1.0f)  // ==[ERR:0]==
                .In("SpiceNut", 1.0f)  // ==[ERR:0]==
                .In("Water", 3.0f)  // ==[ERR:0]==
                .Out("GruitAle", 3.6f)  // ==[ERR:0]==
                .Build();
            // 蔗糖化水，入罐候酵，待其起泡生香。入铜釜蒸馏，酒液初成，入桶陈放。焦糖香渐出，滤清调香。色淡香浓，甘蔗焦甜。
            Brew("ChemicalRefinery", 53.0f, ModStrings.RECIPES.RUM.DESC, 11)
                .In("Sucrose", 4.8f)  // ==[ERR:0]==
                .In("Water", 1.0f)  // ==[ERR:0]==
                .Out("Rum", 2.5f)  // ==[ERR:0]==
                .Build();
            // 酒曲研细，米酒备好，水净入缸。三者拌匀，入窖发酵，候其香熟。上甑蒸馏，酒液入坛，陈放数年。勾调定香，色微黄亮，酱香陈厚。
            Brew("ChemicalRefinery", 50.0f, ModStrings.RECIPES.MOUTAI.DESC, 11)
                .In("Koji", 2.0f)  // ==[ERR:0]==
                .In("RiceWine", 3.0f)  // ==[ERR:0]==
                .In("Water", 1.0f)  // ==[ERR:0]==
                .Out("Moutai", 1.2f)  // ==[ERR:0]==
                .Build();
            // 米虱淘净，小麦备好，入甑蒸透。摊凉撒曲，入缸发酵，候其甜香四溢。滤取酒液，澄清。色乳白润，米香清甜。
            Brew("ChemicalRefinery", 120.0f, ModStrings.RECIPES.RICEWINE.DESC, 11)
                .In(new Tag[] { "ColdWheatSeed", "BasicPlantFood", "FernFood" },
                    new float[] { 3.0f, 2.0f, 3.0f })  // ==[ERR:0, 0, 0]==
                .In("Water", 1.5f)  // ==[ERR:0]==
                .Out("RiceWine", 4.0f)  // ==[ERR:0]==
                .Build();
            // 小麦淘净，入锅糖化，麦汁滤出。煮沸凉却，加冰降温，入罐发酵。麦芽香渐浓，熟成后滤清。色深沫厚，麦芽浓香。
            Brew("ChemicalRefinery", 125.0f, ModStrings.RECIPES.MUNICHBEER.DESC, 11)
                .In("ColdWheatSeed", 1.0f)  // ==[ERR:0]==
                .In("Water", 3.0f)  // ==[ERR:0]==
                .In("Ice", 0.5f)  // ==[ERR:0]==
                .Out("MunichBeer", 4.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:ChemicalRefinery:REG]END==
// ==[ITEM:RB:EspressoMachine:REG]BEGIN==
        // ════ EspressoMachine 配方（5 条，配方创建器生成）════
        public static void RegisterEspressoMachine()
        {
            // 耶加豆入锅烘焙，火候轻浅，豆香清扬。磨成粗粒，热水缓缓冲滤，咖啡液滤出。兑冰水降温，杯中加冰。色深香雅，清凉微苦。
            Brew("EspressoMachine", 35.0f, ModStrings.RECIPES.ICEDCOFFEE.DESC, 10)
                .In("YirgacheffeBean", 0.05f)  // ==[ERR:0]==
                .In("Water", 0.8f)  // ==[ERR:0]==
                .In("Ice", 0.28f)  // ==[ERR:0]==
                .Out("IcedCoffee", 1.0f)  // ==[ERR:0]==
                .Build();
            // 桑托豆入锅烘焙，火候深沉，豆香厚重。磨成细粉，压入粉碗，热水高压冲过。浓缩液缓缓滴出。色浓香烈，坚果焦甜。
            Brew("EspressoMachine", 32.0f, ModStrings.RECIPES.ESPRESSO.DESC, 10)
                .In("SantosBean", 0.5f)  // ==[ERR:0]==
                .In("Water", 1.5f)  // ==[ERR:0]==
                .Out("Espresso", 1.0f)  // ==[ERR:0]==
                .Build();
            // 耶加豆与桑托豆拼配，入锅同烘，花果香与烘焙香交缠。磨细压粉，高压冲萃，浓缩液色深味厚。花果气隐约其间，色厚香复，花果烘焙。
            Brew("EspressoMachine", 32.0f, ModStrings.RECIPES.RETROESPRESSO.DESC, 10)
                .In("SantosBean", 0.1f)  // ==[ERR:0]==
                .In("YirgacheffeBean", 0.3f)  // ==[ERR:0]==
                .In("Water", 1.5f)  // ==[ERR:0]==
                .Out("RetroEspresso", 1.0f)  // ==[ERR:0]==
                .Build();
            // 浓缩咖啡液备好，生蛋打散，奶液调温。三者拌匀，入模加冰，凝冻渐成。冷藏定型，切块装盘。色褐凝润，咖啡奶甜。
            Brew("EspressoMachine", 28.0f, ModStrings.RECIPES.COFFEEDOUBLESKINJELLY.DESC, 10)
                .In(new Tag[] { "Espresso", "RetroEspresso" },
                    new float[] { 0.5f, 0.5f })  // ==[ERR:0, 0]==
                .In("RawEgg", 0.2f)  // ==[ERR:0]==
                .In("Ice", 0.2f)  // ==[ERR:0]==
                .Out("CoffeeDoubleSkinJelly", 1.0f)  // ==[ERR:0]==
                .Build();
            // 浓缩咖啡液备好，奶液加热，巧克力融化成浆。奶泡打得细密，咖啡入杯，兑奶与巧克力。搅匀调温，杯面浮奶泡，撒巧克力屑。色深香浓，奶巧咖甜。
            Brew("EspressoMachine", 60.0f, ModStrings.RECIPES.MOCHA.DESC, 10)
                .In(new Tag[] { "Espresso", "RetroEspresso" },
                    new float[] { 0.5f, 0.5f })  // ==[ERR:0, 0]==
                .In("FreshMilk", 1.0f)  // ==[ERR:0]==
                .In("Chocolate", 0.5f)  // ==[ERR:0]==
                .Out("Mocha", 1.9f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:EspressoMachine:REG]END==
// ==[ITEM:RB:Deepfryer:REG]BEGIN==
        // ════ Deepfryer 配方（1 条）════
        public static void RegisterDeepfryer()
        {
            // 土豆洗净削皮，穿签，顺刀旋成不断的风琴片，拉开成螺旋。清水冲去表面淀粉，沥到干爽，薄薄拍一层干面粉。动物油脂下锅烧滚，螺旋入油，先定型再翻面，炸到片片舒展、边缘转金。捞出沥油，趁热抖匀。外片焦脆，内芯绵糯，油香钻进缝里。
            Brew("Deepfryer", 40.0f, ModStrings.RECIPES.POTATOTORNADO.DESC, 18)
                .In("Potato", 2.0f)  // ==[ERR:0]==
                .In("Flour", 0.2f)  // ==[ERR:0]==
                .In("Tallow", 0.1f)  // ==[ERR:0]==
                .Out("PotatoTornado", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:Deepfryer:REG]END==
// ==[ITEM:RB:SushiBar:REG]BEGIN==
        // ════ SushiBar 配方（1 条）════
        public static void RegisterSushiBar()
        {
            // 米饭蒸透，趁温拌匀，摊开晾到不烫手，粒粒分明。鱼片斜刀切厚片，修去筋膜，拭干水气。手沾凉水，取饭捏成小枕，覆一片鱼，轻压贴合。摆盘收口朝上，米温鱼凉，鲜味干净。
            Brew("SushiBar", 35.0f, ModStrings.RECIPES.EELSUSHI.DESC, 18)
                .In("FishMeat", 1.0f)  // ==[ERR:0]==
                .In("CookedRice", 1.0f)  // ==[ERR:0]==
                .Out("EelSushi", 1.0f)  // ==[ERR:0]==
                .Build();
        }
// ==[ITEM:RB:SushiBar:REG]END==
        // ==[ZONE:RECIPES]END==

        // 构造 RecipeBuilder 的便捷方法
        private static RecipeBuilder Brew(string fab, float time, string desc, int sort = 0)
        {
            return new RecipeBuilder(fab, time, desc, sort);
        }
    }

    // 温度简写
    internal static class Temp
    {
        public const ComplexRecipe.RecipeElement.TemperatureOperation
            Average = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature;
        public const ComplexRecipe.RecipeElement.TemperatureOperation
            Melted = ComplexRecipe.RecipeElement.TemperatureOperation.Melted;
        public const ComplexRecipe.RecipeElement.TemperatureOperation
            Heated = ComplexRecipe.RecipeElement.TemperatureOperation.Heated;
    }

    // 配方构建器 — 链式调用（源自 Savor Recipes.cs）
    public class RecipeBuilder
    {
        // 配方名与描述
        private readonly string _fab, _desc;
        // 制作耗时（秒）
        private readonly float _time;
        // 菜单排序
        private readonly int _sort;
        // 输入材料
        private readonly List<ComplexRecipe.RecipeElement> _in;
        // 产出
        private readonly List<ComplexRecipe.RecipeElement> _out;

        // 添加静态计数器，追踪 Build 被调用次数
        private static int _buildCount = 0;
        // 记录已注册的 recipeId，防止重复
        private static HashSet<string> _registeredIds = new HashSet<string>();

// 追加输入：按 Tag 数组 + 每项千克数
        public RecipeBuilder In(Tag[] ids, float kg)
        {
            _in.Add(new ComplexRecipe.RecipeElement(ids, kg));
            return this;
        }

        // ids：id 列表、kgs：重量(kg)
        public RecipeBuilder In(Tag[] ids, float[] kgs)
        {
            _in.Add(new ComplexRecipe.RecipeElement(ids, kgs));
            return this;
        }//测试结束


        internal RecipeBuilder(string fab, float time, string desc, int sort)
        {
            _fab = fab;
            _time = time;
            _desc = desc;
            _sort = sort;
            _in = new List<ComplexRecipe.RecipeElement>();
            _out = new List<ComplexRecipe.RecipeElement>();
        }

        // 链式：给配方加一条输入（物品 id + 重量 kg）
        public RecipeBuilder In(string id, float kg)
        {
            _in.Add(new ComplexRecipe.RecipeElement(id, kg));
            return this;
        }

        public RecipeBuilder Out(string id, float kg,
            ComplexRecipe.RecipeElement.TemperatureOperation temp
                = ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature)
        {
            _out.Add(new ComplexRecipe.RecipeElement(id, kg, temp, false));
            return this;
        }

        public void Build()
        {
            _buildCount++;
            var inp = _in.ToArray();
            var outp = _out.ToArray();
            var recipeId = ComplexRecipeManager.MakeRecipeID(_fab, inp, outp);

            //检查是否已注册过相同 recipeId
            if (_registeredIds.Contains(recipeId))
            {
                //UnityEngine.Debug.LogWarning($"[Recipe Debug] ⚠️ 配方已存在! recipeId: {recipeId} 被重复注册!");
                //UnityEngine.Debug.LogWarning($"[Recipe Debug] 重复注册堆栈: {System.Environment.StackTrace}");
                return;
            }
            else
            {
                _registeredIds.Add(recipeId);
                //UnityEngine.Debug.Log($"[Recipe Debug] recipeId: {recipeId} 首次注册");
            }

            // 检查 ComplexRecipeManager 中是否已存在
            var recipesList = ComplexRecipeManager.Get().recipes;
            bool alreadyExists = false;
            for (int i = 0; i < recipesList.Count; i++)
            {
                if (recipesList[i].id == recipeId)
                {
                    alreadyExists = true;
                    //UnityEngine.Debug.LogWarning($"[Recipe Debug] ⚠️ ComplexRecipeManager 中已存在此 recipeId!");
                    break;
                }
            }

            var recipe = new ComplexRecipe(recipeId, inp, outp);

            recipe.time = _time;
            recipe.description = _desc;
            recipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.Result;
            recipe.fabricators = new List<Tag> { _fab };
            recipe.sortOrder = _sort;

            MFLog.Info(LogCat.Food, $"[Recipe Debug] Build() 完成，配方已注册: {recipeId}");
        }
    }

    // ==[ZONE:PATCHES]BEGIN==

// ==[ITEM:RB:GourmetCookingStation:PATCH]BEGIN==
        // ════ GourmetCookingStation 配方补丁 ════
        [HarmonyPatch(typeof(GourmetCookingStationConfig), "ConfigureBuildingTemplate")]
        internal static class GourmetCookingStationRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterGourmetCookingStation();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:GourmetCookingStationRecipePatch", e); }
            }
        }
// ==[ITEM:RB:GourmetCookingStation:PATCH]END==
// ==[ITEM:RB:Smoker:PATCH]BEGIN==
        // ════ Smoker 配方补丁 ════
        [HarmonyPatch(typeof(SmokerConfig), "ConfigureBuildingTemplate")]
        internal static class SmokerRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterSmoker();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:SmokerRecipePatch", e); }
            }
        }
// ==[ITEM:RB:Smoker:PATCH]END==
// ==[ITEM:RB:FoodDehydrator:PATCH]BEGIN==
        // ════ FoodDehydrator 配方补丁 ════
        [HarmonyPatch(typeof(FoodDehydratorConfig), "ConfigureBuildingTemplate")]
        internal static class FoodDehydratorRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterFoodDehydrator();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:FoodDehydratorRecipePatch", e); }
            }
        }
// ==[ITEM:RB:FoodDehydrator:PATCH]END==
// ==[ITEM:RB:MilkPress:PATCH]BEGIN==
        // ════ MilkPress 配方补丁 ════
        [HarmonyPatch(typeof(MilkPressConfig), "ConfigureBuildingTemplate")]
        internal static class MilkPressRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterMilkPress();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:MilkPressRecipePatch", e); }
            }
        }
// ==[ITEM:RB:MilkPress:PATCH]END==
// ==[ITEM:RB:MicrobeMusher:PATCH]BEGIN==
        // ════ MicrobeMusher 配方补丁 ════
        [HarmonyPatch(typeof(MicrobeMusherConfig), "ConfigureBuildingTemplate")]
        internal static class MicrobeMusherRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterMicrobeMusher();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:MicrobeMusherRecipePatch", e); }
            }
        }
// ==[ITEM:RB:MicrobeMusher:PATCH]END==
// ==[ITEM:RB:SodaFountain:PATCH]BEGIN==
        // ════ SodaFountain 配方补丁 ════
        [HarmonyPatch(typeof(SodaFountainConfig), "ConfigureBuildingTemplate")]
        internal static class SodaFountainRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterSodaFountain();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:SodaFountainRecipePatch", e); }
            }
        }
// ==[ITEM:RB:SodaFountain:PATCH]END==
// ==[ITEM:RB:ChemicalRefinery:PATCH]BEGIN==
        // ════ ChemicalRefinery 配方补丁 ════
        [HarmonyPatch(typeof(ChemicalRefineryConfig), "ConfigureBuildingTemplate")]
        internal static class ChemicalRefineryRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterChemicalRefinery();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:ChemicalRefineryRecipePatch", e); }
            }
        }
// ==[ITEM:RB:ChemicalRefinery:PATCH]END==
// ==[ITEM:RB:EspressoMachine:PATCH]BEGIN==
        // ════ EspressoMachine 配方补丁 ════
        [HarmonyPatch(typeof(EspressoMachineConfig), "ConfigureBuildingTemplate")]
        internal static class EspressoMachineRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterEspressoMachine();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:EspressoMachineRecipePatch", e); }
            }
        }
// ==[ITEM:RB:EspressoMachine:PATCH]END==
// ==[ITEM:RB:Deepfryer:PATCH]BEGIN==
        // ════ Deepfryer 配方补丁 ════
        [HarmonyPatch(typeof(DeepfryerConfig), "ConfigureBuildingTemplate")]
        internal static class DeepfryerRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterDeepfryer();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:DeepfryerRecipePatch", e); }
            }
        }
// ==[ITEM:RB:Deepfryer:PATCH]END==
// ==[ITEM:RB:SushiBar:PATCH]BEGIN==
        // ════ SushiBar 配方补丁 ════
        [HarmonyPatch(typeof(SushiBarConfig), "ConfigureBuildingTemplate")]
        internal static class SushiBarRecipePatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    ModRecipes.RegisterSushiBar();
                }
                catch (System.Exception e) { MFLog.HotFail("patch:SushiBarRecipePatch", e); }
            }
        }
// ==[ITEM:RB:SushiBar:PATCH]END==
    // ==[ZONE:PATCHES]END==
}
