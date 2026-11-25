using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Qin.UnityEffects.Fluids;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class QinFluidReplay
{
    [Serializable]
    private sealed class WaterCastCosts
    {
        public int casts;
        public int solverOwners;
        public int particleBuffers;
        public int filmTextures;
        public long idleSolverSteps;
    }

    [Serializable]
    private sealed class WaterReuseResult
    {
        public WaterCastCosts cold = new();
        public WaterCastCosts warm = new();
        public string gpuStateSha256;
        public bool independentPeerRetirement;
    }

    [UnityTest]
    public IEnumerator ReuseRockImpact()
    {
        string output = ApplicationCapture("RockImpactReuse");
        WaterReuseResult result = new();
        byte[] reference = null;
        foreach (WaterCastCosts costs in new[] { result.cold, result.warm })
        {
            bool cold = costs == result.cold;
            FluidMixController water = OpenVariant("RockImpact", Vector3.zero);
            HashSet<object> solvers = new();
            HashSet<GraphicsBuffer> buffers = new();
            HashSet<RenderTexture> films = new();
            for (int cast = 0; cast < 4; cast++)
            {
                if (cold && cast > 0) water.gameObject.SetActive(true);
                water.BeginCast(Seed);
                object solver = Simulation.GetValue(water);
                Assert.That(solver, Is.Not.Null);
                Type type = solver.GetType();
                var particles = (GraphicsBuffer)type.GetProperty("Particles", Members).GetValue(solver);
                var film = (RenderTexture)type.GetProperty("Film", Members).GetValue(solver);
                var step = type.GetProperty("Step", Members);
                solvers.Add(solver);
                buffers.Add(particles);
                films.Add(film);
                Assert.That(particles.IsValid() && film.IsCreated(), Is.True);
                byte[] initial = State(water);
                water.SetCastTime(.4f);
                byte[] advanced = State(water);
                Assert.That(advanced.SequenceEqual(initial), Is.False, "The cast must change the GPU field.");
                reference ??= advanced;
                Assert.That(advanced.SequenceEqual(reference), Is.True, "The same seed and time must reproduce every parcel and wave.");
                costs.casts++;

                water.EndCast();
                water.SetCastTime(.75f);
                Assert.That(water.CurrentStage, Is.EqualTo(FluidMixController.VisualStage.Recycled));
                Assert.That(water.GetComponentsInChildren<MeshRenderer>().All(draw => !draw.enabled), Is.True);
                long parked = (long)step.GetValue(solver);
                Assert.That(parked, Is.GreaterThan(0));
                for (int frame = 0; frame < 3; frame++)
                {
                    water.SetCastTime(1f + frame);
                    yield return null;
                }
                Assert.That((long)step.GetValue(solver), Is.EqualTo(parked));
                costs.idleSolverSteps += (long)step.GetValue(solver) - parked;

                if (cold || cast == 3)
                {
                    water.gameObject.SetActive(false);
                    Assert.That(Simulation.GetValue(water), Is.Null);
                    Assert.That(particles.IsValid(), Is.False);
                    Assert.That(film.IsCreated(), Is.False);
                }
            }
            costs.solverOwners = solvers.Count;
            costs.particleBuffers = buffers.Count;
            costs.filmTextures = films.Count;
            int expected = cold ? costs.casts : 1;
            Assert.That(costs.solverOwners, Is.EqualTo(expected));
            Assert.That(costs.particleBuffers, Is.EqualTo(expected));
            Assert.That(costs.filmTextures, Is.EqualTo(expected));
        }
        FluidMixController survivor = OpenVariant("RockImpact", Vector3.zero);
        FluidMixController peer = OpenVariant("RockImpact", Vector3.right);
        survivor.BeginCast(Seed);
        survivor.SetCastTime(.4f);
        peer.BeginCast(Seed);
        peer.SetCastTime(.2f);
        Assert.That(Simulation.GetValue(survivor), Is.Not.SameAs(Simulation.GetValue(peer)));
        Assert.That(State(peer).SequenceEqual(reference), Is.False);
        peer.gameObject.SetActive(false);
        Assert.That(State(survivor).SequenceEqual(reference), Is.True, "Retiring the overlapping peer must preserve the live water.");
        survivor.SetCastTime(.5f);
        Assert.That(State(survivor).SequenceEqual(reference), Is.False, "The surviving cast must keep advancing.");
        survivor.gameObject.SetActive(false);
        result.independentPeerRetirement = true;
        using var hash = SHA256.Create();
        result.gpuStateSha256 = BitConverter.ToString(hash.ComputeHash(reference)).Replace("-", "").ToLowerInvariant();
        File.WriteAllText(Path.Combine(output, "costs.json"), JsonUtility.ToJson(result, true));
    }
}
