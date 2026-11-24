using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Qin.UnityEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class FlowSkyNativeReplay
{
    [Serializable]
    public class CaseResult
    {
        public string subject;
        public int frames, peakPixels, changedFrames, maxChangingPixels;
    }

    [Serializable]
    public class ReplayResult
    {
        public string editor, graphics;
        public int width, height, fps;
        public List<CaseResult> cases = new List<CaseResult>();
    }

    private const int Width = 640, Height = 360, Fps = 30;
    private Camera camera;
    private RenderTexture target;
    private Texture2D pixels;
    private GameObject rig;
    private FlowSky2DPlayer player;
    private MeshRenderer surface;
    private bool previousMode;
    private EnterPlayModeOptions previousOptions;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousMode = EditorSettings.enterPlayModeOptionsEnabled;
        previousOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(expectDomainReload: false);
        rig = new GameObject("Flow sky replay camera");
        camera = rig.AddComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 1f;
        camera.aspect = (float)Width / Height;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.026f, 0.045f, 0.085f, 1f);
        camera.transform.position = new Vector3(0, 0, -3f);
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << 30;
        target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        string[] matches = AssetDatabase.FindAssets("FlowSky2D t:Prefab");
        Assert.AreEqual(1, matches.Length, "Use one installed FlowSky2D prefab in the disposable receiver.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(matches[0]));
        player = Object.Instantiate(prefab).GetComponent<FlowSky2DPlayer>();
        player.gameObject.layer = 30;
        player.transform.localScale = new Vector3(2f * camera.aspect, 2f, 1f);
        player.AutomaticClock = false;
        surface = player.GetComponent<MeshRenderer>();
        Assert.IsTrue(surface.sharedMaterial.shader.isSupported);
        Assert.IsEmpty(ShaderUtil.GetShaderMessages(surface.sharedMaterial.shader));
    }

    private Color32[] Sample()
    {
        camera.Render();
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply(false);
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = previous; }
    }

    private static int Difference(Color32[] first, Color32[] second)
    {
        int count = 0;
        for (int index = 0; index < first.Length; index++)
        {
            Color32 a = first[index], b = second[index];
            if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) >= 12)
                count++;
        }
        return count;
    }

    [UnityTest]
    public IEnumerator PlayEachSubject()
    {
        string output = Environment.GetEnvironmentVariable("FLOW_SKY_OUTPUT");
        var summary = new ReplayResult { editor = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(),
            width = Width, height = Height, fps = Fps };
        surface.enabled = false;
        Color32[] baseline = Sample();
        foreach (FlowSky2DCase subject in Enum.GetValues(typeof(FlowSky2DCase)))
        {
            bool ambient = (int)subject < (int)FlowSky2DCase.WaterBolt;
            int frameCount = ambient ? 120 : 73;
            var result = new CaseResult { subject = subject.ToString(), frames = frameCount };
            string folder = string.IsNullOrEmpty(output) ? null : Path.Combine(output, ((int)subject).ToString("D2"));
            if (folder != null)
            {
                Assert.IsFalse(Directory.Exists(folder), "Use fresh output for each replay.");
                Directory.CreateDirectory(folder);
            }
            player.Play(subject);
            Assert.AreEqual(FlowSky2DStage.Prepare, player.Stage);
            Assert.AreEqual(0, Difference(baseline, Sample()), subject + " must enter clear.");
            var seen = new HashSet<FlowSky2DStage> { player.Stage };
            Color32[] previous = baseline, first = null, midpoint = null;
            int midpointFrame = frameCount / 2;
            for (int frame = 0; frame < frameCount; frame++)
            {
                if (frame != 0) player.Advance(1f / Fps);
                Color32[] sample = Sample();
                if (frame == 0) first = sample;
                if (frame == midpointFrame) midpoint = sample;
                if (folder != null)
                    File.WriteAllBytes(Path.Combine(folder, frame.ToString("D3") + ".png"), pixels.EncodeToPNG());
                int change = Difference(previous, sample);
                if (change >= 12) result.changedFrames++;
                result.maxChangingPixels = Math.Max(change, result.maxChangingPixels);
                result.peakPixels = Math.Max(result.peakPixels, Difference(baseline, sample));
                previous = sample;
                seen.Add(player.Stage);
                if (frame % 15 == 0) yield return null;
            }
            Assert.Greater(result.peakPixels, 180, subject + " must draw a visible form.");
            Assert.Greater(result.changedFrames, frameCount / 4, subject + " must move.");
            if (ambient)
            {
                // Compare the same full-weight phase on the next loop.
                for (int step = 0; step <= midpointFrame; step++) player.Advance(1f / Fps);
                Assert.Less(Difference(midpoint, Sample()), Math.Max(32, result.peakPixels / 50), subject + " must repeat.");
                player.Stop();
                seen.Add(player.Stage);
            }
            player.Advance(1f);
            seen.Add(player.Stage);
            Assert.AreEqual(7, seen.Count, subject + " lifecycle.");
            Assert.IsFalse(surface.enabled);
            Assert.AreEqual(0, Difference(first, Sample()), subject + " must clear the draw.");

            // The same pooled instance must replay exactly and a real stop must drain its current shape.
            player.Play(subject);
            for (int step = 0; step < midpointFrame; step++) player.Advance(1f / Fps);
            Assert.AreEqual(0, Difference(midpoint, Sample()), subject + " pooled replay.");
            player.Stop();
            player.Advance(0.10f);
            Assert.AreEqual(FlowSky2DStage.Retracting, player.Stage);
            Assert.Greater(Difference(baseline, Sample()), 0, subject + " early stop lost its tail.");
            player.Advance(0.25f);
            Assert.IsFalse(surface.enabled);
            Assert.AreEqual(0, Difference(baseline, Sample()));
            summary.cases.Add(result);
        }
        if (!string.IsNullOrEmpty(output))
            File.WriteAllText(Path.Combine(output, "Replay.json"), JsonUtility.ToJson(summary, true));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (camera != null) camera.targetTexture = null;
        if (player != null) Object.Destroy(player.gameObject);
        if (rig != null) Object.Destroy(rig);
        if (pixels != null) Object.Destroy(pixels);
        if (target != null) Object.Destroy(target);
        yield return null;
        yield return new ExitPlayMode();
        EditorSettings.enterPlayModeOptionsEnabled = previousMode;
        EditorSettings.enterPlayModeOptions = previousOptions;
    }
}
