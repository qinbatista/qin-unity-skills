using System;
using System.IO;
using NUnit.Framework;
using Qin.UnityAssets;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

public sealed class ImportAtlasExample
{
    private const string World = "Assets/ImageExamples/World/";
    private const float WorldPpu = 1000;
    private const int Layer = 30;

    [Serializable]
    private sealed class ImportState
    {
        public int width, height, platformCap;
        public float configuredPpu, effectivePpu, rendererScale, worldWidth, worldHeight;
        public long textureObjectBytes;
        public string graphicsFormat;
    }

    [Serializable]
    private sealed class PackedPiece
    {
        public string name;
        public int x, y, width, height;
    }

    [Serializable]
    private sealed class Measurements
    {
        public string editorVersion, activeTarget;
        public string source = "World/TreeCrown.png";
        public int sourceWidth, sourceHeight, projectedWidth, projectedHeight;
        public ImportState before, after;
        public float visibleAlphaOverlap, meanVisibleRgbError;
        public int atlasWidth, atlasHeight;
        public long atlasTextureObjectBytes;
        public string atlasFormat;
        public string excluded = "Ground";
        public PackedPiece[] pieces;
    }

    [Test]
    public void CrownKeepsItsWorldSizeAndTreePartsShareOnePackedPage()
    {
        string output = Environment.GetEnvironmentVariable("IMAGE_EXAMPLE_OUTPUT");
        Assert.That(Directory.Exists(output), Is.True, "Set IMAGE_EXAMPLE_OUTPUT to an existing output directory.");
        Measurements result = new() { editorVersion = Application.unityVersion, activeTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
        string folderName = "ImageExampleTrial" + Guid.NewGuid().ToString("N");
        string folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets", folderName));
        try
        {
            // Use copies so changes to an importer or atlas cannot touch the example artwork or its GUID.
            string trialCrown = ImportCopy(folder, "TreeCrown", 100);
            CompareCrown(trialCrown, output, result);
            PackTree(folder, output, result);
            File.WriteAllText(Path.Combine(output, "ImportAtlas.json"), JsonUtility.ToJson(result, true) + "\n");
        }
        finally
        {
            AssetDatabase.DeleteAsset(folder);
        }
    }

    private static string ImportCopy(string folder, string name, float pixelsPerUnit)
    {
        string path = folder + "/" + name + ".png";
        File.Copy(World + name + ".png", path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spritePixelsPerUnit = pixelsPerUnit;
        importer.SetTextureSettings(settings);
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return path;
    }

    private static void CompareCrown(string path, string output, Measurements result)
    {
        Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        result.sourceWidth = original.texture.width;
        result.sourceHeight = original.texture.height;
        Assert.That(result.sourceWidth, Is.EqualTo(768));
        Assert.That(result.sourceHeight, Is.EqualTo(576));

        GameObject actor = new GameObject("Crown before and after", typeof(SpriteRenderer)) { layer = Layer };
        SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
        Camera camera = new GameObject("Fixed crown camera").AddComponent<Camera>();
        RenderTexture frame = new RenderTexture(256, 192, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D before = null;
        Texture2D after = null;
        try
        {
            camera.cullingMask = 1 << Layer;
            camera.orthographic = true;
            camera.orthographicSize = .384f;
            camera.transform.position = Vector3.back * 10;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.targetTexture = frame;

            renderer.sprite = original;
            actor.transform.localScale = Vector3.one * .1f;
            result.before = Measure(path, renderer);
            // At this zoom the 768 x 576 source covers only 192 x 144 screen pixels.
            float pixelsPerMeter = frame.height / (2 * camera.orthographicSize);
            result.projectedWidth = Mathf.RoundToInt(renderer.bounds.size.x * pixelsPerMeter);
            result.projectedHeight = Mathf.RoundToInt(renderer.bounds.size.y * pixelsPerMeter);
            camera.Render();
            before = ReadPixels(frame);
            File.WriteAllBytes(Path.Combine(output, "Import-Before.png"), before.EncodeToPNG());

            Assert.That(WorldRepeatSprite.ConfigureForDisplay(path, "Standalone", result.projectedWidth, result.projectedHeight), Is.True);
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            actor.transform.localScale = Vector3.one;
            result.after = Measure(path, renderer);
            Assert.That(result.after.width, Is.EqualTo(256));
            Assert.That(result.after.height, Is.EqualTo(192));
            Assert.That(result.after.platformCap, Is.EqualTo(256));
            Assert.That(result.after.configuredPpu, Is.EqualTo(WorldPpu));
            Assert.That(result.after.worldWidth, Is.EqualTo(result.before.worldWidth).Within(.0001f));
            Assert.That(result.after.worldHeight, Is.EqualTo(result.before.worldHeight).Within(.0001f));
            Assert.That(result.after.worldWidth, Is.EqualTo(.768f).Within(.0001f));
            Assert.That(result.after.textureObjectBytes, Is.Positive.And.LessThan(result.before.textureObjectBytes));
            camera.Render();
            after = ReadPixels(frame);
            File.WriteAllBytes(Path.Combine(output, "Import-After.png"), after.EncodeToPNG());
            CompareFrames(before, after, result);
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(frame);
            if (before != null) Object.DestroyImmediate(before);
            if (after != null) Object.DestroyImmediate(after);
        }
    }

    private static ImportState Measure(string path, SpriteRenderer renderer)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        TextureImporterPlatformSettings platform = importer.GetPlatformTextureSettings("Standalone");
        Sprite sprite = renderer.sprite;
        return new ImportState
        {
            width = sprite.texture.width, height = sprite.texture.height,
            configuredPpu = importer.spritePixelsPerUnit, effectivePpu = sprite.pixelsPerUnit,
            platformCap = platform.overridden ? platform.maxTextureSize : importer.maxTextureSize,
            rendererScale = renderer.transform.localScale.x,
            worldWidth = renderer.bounds.size.x, worldHeight = renderer.bounds.size.y,
            textureObjectBytes = Profiler.GetRuntimeMemorySizeLong(sprite.texture),
            graphicsFormat = sprite.texture.graphicsFormat.ToString()
        };
    }

    private static void CompareFrames(Texture2D before, Texture2D after, Measurements result)
    {
        Color32[] reference = before.GetPixels32();
        Color32[] candidate = after.GetPixels32();
        int union = 0, intersection = 0;
        long channelError = 0;
        for (int index = 0; index < reference.Length; index++)
        {
            Color32 a = reference[index];
            Color32 b = candidate[index];
            if (a.a < 128 && b.a < 128) continue;
            union++;
            if (a.a >= 128 && b.a >= 128) intersection++;
            channelError += Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
        }
        Assert.That(union, Is.GreaterThan(1000), "Crown must be visible in the matched camera.");
        result.visibleAlphaOverlap = (float)intersection / union;
        result.meanVisibleRgbError = (float)channelError / (3 * 255 * union);
        Assert.That(result.visibleAlphaOverlap, Is.GreaterThan(.96f), "Foreground silhouette overlap");
        Assert.That(result.meanVisibleRgbError, Is.LessThan(.08f), "Normalized RGB error over foreground union");
    }

    private static void PackTree(string folder, string output, Measurements result)
    {
        string[] names = { "TreeBase", "TreeTrunk", "TreeCrown" };
        string sourceFolder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(folder, "AtlasSources"));
        Sprite[] originals = new Sprite[names.Length];
        Sprite[] packed = new Sprite[names.Length];
        SpritePackerMode priorMode = EditorSettings.spritePackerMode;
        Texture2D image = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            for (int i = 0; i < names.Length; i++)
                originals[i] = AssetDatabase.LoadAssetAtPath<Sprite>(ImportCopy(sourceFolder, names[i], WorldPpu));
            Sprite ground = AssetDatabase.LoadAssetAtPath<Sprite>(ImportCopy(sourceFolder, "Ground", WorldPpu));
            string path = folder + "/Tree.spriteatlasv2";
            SpriteAtlasAsset asset = new();
            asset.Add(originals);
            SpriteAtlasAsset.Save(asset, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            SpriteAtlasImporter importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            SpriteAtlasPackingSettings packing = importer.packingSettings;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            importer.packingSettings = packing;
            importer.SaveAndReimport();
            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
            Assert.That(atlas.spriteCount, Is.EqualTo(originals.Length));
            Assert.That(atlas.CanBindTo(ground), Is.False);
            result.pieces = new PackedPiece[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(atlas.CanBindTo(originals[i]), Is.True);
                packed[i] = atlas.GetSprite(names[i]);
                Assert.That(packed[i], Is.Not.Null);
                Assert.That(packed[i].texture, Is.EqualTo(packed[0].texture));
                Assert.That(packed[i].packingRotation, Is.EqualTo(SpritePackingRotation.None));
                Rect rect = packed[i].textureRect;
                for (int j = 0; j < i; j++) Assert.That(rect.Overlaps(packed[j].textureRect), Is.False);
                result.pieces[i] = new PackedPiece { name = names[i], x = (int)rect.x, y = (int)rect.y, width = (int)rect.width, height = (int)rect.height };
            }

            Texture2D page = packed[0].texture;
            result.atlasWidth = page.width;
            result.atlasHeight = page.height;
            result.atlasFormat = page.graphicsFormat.ToString();
            result.atlasTextureObjectBytes = Profiler.GetRuntimeMemorySizeLong(page);
            target = new RenderTexture(page.width, page.height, 0);
            Graphics.Blit(page, target);
            image = ReadPixels(target);
            Color32[] atlasPixels = image.GetPixels32();
            foreach (PackedPiece piece in result.pieces)
            {
                int opaqueSamples = 0;
                for (int y = piece.y; y < piece.y + piece.height; y += 4)
                    for (int x = piece.x; x < piece.x + piece.width; x += 4)
                        if (atlasPixels[y * page.width + x].a > 127) opaqueSamples++;
                Assert.That(opaqueSamples, Is.GreaterThan(8), piece.name + " must have visible packed pixels.");
            }
            File.WriteAllBytes(Path.Combine(output, "Atlas.png"), image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) Object.DestroyImmediate(target);
            if (image != null) Object.DestroyImmediate(image);
            foreach (Sprite sprite in packed)
                if (sprite != null) Object.DestroyImmediate(sprite);
            EditorSettings.spritePackerMode = priorMode;
        }
    }

    private static Texture2D ReadPixels(RenderTexture source)
    {
        RenderTexture.active = source;
        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        result.Apply();
        return result;
    }
}
