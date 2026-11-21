using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class CodeCases
    {
        private struct MotionSample
        {
            public float Position;
            public float Speed;
        }

        private struct AdvanceJob : IJob
        {
            public NativeArray<MotionSample> Samples;
            public float Seconds;

            public void Execute()
            {
                for (int index = 0; index < Samples.Length; index++)
                {
                    MotionSample sample = Samples[index];
                    sample.Position += sample.Speed * Seconds;
                    Samples[index] = sample;
                }
            }
        }

        [Test]
        public void Code04_OneJobUsesTheSameStorageUntilItsLastConsumerFinishes()
        {
            MotionSample[] input = { new MotionSample { Speed = 2f }, new MotionSample { Position = 10f, Speed = -3f } };
            using NativeArray<MotionSample> samples = new NativeArray<MotionSample>(input, Allocator.Persistent);
            AdvanceJob job = new AdvanceJob { Samples = samples, Seconds = 0.5f };

            JobHandle pending = job.Schedule();
            pending.Complete();
            Assert.That(samples[0].Position, Is.EqualTo(1f));
            Assert.That(samples[1].Position, Is.EqualTo(8.5f));

            pending = job.Schedule();
            pending.Complete();
            Assert.That(samples[0].Position, Is.EqualTo(2f));
            Assert.That(samples[1].Position, Is.EqualTo(7f));

            Type[] slotContainers = typeof(AdvanceJob).GetFields()
                .Select(field => field.FieldType)
                .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(NativeArray<>))
                .ToArray();
            CollectionAssert.AreEqual(new[] { typeof(NativeArray<MotionSample>) }, slotContainers);
        }

        [Test]
        public void Code05_AWarmTypedScanReusesCapacityAndAvoidsMeasuredGcAllocation()
        {
            int[] samples = { -2, 1, 4, 0, 6 };
            List<int> owned = new List<int>(samples.Length);
            Filter(samples, 0, owned);
            GC.KeepAlive(AllocatingScan(0));
            using ProfilerRecorder recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1024, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid, Is.True);

            recorder.Start();
            recorder.Stop();
            int idle = recorder.Count;
            recorder.Reset();
            recorder.Start();
            GC.KeepAlive(new byte[64]);
            recorder.Stop();
            int deliberate = recorder.Count;
            Assert.That(idle, Is.Zero, "The idle probe must stay quiet.");
            Assert.That(deliberate, Is.GreaterThan(0), "A silent positive control invalidates a zero-allocation claim.");

            int reusedChecksum = 0;
            recorder.Reset();
            recorder.Start();
            for (int iteration = 0; iteration < 64; iteration++)
            {
                Filter(samples, iteration % 2, owned);
                reusedChecksum += owned.Count;
            }
            recorder.Stop();
            int reused = recorder.Count;

            int transientChecksum = 0;
            recorder.Reset();
            recorder.Start();
            for (int iteration = 0; iteration < 64; iteration++)
            {
                int[] temporary = AllocatingScan(iteration % 2);
                transientChecksum += temporary.Length;
            }
            recorder.Stop();
            int transient = recorder.Count;

            CollectionAssert.AreEqual(new[] { 4, 6 }, owned);
            Assert.That(reusedChecksum, Is.EqualTo(transientChecksum));
            Assert.That(reused, Is.Zero);
            Assert.That(transient, Is.InRange(1, recorder.Capacity - 1));
            TestContext.WriteLine($"GC.Alloc samples: idle {idle}, deliberate {deliberate}, reuse {reused}, transient {transient} for 64 matched calls.");

            int[] AllocatingScan(int threshold) => samples.Where(sample => sample > threshold).ToArray();
        }

        [Test]
        public void Code06_MaterialPoolRejectsExhaustionAndResetsItsExactInstance()
        {
            Shader shader = Shader.Find("Qin Samples/Pulse Glow");
            Assert.That(shader, Is.Not.Null);
            Material shared = new Material(shader);
            int tint = Shader.PropertyToID("_Tint");
            shared.SetColor(tint, Color.white);
            Material active = null;
            try
            {
                using ObjectPool<Material> pool = new ObjectPool<Material>(() => new Material(shared),
                    actionOnRelease: material => material.SetColor(tint, shared.GetColor(tint)),
                    actionOnDestroy: Object.DestroyImmediate, defaultCapacity: 1, maxSize: 1);

                active = pool.CountActive < 1 ? pool.Get() : null;
                active.SetColor(tint, Color.red);
                Material denied = pool.CountActive < 1 ? pool.Get() : null;
                Assert.That(denied, Is.Null, "maxSize alone only bounds retained entries.");
                Assert.That(shared.GetColor(tint), Is.EqualTo(Color.white));
                pool.Release(active);

                Material reused = pool.Get();
                Assert.That(reused, Is.SameAs(active));
                Assert.That(reused.GetColor(tint), Is.EqualTo(Color.white));
                pool.Release(reused);
            }
            finally
            {
                if (active != null) Object.DestroyImmediate(active);
                Object.DestroyImmediate(shared);
            }
        }

        [Test]
        public async Task Code07_AClosedRequestReleasesLateResultsWithoutChangingCurrentState()
        {
            using CancellationTokenSource first = new CancellationTokenSource();
            using CancellationTokenSource second = new CancellationTokenSource();
            TaskCompletionSource<NativeArray<int>> oldRead = new TaskCompletionSource<NativeArray<int>>();
            TaskCompletionSource<NativeArray<int>> newRead = new TaskCompletionSource<NativeArray<int>>();
            NativeArray<int> current = default;
            int discarded = 0;
            try
            {
                Task previous = Publish(oldRead.Task, first.Token);
                first.Cancel();
                Task next = Publish(newRead.Task, second.Token);
                newRead.SetResult(new NativeArray<int>(new[] { 42 }, Allocator.Persistent));
                await next;

                oldRead.SetResult(new NativeArray<int>(new[] { 9 }, Allocator.Persistent));
                await previous;
                Assert.That(current[0], Is.EqualTo(42));
                Assert.That(discarded, Is.EqualTo(1));
            }
            finally
            {
                if (current.IsCreated) current.Dispose();
            }

            async Task Publish(Task<NativeArray<int>> read, CancellationToken lifetime)
            {
                NativeArray<int> result = await read;
                if (lifetime.IsCancellationRequested)
                {
                    result.Dispose();
                    discarded++;
                    return;
                }

                if (current.IsCreated) current.Dispose();
                current = result;
            }
        }

        [Test]
        public void Code08_AJsonPromptPreservesValueAndEscapingThroughARealParser()
        {
            const string answer = "Ring \"A\"\nblue";
            PromptValue value = new PromptValue { answer = answer };
            string promptBody = JsonUtility.ToJson(value);
            PromptValue decoded = JsonUtility.FromJson<PromptValue>(promptBody);
            Assert.That(decoded.answer, Is.EqualTo(answer));
        }

        private static void Filter(int[] source, int threshold, List<int> result)
        {
            result.Clear();
            foreach (int sample in source)
                if (sample > threshold) result.Add(sample);
        }

        [Serializable]
        private sealed class PromptValue
        {
            public string answer;
        }
    }
}
