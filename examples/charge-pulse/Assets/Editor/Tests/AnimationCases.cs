using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class AnimationCases
    {
        [Test]
        public void Animation03_LoopClosingPoseAndTangentsMatchOnTheActualBinding()
        {
            AnimationClip sway = new AnimationClip { name = "Sway", frameRate = 60f };
            GameObject root = new GameObject("Root");
            GameObject leaf = new GameObject("Leaf");
            leaf.transform.SetParent(root.transform, false);
            try
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve("Leaf", typeof(Transform), "m_LocalPosition.x");
                AnimationCurve curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0.8f), new Keyframe(0.65f, -0.32f), new Keyframe(1f, 0f));
                AnimationUtility.SetEditorCurve(sway, binding, curve);
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(sway);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(sway, settings);
                Assert.That(AnimationUtility.GetAnimationClipSettings(sway).loopTime, Is.True);
                sway.SampleAnimation(root, 0f);
                Vector3 first = leaf.transform.localPosition;
                sway.SampleAnimation(root, 0.3f);
                Assert.That(leaf.transform.localPosition.x, Is.EqualTo(0.8f).Within(0.001f));
                sway.SampleAnimation(root, 1f);
                Assert.That(leaf.transform.localPosition, Is.EqualTo(first));
                Assert.That(curve.keys[0].outTangent, Is.EqualTo(curve.keys[3].inTangent));
                Assert.That(curve.Evaluate(0.075f), Is.LessThan(0.2f), "The first quarter must ease in instead of following a straight chord.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sway);
            }
        }

        [TestCase(BlendTreeType.Simple1D)]
        [TestCase(BlendTreeType.SimpleDirectional2D)]
        [TestCase(BlendTreeType.Direct)]
        public void Animation04_BlendModesRetainTheirActualParametersAndBindings(BlendTreeType mode)
        {
            using EditorCaseAssets assets = new EditorCaseAssets();
            AnimationClip idle = SavePose(assets.Folder, "IdlePose", 0f);
            AnimationClip raised = SavePose(assets.Folder, "RaisedPose", 2f);
            AnimationClip variant = SavePose(assets.Folder, "VariantPose", 4f);
            string path = $"{assets.Folder}/Example.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
            controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);
            controller.AddParameter("Recover", AnimatorControllerParameterType.Trigger);
            controller.CreateBlendTreeInController("Motion", out BlendTree tree);
            tree.blendType = mode;
            tree.useAutomaticThresholds = false;
            tree.blendParameter = "Horizontal";
            tree.blendParameterY = "Vertical";
            if (mode == BlendTreeType.SimpleDirectional2D)
            {
                tree.AddChild(idle, Vector2.zero);
                tree.AddChild(raised, Vector2.up);
            }
            else
            {
                tree.AddChild(idle, 0f);
                tree.AddChild(raised, 1f);
                if (mode == BlendTreeType.Direct)
                {
                    ChildMotion[] children = tree.children;
                    children[0].directBlendParameter = "Horizontal";
                    children[1].directBlendParameter = "Vertical";
                    tree.children = children;
                }
            }

            AnimatorStateMachine states = controller.layers[0].stateMachine;
            AnimatorState settle = states.AddState("Settle");
            settle.motion = idle;
            AnimatorStateTransition interrupt = states.AddAnyStateTransition(settle);
            interrupt.hasExitTime = false;
            interrupt.canTransitionToSelf = false;
            interrupt.hasFixedDuration = true;
            interrupt.duration = 0.1f;
            interrupt.AddCondition(AnimatorConditionMode.If, 0f, "Recover");
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            AnimatorController saved = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            AnimatorStateMachine savedStates = saved.layers[0].stateMachine;
            BlendTree observed = (BlendTree)savedStates.states.Single(child => child.state.name == "Motion").state.motion;
            Assert.That(observed.blendType, Is.EqualTo(mode));
            Assert.That(observed.children.Select(child => child.motion), Is.EqualTo(new Motion[] { idle, raised }));
            if (mode == BlendTreeType.Simple1D) Assert.That(observed.children[1].threshold, Is.EqualTo(1f));
            if (mode == BlendTreeType.SimpleDirectional2D) Assert.That(observed.children[1].position, Is.EqualTo(Vector2.up));
            if (mode == BlendTreeType.Direct) Assert.That(observed.children[1].directBlendParameter, Is.EqualTo("Vertical"));
            Assert.That(savedStates.anyStateTransitions[0].conditions[0].parameter, Is.EqualTo("Recover"));
            Assert.That(savedStates.anyStateTransitions[0].canTransitionToSelf, Is.False);

            AnimatorOverrideController replacement = new AnimatorOverrideController(saved);
            try
            {
                replacement["RaisedPose"] = variant;
                Assert.That(replacement["RaisedPose"], Is.SameAs(variant));
                Assert.That(observed.children[1].motion, Is.SameAs(raised));
            }
            finally
            {
                Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void Animation05_NativeMixerProducesEndpointsAndAnActualIntermediatePose()
        {
            using EditorCaseAssets assets = new EditorCaseAssets();
            AnimationClip low = SavePose(assets.Folder, "Low", 0f);
            AnimationClip high = SavePose(assets.Folder, "High", 4f);
            GameObject actor = new GameObject("Blended actor");
            Animator animator = actor.AddComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("Pose comparison");
            try
            {
                AnimationMixerPlayable mixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(AnimationClipPlayable.Create(graph, low), 0, mixer, 0);
                graph.Connect(AnimationClipPlayable.Create(graph, high), 0, mixer, 1);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Root pose", animator);
                output.SetSourcePlayable(mixer);
                foreach (float weight in new[] { 0f, 0.25f, 1f })
                {
                    mixer.SetInputWeight(0, 1f - weight);
                    mixer.SetInputWeight(1, weight);
                    graph.Evaluate();
                    Assert.That(actor.transform.localPosition.x, Is.EqualTo(4f * weight).Within(0.001f));
                }
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void Animation06_NativeSkinPreservesBindPoseAndMovesOnlyWeightedVertices()
        {
            GameObject actor = new GameObject("Rig");
            GameObject hip = new GameObject("Hip");
            GameObject wrist = new GameObject("Wrist");
            hip.transform.SetParent(actor.transform, false);
            wrist.transform.SetParent(hip.transform, false);
            wrist.transform.localPosition = Vector3.right;
            SkinnedMeshRenderer renderer = actor.AddComponent<SkinnedMeshRenderer>();
            Mesh source = new Mesh();
            Mesh evaluated = new Mesh();
            try
            {
                Vector3[] vertices = { Vector3.zero, Vector3.up, Vector3.right, Vector3.one - Vector3.forward };
                source.vertices = vertices;
                source.triangles = new[] { 0, 1, 2, 2, 1, 3 };
                source.bindposes = new[] { hip.transform.worldToLocalMatrix * actor.transform.localToWorldMatrix, wrist.transform.worldToLocalMatrix * actor.transform.localToWorldMatrix };
                source.boneWeights = new[]
                {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 1, weight0 = 1f },
                    new BoneWeight { boneIndex0 = 0, weight0 = 0.5f, boneIndex1 = 1, weight1 = 0.5f }
                };
                source.RecalculateBounds();
                renderer.rootBone = hip.transform;
                renderer.bones = new[] { hip.transform, wrist.transform };
                renderer.sharedMesh = source;
                renderer.BakeMesh(evaluated);
                CollectionAssert.AreEqual(vertices, evaluated.vertices);

                wrist.transform.localPosition += Vector3.up;
                renderer.BakeMesh(evaluated);
                Vector3[] deformed = evaluated.vertices;
                Assert.That(deformed[0], Is.EqualTo(vertices[0]));
                Assert.That(deformed[2], Is.EqualTo(vertices[2] + Vector3.up));
                Assert.That(deformed[3], Is.EqualTo(vertices[3] + Vector3.up * 0.5f));
                CollectionAssert.AreEqual(vertices, source.vertices);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(evaluated);
            }
        }

        private static AnimationClip SavePose(string folder, string name, float position)
        {
            AnimationClip clip = new AnimationClip { name = name };
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, position));
            AssetDatabase.CreateAsset(clip, $"{folder}/{name}.anim");
            return clip;
        }
    }
}
