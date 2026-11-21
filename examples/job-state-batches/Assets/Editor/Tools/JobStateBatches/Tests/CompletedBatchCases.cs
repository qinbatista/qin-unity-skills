using System;
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.JobStateBatches.Editor
{
    public sealed class CompletedBatchCases
    {
        [UnityTest]
        public IEnumerator EnemyChaseMovesWithoutPresentationAndRetiresOnlyAtPlayer()
        {
            Open("EnemyChase");
            yield return new EnterPlayMode();
            yield return VerifyEnemyChase();
        }

        private static IEnumerator VerifyEnemyChase()
        {
            yield return Await(() => Actors<EnemyChaseController>().Length == 3, "Three enemies spawn.");

            EnemyChaseController[] actors = Actors<EnemyChaseController>();
            EnemyChaseController right = Array.Find(actors, actor => actor.transform.position.x > 2f);
            EnemyChaseController left = Array.Find(actors, actor => actor.transform.position.x < -1f);
            EnemyChaseManager manager = Object.FindAnyObjectByType<EnemyChaseManager>();
            Animator animator = right.GetComponentInChildren<Animator>();
            right.enabled = false;
            animator.SetBool("Chasing", false);
            float start = right.transform.position.x;

            FrameProbe<EnemyChaseState> probe = new FrameProbe<EnemyChaseState>(3, right.transform);
            manager.StateCompleted += probe.Observe;
            yield return Await(() => right.transform.position.x < start - 0.17f, "The detached enemy keeps moving.");
            Assert.That(animator.GetBool("Chasing"), Is.False);
            Collider2D decoy = GameObject.Find("Decoy").GetComponent<Collider2D>();
            Assert.That(left.GetComponent<Collider2D>().Distance(decoy).isOverlapped, Is.True);
            Assert.That(Actors<EnemyChaseController>(), Has.Length.EqualTo(3));

            right.enabled = true;
            yield return Await(() => animator.GetBool("Chasing") && animator.GetCurrentAnimatorStateInfo(0).IsName("Active"), "Reattaching drives the chase animation.");
            yield return Await(() => Actors<EnemyChaseController>().Length == 1, "Only the distant enemy remains after the first two player contacts.");
            Assert.That(Actors<EnemyChaseController>()[0], Is.SameAs(right));
            yield return Await(() => Actors<EnemyChaseController>().Length == 0, "The distant enemy retires at the player.");

            int completed = probe.Count;
            yield return TickFor(0.10f);
            Assert.That(probe.Count, Is.EqualTo(completed));
            manager.StateCompleted -= probe.Observe;
            probe.Check();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FireballVolleyMovesFiveDirectionsAndReplaysTheSettledState()
        {
            Open("FireballVolley");
            yield return new EnterPlayMode();
            yield return VerifyFireballVolley();
        }

        private static IEnumerator VerifyFireballVolley()
        {
            yield return Await(() => Actors<FireballVolleyController>().Length == 5, "Five fireballs launch.");
            yield return TickFor(0.14f);

            FireballVolleyController[] actors = Actors<FireballVolleyController>();
            Array.Sort(actors, (left, right) => left.transform.position.y.CompareTo(right.transform.position.y));
            FireballVolleyController far = actors[4];
            Assert.That(far.transform.position.y, Is.GreaterThan(0f), "The Job fans out the fireballs.");
            Assert.That(actors[0].transform.position.y, Is.LessThan(0f));
            GameObject visual = far.transform.Find("Visual").gameObject;
            far.enabled = false;
            float start = far.transform.position.x;

            FireballVolleyManager manager = Object.FindAnyObjectByType<FireballVolleyManager>();
            FrameProbe<FireballVolleyState> probe = new FrameProbe<FireballVolleyState>(5, far.transform);
            manager.StateCompleted += probe.Observe;
            yield return Await(() => far.transform.position.x > start + 0.2f, "The detached fireball keeps flying.");
            yield return Await(() => !actors[2].transform.Find("Visual").gameObject.activeSelf, "The subscribed fireball hides at maximum range.");

            Assert.That(visual.activeSelf, Is.True);
            for (int index = 0; index < actors.Length; index++)
            {
                Assert.That(actors[index].transform.position.magnitude, Is.EqualTo(4.8f).Within(0.025f));
                if (index > 0)
                    Assert.That(actors[index].transform.position.y, Is.GreaterThan(actors[index - 1].transform.position.y + 0.2f));
            }

            int completed = probe.Count;
            far.enabled = true;
            Assert.That(visual.activeSelf, Is.False, "Late reattachment consumes the settled result.");
            yield return TickFor(0.10f);
            Assert.That(probe.Count, Is.EqualTo(completed));
            manager.StateCompleted -= probe.Observe;
            probe.Check();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CoinMagnetAdmitsByRangeAndCollectsWithPresentationDetached()
        {
            Open("CoinMagnet");
            yield return new EnterPlayMode();
            yield return VerifyCoinMagnet();
        }

        private static IEnumerator VerifyCoinMagnet()
        {
            yield return Await(() => Actors<CoinMagnetController>().Length == 2, "Two coins spawn.");

            CoinMagnetController[] actors = Actors<CoinMagnetController>();
            CoinMagnetController far = Array.Find(actors, actor => actor.transform.position.x < -4.5f);
            CoinMagnetController near = Array.Find(actors, actor => actor != far);
            GameObject farVisual = far.transform.Find("Visual").gameObject;
            Vector3 initial = far.transform.position;
            yield return TickFor(0.14f);
            Assert.That(far.transform.position, Is.EqualTo(initial), "A coin outside the attraction radius waits.");
            Assert.That(near.transform.Find("Visual").gameObject.activeSelf, Is.True);

            CoinMagnetManager manager = Object.FindAnyObjectByType<CoinMagnetManager>();
            FrameProbe<CoinMagnetState> probe = new FrameProbe<CoinMagnetState>(2, far.transform);
            manager.StateCompleted += probe.Observe;
            far.enabled = false;
            Transform collector = GameObject.Find("Collector").transform;
            collector.position = new Vector3(-3f, 0f, 0f);

            yield return Await(() => far.transform.position.x > initial.x + 0.2f, "The in-range coin is moved by the Job while detached.");
            yield return Await(() => !near.transform.Find("Visual").gameObject.activeSelf, "The subscribed coin hides after reaching the collector.");
            yield return Await(() => far.transform.position.Equals(collector.position), "The detached coin reaches the collector.");
            Assert.That(farVisual.activeSelf, Is.True);

            far.enabled = true;
            yield return Await(() => !farVisual.activeSelf, "A new callback applies the collected state.");
            Vector3 collected = far.transform.position;
            collector.position += Vector3.right;
            yield return TickFor(0.12f);
            Assert.That(far.transform.position, Is.EqualTo(collected));
            manager.StateCompleted -= probe.Observe;
            probe.Check();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlatformPatrolMovesWhileDetachedThenHoldsAndReverses()
        {
            Open("PlatformPatrol");
            yield return new EnterPlayMode();
            yield return VerifyPlatformPatrol();
        }

        private static IEnumerator VerifyPlatformPatrol()
        {
            yield return Await(() => Actors<PlatformPatrolController>().Length == 2, "Two platforms spawn.");

            PlatformPatrolController lower = Array.Find(Actors<PlatformPatrolController>(), actor => actor.transform.position.y < 0f);
            Animator animator = lower.GetComponentInChildren<Animator>();
            lower.enabled = false;
            float start = lower.transform.position.x;

            PlatformPatrolManager manager = Object.FindAnyObjectByType<PlatformPatrolManager>();
            FrameProbe<PlatformPatrolState> probe = new FrameProbe<PlatformPatrolState>(2, lower.transform);
            manager.StateCompleted += probe.Observe;
            yield return Await(() => lower.transform.position.x > start + 0.2f, "The detached platform traverses its route.");
            yield return Await(() => Mathf.Abs(lower.transform.position.x + 0.5f) < 0.001f, "The Job reaches the exact endpoint.");
            Assert.That(animator.GetBool("Holding"), Is.False);

            lower.enabled = true;
            yield return Await(() => animator.GetBool("Holding") && animator.GetCurrentAnimatorStateInfo(0).IsName("Active"), "The event presents the hold state.");
            yield return TickFor(0.12f);
            Assert.That(lower.transform.position.x, Is.EqualTo(-0.5f).Within(0.001f));
            yield return Await(() => lower.transform.position.x < -0.62f && !animator.GetBool("Holding"), "The Job reverses after its endpoint hold.");
            manager.StateCompleted -= probe.Observe;
            probe.Check();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CompanionFormationKeepsItsSlotAcrossLeaderRotation()
        {
            Open("CompanionFormation");
            yield return new EnterPlayMode();
            yield return VerifyCompanionFormation();
        }

        private static IEnumerator VerifyCompanionFormation()
        {
            yield return Await(() => Actors<CompanionFormationController>().Length == 3, "Three companions spawn.");

            CompanionFormationController top = Array.Find(Actors<CompanionFormationController>(), actor => actor.transform.position.y > 1.2f);
            Animator animator = top.GetComponentInChildren<Animator>();
            top.enabled = false;
            animator.SetBool("Following", false);
            float start = top.transform.position.x;

            CompanionFormationManager manager = Object.FindAnyObjectByType<CompanionFormationManager>();
            FrameProbe<CompanionFormationState> probe = new FrameProbe<CompanionFormationState>(3, top.transform);
            manager.StateCompleted += probe.Observe;
            yield return Await(() => top.transform.position.x > start + 0.25f, "The detached companion moves toward its slot.");
            Assert.That(animator.GetBool("Following"), Is.False);
            top.enabled = true;
            yield return Await(() => animator.GetBool("Following") && animator.GetCurrentAnimatorStateInfo(0).IsName("Active"), "The event resumes the follow animation.");
            yield return Await(() => top.transform.position.Equals(new Vector3(-1f, 1f)) && !animator.GetBool("Following"), "The companion settles on its original slot.");

            Transform leader = GameObject.Find("Leader").transform;
            Assert.That(leader.localScale, Is.EqualTo(Vector3.one));
            Assert.That(leader.Find("Visual").localScale, Is.EqualTo(Vector3.one * 0.6f));
            leader.SetPositionAndRotation(new Vector3(2f, 0f), Quaternion.Euler(0f, 0f, 90f));
            Vector3 expected = leader.TransformPoint(new Vector3(-1f, 1f));
            yield return Await(() => (top.transform.position - expected).sqrMagnitude < 0.0001f && !animator.GetBool("Following"), "The same slot follows the translated and rotated leader.");
            manager.StateCompleted -= probe.Observe;
            probe.Check();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator ExitScene()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static void Open(string name)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene/" + name + "/" + name + ".unity");
        }

        private static T[] Actors<T>() where T : MonoBehaviour => Object.FindObjectsByType<T>();

        private static IEnumerator TickFor(float seconds)
        {
            float finish = Time.fixedTime + seconds;
            yield return Await(() => Time.fixedTime >= finish, "Physics advances.");
        }

        private static IEnumerator Await(Func<bool> isReady, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 8d;
            while (!isReady() && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.That(isReady(), Is.True, message);
        }

        private sealed class FrameProbe<T> where T : struct
        {
            private readonly int _expectedCount;
            private readonly Transform _actor;
            private readonly Vector3 _start;
            private float _lastFrame = float.NegativeInfinity;
            private bool _duplicated;
            private bool _wrongCount;
            private bool _movedBeforeDispatch;

            public int Count { get; private set; }

            public FrameProbe(int expectedCount, Transform actor)
            {
                _expectedCount = expectedCount;
                _actor = actor;
                _start = actor.position;
            }

            public void Observe(NativeArray<T>.ReadOnly states)
            {
                _wrongCount |= states.Length != _expectedCount;
                _duplicated |= _lastFrame == Time.fixedTime;
                _movedBeforeDispatch |= _actor != null && !_actor.position.Equals(_start);
                _lastFrame = Time.fixedTime;
                Count++;
            }

            public void Check()
            {
                Assert.That(Count, Is.GreaterThan(1));
                Assert.That(_wrongCount, Is.False);
                Assert.That(_duplicated, Is.False);
                Assert.That(_movedBeforeDispatch, Is.True);
            }
        }
    }
}
