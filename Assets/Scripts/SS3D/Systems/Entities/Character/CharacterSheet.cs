using System;
using UnityEngine;

namespace SS3D.Systems.Entities.Character
{
    /// <summary>
    /// Session character customization data submitted before spawn.
    /// </summary>
    [Serializable]
    public struct CharacterSheet
    {
        public const int MaxNameLength = 32;

        public string Name;
        public int HairStyleId;
        public int BeardStyleId;
        public int SkinToneIndex;
        public int HairColorIndex;

        public static CharacterSheet CreateDefault(string ckey)
        {
            string fallback = string.IsNullOrWhiteSpace(ckey) ? "Unnamed" : ckey.Trim();
            return new CharacterSheet
            {
                Name = fallback,
                HairStyleId = 0,
                BeardStyleId = 0,
                SkinToneIndex = 0,
                HairColorIndex = 0,
            };
        }

        public CharacterSheet Validated(AppearanceCatalog catalog)
        {
            CharacterSheet sheet = this;
            sheet.Name = SanitizeName(sheet.Name);

            if (catalog == null)
            {
                sheet.HairStyleId = Mathf.Max(0, sheet.HairStyleId);
                sheet.BeardStyleId = Mathf.Max(0, sheet.BeardStyleId);
                sheet.SkinToneIndex = Mathf.Max(0, sheet.SkinToneIndex);
                sheet.HairColorIndex = Mathf.Max(0, sheet.HairColorIndex);
                return sheet;
            }

            sheet.HairStyleId = catalog.ClampHairStyleId(sheet.HairStyleId);
            sheet.BeardStyleId = catalog.ClampBeardStyleId(sheet.BeardStyleId);
            sheet.SkinToneIndex = catalog.ClampSkinToneIndex(sheet.SkinToneIndex);
            sheet.HairColorIndex = catalog.ClampHairColorIndex(sheet.HairColorIndex);
            return sheet;
        }

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Unnamed";
            }

            string trimmed = name.Trim();
            if (trimmed.Length > MaxNameLength)
            {
                trimmed = trimmed.Substring(0, MaxNameLength);
            }

            return trimmed;
        }

        public bool IsValidName()
        {
            return !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= MaxNameLength;
        }
    }
}
