using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class AssetCases
    {
        [Test]
        public void Assets01_MovePreservesGuidSubassetAndTheSavedPrefabBinding()
        {
            using EditorCaseAssets assets = new EditorCaseAssets();
            string original = assets.Write("Original.png", CreatePng());
            Sprite sprite = ImportSprite(original, 4);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId), Is.True);
            string prefabPath = $"{assets.Folder}/Receiver.prefab";
            GameObject receiver = new GameObject("Receiver");
            try
            {
                receiver.AddComponent<SpriteRenderer>().sprite = sprite;
                PrefabUtility.SaveAsPrefabAsset(receiver, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(receiver);
            }

            string next = $"{assets.Folder}/Moved.png";
            Assert.That(AssetDatabase.MoveAsset(original, next), Is.Empty);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);

            Sprite bound = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<SpriteRenderer>().sprite;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bound, out string movedGuid, out long movedId), Is.True);
            Assert.That((movedGuid, movedId), Is.EqualTo((guid, fileId)));
            Assert.That(AssetDatabase.GetAssetPath(bound), Is.EqualTo(next));
            CollectionAssert.Contains(AssetDatabase.GetDependencies(prefabPath), next);
            CollectionAssert.DoesNotContain(AssetDatabase.GetDependencies(prefabPath), original);
        }

        [Test]
        public void Assets02_DynamicResourcesKeysNeedAConsumerUpdateEvenWithTheSameGuid()
        {
            using EditorCaseAssets assets = new EditorCaseAssets("Assets/Prefabs");
            Assert.That(AssetDatabase.CreateFolder(assets.Folder, "Resources"), Is.Not.Empty);
            string oldKey = $"{assets.Folder.Split('/').Last()}-old";
            string newKey = $"{assets.Folder.Split('/').Last()}-new";
            string original = assets.Write($"Resources/{oldKey}.txt", System.Text.Encoding.UTF8.GetBytes("bound through a key"));
            string guid = AssetDatabase.AssetPathToGUID(original);
            TextAsset loaded = Resources.Load<TextAsset>(oldKey);
            Assert.That(loaded.text, Is.EqualTo("bound through a key"));
            Resources.UnloadAsset(loaded);

            string moved = $"{assets.Folder}/Resources/{newKey}.txt";
            Assert.That(AssetDatabase.MoveAsset(original, moved), Is.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(moved), Is.EqualTo(guid));
            Assert.That(Resources.Load<TextAsset>(oldKey), Is.Null);
            TextAsset rebound = Resources.Load<TextAsset>(newKey);
            Assert.That(rebound.text, Is.EqualTo("bound through a key"));
            Resources.UnloadAsset(rebound);
        }

        [Test]
        public void Assets03_NativePpuChangesGeometryAndTheCompanionMapKeepsItsData()
        {
            using EditorCaseAssets assets = new EditorCaseAssets();
            byte[] png = CreatePng();
            string basePath = assets.Write("Base.png", png);
            string maskPath = assets.Write("Mask.png", png);
            Sprite original = ImportSprite(basePath, 4);
            Assert.That(original.rect.size, Is.EqualTo(new Vector2(8f, 4f)));
            Assert.That((Vector2)original.bounds.size, Is.EqualTo(new Vector2(2f, 1f)));
            Sprite resized = ImportSprite(basePath, 8);
            Assert.That((Vector2)resized.bounds.size, Is.EqualTo(new Vector2(1f, 0.5f)));
            Assert.That(resized.pivot, Is.EqualTo(new Vector2(4f, 2f)));

            TextureImporter maskImporter = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            maskImporter.sRGBTexture = false;
            maskImporter.mipmapEnabled = false;
            maskImporter.isReadable = true;
            maskImporter.textureCompression = TextureImporterCompression.Uncompressed;
            maskImporter.SaveAndReimport();
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            Assert.That((mask.width, mask.height), Is.EqualTo((resized.texture.width, resized.texture.height)));
            Assert.That(mask.GetPixels32()[0], Is.EqualTo(new Color32(24, 90, 130, 0)));
            Assert.That(mask.GetPixels32()[31], Is.EqualTo(new Color32(180, 20, 50, 255)));
            CollectionAssert.AreEqual(png, File.ReadAllBytes(basePath));
        }

        [Test]
        public void Assets04_PlayerAssemblySelectionExcludesAutomation()
        {
            string[] player = CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(assembly => assembly.name).ToArray();
            CollectionAssert.Contains(player, "ChargePulse.Runtime");
            CollectionAssert.DoesNotContain(player, "ChargePulse.EditorTests");
        }

        [Test]
        public void Assets05_AFormerSerializedFieldKeepsItsPrefabReference()
        {
            using EditorCaseAssets assets = new EditorCaseAssets();
            string path = $"{assets.Folder}/Physics.prefab";
            GameObject root = new GameObject("Physics");
            GameObject mark = new GameObject("Marker");
            mark.transform.SetParent(root.transform, false);
            try
            {
                StepMotionController motion = root.AddComponent<StepMotionController>();
                SerializedObject bindings = new SerializedObject(motion);
                bindings.FindProperty("_marker").objectReferenceValue = mark.transform;
                bindings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            string yaml = File.ReadAllText(path);
            Assert.That(yaml, Does.Contain("  _marker:"));
            File.WriteAllText(path, yaml.Replace("  _marker:", "  _indicator:"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SerializedObject restored = new SerializedObject(imported.GetComponent<StepMotionController>());
            Assert.That(restored.FindProperty("_marker").objectReferenceValue, Is.SameAs(imported.transform.GetChild(0)));
        }

        private static Sprite ImportSprite(string path, float ppu)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static byte[] CreatePng()
        {
            Texture2D texture = new Texture2D(8, 4, TextureFormat.RGBA32, false, true);
            try
            {
                Color32[] pixels = new Color32[32];
                for (int index = 0; index < pixels.Length; index++)
                    pixels[index] = index < 8 ? new Color32(24, 90, 130, 0) : new Color32(180, 20, 50, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
