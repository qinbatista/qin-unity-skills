using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed partial class ResponsiveLayoutCases
    {
        [UnityTest]
        public IEnumerator WaypointTracksClampsAndDragsInTheActivePane()
        {
            GameObject rig = new GameObject("Waypoint projection");
            rig.transform.position = new Vector3(0, 0, -10);
            Camera camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5;
            WaypointMap map = null;
            try
            {
                IEnumerator Check(VisualElement screen, LayoutProfile profile, LayoutText text, RenderTexture target)
                {
                    Rect toolbar = screen.Q("map-toolbar").worldBound;
                    Button zoomIn = screen.Q<Button>("zoom-in");
                    Button zoomOut = screen.Q<Button>("zoom-out");
                    foreach (Button button in new[] { zoomOut, zoomIn })
                    {
                        AssertInside(button.worldBound, toolbar, button.name);
                        Assert.That(button.panel.Pick(button.worldBound.center), Is.SameAs(button), button.name);
                        foreach (Rect obstruction in profile.PanelReservations)
                            Assert.That(button.worldBound.Overlaps(obstruction), Is.False, button.name + " overlaps " + obstruction);
                    }
                    Rect natural = Rect.MinMaxRect(toolbar.xMax - (zoomIn.worldBound.xMax - zoomOut.worldBound.xMin),
                        toolbar.yMin, toolbar.xMax, toolbar.yMax);
                    if (!Array.Exists(profile.PanelReservations, area => area.Overlaps(natural)))
                        Assert.That(zoomIn.worldBound.xMax, Is.EqualTo(toolbar.xMax).Within(Tolerance), "restored toolbar anchor");
                    yield return CheckWaypoint(map, screen);
                    if (profile.Name != Profiles[Profiles.Length - 1].Name || text.Locale != text.Locales[text.Locales.Count - 1])
                        yield break;

                    // A replaced document can still receive layout and paint callbacks for one frame.
                    map.Dispose();
                    Object.Destroy(rig);
                    yield return null;
                    float before = map.Surface.worldBound.width;
                    map.Surface.style.width = before - 24f;
                    map.Surface.MarkDirtyRepaint();
                    Submit(screen.Q<Button>("zoom-in"));
                    yield return null;
                    yield return null;
                    Assert.That(map.Surface.worldBound.width, Is.LessThan(before - 10f), "retired map relayout");
                }
                yield return Review("Waypoint", (screen, text) => map = new WaypointMap(screen, camera), Check,
                    profile => map.SetExclusion(new Rect(profile.Cutout.position / profile.Scale, profile.Cutout.size / profile.Scale)));
            }
            finally
            {
                map?.Dispose();
                if (rig != null) Object.Destroy(rig);
            }
        }

        static IEnumerator CheckWaypoint(WaypointMap map, VisualElement screen)
        {
            Rect surface = map.Surface.worldBound;
            AssertInside(surface, screen.Q("safe").worldBound, "map viewport");
            AssertInside(screen.Q("map-toolbar").worldBound, screen.Q("safe").worldBound, "map zoom");
            Vector3 landmark = new Vector3(1.25f, 0.8f, 0);
            map.SetWaypoint(landmark);
            yield return null;
            Rect pin = map.Pin.worldBound;
            Assert.That(pin.height, Is.GreaterThanOrEqualTo(47f));
            Assert.That(pin.width, Is.EqualTo(pin.height).Within(Tolerance));
            Assert.That(Vector2.Distance(pin.center, map.Surface.LocalToWorld(map.Project(landmark))), Is.LessThan(Tolerance + .5f));

            Submit(screen.Q<Button>("zoom-in"));
            yield return null;
            Assert.That(Vector2.Distance(map.Pin.worldBound.center, surface.center),
                Is.GreaterThan(Vector2.Distance(pin.center, surface.center) + 1f), "zoom moves the projected world anchor");
            Submit(screen.Q<Button>("zoom-out"));
            yield return null;
            Assert.That(Vector2.Distance(map.Pin.worldBound.center, pin.center), Is.LessThan(Tolerance + .5f));

            map.SetWaypoint(new Vector3(100, -100, 0));
            yield return null;
            AssertInside(map.Pin.worldBound, surface, "offscreen waypoint remains reachable");
            Assert.That(map.Pin.worldBound.xMax, Is.EqualTo(surface.xMax).Within(Tolerance));
            Assert.That(map.Pin.worldBound.yMax, Is.EqualTo(surface.yMax).Within(Tolerance));

            Vector2 down = map.Pin.worldBound.center;
            Assert.That(map.Pin.panel.Pick(down), Is.SameAs(map.Pin));
            using (PointerDownEvent press = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = down, button = 0 }))
            { press.target = map.Pin; map.Pin.SendEvent(press); }
            yield return null;
            Assert.That(map.Pin.HasPointerCapture(PointerId.mousePointerId), Is.True);
            Vector2 destination = surface.position + surface.size * .38f;
            using (PointerMoveEvent drag = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = destination, button = 0 }))
            { drag.target = map.Pin; map.Pin.SendEvent(drag); }
            using (PointerUpEvent release = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = destination, button = 0 }))
            { release.target = map.Pin; map.Pin.SendEvent(release); }
            yield return null;
            Assert.That(map.Pin.HasPointerCapture(PointerId.mousePointerId), Is.False);
            Assert.That(Vector2.Distance(map.Pin.worldBound.center, destination), Is.LessThan(Tolerance + .5f));
            map.SetWaypoint(new Vector3(0, 0, -20));
            yield return null;
            Assert.That(map.Pin.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "target behind the camera");
            map.SetWaypoint(landmark);
            yield return null;
        }

        sealed class WaypointMap : IDisposable
        {
            static readonly Vector3[] Trail =
            {
                new Vector3(-8, -4), new Vector3(-5, -2.5f), new Vector3(-3, -2.8f),
                new Vector3(-1, -.4f), new Vector3(-.9f, .6f), new Vector3(1.25f, .8f)
            };
            readonly Camera _camera;
            readonly VisualElement _toolbar;
            readonly Button _zoomIn, _zoomOut;
            Rect _exclusion;
            int _pointer = -1;
            Vector3 _worldPoint = new Vector3(1.25f, .8f, 0);
            public VisualElement Surface { get; }
            public VisualElement Pin { get; }

            public WaypointMap(VisualElement screen, Camera camera)
            {
                _camera = camera;
                Surface = screen.Q("map");
                Pin = screen.Q("pin");
                _toolbar = screen.Q("map-toolbar");
                _zoomIn = screen.Q<Button>("zoom-in");
                _zoomOut = screen.Q<Button>("zoom-out");
                _toolbar.RegisterCallback<GeometryChangedEvent>(ToolbarLayout);
                Surface.RegisterCallback<GeometryChangedEvent>(OnLayout);
                Surface.generateVisualContent += DrawMap;
                _zoomIn.clicked += ZoomIn;
                _zoomOut.clicked += ZoomOut;
                Pin.RegisterCallback<PointerDownEvent>(BeginDrag);
                Pin.RegisterCallback<PointerMoveEvent>(Drag);
                Pin.RegisterCallback<PointerUpEvent>(Release);
                Pin.RegisterCallback<PointerCancelEvent>(Cancel);
                Pin.RegisterCallback<PointerCaptureOutEvent>(CaptureEnded);
            }

            public void Dispose()
            {
                _toolbar.UnregisterCallback<GeometryChangedEvent>(ToolbarLayout);
                EndDrag(_pointer);
                Surface.UnregisterCallback<GeometryChangedEvent>(OnLayout);
                Surface.generateVisualContent -= DrawMap;
                _zoomIn.clicked -= ZoomIn;
                _zoomOut.clicked -= ZoomOut;
                Pin.UnregisterCallback<PointerDownEvent>(BeginDrag);
                Pin.UnregisterCallback<PointerMoveEvent>(Drag);
                Pin.UnregisterCallback<PointerUpEvent>(Release);
                Pin.UnregisterCallback<PointerCancelEvent>(Cancel);
                Pin.UnregisterCallback<PointerCaptureOutEvent>(CaptureEnded);
            }

            public void SetExclusion(Rect panelExclusion)
            {
                _exclusion = panelExclusion;
                PlaceToolbar();
            }

            void ToolbarLayout(GeometryChangedEvent evt) => PlaceToolbar();

            void PlaceToolbar()
            {
                Rect row = _toolbar.worldBound;
                if (row.width <= 0f || row.height <= 0f) return;
                // The two zoom buttons share a trailing anchor. Measure their unshifted span so
                // adding clearance cannot toggle it off on the next layout pass.
                float span = _zoomOut.resolvedStyle.width + _zoomIn.resolvedStyle.marginLeft + _zoomIn.resolvedStyle.width;
                Rect actions = new Rect(row.xMax - span, row.yMin, span, row.height);
                float clearance = actions.Overlaps(_exclusion) ? row.xMax - _exclusion.xMin + 4f : 0f;
                if (!Mathf.Approximately(_toolbar.resolvedStyle.paddingRight, clearance))
                    _toolbar.style.paddingRight = clearance;
            }

            void OnLayout(GeometryChangedEvent evt)
            {
                Rect area = Surface.contentRect;
                if (area.height <= 0 || area.width <= 0) return;
                _camera.aspect = area.width / area.height;
                UpdatePin();
            }

            void ZoomIn() => Zoom(.8f);
            void ZoomOut() => Zoom(1.25f);

            void BeginDrag(PointerDownEvent evt)
            {
                if (evt.button != 0 || _pointer >= 0) return;
                _pointer = evt.pointerId;
                Pin.Focus();
                Pin.CapturePointer(_pointer);
                Pin.AddToClassList("dragging");
                evt.StopPropagation();
            }

            void Drag(PointerMoveEvent evt)
            {
                if (evt.pointerId != _pointer) return;
                Vector2 local = Surface.WorldToLocal(evt.position);
                Rect area = Surface.contentRect;
                SetWaypoint(_camera.ViewportToWorldPoint(new Vector3(Mathf.Clamp01(local.x / area.width),
                    1f - Mathf.Clamp01(local.y / area.height), -_camera.transform.position.z)));
            }

            void Release(PointerUpEvent evt) => EndDrag(evt.pointerId);
            void Cancel(PointerCancelEvent evt) => EndDrag(evt.pointerId);
            void CaptureEnded(PointerCaptureOutEvent evt) => EndDrag(evt.pointerId);

            void EndDrag(int pointer)
            {
                if (pointer < 0 || _pointer != pointer) return;
                _pointer = -1;
                if (Pin.HasPointerCapture(pointer)) Pin.ReleasePointer(pointer);
                Pin.RemoveFromClassList("dragging");
            }

            void Zoom(float factor)
            {
                _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * factor, 2f, 12f);
                UpdatePin();
                Surface.MarkDirtyRepaint();
            }

            public Vector2 Project(Vector3 point)
            {
                Vector3 view = _camera.WorldToViewportPoint(point);
                Rect area = Surface.contentRect;
                return new Vector2(view.x * area.width, (1 - view.y) * area.height);
            }

            public void SetWaypoint(Vector3 point)
            {
                _worldPoint = point;
                UpdatePin();
            }

            void UpdatePin()
            {
                Rect area = Surface.contentRect;
                Vector2 point = Project(_worldPoint);
                Pin.style.display = _camera.WorldToViewportPoint(_worldPoint).z > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                float half = Pin.resolvedStyle.width * .5f;
                if (!float.IsFinite(half) || half <= 0) return;
                Pin.style.left = Mathf.Clamp(point.x, half, area.width - half) - half;
                Pin.style.top = Mathf.Clamp(point.y, half, area.height - half) - half;
            }

            void DrawMap(MeshGenerationContext context)
            {
                var pen = context.painter2D;
                pen.lineWidth = 1.5f;
                pen.strokeColor = new Color32(85, 119, 93, 255);
                for (int ring = 1; ring <= 10; ring++)
                {
                    pen.BeginPath();
                    for (int i = 0; i <= 72; i++)
                    {
                        float theta = i * Mathf.PI / 36f;
                        float radius = ring * (.81f + .12f * Mathf.Sin(3 * theta) + .10f * Mathf.Cos(5 * theta));
                        Vector2 position = Project(new Vector3(-2 + radius * Mathf.Cos(theta), 1 + .72f * radius * Mathf.Sin(theta), 0));
                        if (i == 0) pen.MoveTo(position); else pen.LineTo(position);
                    }
                    pen.ClosePath();
                    pen.Stroke();
                }
                pen.lineWidth = 3;
                pen.strokeColor = new Color32(227, 190, 116, 255);
                pen.BeginPath();
                pen.MoveTo(Project(Trail[0]));
                for (int i = 1; i < Trail.Length; i++) pen.LineTo(Project(Trail[i]));
                pen.Stroke();
            }
        }
    }
}
