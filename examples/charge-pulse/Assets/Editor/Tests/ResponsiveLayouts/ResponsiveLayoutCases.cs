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
    public sealed partial class ResponsiveLayoutCases
    {
        const string Assets = "Assets/Editor/Tests/ResponsiveLayouts/";
        const float Tolerance = 1.1f;

        // All inputs are explicit top-left pixel fixtures. Display identity is independent of window size.
        static readonly LayoutProfile[] Profiles =
        {
            new LayoutProfile("portrait-notch", 540, 1170, new Rect(0, 66, 540, 1072), new Rect(192, 12, 156, 36)),
            new LayoutProfile("landscape-left", 1170, 540, new Rect(74, 0, 1072, 518), new Rect(12, 192, 36, 156)),
            new LayoutProfile("landscape-right", 1170, 540, new Rect(24, 0, 1072, 518), new Rect(1122, 192, 36, 156)),
            new LayoutProfile("portrait-notch-2x", 1080, 2340, new Rect(0, 132, 1080, 2144), new Rect(384, 24, 312, 72)),
            new LayoutProfile("duo-outer-portrait", 1398, 2034, new Rect(0, 0, 1398, 2034), new Rect(1200, 88, 110, 110), HeaderMode.Outer),
            new LayoutProfile("duo-inner-same-window", 1398, 2034, new Rect(0, 72, 1398, 1902), default, HeaderMode.Safe),
            new LayoutProfile("duo-outer-landscape-left", 2034, 1398, new Rect(0, 0, 2034, 1398), new Rect(88, 88, 110, 110), HeaderMode.Outer),
            new LayoutProfile("duo-outer-landscape-right", 2034, 1398, new Rect(0, 0, 2034, 1398), new Rect(1836, 1200, 110, 110), HeaderMode.Outer),
            new LayoutProfile("duo-inner-portrait", 1878, 2670, new Rect(0, 80, 1878, 2530), default, HeaderMode.Safe),
            new LayoutProfile("duo-inner-landscape", 2670, 1878, new Rect(40, 56, 2590, 1750), default, HeaderMode.Safe),
            // Host-selected primary pane with a separating vertical hinge. The full window stays landscape.
            new LayoutProfile("separating-left-pane", 1000, 720, new Rect(0, 0, 490, 720), new Rect(490, 0, 20, 720), HeaderMode.Safe),
            new LayoutProfile("compact-portrait", 360, 780),
            new LayoutProfile("before-wide-body", 599, 960),
            new LayoutProfile("at-wide-body", 600, 960),
            new LayoutProfile("small-phone", 320, 568),
            new LayoutProfile("separating-right-pane", 1000, 720, new Rect(510, 0, 490, 720), new Rect(490, 0, 20, 720), HeaderMode.Safe),
            new LayoutProfile("horizontal-fold-upper-pane", 1000, 800, new Rect(0, 0, 1000, 390), new Rect(0, 390, 1000, 20), HeaderMode.Safe),
            new LayoutProfile("handheld-landscape", 800, 480),
            new LayoutProfile("tablet-portrait", 1536, 2048),
            new LayoutProfile("tablet-landscape", 2048, 1536),
            new LayoutProfile("desktop-16x10", 1440, 900),
            new LayoutProfile("desktop-ultrawide", 2560, 1080),
            new LayoutProfile("android-gesture-portrait", 1080, 2400, new Rect(0, 96, 1080, 2214), new Rect(496, 18, 88, 56)),
            new LayoutProfile("portrait-reference", 2160, 3840, new Rect(0, 0, 2160, 3840), default),
            new LayoutProfile("landscape-reference", 3840, 2160, new Rect(0, 0, 3840, 2160), default)
        };

        [UnityTest]
        public IEnumerator HudKeepsTheWingsAndMovesTheCenter()
        {
            int mapRequests = 0;
            int packRequests = 0;
            int frames = 0;
            yield return Review("Hud", (screen, text) =>
            {
                screen.Q<Button>("left-wing").clicked += () => mapRequests++;
                screen.Q<Button>("right-wing").clicked += () => packRequests++;
                text.Bind(screen.Q<Label>("stamina-text"), "hud.stamina-text", 72, 100);
                text.Bind(screen.Q<Label>("elevation"), "hud.elevation", 2480);
            }, (screen, profile, text, target) =>
            {
                Button left = screen.Q<Button>("left-wing");
                Button right = screen.Q<Button>("right-wing");
                Label center = screen.Q<Label>("center-title");
                Rect leftBounds = left.worldBound;
                Rect rightBounds = right.worldBound;
                Rect centerBounds = center.worldBound;
                bool besideLens = profile.Header == HeaderMode.Outer && profile.Cutout.center.y < profile.Size.y / 2f;
                float anchorTop = profile.Header == HeaderMode.Safe ? profile.Safe.yMin / profile.Scale : 0f;
                if (besideLens)
                {
                    Assert.That(leftBounds.center.y * profile.Scale, Is.EqualTo(profile.Cutout.center.y).Within(Tolerance));
                    if (profile.Cutout.center.x > profile.Size.x / 2f)
                        Assert.That(rightBounds.xMax * profile.Scale, Is.LessThan(profile.Cutout.xMin));
                    else
                        Assert.That(leftBounds.xMin * profile.Scale, Is.GreaterThan(profile.Cutout.xMax));
                }
                else
                {
                    Assert.That(leftBounds.yMin, Is.EqualTo(anchorTop + 14f).Within(Tolerance));
                    Assert.That(leftBounds.xMin * profile.Scale, Is.EqualTo(profile.Safe.xMin + 18f * profile.Scale).Within(Tolerance));
                    Assert.That(rightBounds.xMax * profile.Scale, Is.EqualTo(profile.Safe.xMax - 18f * profile.Scale).Within(Tolerance));
                }
                Assert.That(rightBounds.yMin, Is.EqualTo(leftBounds.yMin).Within(Tolerance));
                Assert.That(centerBounds.center.x, Is.EqualTo((leftBounds.center.x + rightBounds.center.x) / 2f).Within(Tolerance));
                float titleDrop = profile.Header == HeaderMode.Standard ? profile.Safe.yMin / profile.Scale : 0f;
                if (profile.SmallPane) titleDrop = Mathf.Max(54f, titleDrop);
                Assert.That(centerBounds.yMin, Is.EqualTo(leftBounds.yMin + titleDrop).Within(Tolerance));
                if (profile.SmallPane)
                    Assert.That(centerBounds.yMin, Is.GreaterThanOrEqualTo(leftBounds.yMax + 5f));
                else
                {
                    Assert.That(leftBounds.xMax, Is.LessThanOrEqualTo(centerBounds.xMin + Tolerance));
                    Assert.That(centerBounds.xMax, Is.LessThanOrEqualTo(rightBounds.xMin + Tolerance));
                }
                Assert.That(leftBounds.width * profile.Scale, Is.EqualTo((profile.NarrowBody ? 88f : 124f) * profile.Scale).Within(Tolerance));
                Assert.That(leftBounds.height * profile.Scale, Is.GreaterThanOrEqualTo(44f), "fixture minimum hit height");
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
                if (profile.Header == HeaderMode.Standard && profile.Portrait && profile.Cutout.width > 0f)
                    Assert.That(centerBounds.yMin * profile.Scale, Is.GreaterThanOrEqualTo(profile.Cutout.yMax - Tolerance), "title clears the portrait aperture");

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
                Assert.That(objective.worldBound.yMax, Is.LessThan(tools.worldBound.yMin));
                AssertInside(tools.worldBound, screen.Q("safe").worldBound, tools.name);
                if (profile.Cutout.width > 0f)
                    Assert.That(tools.worldBound.Overlaps(new Rect(profile.Cutout.position / profile.Scale, profile.Cutout.size / profile.Scale)), Is.False, tools.name);
                bool lastCapture = profile.Name == Profiles[Profiles.Length - 1].Name && text.Locale == text.Locales[text.Locales.Count - 1];
                return lastCapture ? CheckButtonFeedback(screen, left, target, $"hud-{profile.Name}-{text.Locale}") : null;
            });
        }

        [UnityTest]
        public IEnumerator InventoryReflowsOneSetOfInstances()
        {
            InventoryReorder reorder = null;
            VisualElement[] cards = new VisualElement[6];
            string[] itemIds = { "rope", "map", "compass", "lantern", "salve", "rations" };
            int[] counts = { 2, 1, 1, 1, 3, 4 };
            yield return Review("Inventory", (screen, text) =>
            {
                VisualTreeAsset item = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Assets + "Item.uxml");
                VisualElement grid = screen.Q("tiles");
                text.Bind(screen.Q<Label>("capacity"), "inventory.capacity", itemIds.Length, 12);
                for (int index = 0; index < cards.Length; index++)
                {
                    item.CloneTree(grid);
                    VisualElement tile = grid.ElementAt(index);
                    cards[index] = tile;
                    tile.userData = itemIds[index];
                    text.Bind(tile.Q<Label>("item-icon"), $"items.{itemIds[index]}.icon");
                    text.Bind(tile.Q<Label>("item-name"), $"items.{itemIds[index]}.name");
                    text.Bind(tile.Q<Label>("item-count"), "inventory.quantity", counts[index]);
                }
                reorder = new InventoryReorder(screen);
            }, (screen, profile, text, target) =>
            {
                VisualElement grid = screen.Q("tiles");
                Assert.That(grid.childCount, Is.EqualTo(cards.Length));
                Assert.That(grid.worldBound.center.x, Is.EqualTo(screen.Q("safe").worldBound.center.x).Within(Tolerance));
                AssertInside(grid.worldBound, screen.Q("safe").worldBound, "inventory grid");
                int columns = profile.WideBody ? 3 : 2;
                for (int index = 0; index < cards.Length; index++)
                {
                    VisualElement tile = grid.ElementAt(index);
                    Assert.That(tile, Is.SameAs(cards[index]));
                    Rect bounds = tile.worldBound;
                    int column = index % columns;
                    Assert.That(bounds.xMin, Is.EqualTo(cards[column].worldBound.xMin).Within(Tolerance), itemIds[index] + " column alignment");
                    Assert.That(bounds.yMin, Is.EqualTo(cards[index - column].worldBound.yMin).Within(Tolerance), itemIds[index] + " row alignment");
                    // Measure adjacent edges. Native pixel snapping rounds each gap independently.
                    if (column > 0)
                        Assert.That(bounds.xMin - cards[index - 1].worldBound.xMax, Is.EqualTo(12f).Within(Tolerance), itemIds[index] + " horizontal gap");
                    if (index >= columns)
                        Assert.That(bounds.yMin - cards[index - columns].worldBound.yMax, Is.EqualTo(12f).Within(Tolerance), itemIds[index] + " vertical gap");
                    AssertInside(bounds, grid.worldBound, itemIds[index]);
                    Rect identity = tile.Q("item-identity").worldBound;
                    Label icon = tile.Q<Label>("item-icon");
                    Label name = tile.Q<Label>("item-name");
                    AssertInside(icon.worldBound, identity, "inventory icon");
                    AssertInside(name.worldBound, identity, "inventory name");
                    Assert.That(icon.worldBound.xMax, Is.LessThan(name.worldBound.xMin));
                    Assert.That((icon.worldBound.xMin + name.worldBound.xMax) / 2f, Is.EqualTo(identity.center.x).Within(Tolerance));
                }
                return CheckInventoryReorder(screen, reorder, cards, profile, text, target);
            });
        }

        [UnityTest]
        public IEnumerator SettingsKeepRowsContainedAndCardsAligned()
        {
            yield return Review("Settings", (screen, text) =>
            {
                BindSetting(screen, text, "display", 0.8d);
                BindSetting(screen, text, "frame-rate", 60);
                BindSetting(screen, text, "environment", 0.65d);
                BindSetting(screen, text, "music", 0.5d);
            }, (screen, profile, text, target) =>
            {
                Rect group = screen.Q("settings-groups").worldBound;
                Rect display = screen.Q("display").worldBound;
                Rect audio = screen.Q("audio").worldBound;
                AssertInside(group, screen.Q("safe").worldBound, "settings group");
                AssertInside(display, group, "display card");
                AssertInside(audio, group, "audio card");
                Assert.That(display.width * profile.Scale, Is.EqualTo(audio.width * profile.Scale).Within(Tolerance), profile.Name);
                Assert.That(display.height * profile.Scale, Is.EqualTo(audio.height * profile.Scale).Within(Tolerance), profile.Name);
                if (!profile.WideBody)
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
                }
                return null;
            });
        }

        static void BindSetting(VisualElement screen, LayoutText text, string key, object value)
        {
            TemplateContainer row = screen.Q<TemplateContainer>(key + "-row");
            Assert.That(row, Is.Not.Null, key);
            Assert.That(row.childCount, Is.EqualTo(2), key);
            text.Bind(row.Q<Label>("caption"), $"settings.{key}-label");
            text.Bind(row.Q<Label>("value"), $"settings.{key}-value", value);
        }

        [UnityTest]
        public IEnumerator ConfirmationMatchesCanvas()
        {
            CanvasConfirmation canvas = null;
            string choice = null;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            try
            {
                yield return Review("Dialog", (screen, text) =>
                {
                    VisualElement dialog = screen.Q("dialog");
                    Button stay = dialog.Q<Button>("stay");
                    Button leave = dialog.Q<Button>("leave");
                    foreach (TextElement element in dialog.Query<TextElement>().ToList())
                        element.style.unityFont = font;
                    stay.clicked += () => { choice = stay.name; dialog.style.display = DisplayStyle.None; };
                    leave.clicked += () => { choice = leave.name; dialog.style.display = DisplayStyle.None; };
                    canvas = new CanvasConfirmation(font);
                }, (screen, profile, text, target) =>
                {
                    VisualElement dialog = screen.Q("dialog");
                    AssertInside(dialog.worldBound, screen.Q("safe").worldBound, "confirmation dialog");
                    Assert.That(Vector2.Distance(dialog.worldBound.center, screen.Q("safe").worldBound.center), Is.LessThanOrEqualTo(Tolerance));
                    Rect actions = dialog.Q("actions").worldBound;
                    foreach (string name in new[] { "stay", "leave" })
                    {
                        Button button = dialog.Q<Button>(name);
                        AssertInside(button.worldBound, actions, name);
                    }

                    canvas.ApplyLocale(text);
                    canvas.Draw(profile.Size, profile.Safe, profile.Scale, $"{profile.Name}-{text.Locale}");
                    foreach (string name in new[] { "heading", "message", "stay", "leave" })
                        Assert.That(canvas.CaptionText(name), Is.EqualTo(screen.Q<TextElement>(name).text), name);
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
                    Assert.That(choice, Is.EqualTo(action));
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
            const int entryCount = 12;
            yield return Review("Quests", (screen, text) =>
            {
                ScrollView list = screen.Q<ScrollView>("quest-scroll");
                text.Bind(screen.Q<Label>("entry-count"), "quests.entry-count", entryCount);
                text.Bind(screen.Q<Label>("journal-footer"), "quests.journal-footer", 4, new DateTime(2020, 1, 4, 6, 20, 0));
                for (int number = 1; number <= entryCount; number++)
                {
                    Label entry = new Label();
                    entry.AddToClassList("quest-row");
                    text.Bind(entry, $"quests.entry-{number}");
                    list.Add(entry);
                }
            }, CheckQuestScroll);
        }

        static IEnumerator CheckQuestScroll(VisualElement screen, LayoutProfile profile, LayoutText text, RenderTexture target)
        {
            VisualElement journal = screen.Q("journal");
            VisualElement header = screen.Q("journal-header");
            VisualElement footer = screen.Q("journal-footer");
            ScrollView list = screen.Q<ScrollView>("quest-scroll");
            VisualElement last = list.contentContainer.ElementAt(list.contentContainer.childCount - 1);
            Rect pane = screen.Q("safe").worldBound;
            AssertInside(journal.worldBound, pane, "trail journal");
            Assert.That(journal.worldBound.xMin - pane.xMin, Is.GreaterThan(6f), "journal left gutter");
            Assert.That(pane.xMax - journal.worldBound.xMax, Is.GreaterThan(6f), "journal right gutter");
            Rect heading = header.Q("heading").worldBound;
            Rect entryCount = header.Q("entry-count").worldBound;
            Assert.That((entryCount.xMin - heading.xMax) * profile.Scale, Is.GreaterThanOrEqualTo(12f * profile.Scale - Tolerance), text.Locale);
            AssertInside(entryCount, header.worldBound, "entry count");
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
            using (NativeReadback image = new NativeReadback(target)) image.SaveIfRequested($"quests-{profile.Name}-{text.Locale}-last.png");
        }

        static IEnumerator Review(string name, Action<VisualElement, LayoutText> bind, Func<VisualElement, LayoutProfile, LayoutText, RenderTexture, IEnumerator> check,
            Action<LayoutProfile> onProfile = null)
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
                LayoutFeedback.Enter(screen, false);
                VisualElement cutout = new VisualElement { pickingMode = PickingMode.Ignore };
                cutout.AddToClassList("cutout");
                screen.Add(cutout);
                string scope = name.ToLowerInvariant();
                LayoutText strings = new LayoutText(AssetDatabase.LoadAssetAtPath<TextAsset>(Assets + "LayoutStrings.json"));
                foreach (TextElement element in screen.Query<TextElement>(className: "localized").ToList())
                    strings.Bind(element, $"{scope}.{element.name}");
                bind(screen, strings);
                ScrollView journal = screen.Q<ScrollView>("quest-scroll");
                using JournalScrollAnchor journalAnchor = journal == null ? null : new JournalScrollAnchor(journal);
                bool firstView = true;
                Func<bool> idleButtons = null;
                bool inspectMiddleOnTransfer = false;
                foreach (LayoutProfile profile in Profiles)
                {
                    Focusable priorFocus = screen.focusController.focusedElement;
                    VisualElement lastVisibleEntry = null;
                    if (journal != null && current != null)
                    {
                        VisualElement last = journal.contentContainer.ElementAt(journal.contentContainer.childCount - 1);
                        if (!inspectMiddleOnTransfer)
                        {
                            AssertInside(last.worldBound, journal.contentViewport.worldBound, "outgoing final entry");
                            lastVisibleEntry = last;
                        }
                    }
                    float priorScroll = journal?.scrollOffset.y ?? 0f;
                    journalAnchor?.BeforeViewportChange();
                    RenderTexture next = new RenderTexture(profile.Size.x, profile.Size.y, 24);
                    settings.targetTexture = next;
                    Object.Destroy(current);
                    current = next;
                    settings.referenceResolution = profile.Portrait ? new Vector2Int(2160, 3840) : new Vector2Int(3840, 2160);
                    // USS uses quarter-resolution design units. PanelSettings projects them directly.
                    // This receiver declares a 1:1 minimum for its compact, low-density test window.
                    settings.scale = 4f * profile.Scale / profile.ProportionalScale;
                    ApplyGeometry(screen, cutout, profile);
                    onProfile?.Invoke(profile);
                    yield return null;
                    yield return null;
                    if (priorFocus != null)
                        Assert.That(screen.focusController.focusedElement, Is.SameAs(priorFocus), profile.Name);
                    if (journal != null && !firstView)
                    {
                        if (inspectMiddleOnTransfer)
                        {
                            Assert.That(priorScroll, Is.GreaterThan(0f), "outgoing mid-list position");
                            Assert.That(journal.scrollOffset.y, Is.EqualTo(priorScroll).Within(Tolerance), profile.Name + " retained offset");
                        }
                        else
                            AssertInside(lastVisibleEntry.worldBound, journal.contentViewport.worldBound, profile.Name + " retained quest");
                        journal.scrollOffset = Vector2.zero; // Start the standard capture after observing the transferred state.
                    }
                    for (int locale = 0; locale < strings.Locales.Count; locale++)
                    {
                        Focusable focus = screen.focusController.focusedElement;
                        Vector2 scroll = journal?.scrollOffset ?? Vector2.zero;
                        strings.SelectLocale(locale);
                        yield return null;
                        yield return null;
                        if (locale > 0)
                        {
                            Assert.That(screen.focusController.focusedElement, Is.SameAs(focus), strings.Locale);
                            if (journal != null)
                            {
                                Assert.That(journal.scrollOffset, Is.EqualTo(scroll), strings.Locale);
                                journal.scrollOffset = Vector2.zero;
                                yield return null;
                            }
                        }

                        if (firstView)
                        {
                            yield return new WaitUntil(() => !screen.ClassListContains("view-entering") && screen.Q("safe").resolvedStyle.opacity >= 0.999f,
                                TimeSpan.FromSeconds(3), () => Assert.Fail("Screen entrance did not settle"));
                            idleButtons = CaptureIdleButtonStyles(screen);
                            firstView = false;
                        }
                        Assert.That(document.rootVisualElement.Q("screen"), Is.SameAs(screen));
                        Assert.That(screen.worldBound.width * profile.Scale, Is.EqualTo(profile.Size.x).Within(Tolerance));
                        Assert.That(screen.worldBound.height * profile.Scale, Is.EqualTo(profile.Size.y).Within(Tolerance));
                        AssertVisibleTextFits(strings);
                        if (idleButtons != null)
                        {
                            yield return new WaitUntil(idleButtons, TimeSpan.FromSeconds(3), () => Assert.Fail("Button feedback did not settle"));
                            yield return null;
                        }
                        using (NativeReadback image = new NativeReadback(current))
                        {
                            Assert.That(image.BrightPixels, Is.GreaterThan(100), $"{name} {profile.Name} {strings.Locale} has no bright foreground");
                            image.SaveIfRequested($"{scope}-{profile.Name}-{strings.Locale}.png");
                        }
                        IEnumerator checkFrames = check(screen, profile, strings, current);
                        if (checkFrames != null) yield return checkFrames;
                    }
                    // Exercise the opposite resize behavior when opening the taller inner display mid-list.
                    inspectMiddleOnTransfer = journal != null && profile.Name == "duo-outer-landscape-right";
                    if (inspectMiddleOnTransfer)
                    {
                        journal.scrollOffset = new Vector2(0f, 60f);
                        yield return null;
                    }
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
            screen.EnableInClassList("wide-body", profile.WideBody);
            screen.EnableInClassList("narrow-body", profile.NarrowBody);
            screen.EnableInClassList("small-pane", profile.SmallPane);
            screen.EnableInClassList("short-body", safePixels.height / scale < 700f);
            VisualElement safe = screen.Q("safe");
            safe.style.left = safePixels.xMin / scale;
            safe.style.right = (profile.Size.x - safePixels.xMax) / scale;
            safe.style.top = safePixels.yMin / scale;
            safe.style.bottom = (profile.Size.y - safePixels.yMax) / scale;
            VisualElement header = screen.Q("header");
            if (header != null)
            {
                float left = safePixels.xMin;
                float right = safePixels.xMax;
                float top = 14f;
                if (profile.Header == HeaderMode.Safe) top += safePixels.yMin / scale;
                if (profile.Header == HeaderMode.Outer && profile.Cutout.center.y < profile.Size.y / 2f)
                {
                    if (profile.Cutout.center.x > profile.Size.x / 2f) right = profile.Cutout.xMin;
                    else left = profile.Cutout.xMax;
                    top = profile.Cutout.center.y / scale - 24f;
                }
                header.style.left = left / scale + 18f;
                header.style.right = (profile.Size.x - right) / scale + 18f;
                header.style.top = top;
                float titleOffset = profile.Header == HeaderMode.Standard ? safePixels.yMin / scale : 0f;
                header.Q("center-title").style.top = profile.SmallPane ? Mathf.Max(54f, titleOffset) : titleOffset;
            }
            cutout.style.borderTopLeftRadius = cutout.style.borderTopRightRadius =
                cutout.style.borderBottomLeftRadius = cutout.style.borderBottomRightRadius = profile.Header == HeaderMode.Outer ? profile.Cutout.width / (2f * scale) : 18f;
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

        static void AssertVisibleTextFits(LayoutText strings)
        {
            foreach (TextElement element in strings.BoundElements)
            {
                VisualElement ancestor = element;
                while (ancestor != null && ancestor.resolvedStyle.display != DisplayStyle.None) ancestor = ancestor.parent;
                if (ancestor == null) AssertTextFits(element);
            }
        }

        static void AssertTextFits(TextElement text)
        {
            Vector2 ink = text.MeasureTextSize(text.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            if (ink.x > text.contentRect.width + Tolerance && text.resolvedStyle.whiteSpace == WhiteSpace.Normal)
                ink = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);
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

        enum HeaderMode { Standard, Outer, Safe }

        readonly struct LayoutProfile
        {
            public readonly string Name;
            public readonly Vector2Int Size;
            public readonly Rect Safe;
            public readonly Rect Cutout;
            public readonly HeaderMode Header;
            // Two 284-unit Settings cards with 7-unit margins on each side need 596 units.
            public bool WideBody => Safe.width / Scale >= 600f;
            public bool NarrowBody => Safe.width / Scale < 400f;
            // Two 88-unit controls, 36 units of margins and a 133-unit translated title need 345 units.
            public bool SmallPane => Safe.width / Scale < 346f;
            public bool Portrait => Size.y > Size.x;
            public float Scale => Mathf.Max(1f, ProportionalScale);
            public float ProportionalScale => Portrait ? Mathf.Min(Size.x / 540f, Size.y / 960f) : Mathf.Min(Size.x / 960f, Size.y / 540f);

            public LayoutProfile(string name, int width, int height) : this(name, width, height, new Rect(0, 0, width, height), default)
            {
            }

            public LayoutProfile(string name, int width, int height, Rect safe, Rect cutout, HeaderMode header = HeaderMode.Standard)
            {
                Name = name;
                Size = new Vector2Int(width, height);
                Safe = safe;
                Cutout = cutout;
                Header = header;
            }
        }
    }
}
