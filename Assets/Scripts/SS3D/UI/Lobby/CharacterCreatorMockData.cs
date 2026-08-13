using System.Collections.Generic;
using UnityEngine;

namespace SS3D.UI.Lobby
{
    /// <summary>Phase B mock data for the Character Creator guided-steps visual.</summary>
    public static class CharacterCreatorMockData
    {
        public static readonly string[] SpeciesOptions = { "Human", "Skrell", "Vox" };

        public static readonly string[] AngleLabels = { "Front", "Side", "Back" };

        public static readonly StepDef[] Steps =
        {
            new("identity", "Identity"),
            new("body", "Body"),
            new("style", "Style"),
            new("review", "Review"),
        };

        public static readonly SliderDef[] BodySliders =
        {
            new("height", "Height", 0.9f),
            new("belly", "Belly", 1f),
            new("upperBody", "Upper Body", 0.9f),
            new("lowerBody", "Lower Body", 0.91f),
            new("chest", "Chest", 0.25f),
            new("waist", "Waist", 0.88f),
            new("jaw", "Jaw", 1f),
            new("skinTone", "Skin Tone", 0.64f),
        };

        public static readonly Color[] UniformSwatches =
        {
            new(0.77f, 0.31f, 0.19f), // rust
            new(0.24f, 0.38f, 0.54f), // blue
            new(0.29f, 0.56f, 0.36f), // green
            new(0.79f, 0.56f, 0.22f), // amber
            new(0.55f, 0.58f, 0.60f), // steel
        };

        public static readonly Color[] HairColors =
        {
            Hex("#1b1410"),
            Hex("#3d2314"),
            Hex("#5b3a29"),
            Hex("#8a5a2e"),
            Hex("#cfa15e"),
            Hex("#8a3a24"),
            Hex("#9a9a9a"),
            Hex("#eeeeee"),
        };

        public static readonly Color[] EyeColors =
        {
            Hex("#3d2b1f"),
            Hex("#4c7ea8"),
            Hex("#4c8a5e"),
            Hex("#8a7130"),
            Hex("#7d8a8a"),
            Hex("#b5813a"),
            Hex("#5b3a29"),
            Hex("#2f2f33"),
        };

        public static readonly StyleTabDef[] StyleTabs =
        {
            new("hair", "Hair"),
            new("facialHair", "Facial Hair"),
            new("eyebrows", "Eyebrows"),
        };

        public static readonly StyleOptionDef[] HairOptions =
        {
            new("none", "None", true),
            new("anime", "Anime", false),
            new("beep", "Beep", false),
            new("clown", "Clown", false),
            new("emo", "Emo", false),
            new("fry", "Fry", false),
            new("mane", "Mane", false),
            new("mohawk", "Mohawk", false),
            new("mullet", "Mullet", false),
            new("shortmessy", "Short Messy", false),
            new("simple", "Simple", false),
        };

        public static readonly StyleOptionDef[] FacialHairOptions =
        {
            new("none", "None", true),
            new("stubble", "Stubble", false),
            new("fullbeard", "Full Beard", false),
            new("goatee", "Goatee", false),
            new("mustache", "Mustache", false),
            new("sideburns", "Sideburns", false),
        };

        public static readonly StyleOptionDef[] EyebrowOptions =
        {
            new("none", "None", true),
            new("thin", "Thin", false),
            new("thick", "Thick", false),
            new("angled", "Angled", false),
            new("bushy", "Bushy", false),
        };

        public static readonly LoadoutDef[] Loadouts =
        {
            new("marcus-voss", "Marcus Voss", "PnSecurity"),
            new("r-oyelaran", "R. Oyelaran", "PnJanitor"),
        };

        public static IReadOnlyList<StyleOptionDef> StyleOptionsFor(string tabKey) => tabKey switch
        {
            "facialHair" => FacialHairOptions,
            "eyebrows" => EyebrowOptions,
            _ => HairOptions,
        };

        public static string StyleLabel(string tabKey, string optionKey)
        {
            foreach (StyleOptionDef opt in StyleOptionsFor(tabKey))
            {
                if (opt.Key == optionKey)
                {
                    return opt.Label;
                }
            }

            return optionKey;
        }

        private static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }

            return Color.white;
        }

        public readonly struct StepDef
        {
            public readonly string Key;
            public readonly string Label;

            public StepDef(string key, string label)
            {
                Key = key;
                Label = label;
            }
        }

        public readonly struct SliderDef
        {
            public readonly string Key;
            public readonly string Label;
            public readonly float DefaultValue;

            public SliderDef(string key, string label, float defaultValue)
            {
                Key = key;
                Label = label;
                DefaultValue = defaultValue;
            }
        }

        public readonly struct StyleTabDef
        {
            public readonly string Key;
            public readonly string Label;

            public StyleTabDef(string key, string label)
            {
                Key = key;
                Label = label;
            }
        }

        public readonly struct StyleOptionDef
        {
            public readonly string Key;
            public readonly string Label;
            public readonly bool IsNone;

            public StyleOptionDef(string key, string label, bool isNone)
            {
                Key = key;
                Label = label;
                IsNone = isNone;
            }
        }

        public readonly struct LoadoutDef
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string ThumbId;

            public LoadoutDef(string id, string name, string thumbId)
            {
                Id = id;
                Name = name;
                ThumbId = thumbId;
            }
        }
    }
}
