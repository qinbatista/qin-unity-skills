using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Qin.UnityAssets
{
    // Apply in an isolated import trial to a dedicated Single Sprite used only for rendering.
    public static class WorldRepeatSprite
    {
        private static TextureImporter GetSingleSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null
                || !importer.assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                throw new ArgumentException($"Expected an owned Single Sprite under Assets: {assetPath}", nameof(assetPath));
            return importer;
        }

        public static bool ConfigureForDisplay(string assetPath, string platform,
            int maxFullCanvasScreenWidth, int maxFullCanvasScreenHeight)
        {
            if (maxFullCanvasScreenWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFullCanvasScreenWidth));
            if (maxFullCanvasScreenHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFullCanvasScreenHeight));

            var importer = GetSingleSpriteImporter(assetPath);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"Missing source dimensions for {assetPath}.");

            // Round up each axis without floating-point error at a power-of-two boundary.
            long longest = Math.Max(width, height);
            long fromWidth = (Math.Min(maxFullCanvasScreenWidth, width) * longest + width - 1) / width;
            long fromHeight = (Math.Min(maxFullCanvasScreenHeight, height) * longest + height - 1) / height;
            long required = Math.Max(fromWidth, fromHeight);
            if (required > 16384)
                throw new InvalidOperationException($"The footprint of {assetPath} exceeds the supported 16384 cap.");
            int cap = Mathf.NextPowerOfTwo(Math.Max(32, (int)required));
            return ConfigureImport(importer, platform, cap);
        }

        public static bool ConfigureImport(string assetPath, string platform, int maxSize)
        {
            return ConfigureImport(GetSingleSpriteImporter(assetPath), platform, maxSize);
        }

        private static bool ConfigureImport(TextureImporter importer, string platform, int maxSize)
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var activePlatform = NamedBuildTarget.FromBuildTargetGroup(group).TargetName;
            if (string.IsNullOrEmpty(platform) || platform != activePlatform)
                throw new ArgumentException($"Use the active importer platform '{activePlatform}'.", nameof(platform));
            if (maxSize < 32 || maxSize > 16384 || !Mathf.IsPowerOfTwo(maxSize))
                throw new ArgumentOutOfRangeException(nameof(maxSize), "Use a power of two from 32 to 16384.");

            string assetPath = importer.assetPath;
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            var target = importer.GetPlatformTextureSettings(platform);
            var effective = target.overridden ? target : importer.GetDefaultPlatformTextureSettings();
            bool reduceCap = maxSize < effective.maxTextureSize;
            if (!reduceCap && settings.spritePixelsPerUnit == 1000f
                && settings.spriteMeshType == SpriteMeshType.FullRect && !settings.readable)
                return false;

            settings.spritePixelsPerUnit = 1000f;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.readable = false;
            importer.SetTextureSettings(settings);
            if (reduceCap)
            {
                effective.name = platform;
                effective.overridden = true;
                effective.maxTextureSize = maxSize;
                importer.SetPlatformTextureSettings(effective);
            }
            importer.SaveAndReimport();

            importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.ReadTextureSettings(settings);
            target = importer.GetPlatformTextureSettings(platform);
            effective = target.overridden ? target : importer.GetDefaultPlatformTextureSettings();
            if (effective.maxTextureSize > maxSize || settings.spritePixelsPerUnit != 1000f
                || settings.spriteMeshType != SpriteMeshType.FullRect || settings.readable
                || AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
                throw new InvalidOperationException($"Import policy changed the trial settings or Sprite at {assetPath}.");
            return true;
        }
    }
}
