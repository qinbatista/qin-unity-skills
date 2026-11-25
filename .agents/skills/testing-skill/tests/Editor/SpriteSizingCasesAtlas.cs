using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public sealed class SpriteSizingCasesAtlas
{
    [Test]
    public void CoUsedPiecesPackOnOnePageWithoutTheLargeBackdrop()
    {
        string folderName = "AtlasCase" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folderName);
        string folder = "Assets/" + folderName;
        SpritePackerMode previousMode = EditorSettings.spritePackerMode;
        Sprite packedLeft = null;
        Sprite packedRight = null;
        try
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            Sprite left = ImportSprite(folder + "/Left.png", 32, Color.red);
            Sprite right = ImportSprite(folder + "/Right.png", 32, Color.green);
            Sprite backdrop = ImportSprite(folder + "/Backdrop.png", 512, Color.blue);
            string path = folder + "/Panel.spriteatlasv2";
            SpriteAtlasAsset asset = new SpriteAtlasAsset();
            asset.Add(new UnityEngine.Object[] { left, right });
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
            Assert.That(atlas, Is.Not.Null);
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);

            Assert.That(atlas.spriteCount, Is.EqualTo(2));
            Assert.That(atlas.CanBindTo(backdrop), Is.False);
            packedLeft = atlas.GetSprite(left.name);
            packedRight = atlas.GetSprite(right.name);
            Assert.That(packedLeft, Is.Not.Null);
            Assert.That(packedRight, Is.Not.Null);
            Assert.That(packedLeft.packingRotation, Is.EqualTo(SpritePackingRotation.None));
            Assert.That(packedRight.packingRotation, Is.EqualTo(SpritePackingRotation.None));
            Assert.That(packedLeft.textureRect.Overlaps(packedRight.textureRect), Is.False);
            Texture2D page = packedLeft.texture;
            Assert.That(packedRight.texture, Is.EqualTo(page));
            Assert.That(page.width * page.height, Is.LessThan(backdrop.texture.width * backdrop.texture.height));
            TestContext.Out.WriteLine($"Two 32 x 32 pieces share one {page.width} x {page.height} {page.graphicsFormat} page. The 512 x 512 backdrop is outside this atlas.");
        }
        finally
        {
            if (packedLeft != null) UnityEngine.Object.DestroyImmediate(packedLeft);
            if (packedRight != null) UnityEngine.Object.DestroyImmediate(packedRight);
            AssetDatabase.DeleteAsset(folder);
            EditorSettings.spritePackerMode = previousMode;
        }
    }

    private static Sprite ImportSprite(string path, int side, Color32 color)
    {
        Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        try
        {
            Color32[] pixels = new Color32[side * side];
            Array.Fill(pixels, color);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
