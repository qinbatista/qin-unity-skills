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
        public IEnumerator ExpeditionMenuReflowsAndNavigates()
        {
            yield return Review("Expedition", (screen, _) => new ExpeditionMenu(screen), CheckExpedition);
        }

        static IEnumerator CheckExpedition(VisualElement screen, LayoutProfile profile, LayoutText text, RenderTexture target)
        {
            string label = profile.Name + "-" + text.Locale;
            VisualElement home = screen.Q("home");
            VisualElement safe = screen.Q("safe");
            VisualElement briefing = screen.Q("briefing");
            VisualElement plot = home.Q("route-plot");
            VisualElement summary = home.Q("route-summary");
            Button open = home.Q<Button>("open-route");
            Button back = briefing.Q<Button>("back");
            AssertInside(home.worldBound, safe.worldBound, label + " home");
            AssertInside(plot.worldBound, home.worldBound, label + " route plot");
            AssertInside(summary.worldBound, home.worldBound, label + " route summary");
            if (profile.WideBody)
                Assert.That(plot.worldBound.xMax, Is.EqualTo(summary.worldBound.xMin).Within(Tolerance), label);
            else
                Assert.That(plot.worldBound.yMax, Is.EqualTo(summary.worldBound.yMin).Within(Tolerance), label);
            Assert.That(open.worldBound.height, Is.GreaterThanOrEqualTo(44 - Tolerance), label);
            Submit(open);
            yield return null;
            yield return null;
            Assert.That(home.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), label);
            AssertInside(briefing.worldBound, safe.worldBound, label + " briefing");
            Assert.That(screen.focusController.focusedElement, Is.SameAs(back), label);
            AssertVisibleTextFits(text);
            yield return CaptureFamily(briefing, target, "expedition-briefing-" + label);
            Submit(back);
            yield return null;
            yield return null;
            yield return WaitForFamilyRest(home);
            Assert.That(briefing.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), label);
            Assert.That(screen.focusController.focusedElement, Is.SameAs(open), label);
        }

        [UnityTest]
        public IEnumerator PreferencesPreserveFieldsAndAnchorOverlays()
        {
            (string, bool, int)? previous = null;
            IEnumerator Check(VisualElement screen, LayoutProfile profile, LayoutText text, RenderTexture target)
            {
                TextField callSign = screen.Q<TextField>("call-sign");
                Toggle alerts = screen.Q<Toggle>("alerts");
                SliderInt volume = screen.Q<SliderInt>("volume");
                if (previous.HasValue)
                    Assert.That((callSign.value, alerts.value, volume.value), Is.EqualTo(previous.Value),
                        profile.Name + " " + text.Locale + " retained form values");
                yield return CheckPreferences(screen, profile, text, target);
                previous = (callSign.value, alerts.value, volume.value);
            }

            yield return Review("Preferences", (screen, text) => new PreferencesForm(screen, text), Check);
        }

        static IEnumerator CheckPreferences(VisualElement screen, LayoutProfile profile, LayoutText text, RenderTexture target)
        {
            string label = profile.Name + "-" + text.Locale;
            VisualElement safe = screen.Q("safe");
            VisualElement form = screen.Q("form");
            TabView pages = form.Q<TabView>("pages");
            TextField callSign = form.Q<TextField>("call-sign");
            Toggle alerts = form.Q<Toggle>("alerts");
            SliderInt volume = form.Q<SliderInt>("volume");
            Button apply = form.Q<Button>("apply");
            Button help = form.Q<Button>("help");
            Label saved = form.Q<Label>("saved");
            AssertInside(form.worldBound, safe.worldBound, label + " form");
            AssertInside(apply.worldBound, form.worldBound, label + " footer");
            Assert.That(apply.enabledSelf, Is.False, label);
            Assert.That(pages.selectedTabIndex, Is.Zero, label);
            AssertSelectedTabMark(pages);
            for (int i = 0; i < 2; i++)
            {
                Tab tab = pages.GetTab(i);
                Label title = tab.tabHeader.Q<Label>();
                Assert.That(title.text, Is.EqualTo(text.Resolve("preferences." + tab.name)), label);
                AssertTextFits(title);
                AssertInside(tab.tabHeader.worldBound, form.worldBound, label + " tab");
            }
            ScrollView general = form.Q<ScrollView>("form-scroll");
            foreach (Label field in new[] { callSign.labelElement, alerts.labelElement, volume.labelElement })
            {
                general.ScrollTo(field.parent);
                yield return null;
                AssertTextFits(field);
                Assert.That(field.text, Is.EqualTo(text.Resolve("preferences." + field.parent.name)), label);
                VisualElement input = field.parent.Q(className: BaseField<int>.inputUssClassName);
                Assert.That(input.worldBound.height, Is.GreaterThanOrEqualTo(44f - Tolerance), label + " " + field.parent.name + " hit height");
                Assert.That(input.worldBound.width, Is.GreaterThanOrEqualTo(44f - Tolerance), label + " " + field.parent.name + " hit width");
                VisualElement hit = screen.panel.Pick(new Vector2(input.worldBound.center.x, input.worldBound.yMin + 3f));
                Assert.That(hit == field.parent || field.parent.Contains(hit), Is.True, label + " " + field.parent.name + " upper hit edge");
            }

            general.scrollOffset = Vector2.zero;
            yield return null;
            callSign.value = callSign.value == "Ridge" ? "Alder" : "Ridge";
            string typed = callSign.value;
            bool initialAlerts = alerts.value;
            alerts.Focus();
            using (NavigationSubmitEvent toggle = NavigationSubmitEvent.GetPooled())
            {
                toggle.target = alerts;
                alerts.SendEvent(toggle);
            }
            yield return null;
            Assert.That(alerts.value, Is.EqualTo(!initialAlerts), label + " toggle");
            int previous = volume.value;
            volume.Focus();
            using (NavigationMoveEvent key = NavigationMoveEvent.GetPooled(previous < 10 ? NavigationMoveEvent.Direction.Right : NavigationMoveEvent.Direction.Left, EventModifiers.None))
            {
                key.target = volume;
                volume.SendEvent(key);
            }
            yield return null;
            Assert.That(volume.value, Is.Not.EqualTo(previous), label + " native slider");
            Assert.That(apply.enabledSelf, Is.True, label);
            yield return PressTabHeader(pages.GetTab(1));
            Assert.That(pages.selectedTabIndex, Is.EqualTo(1), label + " map tab");
            AssertSelectedTabMark(pages);
            ScrollView list = form.Q<ScrollView>("display-scroll");
            Toggle last = list.Q<Toggle>("summits");
            Rect header = pages.GetTabHeader(1).worldBound;
            Rect footer = apply.worldBound;
            Assert.That(list.verticalScroller.highValue, Is.Positive, label + " overflow");
            foreach (Toggle field in list.Query<Toggle>().ToList())
            {
                AssertTextFits(field.labelElement);
                Assert.That(field.label, Is.EqualTo(text.Resolve("preferences." + field.name)), label);
            }
            list.ScrollTo(last);
            yield return null;
            yield return null;
            Assert.That(list.scrollOffset.y, Is.Positive, label);
            AssertInside(last.worldBound, list.contentViewport.worldBound, label + " last layer");
            Assert.That(pages.GetTabHeader(1).worldBound, Is.EqualTo(header), label + " fixed tabs");
            Assert.That(apply.worldBound, Is.EqualTo(footer), label + " fixed footer");
            AssertVisibleTextFits(text);
            yield return CaptureFamily(form, target, "preferences-map-" + label);
            list.scrollOffset = Vector2.zero;
            yield return PressTabHeader(pages.GetTab(0));
            Assert.That(pages.selectedTabIndex, Is.Zero, label);
            Assert.That(callSign.value, Is.EqualTo(typed), label + " retained entry");
            Assert.That(alerts.value, Is.EqualTo(!initialAlerts), label + " retained toggle");

            Submit(help);
            yield return null;
            yield return null;
            VisualElement popover = screen.Q("help-popover");
            AssertInside(popover.worldBound, safe.worldBound, label + " help");
            Assert.That(popover.worldBound.xMax, Is.EqualTo(help.worldBound.xMax).Within(Tolerance), label + " help anchor");
            Assert.That(popover.worldBound.yMin, Is.EqualTo(help.worldBound.yMax + 4f).Within(Tolerance), label + " help below anchor");
            AssertVisibleTextFits(text);
            yield return CaptureFamily(popover, target, "preferences-help-" + label);
            Submit(popover.Q<Button>("close-help"));
            yield return null;
            Assert.That(popover.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), label);
            Assert.That(screen.focusController.focusedElement, Is.SameAs(help), label);

            Submit(help);
            yield return null;
            yield return null;
            Submit(apply);
            yield return null;
            yield return null;
            Assert.That(popover.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), label + " modal replaces transient help");
            VisualElement modal = screen.Q("modal");
            AssertInside(modal.worldBound, safe.worldBound, label + " confirmation");
            Assert.That(modal.worldBound.center.x, Is.EqualTo(safe.worldBound.center.x).Within(Tolerance), label);
            Assert.That(form.enabledInHierarchy, Is.False, label + " modal input isolation");
            Assert.That(form.Contains(screen.panel.Pick(help.worldBound.center)), Is.False, label + " modal pointer isolation");
            Button cancel = modal.Q<Button>("cancel");
            Assert.That(screen.focusController.focusedElement, Is.SameAs(cancel), label + " modal focus");
            AssertVisibleTextFits(text);
            Label heading = modal.Q<Label>("confirm-title");
            Vector2 singleLine = heading.MeasureTextSize(heading.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            if (singleLine.x > heading.contentRect.width + Tolerance)
                Assert.That(heading.contentRect.height, Is.GreaterThan(singleLine.y + Tolerance), label + " wrapped heading");
            yield return CaptureFamily(modal, target, "preferences-confirm-" + label);
            Submit(cancel);
            yield return null;
            Assert.That(apply.enabledInHierarchy, Is.True, label + " cancelled edits retained");
            Assert.That(screen.focusController.focusedElement, Is.SameAs(apply), label);
            Submit(apply);
            yield return null;
            yield return null;
            Submit(modal.Q<Button>("save"));
            yield return null;
            yield return null;
            Assert.That(saved.resolvedStyle.visibility, Is.EqualTo(Visibility.Visible), label);
            Assert.That(apply.enabledSelf, Is.False, label + " saved values");
            Assert.That(callSign.value, Is.EqualTo(typed), label + " saved text");
            Assert.That(screen.focusController.focusedElement, Is.SameAs(help), label);
            AssertInside(saved.worldBound, form.worldBound, label + " notice");
            yield return CaptureFamily(form, target, "preferences-saved-" + label);
            if (profile.Name == "compact-portrait" && text.Locale == text.Locales[0])
            {
                yield return new WaitForSecondsRealtime(2.5f);
                Assert.That(saved.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden), label + " timed notice");
            }
            callSign.value = "";
            callSign.value = typed;
            yield return null;
            Assert.That(saved.resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden), label + " notice after editing");
        }

        static void AssertSelectedTabMark(TabView pages)
        {
            for (int index = 0; index < 2; index++)
            {
                VisualElement underline = pages.GetTabHeader(index).Q(className: Tab.tabHeaderUnderlineUssClassName);
                Assert.That(underline.resolvedStyle.backgroundColor.a,
                    Is.EqualTo(index == pages.selectedTabIndex ? 1f : 0f), "only the selected tab draws the underline");
            }
        }

        static IEnumerator PressTabHeader(Tab tab)
        {
            VisualElement header = tab.tabHeader;
            Vector2 point = header.worldBound.center;
            VisualElement hit = header.panel.Pick(point);
            Assert.That(hit == header || header.Contains(hit), Is.True, tab.name + " hit target");
            using (PointerDownEvent down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = point }))
            {
                down.target = hit;
                hit.SendEvent(down);
            }
            using (PointerUpEvent up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = point }))
            {
                up.target = hit;
                hit.SendEvent(up);
            }
            yield return null;
            yield return null;
        }

        static IEnumerator WaitForFamilyRest(VisualElement group)
        {
            var buttons = group.Query<Button>(className: "family-button").ToList();
            bool Resting() => buttons.TrueForAll(button =>
                Mathf.Approximately(button.resolvedStyle.borderBottomWidth, button.resolvedStyle.borderTopWidth));
            if (!Resting())
                yield return new WaitUntil(Resting, TimeSpan.FromSeconds(3), () => Assert.Fail(group.name + " retained a held button"));
            yield return null;
        }

        static IEnumerator CaptureFamily(VisualElement group, RenderTexture target, string state)
        {
            yield return WaitForFamilyRest(group);
            using NativeReadback image = new NativeReadback(target);
            image.SaveIfRequested(state + ".png");
        }
    }
}
