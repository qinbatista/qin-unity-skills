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
    private const int EarlyStopFrame = 72;
    private const int FallFrames = 292;
    private const float NeedleSpeed = 8.45f;
    private static readonly int ReflectionTint = Shader.PropertyToID("_ReflectionTint");
    private static readonly int DepthTint = Shader.PropertyToID("_DepthTint");
    private static readonly int WaterOptics = Shader.PropertyToID("_WaterOptics");

    private readonly List<Object> applicationResources = new();

    // Receiving games supply these optional partial methods from their own test assembly.
    partial void SelectApplicationFloor(ref float y);
    partial void BindReceiverStage(bool waterfall, ref bool handled);
    partial void BindReceiverWater(FluidMixController controller);

    private float ApplicationFloor
    {
        get
        {
            float y = SurfaceY;
            SelectApplicationFloor(ref y);
            return y;
        }
    }

    [UnityTest]
    public IEnumerator ApplyWaterCurtain()
    {
        string root = ApplicationCapture("Curtain");
        SetApplicationCamera();
        DrawApplicationStage(false);
        Transform anchor = new GameObject("Contact anchor").transform;
        anchor.SetParent(stage.transform, false);
        anchor.position = new Vector3(.92f, ApplicationFloor + 1.02f, 0f);
        var collider = anchor.gameObject.AddComponent<CircleCollider2D>();
        collider.radius = .32f;
        Physics2D.SyncTransforms();

        var gather = OpenVariant("Vortex", new Vector3(-2.69f, ApplicationFloor + .948f, 0f));
        gather.transform.localScale = Vector3.one * .245f;
        gather.transform.rotation = Quaternion.Euler(0f, 0f, 22f);
        gather.SetRecipe(gather.Recipe);
        var needle = OpenVariant("WaterNeedle2D", new Vector3(0, ApplicationFloor, 0));
        var curtain = OpenVariant("WaterCurtain2D", new Vector3(0, ApplicationFloor, 0));
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
                SerializedObject data = new(missRecipe);
                data.FindProperty("_responses").intValue = (int)SOFluidMixRecipe.Response.Spray;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var originalRecipe = needle.Recipe;
            if (missRecipe) needle.SetRecipe(missRecipe);

            int hitFrame = -1;
            float targetX = 0f;
            float previousX = needle.Recipe.Origin.x;
            float rayHeight = ApplicationFloor + needle.Recipe.Origin.y;
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
                            curtain.transform.position = new Vector3(targetX, ApplicationFloor, 0f);
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
            Assert.That(Capture(), Is.EqualTo(dry), "All water contributions must return to the dry frame");
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
        var fall = OpenVariant("ContinuousWaterfall", new Vector3(0, ApplicationFloor, 0));
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
        SerializedObject settings = new(stillPool);
        settings.FindProperty("_responses").intValue &= ~(int)SOFluidMixRecipe.Response.SurfaceWaves;
        settings.ApplyModifiedPropertiesWithoutUndo();
        int[] samples = { 12, 23, 32, 66, 81, 96, 132, 162, 190, 221, 231, 253, 279 };
        foreach (var mode in new[] { "normal", "no-waves", "variation", "early-stop" })
        {
            string folder = Path.Combine(root, mode);
            Directory.CreateDirectory(folder);
            int stopFrame = mode == "early-stop" ? EarlyStopFrame : FallStopFrame;
            fall.SetRecipe(mode == "no-waves" ? stillPool : original);
            fall.BeginCast(mode == "variation" ? Seed ^ 0x9e3779b9u : Seed);
            for (int frame = 0; frame < FallFrames; frame++)
            {
                float time = frame / (float)Fps;
                fall.SetCastTime(time);
                if (frame == stopFrame) fall.EndCast();
                yield return null;
                fall.SetCastTime(time);

                bool probe = System.Array.IndexOf(samples, frame) >= 0;
                if (mode == "normal" || mode == "early-stop" || probe) SaveFrame(folder, frame);
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
                if (frame >= 66 && frame < stopFrame)
                    Assert.That(fall.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Releasing), "Continuous inlet retired while still open");
                if (frame > stopFrame && frame < stopFrame + Fps)
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
        camera.transform.position = new Vector3(0f, ApplicationFloor + 1.24f, -10f);
    }

    private FluidMixController OpenVariant(string name, Vector3 position, string path = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path ?? $"{Imported}/Variants/{name}.prefab");
        Assert.That(asset, Is.Not.Null, name);
        Assert.That(PrefabUtility.GetPrefabAssetType(asset), Is.EqualTo(PrefabAssetType.Variant));
        var instance = Object.Instantiate(asset, stage.transform, false);
        instance.transform.position = position;
        var controller = instance.GetComponent<FluidMixController>();
        Assert.That(controller.Recipe.name, Is.EqualTo(name));
        Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        controller.SetWaterSortingLayer(WaterLayer);
        BindReceiverWater(controller);
        foreach (var draw in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (draw.sharedMaterial.shader.name == "Qin/Water/Side Rain")
            {
                Assert.That(controller.Recipe.RainExposure, Is.GreaterThan(0f));
                continue;
            }
            Assert.That(AssetDatabase.GetAssetPath(draw.sharedMaterial.shader), Does.EndWith(".shadergraph"));
            Material material = new(draw.sharedMaterial);
            material.SetColor(ReflectionTint, new Color(.65f, .94f, .98f, 1f));
            material.SetColor(DepthTint, new Color(.030f, .226f, .356f, 1f));
            material.SetVector(WaterOptics, new Vector4(.74f, .83f, 0f, 0f));
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
        bool handled = false;
        BindReceiverStage(waterfall, ref handled);
        if (handled) return;
        const int width = 768;
        const int height = 432;
        float viewHeight = 2f * camera.orthographicSize;
        float viewWidth = viewHeight * Width / Height;
        Color32[] pixels = new Color32[width * height];
        for (int py = 0; py < height; py++)
        {
            float y = 1.24f + ((py + .5f) / height - .5f) * viewHeight;
            for (int px = 0; px < width; px++)
            {
                float x = ((px + .5f) / width - .5f) * viewWidth;
                pixels[py * width + px] = waterfall ? PaintGrotto(x, y) : PaintCourtyard(x, y);
            }
        }
        Texture2D texture = new(width, height, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, height / viewHeight);
        SpriteRenderer plate = new GameObject("Application plate", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
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
        float grain = Mathf.PerlinNoise(x * 2.85f + 21f, y * 3.9f + 39f);
        float nearCliff = .53f + .16f * Mathf.Sin(3.5f * x) + .09f * Mathf.Sin(7.1f * x + 1f);
        float remoteCliff = 1.44f + .16f * Mathf.Sin(2.21f * x + .9f) + .10f * Mathf.Sin(4.7f * x);
        Color color = Color.Lerp(new Color(.071f, .162f, .224f), new Color(.133f, .304f, .359f), grain * .52f);
        if (y < remoteCliff)
            color = Color.Lerp(new Color(.066f, .150f, .182f), new Color(.11f, .24f, .253f), .24f + .42f * grain);
        if (y < nearCliff)
            color = Color.Lerp(new Color(.045f, .115f, .148f), new Color(.083f, .180f, .192f), .21f + .39f * grain);

        float seamX = x + .39f - .16f * Mathf.Sin(5.2f * y + .8f);
        float paintedStone = (1f - Mathf.SmoothStep(0f, 1f, (Mathf.Abs(seamX) - .085f) / .23f))
            * Mathf.SmoothStep(0f, 1f, (y - .88f) / .46f);
        color = Color.Lerp(color, new Color(.419f, .263f, .147f), paintedStone * (.49f + .25f * grain));

        // A faceted cutaway shows the shelf and the pool on the same vertical plane.
        float ledgeTop = 2.57f + .047f * Mathf.Sin(7.1f * x) + .025f * Mathf.Sin(21.1f * x);
        float leftWall = Mathf.Abs(x + 3.07f + .22f * Mathf.Sin(y * 4f)) - (.74f + .12f * Mathf.Sin(y));
        if ((y > ledgeTop && x < .32f + .17f * Mathf.Sin(5f * y)) || leftWall < 0f)
        {
            float strata = Mathf.Repeat(y + .22f * x + .035f * Mathf.Sin(x * 3.6f), .28f);
            Color shade = Color.Lerp(new Color(.105f, .144f, .180f), new Color(.196f, .255f, .255f),
                .23f + .56f * grain);
            float face = Mathf.SmoothStep(0f, 1f, (strata - .055f) * 48f);
            color = shade * (.82f + .21f * face);
            if (y > ledgeTop + .015f && y < ledgeTop + .074f && leftWall >= 0f)
                color = Color.Lerp(color, new Color(.38f, .50f, .31f), .53f);
        }

        const float line = .040f;
        if (y < line)
        {
            float broken = Mathf.SmoothStep(0f, 1f, (Mathf.PerlinNoise(x * 9f + 6f, y * 19f + 15f) - .56f) * 7f);
            float bands = Mathf.SmoothStep(0f, 1f, (Mathf.Sin(39f * y + .40f * Mathf.Sin(6.6f * x)) - .72f) * 5f)
                * broken;
            float fade = Mathf.SmoothStep(0f, 1f, (y - line + .71f) * 1.34f);
            color = Color.Lerp(new Color(.028f, .089f, .156f), new Color(.057f, .252f, .319f),
                .56f * fade + .21f * bands);
            if (y > line - .013f)
                color += new Color(.049f, .141f, .141f);
        }
        color = PaintRock(color, x, y, -2.86f, -.16f, 1.12f, .44f, grain);
        color = PaintRock(color, x, y, 2.85f, -.24f, 1.05f, .49f, grain);
        color = PaintRock(color, x, y, -.26f, .24f, .43f, .59f, grain * .84f);
        return PaintRock(color, x, y, -.26f, .79f, .52f, .52f, grain);
    }

    private static Color PaintRock(Color background, float x, float y, float cx, float cy, float rx, float ry, float grain)
    {
        float nx = (x - cx) / rx;
        float ny = (y - cy) / ry;
        float coverage = 1f - Mathf.SmoothStep(0f, 1f, (nx * nx + ny * ny - .94f) / .08f);
        return Color.Lerp(background, SideStoneColor(nx, ny, grain), coverage);
    }

    private static Color SideStoneColor(float x, float y, float grain)
    {
        float facet = y - .27f * x - .11f * Mathf.Abs(1.7f * x - .25f);
        Color baseColor = Color.Lerp(new Color(.073f, .104f, .160f), new Color(.21f, .267f, .286f), .43f * grain);
        Color plane = Color.Lerp(new Color(.156f, .237f, .272f), new Color(.33f, .392f, .365f), .64f * grain);
        Color top = Color.Lerp(new Color(.221f, .337f, .324f), new Color(.44f, .52f, .37f), .68f * grain);
        Color color = Color.Lerp(baseColor, plane, Mathf.SmoothStep(0f, 1f, (facet + .17f) * 23f));
        color = Color.Lerp(color, top, Mathf.SmoothStep(0f, 1f, (facet - .21f) * 23f));
        float seam = Mathf.Abs(y + .42f * x - .24f);
        if (x > -.33f && seam < .021f) color *= .71f;
        return color;
    }

}
