using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.JobStateBatches.Editor
{
    public static class BuildBatchScenes
    {
        private const string SceneRoot = "Assets/Scenes/GameScene/";
        private const string PrefabRoot = "Assets/Prefabs/";

        public static void BuildAll()
        {
            BuildEnemyChase();
            BuildFireballVolley();
            BuildCoinMagnet();
            BuildPlatformPatrol();
            BuildCompanionFormation();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath("EnemyChase"), true),
                new EditorBuildSettingsScene(ScenePath("FireballVolley"), true),
                new EditorBuildSettingsScene(ScenePath("CoinMagnet"), true),
                new EditorBuildSettingsScene(ScenePath("PlatformPatrol"), true),
                new EditorBuildSettingsScene(ScenePath("CompanionFormation"), true)
            };
            AssetDatabase.SaveAssets();
        }

        private static void BuildEnemyChase()
        {
            const string name = "EnemyChase";
            EnemyChaseSceneController controller = CreateScene<EnemyChaseController, EnemyChaseSceneController>(name, PrimitiveType.Sphere, Vector3.one * 0.42f, "Chasing");
            GameObject player = Marker("Player", Vector3.zero, PrimitiveType.Cube, Vector3.one * 0.7f);
            player.AddComponent<CircleCollider2D>().radius = 0.65f;
            Marker("Decoy", new Vector3(-3f, 0f, 0f), PrimitiveType.Cube, Vector3.one * 0.28f).AddComponent<CircleCollider2D>().radius = 1f;

            SerializedObject binding = new SerializedObject(controller);
            binding.FindProperty("_target").objectReferenceValue = player.GetComponent<CircleCollider2D>();
            BindPoints(binding, "_points", Points(controller.transform, "SpawnPoints", new Vector3(-3f, 0f), new Vector3(0f, 3f), new Vector3(8f, 0f)));
            SaveScene(binding, name);
        }

        private static void BuildFireballVolley()
        {
            const string name = "FireballVolley";
            FireballVolleySceneController controller = CreateScene<FireballVolleyController, FireballVolleySceneController>(name, PrimitiveType.Sphere, Vector3.one * 0.24f, null);
            GameObject muzzle = Marker("Muzzle", Vector3.zero, PrimitiveType.Cube, Vector3.one * 0.5f);
            SerializedObject binding = new SerializedObject(controller);
            binding.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            SaveScene(binding, name);
        }

        private static void BuildCoinMagnet()
        {
            const string name = "CoinMagnet";
            CoinMagnetSceneController controller = CreateScene<CoinMagnetController, CoinMagnetSceneController>(name, PrimitiveType.Cylinder, new Vector3(0.34f, 0.1f, 0.34f), null);
            GameObject collector = Marker("Collector", Vector3.zero, PrimitiveType.Cube, Vector3.one * 0.7f);
            SerializedObject binding = new SerializedObject(controller);
            binding.FindProperty("_collector").objectReferenceValue = collector.transform;
            BindPoints(binding, "_points", Points(controller.transform, "Coins", new Vector3(-4f, -0.4f), new Vector3(-5f, 0.4f)));
            SaveScene(binding, name);
        }

        private static void BuildPlatformPatrol()
        {
            const string name = "PlatformPatrol";
            PlatformPatrolSceneController controller = CreateScene<PlatformPatrolController, PlatformPatrolSceneController>(name, PrimitiveType.Cube, new Vector3(0.8f, 0.27f, 0.4f), "Holding");
            SerializedObject binding = new SerializedObject(controller);
            BindPoints(binding, "_starts", Points(controller.transform, "Starts", new Vector3(-2f, -1.5f), new Vector3(-2f, 1.5f)));
            BindPoints(binding, "_ends", Points(controller.transform, "Ends", new Vector3(-0.5f, -1.5f), new Vector3(1f, 1.5f)));
            SaveScene(binding, name);
        }

        private static void BuildCompanionFormation()
        {
            const string name = "CompanionFormation";
            CompanionFormationSceneController controller = CreateScene<CompanionFormationController, CompanionFormationSceneController>(name, PrimitiveType.Capsule, Vector3.one * 0.3f, "Following");
            GameObject leader = new GameObject("Leader");
            GameObject leaderVisual = Marker("Visual", Vector3.zero, PrimitiveType.Cube, Vector3.one * 0.6f);
            leaderVisual.transform.SetParent(leader.transform, false);
            SerializedObject binding = new SerializedObject(controller);
            binding.FindProperty("_leader").objectReferenceValue = leader.transform;
            BindPoints(binding, "_starts", Points(controller.transform, "Starts", new Vector3(-5f, -2f), new Vector3(-5f, 0f), new Vector3(-5f, 2f)));
            SerializedProperty offsets = binding.FindProperty("_offsets");
            offsets.arraySize = 3;
            offsets.GetArrayElementAtIndex(0).vector3Value = new Vector3(-1f, -1f);
            offsets.GetArrayElementAtIndex(1).vector3Value = new Vector3(-1f, 0f);
            offsets.GetArrayElementAtIndex(2).vector3Value = new Vector3(-1f, 1f);
            SaveScene(binding, name);
        }

        private static TScene CreateScene<TActor, TScene>(string name, PrimitiveType primitive, Vector3 scale, string parameter)
            where TActor : MonoBehaviour
            where TScene : MonoBehaviour
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(SceneRoot + name);
            string folder = PrefabRoot + name + "/";
            EnsureFolder(folder.TrimEnd('/'));

            GameObject actor = new GameObject(name + "Actor");
            actor.SetActive(false);
            TActor actorController = actor.AddComponent<TActor>();
            GameObject visual = Marker("Visual", Vector3.zero, primitive, scale);
            visual.transform.SetParent(actor.transform, false);
            SerializedObject actorBinding = new SerializedObject(actorController);
            if (parameter != null)
            {
                Animator animator = visual.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = CreateAnimator(folder + name + ".controller", parameter);
                actorBinding.FindProperty("_animator").objectReferenceValue = animator;
            }
            else actorBinding.FindProperty("_visual").objectReferenceValue = visual;

            if (actor.TryGetComponent<Rigidbody2D>(out Rigidbody2D body))
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                CircleCollider2D shape = actor.GetComponent<CircleCollider2D>();
                shape.radius = 0.21f;
                shape.isTrigger = true;
            }

            actorBinding.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(actor, folder + actor.name + ".prefab");
            Object.DestroyImmediate(actor);
            if (prefab == null) throw new InvalidOperationException("Could not save " + name + " prefab.");

            GameObject sceneRoot = new GameObject(name);
            TScene sceneController = sceneRoot.AddComponent<TScene>();
            SerializedObject sceneBinding = new SerializedObject(sceneController);
            sceneBinding.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<TActor>();
            sceneBinding.ApplyModifiedPropertiesWithoutUndo();
            CreateCamera();
            return sceneController;
        }

        private static AnimatorController CreateAnimator(string path, string parameter)
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = machine.AddState("Idle");
            AnimatorState active = machine.AddState("Active");
            idle.motion = new AnimationClip { name = "Idle" };
            active.motion = new AnimationClip { name = "Active" };
            AssetDatabase.AddObjectToAsset(idle.motion, controller);
            AssetDatabase.AddObjectToAsset(active.motion, controller);
            machine.defaultState = idle;
            SetTransition(idle.AddTransition(active), AnimatorConditionMode.If, parameter);
            SetTransition(active.AddTransition(idle), AnimatorConditionMode.IfNot, parameter);
            return controller;
        }

        private static void SetTransition(AnimatorStateTransition transition, AnimatorConditionMode mode, string parameter)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(mode, 0f, parameter);
        }

        private static GameObject Marker(string name, Vector3 position, PrimitiveType primitive, Vector3 scale)
        {
            GameObject marker = GameObject.CreatePrimitive(primitive);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = scale;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            return marker;
        }

        private static Transform[] Points(Transform owner, string name, params Vector3[] positions)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(owner, false);
            Transform[] points = new Transform[positions.Length];
            for (int index = 0; index < points.Length; index++)
            {
                points[index] = new GameObject("Point" + (index + 1)).transform;
                points[index].SetParent(group, false);
                points[index].localPosition = positions[index];
            }
            return points;
        }

        private static void BindPoints(SerializedObject binding, string field, Transform[] points)
        {
            SerializedProperty property = binding.FindProperty(field);
            property.arraySize = points.Length;
            for (int index = 0; index < points.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = points[index];
        }

        private static void CreateCamera()
        {
            GameObject view = new GameObject("Main Camera");
            view.tag = "MainCamera";
            view.transform.position = new Vector3(1f, 0f, -12f);
            Camera camera = view.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            Light light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int boundary = path.LastIndexOf('/');
            string parent = path.Substring(0, boundary);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(boundary + 1));
        }

        private static string ScenePath(string name) => SceneRoot + name + "/" + name + ".unity";

        private static void SaveScene(SerializedObject binding, string name)
        {
            binding.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath(name)))
                throw new InvalidOperationException("Could not save " + name + " scene.");
        }
    }
}
