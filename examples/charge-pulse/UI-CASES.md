# Unity UI Toolkit case inventory

The Charge Pulse sample mounts ten UI Toolkit screens, switches between English and German on the same tree, and replays 25 viewport configurations. Each screen has 50 primary viewport and language combinations. Additional checks cover daily use and edge cases. For example, the HUD runs its held, disabled and reduced-motion sequence in the 4K German landscape case, and the form checks notification expiry in compact English. The device profiles supply coordinates to a real Unity panel rendered in Play Mode.

[Compare all ten screens on a small phone and tablet](ui-device-gallery.jpg)

## Verified results

Two independent Unity 6000.6.7f1 launches passed all ten screen tests across 25 viewport profiles in English and German. Both used identical fixture files and produced 500 primary combinations and 414 additional captures of interactive states or Canvas comparisons. All 1,828 PNGs decoded at their expected framebuffer size and contained rendered content. The gallery uses the first launch.

| Screen | Primary views | Additional views | Total frames per run |
| --- | ---: | ---: | ---: |
| Expedition menu | 50 | 50 route briefings | 100 |
| Gameplay controller | 50 | 6 held and paused states | 56 |
| Expedition HUD | 50 | 5 input feedback states | 55 |
| Waypoint map | 50 | 0 | 50 |
| Inventory | 50 | 1 active drag | 51 |
| Trail atlas | 50 | 50 sort surfaces and 2 errors | 102 |
| Settings overview | 50 | 0 | 50 |
| Preferences | 50 | 200 tab, help, confirmation and saved states | 250 |
| Confirmation dialog | 50 | 50 Canvas comparisons | 100 |
| Trail journal | 50 | 50 end-of-list views | 100 |
| **Total** | **500** | **414** | **914** |

At 320 × 568 the inventory extended 16 pixels past the viewport, the HUD footer extended four pixels past each side, and a German confirmation heading needed 277 panel units in a 258-unit space. The corrected grid fits two columns, the HUD keeps its full-size actions, and the heading wraps onto two lines above its buttons. The preferences form also keeps its footer reachable in a 390-pixel-high hinge pane. Its text field, slider and toggle have native input regions of at least 44 × 44 panel units. The native tests also caught a wide-to-compact atlas transition that hid the search field above the detail page.

Compare the [held virtual stick and action](ui-device-captures/compact-controller-held.png) with the [inventory drag and its highlighted destination](ui-device-captures/compact-inventory-drag.png). Both are full-resolution 360 × 780 Unity Editor frames.

## Screens and controls

| Screen | States and controls exercised | What must remain usable |
| --- | --- | --- |
| [Expedition menu](Assets/Editor/Tests/ResponsiveLayouts/Expedition.uxml) | Full-screen menu, saved expedition, route briefing and return | The page fills the window. Its foreground reflows inside the active pane. Opening the briefing and returning restores the initiating control. |
| [Gameplay controller](Assets/Editor/Tests/ResponsiveLayouts/ControllerSurface.uxml) | Virtual stick and action, two simultaneous touch contacts, drag, clamp, release, cancel, directional navigation, submit and pause/resume | A second contact can pulse while movement is held. The camera and fold clear the controls. Pausing transfers focus and prevents play input. Resize releases invalid contacts. |
| [Expedition HUD](Assets/Editor/Tests/ResponsiveLayouts/Hud.uxml) | Corner actions, centered title, objective, meter, status, focus, held/released/cancelled press, disabled and reduced-motion feedback | Visible labels and hit areas clear local obstructions. Press feedback preserves the target geometry and dispatches one action. |
| [Waypoint map](Assets/Editor/Tests/ResponsiveLayouts/Waypoint.uxml) | Map viewport, zoom, point projected from a Unity camera, edge marker and dragging | Zoom moves the projected point. An offscreen point clamps to the reachable edge and a point behind the camera disappears. Drag updates the world position and releases capture. |
| [Inventory](Assets/Editor/Tests/ResponsiveLayouts/Inventory.uxml) | Six instances of one item template, two- or three-column grid, drag to reorder, cancellation and two-submit alternative | Drop or submit moves the selected item once. An outside release preserves order. Items keep their identities through resizing. Actual icon and text widths fit their slots with consistent gaps. |
| [Trail atlas](Assets/Editor/Tests/ResponsiveLayouts/Atlas.uxml) | Search field, 64 virtualized items, compact detail page, wide master-detail view, no results, loading, error, retry, bottom sheet and side drawer | After scrolling and filtering, a recycled row opens its newly bound item exactly once. Returning from compact detail restores focus to the selected, visible row. The sort surface blocks the underlying controls and changes the collection order. |
| [Settings overview](Assets/Editor/Tests/ResponsiveLayouts/Settings.uxml) | Display and audio cards with shared field rows, localized labels, numbers and percentages | Cards stack or sit side by side at the usable-width boundary. Labels and right-aligned values stay inside their own row. |
| [Preferences](Assets/Editor/Tests/ResponsiveLayouts/Preferences.uxml) | Tabs, text field, toggle, slider, scrollable settings, anchored help popover, confirmation modal, save notification and disabled action | Focused fields and footer actions stay reachable. Tab selection remains visible. Cancel and save have distinct outcomes. The modal shields the underlying page. |
| [Confirmation dialog](Assets/Editor/Tests/ResponsiveLayouts/Dialog.uxml) | Open dialog, two localized actions and a separately rendered Canvas equivalent | Both native hosts center on the declared safe area, fit their captions and dispatch matching action IDs. |
| [Trail journal](Assets/Editor/Tests/ResponsiveLayouts/Quests.uxml) | Fixed header and footer, long localized list, first item, scroll to end and mid-list resize | The list owns the scroll viewport. The final row stays reachable and the live resize preserves reading position. |

The [saved gameplay HUD](Assets/Prefabs/ChargePulse/ChargePulseHud.uxml) and [existing review HUD](Assets/Prefabs/ChargePulse/ReviewHud.uxml) also have native scene and interaction tests. The [shared item](Assets/Editor/Tests/ResponsiveLayouts/Item.uxml) and [settings row](Assets/Editor/Tests/ResponsiveLayouts/SettingRow.uxml) are mounted and checked through the screens that own them. The separate [sliced/tiled Toolkit button](../image-optimization/README.md#one-backing-for-a-panel-and-two-buttons) verifies that an expanded button preserves its artwork edges and responds in its expanded hit region.

## Device profiles

Coordinates below are framebuffer pixels. The fixture selects UI scale and declares local obstructions in [ResponsiveLayoutCases.cs](Assets/Editor/Tests/ResponsiveLayouts/ResponsiveLayoutCases.cs). Body layout follows the available pane in panel units. The same pixel size may represent different display identities, insets or camera reservations.

| Geometry | Profiles and framebuffer dimensions |
| --- | --- |
| Small and compact phones | 320 × 568 and 360 × 780 |
| Centered portrait camera | 540 × 1170 and its 1080 × 2340 replay |
| Landscape phone cutout | 1170 × 540 with a left obstruction and with a right obstruction |
| Android gesture-area fixture | 1080 × 2400 with top camera and bottom system inset |
| iPhone Duo outer profiles | 1398 × 2034 upright, 2034 × 1398 in each landscape turn |
| iPhone Duo inner profiles | Same-size 1398 × 2034 outer-to-inner transfer, 1878 × 2670 portrait, 2670 × 1878 landscape |
| Separating hinges | Left and right 490 × 720 panes in a 1000 × 720 window, upper 1000 × 390 pane in a 1000 × 800 window |
| Layout breakpoint | 599 × 960 and 600 × 960 |
| Short handheld | 800 × 480 |
| 4:3 tablet | 1536 × 2048 portrait and 2048 × 1536 landscape |
| Desktop and ultrawide | 1440 × 900 and 2560 × 1080 |
| 4K design references | 2160 × 3840 portrait and 3840 × 2160 landscape |

Gamepad-style checks use Unity navigation events. Touch and drag checks dispatch pointer events through mounted controls. The map uses a real Unity camera and a screen-space Toolkit marker. Search/loading/error use local fixture data. Platform keyboards, hardware gamepads, physical folding, screen readers and store transactions have separate device or service acceptance requirements.

## Simulator Players

A focused arm64 Unity 6000.6.7f1 Player mounted the German controller and expedition menu on both iOS 27.2 models. It picked each action through the live panel, submitted a navigation event, and verified Pulse, Pause, Resume, the route briefing and Back. Each model produced native frames for the controller, pause, menu and briefing. A fresh Player build from the delivered fixture files reproduced all eight screenshots byte-for-byte. Coordinates below use Unity’s bottom-left screen origin.

| Simulator | Native viewport | Reported safe area (x, y, width, height) | Native frames |
| --- | --- | --- | --- |
| iPhone Duo, outer display | 1398 × 2034 | 0, 102, 1146, 1932 | [Controller](ui-device-captures/duo-outer-controller.png) · [Full-screen menu](ui-device-captures/duo-outer-expedition.png) |
| iPad Pro 13-inch (M5) | 2064 × 2752 | 0, 40, 2064, 2712 | [Controller](ui-device-captures/ipad-controller.png) · [Full-screen menu](ui-device-captures/ipad-expedition.png) |

The controller action measured 111.86 × 48.14 panel units on the Duo and 111.98 × 47.79 on the iPad. The menu stacked in the narrower Duo safe area and displayed its map beside the route on iPad. Switching the inner display power on and the outer power off left the same running Player at 1398 × 2034. Its Pause action still worked, and the inner display returned an all-black 2007 × 2853 frame. Inner-display layout and transfer evidence comes from the Editor matrix. The two-finger gestures and directional navigation were exercised there with scripted input. The Android gesture-inset profile also ran in the Editor. This environment had no connected Android device or configured emulator image.

## Reproduce the native matrix

Use a task-owned Charge Pulse project copy, Unity 6000.6.7f1, and a graphics device. The Python verifier uses Pillow. The gallery uses Pillow 10.1 or later for its default font, or accepts a TrueType file with `--font`. Follow the [sample runner setup](README.md#run-the-focused-tests). Create an empty `<run>/frames` directory and set `CHARGE_PULSE_EVIDENCE` to that path. The fixture writes native PNGs there. Run all methods with the same source and pass the resulting NUnit XML and frame directory to the [matrix verifier](../../.agents/skills/testing-skill/tests/verify_ui_case_matrix.py).

```text
python3 -B unity-code-skill/scripts/unity_cli_daily.py test "<task-project>" --non-interactive --no-pager --mode EditMode --filter ResponsiveLayoutCases --output "<run>/result.xml" -- -logFile "<run>/unity.log"
python3 -B .agents/skills/testing-skill/tests/verify_ui_case_matrix.py --results "<run>/result.xml" --frames "<run>/frames" --gallery "<run>/ui-device-gallery.jpg"
```

The verifier requires a passing native method and each named, decodable frame at its declared size. It also checks the alternate-state images and Canvas comparisons. A skipped coroutine or a partially rendered screen fails the inventory even if the test runner reports success.
