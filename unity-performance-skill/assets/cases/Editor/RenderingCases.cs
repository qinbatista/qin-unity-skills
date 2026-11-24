using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace Qin.UnityPerformance.Cases
{
    public sealed partial class RenderingCases
    {
        private const int Width = 160;
        private const int Height = 80;
        private static readonly int Impact = Shader.PropertyToID("Impact");
        private static readonly int SpawnCount = Shader.PropertyToID("spawnCount");
        private static readonly int Position = Shader.PropertyToID("position");
        private static readonly int ColorAttribute = Shader.PropertyToID("color");
        private static readonly int Size = Shader.PropertyToID("size");
        private static readonly int Lifetime = Shader.PropertyToID("lifetime");
        private GameObject _stage;
        private RenderTexture _target;
        private Texture2D _pixels;
        private bool _previousMode;
        private EnterPlayModeOptions _previousOptions;
        private int _previousCaptureRate;
        private bool _ownsPlayMode;

        private void PreparePlayMode()
        {
            _previousMode = EditorSettings.enterPlayModeOptionsEnabled;
            _previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            _ownsPlayMode = true;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator SharedImpactKeepsConcurrentPayloadsAtTheirHitPositions()
        {
            PreparePlayMode();
            yield return new EnterPlayMode(expectDomainReload: false);
            _previousCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            _stage = new GameObject("Shared impact case");
            GameObject view = new GameObject("Impact camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            view.transform.SetParent(_stage.transform, false);
            Camera camera = view.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 0.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Assert.That(_target.Create(), Is.True);
            camera.targetTexture = _target;
            _pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            VisualEffect effect = _stage.AddComponent<VisualEffect>();
            effect.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
            Assert.That(effect.visualEffectAsset, Is.Not.Null);
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            for (int frame = 0; frame < 12; frame++) yield return null;
            Assert.That(CaptureColors(camera, "impact-before"), Is.EqualTo(new int[4]));

            GameObject enemy = new GameObject("Hit target");
            enemy.transform.SetParent(_stage.transform, false);
            enemy.transform.position = new Vector3(-0.25f, 0, 0);
            using (VFXEventAttribute payload = effect.CreateVFXEventAttribute())
            {
                int hitFrame = Time.frameCount;
                payload.SetFloat(SpawnCount, 1);
                payload.SetFloat(Lifetime, 0.9f);
                payload.SetFloat(Size, 0.095f);
                SendImpact(effect, payload, enemy.transform.position, Vector3.right);
                SendImpact(effect, payload, new Vector3(0.25f, 0, 0), Vector3.up);
                SendImpact(effect, payload, new Vector3(0.485f, 0, 0), Vector3.forward);
                Assert.That(Time.frameCount, Is.EqualTo(hitFrame));
            }
            Object.Destroy(enemy);
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.That(enemy == null, Is.True);
            int[] active = CaptureColors(camera, "impact-active");
            Assert.That(active[0], Is.GreaterThan(12), "Left impact red pixels");
            Assert.That(active[1], Is.GreaterThan(12), "Right impact green pixels");
            Assert.That(active[2], Is.GreaterThan(4), "Blue impact at the right camera edge");
            Assert.That(active[3], Is.Zero, "Each color stayed at its hit position");
            Assert.That(effect.aliveParticleCount, Is.EqualTo(3));
            Assert.That(_stage.GetComponentsInChildren<VisualEffect>().Length, Is.EqualTo(1));
            camera.transform.position += Vector3.right * 0.92f;
            for (int frame = 0; frame < 3; frame++) yield return null;
            int[] panned = CaptureColors(camera, "impact-panned", blueAtLeft: true);
            Assert.That(panned[2], Is.GreaterThan(4), "Edge hit stays visible after the host origin leaves the camera");
            camera.transform.position -= Vector3.right * 0.92f;
            for (int frame = 0; frame < 110; frame++) yield return null;
            Assert.That(CaptureColors(camera, "impact-retired"), Is.EqualTo(new int[4]));
            Assert.That(effect.aliveParticleCount, Is.Zero);
            TestContext.WriteLine($"QIN_CASE One {SystemInfo.graphicsDeviceType} VFX host. Active red, green, blue pixels {active[0]}, {active[1]}, {active[2]}. Panned blue pixels {panned[2]}. All particles retired.");
        }

        private static void SendImpact(VisualEffect effect, VFXEventAttribute payload, Vector3 position, Vector3 color)
        {
            payload.SetVector3(Position, position);
            payload.SetVector3(ColorAttribute, color);
            effect.SendEvent(Impact, payload);
        }

        private int[] CaptureColors(Camera camera, string label, bool blueAtLeft = false)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                _pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                _pixels.Apply(false, false);
            }
            finally { RenderTexture.active = previous; }

            string folder = Environment.GetEnvironmentVariable("QIN_PERFORMANCE_OUTPUT");
            if (!string.IsNullOrEmpty(folder)) File.WriteAllBytes(Path.Combine(folder, label + ".png"), _pixels.EncodeToPNG());
            int[] counts = new int[4];
            Color32[] colors = _pixels.GetPixels32();
            for (int i = 0; i < colors.Length; i++)
            {
                int x = i % Width;
                int y = i / Width;
                bool inBand = y >= 25 && y <= 55;
                Color32 pixel = colors[i];
                if (pixel.r > 80 && pixel.g < 30 && pixel.b < 30) counts[inBand && x >= 25 && x < 55 ? 0 : 3]++;
                if (pixel.g > 80 && pixel.r < 30 && pixel.b < 30) counts[inBand && x >= 105 && x < 135 ? 1 : 3]++;
                if (pixel.b > 80 && pixel.r < 30 && pixel.g < 30) counts[inBand && (blueAtLeft ? x < 23 : x >= 141) ? 2 : 3]++;
            }
            return counts;
        }

        [UnityTearDown]
        public IEnumerator ReleasePlayMode()
        {
            if (!_ownsPlayMode) yield break;
            if (_stage != null) Object.Destroy(_stage);
            if (_pixels != null) Object.Destroy(_pixels);
            if (_target != null) Object.Destroy(_target);
            Time.captureFramerate = _previousCaptureRate;
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptions = _previousOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _previousMode;
            _ownsPlayMode = false;
        }

        [UnityTest]
        public IEnumerator WorldSpaceEmitterPreservesOldBurstWhenHostMoves()
        {
            PreparePlayMode();
            yield return new EnterPlayMode(expectDomainReload: false);
            _previousCaptureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            _stage = new GameObject("Reusable particle emitter");
            ParticleSystem system = _stage.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 2;
            main.startSpeed = 0;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            system.Play(false);
            yield return null;

            ParticleSystem.EmitParams hit = new ParticleSystem.EmitParams { position = Vector3.left, velocity = Vector3.zero, startLifetime = 0.5f, startColor = Color.red, startSize = 0.1f };
            system.Emit(hit, 1);
            _stage.transform.position = Vector3.right * 4;
            hit.position = Vector3.right;
            hit.startColor = Color.green;
            system.Emit(hit, 1);
            yield return null;
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[2];
            Assert.That(system.GetParticles(particles), Is.EqualTo(2));
            foreach (ParticleSystem.Particle particle in particles)
            {
                bool red = particle.startColor.r > 200;
                Assert.That(red || particle.startColor.g > 200, Is.True);
                Assert.That(particle.position.x, Is.EqualTo(red ? -1f : 1f).Within(0.001f));
            }
            Assert.That(particles[0].startColor, Is.Not.EqualTo(particles[1].startColor));
            for (int frame = 0; frame < 45; frame++) yield return null;
            Assert.That(system.particleCount, Is.Zero);
            system.Emit(hit, 1);
            Assert.That(system.particleCount, Is.EqualTo(1));
            TestContext.WriteLine("QIN_CASE One world-space ParticleSystem retained two colored positions across host movement, expired both particles and accepted a new burst.");
        }

        [Test]
        public void CombinedShaderPreservesOverlappingCuesWithFewerSubmittedDraws()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/PerformanceCases/Rendering/ImpactLayers.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            Material material = new Material(shader);
            RenderTexture target = new RenderTexture(96, 96, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Texture2D pixels = new Texture2D(96, 96, TextureFormat.RGBAFloat, false, true);
            try
            {
                Assert.That(target.Create(), Is.True);
                Color[] before = RenderLayers(material, target, pixels, new[] { "CoreOnly", "RingOnly" }, out int beforeDraws);
                Color[] after = RenderLayers(material, target, pixels, new[] { "Combined" }, out int afterDraws);
                float greatestError = 0;
                int overlapPixels = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    if (before[i].r > 0.15f && before[i].g > 0.3f) overlapPixels++;
                    Color difference = before[i] - after[i];
                    greatestError = Mathf.Max(greatestError, Mathf.Abs(difference.r), Mathf.Abs(difference.g));
                    Assert.That(after[i].b, Is.EqualTo(before[i].b).Within(0.0001f));
                    Assert.That(after[i].a, Is.EqualTo(before[i].a).Within(0.0001f));
                }
                Assert.That(overlapPixels, Is.GreaterThan(30), "Several distinct additive contributions must cover the same pixels");
                Assert.That(greatestError, Is.LessThan(0.001f));
                Assert.That(beforeDraws, Is.EqualTo(16));
                Assert.That(afterDraws, Is.EqualTo(8));
                TestContext.WriteLine($"QIN_CASE {SystemInfo.graphicsDeviceType}: {overlapPixels} overlapping pixels, maximum RGB difference {greatestError}, {beforeDraws} vs {afterDraws} submitted draws. GPU duration unmeasured.");
            }
            finally
            {
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
            }
        }

        private static Color[] RenderLayers(Material material, RenderTexture target, Texture2D pixels, string[] passNames, out int submittedDraws)
        {
            submittedDraws = 0;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            using (CommandBuffer draws = new CommandBuffer())
            {
                draws.SetRenderTarget(target);
                draws.ClearRenderTarget(false, true, Color.black);
                for (int layer = 0; layer < 8; layer++)
                {
                    float angle = layer * Mathf.PI / 4;
                    properties.SetVector("_ImpactCenter", new Vector4(0.5f + 0.08f * Mathf.Cos(angle), 0.5f + 0.08f * Mathf.Sin(angle), 0, 0));
                    foreach (string passName in passNames)
                    {
                        int pass = material.FindPass(passName);
                        Assert.That(pass, Is.GreaterThanOrEqualTo(0), passName);
                        draws.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, properties);
                        submittedDraws++;
                    }
                }
                Graphics.ExecuteCommandBuffer(draws);
            }
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                pixels.Apply(false, false);
                return pixels.GetPixels();
            }
            finally { RenderTexture.active = previous; }
        }
    }
}
