using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class UiCases
    {
        [UnityTest]
        public IEnumerator Ui03_LocaleAndInsetsPreserveTheHitRegionAndTheModalRestoresFocus()
        {
            yield return new EnterPlayMode();
            GameObject screen = new GameObject("Review screen");
            screen.SetActive(false);
            PanelSettings settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Prefabs/ChargePulse/ChargePulsePanel.asset"));
            RenderTexture target = new RenderTexture(1080, 1440, 0);
            settings.targetTexture = target;
            UIDocument document = screen.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Prefabs/ChargePulse/ReviewHud.uxml");
            ReviewScreenController controller = screen.AddComponent<ReviewScreenController>();
            screen.SetActive(true);
            try
            {
                VisualElement root = document.rootVisualElement;
                Button open = root.Q<Button>("open");
                Button back = root.Q<Button>("back");
                int deadlines = 0;
                root.schedule.Execute(() => deadlines++).StartingIn(1);
                IVisualElementScheduledItem canceled = root.schedule.Execute(() => deadlines++).StartingIn(1);
                canceled.Pause();
                Assert.That(deadlines, Is.Zero);
                ScrollView results = root.Q<ScrollView>("results");
                foreach (string title in new[] { "Prefab saved", "Curve sampled", "Renderer active", "Release observed", "Particles cleared", "Replay ready" })
                {
                    Label row = new Label(title);
                    row.AddToClassList("review-result");
                    results.Add(row);
                }

                yield return null;
                yield return null;
                controller.ApplyInsets(new Rect(0f, 0f, 1080f, 1440f), new Rect(24f, 32f, 1032f, 1328f));
                yield return null;
                results.scrollOffset = new Vector2(0f, 150f);
                yield return null;
                Vector2 savedOffset = results.scrollOffset;
                Assert.That(savedOffset.y, Is.GreaterThan(0f));
                Rect original = open.worldBound;
                controller.SetLanguage(ReviewLanguage.German);
                yield return null;
                Assert.That(open.worldBound, Is.EqualTo(original));
                Assert.That(root.Q<Label>("heading").text, Is.EqualTo("Überprüfung der Aufladeanimation"));
                float scale = 1080f / root.Q("review-viewport").worldBound.width;
                Assert.That(open.worldBound.height * scale, Is.GreaterThanOrEqualTo(44f));
                Assert.That(open.worldBound.xMin * scale, Is.GreaterThanOrEqualTo(24f));
                using (NativeReadback home = new NativeReadback(target)) home.SaveIfRequested("review-portrait-de.png");

                open.Focus();
                using (NavigationSubmitEvent input = NavigationSubmitEvent.GetPooled())
                {
                    input.target = open;
                    open.SendEvent(input);
                }
                yield return null;
                Assert.That(root.Q("home").enabledInHierarchy, Is.False);
                Assert.That(root.Q("details").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(back.focusController.focusedElement, Is.SameAs(back));
                Assert.That(root.Q<Label>("detail-heading").text, Is.EqualTo("Zeitpunkt der Freisetzung"));
                using (NativeReadback details = new NativeReadback(target)) details.SaveIfRequested("review-details-de.png");

                using (NavigationSubmitEvent input = NavigationSubmitEvent.GetPooled())
                {
                    input.target = back;
                    back.SendEvent(input);
                }
                yield return null;
                Assert.That(root.Q("home").enabledInHierarchy, Is.True);
                Assert.That(root.Q("details").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(open.focusController.focusedElement, Is.SameAs(open));
                Assert.That(results.scrollOffset, Is.EqualTo(savedOffset));
                Assert.That(deadlines, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(screen);
                settings.targetTexture = null;
                Object.Destroy(settings);
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator Ui04_ExistingCanvasKeepsItsAnchorAndConsumesItsOwnPointerEvent()
        {
            yield return new EnterPlayMode();
            GameObject host = new GameObject("Existing Canvas", typeof(RectTransform), typeof(Canvas));
            GameObject control = new GameObject("Anchored button", typeof(RectTransform));
            GameObject events = new GameObject("Fixture EventSystem", typeof(EventSystem));
            GameObject cameraObject = new GameObject("Canvas camera", typeof(Camera));
            RenderTexture target = new RenderTexture(320, 180, 24);
            Texture2D artwork = new Texture2D(12, 12, TextureFormat.RGBA32, false);
            Sprite backing = null;
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform frame = (RectTransform)host.transform;
                frame.sizeDelta = new Vector2(320f, 180f);
                RectTransform hit = (RectTransform)control.transform;
                hit.SetParent(frame, false);
                hit.anchorMin = Vector2.one;
                hit.anchorMax = Vector2.one;
                hit.pivot = Vector2.one;
                hit.anchoredPosition = new Vector2(-12f, -12f);
                hit.sizeDelta = new Vector2(80f, 48f);
                Color32 edge = new Color32(87, 232, 216, 255);
                Color32 middle = new Color32(29, 150, 147, 255);
                Color32[] pixels = new Color32[artwork.width * artwork.height];
                for (int row = 0; row < artwork.height; row++)
                    for (int column = 0; column < artwork.width; column++)
                        pixels[row * artwork.width + column] = row < 3 || row >= 9 || column < 3 || column >= 9 ? edge : middle;
                artwork.SetPixels32(pixels);
                artwork.Apply();
                backing = Sprite.Create(artwork, new Rect(0, 0, 12, 12), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * 3f);
                UnityEngine.UI.Image image = control.AddComponent<UnityEngine.UI.Image>();
                image.sprite = backing;
                image.type = UnityEngine.UI.Image.Type.Sliced;
                Assert.That(image.sprite.border, Is.EqualTo(Vector4.one * 3f));
                UnityEngine.UI.Button button = control.AddComponent<UnityEngine.UI.Button>();
                int requests = 0;
                button.onClick.AddListener(() => requests++);

                Camera camera = cameraObject.GetComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 90f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                yield return null;

                Vector3[] corners = new Vector3[4];
                hit.GetWorldCorners(corners);
                Assert.That(frame.rect.xMax - corners[2].x, Is.EqualTo(12f).Within(0.1f));
                frame.sizeDelta = new Vector2(240f, 180f);
                Canvas.ForceUpdateCanvases();
                hit.GetWorldCorners(corners);
                Assert.That(frame.rect.xMax - corners[2].x, Is.EqualTo(12f).Within(0.1f));
                Vector2 center = camera.WorldToScreenPoint(hit.TransformPoint(hit.rect.center));
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(hit, center, camera), Is.True);

                PointerEventData pointer = new PointerEventData(events.GetComponent<EventSystem>()) { position = center, button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(requests, Is.EqualTo(1));
                button.interactable = false;
                ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(requests, Is.EqualTo(1));
                camera.Render();
                using NativeReadback frameImage = new NativeReadback(target);
                Assert.That(frameImage.CoveredPixels, Is.GreaterThan(500));
                frameImage.SaveIfRequested("canvas-control.png");
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(events);
                Object.Destroy(cameraObject);
                Object.Destroy(target);
                Object.Destroy(backing);
                Object.Destroy(artwork);
            }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
