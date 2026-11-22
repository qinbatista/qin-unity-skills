using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class GroundContactCases
    {
        [UnityTest]
        public IEnumerator NativeRootTravelScalesOnceAndHoldsWhilePaused()
        {
            yield return new EnterPlayMode();
            using EditorCaseAssets assets = new EditorCaseAssets();
            AnimationClip clip = new AnimationClip { name = "Root travel", frameRate = 60f };
            EditorCurveBinding rootX = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, rootX, AnimationCurve.Linear(0f, 0f, 2f, 4f));
            AnimationUtility.SetGenerateMotionCurves(clip, true);
            string path = assets.Folder + "/Travel.anim";
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            GameObject actor = new GameObject("Root motion actor");
            Animator animator = actor.AddComponent<Animator>();
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PlayableGraph graph = PlayableGraph.Create("Native root travel");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                AnimationClipPlayable player = AnimationClipPlayable.Create(graph, saved);
                AnimationPlayableOutput.Create(graph, "Root", animator).SetSourcePlayable(player);
                graph.Play();
                graph.Evaluate(0f);
                foreach (float rate in new[] { 1f, 0.5f, 0f, 0.5f })
                {
                    player.SetSpeed(rate);
                    float start = actor.transform.position.x;
                    float accumulated = 0f;
                    for (int i = 0; i < 24; i++)
                    {
                        graph.Evaluate(1f / 60f);
                        accumulated += animator.deltaPosition.x;
                    }
                    float expected = rate * 2f * 24f / 60f;
                    Assert.That(accumulated, Is.EqualTo(expected).Within(0.003f), $"Native deltas at {rate}x");
                    Assert.That(actor.transform.position.x - start, Is.EqualTo(accumulated).Within(0.003f), "Each native delta must reach the root exactly once.");
                }
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(actor);
            }
        }

        [UnityTest]
        public IEnumerator HeldInputAtAPhysicsWallProducesNoNewFootfalls()
        {
            yield return new EnterPlayMode();
            using EditorCaseAssets assets = new EditorCaseAssets();
            AnimationClip saved = VirtualWalker.SaveGait(assets.Folder, false);
            Transform support = VirtualWalker.Node("Support", null, Vector3.zero);
            GameObject wall = new GameObject("Wall", typeof(BoxCollider));
            try
            {
                using VirtualWalker actor = new VirtualWalker(support, saved);
                CharacterController body = actor.Root.gameObject.AddComponent<CharacterController>();
                body.radius = 0.25f;
                body.height = 1.75f;
                body.center = Vector3.up * 0.875f;
                wall.transform.position = new Vector3(2f, 1f, 0f);
                wall.GetComponent<BoxCollider>().size = new Vector3(0.1f, 2f, 2f);
                Physics.SyncTransforms();
                double phase = 0.25;
                Vector3[] previousSoles = new Vector3[2];
                int approachingSteps = 0, blockedTicks = 0;
                bool[] wasDown = { true, false };
                for (int tick = 0; tick < 150; tick++)
                {
                    float previousX = actor.Root.position.x;
                    CollisionFlags hit = body.Move(Vector3.right * 0.04f);
                    float accepted = actor.Root.position.x - previousX;
                    phase += accepted / VirtualWalker.Stride;
                    actor.Sample(phase, 0f);
                    bool blocked = (hit & CollisionFlags.Sides) != 0 && Mathf.Abs(accepted) < 0.0001f;
                    if (blocked) blockedTicks++;
                    for (int side = 0; side < 2; side++)
                    {
                        Vector3 sole = actor.Soles[side].position;
                        bool down = sole.y <= 0.00001f;
                        if (down && !wasDown[side])
                        {
                            Assert.That(blocked, Is.False, "A new landing occurred under sustained blocked input.");
                            approachingSteps++;
                        }
                        if (blockedTicks > 1 && blocked)
                            Assert.That(Vector3.Distance(sole, previousSoles[side]), Is.LessThan(0.001f));
                        wasDown[side] = down;
                        previousSoles[side] = sole;
                    }
                }
                Assert.That(approachingSteps, Is.Positive);
                Assert.That(blockedTicks, Is.GreaterThan(75));
                Assert.That(actor.Root.position.x, Is.InRange(1.5f, 1.85f));
            }
            finally
            {
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(support.gameObject);
            }
        }

        [Test]
        public void LoadedForefootStaysOnItsPatchWhileHeelLifts()
        {
            using EditorCaseAssets assets = new EditorCaseAssets();
            GameObject stage = new GameObject("Foot roll");
            Transform toe = VirtualWalker.Node("Forefoot", stage.transform, Vector3.zero);
            Transform heel = VirtualWalker.Node("Heel", toe, Vector3.left * 0.24f);
            AnimationClip clip = new AnimationClip { name = "Toe off" };
            EditorCurveBinding angle = EditorCurveBinding.FloatCurve("Forefoot", typeof(Transform), "localEulerAnglesRaw.z");
            AnimationUtility.SetEditorCurve(clip, angle, AnimationCurve.EaseInOut(0f, 0f, 0.5f, -30f));
            float end = 0.8f;
            EditorCurveBinding lift = EditorCurveBinding.FloatCurve("Forefoot", typeof(Transform), "m_LocalPosition.y");
            AnimationUtility.SetEditorCurve(clip, lift, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(end, 0.16f)));
            string path = assets.Folder + "/Roll.anim";
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            try
            {
                Vector3 footprint = toe.position;
                float previousHeight = 0f;
                for (int frame = 0; frame <= 30; frame++)
                {
                    saved.SampleAnimation(stage, frame / 60f);
                    Assert.That(Vector3.Distance(toe.position, footprint), Is.LessThan(0.001f));
                    Assert.That(heel.position.y, Is.GreaterThanOrEqualTo(previousHeight - 0.0001f));
                    previousHeight = heel.position.y;
                }
                Assert.That(previousHeight, Is.EqualTo(0.12f).Within(0.001f));
                saved.SampleAnimation(stage, end);
                Assert.That(toe.position.y, Is.GreaterThan(0.15f));
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
