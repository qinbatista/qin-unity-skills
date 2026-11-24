using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

public sealed partial class QinFluidReplay
{
    private const float SurfaceY = -1.88f;
    private const string WaterLayer = "Fluid replay water";
    private const string Pipeline = "Assets/FluidReplayPipeline.asset";
    private const int Width = 960, Height = 540, Frames = 84, Fps = 30;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly string[] Forms = { "PlanarRipples", "FallingMass", "FreeJet", "RockImpact", "OpposedJets", "Waterfall", "Vortex" };
    private static readonly int[] Probes = { 0, 6, 12, 17, 20, 24, 28, 33, 38, 47, 57, 66, 74 };
    private static readonly int OpticMode = Shader.PropertyToID("_QinFluidOpticsMode");
    private static readonly FieldInfo Simulation = typeof(FluidMixController).GetField("_simulation", Members);

    private GameObject stage;
    private Camera camera;
    private RenderTexture target;
    private Texture2D pixels;
    private Material spriteMaterial;
    private Sprite backdropSprite, fixtureFluidSprite;
    private Texture2D backdropTexture, fixtureFluidTexture;
    private int previousRate;
    private bool previousMode;
    private EnterPlayModeOptions previousOptions;
    private float previousOptics;

    [StructLayout(LayoutKind.Sequential)]
    private struct Parcel { public Vector4 position, velocity; }

    private static string Imported => Environment.GetEnvironmentVariable("QIN_FLUID_ASSET");
    private static uint Seed => uint.Parse(Environment.GetEnvironmentVariable("QIN_FLUID_SEED"));

    // This entry only configures a new, disposable receiver made by the replay command.
    public static void Configure()
    {
        if (!File.Exists(".qin-fluid-replay") || string.IsNullOrEmpty(Imported))
            throw new InvalidOperationException("Use the fluid replay command to make a disposable receiver.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("m_SortingLayers");
        if (!SortingLayer.layers.Any(layer => layer.name == WaterLayer))
        {
            layers.InsertArrayElementAtIndex(layers.arraySize);
            var added = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            added.FindPropertyRelative("name").stringValue = WaterLayer;
            added.FindPropertyRelative("uniqueID").intValue = 1986412;
            added.FindPropertyRelative("locked").boolValue = false;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
        var settings = new SerializedObject(renderer);
        settings.FindProperty("m_UseCameraSortingLayersTexture").boolValue = true;
        settings.FindProperty("m_CameraSortingLayersTextureBound").intValue = SortingLayer.NameToID("Default");
        settings.FindProperty("m_CameraSortingLayerDownsamplingMethod").intValue = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(renderer, "Assets/FluidReplayRenderer.asset");
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        AssetDatabase.CreateAsset(pipeline, Pipeline);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        foreach (string pattern in new[] { "*.shadergraph", "*.vfx", "*.compute" })
            foreach (string path in Directory.GetFiles(Imported, pattern, SearchOption.AllDirectories))
                AssetDatabase.ImportAsset(path.Replace('\\','/'), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Debug.Log("QIN_FLUID_RECEIVER_READY");
    }

    [UnitySetUp]
    public IEnumerator Open()
    {
        previousMode = EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode(expectDomainReload: false);
        Assert.That(Application.unityVersion, Is.EqualTo("6000.6.7f1"));
        previousRate = Time.captureFramerate;
        previousOptics = Shader.GetGlobalFloat(OpticMode);
        Time.captureFramerate = 60;
        Shader.SetGlobalFloat(OpticMode, 0f);
        stage = new GameObject("Fluid inspection");
        var view = new GameObject("Fluid camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        view.transform.SetParent(stage.transform, false);
        view.transform.position = new Vector3(0,-.7f,-10f);
        camera = view.GetComponent<Camera>();
        camera.orthographic = true;
        Assert.That(camera.transform.forward, Is.EqualTo(Vector3.forward));
        Vector3 near = camera.WorldToViewportPoint(new Vector3(1f, 1f, -.5f));
        Vector3 far = camera.WorldToViewportPoint(new Vector3(1f, 1f, .5f));
        Assert.That((Vector2)near, Is.EqualTo((Vector2)far));
        camera.orthographicSize = 4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f,.14f,.18f);
        camera.allowHDR = false;
        Assert.That(view.GetComponent<UniversalAdditionalCameraData>().scriptableRenderer.GetType().Name, Is.EqualTo("Renderer2D"));
        target = new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);
        Assert.That(target.Create(), Is.True);
        camera.targetTexture = target;
        pixels = new Texture2D(Width,Height,TextureFormat.RGB24,false);
        spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        backdropTexture = MakeFluidBackdrop(out backdropSprite);
    }

    [UnityTest]
    public IEnumerator Replay([ValueSource(nameof(Forms))] string form)
    {
        string folder = Path.Combine(Environment.GetEnvironmentVariable("QIN_FLUID_OUTPUT"),form);
        foreach (string sub in new[] { "frames","normal","control" }) Directory.CreateDirectory(Path.Combine(folder,sub));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Imported + "/Variants/" + form + ".prefab");
        var recipe = AssetDatabase.LoadAssetAtPath<SOFluidMixRecipe>(Imported + "/Presets/" + form + ".asset");
        Assert.That(prefab && recipe, Is.True);
        Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Variant));
        Assert.That(prefab.GetComponent<FluidMixController>().Recipe, Is.EqualTo(recipe), "The saved drag-and-play variant must bind its own recipe");
        if (recipe.SolidObstacle) DrawFluidObstacle(recipe.ObstacleCenter,recipe.ObstacleRadius);
        yield return null;
        Capture();
        byte[] dry = Capture();
        File.WriteAllBytes(Path.Combine(folder,"dry.png"),dry);
        var holder = new GameObject("Receiver");
        holder.transform.SetParent(stage.transform,false);
        holder.SetActive(false);
        var effect = Object.Instantiate(prefab,holder.transform,false);
        effect.transform.localPosition = new Vector3(0,SurfaceY,0);
        var owner = effect.GetComponent<FluidMixController>();
        Assert.That(effect.GetComponentsInChildren<Component>(true).All(c => c), Is.True);
        Assert.That(effect.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        foreach (var mesh in effect.GetComponentsInChildren<MeshRenderer>(true))
        {
            Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMaterial.shader), Does.EndWith(".shadergraph"));
            Assert.That(mesh.sharedMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(mesh.sharedMaterial.shader), Is.True);
        }
        var spray = effect.GetComponentInChildren<VisualEffect>(true);
        Assert.That(AssetDatabase.GetAssetPath(spray.visualEffectAsset), Does.EndWith(".vfx"));
        Assert.That(spray.HasTexture("MaterialState") && spray.HasFloat("ReleaseWeight"), Is.True);
        var control = Object.Instantiate(recipe);
        var typed = new SerializedObject(control);
        var flags = typed.FindProperty("_responses");
        switch (form)
        {
            case "PlanarRipples": typed.FindProperty("_strength").floatValue = 0; break;
            case "FallingMass": case "Waterfall": typed.FindProperty("_gravity").floatValue = 0; break;
            case "FreeJet": flags.intValue &= ~(int)SOFluidMixRecipe.Response.SurfaceWaves; break;
            case "RockImpact": flags.intValue &= ~(int)SOFluidMixRecipe.Response.SolidObstacle; break;
            case "OpposedJets": typed.FindProperty("_pressure").floatValue = 0; break;
            case "Vortex": typed.FindProperty("_swirl").floatValue = 0; break;
        }
        typed.ApplyModifiedPropertiesWithoutUndo();
        var stages = new HashSet<FluidMixController.VisualStage>();
        owner.SetWaterSortingLayer(WaterLayer);
        try
        {
            foreach (var mode in new[] { "normal","control" })
            {
                owner.SetRecipe(mode == "normal" ? recipe : control);
                holder.SetActive(true);
                owner.BeginCast(Seed);
                for (int frame = 0; frame < Frames; frame++)
                {
                    float time = frame / (float)Fps;
                    owner.SetCastTime(time);
                    stages.Add(owner.CurrentStage);
                    yield return null;
                    owner.SetCastTime(time);
                    var file = frame.ToString("D4");
                    if (mode == "normal")
                        File.WriteAllBytes(Path.Combine(folder,"frames",file + ".png"),Capture());
                    if (Array.IndexOf(Probes,frame) >= 0)
                    {
                        File.WriteAllBytes(Path.Combine(folder,mode,file + ".bin"),State(owner));
                        if (mode == "control") File.WriteAllBytes(Path.Combine(folder,mode,file + ".png"),Capture());
                    }
                    if (mode == "normal" && frame == 24)
                    {
                        byte[] frozen = State(owner);
                        owner.SetCastTime(time);
                        CollectionAssert.AreEqual(frozen,State(owner),"Fixed time changed the liquid state");
                        foreach (var optical in new[] { 1,2,3 })
                        {
                            Shader.SetGlobalFloat(OpticMode,optical);
                            File.WriteAllBytes(Path.Combine(folder,"optics-" + optical + ".png"),Capture());
                        }
                        Shader.SetGlobalFloat(OpticMode,0);
                    }
                }
                Assert.That(owner.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
                Assert.That(Capture().SequenceEqual(dry), Is.True, "Visible residue after retirement");
            }
            Assert.That(stages.Count, Is.EqualTo(7));
            owner.SetRecipe(recipe);
            owner.BeginCast(Seed);
            owner.SetCastTime(12f / Fps);
            yield return null;
            owner.SetCastTime(12f / Fps);
            Assert.That(State(owner).SequenceEqual(File.ReadAllBytes(Path.Combine(folder,"normal","0012.bin"))), Is.True,
                        "Recasting the pooled component must restore the same fixed-step state");
            owner.EndCast();
            owner.SetCastTime(.51f);
            Assert.That(owner.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Retracting));
            owner.SetCastTime(.72f);
            yield return null;
            Assert.That(Capture().SequenceEqual(dry), Is.True, "Interrupted water left a visible footprint");
            owner.BeginCast(Seed ^ 0x9e3779b9u);
            for (int frame = 0; frame <= 24; frame++)
            {
                owner.SetCastTime(frame / (float)Fps);
                yield return null;
            }
            owner.SetCastTime(24f / Fps);
            File.WriteAllBytes(Path.Combine(folder,"alternate.bin"),State(owner));
            File.WriteAllBytes(Path.Combine(folder,"alternate.png"),Capture());
            File.WriteAllText(Path.Combine(folder,"device.txt"),Application.unityVersion + "\n" + SystemInfo.graphicsDeviceName + "\n" + SystemInfo.graphicsDeviceType);
            if (form == "FallingMass")
                yield return VerifyDepthDoesNotShiftImage(owner, recipe, folder);
        }
        finally { Object.Destroy(control); }
    }

    private IEnumerator VerifyDepthDoesNotShiftImage(FluidMixController owner, SOFluidMixRecipe recipe, string folder)
    {
        SOFluidMixRecipe moved = Object.Instantiate(recipe);
        applicationResources.Add(moved);
        SerializedObject data = new(moved);
        data.FindProperty("_origin").vector3Value += new Vector3(0f, 0f, .52f);
        data.ApplyModifiedPropertiesWithoutUndo();
        string path = Path.Combine(folder, "depth-check");
        Directory.CreateDirectory(path);

        foreach (var sample in new[] { (Name: "base", Recipe: recipe), (Name: "offset", Recipe: moved) })
        {
            owner.SetRecipe(sample.Recipe);
            owner.BeginCast(Seed);
            const float inFlight = .4f;
            owner.SetCastTime(inFlight);
            yield return null;
            owner.SetCastTime(inFlight);
            File.WriteAllBytes(Path.Combine(path, sample.Name + ".png"), Capture());
            File.WriteAllBytes(Path.Combine(path, sample.Name + ".bin"), State(owner));
        }
    }

    private byte[] Capture()
    {
        var previous = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,Width,Height),0,0,false);
            pixels.Apply(false,false);
            return pixels.EncodeToPNG();
        }
        finally { RenderTexture.active = previous; }
    }

    private static byte[] State(FluidMixController owner)
    {
        var simulation = Simulation.GetValue(owner);
        Assert.That(simulation, Is.Not.Null,"This GPU must support the compute and render texture formats");
        Type type = simulation.GetType();
        var buffer = (GraphicsBuffer)type.GetProperty("Particles",Members).GetValue(simulation);
        var waves = (RenderTexture)type.GetProperty("Waves",Members).GetValue(simulation);
        var parcels = new Parcel[buffer.count];
        buffer.GetData(parcels);
        var read = AsyncGPUReadback.Request(waves,0);
        read.WaitForCompletion();
        Assert.That(read.hasError, Is.False);
        var values = read.GetData<float>();
        Assert.That(parcels.Length, Is.EqualTo(384));
        Assert.That(values.Length, Is.EqualTo(128 * 64 * 2));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (var p in parcels)
        {
            for (int i=0;i<4;i++) writer.Write(p.position[i]);
            for (int i=0;i<4;i++) writer.Write(p.velocity[i]);
        }
        for (int i=0;i<values.Length;i++) writer.Write(values[i]);
        return stream.ToArray();
    }

    private Texture2D MakeFluidBackdrop(out Sprite sprite)
    {
        const int width = 640, height = 360;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        var pixels = new Color32[width * height];
        float plane = .5f + (SurfaceY - camera.transform.position.y) / (2f * camera.orthographicSize);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                float v = y / (float)height;
                float grain = Mathf.PerlinNoise(20f * u + 9f, 30f * v + 11f);
                float muted = .5f + .5f * Mathf.Sin(31f * u + .82f * Mathf.Sin(7f * v));
                Color sky = Color.Lerp(new Color(.025f, .071f, .117f), new Color(.081f, .167f, .210f),
                    .48f * grain + .24f * muted + .20f * Mathf.SmoothStep(0f, 1f, v));
                Color bank = Color.Lerp(new Color(.023f, .055f, .084f), new Color(.063f, .127f, .151f), grain * .63f);
                float seam = Mathf.SmoothStep(0f, 1f, (v - plane) * 95f);
                Color waterline = new Color(.063f, .088f, .079f) * Mathf.Exp(-Mathf.Abs(v - plane) * 116f);
                pixels[y * width + x] = Color.Lerp(bank, sky, seam) + waterline;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        float units = camera.orthographicSize * 2f;
        sprite = Sprite.Create(texture, new Rect(0,0,width,height), Vector2.one * .5f, height / units);
        var back = new GameObject("Fluid lighting backdrop", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        back.transform.SetParent(stage.transform,false);
        back.transform.position = new Vector3(camera.transform.position.x,camera.transform.position.y,1f);
        back.sprite = sprite;
        back.sortingLayerName = "Default";
        back.sortingOrder = -300;
        back.sharedMaterial = spriteMaterial;
        return texture;
    }

    private void DrawFluidObstacle(Vector3 center, float radius)
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(2f * (x + .5f) / size - 1f, 2f * (y + .5f) / size - 1f);
                Color pigment = SideStoneColor(uv.x, uv.y, Mathf.PerlinNoise(1.8f * uv.x + 8f, 2f * uv.y + 7f));
                pigment.a = Mathf.Clamp01((1f - uv.magnitude) * size);
                colors[y * size + x] = pigment;
            }
        }
        texture.SetPixels32(colors);
        texture.Apply(false);
        SpriteRenderer obstacle = new GameObject("Side sprite boundary", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        obstacle.transform.SetParent(stage.transform,false);
        obstacle.transform.localPosition = new Vector3(center.x, center.y + SurfaceY, 0f);
        var sprite = Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one * .5f,size / (2f * radius));
        obstacle.sprite = sprite;
        obstacle.sharedMaterial = spriteMaterial;
        obstacle.sortingLayerName = "Default";
        obstacle.sortingOrder = -10;
        fixtureFluidSprite = sprite;
        fixtureFluidTexture = texture;
    }

    [UnityTearDown]
    public IEnumerator Close()
    {
        if (camera) camera.targetTexture = null;
        if (stage) Object.DestroyImmediate(stage);
        foreach (Object resource in applicationResources)
            if (resource) Object.DestroyImmediate(resource);
        applicationResources.Clear();
        foreach (Object item in new Object[] { backdropSprite,fixtureFluidSprite,backdropTexture,fixtureFluidTexture,spriteMaterial,pixels })
            if (item) Object.DestroyImmediate(item);
        if (target) { target.Release(); Object.DestroyImmediate(target); }
        Time.captureFramerate = previousRate;
        Shader.SetGlobalFloat(OpticMode,previousOptics);
        yield return new ExitPlayMode();
        EditorSettings.enterPlayModeOptionsEnabled = previousMode;
        EditorSettings.enterPlayModeOptions = previousOptions;
    }
}
