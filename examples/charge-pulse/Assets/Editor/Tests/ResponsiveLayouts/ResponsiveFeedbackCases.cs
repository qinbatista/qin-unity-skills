using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    public sealed partial class ResponsiveLayoutCases
    {
        static Func<bool> CaptureIdleButtonStyles(VisualElement screen)
        {
            var idle = screen.Query<Button>().ToList()
                .Where(button => button.ClassListContains("wing") || button.ClassListContains("dialog-button"))
                .Select(button => (Button: button, Face: (Color32)button.resolvedStyle.backgroundColor))
                .ToArray();
            if (idle.Length == 0) return null;
            // Border widths snap to device pixels at the active panel scale. Compare both edges in the current frame.
            return () => idle.All(item => ((Color32)item.Button.resolvedStyle.backgroundColor).Equals(item.Face) &&
                Mathf.Approximately(item.Button.resolvedStyle.borderBottomWidth, item.Button.resolvedStyle.borderTopWidth));
        }

        static IEnumerator CheckButtonFeedback(VisualElement screen, Button button, RenderTexture target, string profile)
        {
            button.Focus();
            // The preceding keyboard submit briefly sets :active before the release transition.
            yield return new WaitForSecondsRealtime(0.30f);
            Color rest = button.resolvedStyle.backgroundColor;
            float idleEdge = button.resolvedStyle.borderBottomWidth;
            Rect bounds = button.worldBound;
            Button neighbor = screen.Q<Button>("right-wing");
            Rect neighborBounds = neighbor.worldBound;
            Assert.That(button.focusController.focusedElement, Is.SameAs(button));
            Assert.That(button.resolvedStyle.borderTopColor.a, Is.EqualTo(1f));
            using (NativeReadback image = new NativeReadback(target))
                image.SaveIfRequested($"feedback-{profile}-focus.png");

            int actions = 0;
            void CountAction() => actions++;
            button.clicked += CountAction;
            try
            {
                foreach (bool reduceMotion in new[] { false, true })
                {
                    screen.EnableInClassList("reduced-motion", reduceMotion);
                    yield return null;
                    Assert.That(button.resolvedStyle.transitionDuration.Single().value, reduceMotion ? Is.Zero : Is.Positive);
                    Assert.That(button.panel.Pick(bounds.center), Is.SameAs(button));
                    Event down = new Event { type = EventType.MouseDown, button = 0, mousePosition = bounds.center };
                    using (PointerDownEvent press = PointerDownEvent.GetPooled(down))
                    {
                        press.target = button;
                        button.SendEvent(press);
                    }
                    yield return null;
                    if (!reduceMotion) yield return new WaitForSecondsRealtime(0.16f);
                    yield return null;
                    Assert.That(button.resolvedStyle.backgroundColor, Is.Not.EqualTo(rest), "held press needs visible feedback");
                    Assert.That(button.resolvedStyle.borderBottomWidth, Is.GreaterThan(idleEdge), "held press needs a non-color cue");
                    Assert.That(button.worldBound, Is.EqualTo(bounds), "feedback changed the input bounds");
                    Assert.That(neighbor.worldBound, Is.EqualTo(neighborBounds));
                    Assert.That(button.panel.Pick(bounds.min + Vector2.one * 4f), Is.SameAs(button));
                    using (NativeReadback image = new NativeReadback(target))
                        image.SaveIfRequested(reduceMotion ? $"feedback-{profile}-reduced.png" : $"feedback-{profile}-held.png");

                    Event up = new Event { type = EventType.MouseUp, button = 0, mousePosition = bounds.center };
                    if (reduceMotion)
                    {
                        using PointerUpEvent release = PointerUpEvent.GetPooled(up);
                        release.target = button;
                        button.SendEvent(release);
                    }
                    else
                    {
                        using PointerCancelEvent cancel = PointerCancelEvent.GetPooled(up);
                        cancel.target = button;
                        button.SendEvent(cancel);
                    }
                    yield return null;
                    if (!reduceMotion) yield return new WaitForSecondsRealtime(0.16f);
                    Assert.That(actions, Is.EqualTo(reduceMotion ? 1 : 0), "only a completed press may activate the action");
                    Assert.That(button.resolvedStyle.backgroundColor, Is.EqualTo(rest), "completion must restore the idle face");
                    Assert.That(button.resolvedStyle.borderBottomWidth, Is.EqualTo(idleEdge));
                    if (reduceMotion)
                    {
                        yield return null;
                        using NativeReadback image = new NativeReadback(target);
                        image.SaveIfRequested($"feedback-{profile}-released.png");
                    }
                }

                button.SetEnabled(false);
                yield return null;
                yield return null;
                Assert.That(button.resolvedStyle.backgroundColor, Is.Not.EqualTo(rest));
                using (NativeReadback image = new NativeReadback(target))
                    image.SaveIfRequested($"feedback-{profile}-disabled.png");
                using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
                submit.target = button;
                button.SendEvent(submit);
                Assert.That(actions, Is.EqualTo(1), "disabled submission dispatched a second action");
            }
            finally
            {
                button.clicked -= CountAction;
                button.SetEnabled(true);
                screen.RemoveFromClassList("reduced-motion");
            }
        }
    }
}
