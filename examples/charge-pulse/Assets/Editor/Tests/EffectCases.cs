using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class EffectCases
    {
        [TestCase(0f, 0, 0)]
        [TestCase(0.5f, 126, 129)]
        [TestCase(1f, 255, 255)]
        public void Effects04_IndependentLineCoverageIgnoresHiddenBodyRgb(float lineAlpha, int expectedMin, int expectedMax)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prefabs/ChargePulse/PulseComposite.shader");
            Assert.That(shader.isSupported, Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            Material material = new Material(shader);
            Texture2D body = Pixel(new Color(1f, 0f, 0f, 0f));
            Texture2D line = Pixel(new Color(0f, 1f, 0f, lineAlpha));
            try
            {
                material.SetTexture("_LineTex", line);
                using NativeReadback image = Render(body, material);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
                Color32 center = image.CenterPixel;
                Assert.That(center.a, Is.InRange(expectedMin, expectedMax));
                Assert.That(center.r, Is.Zero);
                Assert.That(center.g, Is.InRange(expectedMin, expectedMax));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(line);
            }
        }

        [Test]
        public void Effects05_OpeningTheArcChangesCoverageInsteadOfDimmingTheWholeShape()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Prefabs/ChargePulse/PulseComposite.shader");
            Material material = new Material(shader);
            Texture2D empty = Pixel(Color.clear);
            Texture2D line = Pixel(Color.white);
            try
            {
                material.SetTexture("_LineTex", line);
                int previous = 0;
                foreach (float sweep in new[] { 0.2f, 0.5f, 0.8f })
                {
                    material.SetFloat("_Sweep", sweep);
                    using NativeReadback frame = Render(empty, material);
                    Assert.That(frame.CoveredPixels, Is.GreaterThan(previous));
                    Assert.That(frame.BrightPixels, Is.EqualTo(frame.CoveredPixels), "The formed area has full intensity in every phase.");
                    previous = frame.CoveredPixels;
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(empty);
                Object.DestroyImmediate(line);
            }
        }

        [Test]
        public void Effects07_EditModeReadbackReleasesItsImageAndPreservesItsBorrowedTarget()
        {
            RenderTexture target = RenderTexture.GetTemporary(8, 8);
            RenderTexture previous = RenderTexture.active;
            Texture2D image;
            try
            {
                using (NativeReadback readback = new NativeReadback(target))
                {
                    image = readback.CapturedTexture;
                    Assert.That(image != null, Is.True);
                    Assert.That(RenderTexture.active, Is.SameAs(previous));
                }
                Assert.That(image == null, Is.True, "The owned Edit Mode image must be destroyed during disposal.");
                Assert.That(target.IsCreated(), Is.True, "The caller still owns the render target.");
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }

        [Test]
        public void Effects08_SharedEmitterKeepsOverlappingImpactsAtTheirWorldPositions()
        {
            GameObject host = new GameObject("Impact emitter");
            try
            {
                ParticleSystem emitter = host.AddComponent<ParticleSystem>();
                emitter.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = emitter.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ParticleSystem.EmissionModule emission = emitter.emission;
                emission.rateOverTime = 0f;
                emitter.Play(false);

                Vector3 left = new Vector3(-3f, 2f, 0f);
                Vector3 right = new Vector3(4f, -1f, 0f);
                ParticleSystem.EmitParams burst = new ParticleSystem.EmitParams
                {
                    position = left,
                    velocity = Vector3.zero,
                    startLifetime = 0.125f
                };
                emitter.Emit(burst, 2);
                burst.position = right;
                emitter.Emit(burst, 1);

                host.transform.position = new Vector3(12f, 5f, 0f);
                emitter.Simulate(0.03125f, withChildren: false, restart: false, fixedTimeStep: false);
                ParticleSystem.Particle[] particles = new ParticleSystem.Particle[4];
                int count = emitter.GetParticles(particles);
                Assert.That(count, Is.EqualTo(3));
                Vector3[] worldPositions = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    Vector3 position = particles[i].position;
                    worldPositions[i] = main.simulationSpace == ParticleSystemSimulationSpace.Local
                        ? host.transform.TransformPoint(position)
                        : position;
                }
                Assert.That(worldPositions, Is.EquivalentTo(new[] { left, left, right }));

                emitter.Simulate(0.25f, withChildren: false, restart: false, fixedTimeStep: false);
                Assert.That(emitter.GetParticles(particles), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Texture2D Pixel(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static NativeReadback Render(Texture2D body, Material material)
        {
            RenderTexture target = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(false, true, Color.clear);
                Graphics.Blit(body, target, material, 0);
                return new NativeReadback(target);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
