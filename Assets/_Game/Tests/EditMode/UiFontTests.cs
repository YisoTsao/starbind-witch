using NUnit.Framework;
using Starbind.UI;
using UnityEditor;
using UnityEngine;

namespace Starbind.Tests
{
    /// <summary>Licensed pixel font (Fusion Pixel 12px, SIL OFL 1.1) is in Resources with its licence and
    /// imported without anti-aliasing; label sizes snap to its 12 px grid.</summary>
    public class UiFontTests
    {
        const string FontPath = "Assets/_Game/Resources/Fonts/FusionPixel12_zh_tw.ttf";

        [Test]
        public void PixelFontLoadsWithLicenceAndHintedRaster()
        {
            Assert.IsNotNull(Resources.Load<Font>(UIKit.FontResource));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Resources/Fonts/FusionPixel12_OFL.txt"), "OFL licence ships with the font");
            var imp = (TrueTypeFontImporter)AssetImporter.GetAtPath(FontPath);
            Assert.AreEqual(FontRenderingMode.HintedRaster, imp.fontRenderingMode);
            Assert.AreEqual(FontTextureCase.Dynamic, imp.fontTextureCase);
        }

        [Test]
        public void FontCoversEveryGlyphTheUiUses()
        {
            var font = Resources.Load<Font>(UIKit.FontResource);
            foreach (var ch in "魔典形態元素修飾等級提升選擇一張書頁訓練場月影森林蒸汽爆裂超載爆燃碎晶雷暴火焰龍捲菇王棲地光門0123ABCxyz！？，。、")
                Assert.IsTrue(font.HasCharacter(ch), "missing glyph " + ch);
        }

        [Test]
        public void PrewarmCharsetIsFullyCoveredByTheFont()
        {
            var font = Resources.Load<Font>(UIKit.FontResource);
            var charset = Resources.Load<TextAsset>(UIKit.CharsetResource);
            Assert.IsNotNull(charset, "run tools/art/font_charset.py");
            Assert.That(charset.text.Length, Is.GreaterThan(600));
            foreach (var ch in charset.text)
                Assert.IsTrue(font.HasCharacter(ch), "missing glyph U+" + ((int)ch).ToString("X4"));
        }

        [TestCase(10, 12)]
        [TestCase(16, 12)]
        [TestCase(20, 24)]
        [TestCase(26, 24)]
        [TestCase(32, 36)]
        [TestCase(44, 48)]
        [TestCase(4, 12)]
        public void LabelSizesSnapToTwelve(int asked, int expected) => Assert.AreEqual(expected, UIKit.PixelSize(asked));
    }
}
