using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class QinFluidReplay
{
    private const int ReleaseFrame = 10;
    private const int CurtainFrames = 92;
    private const int FallStopFrame = 222;
    private const int FallFrames = 292;
    private const float NeedleSpeed = 8.45f;
    private static readonly int ReflectionTint = Shader.PropertyToID("_ReflectionTint");
    private static readonly int DepthTint = Shader.PropertyToID("_DepthTint");
    private static readonly int WaterOptics = Shader.PropertyToID("_WaterOptics");

    private readonly List<Object> applicationResources = new();

    [UnityTest]
    public IEnumerator ApplyWaterCurtain()
    {
        string root = ApplicationCapture("Curtain");
        SetApplicationCamera();
        DrawApplicationStage(false);
        var anchor = new GameObject("Contact anchor").transform;
        anchor.SetParent(stage.transform, false);
        anchor.position = new Vector3(.92f, SurfaceY + 1.02f, 0f);
        var collider = anchor.gameObject.AddComponent<CircleCollider2D>();
        collider.radius = .32f;
        Physics2D.SyncTransforms();

        var gather = OpenVariant("Vortex", new Vector3(-2.69f, SurfaceY + .948f, 0f));
        gather.transform.localScale = Vector3.one * .245f;
        gather.transform.rotation = Quaternion.Euler(0f, 0f, 22f);
        gather.SetRecipe(gather.Recipe);
        var needle = OpenVariant("WaterNeedle2D", new Vector3(0, SurfaceY, 0));
        var curtain = OpenVariant("WaterCurtain2D", new Vector3(0, SurfaceY, 0));
        needle.SetObstacleAnchor(anchor);
        curtain.SetObstacleAnchor(anchor);
        // Clear colour history before the first capture.
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);

        foreach (var mode in new[] { "normal", "miss", "variation" })
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            bool misses = mode == "miss";
            collider.enabled = !misses;
            needle.SetObstacleAnchor(misses ? null : anchor);
            SOFluidMixRecipe missRecipe = null;
            if (misses)
            {
                missRecipe = Object.Instantiate(needle.Recipe);
                var data = new SerializedObject(missRecipe);
                data.FindProperty("_responses").intValue = (int)SOFluidMixRecipe.Response.Spray;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var originalRecipe = needle.Recipe;
            if (missRecipe) needle.SetRecipe(missRecipe);

            int hitFrame = -1;
            float targetX = 0f;
            float previousX = needle.Recipe.Origin.x;
            float rayHeight = SurfaceY + needle.Recipe.Origin.y;
            uint seed = mode == "variation" ? Seed ^ 0x9e3779b9u : Seed;
            int probe = 0;
            int[] samples = { 3, 10, 14, 20, 26, 35, 45 };
            for (int frame = 0; frame < CurtainFrames; frame++)
            {
                if (frame == 1) gather.BeginCast(seed ^ 41u);
                if (frame > 0) gather.SetCastTime((frame - 1) / (float)Fps);
                if (frame == ReleaseFrame)
                {
                    gather.EndCast();
                    needle.BeginCast(seed);
                }
                if (frame >= ReleaseFrame)
                {
                    float age = (frame - ReleaseFrame) / (float)Fps;
                    needle.SetCastTime(age);
                    float frontX = needle.Recipe.Origin.x + NeedleSpeed * Mathf.Max(0f, age - .087f);
                    if (hitFrame < 0 && frontX > previousX)
                    {
                        var hit = Physics2D.CircleCast(new Vector2(previousX, rayHeight), .055f,
                            Vector2.right, frontX - previousX);
                        if (hit.collider == collider)
                        {
                            hitFrame = frame;
                            needle.EndCast();
                            targetX = collider.bounds.center.x;
                            curtain.transform.position = new Vector3(targetX, SurfaceY, 0f);
                            curtain.BeginCast(seed ^ 63u);
                        }
                        previousX = frontX;
                    }
                }
                if (hitFrame >= 0) curtain.SetCastTime((frame - hitFrame) / (float)Fps);
                // Keep the original cast and every replay on the same rendered player-frame boundary.
                yield return null;
                if (frame > 0) gather.SetCastTime((frame - 1) / (float)Fps);
                if (frame >= ReleaseFrame) needle.SetCastTime((frame - ReleaseFrame) / (float)Fps);
                if (hitFrame >= 0) curtain.SetCastTime((frame - hitFrame) / (float)Fps);
                if (mode != "variation" || (frame >= 27 && frame <= 55))
                    SaveFrame(folder, frame);
                if (hitFrame >= 0 && probe < samples.Length && frame == hitFrame + samples[probe])
                {
                    File.WriteAllBytes(Path.Combine(folder, $"state-{samples[probe]:D2}.bin"), State(curtain));
                    probe++;
                }
                if (mode == "normal" && hitFrame >= 0 && frame == hitFrame + 14)
                    SaveOptics(root);
            }
            Assert.That(hitFrame >= 0, Is.EqualTo(!misses), "The curtain must start only on a real 2D hit");
            Assert.That(gather.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
            Assert.That(needle.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
            Assert.That(curtain.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
            Assert.That(Capture(), Is.EqualTo(dry), "Both water contributions must return to the dry frame");
            File.WriteAllLines(Path.Combine(folder, "contact.txt"), new[] { hitFrame.ToString(), targetX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) });
            if (missRecipe)
            {
                needle.SetRecipe(originalRecipe);
                Object.Destroy(missRecipe);
            }
        }
    }

    [UnityTest]
    public IEnumerator ApplyContinuousWaterfall()
    {
        string root = ApplicationCapture("Waterfall2D");
        SetApplicationCamera();
        var fall = OpenVariant("ContinuousWaterfall", new Vector3(0, SurfaceY, 0));
        SOFluidMixRecipe original = fall.Recipe;
        Assert.That(original.ContinuousFeed && original.PlayOnEnable, Is.True);
        for (int frame = 0; frame < 12; frame++) yield return null;
        Assert.That(fall.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Releasing),
            "A saved continuous variant must run on enable with the scene clock");
        fall.SetRecipe(original);
        DrawApplicationStage(true);
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(root, "dry.png"), dry);

        var stillPool = Object.Instantiate(original);
        applicationResources.Add(stillPool);
        var settings = new SerializedObject(stillPool);
        settings.FindProperty("_responses").intValue &= ~(int)SOFluidMixRecipe.Response.SurfaceWaves;
        settings.ApplyModifiedPropertiesWithoutUndo();
        int[] samples = { 12, 23, 32, 66, 81, 96, 132, 162, 190, 221, 231, 253, 279 };
        foreach (var mode in new[] { "normal", "no-waves", "variation" })
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            fall.SetRecipe(mode == "no-waves" ? stillPool : original);
            fall.BeginCast(mode == "variation" ? Seed ^ 0x9e3779b9u : Seed);
            for (int frame = 0; frame < FallFrames; frame++)
            {
                float time = frame / (float)Fps;
                fall.SetCastTime(time);
                if (frame == FallStopFrame) fall.EndCast();
                yield return null;
                fall.SetCastTime(time);

                bool probe = System.Array.IndexOf(samples, frame) >= 0;
                if (mode == "normal" || probe) SaveFrame(folder, frame);
                if (probe)
                {
                    byte[] settled = State(fall);
                    if (frame == 162)
                    {
                        fall.SetCastTime(time);
                        Assert.That(State(fall), Is.EqualTo(settled), "Held time advanced the waterfall");
                    }
                    File.WriteAllBytes(Path.Combine(folder, $"state-{frame:D2}.bin"), settled);
                }
                if (frame == 162 && mode == "normal") SaveOptics(root);
                if (frame >= 66 && frame < FallStopFrame)
                    Assert.That(fall.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Releasing), "Continuous inlet retired while still open");
                if (frame > FallStopFrame && frame < FallStopFrame + Fps)
                    Assert.That(fall.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Retracting));
            }
            Assert.That(fall.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
            Assert.That(Capture(), Is.EqualTo(dry), "Closing the inlet must drain the active body and settle the water surface");
        }
    }

    private static string ApplicationCapture(string name)
    {
        string path = Path.Combine(System.Environment.GetEnvironmentVariable("QIN_FLUID_OUTPUT"), name);
        Directory.CreateDirectory(path);
        File.WriteAllLines(Path.Combine(Path.GetDirectoryName(path), "device.txt"),
            new[] { Application.unityVersion, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceType.ToString() });
        return path;
    }

    private void SetApplicationCamera()
    {
        stage.transform.Find("Fluid lighting backdrop").gameObject.SetActive(false);
        camera.orthographicSize = 2.16f;
        camera.transform.position = new Vector3(0f, SurfaceY + 1.24f, -10f);
    }

    private FluidMixController OpenVariant(string name, Vector3 position)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Imported}/Variants/{name}.prefab");
        Assert.That(asset, Is.Not.Null, name);
        Assert.That(PrefabUtility.GetPrefabAssetType(asset), Is.EqualTo(PrefabAssetType.Variant));
        var instance = Object.Instantiate(asset, stage.transform, false);
        instance.transform.position = position;
        var controller = instance.GetComponent<FluidMixController>();
        Assert.That(controller.Recipe.name, Is.EqualTo(name));
        Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        controller.SetWaterSortingLayer(WaterLayer);
        foreach (var draw in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            Assert.That(AssetDatabase.GetAssetPath(draw.sharedMaterial.shader), Does.EndWith(".shadergraph"));
            var material = new Material(draw.sharedMaterial);
            material.SetColor(ReflectionTint, new Color(.73f, .91f, .94f, 1f));
            material.SetColor(DepthTint, new Color(.023f, .126f, .207f, 1f));
            material.SetVector(WaterOptics, new Vector4(.91f, .73f, 0f, 0f));
            draw.sharedMaterial = material;
            applicationResources.Add(material);
        }
        var spray = instance.GetComponentInChildren<UnityEngine.VFX.VisualEffect>(true);
        Assert.That(AssetDatabase.GetAssetPath(spray.visualEffectAsset), Does.EndWith(".vfx"));
        return controller;
    }

    private void SaveFrame(string folder, int index)
    {
        File.WriteAllBytes(Path.Combine(folder, $"{index:D4}.png"), Capture());
    }

    private void SaveOptics(string folder)
    {
        try
        {
            Shader.SetGlobalFloat(OpticMode, 2f);
            File.WriteAllBytes(Path.Combine(folder, "no-chroma-reversal.png"), Capture());
            Shader.SetGlobalFloat(OpticMode, 1f);
            File.WriteAllBytes(Path.Combine(folder, "no-optics.png"), Capture());
        }
        finally { Shader.SetGlobalFloat(OpticMode, 0f); }
    }

    private void DrawApplicationStage(bool waterfall)
    {
        const int width = 768;
        const int height = 432;
        float viewHeight = 2f * camera.orthographicSize;
        float viewWidth = viewHeight * Width / Height;
        var pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
        {
            float y = 1.24f + ((py + .5f) / height - .5f) * viewHeight;
            for (int px = 0; px < width; px++)
            {
                float x = ((px + .5f) / width - .5f) * viewWidth;
                pixels[py * width + px] = waterfall ? PaintGrotto(x, y) : PaintCourtyard(x, y);
            }
        }
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, height / viewHeight);
        var plate = new GameObject("Application plate", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        plate.transform.SetParent(stage.transform, false);
        plate.transform.position = new Vector3(0f, camera.transform.position.y, 1f);
        plate.sprite = sprite;
        plate.sharedMaterial = spriteMaterial;
        plate.sortingOrder = -200;
        applicationResources.Add(sprite);
        applicationResources.Add(texture);
    }

    private static Color PaintCourtyard(float x, float y)
    {
        float glow = Mathf.Exp(-.25f * x * x - 1.2f * (y - 1.12f) * (y - 1.12f));
        float grain = Mathf.PerlinNoise(x * 2.8f + 15f, y * 5.2f + 18f);
        Color scene = Color.Lerp(new Color(.067f, .116f, .16f), new Color(.22f, .32f, .35f),
            .49f * glow + .20f * grain);
        float arch = Mathf.Abs(Mathf.Sqrt(x * x * .65f + (y + .20f) * (y + .20f)) - 2.01f);
        scene *= 1f - .26f * (1f - Mathf.SmoothStep(0f, 1f, arch / .14f));
        if (y < -.04f)
        {
            float joint = Mathf.Abs(Mathf.Repeat(x + .18f, .55f) - .277f);
            float stone = .37f + .41f * grain - .25f * Mathf.SmoothStep(0f, 1f, (joint - .24f) / .025f);
            scene = Color.Lerp(new Color(.063f, .113f, .132f), new Color(.225f, .282f, .278f), stone);
            if (y > -.076f) scene += new Color(.081f, .084f, .061f);
        }
        float muzzle = new Vector2((x + 2.68f) / .27f, (y - 1.045f) / .23f).magnitude;
        float baseWidth = Mathf.Lerp(.23f, .145f, Mathf.Clamp01(y));
        bool pedestal = Mathf.Abs(x + 2.76f) < baseWidth && y >= -.045f && y < .96f;
        if (pedestal || muzzle < 1)
        {
            scene = Color.Lerp(new Color(.21f, .142f, .107f), new Color(.63f, .51f, .32f), .66f * grain);
            if (muzzle < .62f) scene = new Color(.09f, .20f, .24f) + scene * .23f;
            if (muzzle > .61f && muzzle < .82f) scene += new Color(.08f, .24f, .22f);
        }
        float head = new Vector2((x - .92f) / .158f, (y - 1.255f) / .156f).magnitude;
        float shoulders = new Vector2((x - .92f) / .293f, (y - .91f) / .39f).magnitude;
        float foot = Mathf.Abs(x - .92f);
        bool body = shoulders < 1f || (foot < .183f && y >= -.04f && y < .94f);
        if (head < 1f || body)
        {
            Color shade = Color.Lerp(new Color(.27f, .165f, .10f), new Color(.81f, .66f, .46f),
                .18f + .42f * grain + .23f * Mathf.Clamp01((1.15f - x) * 2f));
            if (y > .70f && y < 1.075f && y + x * .46f > 1.35f)
                shade = Color.Lerp(shade, new Color(.61f, .209f, .126f), .59f);
            scene = shade;
        }
        return scene;
    }

    private static Color PaintGrotto(float x, float y)
    {
        float stone = .64f * Mathf.PerlinNoise(2.3f * x + 8f, 2.7f * y + 13f)
            + .36f * Mathf.PerlinNoise(17f * x + 40f, 12f * y + 10f);
        float xRidge = x + .115f * Mathf.Sin(y * 4.7f + .9f) + .038f * Mathf.Sin(y * 14.1f);
        float crevice = .5f + .5f * Mathf.Sin(xRidge * 21.42f + .9f * Mathf.Sin(xRidge * 5f));
        float open = Mathf.Exp(-.76f * (x + .28f) * (x + .28f) - .29f * (y - 1.65f) * (y - 1.65f));
        Color color = Color.Lerp(new Color(.046f, .088f, .116f), new Color(.33f, .37f, .29f),
            .09f + .34f * stone + .22f * open + .12f * crevice * open);
        float quartz = Mathf.SmoothStep(0f, 1f, (crevice - .89f) * 7f) * .1f;
        color += quartz * new Color(.24f, .18f, .12f);

        // The water opens below a chipped shelf. The lower basin has a dry reference plate.
        float lip = 2.59f + .076f * Mathf.Sin(x * 3.5f) + .042f * Mathf.Sin(x * 14.5f + 1.2f);
        float wall = Mathf.Abs(x + .27f * Mathf.Sin(y * 2.3f)) - (2.11f + .35f * Mathf.Sin(y * 1.7f));
        float surface = Mathf.Max(y - lip, wall);
        float vein = .5f + .5f * Mathf.Sin(14f * y + 1.5f * x + .42f * Mathf.Sin(7f * x));
        Color ledge = Color.Lerp(new Color(.073f, .126f, .153f), new Color(.46f, .421f, .288f),
            .51f * stone + .22f * vein);
        color = Color.Lerp(color, ledge, Mathf.SmoothStep(0f, 1f, (surface + .05f) / .18f));
        if (y >= lip && y < lip + .10f && Mathf.Abs(x) < 2.25f)
            color += new Color(.079f, .087f, .053f) * (1f - (y - lip) * 10f);
        float shore = .057f - .24f * Mathf.Pow(Mathf.Abs(x) * .34f, 2f) + .026f * Mathf.Sin(8.5f * x);
        if (y < shore)
        {
            float grain = Mathf.PerlinNoise(4.1f * x + 24f, 26f * y + 23f);
            float lit = .46f * grain + .31f * stone + .23f * open;
            color = Color.Lerp(new Color(.035f, .125f, .160f), new Color(.14f, .32f, .33f), lit);
            if (y > shore - .021f)
                color += new Color(.048f, .097f, .081f) * stone;
        }
        color = PaintRock(color, x, y, -.37f, .25f, .64f, .62f, stone * .83f);
        color = PaintRock(color, x, y, -.26f, .79f, .52f, .546f, stone);
        color = PaintRock(color, x, y, -2.23f, .12f, .88f, .42f, stone);
        return PaintRock(color, x, y, 2.14f, -.19f, .93f, .31f, stone);
    }

    private static Color PaintRock(Color beneath, float x, float y, float cx, float cy, float rx, float ry, float grain)
    {
        float nx = (x - cx) / rx;
        float ny = (y - cy) / ry;
        float radius = nx * nx + ny * ny;
        if (radius >= 1f) return beneath;
        float nz = Mathf.Sqrt(1f - radius);
        float light = Mathf.Clamp01(.66f * ny - .31f * nx + .53f * nz);
        float layer = .5f + .5f * Mathf.Sin(16.8f * (ny + .30f * nx) + .6f * nz);
        float moss = .28f * Mathf.SmoothStep(0f, 1f, (ny - .10f) * 2.8f) * (.22f + .78f * grain);
        return Color.Lerp(new Color(.090f, .135f, .158f), new Color(.57f, .46f, .30f),
            .54f * light + .11f * layer + .23f * grain) + moss * new Color(.10f, .28f, .057f);
    }
}
