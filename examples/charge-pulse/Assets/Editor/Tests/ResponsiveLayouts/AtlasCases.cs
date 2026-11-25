using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    public sealed partial class ResponsiveLayoutCases
    {
        [UnityTest]
        public IEnumerator AtlasSearchesAndRecyclesRealRows()
        {
            AtlasDirectory directory = null;
            yield return Review("Atlas", (screen, text) => directory = new AtlasDirectory(screen, text),
                (screen, profile, text, target) => CheckAtlas(directory, profile, text, target));
        }

        static IEnumerator CheckAtlas(AtlasDirectory directory, LayoutProfile profile, LayoutText text, RenderTexture target)
        {
            directory.ShowStatus(null);
            VisualElement root = directory.Screen.Q("safe");
            VisualElement detail = directory.Screen.Q("detail-card");
            AssertInside(directory.Search.worldBound, root.worldBound, profile.Name + " search");
            AssertInside(directory.Rows.worldBound, root.worldBound, profile.Name + " collection");
            Assert.That(directory.Rows.worldBound.height, Is.GreaterThan(100f));
            Assert.That(directory.Rows.Query<Button>(className: "atlas-row").ToList().Count,
                Is.InRange(1, AtlasDirectory.CatalogSize - 1), "offscreen rows should be virtualized");
            int before = directory.Activations;
            directory.Rows.ScrollToItem(AtlasDirectory.CatalogSize - 1);
            yield return null;
            yield return null;
            Button last = directory.VisibleRow(AtlasDirectory.CatalogSize - 1);
            AssertInside(last.worldBound, directory.Rows.Q<ScrollView>().contentViewport.worldBound, last.text);
            AssertTextFits(last);
            Submit(last);
            yield return null;
            Assert.That(directory.Selected, Is.EqualTo(AtlasDirectory.CatalogSize - 1));
            Assert.That(directory.Activations, Is.EqualTo(before + 1));
            AssertInside(detail.worldBound, root.worldBound, "selected route");
            AssertTextFits(directory.Screen.Q<Label>("route-name"));
            if (profile.WideBody)
                Assert.That(directory.Rows.worldBound.xMax, Is.LessThanOrEqualTo(detail.worldBound.xMin));
            else
            {
                Assert.That(directory.Screen.Q("directory").resolvedStyle.visibility, Is.EqualTo(Visibility.Hidden));
                Button back = directory.Screen.Q<Button>("return-list");
                AssertTextFits(back);
                Submit(back);
                yield return null;
                yield return new WaitUntil(() => directory.FocusedRoute == directory.Selected,
                    TimeSpan.FromSeconds(3), () => Assert.Fail($"return focus: selected {directory.Selected}, focused route {directory.FocusedRoute}, element {directory.Screen.focusController.focusedElement}"));
                Button restored = directory.VisibleRow(AtlasDirectory.CatalogSize - 1);
                Assert.That(restored.panel.Pick(restored.worldBound.center), Is.SameAs(restored));
            }

            // Updating the native text field dispatches its ChangeEvent, which owns filtering.
            directory.Search.value = "Monte";
            yield return null;
            Assert.That(directory.Rows.itemsSource.Count, Is.EqualTo(AtlasDirectory.CatalogSize / 4));
            directory.Rows.ScrollToItem(0);
            yield return null;
            Button filtered = directory.VisibleRow(0);
            Assert.That(filtered.text, Does.Contain("Monte Rosa"));
            Submit(filtered);
            yield return null;
            Assert.That(directory.Selected, Is.Zero);
            Assert.That(directory.Activations, Is.EqualTo(before + 2), "a recycled row fires one action");
            if (!profile.WideBody)
            {
                Submit(directory.Screen.Q<Button>("return-list"));
                yield return null;
            }

            directory.Search.value = "no-such-route";
            yield return null;
            Assert.That(directory.Rows.itemsSource.Count, Is.Zero);
            Assert.That(directory.Status.text, Is.EqualTo(text.Resolve("atlas.empty")));
            AssertInside(directory.Status.worldBound, root.worldBound, "empty state");
            AssertTextFits(directory.Status);
            directory.ShowStatus("loading");
            yield return null;
            Assert.That(directory.Status.text, Is.EqualTo(text.Resolve("atlas.loading")));
            AssertTextFits(directory.Status);
            directory.ShowStatus("error");
            yield return null;
            Assert.That(directory.Status.text, Is.EqualTo(text.Resolve("atlas.error")));
            Button retry = directory.Screen.Q<Button>("retry");
            AssertTextFits(directory.Status);
            AssertTextFits(retry);
            AssertInside(retry.worldBound, root.worldBound, "retry");
            if (profile.Name == "compact-portrait")
            {
                using NativeReadback frame = new NativeReadback(target);
                frame.SaveIfRequested($"atlas-{profile.Name}-{text.Locale}-error.png");
            }
            Submit(retry);
            yield return null;
            Assert.That(directory.Search.value, Is.Empty);
            Assert.That(directory.Rows.itemsSource.Count, Is.EqualTo(AtlasDirectory.CatalogSize));
            Assert.That(directory.Status.parent.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            yield return CheckAtlasSort(directory, profile, text, target);
            directory.Rows.ScrollToItem(0);
            directory.Search.Focus();
            yield return null;
        }

        static IEnumerator CheckAtlasSort(AtlasDirectory directory, LayoutProfile profile, LayoutText text, RenderTexture target)
        {
            VisualElement root = directory.Screen.Q("safe");
            VisualElement sheet = directory.Screen.Q("sort-sheet");
            Button open = root.Q<Button>("sort");
            Submit(open);
            yield return null;
            AssertInside(sheet.worldBound, root.worldBound, "sort sheet");
            if (profile.WideBody)
            {
                Assert.That(sheet.worldBound.xMax, Is.EqualTo(root.worldBound.xMax).Within(Tolerance));
                Assert.That(sheet.worldBound.height, Is.EqualTo(root.worldBound.height).Within(Tolerance));
            }
            else
            {
                Assert.That(sheet.worldBound.yMax, Is.EqualTo(root.worldBound.yMax).Within(Tolerance));
                Assert.That(sheet.worldBound.width, Is.EqualTo(root.worldBound.width).Within(Tolerance));
            }
            Assert.That(open.panel.Pick(open.worldBound.center), Is.Not.SameAs(open), "modal shield blocks the opener");
            foreach (Button choice in sheet.Query<Button>().ToList())
            {
                AssertInside(choice.worldBound, sheet.worldBound, choice.name);
                Assert.That(choice.worldBound.height, Is.GreaterThanOrEqualTo(47f));
                AssertTextFits(choice);
            }
            using (NativeReadback frame = new NativeReadback(target))
                frame.SaveIfRequested($"atlas-sort-{profile.Name}-{text.Locale}.png");
            Submit(sheet.Q<Button>("by-height"));
            yield return null;
            Assert.That(directory.Rows.itemsSource[0], Is.EqualTo(AtlasDirectory.CatalogSize - 1));
            Assert.That(root.focusController.focusedElement, Is.SameAs(open));
            Submit(open);
            yield return null;
            Submit(sheet.Q<Button>("by-number"));
            yield return null;
            Assert.That(directory.Rows.itemsSource[0], Is.EqualTo(0));
        }

        sealed class AtlasDirectory
        {
            public const int CatalogSize = 64;
            static readonly string[] Peaks = { "Monte Rosa", "Les Drus", "Piz Bernina", "Mont Blanc" };
            readonly List<int> _matches = new List<int>(CatalogSize);
            readonly LayoutText _text;
            bool _highestFirst;
            public VisualElement Screen { get; }
            public ListView Rows { get; }
            public TextField Search { get; }
            public Label Status { get; }
            public int Selected { get; private set; }
            public int Activations { get; private set; }
            public int FocusedRoute { get; private set; } = -1;

            public AtlasDirectory(VisualElement screen, LayoutText text)
            {
                Screen = screen;
                _text = text;
                Rows = screen.Q<ListView>("routes");
                Search = screen.Q<TextField>("query");
                Status = screen.Q<Label>("status-label");
                Rows.selectionType = SelectionType.None;
                Rows.fixedItemHeight = 50;
                Rows.makeItem = MakeRow;
                Rows.bindItem = (element, index) =>
                {
                    int id = _matches[index];
                    element.userData = id;
                    ((Button)element).text = $"{id + 1:D2}    {Peaks[id % Peaks.Length]}";
                };
                Rows.unbindItem = (element, _) => element.userData = null;
                Rows.itemsSource = _matches;
                Search.RegisterValueChangedCallback(change => Filter(change.newValue));
                Search.RegisterCallback<FocusInEvent>(_ => Screen.RemoveFromClassList("detail-open"));
                Button back = screen.Q<Button>("return-list");
                text.Bind(back, "atlas.return-list");
                back.clicked += () =>
                {
                    Screen.RemoveFromClassList("detail-open");
                    int selectedIndex = _matches.IndexOf(Selected);
                    // Visibility keeps the virtualized scroll viewport at its existing size.
                    if (selectedIndex < 0) return;
                    Rows.schedule.Execute(() =>
                    {
                        Rows.ScrollToItem(selectedIndex);
                        Rows.schedule.Execute(() => VisibleRow(selectedIndex)?.Focus());
                    });
                };
                Button retry = screen.Q<Button>("retry");
                text.Bind(retry, "atlas.retry");
                retry.clicked += () =>
                {
                    Search.SetValueWithoutNotify(string.Empty);
                    Filter(string.Empty);
                };
                Button openSort = screen.Q<Button>("sort");
                VisualElement shade = screen.Q("sort-shade");
                openSort.clicked += () =>
                {
                    shade.style.display = DisplayStyle.Flex;
                    screen.Q<Button>(_highestFirst ? "by-height" : "by-number").Focus();
                };
                void ChooseSort(bool highest)
                {
                    _highestFirst = highest;
                    Filter(Search.value);
                    Rows.ScrollToItem(0);
                    shade.style.display = DisplayStyle.None;
                    openSort.Focus();
                }
                screen.Q<Button>("by-height").clicked += () => ChooseSort(true);
                screen.Q<Button>("by-number").clicked += () => ChooseSort(false);
                Describe(0);
                Filter(string.Empty);
            }

            public Button VisibleRow(int index)
            {
                VisualElement root = Rows.GetRootElementForIndex(index);
                return root as Button ?? root?.Q<Button>(className: "atlas-row");
            }

            Button MakeRow()
            {
                Button button = new Button();
                button.AddToClassList("atlas-row");
                button.RegisterCallback<FocusInEvent>(_ => FocusedRoute = button.userData is int id ? id : -1);
                button.RegisterCallback<FocusOutEvent>(_ => FocusedRoute = -1);
                button.clicked += () =>
                {
                    if (!(button.userData is int id)) return;
                    Activations++;
                    Describe(id);
                    // Selecting a row opens a second page only in the single-column composition.
                    Screen.EnableInClassList("detail-open", !Screen.ClassListContains("wide-body"));
                };
                return button;
            }

            void Describe(int id)
            {
                Selected = id;
                Screen.Q<Label>("route-name").text = Peaks[id % Peaks.Length];
                Screen.Q<Label>("route-altitude").text = $"{2800 + id * 17} m";
            }

            void Filter(string query)
            {
                _matches.Clear();
                for (int id = 0; id < CatalogSize; id++)
                    if (Peaks[id % Peaks.Length].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        _matches.Add(id);
                if (_highestFirst) _matches.Reverse();
                Rows.RefreshItems();
                ShowStatus(_matches.Count == 0 ? "empty" : null);
            }

            public void ShowStatus(string state)
            {
                Rows.style.display = state == null ? DisplayStyle.Flex : DisplayStyle.None;
                Status.parent.style.display = state == null ? DisplayStyle.None : DisplayStyle.Flex;
                Screen.Q("retry").style.display = state == "error" ? DisplayStyle.Flex : DisplayStyle.None;
                if (state != null) Status.text = _text.Resolve("atlas." + state);
            }
        }
    }
}
