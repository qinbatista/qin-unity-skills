using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Qin.UnityPerformance.Cases
{
    public sealed class RuntimeCases
    {
        private sealed class Ammo
        {
            public int Count { get; private set; }
            public event Action<int> Changed;

            public void Set(int count)
            {
                if (count == Count) return;
                Count = count;
                Changed?.Invoke(count);
            }
        }

        [Test]
        public void HudRefreshesOnChangeAndReadsCurrentValueOnReattach()
        {
            const int frames = 60;
            Ammo ammo = new Ammo();
            int writes = 0;
            int displayed = -1;
            int pollingWrites = 0;

            void Draw(int count)
            {
                displayed = count;
                writes++;
            }

            void Attach(Ammo source)
            {
                source.Changed += Draw;
                Draw(source.Count);
            }

            ammo.Set(7);
            Attach(ammo);
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame == 10 || frame == 11) ammo.Set(6);
                pollingWrites++;
                Assert.AreEqual(ammo.Count, displayed);
            }
            Assert.AreEqual(2, writes);
            ammo.Changed -= Draw;
            ammo.Set(5);
            Assert.AreEqual(6, displayed);
            Attach(ammo);
            Assert.AreEqual(5, displayed);

            Ammo previous = ammo;
            previous.Changed -= Draw;
            ammo = new Ammo();
            ammo.Set(12);
            Attach(ammo);
            previous.Set(4);
            Assert.AreEqual(12, displayed);
            ammo.Changed -= Draw;
            TestContext.WriteLine($"QIN_CASE visible frames={pollingWrites}, continuous polling writes={pollingWrites}, event writes over the same interval=2, all attach/change writes={writes}");
        }

        [Test]
        public void ActiveSetMatchesFullScanAcrossSpawnsAndRetirements()
        {
            const int population = 128;
            SortedSet<int> active = new SortedSet<int>();
            bool[] pending = new bool[population];
            int[] baselinePositions = new int[population];
            int[] batchedPositions = new int[population];
            List<int> referenceOrder = new List<int>();
            List<int> batchOrder = new List<int>();
            int fullVisits = 0;
            int activeVisits = 0;

            for (int tick = 0; tick < 16; tick++)
            {
                int joining = (tick * 31) % population;
                active.Add(joining);
                pending[joining] = true;
                if (tick >= 3)
                {
                    int retiring = ((tick - 3) * 31) % population;
                    active.Remove(retiring);
                    pending[retiring] = false;
                }
                referenceOrder.Clear();
                batchOrder.Clear();
                for (int id = 0; id < population; id++)
                {
                    fullVisits++;
                    if (!pending[id]) continue;
                    baselinePositions[id] += tick + 1;
                    referenceOrder.Add(id);
                }
                foreach (int id in active)
                {
                    activeVisits++;
                    batchedPositions[id] += tick + 1;
                    batchOrder.Add(id);
                }
                CollectionAssert.AreEqual(referenceOrder, batchOrder);
                CollectionAssert.AreEqual(baselinePositions, batchedPositions);
            }
            Assert.AreEqual(45, activeVisits);
            Assert.AreEqual(population * 16, fullVisits);
            TestContext.WriteLine($"QIN_CASE actor slots visited: full={fullVisits}, active={activeVisits}; positions and ordered updates matched on 16 ticks");
        }

        [Test]
        public void ColdGrowthAndTransientFilteringAllocateWhileWarmReusePreservesResults()
        {
            int[] values = { -3, 2, 5, 0, 8, 13 };
            List<int> output = new List<int>();
            List<int> expected = new List<int>();
            Filter(values, 4, expected);
            using ProfilerRecorder recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1024,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.IsTrue(recorder.Valid);

            int Sample(Action operation)
            {
                recorder.Reset();
                recorder.Start();
                operation();
                recorder.Stop();
                return recorder.Count;
            }

            int idle = Sample(() => { });
            int control = Sample(() => GC.KeepAlive(new byte[64]));
            int cold = Sample(() => Filter(values, 4, output));
            CollectionAssert.AreEqual(expected, output);
            const int repeats = 64;
            int warmChecksum = 0;
            int transientChecksum = 0;
            int warm = Sample(() =>
            {
                for (int i = 0; i < repeats; i++)
                {
                    Filter(values, i % 6, output);
                    foreach (int number in output) warmChecksum += number;
                }
            });
            int transient = Sample(() =>
            {
                for (int i = 0; i < repeats; i++)
                {
                    List<int> temporary = new List<int>();
                    Filter(values, i % 6, temporary);
                    foreach (int number in temporary) transientChecksum += number;
                }
            });
            Assert.AreEqual(0, idle);
            Assert.Greater(control, 0);
            Assert.Greater(cold, 0);
            Assert.AreEqual(0, warm);
            Assert.That(transient, Is.InRange(1, recorder.Capacity - 1));
            Assert.AreEqual(transientChecksum, warmChecksum);
            CollectionAssert.AreEqual(new[] { 5, 8, 13 }, output);
            TestContext.WriteLine($"QIN_CASE main-thread GC.Alloc events: idle={idle}, control={control}, cold={cold}, warm={warm}, transient={transient}; repeated calls={repeats}, checksum={warmChecksum}");
        }

        private static void Filter(int[] values, int threshold, List<int> output)
        {
            output.Clear();
            foreach (int value in values)
                if (value > threshold) output.Add(value);
        }

        private sealed class Projectile
        {
            public GameObject View;
            public int Generation;
            public int Target;

            public void Launch(int target)
            {
                Generation++;
                Target = target;
                View.SetActive(true);
            }

            public bool Hit(int generation)
            {
                if (!View.activeSelf || generation != Generation || Target < 0) return false;
                Target = -1;
                return true;
            }

            public void Reset()
            {
                Target = -1;
                View.SetActive(false);
            }
        }

        [UnityTest]
        public IEnumerator PrewarmedProjectilesMatchRepeatedSpawnsAndRejectStaleHits()
        {
            yield return new EnterPlayMode();
            yield return ReplayProjectiles();
        }

        private static IEnumerator ReplayProjectiles()
        {
            const int bursts = 8;
            const int capacity = 2;
            GameObject fixture = new GameObject("Projectile case");
            GameObject template = new GameObject("Projectile template");
            template.transform.SetParent(fixture.transform);
            template.SetActive(false);
            int normalHits = 0;
            int pooledHits = 0;
            int normalCreations = 0;
            List<GameObject> spawned = new List<GameObject>();
            List<GameObject> pooledObjects = new List<GameObject>();
            ObjectPool<Projectile> pool = new ObjectPool<Projectile>(() =>
            {
                GameObject view = Object.Instantiate(template, fixture.transform);
                pooledObjects.Add(view);
                return new Projectile { View = view };
            }, actionOnRelease: shot => shot.Reset(), actionOnDestroy: shot => Object.Destroy(shot.View),
               defaultCapacity: capacity, maxSize: capacity);

            try
            {
                Projectile[] leases = new Projectile[capacity];
                Projectile Acquire() => pool.CountActive < capacity ? pool.Get() : null;
                for (int i = 0; i < capacity; i++) leases[i] = Acquire();
                foreach (Projectile shot in leases) pool.Release(shot);

                Projectile prior = null;
                int priorGeneration = -1;
                for (int burst = 0; burst < bursts; burst++)
                {
                    for (int i = 0; i < capacity; i++)
                    {
                        Projectile normal = new Projectile { View = Object.Instantiate(template, fixture.transform) };
                        spawned.Add(normal.View);
                        normalCreations++;
                        normal.Launch(burst * capacity + i);
                        if (normal.Hit(normal.Generation)) normalHits++;
                        Object.Destroy(normal.View);

                        leases[i] = Acquire();
                        Assert.AreEqual(-1, leases[i].Target);
                        leases[i].Launch(burst * capacity + i);
                    }
                    Assert.IsNull(Acquire(), "An extra gameplay request must report exhaustion.");
                    if (prior != null)
                    {
                        Assert.IsTrue(prior.View.activeSelf);
                        Assert.IsFalse(prior.Hit(priorGeneration));
                    }
                    prior = leases[0];
                    priorGeneration = prior.Generation;
                    foreach (Projectile shot in leases)
                    {
                        if (shot.Hit(shot.Generation)) pooledHits++;
                        Assert.IsFalse(shot.Hit(shot.Generation));
                        pool.Release(shot);
                    }
                    yield return null;
                    Assert.IsTrue(spawned[spawned.Count - 1] == null);
                    Assert.AreEqual(capacity, pool.CountInactive);
                }
                Assert.AreEqual(bursts * capacity, normalHits);
                Assert.AreEqual(normalHits, pooledHits);
                Assert.AreEqual(bursts * capacity, normalCreations);
                Assert.AreEqual(capacity, pooledObjects.Count);
            }
            finally
            {
                pool.Dispose();
                Object.Destroy(fixture);
            }
            yield return null;
            Assert.IsTrue(pooledObjects.TrueForAll(view => view == null));
            TestContext.WriteLine($"QIN_CASE bursts={bursts}, hits={pooledHits}, transient creations={normalCreations}, pooled creations={pooledObjects.Count}, exhaustion checked once per burst");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
