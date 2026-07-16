using NUnit.Framework;
using SS3D.Systems.Entities.Character;
using UnityEngine;

namespace EditorTests
{
    public class CharacterSheetTests
    {
        [Test]
        public void CreateDefault_UsesCkeyAsName()
        {
            CharacterSheet sheet = CharacterSheet.CreateDefault("spessman");
            Assert.AreEqual("spessman", sheet.Name);
            Assert.AreEqual(0, sheet.HairStyleId);
            Assert.AreEqual(0, sheet.BeardStyleId);
        }

        [Test]
        public void CreateDefault_FallsBackWhenCkeyEmpty()
        {
            CharacterSheet sheet = CharacterSheet.CreateDefault("  ");
            Assert.AreEqual("Unnamed", sheet.Name);
        }

        [Test]
        public void SanitizeName_TrimsAndClampsLength()
        {
            string longName = new string('a', CharacterSheet.MaxNameLength + 10);
            string sanitized = CharacterSheet.SanitizeName("  " + longName + "  ");
            Assert.AreEqual(CharacterSheet.MaxNameLength, sanitized.Length);
            Assert.IsTrue(sanitized.StartsWith("aaa"));
        }

        [Test]
        public void Validated_ClampsCatalogIndexes()
        {
            AppearanceCatalog catalog = ScriptableObject.CreateInstance<AppearanceCatalog>();
            // Empty lists clamp to 0
            CharacterSheet sheet = new CharacterSheet
            {
                Name = "  Bob  ",
                HairStyleId = 99,
                BeardStyleId = -3,
                SkinToneIndex = 50,
                HairColorIndex = -1,
            };

            CharacterSheet validated = sheet.Validated(catalog);
            Assert.AreEqual("Bob", validated.Name);
            Assert.AreEqual(0, validated.HairStyleId);
            Assert.AreEqual(0, validated.BeardStyleId);
            Assert.AreEqual(0, validated.SkinToneIndex);
            Assert.AreEqual(0, validated.HairColorIndex);

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void AppearanceCatalog_ClampWithinRange()
        {
            AppearanceCatalog catalog = ScriptableObject.CreateInstance<AppearanceCatalog>();
            // Use reflection-free approach via Validated after populating via serialized lists isn't easy.
            // Clamp helpers still work with empty lists.
            Assert.AreEqual(0, catalog.ClampHairStyleId(5));
            Assert.AreEqual(0, catalog.ClampBeardStyleId(-1));
            Object.DestroyImmediate(catalog);
        }
    }
}
