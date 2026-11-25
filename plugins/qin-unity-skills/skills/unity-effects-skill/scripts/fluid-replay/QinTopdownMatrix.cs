using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class QinFluidReplay
{
    static readonly string[] TopdownSubjects =
        { "BoatWake", "IslandCurrent", "StreamConfluence", "ForkedChannel", "DrainVortex", "RainPond" };
    static readonly int[] TopdownSamples = { 0, 30, 60, 105, 150, 179 };

    [UnityTest]
    public IEnumerator TopdownMatrix([ValueSource(nameof(TopdownSubjects))] string subject)
    {
        string root = ApplicationCapture(subject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/GameWater/{subject}/{subject}.prefab");
        Assert.That(prefab && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant, Is.True);
        var instance = Object.Instantiate(prefab, stage.transform, false);
        var grid = instance.GetComponent<TopDownWater2D>();
        grid.Automatic = false;
        Assert.That(grid.Profile.ToString(), Is.EqualTo(subject));
        Assert.That(grid.Field && grid.Field.width == 128 && grid.Field.height == 72, Is.True);
        var material = instance.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.That(material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), Is.True);
        Assert.That(AssetDatabase.GetAssetPath(material.shader), Does.EndWith(".shadergraph"));
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographicSize = 2.25f;
        yield return null;
        try
        {
            foreach (bool input in new[] { true, false })
            {
                string folder = Path.Combine(root, input ? "normal" : "off");
                Directory.CreateDirectory(folder);
                grid.InputEnabled = input;
                grid.ResetWater();
                for (int tick = 0; tick < 180; tick++)
                {
                    if (tick != 0) grid.Advance(1f / 30f);
                    bool probe = Array.IndexOf(TopdownSamples, tick) >= 0;
                    if (input || probe) SaveFrame(folder, tick);
                    if (probe) SaveTopdownField(grid.Field, folder, tick);
                    if ((tick & 15) == 15) yield return null;
                }
                if (input)
                {
                    string coast = Path.Combine(root, "coast");
                    Directory.CreateDirectory(coast);
                    grid.InputEnabled = false;
                    for (int tick = 0; tick < 12; tick++) grid.Advance(1f / 30f);
                    SaveTopdownField(grid.Field, coast, 12);
                    SaveFrame(coast, 12);
                }
            }
            if (subject == "RainPond")
            {
                string pulse = Path.Combine(root, "impulse");
                Directory.CreateDirectory(pulse);
                grid.ResetWater();
                grid.Impulse(new Vector2(-1.15f, .32f), .12f, .09f);
                for (int tick = 1; tick <= 30; tick++)
                {
                    grid.Advance(1f / 30f);
                    if (tick == 1 || tick == 30) SaveTopdownField(grid.Field, pulse, tick);
                }
                string multi = Path.Combine(root, "simultaneous");
                Directory.CreateDirectory(multi);
                for (int mask = 1; mask <= 3; mask++)
                {
                    grid.ResetWater();
                    if ((mask & 1) != 0) grid.Impulse(new Vector2(-1.15f, .32f), .10f, .12f);
                    if ((mask & 2) != 0) grid.Impulse(new Vector2(1.21f, -.54f), .08f, .12f);
                    Assert.That(grid.Elapsed, Is.Zero);
                    SaveTopdownField(grid.Field, multi, mask);
                }
                for (int tick = 0; tick < 12; tick++) grid.Advance(1f / 30f);
                SaveTopdownField(grid.Field, multi, 12);
                SaveFrame(multi, 12);
            }
        }
        finally { Object.Destroy(instance); }
    }

    static void SaveTopdownField(RenderTexture grid, string folder, int tick)
    {
        var request = AsyncGPUReadback.Request(grid, 0, TextureFormat.RGBAFloat);
        request.WaitForCompletion();
        Assert.That(request.hasError, Is.False);
        byte[] bytes = request.GetData<byte>().ToArray();
        Assert.That(bytes.Length, Is.EqualTo(128 * 72 * 16));
        File.WriteAllBytes(Path.Combine(folder, $"field-{tick:D4}.bin"), bytes);
    }
}
