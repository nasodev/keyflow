using System;
using UnityEditor;

namespace KeyFlow.Editor
{
    // Enforces import settings for full-screen background sprites (gameplay + start).
    // Mirrors PianoSampleImportPostprocessor so settings stick across re-imports and
    // fresh worktree checkouts; without the allowlist, deleting a .meta would revert
    // ASTC 4x4 to default compression.
    public class BackgroundImporterPostprocessor : AssetPostprocessor
    {
        private static readonly string[] TargetPaths = new[]
        {
            "Assets/Sprites/background_gameplay.png",
            "Assets/Sprites/background_yellow.png",
            "Assets/Sprites/background_start.png",
        };

        private void OnPreprocessTexture()
        {
            if (Array.IndexOf(TargetPaths, assetPath) < 0) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.isReadable = false;

            var androidSettings = importer.GetPlatformTextureSettings("Android");
            androidSettings.overridden = true;
            androidSettings.format = TextureImporterFormat.ASTC_4x4;
            androidSettings.compressionQuality = 50; // Normal (0=Fast, 50=Normal, 100=Best)
            importer.SetPlatformTextureSettings(androidSettings);
        }
    }
}
