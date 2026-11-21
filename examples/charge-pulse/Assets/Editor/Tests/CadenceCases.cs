using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Qin.UnitySamples.Tests
{
    public sealed class CadenceCases
    {
        [UnityTest]
        public IEnumerator Code03_ContinuousRevealRunsOnlyWhileProducingNewFrames()
        {
            yield return new EnterPlayMode();
            GameObject subject = new GameObject("Timed reveal");
            try
            {
                TimedRevealController reveal = subject.AddComponent<TimedRevealController>();
                subject.transform.localScale = Vector3.zero;
                Assert.That(reveal.enabled, Is.False);
                reveal.ScaleTo(1f, 0.4f);
                Assert.That(subject.transform.localScale, Is.EqualTo(Vector3.zero));
                float deadline = Time.realtimeSinceStartup + 3f;
                bool sawIntermediate = false;
                while (reveal.enabled && Time.realtimeSinceStartup < deadline)
                {
                    float current = subject.transform.localScale.x;
                    sawIntermediate |= current > 0f && current < 1f;
                    yield return null;
                }
                Assert.That(sawIntermediate, Is.True);
                Assert.That(subject.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(reveal.enabled, Is.False);
                reveal.ScaleTo(1f, 0.4f);
                Assert.That(reveal.enabled, Is.False, "An already settled property needs no frame callback.");
            }
            finally
            {
                Object.Destroy(subject);
            }
        }

        [UnityTest]
        public IEnumerator Effects06_InterruptedShapeRetractsFromItsCurrentExtent()
        {
            yield return new EnterPlayMode();
            GameObject subject = new GameObject("Interrupted shape");
            try
            {
                subject.transform.localScale = Vector3.zero;
                TimedRevealController reveal = subject.AddComponent<TimedRevealController>();
                reveal.ScaleTo(1f, 0.6f);
                float deadline = Time.realtimeSinceStartup + 3f;
                while (subject.transform.localScale.x < 0.1f && Time.realtimeSinceStartup < deadline) yield return null;
                float current = subject.transform.localScale.x;
                Assert.That(current, Is.InRange(0.1f, 0.99f));
                reveal.ScaleTo(0f, 0.25f);
                Assert.That(subject.transform.localScale.x, Is.EqualTo(current));
                while (reveal.enabled && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(subject.transform.localScale.x, Is.Zero);
                Assert.That(reveal.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(subject);
            }
        }

        [UnityTest]
        public IEnumerator Code09_FixedStepMovesTheBodyAndLateStepReadsTheResult()
        {
            yield return new EnterPlayMode();
            GameObject subject = new GameObject("Kinematic body");
            GameObject marker = new GameObject("Late marker");
            try
            {
                Rigidbody body = subject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                StepMotionController motion = subject.AddComponent<StepMotionController>();
                SerializedObject binding = new SerializedObject(motion);
                binding.FindProperty("_marker").objectReferenceValue = marker.transform;
                binding.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(motion.enabled, Is.False);
                motion.SetVelocity(new Vector3(2f, 0f, 0f));
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.That(body.position.x, Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(marker.transform.position, subject.transform.position), Is.LessThanOrEqualTo(0.1f));
                motion.SetVelocity(Vector3.zero);
                Vector3 stopped = body.position;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Assert.That(body.position, Is.EqualTo(stopped));
                Assert.That(motion.enabled, Is.False);
            }
            finally
            {
                Object.Destroy(subject);
                Object.Destroy(marker);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
