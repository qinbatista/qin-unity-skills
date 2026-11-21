using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Qin.UnitySamples.Tests
{
    public sealed class ResponsiveLayoutCases
    {
        const string Assets = "Assets/Editor/Tests/ResponsiveLayouts/";
        const float Tolerance = 1.1f;

        // All rectangles use top-left viewport pixels. Duo profiles replay display size alone.
        static readonly LayoutProfile[] Profiles =
        {
            new LayoutProfile("portrait-notch", 540, 1170, new Rect(0, 66, 540, 1072), new Rect(192, 12, 156, 36)),
            new LayoutProfile("landscape-left", 1170, 540, new Rect(74, 0, 1072, 518), new Rect(12, 192, 36, 156)),
            new LayoutProfile("landscape-right", 1170, 540, new Rect(24, 0, 1072, 518), new Rect(1122, 192, 36, 156)),
            new LayoutProfile("portrait-notch-2x", 1080, 2340, new Rect(0, 132, 1080, 2144), new Rect(384, 24, 312, 72)),
            new LayoutProfile("duo-outer-portrait", 1398, 2034),
            new LayoutProfile("duo-outer-landscape", 2034, 1398),
            new LayoutProfile("duo-inner-portrait", 1878, 2670),
            new LayoutProfile("duo-inner-landscape", 2670, 1878),
            new LayoutProfile("portrait-reference", 2160, 3840, new Rect(0, 0, 2160, 3840), default),
            new LayoutProfile("landscape-reference", 3840, 2160, new Rect(0, 0, 3840, 2160), default)
        };

        [UnityTest]
        public IEnumerator HudKeepsTheWingsAndMovesTheCenter()
        {
            int mapRequests = 0;
            int packRequests = 0;
            int frames = 0;
            yield return Review("Hud", screen =>
            {
                screen.Q<Button>("left-wing").clicked += () => mapRequests++;
                screen.Q<Button>("right-wing").clicked += () => packRequests++;
            }, (screen, profile, target) =>
            {
                Button left = screen.Q<Button>("left-wing");
                Button right = screen.Q<Button>("right-wing");
                Label center = screen.Q<Label>("center-title");
                Rect leftBounds = left.worldBound;
                Rect rightBounds = right.worldBound;
                Rect centerBounds = center.worldBound;
                AssertTextFits(left);
                AssertTextFits(right);
                AssertTextFits(center);
                Assert.That(leftBounds.yMin, Is.EqualTo(14f).Within(Tolerance));
                Assert.That(rightBounds.yMin, Is.EqualTo(leftBounds.yMin).Within(Tolerance));
                Assert.That(leftBounds.xMin * profile.Scale, Is.EqualTo(profile.Safe.xMin + 18f * profile.Scale).Within(Tolerance));
                Assert.That(rightBounds.xMax * profile.Scale, Is.EqualTo(profile.Safe.xMax - 18f * profile.Scale).Within(Tolerance));
                Assert.That(centerBounds.center.x, Is.EqualTo(screen.worldBound.center.x).Within(Tolerance));
                Assert.That(centerBounds.yMin, Is.EqualTo(profile.Safe.yMin / profile.Scale + 14f).Within(Tolerance));
                Assert.That(leftBounds.width * profile.Scale, Is.EqualTo(124f * profile.Scale).Within(Tolerance));
                Assert.That(leftBounds.height * profile.Scale, Is.EqualTo(48f * profile.Scale).Within(Tolerance));
                foreach (VisualElement group in screen.Q("header").Children())
                {
                    AssertInside(group.worldBound, screen.worldBound, group.name);
                    if (profile.Cutout.width > 0f)
                    {
                        Rect obstruction = new Rect(profile.Cutout.position / profile.Scale, profile.Cutout.size / profile.Scale);
                        Assert.That(group.worldBound.Overlaps(obstruction), Is.False, group.name);
                    }
                }
                if (profile.Portrait && profile.Cutout.width > 0f)
                    Assert.That(leftBounds.yMax * profile.Scale, Is.LessThan(profile.Safe.yMin));

                Submit(left);
                Submit(right);
                frames++;
                Assert.That(mapRequests, Is.EqualTo(frames));
                Assert.That(packRequests, Is.EqualTo(frames));
                VisualElement objective = screen.Q("objective");
                VisualElement tools = screen.Q("tools");
                AssertInside(objective.worldBound, screen.Q("safe").worldBound, objective.name);
                Label objectiveTitle = objective.Q<Label>(className: "title");
                AssertInside(objectiveTitle.worldBound, objective.worldBound, objectiveTitle.text);
                AssertTextFits(objectiveTitle);
                foreach (Label status in tools.Query<Label>().ToList()) AssertTextFits(status);
                Assert.That(objective.worldBound.yMax, Is.LessThan(tools.worldBound.yMin));
                return null;
            });
        }

        [UnityTest]
        public IEnumerator InventoryReflowsOneSetOfInstances()
        {
            VisualElement[] cards = new VisualElement[6];
            string[] names = { "Rope", "Map", "Compass", "Lantern", "Salve", "Rations" };
            int[] counts = { 2, 1, 1, 1, 3, 4 };
            yield return Review("Inventory", screen =>
            {
                VisualTreeAsset item = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Assets + "Item.uxml");
                VisualElement grid = screen.Q("tiles");
                for (int index = 0; index < cards.Length; index++)
                {
                    item.CloneTree(grid);
                    VisualElement tile = grid.ElementAt(index);
                    cards[index] = tile;
                    tile.Q<Label>("item-icon").text = names[index].Substring(0, 1);
                    tile.Q<Label>("item-name").text = names[index];
                    tile.Q<Label>("item-count").text = $"Qty {counts[index]}";
                }
            }, (screen, profile, target) =>
            {
                VisualElement grid = screen.Q("tiles");
                Assert.That(grid.childCount, Is.EqualTo(cards.Length));
                Assert.That(grid.worldBound.center.x, Is.EqualTo(screen.Q("safe").worldBound.center.x).Within(Tolerance));
                int columns = profile.Portrait ? 2 : 3;
                for (int index = 0; index < cards.Length; index++)
                {
                    VisualElement tile = grid.ElementAt(index);
                    Assert.That(tile, Is.SameAs(cards[index]));
                    Rect bounds = tile.worldBound;
                    Rect first = cards[0].worldBound;
                    Assert.That(bounds.xMin, Is.EqualTo(first.xMin + index % columns * 174f).Within(Tolerance), $"{profile.Name} {names[index]} column");
                    Assert.That(bounds.yMin, Is.EqualTo(first.yMin + index / columns * 116f).Within(Tolerance), $"{profile.Name} {names[index]} row");
                    AssertInside(bounds, grid.worldBound, names[index]);
                    Rect identity = tile.Q("item-identity").worldBound;
                    Label icon = tile.Q<Label>("item-icon");
                    Label name = tile.Q<Label>("item-name");
                    AssertInside(icon.worldBound, identity, "inventory icon");
                    AssertInside(name.worldBound, identity, "inventory name");
                    Assert.That(icon.worldBound.xMax, Is.LessThan(name.worldBound.xMin));
                    Assert.That((icon.worldBound.xMin + name.worldBound.xMax) / 2f, Is.EqualTo(identity.center.x).Within(Tolerance));
                    AssertTextFits(name);
                }
                return null;
            });
        }

        [UnityTest]
        public IEnumerator SettingsKeepRowsContainedAndCardsAligned()
        {
            yield return Review("Settings", null, (screen, profile, target) =>
            {
                Rect group = screen.Q("settings-groups").worldBound;
                Rect display = screen.Q("display").worldBound;
                Rect audio = screen.Q("audio").worldBound;
                AssertInside(group, screen.Q("safe").worldBound, "settings group");
                AssertInside(display, group, "display card");
                AssertInside(audio, group, "audio card");
                Assert.That(display.width * profile.Scale, Is.EqualTo(audio.width * profile.Scale).Within(Tolerance), profile.Name);
                Assert.That(display.height * profile.Scale, Is.EqualTo(audio.height * profile.Scale).Within(Tolerance), profile.Name);
                if (profile.Portrait)
                {
                    Assert.That(display.center.x, Is.EqualTo(audio.center.x).Within(Tolerance));
                    Assert.That(audio.yMin - display.yMax, Is.EqualTo(14f).Within(Tolerance));
                }
                else
                {
                    Assert.That(display.yMin, Is.EqualTo(audio.yMin).Within(Tolerance));
                    Assert.That(audio.xMin - display.xMax, Is.EqualTo(14f).Within(Tolerance));
                }
                foreach (VisualElement row in screen.Query<VisualElement>(className: "setting-row").ToList())
                {
                    Label label = (Label)row[0];
                    Label value = (Label)row[1];
                    AssertInside(label.worldBound, row.worldBound, label.text);
                    AssertInside(value.worldBound, row.worldBound, value.text);
                    Assert.That(label.worldBound.xMax, Is.LessThan(value.worldBound.xMin));
                    Assert.That(label.worldBound.center.y, Is.EqualTo(value.worldBound.center.y).Within(Tolerance));
                    AssertTextFits(label);
                }
                return null;
            });
        }

        [UnityTest]
        public IEnumerator ConfirmationMatchesCanvas()
        {
            CanvasConfirmation canvas = null;
            string choice = null;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            try
            {
                yield return Review("Dialog", screen =>
                {
                    VisualElement dialog = screen.Q("dialog");
                    Button stay = dialog.Q<Button>("stay");
                    Button leave = dialog.Q<Button>("leave");
                    foreach (TextElement text in dialog.Query<TextElement>().ToList())
                        text.style.unityFont = font;
                    stay.clicked += () => { choice = stay.text; dialog.style.display = DisplayStyle.None; };
                    leave.clicked += () => { choice = leave.text; dialog.style.display = DisplayStyle.None; };
                    canvas = new CanvasConfirmation(font, dialog.Q<Label>("heading").text, dialog.Q<Label>("message").text, stay.text, leave.text);
                }, (screen, profile, target) =>
                {
                    VisualElement dialog = screen.Q("dialog");
                    AssertInside(dialog.worldBound, screen.Q("safe").worldBound, "confirmation dialog");
                    Assert.That(Vector2.Distance(dialog.worldBound.center, screen.Q("safe").worldBound.center), Is.LessThanOrEqualTo(Tolerance));
                    Rect actions = dialog.Q("actions").worldBound;
                    foreach (string name in new[] { "stay", "leave" })
                    {
                        Button button = dialog.Q<Button>(name);
                        AssertInside(button.worldBound, actions, name);
                        AssertTextFits(button);
                    }

                    canvas.Draw(profile.Size, profile.Safe, profile.Scale, profile.Name);
                    foreach (string name in new[] { "dialog", "heading", "message", "stay", "leave" })
                    {
                        Rect toolkit = screen.Q(name).worldBound;
                        Rect expected = new Rect(toolkit.position * profile.Scale, toolkit.size * profile.Scale);
                        Assert.That(Vector2.Distance(canvas.Bounds(name).min, expected.min), Is.LessThanOrEqualTo(1.5f), name);
                        Assert.That(Vector2.Distance(canvas.Bounds(name).max, expected.max), Is.LessThanOrEqualTo(1.5f), name);
                    }

                    string action = profile.Portrait ? "stay" : "leave";
                    Button selected = dialog.Q<Button>(action);
                    choice = null;
                    Submit(selected);
                    Assert.That(choice, Is.EqualTo(selected.text));
                    Assert.That(canvas.Choose(action), Is.EqualTo(choice));
                    Assert.That(dialog.style.display.value, Is.EqualTo(DisplayStyle.None));
                    dialog.style.display = DisplayStyle.Flex;
                    return null;
                });
            }
            finally
            {
                canvas?.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator QuestListScrollsInsideFixedHeaderAndFooter()
        {
            string[] titles =
            {
                "Check the trailhead", "Read the ridge map", "Fill the canteen", "Cross the stone bridge",
                "Mark the fork", "Find the north trail", "Rest at the shelter", "Follow the cairns",
                "Collect the marker", "Reach the upper ridge", "Record the view", "Return to camp"
            };
            yield return Review("Quests", screen =>
            {
                ScrollView list = screen.Q<ScrollView>("quest-scroll");
                foreach (string title in titles)
                {
                    Label entry = new Label(title);
                    entry.AddToClassList("quest-row");
                    list.Add(entry);
                }
            }, CheckQuestScroll);
        }

        static IEnumerator CheckQuestScroll(VisualElement screen, LayoutProfile profile, RenderTexture target)
        {
            VisualElement journal = screen.Q("journal");
            VisualElement header = screen.Q("journal-header");
            VisualElement footer = screen.Q("journal-footer");
            ScrollView list = screen.Q<ScrollView>("quest-scroll");
            VisualElement last = list.contentContainer.ElementAt(list.contentContainer.childCount - 1);
            AssertInside(journal.worldBound, screen.Q("safe").worldBound, "trail journal");
            Assert.That(list.worldBound.yMin, Is.GreaterThan(header.worldBound.yMax));
            Assert.That(list.worldBound.yMax, Is.LessThanOrEqualTo(footer.worldBound.yMin + Tolerance));
            Rect fixedHeader = header.worldBound;
            Rect fixedFooter = footer.worldBound;
            list.ScrollTo(last);
            yield return null;
            Assert.That(list.scrollOffset.y, Is.GreaterThan(0f));
            AssertInside(last.worldBound, list.contentViewport.worldBound, "last quest entry");
            Assert.That(header.worldBound, Is.EqualTo(fixedHeader));
            Assert.That(footer.worldBound, Is.EqualTo(fixedFooter));
            using (NativeReadback image = new NativeReadback(target)) image.SaveIfRequested($"quests-{profile.Name}-last.png");
            list.scrollOffset = Vector2.zero;
        }

        static IEnumerator Review(string name, Action<VisualElement> bind, Func<VisualElement, LayoutProfile, RenderTexture, IEnumerator> check)
        {
            Assert.That(Application.isPlaying, Is.True, "The layout fixtures require Unity Play Mode.");
            GameObject host = new GameObject(name + " layout fixture");
            host.SetActive(false);
            PanelSettings settings = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Prefabs/ChargePulse/ChargePulsePanel.asset"));
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.screenMatchMode = PanelScreenMatchMode.Expand;
            settings.clearColor = true;
            UIDocument document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Assets + name + ".uxml");
            host.SetActive(true);
            RenderTexture current = null;
            try
            {
                VisualElement screen = document.rootVisualElement.Q("screen");
                VisualElement cutout = new VisualElement { pickingMode = PickingMode.Ignore };
                cutout.AddToClassList("cutout");
                screen.Add(cutout);
                bind?.Invoke(screen);
                foreach (LayoutProfile profile in Profiles)
                {
                    RenderTexture next = new RenderTexture(profile.Size.x, profile.Size.y, 24);
                    settings.targetTexture = next;
                    Object.Destroy(current);
                    current = next;
                    settings.referenceResolution = profile.Portrait ? new Vector2Int(2160, 3840) : new Vector2Int(3840, 2160);
                    // USS uses quarter-resolution design units. PanelSettings projects them directly.
                    settings.scale = 4f;
                    ApplyGeometry(screen, cutout, profile);
                    yield return null;
                    yield return null;
                    Assert.That(document.rootVisualElement.Q("screen"), Is.SameAs(screen));
                    Assert.That(screen.worldBound.width * profile.Scale, Is.EqualTo(profile.Size.x).Within(Tolerance));
                    Assert.That(screen.worldBound.height * profile.Scale, Is.EqualTo(profile.Size.y).Within(Tolerance));
                    using (NativeReadback image = new NativeReadback(current))
                    {
                        Assert.That(image.BrightPixels, Is.GreaterThan(100), $"{name} {profile.Name} has no bright foreground");
                        image.SaveIfRequested($"{name.ToLowerInvariant()}-{profile.Name}.png");
                    }
                    IEnumerator checkFrames = check(screen, profile, current);
                    if (checkFrames != null) yield return checkFrames;
                }
            }
            finally
            {
                host.SetActive(false);
                settings.targetTexture = null;
                Object.Destroy(host);
                Object.Destroy(settings);
                Object.Destroy(current);
            }
        }

        static void ApplyGeometry(VisualElement screen, VisualElement cutout, LayoutProfile profile)
        {
            float scale = profile.Scale;
            Rect safePixels = profile.Safe;
            screen.EnableInClassList("landscape", !profile.Portrait);
            VisualElement safe = screen.Q("safe");
            safe.style.left = safePixels.xMin / scale;
            safe.style.right = (profile.Size.x - safePixels.xMax) / scale;
            safe.style.top = safePixels.yMin / scale;
            safe.style.bottom = (profile.Size.y - safePixels.yMax) / scale;
            VisualElement header = screen.Q("header");
            if (header != null)
            {
                VisualElement left = header.Q("left-wing");
                left.style.left = 18f + safePixels.xMin / scale;
                left.style.top = 14f;
                VisualElement right = header.Q("right-wing");
                right.style.right = 18f + (profile.Size.x - safePixels.xMax) / scale;
                right.style.top = 14f;
                header.Q("center-title").style.top = 14f + safePixels.yMin / scale;
            }
            cutout.style.left = profile.Cutout.x / scale;
            cutout.style.top = profile.Cutout.y / scale;
            cutout.style.width = profile.Cutout.width / scale;
            cutout.style.height = profile.Cutout.height / scale;
            cutout.style.display = profile.Cutout.width > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void Submit(Button button)
        {
            Assert.That(button.panel.Pick(button.worldBound.center), Is.SameAs(button), button.name);
            button.Focus();
            using NavigationSubmitEvent input = NavigationSubmitEvent.GetPooled();
            input.target = button;
            button.SendEvent(input);
        }

        static void AssertInside(Rect child, Rect parent, string name)
        {
            Assert.That(child.width, Is.GreaterThan(0), name);
            Assert.That(child.height, Is.GreaterThan(0), name);
            Assert.That(child.xMin, Is.GreaterThanOrEqualTo(parent.xMin - Tolerance), name);
            Assert.That(child.yMin, Is.GreaterThanOrEqualTo(parent.yMin - Tolerance), name);
            Assert.That(child.xMax, Is.LessThanOrEqualTo(parent.xMax + Tolerance), name);
            Assert.That(child.yMax, Is.LessThanOrEqualTo(parent.yMax + Tolerance), name);
        }

        static void AssertTextFits(TextElement text)
        {
            Vector2 ink = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            Assert.That(ink.x, Is.Positive, text.text);
            Assert.That(ink.x, Is.LessThanOrEqualTo(text.contentRect.width + Tolerance), text.text);
            Assert.That(ink.y, Is.LessThanOrEqualTo(text.contentRect.height + Tolerance), text.text);
        }

        [UnitySetUp]
        public IEnumerator EnterFixturePlayMode()
        {
            if (!Application.isPlaying) yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        readonly struct LayoutProfile
        {
            public readonly string Name;
            public readonly Vector2Int Size;
            public readonly Rect Safe;
            public readonly Rect Cutout;
            public bool Portrait => Size.y > Size.x;
            public float Scale => Portrait ? Mathf.Min(Size.x / 540f, Size.y / 960f) : Mathf.Min(Size.x / 960f, Size.y / 540f);

            public LayoutProfile(string name, int width, int height) : this(name, width, height, new Rect(0, 0, width, height), default)
            {
            }

            public LayoutProfile(string name, int width, int height, Rect safe, Rect cutout)
            {
                Name = name;
                Size = new Vector2Int(width, height);
                Safe = safe;
                Cutout = cutout;
            }
        }
    }
}
