using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Qin.UnitySamples.Tests;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class IPhoneReplayPlayer : MonoBehaviour
{
    static readonly string[] Screens = { "Expedition", "ControllerSurface", "Hud", "Inventory", "Settings", "Preferences", "Dialog", "Quests", "Atlas", "Waypoint" };
    static readonly ScreenOrientation[] Orientations = { ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft, ScreenOrientation.LandscapeRight };
    [DllImport("__Internal")] static extern int ReplayInterfaceOrientation();
    [DllImport("__Internal")] static extern IntPtr ReplayRegionsJSON();
    UIDocument document;
    PanelSettings panel;
    string output;
    int failures;
    bool duo;
    bool waypointOnly;
    VisualElement retainedScreen;
    Observation retainedState;
    float nextGeometryCheck;

    [Serializable]
    sealed class Observation
    {
        public string pose, screen, requested, unityOrientation, model, locale, action, error, nativeRegions;
        public int width, height, uiKitOrientation, geometryUpdates;
        public Rect safe, subject, zoomOut;
        public Rect[] cutouts;
        public float pointScale, pixelsPerPoint, pixelsPerPanelUnit;
        public Rect primaryPaneTopLeft;
        public Vector2 hitSizeInPoints;
        public bool picked, edgesPicked, zoomOutPicked;
    }

    [Serializable]
    sealed class NativeMetrics { public float pointScale; public float[] windowTopLeft, primaryPaneTopLeft, upperCameraTopLeft; }

    IEnumerator Start()
    {
        output = Path.Combine(Application.persistentDataPath, "iphone-replay");
        Directory.CreateDirectory(output);
        Application.logMessageReceived += OnLog;
        panel = ScriptableObject.CreateInstance<PanelSettings>();
        panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("PulseTheme");
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.screenMatchMode = PanelScreenMatchMode.Expand;
        panel.clearColor = true;
        document = gameObject.AddComponent<UIDocument>();
        document.panelSettings = panel;
        duo = File.Exists(Path.Combine(Application.persistentDataPath, "duo-replay"));
        waypointOnly = File.Exists(Path.Combine(Application.persistentDataPath, "waypoint-only"));
        yield return Display(duo ? "outer" : "phone");
        Observation live = new Observation { screen = "Expedition", pose = "retained-rotation", model = SystemInfo.deviceModel };
        yield return Guard(Retained(duo, live), live);
        retainedScreen = null;
        retainedState = null;
        if (!string.IsNullOrEmpty(live.error))
            File.WriteAllText(Path.Combine(output, "retention-error.json"), JsonUtility.ToJson(live));
        if (duo && File.Exists(Path.Combine(output, "inner-again-ready")))
        {
            yield return Display("inner");
            File.WriteAllText(Path.Combine(output, "ready-for-half-matrix"), "Inner matrix complete.");
            float deadline = Time.realtimeSinceStartup + 120f;
            while (!File.Exists(Path.Combine(output, "half-matrix")) && Time.realtimeSinceStartup < deadline)
                yield return new WaitForSecondsRealtime(.2f);
            if (File.Exists(Path.Combine(output, "half-matrix"))) yield return Display("half");
        }
        File.WriteAllText(Path.Combine(output, "complete"), failures.ToString());
    }

    IEnumerator Display(string display)
    {
        foreach (ScreenOrientation requested in Orientations)
        {
            Screen.orientation = requested;
            float stop = Time.realtimeSinceStartup + 8f;
            do { yield return null; }
            while (Time.realtimeSinceStartup < stop && (Screen.orientation != requested || (Screen.height > Screen.width) != (requested == ScreenOrientation.Portrait)));
            yield return new WaitForSecondsRealtime(.4f);
            string pose = display + "-" + requested;
            // Reproduce the original proportional sizing before applying the native point floor.
            if (!waypointOnly && requested == ScreenOrientation.Portrait)
                foreach (string asset in new[] { "Expedition", "ControllerSurface" })
                {
                    Observation before = new Observation { pose = "before-" + pose, screen = asset, requested = requested.ToString(), model = SystemInfo.deviceModel };
                    yield return Guard(Case(before, false), before);
                    File.WriteAllText(Path.Combine(output, before.pose + "-" + asset + ".json"), JsonUtility.ToJson(before));
                }
            foreach (string asset in Screens)
            {
                if (waypointOnly && asset != "Waypoint") continue;
                Observation receipt = new Observation { pose = pose, screen = asset, requested = requested.ToString(), model = SystemInfo.deviceModel };
                IEnumerator work = Case(receipt, true);
                yield return Guard(work, receipt);
                File.WriteAllText(Path.Combine(output, pose + "-" + asset + ".json"), JsonUtility.ToJson(receipt));
            }
            File.WriteAllText(Path.Combine(output, pose + ".done"), "Measured at " + Screen.width + "x" + Screen.height);
        }
    }

    // Flatten nested Unity coroutines so one rejected action produces a receipt and the next screen still runs.
    IEnumerator Guard(IEnumerator work, Observation receipt)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(work);
        while (stack.Count > 0)
        {
            IEnumerator active = stack.Peek();
            bool next;
            try { next = active.MoveNext(); }
            catch (Exception e)
            {
                failures++;
                receipt.error = e.GetType().Name + ": " + e.Message;
                foreach (IEnumerator frame in stack) (frame as IDisposable)?.Dispose();
                yield break;
            }
            if (!next) { stack.Pop(); (active as IDisposable)?.Dispose(); continue; }
            if (active.Current is IEnumerator nested) stack.Push(nested);
            else yield return active.Current;
        }
    }

    IEnumerator Case(Observation o, bool honorPointFloor)
    {
        int width = Screen.width, height = Screen.height;
        o.width = width; o.height = height; o.safe = Screen.safeArea; o.cutouts = Screen.cutouts;
        o.unityOrientation = Screen.orientation.ToString(); o.uiKitOrientation = ReplayInterfaceOrientation();
        o.nativeRegions = Marshal.PtrToStringAnsi(ReplayRegionsJSON());
        document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UiCases/" + o.screen);
        VisualElement screen = document.rootVisualElement.Q("screen");
        float scale = Geometry(screen, o, honorPointFloor);
        LayoutText text = new LayoutText(Resources.Load<TextAsset>("UiCases/LayoutStrings"));
        foreach (TextElement label in screen.Query<TextElement>(className: "localized").ToList())
            text.Bind(label, o.screen.ToLowerInvariant() + "." + label.name);
        using var binder = new DeviceScreenBinder(o.screen, screen, text);
        text.SelectLocale(1);
        o.locale = text.Locale;
        // The OS may already have removed a full-width status region from the foreground pane.
        // Route only a local obstruction still inside that pane to the gameplay stick.
        if (o.cutouts != null)
        {
            Rect pane = o.primaryPaneTopLeft;
            foreach (Rect cutout in o.cutouts)
            {
                Rect local = Rect.MinMaxRect(Mathf.Max(pane.xMin, cutout.xMin), Mathf.Max(pane.yMin, height - cutout.yMax),
                                             Mathf.Min(pane.xMax, cutout.xMax), Mathf.Min(pane.yMax, height - cutout.yMin));
                if (local.width <= 0 || local.height <= 0) continue;
                binder.SetExclusion(new Rect(local.x / scale, local.y / scale, local.width / scale, local.height / scale));
                break;
            }
        }
        yield return null; yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        o.action = binder.Primary?.name;
        o.subject = binder.Primary?.worldBound ?? Rect.zero;
        o.picked = binder.Validate();
        o.hitSizeInPoints = o.subject.size * scale / o.pixelsPerPoint;
        o.edgesPicked = true;
        bool actionControl = binder.Primary is Button || o.screen == "Inventory";
        if (actionControl) o.edgesPicked = PicksActionEdges(screen, binder.Primary, 2f * o.pixelsPerPoint / scale);
        if (o.screen == "Waypoint")
        {
            Button secondary = screen.Q<Button>("zoom-out");
            o.zoomOut = secondary.worldBound;
            o.zoomOutPicked = PicksActionEdges(screen, secondary, 2f * o.pixelsPerPoint / scale);
            if (!o.zoomOutPicked || Mathf.Min(o.zoomOut.width, o.zoomOut.height) * scale / o.pixelsPerPoint < 44f - 1f / o.pixelsPerPoint)
                throw new InvalidOperationException("The zoom-out target did not provide five reachable points and a 44-point hit area.");
        }
        yield return CaptureFrame(o.pose + "-" + o.screen, width, height);
        if (!o.picked) throw new InvalidOperationException("The expected UI element did not receive the native panel pick.");
        if (honorPointFloor && actionControl && (!o.edgesPicked || Mathf.Min(o.hitSizeInPoints.x, o.hitSizeInPoints.y) < 44f - 1f / o.pixelsPerPoint))
            throw new InvalidOperationException("The action needs a reachable 44-point target at its center and four inset edges.");
        yield return binder.Exercise();
    }

    static bool PicksActionEdges(VisualElement screen, VisualElement action, float inset)
    {
        Rect bounds = action.worldBound;
        Vector2[] points = { bounds.center, new Vector2(bounds.xMin + inset, bounds.center.y),
                             new Vector2(bounds.xMax - inset, bounds.center.y), new Vector2(bounds.center.x, bounds.yMin + inset),
                             new Vector2(bounds.center.x, bounds.yMax - inset) };
        foreach (Vector2 point in points)
        {
            VisualElement hit = screen.panel.Pick(point);
            if (hit != action && (hit == null || !action.Contains(hit))) return false;
        }
        return true;
    }

    IEnumerator CaptureFrame(string name, int width, int height)
    {
        yield return new WaitForEndOfFrame();
        Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
        if (frame == null) throw new InvalidOperationException("The displayed Player did not produce a framebuffer.");
        try
        {
            if (frame.width != width || frame.height != height)
                throw new InvalidOperationException("Framebuffer does not match Screen dimensions.");
            File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG());
        }
        finally { Destroy(frame); }
    }

    float Geometry(VisualElement screen, Observation o, bool honorPointFloor)
    {
        int width = Screen.width, height = Screen.height;
        o.width = width; o.height = height; o.safe = Screen.safeArea; o.cutouts = Screen.cutouts;
        o.unityOrientation = Screen.orientation.ToString(); o.uiKitOrientation = ReplayInterfaceOrientation();
        o.nativeRegions = Marshal.PtrToStringAnsi(ReplayRegionsJSON());
        var native = JsonUtility.FromJson<NativeMetrics>(o.nativeRegions);
        if (native == null || native.pointScale <= 0 || native.windowTopLeft == null || native.windowTopLeft.Length != 4)
            throw new InvalidOperationException("The active window did not supply its size and backing scale.");
        float fromNative = width / native.windowTopLeft[2];
        float fromNativeY = height / native.windowTopLeft[3];
        o.pointScale = native.pointScale;
        o.pixelsPerPoint = native.pointScale * fromNative;
        bool portrait = height > width;
        float prop = portrait ? Mathf.Min(width / 540f, height / 960f) : Mathf.Min(width / 960f, height / 540f);
        float scale = Mathf.Max(honorPointFloor ? o.pixelsPerPoint : 1f, prop);
        o.pixelsPerPanelUnit = scale;
        panel.referenceResolution = portrait ? new Vector2Int(2160, 3840) : new Vector2Int(3840, 2160);
        panel.scale = 4f * scale / prop;
        Rect usable = o.safe;
        if (native.primaryPaneTopLeft != null && native.primaryPaneTopLeft.Length == 4)
        {
            float[] rect = native.primaryPaneTopLeft;
            usable = new Rect(rect[0] * fromNative, height - (rect[1] + rect[3]) * fromNativeY, rect[2] * fromNative, rect[3] * fromNativeY);
        }
        o.primaryPaneTopLeft = new Rect(usable.x, height - usable.yMax, usable.width, usable.height);
        float headerTop = (height - usable.yMax) / scale + 14f;
        bool outerPose = duo && (o.pose.StartsWith("outer-") || o.pose.StartsWith("before-outer-") ||
                                 o.pose.StartsWith("rotation-") || o.pose == "fold-before" || o.pose == "outer-return-ready");
        if (outerPose && native.upperCameraTopLeft != null && native.upperCameraTopLeft.Length == 4)
        {
            float[] camera = native.upperCameraTopLeft;
            float centerY = (camera[1] + camera[3] * .5f) * fromNativeY;
            headerTop = Mathf.Clamp(centerY / scale - 24f, (height - usable.yMax) / scale, (height - usable.yMin) / scale - 48f);
        }
        ApplyPane(screen, usable, scale, width, height, headerTop);
        return scale;
    }

    IEnumerator Retained(bool duo, Observation receipt)
    {
        Screen.orientation = ScreenOrientation.Portrait;
        yield return new WaitForSecondsRealtime(1f);
        document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UiCases/Expedition");
        VisualElement screen = document.rootVisualElement.Q("screen");
        Geometry(screen, receipt, true);
        retainedScreen = screen;
        retainedState = receipt;
        LayoutText text = new LayoutText(Resources.Load<TextAsset>("UiCases/LayoutStrings"));
        foreach (TextElement label in screen.Query<TextElement>(className: "localized").ToList())
            text.Bind(label, "expedition." + label.name);
        using var binder = new DeviceScreenBinder("Expedition", screen, text);
        text.SelectLocale(1);
        yield return null; yield return null;
        Click(screen.Q<Button>("open-route"));
        yield return null; yield return null;
        Button back = screen.Q<Button>("back");
        back.Focus();
        int identity = screen.GetHashCode();
        yield return RetainedFrame(screen, back, identity, receipt, "rotation-before");
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return new WaitForSecondsRealtime(1f);
        yield return RetainedFrame(screen, back, identity, receipt, "rotation-after");
        if (duo)
        {
            // Keep the supported landscape orientation while opening so the same page spans the inside display.
            yield return RetainedFrame(screen, back, identity, receipt, "fold-before");
            File.WriteAllText(Path.Combine(output, "ready-for-hinge"), "The briefing remains mounted.");
            foreach (string phase in new[] { "inner-ready", "outer-return-ready", "inner-again-ready" })
            {
                float deadline = Time.realtimeSinceStartup + 180f;
                bool halfRecorded = false;
                while (!File.Exists(Path.Combine(output, phase)) && !File.Exists(Path.Combine(output, "hinge-unavailable")) && Time.realtimeSinceStartup < deadline)
                {
                    if (!halfRecorded && phase == "inner-ready" && File.Exists(Path.Combine(output, "half-ready")))
                    {
                        yield return RetainedFrame(screen, back, identity, receipt, "half-ready");
                        halfRecorded = true;
                    }
                    yield return new WaitForSecondsRealtime(.2f);
                }
                if (!File.Exists(Path.Combine(output, phase))) break;
                yield return RetainedFrame(screen, back, identity, receipt, phase);
                float photographDeadline = Time.realtimeSinceStartup + 20f;
                while (!File.Exists(Path.Combine(output, phase + ".photographed")) && Time.realtimeSinceStartup < photographDeadline)
                    yield return new WaitForSecondsRealtime(.1f);
            }
        }
        Click(back);
        yield return null; yield return null;
        if (screen.Q("home").resolvedStyle.display == DisplayStyle.None)
            throw new InvalidOperationException("Return from the retained briefing did not restore the expedition menu.");
    }

    void Update()
    {
        if (retainedScreen == null || Time.unscaledTime < nextGeometryCheck) return;
        nextGeometryCheck = Time.unscaledTime + .1f;
        string native = Marshal.PtrToStringAnsi(ReplayRegionsJSON());
        if (retainedState.width == Screen.width && retainedState.height == Screen.height &&
            retainedState.safe == Screen.safeArea && retainedState.nativeRegions == native) return;
        Geometry(retainedScreen, retainedState, true);
        retainedState.geometryUpdates++;
    }

    IEnumerator RetainedFrame(VisualElement screen, Button back, int identity, Observation o, string label)
    {
        o.pose = label;
        // Sample the already-mounted layout after the independent runtime observer sees the change.
        yield return new WaitForSecondsRealtime(.35f);
        if (o.width != Screen.width || o.height != Screen.height || o.safe != Screen.safeArea ||
            o.nativeRegions != Marshal.PtrToStringAnsi(ReplayRegionsJSON()))
            throw new InvalidOperationException("The mounted page did not observe the current native window geometry.");
        yield return null; yield return null;
        bool same = ReferenceEquals(screen, document.rootVisualElement.Q("screen")) && screen.GetHashCode() == identity;
        bool focus = screen.focusController.focusedElement == back;
        bool open = screen.Q("briefing").resolvedStyle.display != DisplayStyle.None;
        o.picked = screen.panel.Pick(back.worldBound.center) == back;
        o.subject = back.worldBound;
        o.pose = label; o.action = "back"; o.locale = "de-DE"; o.requested = Screen.orientation.ToString();
        yield return CaptureFrame("retained-" + label, o.width, o.height);
        File.WriteAllText(Path.Combine(output, "retained-" + label + ".json"), JsonUtility.ToJson(o));
        File.WriteAllText(Path.Combine(output, "retained-" + label + ".txt"), $"root={identity} same={same} briefing={open} focus={focus} pick={o.picked}");
        if (!same || !focus || !open || !o.picked) throw new InvalidOperationException(label + " lost its mounted briefing, focus or action.");
    }

    static void Click(Button button)
    {
        if (button.panel.Pick(button.worldBound.center) != button) throw new InvalidOperationException("Cannot pick " + button.name);
        button.Focus();
        using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
        evt.target = button;
        button.SendEvent(evt);
    }

    static void ApplyPane(VisualElement screen, Rect safePixels, float scale, int width, int height, float headerTop)
    {
        float paneWidth = safePixels.width / scale;
        screen.EnableInClassList("wide-body", paneWidth >= 600f);
        screen.EnableInClassList("narrow-body", paneWidth < 400f);
        screen.EnableInClassList("compact-header", paneWidth < 442f);
        screen.EnableInClassList("small-pane", paneWidth < 346f);
        screen.EnableInClassList("shallow-pane", safePixels.height / scale < 340f);
        screen.EnableInClassList("short-body", safePixels.height / scale < 700f);
        VisualElement safe = screen.Q("safe");
        safe.style.left = safePixels.xMin / scale;
        safe.style.right = (width - safePixels.xMax) / scale;
        safe.style.top = (height - safePixels.yMax) / scale;
        safe.style.bottom = safePixels.yMin / scale;
        VisualElement header = screen.Q("header");
        if (header == null) return;
        header.style.left = safePixels.xMin / scale + 18f;
        header.style.right = (width - safePixels.xMax) / scale + 18f;
        header.style.top = headerTop;
        header.Q("center-title").style.top = paneWidth < 346f ? 54f : 0;
    }

    void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Assert || type == LogType.Error)
            File.AppendAllText(Path.Combine(output, "runtime-errors.txt"), message + "\n" + stack + "\n");
    }
}
