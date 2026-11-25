using System;
using System.Globalization;
using System.IO;
using System.Text;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

internal static class NativeRainStudy
{
    const int Width = 960, Height = 540;
    const int Seed = 29411;
    static readonly int Field = Shader.PropertyToID("_QinTopDownState");
    static readonly int Air = Shader.PropertyToID("_RainDrops");
    static readonly int Contact = Shader.PropertyToID("_RainHits");
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly Vector4[] AirBuffer = new Vector4[RainDropTimeline.Capacity];
    static readonly Vector4[] ContactBuffer = new Vector4[RainDropTimeline.Capacity];

    public static void Render()
    {
        string output = Environment.GetEnvironmentVariable("QIN_RAIN_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("Missing QIN_RAIN_OUTPUT");
        var previous = RenderTexture.active;
        bool previousWrite = GL.sRGBWrite;
        TopDownWater2D water = null, still = null;
        RenderTexture screen = null;
        Texture2D pixels = null;
        Material optics = null;
        try
        {
            Directory.CreateDirectory(Path.Combine(output, "normal"));
            Directory.CreateDirectory(Path.Combine(output, "control"));
            Directory.CreateDirectory(Path.Combine(output, "coarseOnly"));
            var solver = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Pond/TopDownWater.compute");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/RainLook/RainStudy.shader");
            if (!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The rain presentation shader failed on this renderer.");
            optics = new Material(shader);
            Texture2D plate = ReadTexture("Assets/RainLook/StillPool.jpg", true);
            Texture2D mask = ReadTexture("Assets/RainLook/ShoreMask.png", false);
            optics.SetTexture("_DryPlate", plate);
            optics.SetTexture("_RainMask", mask);
            optics.SetFloat("_UseDryPlate", 1);
            water = CreateWater(solver);
            still = CreateWater(solver);
            screen = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            screen.Create();
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RunClock(output, water, still, optics, screen, pixels, mask);
            File.WriteAllText(Path.Combine(output, "device.txt"), string.Join("\n",
                Application.unityVersion, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceType,
                QualitySettings.activeColorSpace, Width + "x" + Height));
            Debug.Log("QIN_NATIVE_RAIN_CAPTURED");
        }
        finally
        {
            RenderTexture.active = previous;
            GL.sRGBWrite = previousWrite;
            if (optics) Object.DestroyImmediate(optics);
            if (pixels) Object.DestroyImmediate(pixels);
            if (screen) { screen.Release(); Object.DestroyImmediate(screen); }
            ReleaseWater(water);
            ReleaseWater(still);
        }
    }

    static Texture2D ReadTexture(string path, bool colour)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new InvalidOperationException("Missing rain artwork at " + path);
        if (importer.sRGBTexture != colour || importer.isReadable == colour || importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear ||
            importer.npotScale != TextureImporterNPOTScale.None)
            throw new InvalidOperationException("Unexpected texture import settings at " + path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static TopDownWater2D CreateWater(ComputeShader solver)
    {
        if (!solver || !SystemInfo.supportsComputeShaders) throw new InvalidOperationException("A working compute backend is required.");
        GameObject instance = new("RainPond replay");
        instance.SetActive(false);
        var water = instance.AddComponent<TopDownWater2D>();
        water.runInEditMode = true;
        SerializedObject properties = new(water);
        properties.FindProperty("_solver").objectReferenceValue = solver;
        properties.FindProperty("_profile").enumValueIndex = (int)TopDownWater2D.Basin.RainPond;
        properties.FindProperty("_waveSpeed").floatValue = 1.60f;
        properties.FindProperty("_drag").floatValue = .62f;
        properties.ApplyModifiedPropertiesWithoutUndo();
        water.Automatic = false;
        water.InputEnabled = false;
        instance.SetActive(true);
        // runInEditMode requests normal enable/disable callbacks in this
        // disposable preview receiver; the timeline owns the simulation clock.
        if (!water.Field) throw new InvalidOperationException("The native rain pond failed to allocate its field.");
        return water;
    }

    static void ReleaseWater(TopDownWater2D water)
    {
        if (!water) return;
        water.gameObject.SetActive(false);
        Object.DestroyImmediate(water.gameObject);
    }

    static void RunClock(string output, TopDownWater2D water, TopDownWater2D still, Material optics, RenderTexture screen, Texture2D pixels, Texture2D mask)
    {
        RainDropTimeline timeline = new(Seed, mask);
        WriteContacts(output, timeline);
        using var statistics = new StreamWriter(Path.Combine(output, "field.csv"), false, Encoding.UTF8);
        statistics.WriteLine("frame,time,landed,airborne,recent,normalMaxM,sumHeightSquaredM2,firstDropCenterM,firstDropEnergyRadiusM,controlMaxM");
        int tick = 0, next = 0;
        for (int frame = 0; frame < RainDropTimeline.Frames; frame++)
        {
            int endTick = frame * (RainDropTimeline.Hz / RainDropTimeline.Fps);
            while (tick < endTick)
            {
                tick++;
                while (next < timeline.Drops.Count && timeline.Drops[next].Land == tick)
                    Strike(water, timeline.Drops[next++]);
                water.Advance(TopDownWater2D.StepSeconds);
                still.Advance(TopDownWater2D.StepSeconds);
            }
            timeline.Appearance(tick, AirBuffer, ContactBuffer, out int aerial, out int fresh);
            optics.SetVectorArray(Air, AirBuffer);
            optics.SetVectorArray(Contact, ContactBuffer);
            optics.SetInt("_RainDropCount", aerial);
            optics.SetFloat("_RainTime", tick / (float)RainDropTimeline.Hz);
            Capture(output, "normal", frame, water, fresh, optics, screen, pixels);
            Capture(output, "control", frame, still, 0, optics, screen, pixels);
            // Between the first and second contacts, compare the same native field
            // with and without the sub-grid optical response. The crown has retired.
            if (frame == 36 || frame == 38 || frame == 42 || frame == 45 || frame == 49 || frame == RainDropTimeline.Frames - 1)
                Capture(output, "coarseOnly", frame, water, 0, optics, screen, pixels);
            Sample(statistics, frame, tick, next, aerial, fresh, timeline.Drops[0].World, water.Field, still.Field);
        }
        if (next != timeline.Drops.Count) throw new InvalidOperationException("Not all airborne drops reached the pond.");
    }

    static void Strike(TopDownWater2D water, RainDropTimeline.Drop drop)
    {
        // A narrow cavity displaces water into a broad collar at the same
        // impact. Doubling the Gaussian radius quadruples its full-plane area.
        water.Impulse(drop.World, drop.Amplitude * .25f, drop.Radius * 2f);
        water.Impulse(drop.World, -drop.Amplitude, drop.Radius);
    }

    static void Capture(string output, string mode, int frame, TopDownWater2D body, int fresh, Material optics, RenderTexture target, Texture2D pixels)
    {
        optics.SetInt("_RainHitCount", fresh);
        optics.SetTexture(Field, body.Field);
        GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
        Graphics.Blit(null, target, optics, 0);
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
        pixels.Apply(false, false);
        File.WriteAllBytes(Path.Combine(output, mode, frame.ToString("D4") + ".png"), pixels.EncodeToPNG());
    }

    static void Sample(StreamWriter writer, int frame, int tick, int landed, int air, int hits,
                       Vector2 first, RenderTexture normal, RenderTexture control)
    {
        var response = AsyncGPUReadback.Request(normal);
        var quiet = AsyncGPUReadback.Request(control);
        response.WaitForCompletion();
        quiet.WaitForCompletion();
        if (response.hasError || quiet.hasError) throw new InvalidOperationException("Native rain field readback failed.");
        var state = response.GetData<Vector4>();
        var zero = quiet.GetData<Vector4>();
        float total = 0, weighted = 0, peak = 0, disabled = 0;
        int columns = TopDownWater2D.Columns, rows = TopDownWater2D.Rows;
        for (int i = 0; i < state.Length; i++)
        {
            float h = state[i].x;
            float idle = zero[i].x;
            if (float.IsNaN(h) || float.IsInfinity(h) || float.IsNaN(idle) || float.IsInfinity(idle))
                throw new InvalidOperationException("Invalid water displacement.");
            float x = ((i % columns + .5f) / columns - .5f) * 8f;
            float y = ((i / columns + .5f) / rows - .5f) * 4.5f;
            float work = h * h;
            peak = Mathf.Max(peak, Mathf.Abs(h));
            disabled = Mathf.Max(disabled, Mathf.Abs(idle));
            total += work;
            weighted += work * Vector2.Distance(new Vector2(x, y), first);
        }
        int ix = Mathf.FloorToInt((first.x / 8f + .5f) * columns);
        int iy = Mathf.FloorToInt((first.y / 4.5f + .5f) * rows);
        writer.WriteLine(string.Format(Invariant, "{0},{1:R},{2},{3},{4},{5:R},{6:R},{7:R},{8:R},{9:R}",
            frame, tick / (float)RainDropTimeline.Hz, landed, air, hits, peak, total, state[iy * columns + ix].x,
            total == 0 ? 0 : weighted / total, disabled));
    }

    static void WriteContacts(string output, RainDropTimeline timeline)
    {
        using var writer = new StreamWriter(Path.Combine(output, "contacts.csv"), false, Encoding.UTF8);
        writer.WriteLine("id,birthS,impactS,impactTick,groundU,groundV,initialDownMps,impactDownMps,windMps,cavityRadiusM,driveM");
        foreach (var d in timeline.Drops)
            writer.WriteLine(string.Format(Invariant, "{0},{1:R},{2:R},{3},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10:R}",
                d.Id, d.Birth, d.Land / (float)RainDropTimeline.Hz, d.Land,
                d.UV.x, d.UV.y, d.Speed, d.ImpactSpeed, d.Wind, d.Radius, d.Amplitude));
    }
}
