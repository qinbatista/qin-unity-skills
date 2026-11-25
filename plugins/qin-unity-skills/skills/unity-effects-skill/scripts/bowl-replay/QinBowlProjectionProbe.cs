using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using Qin.UnityEffects.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class QinFluidReplay
{
    private static readonly Vector4 BowlRect = new(.56f, .48f, .253f, .265f);
    private const int BowlFrameCount = 75;

    [UnityTest]
    public IEnumerator BowlProjectionProbe()
    {
        string root = ApplicationCapture("BowlProjection");
        stage.transform.Find("Fluid lighting backdrop").gameObject.SetActive(false);
        DrawAssayPan();
        GameObject tankAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Imported + "/Channels/DrivenTank.prefab");
        GameObject tank = Object.Instantiate(tankAsset, stage.transform);
        tank.transform.position = Vector3.right * 100f;
        ChannelWater2D channel = tank.GetComponent<ChannelWater2D>();
        MeshRenderer section = tank.GetComponentInChildren<MeshRenderer>();
        GameObject bowlAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QinBowlProjection/BowlProjection.prefab");
        Assert.That(bowlAsset, Is.Not.Null);
        GameObject bowl = Object.Instantiate(bowlAsset, stage.transform);
        BowlProjection2D lens = bowl.GetComponentInChildren<BowlProjection2D>();
        Assert.That(lens, Is.Not.Null);
        Renderer surface = lens.GetComponent<Renderer>();
        Assert.That(surface.sharedMaterial.shader.name, Is.EqualTo("Qin/Water/BowlProjection"));
        Assert.That(surface.sharedMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(surface.sharedMaterial.shader));
        surface.sortingLayerName = WaterLayer;
        Vector3 center = camera.ViewportToWorldPoint(new Vector3(BowlRect.x, BowlRect.y, -camera.transform.position.z));
        float height = camera.orthographicSize * 2f;
        bowl.transform.position = center;
        bowl.transform.localScale = new Vector3(2f * BowlRect.z * height * Width / Height, 2f * BowlRect.w * height, 1f);
        lens.Clear();
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);
        Texture2D dryTexture = new(2, 2, TextureFormat.RGB24, false);
        Assert.That(dryTexture.LoadImage(dry), Is.True);
        dryTexture.wrapMode = TextureWrapMode.Clamp;
        applicationResources.Add(dryTexture);
        Texture2D exteriorChallenge = ReplaceExterior(dryTexture);
        using (StreamWriter settings = new(Path.Combine(root, "fixture.csv")))
        {
            settings.WriteLine("width,height,cx,cy,rx,ry");
            settings.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:R},{3:R},{4:R},{5:R}",
                Width, Height, BowlRect.x, BowlRect.y, BowlRect.z, BowlRect.w));
        }

        foreach (string mode in new[] { "driven", "opposite", "zero" })
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            channel.BeginFlow(Seed);
            channel.SetTankAcceleration(0f);
            using StreamWriter inputs = new(Path.Combine(folder, "state.csv"));
            inputs.WriteLine("frame,acceleration,leftHeight,rightHeight,filmWeight");
            MaterialPropertyBlock properties = new();
            for (int frame = 0; frame < BowlFrameCount; frame++)
            {
                float acceleration = frame < 12 ? 0f : frame < 42 ? 2.8f : -2.8f;
                if (mode == "zero") acceleration = 0f;
                if (mode == "opposite") acceleration = -acceleration;
                channel.SetTankAcceleration(acceleration);
                double seconds = frame / (double)Fps;
                channel.SetTime(seconds);
                yield return null;
                channel.SetTime(seconds);
                section.GetPropertyBlock(properties);
                Assert.That(lens.Bind(section, dryTexture, BowlRect), Is.EqualTo(properties.GetFloat("_FilmWeight") > 0f));
                bool sample = frame % 3 == 0;
                if (mode != "opposite" || sample) SaveFrame(folder, frame);
                if (sample)
                {
                    byte[] bytes = ChannelState(section);
                    float[] field = new float[128 * 4 * 4];
                    System.Buffer.BlockCopy(bytes, 0, field, 0, bytes.Length);
                    float left = 0f, right = 0f;
                    for (int cell = 4; cell < 12; cell++)
                    {
                        left += field[(256 + cell) * 4] / 8f;
                        right += field[(256 + 127 - cell) * 4] / 8f;
                    }
                    section.GetPropertyBlock(properties);
                    inputs.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:R},{2:R},{3:R},{4:R}",
                        frame, acceleration, left, right, properties.GetFloat("_FilmWeight")));
                    if (frame == 9 || frame == 30 || frame == 48 || frame == 72)
                        File.WriteAllBytes(Path.Combine(folder, $"field-{frame:D4}.bin"), bytes);
                }
                if (mode == "driven" && frame == 30)
                {
                    Assert.That(lens.Bind(section, exteriorChallenge, BowlRect), Is.True);
                    Assert.That(Capture(), Is.EqualTo(File.ReadAllBytes(Path.Combine(folder, $"{frame:D4}.png"))),
                        "Refraction inside the wet area sampled color from outside the bowl");
                    Assert.That(lens.Bind(section, null, BowlRect), Is.True);
                    File.WriteAllBytes(Path.Combine(root, "renderer-backdrop.png"), Capture());
                    RecordSortingTexture(root);
                }
            }
            lens.Clear();
            Assert.That(Capture(), Is.EqualTo(dry), "Clearing the bowl must restore every camera pixel");
        }
    }

    private Texture2D ReplaceExterior(Texture2D dry)
    {
        Color32[] colors = dry.GetPixels32();
        int replaced = 0;
        for (int y = 0; y < dry.height; y++)
        for (int x = 0; x < dry.width; x++)
        {
            Vector2 local = new(((x + .5f) / dry.width - BowlRect.x) / BowlRect.z,
                ((y + .5f) / dry.height - BowlRect.y) / BowlRect.w);
            if (local.sqrMagnitude <= 1f) continue;
            colors[y * dry.width + x] = new Color32(255, 0, 255, 255);
            replaced++;
        }
        Assert.That(replaced, Is.GreaterThan(dry.width * dry.height / 2));
        Texture2D copy = new(dry.width, dry.height, TextureFormat.RGB24, false);
        copy.SetPixels32(colors);
        copy.Apply(false, true);
        copy.wrapMode = TextureWrapMode.Clamp;
        applicationResources.Add(copy);
        return copy;
    }

    private static void RecordSortingTexture(string root)
    {
        Texture source = Shader.GetGlobalTexture("_CameraSortingLayerTexture");
        Vector4 texelSize = Shader.GetGlobalVector("_CameraSortingLayerTexture_TexelSize");
        Vector4 scaleBias = Shader.GetGlobalVector("_ScaleBiasRt");
        string dimensions = source ? $"{source.width}x{source.height}" : "not retained after Camera.Render";
        File.WriteAllText(Path.Combine(root, "renderer-diagnostic.txt"),
            $"Global texture after draw: {dimensions}\nTexel size: {texelSize}\nScale/bias: {scaleBias}\n");
        if (!(source is RenderTexture sorting) || !sorting.IsCreated()) return;
        RenderTexture previous = RenderTexture.active;
        Texture2D copy = new(sorting.width, sorting.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = sorting;
            copy.ReadPixels(new Rect(0, 0, sorting.width, sorting.height), 0, 0);
            copy.Apply(false, false);
            File.WriteAllBytes(Path.Combine(root, "sorting-texture.png"), copy.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(copy);
        }
    }

    private void DrawAssayPan()
    {
        const int w = 640, h = 360;
        Texture2D tex = new(w, h, TextureFormat.RGB24, false);
        Color32[] swatches = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2 uv = new((x + .5f) / w, (y + .5f) / h);
            Vector2 p = new((uv.x - BowlRect.x) / BowlRect.z, (uv.y - BowlRect.y) / BowlRect.w);
            float r = p.magnitude;
            float grain = Mathf.PerlinNoise(73f * uv.x + 1.4f, 87f * uv.y + .9f);
            float timber = .08f * Mathf.Sin(uv.y * 65f + .5f * Mathf.Sin(uv.x * 16f) + grain * 3f);
            Color color = Color.Lerp(new Color(.15f, .115f, .097f), new Color(.40f, .27f, .16f), grain + timber);
            if (r < 1.095f)
            {
                float light = .40f + .28f * p.y - .21f * p.x;
                float rim = .64f * Mathf.Exp(-Mathf.Pow((r - 1.032f) / .019f, 2f));
                float scratches = .06f * Mathf.Sin(92f * r + 5f * Mathf.Atan2(p.y, p.x) + grain * 2f);
                color = Color.Lerp(new Color(.11f, .16f, .18f), new Color(.59f, .58f, .48f),
                    Mathf.Clamp01(light + rim + scratches + (grain - .5f) * .25f));
            }
            color = AssayStone(p, new Vector2(-.36f, .10f), new Vector2(.13f, .095f), new Color(.95f,.62f,.10f), color);
            color = AssayStone(p, new Vector2(.33f, -.17f), new Vector2(.11f, .13f), new Color(.14f,.59f,.67f), color);
            color = AssayStone(p, new Vector2(.10f, .37f), new Vector2(.071f, .051f), new Color(.77f,.45f,.17f), color);
            swatches[y * w + x] = color;
        }
        tex.SetPixels32(swatches);
        tex.Apply(false, true);
        Sprite art = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * .5f, h / (2f * camera.orthographicSize));
        SpriteRenderer renderer = new GameObject("Reference assay pan", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        renderer.transform.SetParent(stage.transform, false);
        renderer.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 1f);
        renderer.sprite = art;
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = -300;
        applicationResources.Add(art);
        applicationResources.Add(tex);
    }

    private static Color AssayStone(Vector2 p, Vector2 center, Vector2 extent, Color stone, Color background)
    {
        Vector2 q = new((p.x - center.x) / extent.x, (p.y - center.y) / extent.y);
        float boundary = 1f + .14f * Mathf.Sin(5f * Mathf.Atan2(q.y, q.x));
        if (q.magnitude >= boundary) return background;
        float face = .72f + .20f * q.y - .15f * q.x + .12f * Mathf.Sign(q.x + q.y * .52f);
        return stone * Mathf.Clamp(face, .43f, 1.12f);
    }
}
