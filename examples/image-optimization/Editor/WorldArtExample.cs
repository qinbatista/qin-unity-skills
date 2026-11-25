using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WorldArtExample
{
    // One design pixel in the foreground masters covers 8 source texels at 1000 PPU.
    private const float Step = 8f / 1000f;
    private const int Width = 420;
    private const int Height = 224;
    private const int Zoom = 2;
    private const int Layer = 29;
    private const string PrefabPath = "Assets/ImageExamples/Woodland.prefab";

    [Test]
    public void GroundAndOakResizeFromSharedArtwork()
    {
        Sprite turf = Import("Ground");
        Sprite foot = Import("TreeBase");
        Sprite trunk = Import("TreeTrunk");
        Sprite crown = Import("TreeCrown");
        Sprite valley = Import("Valley");
        GameObject scene = new("Woodland artwork example");
        GameObject artwork = new("Woodland");
        artwork.transform.SetParent(scene.transform, false);
        Camera camera = new GameObject("Landscape camera").AddComponent<Camera>();
        camera.transform.SetParent(scene.transform);
        camera.transform.position = new Vector3(Width * Step / 2, Height * Step / 2, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = Height * Step / 2;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << Layer;
        RenderTexture target = new(Width * Zoom, Height * Zoom, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D frame = new(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            var backdrop = Place(artwork.transform, valley, 0, 0, Width, Height, -10);
            backdrop.transform.localScale = new Vector3(Width * Step / valley.bounds.size.x,
                Height * Step / valley.bounds.size.y, 1);
            Capture(camera, frame);
            Color32[] background = frame.GetPixels32();

            var shortGround = Place(artwork.transform, turf, 13, 166, 164, 30, 0, true);
            var longGround = Place(artwork.transform, turf, 204, 158, 204, 30, 0, true);
            shortGround.name = "Short Ledge";
            longGround.name = "Long Ledge";
            var shortTree = PlaceTree(artwork.transform, "Short Oak", foot, trunk, crown, 98, 170, 20);
            var tallTree = PlaceTree(artwork.transform, "Tall Oak", foot, trunk, crown, 305, 162, 50);

            Assert.That(shortGround.sprite, Is.SameAs(longGround.sprite));
            Assert.That(longGround.bounds.size.x, Is.EqualTo(204 * Step).Within(.0001f));
            Assert.That(shortGround.bounds.size.x, Is.EqualTo(164 * Step).Within(.0001f));
            Assert.That(tallTree.body.bounds.size.y - shortTree.body.bounds.size.y,
                Is.EqualTo(30 * Step).Within(.0001f));
            Assert.That(shortTree.body.sprite.texture, Is.SameAs(tallTree.body.sprite.texture));
            Assert.That(shortTree.top.sprite, Is.SameAs(tallTree.top.sprite));
            Assert.That(shortTree.roots.sprite, Is.SameAs(tallTree.roots.sprite));

            Capture(camera, frame);
            Color32[] pixels = frame.GetPixels32();
            // First and second periods plus the fractional tail of the long ledge.
            ComparePatch(pixels, background, 206, 169, 254, 169, 35, 11);
            ComparePatch(pixels, background, 206, 169, 398, 169, 8, 11);
            // Compare two vertical bark periods and the same opaque crown details on both oaks.
            ComparePatch(pixels, background, 301, 101, 301, 119, 8, 11);
            ComparePatch(pixels, background, 77, 84, 284, 46, 39, 17);
            int pastEdge = Index(409, 171);
            Assert.That(pixels[pastEdge], Is.EqualTo(background[pastEdge]), "Ledge stops at its configured width.");

            SaveEditableWoodland(artwork, turf, foot, trunk, crown);

            string output = Environment.GetEnvironmentVariable("IMAGE_EXAMPLE_OUTPUT");
            Assert.That(output, Is.Not.Null.And.Not.Empty, "Provide an output directory for the native frame.");
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "World.png"), frame.EncodeToPNG());
            string package = Path.Combine(output, "Woodland.unitypackage");
            string[] members = { PrefabPath, AssetDatabase.GetAssetPath(turf), AssetDatabase.GetAssetPath(foot),
                AssetDatabase.GetAssetPath(trunk), AssetDatabase.GetAssetPath(crown), AssetDatabase.GetAssetPath(valley) };
            AssetDatabase.ExportPackage(members, package, ExportPackageOptions.Default);
            Assert.That(new FileInfo(package).Length, Is.GreaterThan(0L));
            TestContext.WriteLine($"Ground {shortGround.size.x:F3}/{longGround.size.x:F3} m, " +
                $"trunk {shortTree.body.size.y:F3}/{tallTree.body.size.y:F3} m, authored PPU {turf.pixelsPerUnit}.");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(scene);
            Object.DestroyImmediate(frame);
            Object.DestroyImmediate(target);
        }
    }

    private static Sprite Import(string name)
    {
        string path = "Assets/ImageExamples/World/" + name + ".png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer, Is.Not.Null, path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spritePixelsPerUnit = 1000;
        settings.readable = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Assert.That(sprite, Is.Not.Null, path);
        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(1000f), path);
        return sprite;
    }

    private static SpriteRenderer Place(Transform parent, Sprite sprite, int left, int top, int width,
        int height, int sortingOrder, bool repeat = false)
    {
        GameObject item = new(sprite.name);
        item.layer = Layer;
        item.transform.SetParent(parent, false);
        item.transform.position = new Vector3((left + width / 2f) * Step, (Height - top - height / 2f) * Step);
        var renderer = item.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        if (repeat)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(width * Step, height * Step);
        }
        return renderer;
    }

    private static (SpriteRenderer roots, SpriteRenderer body, SpriteRenderer top) PlaceTree(
        Transform parent, string name, Sprite foot, Sprite trunk, Sprite crown, int x, int baseY, int bodyHeight)
    {
        GameObject oak = new(name);
        oak.transform.SetParent(parent, false);
        oak.transform.position = new Vector3(x * Step, (Height - baseY) * Step);
        int bodyBottom = baseY - 15;
        int crownBottom = bodyBottom - bodyHeight;
        return (Place(oak.transform, foot, x - 20, bodyBottom, 40, 15, 2),
            Place(oak.transform, trunk, x - 9, crownBottom, 18, bodyHeight, 1, true),
            Place(oak.transform, crown, x - 48, crownBottom - 72, 96, 72, 3));
    }

    private static void SaveEditableWoodland(GameObject artwork, Sprite turf, Sprite foot, Sprite trunk, Sprite crown)
    {
        PrefabUtility.SaveAsPrefabAsset(artwork, PrefabPath, out bool saved);
        Assert.That(saved, Is.True, PrefabPath);
        GameObject reopened = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            SpriteRenderer Check(string child, Sprite expected, SpriteDrawMode mode)
            {
                var renderer = reopened.transform.Find(child).GetComponent<SpriteRenderer>();
                Assert.That(renderer.sprite, Is.EqualTo(expected), child);
                Assert.That(renderer.drawMode, Is.EqualTo(mode), child);
                if (mode == SpriteDrawMode.Tiled)
                    Assert.That(renderer.tileMode, Is.EqualTo(SpriteTileMode.Continuous), child);
                return renderer;
            }

            var ground = Check("Long Ledge", turf, SpriteDrawMode.Tiled);
            var body = Check("Tall Oak/" + trunk.name, trunk, SpriteDrawMode.Tiled);
            Check("Tall Oak/" + foot.name, foot, SpriteDrawMode.Simple);
            Check("Tall Oak/" + crown.name, crown, SpriteDrawMode.Simple);
            Assert.That(ground.size.x, Is.EqualTo(204 * Step).Within(.0001f));
            Assert.That(body.size.y, Is.EqualTo(50 * Step).Within(.0001f));
            ground.size += new Vector2(turf.bounds.size.x, 0);
            body.size += new Vector2(0, trunk.bounds.size.y);
            Assert.That(ground.bounds.size.x, Is.EqualTo(252 * Step).Within(.0001f));
            Assert.That(body.bounds.size.y, Is.EqualTo(68 * Step).Within(.0001f));
        }
        finally
        {
            // Leave the saved prefab at the dimensions shown in the frame.
            PrefabUtility.UnloadPrefabContents(reopened);
        }
    }

    private static void Capture(Camera camera, Texture2D frame)
    {
        camera.Render();
        RenderTexture.active = camera.targetTexture;
        frame.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
        frame.Apply();
    }

    private static int Index(int x, int y)
    {
        return (Height * Zoom - 1 - y * Zoom) * Width * Zoom + x * Zoom;
    }

    private static void ComparePatch(Color32[] pixels, Color32[] background,
        int x1, int y1, int x2, int y2, int width, int height)
    {
        int matchingPaint = 0;
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int a = Index(x1 + dx, y1 + dy);
                int b = Index(x2 + dx, y2 + dy);
                if (!pixels[a].Equals(background[a]) && pixels[a].Equals(pixels[b]))
                    matchingPaint++;
            }
        }
        Assert.That(matchingPaint, Is.GreaterThan(width * height * .95f),
            $"Visible painted detail from ({x1},{y1}) must recur at ({x2},{y2}).");
    }
}
