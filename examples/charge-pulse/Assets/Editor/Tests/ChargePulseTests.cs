using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class ChargePulseTests
    {
        private const string _assetFolder = "Assets/Prefabs/ChargePulse/";
        private const string _scene = "Assets/Scenes/ChargePulse.unity";

        [Test]
        public void SavedPrefabSceneAndAnimationResolveTheirBindings()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_assetFolder + "ChargePulse.prefab");
            Assert.That(prefab, Is.Not.Null);
            ChargePulseController pulse = prefab.GetComponent<ChargePulseController>();
            SerializedObject bindings = new SerializedObject(pulse);
            Assert.That(bindings.FindProperty("_animator").objectReferenceValue, Is.SameAs(prefab.GetComponent<Animator>()));
            Assert.That(bindings.FindProperty("_visuals").objectReferenceValue, Is.SameAs(prefab.transform.Find("Visuals").gameObject));
            Assert.That(bindings.FindProperty("_sparks").objectReferenceValue, Is.SameAs(prefab.GetComponentInChildren<ParticleSystem>()));

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_assetFolder + "Pulse.anim");
            CollectionAssert.Contains(prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips, clip);
            Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.False);
            Assert.That(AnimationUtility.GetAnimationEvents(clip), Has.Length.EqualTo(6));
            AnimationCurve radius = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("Visuals/Ring", typeof(Transform), "m_LocalScale.x"));
            Assert.That(radius.Evaluate(clip.length), Is.EqualTo(radius.Evaluate(0f)).Within(0.001f));
            Assert.That(radius.Evaluate(0.52f) - radius.Evaluate(0.45f), Is.GreaterThan(radius.Evaluate(0.45f) - radius.Evaluate(0.38f)));

            EditorSceneManager.OpenScene(_scene);
            ChargePulseController instance = Object.FindFirstObjectByType<ChargePulseController>();
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject), Is.SameAs(prefab));
            UIDocument document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document.visualTreeAsset, Is.SameAs(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(_assetFolder + "ChargePulseHud.uxml")));
            Assert.That(document.panelSettings.referenceResolution, Is.EqualTo(new Vector2Int(3840, 2160)));
        }

        [UnityTest]
        public IEnumerator NativeButtonPlaysTheEffectAndRecyclesForAnotherPulse()
        {
            EditorSceneManager.OpenScene(_scene);
            yield return new EnterPlayMode();

            ChargePulseController pulse = Object.FindFirstObjectByType<ChargePulseController>();
            UIDocument document = Object.FindFirstObjectByType<UIDocument>();
            ParticleSystem sparks = pulse.GetComponentInChildren<ParticleSystem>();
            Transform ring = pulse.transform.Find("Visuals/Ring");
            Camera camera = Camera.main;
            PanelSettings originalPanel = document.panelSettings;
            PanelSettings testPanel = Object.Instantiate(originalPanel);
            List<PulseStage> observed = new List<PulseStage>();
            pulse.StageChanged += observed.Add;
            document.panelSettings = testPanel;
            RenderTexture hudTarget = null;
            RenderTexture worldTarget = new RenderTexture(640, 360, 24);
            try
            {
                foreach (Vector2Int resolution in new[] { new Vector2Int(3840, 2160), new Vector2Int(1280, 720), new Vector2Int(1080, 1440) })
                {
                    if (hudTarget != null) Object.Destroy(hudTarget);
                    hudTarget = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGB32);
                    Color previous = testPanel.colorClearValue;
                    testPanel.colorClearValue = Color.clear;
                    testPanel.clearColor = true;
                    testPanel.targetTexture = hudTarget;
                    yield return null;
                    yield return null;
                    yield return null;

                    VisualElement viewport = document.rootVisualElement.Q("viewport");
                    Button control = viewport.Q<Button>("charge");
                    Rect bounds = control.worldBound;
                    float scale = resolution.x / viewport.worldBound.width;
                    Assert.That(scale, Is.EqualTo(Mathf.Min(resolution.x / 3840f, resolution.y / 2160f)).Within(0.02f));
                    Assert.That(bounds.height * scale, Is.GreaterThanOrEqualTo(44f));
                    Assert.That(viewport.worldBound.Contains(bounds.min) && viewport.worldBound.Contains(bounds.max), Is.True);
                    testPanel.colorClearValue = previous;

                    if (resolution.x == 3840) continue;
                    using (NativeReadback ui = new NativeReadback(hudTarget))
                    {
                        Assert.That(ui.CoveredPixels, Is.GreaterThan(1000), "The native PanelSettings target must contain the HUD.");
                        ui.SaveIfRequested("hud-" + resolution.x + ".png");
                    }
                }

                Button button = document.rootVisualElement.Q<Button>("charge");
                Label status = document.rootVisualElement.Q<Label>("status");
                Rect readyBounds = button.worldBound;
                float restScale = ring.localScale.x;
                camera.targetTexture = worldTarget;
                camera.Render();
                int idlePixels;
                using (NativeReadback floor = new NativeReadback(worldTarget)) idlePixels = floor.BrightPixels;
                Assert.That(idlePixels, Is.GreaterThan(100), "The resting ring and core must render.");

                Submit(button);
                Assert.That(pulse.Stage, Is.EqualTo(PulseStage.Prepare));
                Assert.That(status.text, Is.EqualTo("Charging"));
                Assert.That(button.enabledSelf, Is.False);
                pulse.StartPulse();
                pulse.ApplyAnimationStage((int)PulseStage.Released);
                Assert.That(observed, Is.EqualTo(new[] { PulseStage.Prepare }), "Duplicate requests and later phase events must leave preparation intact.");
                yield return WaitForStage(pulse, PulseStage.Released);
                Assert.That(button.worldBound, Is.EqualTo(readyBounds));
                Assert.That(ring.localScale.x, Is.GreaterThan(restScale * 4f));
                Assert.That(sparks.particleCount, Is.GreaterThan(0));

                camera.Render();
                using (NativeReadback active = new NativeReadback(worldTarget))
                {
                    Assert.That(active.BrightPixels, Is.GreaterThan(idlePixels * 2));
                    active.SaveIfRequested("pulse-released.png");
                }
                yield return WaitForStage(pulse, PulseStage.Recycled);
                CollectionAssert.AreEqual(new[] { PulseStage.Prepare, PulseStage.Preparing, PulseStage.Prepared, PulseStage.Releasing, PulseStage.Released, PulseStage.Retracting, PulseStage.Recycled }, observed);
                Assert.That(button.enabledSelf, Is.True);
                Assert.That(status.text, Is.EqualTo("Ready"));
                Assert.That(sparks.particleCount, Is.Zero);

                Submit(button);
                yield return WaitForStage(pulse, PulseStage.Releasing);
                pulse.enabled = false;
                Assert.That(ring.gameObject.activeInHierarchy, Is.False);
                Assert.That(sparks.particleCount, Is.Zero);
                pulse.enabled = true;
                yield return null;
                Assert.That(ring.localScale.x, Is.EqualTo(restScale).Within(0.001f));
                Submit(button);
                yield return WaitForStage(pulse, PulseStage.Recycled);
                Assert.That(button.enabledSelf, Is.True);
            }
            finally
            {
                pulse.StageChanged -= observed.Add;
                camera.targetTexture = null;
                testPanel.targetTexture = null;
                document.panelSettings = originalPanel;
                Object.Destroy(testPanel);
                Object.Destroy(worldTarget);
                if (hudTarget != null) Object.Destroy(hudTarget);
            }
        }

        [UnityTest]
        public IEnumerator SceneReentryReplacesTheEffectAndRetiresThePriorScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            ChargePulseController previous = null;
            for (int visit = 0; visit < 2; visit++)
            {
                yield return SceneManager.LoadSceneAsync(_scene, LoadSceneMode.Additive);
                Scene loaded = SceneManager.GetSceneByPath(_scene);
                Assert.That(loaded.isLoaded, Is.True);
                ChargePulseController pulse = Object.FindFirstObjectByType<ChargePulseController>();
                Assert.That(pulse.gameObject.scene, Is.EqualTo(loaded));
                Assert.That(pulse.Stage, Is.EqualTo(PulseStage.Recycled));
                if (visit > 0) Assert.That(pulse, Is.Not.SameAs(previous));
                pulse.StartPulse();
                yield return WaitForStage(pulse, PulseStage.Releasing);
                yield return SceneManager.UnloadSceneAsync(loaded);
                Assert.That(pulse == null, Is.True);
                Assert.That(SceneManager.GetSceneByPath(_scene).isLoaded, Is.False);
                previous = pulse;
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static void Submit(Button button)
        {
            button.Focus();
            using NavigationSubmitEvent input = NavigationSubmitEvent.GetPooled();
            input.target = button;
            button.SendEvent(input);
        }

        private static IEnumerator WaitForStage(ChargePulseController pulse, PulseStage stage)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (pulse.Stage != stage && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(pulse.Stage, Is.EqualTo(stage));
        }
    }
}
