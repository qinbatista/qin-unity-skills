using System.Collections;
using System.IO;
using NUnit.Framework;
using Qin.UnityAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public sealed class SpriteSizingCases
{
    private string _directory;

    [TearDown]
    public void TearDown()
    {
        if (_directory != null && AssetDatabase.IsValidFolder(_directory)) AssetDatabase.DeleteAsset(_directory);
    }

    [Test]
    public void ImportCorrectsDensityAndReducesLoadedTexture()
    {
        string path = WriteSprite("Oversized", 2000, 1000);
        Sprite original = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(original.texture.width, Is.EqualTo(2000));
        Assert.That(original.bounds.size.x, Is.EqualTo(20f).Within(.001f));
        long before = Profiler.GetRuntimeMemorySizeLong(original.texture);
        string guid = AssetDatabase.AssetPathToGUID(path);
        byte[] originalFile = File.ReadAllBytes(path);

        // A 260-pixel height needs 520 pixels along the 2:1 source's long edge.
        Assert.That(WorldRepeatSprite.ConfigureForDisplay(path, "Standalone", 350, 260), Is.True);
        Sprite current = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(1000f));
        Assert.That(importer.GetPlatformTextureSettings("Standalone").maxTextureSize, Is.EqualTo(1024));
        Assert.That(new Vector2(current.texture.width, current.texture.height), Is.EqualTo(new Vector2(1024, 512)));
        Assert.That(current.bounds.size.x, Is.EqualTo(2f).Within(.001f));
        Assert.That(current.bounds.size.y, Is.EqualTo(1f).Within(.001f));
        Assert.That(current.texture.isReadable, Is.False);
        Assert.That(current.texture.mipmapCount, Is.EqualTo(1));
        long after = Profiler.GetRuntimeMemorySizeLong(current.texture);
        Assert.That(after, Is.Positive.And.LessThan(before));
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalFile));
        Assert.That(WorldRepeatSprite.ConfigureForDisplay(path, "Standalone", 350, 260), Is.False);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => WorldRepeatSprite.ConfigureImport(path, "Standalone", 1000));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => WorldRepeatSprite.ConfigureForDisplay(path, "Standalone", 0, 260));
        TestContext.Out.WriteLine($"Projected 350x260, selected cap 1024. Imported 2000x1000 to {current.texture.width}x{current.texture.height}, native PPU {current.pixelsPerUnit}, {current.bounds.size.x} x {current.bounds.size.y} units. Texture object bytes {before} to {after}.");
        Assert.That(WorldRepeatSprite.ConfigureForDisplay(path, "Standalone", 512, 256), Is.True);
        Sprite exact = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(exact.texture.width, Is.EqualTo(512));
        Assert.That(exact.texture.height, Is.EqualTo(256));
        Assert.That(exact.bounds.size.x, Is.EqualTo(2f).Within(.001f));
    }

    private string WriteSprite(string name, int width, int height)
    {
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/SpriteSizingTrial");
        _directory = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)));
        string path = $"{_directory}/{name}.png";
        Texture2D source = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            Color32[] colors = new Color32[width * height];
            for (int index = 0; index < colors.Length; index++) colors[index] = index % width < width / 2 ? Color.red : Color.green;
            colors[0] = Color.clear;
            source.SetPixels32(colors);
            source.Apply();
            File.WriteAllBytes(path, source.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return path;
    }

    [Test]
    public void GroundAndCappedTrunkKeepTheirRepeatPitchAfterResize()
    {
        string path = WriteSprite("Repeat", 1000, 250);
        Assert.That(WorldRepeatSprite.ConfigureImport(path, "Standalone", 1024), Is.True);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(1000));
        Assert.That(sprite.bounds.size.x, Is.EqualTo(1f).Within(.001f));
        GameObject scene = new GameObject("Sprite sizing fixture");
        Camera camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(scene.transform);
        camera.transform.position = new Vector3(1.5f, 1f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 2f;
        RenderTexture target = new RenderTexture(256, 256, 24);
        Texture2D reader = new Texture2D(target.width, target.height);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            SpriteRenderer ground = NewRenderer("Ground", scene.transform, sprite);
            SpriteRenderer trunk = NewRenderer("Trunk", scene.transform, sprite);
            trunk.transform.rotation = Quaternion.Euler(0, 0, 90);
            SpriteRenderer root = NewRenderer("Root", scene.transform, sprite);
            SpriteRenderer crown = NewRenderer("Crown", scene.transform, sprite);
            root.transform.position = new Vector3(2, .375f);
            root.flipX = true;
            foreach (float extent in new[] { 1f, 1.5f, 2f })
            {
                ground.size = new Vector2(extent, .25f);
                ground.transform.position = new Vector3(extent * .5f, 0);
                trunk.size = new Vector2(extent, .25f);
                trunk.transform.position = new Vector3(2, .5f + extent * .5f);
                crown.transform.position = new Vector3(2, .625f + extent);
                camera.Render();
                RenderTexture.active = target;
                reader.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                reader.Apply();
                SaveFrame(reader, $"ground-trunk-{extent}.png");
                Color Sample(float x, float y)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(new Vector3(x, y));
                    return reader.GetPixel(Mathf.FloorToInt(viewport.x * target.width), Mathf.FloorToInt(viewport.y * target.height));
                }
                for (int tile = 0; tile < Mathf.FloorToInt(extent); tile++)
                {
                    Assert.That(Sample(tile + .25f, 0).r, Is.GreaterThan(.9f), "Ground start");
                    Assert.That(Sample(tile + .75f, 0).g, Is.GreaterThan(.9f), "Ground end");
                    Assert.That(Sample(2, .75f + tile).r, Is.GreaterThan(.9f), "Trunk start");
                    Assert.That(Sample(2, 1.25f + tile).g, Is.GreaterThan(.9f), "Trunk end");
                }
                if (extent == 1.5f)
                {
                    Assert.That(Sample(1.375f, 0).r, Is.GreaterThan(.9f), "Partial ground period");
                    Assert.That(Sample(1.625f, 0).maxColorComponent, Is.LessThan(.1f), "Ground stops at its target width");
                    Assert.That(Sample(2, 1.875f).r, Is.GreaterThan(.9f), "Partial trunk period");
                }
                Assert.That(Sample(1.65f, .375f).g, Is.GreaterThan(.9f), "Root cap");
                Assert.That(Sample(1.65f, .625f + extent).r, Is.GreaterThan(.9f), "Crown cap");
                Assert.That(Sample(1.65f, .875f).maxColorComponent, Is.LessThan(.1f), "No stretched cap");
                Assert.That(ground.sprite.texture, Is.SameAs(trunk.sprite.texture));
            }
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(scene);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(reader);
        }
    }

    private static SpriteRenderer NewRenderer(string name, Transform parent, Sprite sprite)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent);
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = sprite.bounds.size;
        return renderer;
    }

    [UnityTest]
    public IEnumerator ToolkitButtonPreservesEightPixelEdgesAndExpandedHitRegion()
    {
        yield return new EnterPlayMode();
        GameObject host = new GameObject("Resizable button");
        host.SetActive(false);
        PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
        RenderTexture target = new RenderTexture(256, 96, 24);
        Texture2D source = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Texture2D reader = new Texture2D(target.width, target.height);
        Sprite tiled = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/PulseTheme.tss");
            Assert.That(settings.themeStyleSheet, Is.Not.Null);
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1;
            settings.referenceSpritePixelsPerUnit = 1000;
            settings.targetTexture = target;
            settings.clearColor = true;
            UIDocument document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            host.SetActive(true);
            VisualElement root = document.rootVisualElement;
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ResizableBacking.uss");
            Assert.That(sheet, Is.Not.Null);
            root.styleSheets.Add(sheet);
            Button button = new Button();
            button.ClearClassList();
            button.AddToClassList("resizable-backing");
            button.style.position = Position.Absolute;
            button.style.top = 0;
            button.style.left = 0;
            Color32[] pixels = new Color32[source.width * source.height];
            for (int y = 0; y < source.height; y++)
                for (int x = 0; x < source.width; x++)
                    pixels[y * source.width + x] = x < 8 || x >= 24 || y < 8 || y >= 24 ? Color.red : Color.green;
            source.SetPixels32(pixels);
            source.Apply();
            root.Add(button);
            button.style.backgroundImage = Background.FromTexture2D(source);
            foreach ((int width, int height, bool useTiles) in new[] { (96, 40, false), (224, 64, false), (32, 32, true), (96, 40, true), (105, 45, true) })
            {
                if (useTiles && tiled == null)
                {
                    for (int y = 8; y < 24; y++)
                        for (int x = 12; x < 16; x++) pixels[y * source.width + x] = Color.blue;
                    source.SetPixels32(pixels);
                    source.Apply();
                    tiled = Sprite.Create(source, new Rect(0, 0, 32, 32), Vector2.one * .5f, 1000, 0, SpriteMeshType.FullRect, Vector4.one * 8);
                    button.style.backgroundImage = Background.FromSprite(tiled);
                    button.AddToClassList("resizable-backing--tiled");
                }
                button.style.width = width;
                button.style.height = height;
                yield return null;
                yield return null;
                yield return null;
                Assert.That(root.panel.Pick(new Vector2(width - 2, height / 2)), Is.SameAs(button));
                Assert.That(target.IsCreated(), Is.True);
                RenderTexture.active = target;
                reader.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                reader.Apply();
                string mode = tiled == null ? "sliced" : "tiled";
                SaveFrame(reader, $"button-{mode}-{width}x{height}.png");
                Color At(int x, int y) => reader.GetPixel(x, target.height - 1 - y);
                int midY = height / 2;
                Assert.That(At(7, midY).r, Is.GreaterThan(.9f), "Left border");
                Assert.That(At(8, midY).g, Is.GreaterThan(.9f), "Left center");
                Assert.That(At(width - 8, midY).r, Is.GreaterThan(.9f), "Right border");
                Assert.That(At(width / 2, 7).r, Is.GreaterThan(.9f), "Top border");
                Assert.That(At(width / 2, height - 8).r, Is.GreaterThan(.9f), "Bottom border");
                if (tiled == null)
                {
                    Assert.That(button.resolvedStyle.backgroundImage.texture, Is.SameAs(source));
                    Assert.That(At(width - 9, midY).g, Is.GreaterThan(.9f), "Right center");
                    Assert.That(At(width / 2, height - 9).g, Is.GreaterThan(.9f), "Bottom center");
                }
                else
                {
                    Assert.That(button.resolvedStyle.backgroundImage.sprite, Is.SameAs(tiled));
                    for (int x = 8; x < width - 8; x++)
                    {
                        int phase = (x - 8) % 16;
                        float expectedChannel = phase >= 4 && phase < 8 ? At(x, midY).b : At(x, midY).g;
                        Assert.That(expectedChannel, Is.GreaterThan(.9f), $"Tiled motif at {x} of {width}");
                    }
                }
            }
        }
        finally
        {
            RenderTexture.active = previous;
            settings.targetTexture = null;
            Object.Destroy(host);
            Object.Destroy(settings);
            Object.Destroy(target);
            Object.Destroy(reader);
            Object.Destroy(tiled);
            Object.Destroy(source);
        }
    }

    private static void SaveFrame(Texture2D frame, string filename)
    {
        string directory = System.Environment.GetEnvironmentVariable("SPRITE_SIZING_FRAMES");
        if (!string.IsNullOrEmpty(directory)) File.WriteAllBytes(Path.Combine(directory, filename), frame.EncodeToPNG());
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
