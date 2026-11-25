using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using CanvasImage = UnityEngine.UI.Image;
using CanvasText = UnityEngine.UI.Text;

public sealed class ControlArtExample
{
    const string Art = "Assets/ImageExamples/UI/";
    static readonly Color32 Backdrop = new Color32(17, 33, 29, 255);
    static readonly Color32 Ink = new Color32(250, 240, 213, 255);
    static readonly Color32 Muted = new Color32(188, 210, 186, 255);
    static readonly Color32 Gold = new Color32(234, 199, 133, 255);

    [UnityTest]
    public IEnumerator OneFrameFitsQuestPanelAndActions()
    {
        if (!Application.isPlaying)
        {
            PrepareSprite("WoodlandFrame", 8);
            PrepareSprite("Watchtower", 0);
        }
        yield return new EnterPlayMode();
        // Reload the imported assets after the Edit Mode domain transition.
        Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "WoodlandFrame.png");
        Sprite tower = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Watchtower.png");
        Assert.That(frame.rect.size, Is.EqualTo(new Vector2(32, 32)));
        Assert.That(frame.border, Is.EqualTo(Vector4.one * 8));
        GameObject host = new GameObject("Trail journal");
        host.SetActive(false);
        PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
        RenderTexture target = new RenderTexture(800, 370, 24);
        try
        {
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/PulseTheme.tss");
            Assert.That(settings.themeStyleSheet, Is.Not.Null);
            settings.targetTexture = target;
            settings.clearColor = true;
            settings.colorClearValue = Backdrop;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1;
            settings.referenceSpritePixelsPerUnit = 1000;
            UIDocument document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            host.SetActive(true);
            VisualElement root = document.rootVisualElement;
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ResizableBacking.uss");
            Assert.That(sheet, Is.Not.Null);
            root.styleSheets.Add(sheet);
            root.style.backgroundColor = (Color)Backdrop;

            Caption(root, "Trail journal", 32, 14, 410, 56, 38, Ink, true);
            Caption(root, "Mosswood  /  Day 4", 534, 28, 234, 28, 19, Muted, false, TextAnchor.MiddleRight);
            VisualElement quest = new VisualElement();
            root.Add(quest);
            Place(quest, 32, 86, 735, 154);
            BindFrame(quest, frame);
            quest.AddToClassList("resizable-backing--tiled");
            VisualElement landmark = new VisualElement { pickingMode = PickingMode.Ignore };
            quest.Add(landmark);
            Place(landmark, 26, 25, 96, 96);
            landmark.style.backgroundImage = new StyleBackground(tower);
            Caption(quest, "ACTIVE QUEST", 142, 23, 465, 24, 16, Gold, true);
            Caption(quest, "The old watchtower", 142, 47, 550, 42, 31, Ink, true);
            Caption(quest, "Find the lost scout beyond Fern Hollow.", 142, 91, 550, 30, 20, Muted);

            int mapOpens = 0;
            int advances = 0;
            Button map = Action(root, frame, "Open map", 32, 261, 160, () => mapOpens++);
            Button advance = Action(root, frame, "Continue trail", 208, 261, 267, () => advances++);
            Caption(root, "2 of 3 landmarks found", 500, 276, 267, 34, 18, Muted, false, TextAnchor.MiddleRight);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(quest.resolvedStyle.unitySliceType, Is.EqualTo(SliceType.Tiled));
            Assert.That((quest.worldBound.width - 16) % 16, Is.Not.Zero, "The last tile must be partial.");
            foreach (VisualElement control in new VisualElement[] { quest, map, advance })
                Assert.That(control.resolvedStyle.backgroundImage.sprite, Is.SameAs(frame));
            Assert.That(landmark.worldBound.size, Is.EqualTo(Vector2.one * 96));
            Assert.That(landmark.resolvedStyle.backgroundImage.sprite, Is.SameAs(tower));
            Capture(target, "UI.png");
            ClickExtendedEdge(map);
            ClickExtendedEdge(advance);
            yield return null;
            Assert.That(mapOpens, Is.EqualTo(1));
            Assert.That(advances, Is.EqualTo(1));
            DrawCanvasControls(frame);
        }
        finally
        {
            host.SetActive(false);
            settings.targetTexture = null;
            Object.Destroy(host);
            Object.Destroy(settings);
            Object.Destroy(target);
        }
    }

    static void PrepareSprite(string name, int cut)
    {
        string path = Art + name + ".png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(importer, Is.Not.Null, path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1000;
        importer.spriteBorder = Vector4.one * cut;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static void BindFrame(VisualElement element, Sprite art)
    {
        element.AddToClassList("resizable-backing");
        element.style.backgroundImage = new StyleBackground(art);
    }

    static void Place(VisualElement element, int x, int y, int width, int height)
    {
        element.style.position = Position.Absolute;
        element.style.left = x;
        element.style.top = y;
        element.style.width = width;
        element.style.height = height;
    }

    static void Caption(VisualElement parent, string title, int x, int y, int width, int height,
        int fontSize, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
    {
        Label label = new Label(title) { pickingMode = PickingMode.Ignore };
        label.ClearClassList();
        parent.Add(label);
        Place(label, x, y, width, height);
        label.style.fontSize = fontSize;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.unityTextAlign = align;
        label.style.color = color;
    }

    static Button Action(VisualElement parent, Sprite frame, string title, int x, int y, int width, System.Action act)
    {
        Button button = new Button(act) { text = title };
        button.ClearClassList();
        parent.Add(button);
        Place(button, x, y, width, 60);
        BindFrame(button, frame);
        button.style.fontSize = 22;
        button.style.color = (Color)Ink;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        return button;
    }

    static void ClickExtendedEdge(Button button)
    {
        Vector2 at = new Vector2(button.worldBound.xMax - 4, button.worldBound.center.y);
        Assert.That(button.panel.Pick(at), Is.SameAs(button));
        Event pointer = new Event { type = EventType.MouseDown, button = 0, mousePosition = at };
        using (PointerDownEvent down = PointerDownEvent.GetPooled(pointer))
        {
            down.target = button;
            button.SendEvent(down);
        }
        pointer.type = EventType.MouseUp;
        using (PointerUpEvent up = PointerUpEvent.GetPooled(pointer))
        {
            up.target = button;
            button.SendEvent(up);
        }
    }

    static void DrawCanvasControls(Sprite frame)
    {
        GameObject canvasHost = new GameObject("Trail supplies", typeof(RectTransform), typeof(Canvas)) { layer = 31 };
        Camera camera = new GameObject("Canvas camera").AddComponent<Camera>();
        RenderTexture target = new RenderTexture(560, 136, 24);
        try
        {
            Canvas canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.referencePixelsPerUnit = 1000;
            canvas.worldCamera = camera;
            ((RectTransform)canvasHost.transform).sizeDelta = new Vector2(560, 136);
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = target.height / 2f;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.targetTexture = target;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CanvasControl(canvas, frame, "Rations", -150, 192, CanvasImage.Type.Sliced, font);
            CanvasControl(canvas, frame, "Gather supplies", 102, 273, CanvasImage.Type.Tiled, font);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Capture(target, "UI-Canvas.png");
        }
        finally
        {
            camera.targetTexture = null;
            Object.Destroy(canvasHost);
            Object.Destroy(camera.gameObject);
            Object.Destroy(target);
        }
    }

    static void CanvasControl(Canvas canvas, Sprite frame, string title, int x, int width, CanvasImage.Type type, Font font)
    {
        GameObject holder = new GameObject(title, typeof(RectTransform)) { layer = 31 };
        RectTransform rect = (RectTransform)holder.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchoredPosition = new Vector2(x, 0);
        rect.sizeDelta = new Vector2(width, 64);
        CanvasImage image = holder.AddComponent<CanvasImage>();
        image.sprite = frame;
        image.type = type;
        UnityEngine.UI.Button button = holder.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        GameObject textHost = new GameObject("Caption", typeof(RectTransform)) { layer = 31 };
        RectTransform textRect = (RectTransform)textHost.transform;
        textRect.SetParent(rect, false);
        textRect.sizeDelta = rect.sizeDelta;
        CanvasText text = textHost.AddComponent<CanvasText>();
        text.font = font;
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.color = Ink;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = title;
        text.raycastTarget = false;
        Assert.That(image.sprite, Is.SameAs(frame));
        Assert.That(image.hasBorder, Is.True);
    }

    static void Capture(RenderTexture target, string name)
    {
        string directory = Environment.GetEnvironmentVariable("IMAGE_EXAMPLE_OUTPUT");
        Assert.That(directory, Is.Not.Null.And.Not.Empty);
        Directory.CreateDirectory(directory);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            Object.Destroy(image);
        }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
