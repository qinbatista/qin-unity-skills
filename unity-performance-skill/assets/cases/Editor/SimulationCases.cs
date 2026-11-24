using System.Collections.Generic;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.U2D.Physics;
using UnityEngine;
using Dots = Unity.Physics;
using Object = UnityEngine.Object;

namespace Qin.UnityPerformance.Cases
{
    public struct PerformanceActor : IComponentData
    {
        public int Id;
        public int X;
        public int ManagedExecution;
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct MovePerformanceActors : IJobChunk
    {
        public ComponentTypeHandle<PerformanceActor> Actors;
        public int PlayerX;
        public bool FollowPlayer;

        [BurstDiscard]
        private static void MarkManaged(ref int value) => value = 1;

        public void Execute(in ArchetypeChunk chunk, int chunkIndex, bool useEnabledMask, in v128 mask)
        {
            int managed = 0;
            MarkManaged(ref managed);
            NativeArray<PerformanceActor> actors = chunk.GetNativeArray(ref Actors);
            for (int i = 0; i < actors.Length; i++)
            {
                PerformanceActor actor = actors[i];
                actor.X += FollowPlayer ? math.clamp(PlayerX - actor.X, -1, 1) : 1 + actor.Id % 3;
                actor.ManagedExecution = managed;
                actors[i] = actor;
            }
        }
    }

    public sealed class SimulationCases
    {
        [Test]
        public void SmallAuthoredActorsKeepIndependentPresentation()
        {
            GameObject group = new GameObject("Two actors");
            try
            {
                GameObject leader = new GameObject("Leader", typeof(Animator), typeof(CircleCollider2D));
                GameObject companion = new GameObject("Companion", typeof(Animator), typeof(CircleCollider2D));
                leader.transform.SetParent(group.transform);
                companion.transform.SetParent(group.transform);
                Transform[] active = { leader.transform, companion.transform };
                companion.transform.position = new Vector3(2f, 1f, 0f);
                companion.GetComponent<Animator>().enabled = false;
                foreach (Transform actor in active)
                    actor.position += Vector3.right;
                Assert.That(leader.transform.position, Is.EqualTo(Vector3.right));
                Assert.That(companion.transform.position, Is.EqualTo(new Vector3(3f, 1f, 0f)));
                Assert.That(leader.GetComponent<Animator>().enabled, Is.True);
                Assert.That(companion.GetComponent<CircleCollider2D>().enabled, Is.True);
                companion.SetActive(false);
                Assert.That(leader.activeInHierarchy, Is.True);
            }
            finally { Object.DestroyImmediate(group); }
        }

        [Test]
        public void ChunkJobMatchesIndependentAndSharedTargetMovement()
        {
            const int population = 3000;
            using World world = new World("Actor simulation");
            EntityManager manager = world.EntityManager;
            EntityArchetype archetype = manager.CreateArchetype(typeof(PerformanceActor));
            using NativeArray<Entity> actors = manager.CreateEntity(archetype, population, Allocator.Temp);
            using EntityQuery query = manager.CreateEntityQuery(typeof(PerformanceActor));
            int[] expected = new int[population];
            for (int i = 0; i < population; i++)
            {
                expected[i] = i % 32;
                manager.SetComponentData(actors[i], new PerformanceActor { Id = i, X = expected[i], ManagedExecution = -1 });
            }

            GameObject player = new GameObject("Player");
            GameObject character = new GameObject("Rendered character", typeof(Animator), typeof(SpriteRenderer));
            try
            {
                player.transform.position = Vector3.right * 12f;
                for (int tick = 0; tick < 3; tick++)
                {
                    bool followsPlayer = tick > 0;
                    int snapshot = followsPlayer ? Mathf.RoundToInt(player.transform.position.x) : 0;
                    MovePerformanceActors job = new MovePerformanceActors
                    {
                        Actors = manager.GetComponentTypeHandle<PerformanceActor>(false),
                        PlayerX = snapshot,
                        FollowPlayer = followsPlayer
                    };
                    JobHandle scheduled = job.ScheduleParallel(query, default);
                    if (followsPlayer)
                        player.transform.position = Vector3.right * (snapshot == 12 ? 24f : 12f);
                    scheduled.Complete();
                    using NativeArray<PerformanceActor> output = query.ToComponentDataArray<PerformanceActor>(Allocator.Temp);
                    Assert.That(output.Length, Is.EqualTo(population));
                    foreach (PerformanceActor actor in output)
                    {
                        int x = expected[actor.Id];
                        expected[actor.Id] = followsPlayer ? (int)Mathf.MoveTowards(x, snapshot, 1) : x + 1 + actor.Id % 3;
                        Assert.That(actor.X, Is.EqualTo(expected[actor.Id]), $"Actor {actor.Id}, tick {tick}");
                        Assert.That(actor.ManagedExecution, Is.Zero, "The scheduled Job must execute with Burst.");
                    }
                    // A hidden character keeps simulating and resumes presentation from the completed result.
                    if (tick == 1)
                        character.SetActive(false);
                    if (tick == 2)
                        character.SetActive(true);
                    if (character.activeSelf)
                        character.transform.position = Vector3.right * manager.GetComponentData<PerformanceActor>(actors[0]).X;
                    Assert.That(character.transform.position.x, Is.EqualTo(tick == 1 ? 1 : expected[0]));
                }
                int chunks = query.CalculateChunkCount();
                Assert.That(chunks, Is.GreaterThan(1));
                TestContext.WriteLine($"QIN_CASE Actors={population}, chunks={chunks}, ticks=3, player snapshots=2 follower ticks.");
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(player);
            }
        }

        // Movement over a single tick. Both objects' velocities contribute to closest approach.
        private readonly struct Sweep
        {
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public Sweep(Vector2 start, Vector2 end) { Start = start; End = end; }
            public bool Touches(Sweep other, float radius)
            {
                Vector2 separation = Start - other.Start;
                Vector2 delta = End - other.End - separation;
                float t = delta.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(-Vector2.Dot(separation, delta) / delta.sqrMagnitude);
                return (separation + delta * t).sqrMagnitude <= 4f * radius * radius;
            }
        }

        [Test]
        public void SweptGridMatchesAllPairsAcrossCellBoundaries()
        {
            const float radius = 0.1f;
            List<Sweep> actors = new List<Sweep>
            {
                new Sweep(new Vector2(-4, 2), new Vector2(4, 2)),
                new Sweep(new Vector2(4, 2), new Vector2(-4, 2)),
                new Sweep(new Vector2(-0.05f, 0), new Vector2(-0.05f, 0)),
                new Sweep(new Vector2(0.05f, 0), new Vector2(0.05f, 0)),
                new Sweep(new Vector2(0.8f, 0.7f), new Vector2(0.8f, 0.7f))
            };
            for (int i = 0; i < 48; i++)
            {
                Vector2 p = i < 12 ? new Vector2(5f + i * 0.01f, 8f) : new Vector2(i * 2f, 4f);
                actors.Add(new Sweep(p, p));
            }
            HashSet<(int, int)> expected = new HashSet<(int, int)>();
            for (int a = 0; a < actors.Count; a++)
                for (int b = a + 1; b < actors.Count; b++)
                    if (actors[a].Touches(actors[b], radius))
                        expected.Add((a, b));

            Dictionary<Vector2Int, List<int>> cells = new Dictionary<Vector2Int, List<int>>();
            HashSet<(int, int)> candidates = new HashSet<(int, int)>();
            for (int b = 0; b < actors.Count; b++)
            {
                Sweep actor = actors[b];
                Vector2Int min = Vector2Int.FloorToInt(Vector2.Min(actor.Start, actor.End) - radius * Vector2.one);
                Vector2Int max = Vector2Int.FloorToInt(Vector2.Max(actor.Start, actor.End) + radius * Vector2.one);
                for (int x = min.x; x <= max.x; x++)
                    for (int y = min.y; y <= max.y; y++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        if (!cells.TryGetValue(cell, out List<int> members))
                            cells[cell] = members = new List<int>();
                        foreach (int a in members)
                            candidates.Add((a, b));
                        members.Add(b);
                    }
            }
            HashSet<(int, int)> hits = new HashSet<(int, int)>();
            foreach ((int a, int b) in candidates)
                if (actors[a].Touches(actors[b], radius))
                    hits.Add((a, b));
            int allPairs = actors.Count * (actors.Count - 1) / 2;
            Assert.That(expected, Does.Contain((0, 1)), "Opposite moving actors cross between ticks.");
            Assert.That(expected, Does.Contain((2, 3)), "Negative and positive adjacent cells share a contact.");
            Assert.That(candidates, Does.Contain((3, 4)), "The grid admits a nearby actor in the same cell.");
            Assert.That(hits.Contains((3, 4)), Is.False, "The narrow phase rejects that false positive.");
            Assert.That(expected.Count, Is.EqualTo(68), "Twelve coincident neighbors contribute 66 distinct pairs.");
            Assert.That(hits, Is.EquivalentTo(expected));
            Assert.That(candidates.Count, Is.LessThan(allPairs));
            TestContext.WriteLine($"QIN_CASE All pairs={allPairs}, candidates={candidates.Count}, confirmed={hits.Count}.");
        }

        [Test]
        public void ComponentAndLowLevelWorldsResolveTheirOwnContacts()
        {
            PhysicsWorldDefinition settings = PhysicsWorldDefinition.defaultDefinition;
            settings.gravity = new Vector2(0f, -9.81f);
            PhysicsWorld low = default;
            SimulationMode2D previousMode = Physics2D.simulationMode;
            GameObject floor = null;
            GameObject ball = null;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                low = PhysicsWorld.Create(settings);
                // The casebook receiver owns its empty scene. Only the two component bodies use it.
                Vector2 floorCenter = new Vector2(-10, 0);
                floor = new GameObject("Floor");
                floor.transform.position = floorCenter;
                BoxCollider2D floorShape = floor.AddComponent<BoxCollider2D>();
                floorShape.size = new Vector2(5, 1);
                ball = new GameObject("Ball");
                ball.transform.position = floorCenter + Vector2.up * 2;
                Rigidbody2D body = ball.AddComponent<Rigidbody2D>();
                body.gravityScale = 0;
                body.linearVelocity = Vector2.down * 2f;
                CircleCollider2D ballShape = ball.AddComponent<CircleCollider2D>();
                ballShape.radius = 0.5f;
                Physics2D.SyncTransforms();
                PhysicsScene2D classic = Physics2D.defaultPhysicsScene;
                for (int i = 0; i < 90; i++)
                    classic.Simulate(1f / 60f);
                Assert.That(ballShape.IsTouching(floorShape), Is.True);
                Assert.That(body.position.y, Is.EqualTo(1f).Within(0.04f));

                Vector2 lowOrigin = new Vector2(10, 0);
                PhysicsShape support = low.CreateBody().CreateShape(CircleGeometry.Create(1f, lowOrigin));
                PhysicsBodyDefinition definition = PhysicsBodyDefinition.defaultDefinition;
                definition.type = PhysicsBody.BodyType.Dynamic;
                definition.position = lowOrigin + Vector2.up * 1.4f;
                PhysicsBody lowBall = low.CreateBody(definition);
                PhysicsShape shell = lowBall.CreateShape(CircleGeometry.Create(0.5f));
                for (int i = 0; i < 30; i++)
                    low.Simulate(1f / 60f);
                using NativeArray<PhysicsShape.Contact> contacts = shell.GetContacts(Allocator.Temp);
                Assert.That(contacts.Length, Is.EqualTo(1));
                PhysicsShape.Contact contact = contacts[0];
                Assert.That(contact.shapeA.Equals(support) || contact.shapeB.Equals(support), Is.True);
                Assert.That(contact.manifold.pointCount, Is.GreaterThan(0));
                Assert.That(classic.OverlapPoint(lowOrigin), Is.Null);
                Assert.That(classic.OverlapPoint(floorCenter), Is.EqualTo(floorShape));
                using var classicFloorInLowWorld = low.OverlapPoint(floorCenter, PhysicsQuery.QueryFilter.Everything, Allocator.Temp);
                Assert.That(classicFloorInLowWorld.Length, Is.Zero);
                using var ownFloor = low.OverlapPoint(lowOrigin, PhysicsQuery.QueryFilter.Everything, Allocator.Temp);
                Assert.That(ownFloor[0].shape, Is.EqualTo(support));
            }
            finally
            {
                try
                {
                    Object.DestroyImmediate(ball);
                    Object.DestroyImmediate(floor);
                    if (low.isValid)
                        low.Destroy();
                }
                finally { Physics2D.simulationMode = previousMode; }
            }
        }

        [Test]
        public void PlanarDotsColliderRequiresMatchingDepth()
        {
            Dots.BoxGeometry box = new Dots.BoxGeometry
            {
                Center = float3.zero, Size = new float3(2f, 1f, 0.2f), Orientation = quaternion.identity
            };
            using BlobAssetReference<Dots.Collider> obstacle = Dots.BoxCollider.Create(box);
            Dots.RaycastInput path = new Dots.RaycastInput
            {
                Start = new float3(-2f, 0f, 0f), End = new float3(2f, 0f, 0f), Filter = Dots.CollisionFilter.Default
            };
            Assert.That(obstacle.Value.CastRay(path, out Dots.RaycastHit hit), Is.True);
            Assert.That(hit.Fraction, Is.EqualTo(0.25f).Within(0.001f));
            path.Start = new float3(-2f, 0f, 0.5f);
            path.End = new float3(2f, 0f, 0.5f);
            Assert.That(obstacle.Value.CastRay(path), Is.False, "Same XY path outside the gameplay plane.");
        }

        private sealed class Wallet
        {
            public Transform Target;
            public int Coins;
        }

        private sealed class Pickup
        {
            private readonly Transform view;
            private readonly AnimationCurve flight = AnimationCurve.EaseInOut(0, 0, 1, 1);
            private Vector3 origin;
            private int amount;
            private int generation;
            private Wallet claimant;

            public Pickup(Transform view) => this.view = view;
            public void Drop(Vector3 ground, int value)
            {
                generation++;
                origin = ground;
                view.position = ground;
                amount = value;
                claimant = null;
            }
            public int Claim(Wallet wallet)
            {
                if (claimant != null || amount == 0 || wallet.Target == null || (wallet.Target.position - origin).sqrMagnitude > 4f)
                    return 0;
                claimant = wallet;
                return ++generation;
            }
            public void Advance(int claim, float fraction)
            {
                if (claim != generation || claimant == null)
                    return;
                if (claimant.Target == null)
                {
                    Drop(origin, amount);
                    return;
                }
                view.position = Vector3.Lerp(origin, claimant.Target.position, flight.Evaluate(fraction));
                if (fraction >= 1f)
                {
                    claimant.Coins += amount;
                    amount = 0;
                    claimant = null;
                }
            }
        }

        [Test]
        public void PickupReservationSurvivesTargetLossAndSlotReuse()
        {
            GameObject view = new GameObject("Pickup", typeof(SpriteRenderer));
            GameObject first = new GameObject("First claimant");
            GameObject second = new GameObject("Second claimant");
            GameObject observer = new GameObject("Pickup camera", typeof(Camera));
            try
            {
                Vector3 ground = new Vector3(100, 50, 0);
                Camera camera = observer.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.aspect = 1;
                observer.transform.position = Vector3.back * 10;
                Assert.That(camera.WorldToViewportPoint(ground).x, Is.GreaterThan(1f));
                first.transform.position = ground + Vector3.right;
                second.transform.position = ground + Vector3.left;
                Wallet a = new Wallet { Target = first.transform };
                Wallet b = new Wallet { Target = second.transform };
                Assert.That(view.GetComponent<Collider2D>(), Is.Null);
                Assert.That(view.GetComponent<Rigidbody2D>(), Is.Null);
                Pickup item = new Pickup(view.transform);
                item.Drop(ground, 5);
                int lost = item.Claim(a);
                Assert.That(lost, Is.Positive);
                Assert.That(item.Claim(b), Is.Zero);
                item.Advance(lost, 0.5f);
                Assert.That(view.transform.position.x, Is.EqualTo(ground.x + 0.5f).Within(0.001f));
                Object.DestroyImmediate(first);
                item.Advance(lost, 1);
                Assert.That(a.Coins, Is.Zero);
                Assert.That(view.transform.position, Is.EqualTo(ground));
                int retired = item.Claim(b);
                Assert.That(retired, Is.Positive);
                item.Advance(retired, 0.5f);
                item.Drop(ground, 7);
                int current = item.Claim(b);
                Assert.That(current, Is.GreaterThan(retired));
                item.Advance(retired, 1);
                Assert.That(b.Coins, Is.Zero);
                second.transform.position += Vector3.up;
                item.Advance(current, 1);
                item.Advance(current, 1);
                Assert.That(b.Coins, Is.EqualTo(7));
                Assert.That(view.transform.position, Is.EqualTo(second.transform.position));
                Assert.That(item.Claim(b), Is.Zero);
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(view);
                Object.DestroyImmediate(observer);
            }
        }
    }
}
