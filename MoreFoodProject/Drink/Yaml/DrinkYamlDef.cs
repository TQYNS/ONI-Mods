using System.Collections.Generic;

namespace MoreFood_KFC
{
    public class DrinkYamlFile
    {
        public Dictionary<string, DrinkYamlDef> drinks { get; set; }
    }

    public class DrinkYamlDef
    {
        public string id { get; set; }
        public bool? food_only { get; set; }
        public float? hydration_per_kg { get; set; }
        public float? morale_bonus { get; set; }
        public float? max_serve_mass { get; set; }
        public int? priority { get; set; }
        public int? sort { get; set; }
        public string category { get; set; }
        public bool? garnish { get; set; }
        public string garnish_anims { get; set; }
        public string garnish_symbol { get; set; }
        public float? garnish_consume_rate { get; set; }
        public int? garnish_priority { get; set; }
        public float? hangover_chance { get; set; }
        public DrinkEffectDef effect { get; set; }
    }

    public class DrinkEffectDef
    {
        public string id { get; set; }
        public string name { get; set; }
        public float duration { get; set; }
        public bool is_bad { get; set; }
        public List<DrinkEffectModifierDef> modifiers { get; set; }
    }

    public class DrinkEffectModifierDef
    {
        public string attribute { get; set; }
        public float value { get; set; }
        public bool is_percent { get; set; }
    }
}
