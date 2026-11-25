using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Qin.UnitySamples.Tests
{
    sealed partial class DeviceScreenBinder : IDisposable
    {
        readonly string asset;
        readonly VisualElement root;
        readonly LayoutText strings;
        readonly ControllerSurface controller;
        readonly InventoryReorder inventory;
        readonly AtlasDirectory atlas;
        readonly WaypointMap map;
        readonly GameObject rig;
        int actions;
        public VisualElement Primary { get; }
        public DeviceScreenBinder(string assetName, VisualElement screen, LayoutText text)
        {
            asset = assetName;
            root = screen;
            strings = text;
            switch (asset)
            {
            case "ControllerSurface":
                controller = new ControllerSurface(screen);
                Primary = screen.Q("pulse");
                break;
            case "Expedition":
                new ExpeditionMenu(screen);
                Primary = screen.Q("open-route");
                break;
            case "Preferences":
                new PreferencesForm(screen, text);
                Primary = screen.Q("help");
                break;
            case "Atlas":
                atlas = new AtlasDirectory(screen, text);
                Primary = screen.Q("sort");
                break;
            case "Waypoint":
                rig = new GameObject("Waypoint projection");
                rig.transform.position = new Vector3(0, 0, -10);
                var camera = rig.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                map = new WaypointMap(screen, camera);
                Primary = screen.Q("zoom-in");
                break;
            case "Hud":
                text.Bind(screen.Q<Label>("stamina-text"), "hud.stamina-text", 72, 100);
                text.Bind(screen.Q<Label>("elevation"), "hud.elevation", 2480);
                screen.Q<Button>("left-wing").clicked += () => actions++;
                screen.Q<Button>("right-wing").clicked += () => actions++;
                Primary = screen.Q("left-wing");
                break;
            case "Settings":
                BindSetting("display", 0.8d);
                BindSetting("frame-rate", 60);
                BindSetting("environment", 0.65d);
                BindSetting("music", 0.5d);
                Primary = screen.Q("display-row");
                break;
            case "Inventory":
                var item = Resources.Load<VisualTreeAsset>("UiCases/Item");
                Require(item != null, "missing UiCases/Item");
                string[] names = { "rope", "map", "compass", "lantern", "salve", "rations" };
                int[] counts = { 2, 1, 1, 1, 3, 4 };
                VisualElement grid = screen.Q("tiles");
                text.Bind(screen.Q<Label>("capacity"), "inventory.capacity", names.Length, 12);
                for (int i = 0; i < names.Length; i++)
                {
                    item.CloneTree(grid);
                    VisualElement card = grid.ElementAt(i);
                    card.userData = names[i];
                    text.Bind(card.Q<Label>("item-icon"), "items." + names[i] + ".icon");
                    text.Bind(card.Q<Label>("item-name"), "items." + names[i] + ".name");
                    text.Bind(card.Q<Label>("item-count"), "inventory.quantity", counts[i]);
                }
                inventory = new InventoryReorder(screen);
                Primary = grid.ElementAt(0);
                break;
            case "Dialog":
                Button stay = screen.Q<Button>("stay"), leave = screen.Q<Button>("leave");
                stay.clicked += CloseDialog;
                leave.clicked += CloseDialog;
                Primary = stay;
                break;
            case "Quests":
                ScrollView list = screen.Q<ScrollView>("quest-scroll");
                text.Bind(screen.Q<Label>("entry-count"), "quests.entry-count", 12);
                text.Bind(screen.Q<Label>("journal-footer"), "quests.journal-footer", 4,
                          new DateTime(2020, 1, 4, 6, 20, 0));
                for (int i = 1; i <= 12; i++)
                {
                    Label row = new Label();
                    row.AddToClassList("quest-row");
                    text.Bind(row, "quests.entry-" + i);
                    list.Add(row);
                }
                Primary = list;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(assetName), assetName, "unknown mounted case");
            }
        }
        public void SetExclusion(Rect cutout)
        {
            controller?.SetExclusion(cutout);
            map?.SetExclusion(cutout);
        }
        void BindSetting(string id, object value)
        {
            var row = root.Q<TemplateContainer>(id + "-row");
            strings.Bind(row.Q<Label>("caption"), "settings." + id + "-label");
            strings.Bind(row.Q<Label>("value"), "settings." + id + "-value", value);
        }
        void CloseDialog()
        {
            actions++;
            root.Q("dialog").style.display = DisplayStyle.None;
        }
        static void Require(bool ok, string detail)
        {
            if (!ok)
                throw new InvalidOperationException(detail);
        }
        public bool Validate()
        {
            if (Primary == null || Primary.panel == null || Primary.worldBound.width <= 0 ||
                Primary.worldBound.height <= 0)
                return false;
            VisualElement hit = Primary.panel.Pick(Primary.worldBound.center);
            return hit == Primary || (hit != null && Primary.Contains(hit));
        }
        static void Submit(VisualElement button)
        {
            var hit = button.panel.Pick(button.worldBound.center);
            Require(hit == button || hit != null && button.Contains(hit), "cannot pick " + button.name);
            button.Focus();
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
        }
        public IEnumerator Exercise()
        {
            if (asset == "Quests")
            {
                var view = (ScrollView)Primary;
                var last = view.contentContainer.ElementAt(11);
                view.ScrollTo(last);
                yield return null;
                yield return null;
                Require(view.scrollOffset.y > 0 && view.contentViewport.worldBound.Overlaps(last.worldBound),
                        "journal scroll");
                view.scrollOffset = Vector2.zero;
                yield break;
            }
            if (asset == "Settings")
            {
                Require(root.Q("settings-groups").childCount == 2 &&
                            Primary.Q<Label>("value").text == strings.Resolve("settings.display-value", .8d),
                        "settings values");
                yield break;
            }
            int before = actions;
            int moves = inventory?.Moves ?? 0;
            int pulses = controller?.Pulses ?? 0;
            Vector2 pin = map?.Pin.worldBound.center ?? Vector2.zero;
            Submit(Primary);
            yield return null;
            yield return null;
            switch (asset)
            {
            case "Hud":
                Require(actions == before + 1, "HUD action");
                break;
            case "Dialog":
                Require(actions == before + 1 && root.Q("dialog").resolvedStyle.display == DisplayStyle.None,
                        "dialog choice");
                root.Q("dialog").style.display = DisplayStyle.Flex;
                break;
            case "ControllerSurface":
                Require(controller.Pulses == pulses + 1, "pulse");
                Submit(root.Q("pause"));
                yield return null;
                yield return null;
                Require(controller.Paused, "pause");
                Submit(root.Q("resume"));
                yield return null;
                Require(!controller.Paused, "resume");
                break;
            case "Expedition":
                Require(root.Q("briefing").resolvedStyle.display != DisplayStyle.None, "briefing");
                Submit(root.Q("back"));
                yield return null;
                Require(root.Q("home").resolvedStyle.display != DisplayStyle.None, "return");
                break;
            case "Preferences":
                Require(root.Q("help-popover").resolvedStyle.display != DisplayStyle.None, "help popover");
                Submit(root.Q("close-help"));
                yield return null;
                Require(root.Q("help-popover").resolvedStyle.display == DisplayStyle.None, "close help");
                break;
            case "Atlas":
                Require(root.Q("sort-shade").resolvedStyle.display != DisplayStyle.None, "atlas sort sheet");
                Submit(root.Q("by-height"));
                yield return null;
                Require((int)atlas.Rows.itemsSource[0] == 63, "sort by height");
                break;
            case "Inventory":
                VisualElement grid = root.Q("tiles"), destination = grid.ElementAt(1);
                Submit(destination);
                yield return null;
                Require(inventory.Moves == moves + 1 && grid.ElementAt(1) == Primary, "inventory reorder");
                Submit(Primary);
                Submit(destination);
                yield return null;
                break;
            case "Waypoint":
                Require(Vector2.Distance(pin, map.Pin.worldBound.center) > .1f, "zoom changed point");
                Submit(root.Q("zoom-out"));
                yield return null;
                break;
            }
        }
        public void Dispose()
        {
            controller?.Dispose();
            map?.Dispose();
            if (rig != null)
                UnityEngine.Object.Destroy(rig);
        }
    }
}
