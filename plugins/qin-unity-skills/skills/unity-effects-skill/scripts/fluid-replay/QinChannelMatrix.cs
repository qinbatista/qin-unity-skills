using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.VFX;

public sealed partial class QinFluidReplay
{
    private static readonly string[] MatrixChannels =
        { "DryDamBreak", "WetBedBore", "ReservoirFill", "ReservoirDrawdown", "TidalReversal", "DrivenTank" };
    private const int MatrixStop = 151;
    private const int MatrixLast = 165;

    [UnityTest]
    public IEnumerator ChannelMatrix([ValueSource(nameof(MatrixChannels))] string form)
    {
        string root = ApplicationCapture(form);
        string path = $"Assets/GameWater/{form}/{form}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Variant));
        var instance = Object.Instantiate(prefab, stage.transform);
        var water = instance.GetComponent<ChannelWater2D>();
        var surface = instance.GetComponentInChildren<MeshRenderer>();
        var foam = instance.GetComponentInChildren<VisualEffect>();
        Assert.That(water.IsRunning && foam.visualEffectAsset, Is.True);
        Assert.That(AssetDatabase.GetAssetPath(surface.sharedMaterial.shader), Does.EndWith(".shadergraph"));
        Assert.That(surface.sharedMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(surface.sharedMaterial.shader), Is.True);
        surface.sortingLayerName = WaterLayer;
        foam.GetComponent<Renderer>().sortingLayerName = WaterLayer;
        byte[] initial = ChannelState(surface);
        float[] field = new float[initial.Length / sizeof(float)];
        System.Buffer.BlockCopy(initial, 0, field, 0, initial.Length);
        bool gates = form == "DryDamBreak" || form == "WetBedBore";
        SetChannelStage(form == "TidalReversal" ? ChannelWater2D.Boundary.OpenRiver : ChannelWater2D.Boundary.ClosedBasin, field);
        float bed = field[256 * 4 + 1];
        Transform gate = DressChannelFixture(form, bed, gates);
        instance.SetActive(false);
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);
        instance.SetActive(true);
        Dictionary<int, byte[]> normalGauge = new();
        List<string> gaugeChanges = new();
        foreach (string mode in new[] { "normal", "control" })
        {
            bool normal = mode == "normal";
            string images = Directory.CreateDirectory(Path.Combine(root, mode)).FullName;
            string states = Directory.CreateDirectory(Path.Combine(root, mode + "-state")).FullName;
            water.BeginFlow(Seed);
            if (!normal) water.SetDriver(0);
            for (int frame = 0; frame <= MatrixLast; frame++)
            {
                double time = frame / (double)Fps;
                water.SetTime(time);
                if (form == "DrivenTank")
                {
                    // Both controls receive the same signed input schedule and monotonic clock.
                    if (frame == 60) water.SetTankAcceleration(1.55f);
                    if (frame == 87) water.SetTankAcceleration(-1.55f);
                    if (frame == 114) water.SetTankAcceleration(0f);
                }
                if (frame == MatrixStop) water.EndFlow();
                if (gate) gate.localPosition = new Vector3(-.609375f, bed + .58f + (water.IsRunning ? water.GateAperture * 1.20f : 0f), 0f);
                yield return null;
                water.SetTime(time);
                if (normal || frame % 6 == 0 || frame == MatrixLast) SaveFrame(images, frame);
                if (frame % 6 == 0 || frame == MatrixLast)
                {
                    // Downstream of the gate and clear of either port and the depth staff.
                    int delta = CompareChannelGauge(normalGauge, frame, normal);
                    if (!normal) gaugeChanges.Add(frame + "," + delta);
                }
                if (frame <= 150 && frame % 3 == 0)
                {
                    byte[] state = ChannelState(surface);
                    File.WriteAllBytes(Path.Combine(states, $"{frame:D4}.bin"), state);
                    if (frame == 60)
                    {
                        water.SetTime(time);
                        CollectionAssert.AreEqual(state, ChannelState(surface), "The held GPU state must be stable");
                    }
                }
            }
            Assert.That(water.IsRunning, Is.False);
            CollectionAssert.AreEqual(dry, Capture(), "A retired water body must clear its renderer");
        }
        File.WriteAllLines(Path.Combine(root, "gauge-pixels.csv"), gaugeChanges);
    }

    private int CompareChannelGauge(Dictionary<int, byte[]> references, int frame, bool store)
    {
        const int x = 520, y = 108, w = 248, h = 225;
        var rgb = pixels.GetRawTextureData<byte>();
        if (store)
        {
            byte[] reference = new byte[3 * w * h];
            for (int row = 0; row < h; row++)
                Unity.Collections.NativeArray<byte>.Copy(rgb, 3 * ((y + row) * Width + x), reference, 3 * row * w, 3 * w);
            references.Add(frame, reference);
            return 0;
        }
        var previous = references[frame];
        int count = 0;
        for (int row = 0; row < h; row++)
        {
            int offset = 3 * ((y + row) * Width + x);
            for (int col = 0; col < w; col++)
            {
                int i = offset + col * 3, j = (row * w + col) * 3;
                if (Mathf.Abs(rgb[i] - previous[j]) >= 10 || Mathf.Abs(rgb[i+1] - previous[j+1]) >= 10
                    || Mathf.Abs(rgb[i+2] - previous[j+2]) >= 10) count++;
            }
        }
        return count;
    }

    private Transform DressChannelFixture(string form, float bed, bool gate)
    {
        Texture2D pixel = new(1, 1, TextureFormat.RGBA32, false);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply(false, true);
        var sprite = Sprite.Create(pixel, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
        applicationResources.Add(sprite);
        applicationResources.Add(pixel);
        Color steel = new(.24f, .29f, .28f);
        Color brass = new(.67f, .56f, .35f);
        Transform Plate(string label, float x, float y, float w, float h, Color color, int order = -12)
        {
            SpriteRenderer plate = new GameObject(label, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            plate.transform.SetParent(stage.transform, false);
            plate.transform.localPosition = new Vector3(x, y, 0f);
            plate.transform.localScale = new Vector3(w, h, 1f);
            plate.sprite = sprite;
            plate.sharedMaterial = spriteMaterial;
            plate.color = color;
            plate.sortingOrder = order;
            return plate.transform;
        }
        if (gate)
        {
            Plate("Gate guide", -.71f, bed + .89f, .045f, 1.78f, steel);
            Plate("Gate bridge", -.60f, bed + 1.77f, .39f, .075f, brass);
            return Plate("Sluice gate blade", -.609375f, bed + .58f, .068f, 1.16f, brass, -10);
        }
        if (form == "ReservoirFill" || form == "ReservoirDrawdown")
        {
            float side = form == "ReservoirFill" ? -1f : 1f;
            Plate("Port housing", side * 2.96f, bed + .14f, .22f, .29f, steel);
            Plate("Port mouth", side * 2.86f, bed + .14f, .08f, .15f, new Color(.10f,.15f,.16f));
        }
        float gaugeX = form == "TidalReversal" ? -2.22f : 2.60f;
        Plate("Depth staff", gaugeX, bed + .62f, .034f, 1.24f, steel);
        for (int i = 1; i < 12; i++)
            Plate("Depth graduation", gaugeX + .029f, bed + i * .10f, i % 5 == 0 ? .11f : .065f, .012f, brass);
        return null;
    }
}
