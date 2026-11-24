using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class FlowSkyUrpReplay
{
    private const int Width = 384, Height = 216;
    private static readonly int[] Ticks = { 0, 8, 23, 41, 56 };

    [Serializable]
    private class Sample
    {
        public string subject, stage;
        public int tick, wrapperPixels, graphPixels, differentPixels, largestChannelError;
        public float opacity, meanVisibleChannelError;
    }

    [Serializable]
    private class Receipt
    {
        public string editor, urp, renderer, backend, device, colorSpace;
        public int width = Width, height = Height, fps = 30, hiddenDifference;
        public List<Sample> samples = new List<Sample>();
    }

    private Camera camera;
    private RenderTexture target;
    private Texture2D pixels;
    private FlowSky2DPlayer player;
    private MeshRenderer surface;
    private Material wrapper, graph;
    private string pipelinePath, rendererPath;
    private RenderPipelineAsset previousPipeline, previousQuality;
    private bool previousMode, configured;
    private EnterPlayModeOptions previousOptions;

    [UnitySetUp]
    public IEnumerator Open()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        Assert.IsTrue(File.Exists(Path.Combine(project, ".qin-flow-sky-urp-replay")),
            "Use a disposable receiver with the .qin-flow-sky-urp-replay marker.");
        Assert.AreEqual("6000.6.7f1", Application.unityVersion);
        Assert.AreEqual("17.6.0", PackageInfo.FindForAssembly(typeof(UniversalRenderPipelineAsset).Assembly).version);
        string packageShader = AssetDatabase.GUIDToAssetPath("8d420fba20f74c8b92fb306b8af92acb");
        Assert.IsTrue(packageShader.EndsWith("/FlowSky2D.shader"), "Copy the prefab package with its metadata.");
        previousPipeline = GraphicsSettings.defaultRenderPipeline;
        previousQuality = QualitySettings.renderPipeline;
        previousMode = EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions = EditorSettings.enterPlayModeOptions;
        configured = true;
        var data = ScriptableObject.CreateInstance<Renderer2DData>();
        rendererPath = AssetDatabase.GenerateUniqueAssetPath("Assets/FlowSkyParityRenderer.asset");
        AssetDatabase.CreateAsset(data, rendererPath);
        var pipeline = UniversalRenderPipelineAsset.Create(data);
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        pipelinePath = AssetDatabase.GenerateUniqueAssetPath("Assets/FlowSkyParityPipeline.asset");
        AssetDatabase.CreateAsset(pipeline, pipelinePath);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(expectDomainReload: false);

        var view = new GameObject("Flow sky parity camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        camera = view.GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -3);
        camera.orthographic = true;
        camera.orthographicSize = 1f;
        camera.aspect = (float)Width / Height;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.047f, 0.066f, 0.113f);
        camera.cullingMask = 1 << 30;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        Assert.AreEqual("Renderer2D", view.GetComponent<UniversalAdditionalCameraData>().scriptableRenderer.GetType().Name);
        target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Assert.IsTrue(target.Create());
        camera.targetTexture = target;
        pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path.ChangeExtension(packageShader, ".prefab"));
        Assert.IsNotNull(prefab);
        player = Object.Instantiate(prefab).GetComponent<FlowSky2DPlayer>();
        player.AutomaticClock = false;
        player.gameObject.layer = 30;
        player.transform.localScale = new Vector3(2 * camera.aspect, 2, 1);
        surface = player.GetComponent<MeshRenderer>();
        wrapper = surface.sharedMaterial;
        string[] graphs = AssetDatabase.FindAssets("FlowSkySurface t:Shader");
        Assert.AreEqual(1, graphs.Length, "Copy the single optional FlowSkySurface shader graph.");
        string graphPath = AssetDatabase.GUIDToAssetPath(graphs[0]);
        Assert.IsTrue(graphPath.EndsWith("/FlowSkySurface.shadergraph"));
        var imported = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
        Assert.IsTrue(imported.isSupported && !ShaderUtil.ShaderHasError(imported), graphPath);
        graph = new Material(imported);
        foreach (string property in new[] { "_Case", "_Phase", "_Opacity" })
            Assert.IsTrue(graph.HasProperty(property), property);
    }

    private Color32[] Capture()
    {
        camera.Render();
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            pixels.Apply(false);
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = previous; }
    }

    private static int Difference(Color32 a, Color32 b)
    {
        return Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
    }

    private static void Compare(Sample row, Color32[] off, Color32[] reference, Color32[] candidate)
    {
        long error = 0;
        int visible = 0;
        for (int i = 0; i < off.Length; i++)
        {
            Color32 a = reference[i], b = candidate[i];
            bool wrapperVisible = Difference(off[i], a) >= 12;
            bool graphVisible = Difference(off[i], b) >= 12;
            if (wrapperVisible) row.wrapperPixels++;
            if (graphVisible) row.graphPixels++;
            int delta = Difference(a, b);
            if (delta >= 12) row.differentPixels++;
            if (wrapperVisible || graphVisible)
            {
                visible++;
                error += delta;
            }
            int channel = Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
            row.largestChannelError = Math.Max(row.largestChannelError, channel);
        }
        row.meanVisibleChannelError = visible == 0 ? 0f : error / (3f * visible);
    }

    [UnityTest]
    public IEnumerator CompareMaterials()
    {
        var receipt = new Receipt {
            editor = Application.unityVersion,
            urp = PackageInfo.FindForAssembly(typeof(UniversalRenderPipelineAsset).Assembly).version,
            renderer = camera.GetComponent<UniversalAdditionalCameraData>().scriptableRenderer.GetType().Name,
            backend = SystemInfo.graphicsDeviceType.ToString(),
            device = SystemInfo.graphicsDeviceName,
            colorSpace = QualitySettings.activeColorSpace.ToString()
        };
        surface.enabled = false;
        yield return null;
        Color32[] background = Capture();
        var block = new MaterialPropertyBlock();
        foreach (FlowSky2DCase subject in Enum.GetValues(typeof(FlowSky2DCase)))
        {
            player.Play(subject);
            int previousTick = 0;
            foreach (int tick in Ticks)
            {
                for (int i = previousTick; i < tick; i++) player.Advance(1f / receipt.fps);
                previousTick = tick;
                surface.GetPropertyBlock(block);
                var row = new Sample { subject = subject.ToString(), stage = player.Stage.ToString(),
                    tick = tick, opacity = block.GetFloat("_Opacity") };
                surface.sharedMaterial = wrapper;
                Color32[] reference = Capture();
                surface.sharedMaterial = graph;
                Compare(row, background, reference, Capture());
                receipt.samples.Add(row);
            }
            yield return null;
        }
        surface.enabled = false;
        Color32[] hidden = Capture();
        for (int i = 0; i < hidden.Length; i++)
            receipt.hiddenDifference = Math.Max(receipt.hiddenDifference, Difference(background[i], hidden[i]));
        string output = Environment.GetEnvironmentVariable("FLOW_SKY_URP_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "FlowSkyUrpParity.json"), JsonUtility.ToJson(receipt, true));
        }
        Assert.AreEqual(0, receipt.hiddenDifference, "Hiding the draw must restore the same camera background.");
        foreach (Sample row in receipt.samples)
        {
            string label = row.subject + " at native tick " + row.tick;
            if (row.tick == 0)
            {
                Assert.AreEqual(0, row.wrapperPixels + row.graphPixels, label);
                continue;
            }
            Assert.Greater(row.wrapperPixels, 50, label + " reference draw");
            Assert.Greater(row.graphPixels, 50, label + " graph draw");
            Assert.Less(row.meanVisibleChannelError, 1.5f, label + " visible RGB");
            Assert.Less(Math.Abs(row.wrapperPixels - row.graphPixels), Math.Max(36, row.wrapperPixels / 50),
                label + " coverage");
        }
    }

    [UnityTearDown]
    public IEnumerator Close()
    {
        if (camera != null) camera.targetTexture = null;
        if (player != null) Object.Destroy(player.gameObject);
        if (camera != null) Object.Destroy(camera.gameObject);
        if (graph != null) Object.Destroy(graph);
        if (pixels != null) Object.Destroy(pixels);
        if (target != null) { target.Release(); Object.Destroy(target); }
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (configured)
        {
            GraphicsSettings.defaultRenderPipeline = previousPipeline;
            QualitySettings.renderPipeline = previousQuality;
            EditorSettings.enterPlayModeOptions = previousOptions;
            EditorSettings.enterPlayModeOptionsEnabled = previousMode;
            if (!string.IsNullOrEmpty(pipelinePath)) AssetDatabase.DeleteAsset(pipelinePath);
            if (!string.IsNullOrEmpty(rendererPath)) AssetDatabase.DeleteAsset(rendererPath);
            AssetDatabase.SaveAssets();
        }
    }
}
