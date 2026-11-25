using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    sealed class CanvasConfirmation : IDisposable
    {
        const int FixtureLayer = 31;
        static readonly Color Ink = new Color32(240, 236, 227, 255);
        static readonly Color Muted = new Color32(188, 202, 213, 255);

        readonly GameObject host;
        readonly Camera camera;
        readonly EventSystem events;
        readonly Font font;
        readonly Dictionary<string, RectTransform> groups = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Text> captions = new Dictionary<string, Text>();
        readonly RectTransform dialog;
        readonly Vector3[] corners = new Vector3[4];
        RenderTexture target;
        string choice;

        public CanvasConfirmation(Font font)
        {
            this.font = font;
            host = new GameObject("Confirmation Canvas fixture", typeof(RectTransform), typeof(Canvas));
            host.layer = FixtureLayer;
            Canvas canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            camera = new GameObject("Confirmation Canvas camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.enabled = false;
            camera.orthographic = true;
            camera.cullingMask = 1 << FixtureLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(16, 24, 33, 255);
            canvas.worldCamera = camera;
            events = new GameObject("Confirmation Canvas input").AddComponent<EventSystem>();

            dialog = Group("dialog", canvas.transform, new Vector2(400, 204), Vector2.zero);
            dialog.gameObject.AddComponent<Image>().color = new Color32(37, 56, 74, 255);
            Caption("heading", Group("heading", dialog, new Vector2(352, 44), new Vector2(0, 66)), 32, Ink, FontStyle.Bold);
            Caption("message", Group("message", dialog, new Vector2(352, 44), new Vector2(0, 22)), 20, Muted, FontStyle.Normal);
            RectTransform actions = Group("actions", dialog, new Vector2(336, 48), new Vector2(0, -52));
            AddButton(actions, "stay", -84, new Color32(58, 82, 99, 255), Ink);
            AddButton(actions, "leave", 84, new Color32(239, 198, 129, 255), new Color32(19, 30, 43, 255));
        }

        public void ApplyLocale(LayoutText locale)
        {
            foreach (KeyValuePair<string, Text> caption in captions)
                caption.Value.text = locale.Resolve($"dialog.{caption.Key}");
        }

        public string CaptionText(string name) => captions[name].text;

        public void Draw(Vector2Int size, Rect safe, float scale, string profile)
        {
            choice = null;
            dialog.gameObject.SetActive(true);
            RenderTexture next = new RenderTexture(size.x, size.y, 24);
            camera.targetTexture = next;
            Object.Destroy(target);
            target = next;
            camera.orthographicSize = size.y / (2f * scale);
            float width = Mathf.Min(400f, safe.width / scale * .96f);
            dialog.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            float captionWidth = Mathf.Min(352f, width * .95f);
            groups["heading"].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, captionWidth);
            groups["message"].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, captionWidth);
            dialog.anchoredPosition = new Vector2(safe.center.x - size.x / 2f, size.y / 2f - safe.center.y) / scale;
            Canvas.ForceUpdateCanvases();
            foreach (Text caption in captions.Values)
            {
                Assert.That(caption.preferredWidth, Is.LessThanOrEqualTo(caption.rectTransform.rect.width), caption.text);
                Assert.That(caption.preferredHeight, Is.LessThanOrEqualTo(caption.rectTransform.rect.height), caption.text);
            }
            camera.Render();
            using NativeReadback image = new NativeReadback(next);
            Assert.That(image.BrightPixels, Is.GreaterThan(100), profile);
            image.SaveIfRequested($"canvas-dialog-{profile}.png");
        }

        public Rect Bounds(string name)
        {
            groups[name].GetWorldCorners(corners);
            Vector3 bottomLeft = camera.WorldToScreenPoint(corners[0]);
            Vector3 topRight = camera.WorldToScreenPoint(corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, target.height - topRight.y, topRight.x, target.height - bottomLeft.y);
        }

        public string Choose(string name)
        {
            RectTransform button = groups[name];
            Vector2 center = camera.WorldToScreenPoint(button.TransformPoint(button.rect.center));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(button, center, camera), Is.True, name);
            PointerEventData press = new PointerEventData(events) { position = center, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerClickHandler);
            Assert.That(dialog.gameObject.activeSelf, Is.False);
            return choice;
        }

        void AddButton(RectTransform actions, string name, float offset, Color face, Color ink)
        {
            RectTransform bounds = Group(name, actions, new Vector2(152, 48), new Vector2(offset, 0f));
            Image image = bounds.gameObject.AddComponent<Image>();
            image.color = face;
            Button button = bounds.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                choice = name;
                dialog.gameObject.SetActive(false);
            });
            Caption(name, Group(name + "-caption", bounds, bounds.sizeDelta, Vector2.zero), 20, ink, FontStyle.Bold);
        }

        RectTransform Group(string name, Transform parent, Vector2 size, Vector2 offset)
        {
            GameObject group = new GameObject(name, typeof(RectTransform)) { layer = FixtureLayer };
            RectTransform rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            groups.Add(name, rect);
            return rect;
        }

        void Caption(string key, RectTransform slot, int size, Color ink, FontStyle style)
        {
            Text text = slot.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = ink;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            captions.Add(key, text);
        }

        public void Dispose()
        {
            camera.targetTexture = null;
            Object.Destroy(target);
            Object.Destroy(events.gameObject);
            Object.Destroy(camera.gameObject);
            Object.Destroy(host);
        }
    }
}
