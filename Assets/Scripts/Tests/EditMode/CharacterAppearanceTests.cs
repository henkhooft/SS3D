using NUnit.Framework;
using SS3D.Systems.Entities.Character;
using UnityEngine;

namespace EditorTests
{
    public class CharacterAppearanceTests
    {
        [Test]
        public void Default_IsNotSet()
        {
            Assert.That(default(CharacterAppearance).IsSet, Is.False);
        }

        [Test]
        public void Sanitized_IsSetEvenWithTransparentInput()
        {
            CharacterAppearance appearance = new CharacterAppearance { HairColor = new Color(0.2f, 0.1f, 0f, 0f) }.Sanitized();

            Assert.That(appearance.IsSet, Is.True);
            Assert.That(appearance.HairColor.a, Is.EqualTo(1f));
            Assert.That(appearance.BeardColor.a, Is.EqualTo(1f));
        }

        [Test]
        public void Sanitized_ClampsBodyValuesAndStyles()
        {
            CharacterAppearance appearance = new CharacterAppearance
            {
                Female = 3f,
                Fat = -1f,
                Jaw = float.NaN,
                HairStyle = -4,
                BeardStyle = 2,
            }.Sanitized();

            Assert.That(appearance.Female, Is.EqualTo(1f));
            Assert.That(appearance.Fat, Is.EqualTo(0f));
            Assert.That(appearance.Jaw, Is.EqualTo(0.5f));
            Assert.That(appearance.HairStyle, Is.EqualTo(0));
            Assert.That(appearance.BeardStyle, Is.EqualTo(2));
        }

        [TestCase("  Jane Doe  ", "Jane Doe")]
        [TestCase("<b>Bold</b>", "bBold/b")]
        [TestCase("Line\nBreak", "LineBreak")]
        [TestCase("   ", "")]
        [TestCase(null, "")]
        public void SanitizeName_StripsMarkupControlAndWhitespace(string input, string expected)
        {
            Assert.That(CharacterAppearance.SanitizeName(input), Is.EqualTo(expected));
        }

        [Test]
        public void SanitizeName_TruncatesToMaxLength()
        {
            string name = CharacterAppearance.SanitizeName(new string('a', 100));

            Assert.That(name.Length, Is.EqualTo(CharacterAppearance.MaxNameLength));
        }
    }
}
