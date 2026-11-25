using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class SpriteLitTintCases
{
    [Serializable]
    private sealed class Frame
    {
        public string state, sha256;
        public float red, green, blue, alpha, clearRed, clearAlpha;
    }

    [Serializable]
    private sealed class Capture
    {
        public string unity, urp, shaderGraph, graphics, device, colorSpace, pipeline, readback, target;
        public int width, height, samples, shaderErrors;
        public bool hdr, littleEndian;
        public List<Frame> frames = new List<Frame>();
    }

    // Preserve existing callers' own graphics settings outside the disposable receiver.
    [UnitySetUp]
    public IEnumerator ConfigureReceiver()
    {
        var root = Directory.GetParent(Application.dataPath).FullName;
        if (!File.Exists(Path.Combine(root, ".sprite-graph-receiver")))
            yield break;
        Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear));
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/ReceiverPipeline.asset");
        if (!pipeline)
        {
            var data = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(data, "Assets/Receiver2D.asset");
            pipeline = UniversalRenderPipelineAsset.Create(data);
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 1;
            AssetDatabase.CreateAsset(pipeline, "Assets/ReceiverPipeline.asset");
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        AssetDatabase.SaveAssets();
        yield return null;
    }

    [Test, Timeout(900000)] // First-time URP shader compilation can outlast NUnit’s 180-second default.
    public void LightHdrTintAndSpriteCoverage()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SpriteLitTint.shadergraph");
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        Assert.That(shader.GetPropertyFlags(shader.FindPropertyIndex("_Tint")).HasFlag(ShaderPropertyFlags.HDR));
        var material = new Material(shader);
        var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, new Color(1, .3f, .8f, 0) });
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 1), Vector2.one * .5f, 1f, 0, SpriteMeshType.FullRect);
        var actor = new GameObject("Tinted sprite", typeof(SpriteRenderer));
        var renderer = actor.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        var lamp = new GameObject("White light", typeof(Light2D)).GetComponent<Light2D>();
        lamp.lightType = Light2D.LightType.Global;
        var camera = new GameObject("Linear HDR capture", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = Vector3.back * 10;
        camera.orthographic = true;
        camera.orthographicSize = 1;
        camera.allowHDR = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        var reader = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
        var previousTarget = RenderTexture.active;
        bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
        Color Sample(int x) => reader.GetPixel(x, 32);
        string output = Environment.GetEnvironmentVariable("QIN_SPRITE_GRAPH_CAPTURE");
        var capture = new Capture
        {
            unity = Application.unityVersion,
            urp = PackageInfo.FindForAssembly(typeof(UniversalRenderPipelineAsset).Assembly).version,
            shaderGraph = PackageInfo.FindForAssetPath("Packages/com.unity.shadergraph/package.json").version,
            graphics = SystemInfo.graphicsDeviceType.ToString(),
            device = SystemInfo.graphicsDeviceName,
            colorSpace = QualitySettings.activeColorSpace.ToString(),
            pipeline = GraphicsSettings.currentRenderPipeline.name,
            readback = reader.format.ToString(),
            target = target.format.ToString(),
            width = reader.width,
            height = reader.height,
            samples = target.antiAliasing,
            shaderErrors = ShaderUtil.ShaderHasError(shader) ? 1 : 0,
            hdr = camera.allowHDR,
            littleEndian = BitConverter.IsLittleEndian
        };
        void Render(string state)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            reader.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            reader.Apply();
            if (state != null && !string.IsNullOrEmpty(output))
            {
                byte[] raw = reader.GetRawTextureData();
                Assert.That(raw.Length, Is.EqualTo(reader.width * reader.height * 4 * sizeof(float)));
                string framesPath = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "-frames");
                Directory.CreateDirectory(framesPath);
                File.WriteAllBytes(Path.Combine(framesPath, state + ".rgba32f"), raw);
                using var sha = SHA256.Create();
                var pixel = Sample(16);
                var clear = Sample(48);
                capture.frames.Add(new Frame
                {
                    state = state, sha256 = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant(),
                    red = pixel.r, green = pixel.g, blue = pixel.b, alpha = pixel.a, clearRed = clear.r, clearAlpha = clear.a
                });
            }
        }
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            Assert.That(target.Create());
            camera.targetTexture = target;
            if (!string.IsNullOrEmpty(output))
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
                Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear));
                Assert.That(camera.GetUniversalAdditionalCameraData().scriptableRenderer, Is.Not.Null);
                Assert.That(camera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name, Is.EqualTo("Renderer2D"));
                Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.False);
            }
            lamp.intensity = .25f;
            Render("dim");
            float dim = Sample(16).r;
            if (!string.IsNullOrEmpty(output))
            {
                Assert.That(Sample(16).a, Is.EqualTo(1f).Within(.004f));
                Assert.That(Sample(48).a, Is.EqualTo(0f).Within(.004f));
            }
            lamp.intensity = 1;
            Render("lit");
            float lit = Sample(16).r;
            if (!string.IsNullOrEmpty(output))
            {
                Assert.That(Sample(16).a, Is.EqualTo(1f).Within(.004f));
                Assert.That(Sample(48).a, Is.EqualTo(0f).Within(.004f));
            }
            Assert.That(dim, Is.GreaterThan(.05f));
            Assert.That(lit, Is.GreaterThan(dim * 2));
            material.SetColor("_Tint", new Color(4, 1, 1, 1));
            Render("hdrTint");
            Assert.That(Sample(16).r, Is.GreaterThan(2));
            Assert.That(Sample(16).g, Is.EqualTo(lit).Within(.05));
            Assert.That(Sample(48).r, Is.LessThan(.01));
            renderer.color = new Color(1, 1, 1, .5f);
            Render("rendererHalfAlpha");
            Assert.That(Sample(16).g / lit, Is.EqualTo(.5f).Within(.05), "Renderer alpha must apply once.");
            float halfAlphaGreen = Sample(16).g;
            if (!string.IsNullOrEmpty(output))
                Assert.That(Sample(16).a, Is.EqualTo(.5f).Within(.006f));
            renderer.color = Color.white;
            material.SetColor("_Tint", new Color(1, 1, 1, .3f));
            Render("materialAlpha");
            if (!string.IsNullOrEmpty(output))
                Assert.That(Sample(16).a, Is.EqualTo(.3f).Within(.006f));
            Assert.That(Sample(16).g / lit, Is.EqualTo(.3f).Within(.03), "Material alpha must apply once.");
            texture.SetPixels(new[] { new Color(.25f, .5f, .75f, 1), new Color(1, .3f, .8f, 0) });
            texture.Apply();
            material.SetColor("_Tint", Color.white);
            Render("coloredSprite");
            var colored = Sample(16);
            Assert.That(colored.r / lit, Is.EqualTo(.25f).Within(.02));
            Assert.That(colored.g / lit, Is.EqualTo(.5f).Within(.02));
            Assert.That(colored.b / lit, Is.EqualTo(.75f).Within(.02));
            renderer.color = new Color(1, .5f, .25f, 1);
            Render("rendererRgb");
            var tinted = Sample(16);
            var standardShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            Assert.That(standardShader, Is.Not.Null);
            var standardMaterial = new Material(standardShader);
            try
            {
                renderer.sharedMaterial = standardMaterial;
                Render(null);
                var standard = Sample(16);
                Assert.That(tinted.r, Is.EqualTo(standard.r).Within(.006f));
                Assert.That(tinted.g, Is.EqualTo(standard.g).Within(.006f));
                Assert.That(tinted.b, Is.EqualTo(standard.b).Within(.006f));
                Assert.That(tinted.g, Is.LessThan(colored.g * .7f));
                Assert.That(tinted.b, Is.LessThan(colored.b * .4f));
            }
            finally
            {
                renderer.sharedMaterial = material;
                Object.DestroyImmediate(standardMaterial);
            }
            camera.backgroundColor = new Color(.35f, .6f, .9f, 1);
            renderer.enabled = false;
            Render("backgroundOnly");
            var backdrop = Sample(48);
            Assert.That(backdrop.maxColorComponent, Is.GreaterThan(.1f));
            renderer.enabled = true;
            Render("transparentOnColor");
            var uncovered = Sample(48);
            Assert.That(uncovered.r, Is.EqualTo(backdrop.r).Within(.004f));
            Assert.That(uncovered.g, Is.EqualTo(backdrop.g).Within(.004f));
            Assert.That(uncovered.b, Is.EqualTo(backdrop.b).Within(.004f));
            Assert.That(uncovered.a, Is.EqualTo(backdrop.a).Within(.004f));
            Assert.That(Sample(16).b, Is.LessThan(uncovered.b * .7f));
            if (!string.IsNullOrEmpty(output))
                File.WriteAllText(output, JsonUtility.ToJson(capture, true));
            TestContext.Out.WriteLine($"Low light {dim:R}, full light {lit:R}, half-alpha green {halfAlphaGreen:R}");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previousTarget;
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
            target.Release();
            foreach (var item in new Object[] { camera.gameObject, lamp.gameObject, actor, sprite, texture, reader, target, material })
                Object.DestroyImmediate(item);
        }
    }
}
