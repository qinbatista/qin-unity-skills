using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

public partial class GraphTrialReplay
{
    private const int Width = 480, Height = 320, Rate = 60, ActiveTicks = 120;
    private static readonly string[] Cases = (from form in new[] {"Rain", "Embers", "Fountain", "Leaves", "Motes"}
                                             from pass in Enumerable.Range(1, 3)
                                             where Environment.GetEnvironmentVariable("QIN_VFX_TRIAL") == pass.ToString()
                                             select form + "-" + pass).ToArray();
    private GameObject stage;
    private Camera camera;
    private RenderTexture target;
    private Texture2D frame;
    private int previousRate;
    private bool previousMode;
    private EnterPlayModeOptions previousOptions;

    [Serializable]
    private class Sample
    {
        public int tick, pixels, changedPixels, left, right, bottom, top;
        public float centerX, centerY, meanRadius;
        public uint[] alive;
    }

    [Serializable]
    private class Run
    {
        public string graphGuid, unity, graphics;
        public string[] systems, dependencies;
        public uint[] capacities;
        public List<Sample> normal = new List<Sample>();
        public List<Sample> cohort = new List<Sample>();
        public List<Sample> raisedGround = new List<Sample>();
        public Sample retired, shifted, replay;
        public List<Sample> driverExtremes = new List<Sample>();
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
        Assert.That(SystemInfo.supportsComputeShaders, Is.True, "VFX Graph requires real GPU compute.");
        previousRate = Time.captureFramerate;
        Time.captureFramerate = Rate;
        stage = new GameObject("Graph replay");
        var view = new GameObject("XY camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        view.transform.SetParent(stage.transform, false);
        view.transform.localPosition = new Vector3(0f, 0f, -10f);
        camera = view.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.allowHDR = false;
        target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        Assert.That(target.Create(), Is.True);
        camera.targetTexture = target;
        frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
    }

    // First-use compilation runs synchronously on graphics-capable batch Editors.
    [UnityTest, Timeout(900000)]
    public IEnumerator ShowTrials([ValueSource(nameof(Cases))] string name)
    {
        string output = Path.Combine(Environment.GetEnvironmentVariable("QIN_VFX_OUTPUT"), name);
        Directory.CreateDirectory(output);
        string path = "Assets/GraphTrials/" + name + ".vfx";
        var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
        Assert.That(asset, Is.Not.Null, path);
        var host = new GameObject(name);
        host.SetActive(false);
        host.transform.SetParent(stage.transform, false);
        var effect = host.AddComponent<VisualEffect>();
        effect.visualEffectAsset = asset;
        effect.resetSeedOnPlay = false;
        effect.startSeed = 1729;
        Assert.That(effect.HasVector3("Driver"), Is.True, "The graph must consume the declared position.");
        effect.SetVector3("Driver", Vector3.zero);
        host.SetActive(true);
        effect.Reinit();
        var names = new List<string>();
        effect.GetParticleSystemNames(names);
        Assert.That(names.Count, Is.EqualTo(name.StartsWith("Rain-") ? 2 : 1));
        var run = new Run {
            graphGuid = AssetDatabase.AssetPathToGUID(path),
            unity = Application.unityVersion,
            graphics = SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName,
            systems = names.ToArray(),
            dependencies = AssetDatabase.GetDependencies(path, true),
            capacities = names.Select(system => effect.GetParticleSystemInfo(system).capacity).ToArray()
        };
        Color32[] previous = null;
        for (int tick = 1; tick <= ActiveTicks; tick++)
        {
            yield return null;
            if (tick % 3 == 0)
            {
                var sample = Capture(effect, run.systems, tick, Path.Combine(output, $"frame-{tick:D3}.png"), ref previous);
                run.normal.Add(sample);
            }
        }
        effect.Stop();
        for (int tick = 1; tick <= 240; tick++)
        {
            yield return null;
            if (tick % 30 == 0)
                run.retired = Capture(effect, run.systems, tick, Path.Combine(output, $"tail-{tick:D3}.png"), ref previous);
        }
        // A short emission window lets the same population show its path after the source closes.
        effect.Reinit();
        for (int tick = 1; tick <= ActiveTicks; tick++)
        {
            if (tick == 19) effect.Stop();
            yield return null;
            if (tick % 6 == 0)
                run.cohort.Add(Capture(effect, run.systems, tick, Path.Combine(output, $"cohort-{tick:D3}.png"), ref previous));
        }
        if (name.StartsWith("Rain-"))
        {
            Assert.That(effect.HasFloat("GroundHeight"), Is.True);
            float authoredGround = effect.GetFloat("GroundHeight");
            Assert.That(authoredGround, Is.EqualTo(-1.65f).Within(.01f));
            effect.SetFloat("GroundHeight", authoredGround + .9f);
            effect.Reinit();
            for (int tick = 1; tick <= ActiveTicks; tick++)
            {
                if (tick == 19) effect.Stop();
                yield return null;
                if (tick % 6 == 0)
                    run.raisedGround.Add(Capture(effect, run.systems, tick, Path.Combine(output, $"raised-{tick:D3}.png"), ref previous));
            }
            effect.SetFloat("GroundHeight", authoredGround);
        }
        // Restart the same component at the same seed. Move the input while it is already emitting.
        effect.Reinit();
        for (int tick = 1; tick <= ActiveTicks; tick++)
        {
            if (tick == 31) effect.SetVector3("Driver", Vector3.right * 1.25f);
            yield return null;
        }
        run.shifted = Capture(effect, run.systems, ActiveTicks, Path.Combine(output, "shifted.png"), ref previous);
        effect.SetVector3("Driver", Vector3.zero);
        effect.Reinit();
        for (int tick = 1; tick <= ActiveTicks; tick++) yield return null;
        run.replay = Capture(effect, run.systems, ActiveTicks, Path.Combine(output, "replay.png"), ref previous);
        // A wider camera keeps the whole manual box on screen while the source and floor move to their allowed extremes.
        camera.orthographicSize = 9f;
        var renderer = host.GetComponent<Renderer>();
        foreach (int sign in new[] {-1, 1})
        {
            effect.SetVector3("Driver", new Vector3(sign * 1.25f, sign * .60f, 0));
            if (name.StartsWith("Rain-")) effect.SetFloat("GroundHeight", sign < 0 ? -2.25f : -.50f);
            effect.Reinit();
            for (int tick = 1; tick <= 180; tick++)
            {
                yield return null;
                if (tick % 30 != 0) continue;
                var sample = Capture(effect, run.systems, tick, Path.Combine(output, $"extent-{sign}-{tick:D3}.png"), ref previous);
                Assert.That(sample.pixels, Is.GreaterThan(0), "The wider camera must still see the effect.");
                var bounds = renderer.bounds;
                Vector3 min = camera.WorldToViewportPoint(bounds.min);
                Vector3 max = camera.WorldToViewportPoint(bounds.max);
                Assert.That(sample.left, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(min.x * Width) - 1), name + " left bound");
                Assert.That(sample.right, Is.LessThanOrEqualTo(Mathf.CeilToInt(max.x * Width) + 1), name + " right bound");
                Assert.That(sample.bottom, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(min.y * Height) - 1), name + " lower bound");
                Assert.That(sample.top, Is.LessThanOrEqualTo(Mathf.CeilToInt(max.y * Height) + 1), name + " upper bound");
                run.driverExtremes.Add(sample);
            }
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(run, true));
        Assert.That(run.normal.Max(s => s.pixels), Is.GreaterThan(20), "The graph must draw on the active camera.");
        Assert.That(run.normal.Count(s => s.changedPixels > 8), Is.GreaterThan(20), "Animation must remain visible.");
        Assert.That(run.retired.pixels, Is.Zero, "Stopping must retire the visual tail.");
        Assert.That(run.replay.pixels, Is.GreaterThan(20), "The same instance must emit again.");
        Assert.That(run.shifted.centerX - run.replay.centerX, Is.GreaterThan(4), "The live input must alter screen-space motion.");
        for (int i = 0; i < names.Count; i++)
        {
            Assert.That(run.capacities[i], Is.GreaterThan(0));
            Assert.That(run.normal.Max(s => s.alive[i]), Is.LessThanOrEqualTo(run.capacities[i]));
        }
    }

    private Sample Capture(VisualEffect effect, string[] names, int tick, string file, ref Color32[] previous)
    {
        var priorTarget = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = target;
            frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            frame.Apply(false, false);
        }
        finally { RenderTexture.active = priorTarget; }
        File.WriteAllBytes(file, frame.EncodeToPNG());
        var colors = frame.GetPixels32();
        var sample = new Sample { tick = tick, left = Width, bottom = Height,
            alive = names.Select(name => effect.GetParticleSystemInfo(name).aliveCount).ToArray() };
        long xTotal = 0, yTotal = 0;
        double totalRadius = 0;
        for (int i = 0; i < colors.Length; i++)
        {
            var color = colors[i];
            if (previous != null && (Math.Abs(color.r - previous[i].r) + Math.Abs(color.g - previous[i].g)
                                      + Math.Abs(color.b - previous[i].b)) > 18)
                sample.changedPixels++;
            if (Math.Max(color.r, Math.Max(color.g, color.b)) <= 12) continue;
            int x = i % Width, y = i / Width;
            xTotal += x;
            yTotal += y;
            double dx = x - (Width - 1) * 0.5, dy = y - (Height - 1) * 0.5;
            totalRadius += Math.Sqrt(dx * dx + dy * dy);
            sample.pixels++;
            sample.left = Math.Min(sample.left, x);
            sample.right = Math.Max(sample.right, x);
            sample.bottom = Math.Min(sample.bottom, y);
            sample.top = Math.Max(sample.top, y);
        }
        if (sample.pixels > 0)
        {
            sample.centerX = (float)xTotal / sample.pixels;
            sample.centerY = (float)yTotal / sample.pixels;
            sample.meanRadius = (float)(totalRadius / sample.pixels);
        }
        previous = colors;
        return sample;
    }

    [UnityTearDown]
    public IEnumerator Close()
    {
        if (camera) camera.targetTexture = null;
        if (stage) Object.DestroyImmediate(stage);
        if (frame) Object.DestroyImmediate(frame);
        if (target) { target.Release(); Object.DestroyImmediate(target); }
        Time.captureFramerate = previousRate;
        yield return new ExitPlayMode();
        EditorSettings.enterPlayModeOptionsEnabled = previousMode;
        EditorSettings.enterPlayModeOptions = previousOptions;
    }
}
