using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    // The clip drives the contact markers. The observer reads the bottom of each rendered shoe.
    sealed class VirtualWalker : IDisposable
    {
        public const float Stride = 2f;
        public readonly Transform Root;
        public readonly Transform Hips;
        public readonly Transform Head;
        public readonly Transform[] Soles = new Transform[2];
        public readonly Transform[] Knees = new Transform[2];
        public readonly Transform[] Shoulders = new Transform[2];
        readonly Transform[] thighs = new Transform[2];
        readonly Transform[] shins = new Transform[2];
        readonly Transform[] arms = new Transform[2];
        readonly Transform[] hands = new Transform[2];
        readonly List<Material> materials = new List<Material>();
        readonly PlayableGraph graph;
        readonly AnimationClipPlayable player;
        public const int Layer = 28;

        public VirtualWalker(Transform support, AnimationClip clip)
        {
            Root = Node("Surveyor", support, Vector3.zero);
            Material suit = Paint(new Color32(237, 166, 72, 255));
            Material trim = Paint(new Color32(247, 225, 184, 255));
            Material dark = Paint(new Color32(35, 55, 70, 255));
            Material nearBoot = Paint(new Color32(16, 115, 127, 255));
            Material farBoot = Paint(new Color32(89, 108, 135, 255));
            Hips = Node("Hips", Root, Vector3.up * 0.92f);
            Shape("Jacket", Hips, PrimitiveType.Capsule, new Vector3(0f, 0.27f, 0f), new Vector3(0.43f, 0.32f, 0.41f), suit);
            Shape("Belt", Hips, PrimitiveType.Sphere, Vector3.up * 0.045f, new Vector3(0.41f, 0.20f, 0.39f), dark);
            Head = Shape("Helmet", Hips, PrimitiveType.Sphere, new Vector3(0.02f, 0.70f, 0f), Vector3.one * 0.45f, trim);
            Shape("Visor", Head, PrimitiveType.Sphere, new Vector3(0.19f, 0.01f, -0.10f), new Vector3(0.78f, 0.47f, 0.88f), dark);
            for (int side = 0; side < 2; side++)
            {
                string id = side == 0 ? "Left" : "Right";
                float depth = side == 0 ? -0.13f : 0.13f;
                Material shoe = side == 0 ? nearBoot : farBoot;
                Transform foot = Node(id, Root, new Vector3(0f, 0f, depth));
                Transform boot = Shape("Boot", foot, PrimitiveType.Cube, new Vector3(0.04f, 0.07f, 0f), new Vector3(0.33f, 0.14f, 0.16f), shoe);
                Soles[side] = Node("Sole", boot, new Vector3(0f, -0.5f, 0f));
                Knees[side] = Shape(id + "Knee", Root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, shoe);
                thighs[side] = Shape(id + "Thigh", Root, PrimitiveType.Cube, Vector3.zero, Vector3.one, suit);
                shins[side] = Shape(id + "Shin", Root, PrimitiveType.Cube, Vector3.zero, Vector3.one, shoe);
                Shoulders[side] = Node(id + "Shoulder", Hips, new Vector3(0f, 0.44f, depth * 1.52f));
                hands[side] = Shape(id + "Glove", Root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, shoe);
                arms[side] = Shape(id + "Sleeve", Root, PrimitiveType.Cube, Vector3.zero, Vector3.one, trim);
            }
            Animator animator = Root.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Virtual surveyor gait");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            player = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Surveyor skeleton", animator);
            output.SetSourcePlayable(player);
            graph.Play();
        }

        public static AnimationClip SaveGait(string folder, bool run)
        {
            float duty = run ? 0.375f : 0.5f;
            float height = run ? 0.42f : 0.26f;
            AnimationClip clip = new AnimationClip { name = run ? "Run" : "Walk", frameRate = 64f };
            for (int side = 0; side < 2; side++)
            {
                float offset = side * 0.5f;
                string path = side == 0 ? "Left" : "Right";
                Bind(clip, path, "x", time =>
                {
                    float phase = Mathf.Repeat(time + offset, 1f);
                    float swing = Mathf.Clamp01((phase - duty) / (1f - duty));
                    return Stride * (duty * 0.5f - phase + swing * swing * (3f - 2f * swing));
                });
                Bind(clip, path, "y", time =>
                {
                    float phase = Mathf.Repeat(time + offset, 1f);
                    float arc = Mathf.Sin(Mathf.PI * Mathf.Clamp01((phase - duty) / (1f - duty)));
                    return height * arc * arc;
                });
            }
            Bind(clip, "Hips", "y", time => 0.91f + (run ? 0.046f : 0.026f) * Mathf.Cos(4f * Mathf.PI * time));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string pathName = folder + "/" + clip.name + ".anim";
            AssetDatabase.CreateAsset(clip, pathName);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(pathName, ImportAssetOptions.ForceSynchronousImport);
            AnimationClip reopened = AssetDatabase.LoadAssetAtPath<AnimationClip>(pathName);
            Assert.That(AnimationUtility.GetCurveBindings(reopened), Has.Length.EqualTo(5));
            Assert.That(AnimationUtility.GetAnimationClipSettings(reopened).loopTime, Is.True);
            return reopened;
        }

        static void Bind(AnimationClip clip, string path, string axis, Func<float, float> sample)
        {
            const int intervals = 64;
            Keyframe[] keys = new Keyframe[intervals + 1];
            for (int i = 0; i <= intervals; i++)
            {
                float t = (float)i / intervals;
                keys[i] = new Keyframe(t, sample(t));
            }
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + axis), curve);
        }

        public void Sample(double phase, float injectedHeadOffset)
        {
            player.SetTime(phase - Math.Floor(phase));
            graph.Evaluate(0f);
            Head.localPosition = new Vector3(0.02f, 0.70f + injectedHeadOffset, 0f);
            for (int side = 0; side < 2; side++)
            {
                Vector3 ankle = Root.InverseTransformPoint(Soles[side].position) + new Vector3(-0.04f, 0.11f, 0f);
                Vector3 hip = new Vector3(0f, Hips.localPosition.y, ankle.z);
                Vector3 axis = ankle - hip;
                Vector3 forwardBend = new Vector3(-axis.y, axis.x, 0f).normalized;
                const float segment = 0.58f;
                float bend = Mathf.Sqrt(Mathf.Max(0f, segment * segment - axis.sqrMagnitude * 0.25f));
                Vector3 knee = (hip + ankle) * 0.5f + forwardBend * bend;
                Knees[side].localPosition = knee;
                Beam(thighs[side], hip, knee, 0.155f);
                Beam(shins[side], knee, ankle, 0.115f);
                Vector3 shoulder = Root.InverseTransformPoint(Shoulders[side].position);
                Vector3 hand = shoulder + new Vector3(-ankle.x * 0.51f + 0.06f, -0.36f, 0f);
                hands[side].localPosition = hand;
                Beam(arms[side], shoulder, hand, 0.11f);
            }
        }

        static void Beam(Transform beam, Vector3 a, Vector3 b, float width)
        {
            beam.localPosition = (a + b) * 0.5f;
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            beam.localScale = new Vector3(width, (a - b).magnitude, width);
        }

        Material Paint(Color color)
        {
            Material material = new Material(Shader.Find("Unlit/Color")) { color = color };
            materials.Add(material);
            return material;
        }

        public static Transform Node(string name, Transform parent, Vector3 position)
        {
            Transform node = new GameObject(name).transform;
            node.gameObject.layer = Layer;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        public static Transform Shape(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.layer = Layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            return part.transform;
        }

        public void Dispose()
        {
            graph.Destroy();
            Object.DestroyImmediate(Root.gameObject);
            foreach (Material material in materials) Object.DestroyImmediate(material);
        }
    }
}
