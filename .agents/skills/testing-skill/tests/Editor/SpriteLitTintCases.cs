using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class SpriteLitTintCases
{
    [Test]
    public void LightHdrTintAndSpriteCoverage()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SpriteLitTint.shadergraph");
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        Assert.That(shader.GetPropertyFlags(shader.FindPropertyIndex("_Tint")).HasFlag(ShaderPropertyFlags.HDR));
        var material = new Material(shader);
        var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
        texture.SetPixels(new[] { Color.white, Color.clear });
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
        void Render()
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            reader.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            reader.Apply();
        }
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            Assert.That(target.Create());
            camera.targetTexture = target;
            lamp.intensity = .25f;
            Render();
            float dim = Sample(16).r;
            lamp.intensity = 1;
            Render();
            float lit = Sample(16).r;
            Assert.That(dim, Is.GreaterThan(.05f));
            Assert.That(lit, Is.GreaterThan(dim * 2));
            material.SetColor("_Tint", new Color(4, 1, 1, 1));
            Render();
            Assert.That(Sample(16).r, Is.GreaterThan(2));
            Assert.That(Sample(16).g, Is.EqualTo(lit).Within(.05));
            Assert.That(Sample(48).r, Is.LessThan(.01));
            renderer.color = new Color(1, 1, 1, .5f);
            Render();
            Assert.That(Sample(16).g / lit, Is.EqualTo(.5f).Within(.05), "Renderer alpha must apply once.");
            TestContext.Out.WriteLine($"Low light {dim:R}, full light {lit:R}, half-alpha green {Sample(16).g:R}");
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
