using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    public sealed partial class ResponsiveLayoutCases
    {
        [UnityTest]
        public IEnumerator ControllerSurfaceKeepsIndependentTouchAndNavigation()
        {
            ControllerSurface surface = null;
            yield return Review("ControllerSurface", (screen, _) => surface = new ControllerSurface(screen),
                (screen, profile, strings, target) => CheckControllerSurface(screen, surface, profile, strings, target),
                profile => surface.SetExclusion(ControllerExclusion(profile)));
        }

        // The safe/selected pane already clears full edge strips and separating dividers.
        // Give the local joystick owner only the part of the aperture still inside that foreground.
        static Rect ControllerExclusion(LayoutProfile profile)
        {
            if (!profile.Safe.Overlaps(profile.Cutout)) return default;
            Vector2 low = Vector2.Max(profile.Safe.min, profile.Cutout.min) / profile.Scale;
            Vector2 high = Vector2.Min(profile.Safe.max, profile.Cutout.max) / profile.Scale;
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }

        static IEnumerator CheckControllerSurface(VisualElement screen, ControllerSurface surface, LayoutProfile profile,
            LayoutText strings, RenderTexture target)
        {
            VisualElement stick = screen.Q("stick");
            VisualElement thumb = screen.Q("thumb");
            VisualElement marker = screen.Q("marker");
            Button pulse = screen.Q<Button>("pulse");
            Button pause = screen.Q<Button>("pause");
            Button resume = screen.Q<Button>("resume");
            Rect pane = screen.Q("safe").worldBound;
            string cell = profile.Name + "-" + strings.Locale;
            bool captureState = strings.Locale == "en-US" && (profile.Name == "compact-portrait" ||
                profile.Name == "duo-outer-landscape-right" || profile.Name == "separating-left-pane");
            foreach (VisualElement element in new VisualElement[] { stick, pulse, pause, screen.Q("field") })
            {
                AssertInside(element.worldBound, pane, cell + " " + element.name);
                foreach (Rect reservation in profile.PanelReservations)
                    Assert.That(element.worldBound.Overlaps(reservation), Is.False, cell + " " + element.name + " overlaps " + reservation);
            }
            foreach (VisualElement control in new VisualElement[] { stick, pulse, pause })
            {
                Assert.That(control.worldBound.width, Is.GreaterThanOrEqualTo(44f), cell + " " + control.name);
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(44f), cell + " " + control.name);
                Assert.That(screen.panel.Pick(control.worldBound.center), Is.SameAs(control), cell + " " + control.name);
            }
            Assert.That(stick.worldBound.xMax, Is.LessThan(pulse.worldBound.xMin));
            Assert.That(pause.worldBound.yMax, Is.LessThan(pulse.worldBound.yMin));
            AssertControllerCaption(pause, cell);
            AssertControllerCaption(pulse, cell);
            Color32 pauseFace = pause.resolvedStyle.backgroundColor;
            Color32 pulseFace = pulse.resolvedStyle.backgroundColor;
            Rect inputBounds = stick.worldBound;
            Rect pulseBounds = pulse.worldBound;
            Vector2 center = inputBounds.center;
            Vector2 markerHome = marker.worldBound.center;
            Vector2 beyondEdge = center + new Vector2(150f, -75f);
            int actionsBefore = surface.Pulses;
            int moveId = PointerId.touchPointerIdBase;
            if (strings.Locale == strings.Locales[0] && profile.Name != Profiles[0].Name)
            {
                // The last cell left a finger down across this window change.
                Assert.That(surface.Direction, Is.EqualTo(Vector2.zero), cell + " resize reset movement");
                Assert.That(stick.HasPointerCapture(moveId), Is.False, cell + " resize released contact");
                LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Ended, center);
            }
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, center);
            yield return null;
            Assert.That(stick.HasPointerCapture(moveId), Is.True, cell + " first contact");
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Moved, beyondEdge);
            yield return null;
            Assert.That(surface.Direction.magnitude, Is.EqualTo(1f).Within(0.01f), cell + " clamp");
            Assert.That(surface.Direction.x, Is.Positive);
            Assert.That(surface.Direction.y, Is.Positive);
            AssertInside(thumb.worldBound, stick.worldBound, cell + " thumb");
            Assert.That(marker.worldBound.center.x, Is.GreaterThan(markerHome.x + 15f), cell + " gameplay response");

            LayoutInput.SendTouch(screen.panel, 1, TouchPhase.Began, pulseBounds.center);
            yield return null;
            LayoutInput.SendTouch(screen.panel, 1, TouchPhase.Ended, pulseBounds.center);
            yield return null;
            Assert.That(surface.Pulses, Is.EqualTo(actionsBefore + 1), cell + " independent second contact");
            Assert.That(stick.HasPointerCapture(moveId), Is.True, cell + " action interrupted movement");
            Assert.That(surface.Direction.magnitude, Is.EqualTo(1f).Within(0.01f));
            AssertControllerBounds(stick.worldBound, inputBounds, profile.Scale, cell + " stick held");
            AssertControllerBounds(pulse.worldBound, pulseBounds, profile.Scale, cell + " pulse held");
            if (captureState)
                using (NativeReadback image = new NativeReadback(target)) image.SaveIfRequested($"controller-held-{cell}.png");
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Ended, beyondEdge);
            yield return null;
            Assert.That(surface.Direction, Is.EqualTo(Vector2.zero), cell + " release");
            Assert.That(stick.HasPointerCapture(moveId), Is.False);
            Assert.That(Vector2.Distance(thumb.worldBound.center, center), Is.LessThan(Tolerance));
            Assert.That(Vector2.Distance(marker.worldBound.center, markerHome), Is.LessThan(Tolerance));

            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, center + Vector2.right * 16f);
            Assert.That(surface.Direction.x, Is.Positive);
            LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Canceled, center);
            Assert.That(surface.Direction, Is.EqualTo(Vector2.zero), cell + " touch cancellation");

            pulse.Focus();
            yield return null;
            LayoutInput.MoveFocus(pulse, NavigationMoveEvent.Direction.Up);
            yield return null;
            Assert.That(screen.focusController.focusedElement, Is.SameAs(pause), cell + " d-pad up");
            LayoutInput.MoveFocus(pause, NavigationMoveEvent.Direction.Down);
            yield return null;
            Assert.That(screen.focusController.focusedElement, Is.SameAs(pulse), cell + " d-pad down");
            Assert.That((Color32)pulse.resolvedStyle.borderTopColor, Is.EqualTo((Color32)Color.white), cell + " focus ring");
            AssertControllerBounds(pulse.worldBound, pulseBounds, profile.Scale, cell + " focused pulse");
            // Submit to the focused control without explicitly assigning focus or invoking clicked.
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) pulse.SendEvent(submit);
            Assert.That(surface.Pulses, Is.EqualTo(actionsBefore + 2), cell + " navigation submit");
            LayoutInput.MoveFocus(pulse, NavigationMoveEvent.Direction.Up);
            yield return null;
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) pause.SendEvent(submit);
            yield return null;
            yield return null;
            Assert.That(surface.Paused, Is.True);
            Assert.That(screen.focusController.focusedElement, Is.SameAs(resume), cell + " pause focus");
            Assert.That(pulse.enabledInHierarchy, Is.False);
            Assert.That(screen.panel.Pick(pulseBounds.center), Is.Not.SameAs(pulse));
            AssertTextFits(resume);
            AssertTextFits(screen.Q<Label>("paused"));
            if (captureState)
                using (NativeReadback image = new NativeReadback(target)) image.SaveIfRequested($"controller-paused-{cell}.png");
            using (NavigationCancelEvent cancel = NavigationCancelEvent.GetPooled()) resume.SendEvent(cancel);
            yield return null;
            Assert.That(surface.Paused, Is.False);
            Assert.That(screen.focusController.focusedElement, Is.SameAs(pause), cell + " cancel restored opener");
            Assert.That(screen.panel.Pick(pulseBounds.center), Is.SameAs(pulse));
            AssertControllerCaption(pause, cell + " restored opener");
            // Native Submit briefly retains :active. The next canonical viewport should show its idle faces.
            yield return new WaitUntil(() => ((Color32)pause.resolvedStyle.backgroundColor).Equals(pauseFace) &&
                ((Color32)pulse.resolvedStyle.backgroundColor).Equals(pulseFace), TimeSpan.FromSeconds(3),
                () => Assert.Fail(cell + " action faces failed to settle"));
            yield return null;
            if (strings.Locale == strings.Locales[strings.Locales.Count - 1] && profile.Name != Profiles[Profiles.Length - 1].Name)
            {
                LayoutInput.SendTouch(screen.panel, 0, TouchPhase.Began, center + Vector2.right * 18f);
                Assert.That(surface.Direction.x, Is.Positive, cell + " contact before resize");
            }
        }

        static void AssertControllerCaption(Button button, string cell)
        {
            float Luminance(Color color)
            {
                Color linear = color.linear;
                return 0.2126f * linear.r + 0.7152f * linear.g + 0.0722f * linear.b;
            }
            float ink = Luminance(button.resolvedStyle.color);
            float face = Luminance(button.resolvedStyle.backgroundColor);
            float ratio = (Mathf.Max(ink, face) + 0.05f) / (Mathf.Min(ink, face) + 0.05f);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(4.5f), cell + " " + button.name + " caption contrast");
        }

        static void AssertControllerBounds(Rect result, Rect initial, float scale, string context)
        {
            float drift = Mathf.Max(Vector2.Distance(result.min, initial.min), Vector2.Distance(result.max, initial.max));
            Assert.That(drift * scale, Is.LessThan(0.1f), context + " input corner drift in framebuffer pixels");
        }

    }
}
