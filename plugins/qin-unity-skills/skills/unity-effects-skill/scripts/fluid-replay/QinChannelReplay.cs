using System.Collections;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.VFX;

public sealed partial class QinFluidReplay
{
    private static readonly string[] Channels = { "RiverCurrent", "ShoreWash", "BasinSlosh", "CoveWash" };
    private const int ChannelLastFrame = 255;

    [UnityTest]
    public IEnumerator FlowChannel([ValueSource(nameof(Channels))] string form)
    {
        string root = ApplicationCapture(form);
        string path = form == "CoveWash" ? "Assets/GameWater/CoveWash/CoveWash.prefab" : Imported + "/Channels/" + form + ".prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(asset, Is.Not.Null, path);
        GameObject instance = Object.Instantiate(asset, stage.transform);
        var water = instance.GetComponent<ChannelWater2D>();
        var surface = instance.GetComponentInChildren<MeshRenderer>();
        var foam = instance.GetComponentInChildren<VisualEffect>();
        surface.sortingLayerName = WaterLayer;
        foam.GetComponent<Renderer>().sortingLayerName = WaterLayer;
        Assert.That(water.IsRunning && foam.visualEffectAsset, Is.True);
        Assert.That(instance.GetComponentsInChildren<ParticleSystem>().Length, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(surface.sharedMaterial.shader), Does.EndWith(".shadergraph"));
        float[] presentation = new float[4 * 128 * 4];
        System.Buffer.BlockCopy(ChannelState(surface), 0, presentation, 0, presentation.Length * sizeof(float));
        SetChannelStage(water.Profile, presentation);
        instance.SetActive(false);
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);
        instance.SetActive(true);
        string frames = Path.Combine(root, "normal");
        Directory.CreateDirectory(frames);
        foreach (string mode in new[] { "normal", "still", "alternate" })
        {
            string samples = Path.Combine(root, mode + "-state");
            Directory.CreateDirectory(samples);
            water.BeginFlow(mode == "alternate" ? Seed ^ 0x9e3779b9u : Seed);
            if (mode == "still") water.SetDriver(0);
            int last = mode == "alternate" ? 60 : ChannelLastFrame;
            for (int frame = 0; frame <= last; frame++)
            {
                double seconds = frame / (double)Fps;
                water.SetTime(seconds);
                if (frame == ChannelLastFrame - 14) water.EndFlow();
                yield return null;
                water.SetTime(seconds);
                if (mode == "normal") SaveFrame(frames, frame);
                if (frame % 6 == 0 && frame <= ChannelLastFrame - 15)
                {
                    byte[] values = ChannelState(surface);
                    File.WriteAllBytes(Path.Combine(samples, $"{frame:D4}.bin"), values);
                    if (frame == 60)
                    {
                        water.SetTime(seconds);
                        Assert.That(ChannelState(surface), Is.EqualTo(values), "Held channel time must preserve the field");
                        if (mode != "normal") File.WriteAllBytes(Path.Combine(root, mode + ".png"), Capture());
                    }
                }
                if (mode == "normal" && frame == 66) SaveOptics(root);
            }
            if (mode != "alternate")
            {
                Assert.That(water.IsRunning, Is.False);
                Assert.That(Capture(), Is.EqualTo(dry));
            }
        }
        water.BeginFlow(Seed);
        water.SetTime(2);
        Assert.That(ChannelState(surface), Is.EqualTo(File.ReadAllBytes(Path.Combine(root, "normal-state/0060.bin"))));
    }

    private static byte[] ChannelState(MeshRenderer surface)
    {
        MaterialPropertyBlock block = new();
        surface.GetPropertyBlock(block);
        var data = (RenderTexture)block.GetTexture("_LiquidFilm");
        var request = AsyncGPUReadback.Request(data, 0);
        request.WaitForCompletion();
        Assert.That(request.hasError, Is.False);
        return request.GetData<byte>().ToArray();
    }

    private void SetChannelStage(ChannelWater2D.Boundary kind, float[] presentation, string inlet = null)
    {
        stage.transform.Find("Fluid lighting backdrop").gameObject.SetActive(false);
        camera.orthographicSize = kind == ChannelWater2D.Boundary.ClosedBasin ? 1.76f : 1.64f;
        float viewHeight = 2f * camera.orthographicSize;
        float centerY = inlet == null ? .13f : 1.10f;
        camera.transform.position = new Vector3(0f, centerY, -10f);
        const int w = 800, h = 450;
        Color32[] texels = new Color32[w * h];
        for (int v = 0; v < h; v++)
        {
            float y = centerY + ((v + .5f) / h - .5f) * viewHeight;
            for (int u = 0; u < w; u++)
            {
                float x = ((u + .5f) / w - .5f) * (viewHeight * Width / Height);
                float grain = Mathf.PerlinNoise(2.1f * x + 14f, 3.8f * y + 8f);
                float far = .79f + .20f * Mathf.Sin(2.15f * x + .7f) + .045f * Mathf.Sin(8f * x);
                float ridge = .33f + .12f * Mathf.Sin(3.3f * x + .9f) + .08f * Mathf.Sin(6.7f * x);
                Color sky = Color.Lerp(new Color(.27f, .43f, .48f), new Color(.10f, .27f, .33f), Mathf.Clamp01(y * .61f));
                Color color = y > far ? sky : y > ridge ? new Color(.15f, .29f, .31f) : new Color(.11f, .22f, .24f);
                float bands = Mathf.Sin(19f * y + 2.3f * x + 1.4f * Mathf.Sin(1.5f * x) + grain * 2f);
                if (y < .18f + .11f * Mathf.Sin(3.2f * x))
                    color = Color.Lerp(new Color(.26f,.255f,.22f), new Color(.395f,.356f,.276f),
                                       .17f + .47f * grain + .22f * Mathf.SmoothStep(0, 1, (bands - .55f) * 3f));
                float cell = Mathf.Clamp((x / 6f + .5f) * 128f - .5f, 0, 127);
                int i = Mathf.FloorToInt(cell);
                float bed = presentation == null ? -.20f : Mathf.Lerp(presentation[(256 + i) * 4 + 1],
                    presentation[(256 + Mathf.Min(127, i + 1)) * 4 + 1], cell - i);
                if (y > -.79f && y < .31f)
                {
                    float seam = Mathf.Abs(Mathf.Sin(2.7f * x + 6.3f * y + .46f * Mathf.Sin(5.1f * x) + .5f * grain));
                    float ochre = Mathf.SmoothStep(0f, 1f, (.62f - seam) * 2.7f);
                    color = Color.Lerp(color, new Color(.48f, .29f, .14f), ochre * .69f);
                }
                if (y < bed)
                {
                    float close = Mathf.Clamp01((bed - y) * 4.1f);
                    float sand = .56f * grain + .11f * Mathf.Sin(41f * (y - bed) + 3.7f * x + grain)
                               + .07f * Mathf.Sin(16f * x + grain * 4f);
                    color = Color.Lerp(new Color(.28f, .265f, .208f), new Color(.52f, .46f, .34f),
                                       Mathf.Clamp01(sand + .47f * (1f - close)));
                    if (bed - y < .014f) color *= 1.14f;
                }
                if (inlet != null && y < .040f && y >= bed)
                    color = Color.Lerp(new Color(.10f, .245f, .267f), color, .34f + .12f * grain);
                if (inlet == "LedgeDrip" && y >= 1.67f + .57f * Mathf.Abs(x + .58f) + .023f * Mathf.Sin(13f * (x + .58f)) && x < .45f)
                    color = Color.Lerp(new Color(.14f,.19f,.19f), new Color(.31f,.32f,.25f), .24f + .28f * grain + .19f * bands);
                if (inlet == "SpringFountain" && Mathf.Abs(x + .94f) < .13f && y <= .09f && y >= bed)
                    color = new Color(.32f + .05f * grain, .31f, .23f);
                if (kind == ChannelWater2D.Boundary.ClosedBasin && Mathf.Abs(x) > 2.94f && y < .47f)
                    color = new Color(.25f, .29f, .28f);
                texels[v * w + u] = color;
            }
        }
        Texture2D tex = new(w, h, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels32(texels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0,0,w,h), Vector2.one * .5f, h / viewHeight);
        SpriteRenderer plate = new GameObject("Channel side elevation", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        plate.transform.SetParent(stage.transform, false);
        plate.transform.position = new Vector3(0, centerY, 1f);
        plate.sprite = sprite;
        plate.sharedMaterial = spriteMaterial;
        plate.sortingOrder = -200;
        applicationResources.Add(sprite);
        applicationResources.Add(tex);
    }
}
