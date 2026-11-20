using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples
{
    public static class ChargePulseSampleAuthoring
    {
        private const string _assetFolder = "Assets/Prefabs/ChargePulse/";
        private const string _scenePath = "Assets/Scenes/ChargePulse.unity";

        public static void Rebuild()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ChargePulse";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(_assetFolder + "PulseGlow.shader");
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(_assetFolder + "ChargePulseHud.uxml");
            if (shader == null || tree == null) throw new InvalidOperationException("Import the effect shader and HUD before creating the scene.");

            Material ring = CreateMaterial("Ring", shader, new Color(0.19f, 0.96f, 0.83f, 0.86f), 1f, 0f);
            Material core = CreateMaterial("Core", shader, new Color(0.44f, 0.82f, 1f, 0.8f), 0f, 1f);
            Material spark = CreateMaterial("Spark", shader, new Color(1f, 0.85f, 0.48f, 0.96f), 0f, 1f);
            AnimatorController controller = CreateAnimator();
            GameObject pulse = CreatePulse(controller, ring, core, spark);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pulse, _assetFolder + "ChargePulse.prefab");
            Object.DestroyImmediate(pulse);
            if (prefab == null) throw new InvalidOperationException("Saving the Charge Pulse Prefab failed.");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            CreateCamera();
            CreateHud(tree, instance.GetComponent<ChargePulseController>());
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveScene(scene, _scenePath)) throw new InvalidOperationException("Saving the Charge Pulse scene failed.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(_scenePath, true) };
        }

        private static Material CreateMaterial(string name, Shader shader, Color tint, float ringWeight, float coreWeight)
        {
            Material material = new Material(shader) { name = name };
            material.SetColor("_Tint", tint);
            material.SetFloat("_RingWeight", ringWeight);
            material.SetFloat("_CoreWeight", coreWeight);
            return SaveAsset(material, name + ".mat");
        }

        private static AnimatorController CreateAnimator()
        {
            AnimationClip idle = new AnimationClip { name = ChargePulseController.IdleState, frameRate = 60f };
            AnimateScale(idle, "Visuals/Ring", new Keyframe(0f, 0.8f), new Keyframe(1f / 60f, 0.8f));
            AnimateScale(idle, "Visuals/Core", new Keyframe(0f, 0.32f), new Keyframe(1f / 60f, 0.32f));

            AnimationClip pulse = new AnimationClip { name = ChargePulseController.PulseState, frameRate = 60f };
            AnimateScale(pulse, "Visuals/Ring", new Keyframe(0f, 0.8f), new Keyframe(0.28f, 1.1f), new Keyframe(0.38f, 1.1f), new Keyframe(0.62f, 3.8f), new Keyframe(0.72f, 4.1f), new Keyframe(1.18f, 0.8f));
            AnimateScale(pulse, "Visuals/Core", new Keyframe(0f, 0.32f), new Keyframe(0.28f, 0.92f), new Keyframe(0.38f, 0.92f), new Keyframe(0.53f, 1.08f), new Keyframe(0.75f, 0.46f), new Keyframe(1.18f, 0.32f));
            AnimationUtility.SetAnimationEvents(pulse, new[]
            {
                StageAt(0.06f, PulseStage.Preparing),
                StageAt(0.28f, PulseStage.Prepared),
                StageAt(0.38f, PulseStage.Releasing),
                StageAt(0.62f, PulseStage.Released),
                StageAt(0.72f, PulseStage.Retracting),
                StageAt(1.175f, PulseStage.Recycled)
            });

            string controllerPath = _assetFolder + "ChargePulse.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine states = controller.layers[0].stateMachine;
            states.defaultState = AssignClip(states, SaveAsset(idle, "Idle.anim"), ChargePulseController.IdleState);
            AssignClip(states, SaveAsset(pulse, "Pulse.anim"), ChargePulseController.PulseState);
            EditorUtility.SetDirty(states);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorState AssignClip(AnimatorStateMachine states, AnimationClip clip, string name)
        {
            AnimatorState state = null;
            foreach (ChildAnimatorState child in states.states)
            {
                if (child.state.name != name) continue;
                state = child.state;
                break;
            }

            if (state == null) state = states.AddState(name);
            state.motion = clip;
            EditorUtility.SetDirty(state);
            return state;
        }

        private static void AnimateScale(AnimationClip clip, string path, params Keyframe[] keys)
        {
            AnimationCurve curve = new AnimationCurve(keys);
            foreach (string axis in new[] { "x", "y" })
            {
                EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale." + axis);
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        private static AnimationEvent StageAt(float time, PulseStage stage)
        {
            return new AnimationEvent { time = time, functionName = nameof(ChargePulseController.ApplyAnimationStage), intParameter = (int)stage };
        }

        private static GameObject CreatePulse(AnimatorController controller, Material ring, Material core, Material spark)
        {
            GameObject root = new GameObject("Charge Pulse");
            root.transform.position = new Vector3(0f, 0.85f, 0f);
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);
            CreateQuad("Ring", visuals.transform, ring, 0.8f, 0);
            CreateQuad("Core", visuals.transform, core, 0.32f, 1);

            GameObject sparkObject = new GameObject("Sparks");
            sparkObject.SetActive(false);
            sparkObject.transform.SetParent(visuals.transform, false);
            ParticleSystem sparks = sparkObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
            main.maxParticles = 48;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.26f;
            ParticleSystem.LimitVelocityOverLifetimeModule slowdown = sparks.limitVelocityOverLifetime;
            slowdown.enabled = true;
            slowdown.limit = 1.6f;
            slowdown.dampen = 0.45f;
            ParticleSystemRenderer renderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = spark;
            renderer.sortingOrder = 2;
            sparkObject.SetActive(true);

            ChargePulseController pulse = root.AddComponent<ChargePulseController>();
            SerializedObject bindings = new SerializedObject(pulse);
            bindings.FindProperty("_animator").objectReferenceValue = animator;
            bindings.FindProperty("_visuals").objectReferenceValue = visuals;
            bindings.FindProperty("_sparks").objectReferenceValue = sparks;
            bindings.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static void CreateQuad(string name, Transform parent, Material material, float size, int order)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localScale = new Vector3(size, size, 1f);
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
        }

        private static void CreateCamera()
        {
            GameObject root = new GameObject("Main Camera") { tag = "MainCamera" };
            root.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = root.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.038f, 0.071f, 0.1f);
        }

        private static void CreateHud(VisualTreeAsset tree, ChargePulseController pulse)
        {
            PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "ChargePulsePanel";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(3840, 2160);
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(_assetFolder + "PulseTheme.tss");
            panel = SaveAsset(panel, "ChargePulsePanel.asset");

            GameObject hud = new GameObject("Pulse HUD");
            UIDocument document = hud.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = tree;
            ChargePulseHudController view = hud.AddComponent<ChargePulseHudController>();
            SerializedObject bindings = new SerializedObject(view);
            bindings.FindProperty("_document").objectReferenceValue = document;
            bindings.FindProperty("_pulse").objectReferenceValue = pulse;
            bindings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T SaveAsset<T>(T asset, string name) where T : Object
        {
            string path = _assetFolder + name;
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
