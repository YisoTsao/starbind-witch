using UnityEditor;
using UnityEngine;

namespace Starbind.Editor
{
    /// <summary>Import rules for every texture under Assets/_Game/Art and Assets/_Game/Tiles
    /// (ASSET_PIPELINE.md §4): PPU 32, Point, no compression, no mipmaps, full size.
    /// Slicing and custom pivots are set by the scene builder / artists, not here.</summary>
    public sealed class PixelArtImportRules : AssetPostprocessor
    {
        public const int PixelsPerUnit = 32;

        public static bool Applies(string path) =>
            path.StartsWith("Assets/_Game/Art/") || path.StartsWith("Assets/_Game/Tiles/");

        void OnPreprocessTexture()
        {
            if (!Applies(assetPath))
                return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 4096;
            ti.isReadable = false;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteExtrude = 0;
            ti.SetTextureSettings(s);
        }

        /// <summary>Pixel fonts (Resources/Fonts/*.ttf): dynamic, hinted raster (no anti-aliasing) so glyphs
        /// stay crisp at multiples of their 12 px design size.</summary>
        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/_Game/Resources/Fonts/") || assetImporter is not TrueTypeFontImporter fi)
                return;
            fi.fontTextureCase = FontTextureCase.Dynamic;
            fi.fontRenderingMode = FontRenderingMode.HintedRaster;
            fi.includeFontData = true;
            fi.fontSize = 12;
            fi.characterPadding = 1;
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (!Applies(assetPath))
                return;
            if (texture.width % 2 != 0 || texture.height % 2 != 0)
                Debug.LogWarning($"[PixelArtImportRules] {assetPath} is {texture.width}x{texture.height}; sizes must be even (ART_DIRECTION.md §3)");
            if (assetPath.Contains("/Tilesets/") && (texture.width % 32 != 0 || texture.height % 32 != 0))
                Debug.LogWarning($"[PixelArtImportRules] tile sheet {assetPath} is not a multiple of 32 px");
        }
    }
}
