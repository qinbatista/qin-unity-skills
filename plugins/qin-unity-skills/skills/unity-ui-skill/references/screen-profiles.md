# Choosing a screen layout

Start with the [shared configuration](../assets/screen-layouts.json) and its [Unity reader](../assets/ScreenLayoutPolicy.cs). Give each screen one geometry owner and bind its existing groups to the selected composition. Keep these inputs as a pair under the receiving project's UI owner. Import the JSON as a TextAsset, bind it once and call `ScreenLayoutPolicy.Read`. Both UI Toolkit and Canvas can use the result.

## Choose a composition

Select from the current app window or usable pane. The supplied breakpoints are editable design defaults. Device density, brand, model, marketing size and the orientation sensor are unnecessary for selection. Read the first inclusive aspect limit in the configuration, with the final entry covering the rest. A square window uses the wide-portrait composition.

| Profile | Body | Header and actions |
| --- | --- | --- |
| Tall portrait | Extend the background and scrollable content vertically. Keep persistent actions within reach. | Use one row at the authored scale when the painted groups fit. |
| Portrait | Use one content column between the header and bottom actions. | Align clear corner actions and status on their authored row. |
| Wide portrait or square | Center a readable content column. Use the extra width for the scene, grid or margins. | Reserve a second status row under the corner controls. |
| Landscape | Arrange compatible body regions side by side. | Fit the status and actions to the usable width. Reflow if the measured groups collide. |
| Ultrawide | Keep the primary interaction in a bounded central area. Extend scenery and optional secondary content into the wings. | Anchor persistent controls to their declared reachable region. Preserve readable text and touch sizes. |

Project the authored geometry through `Scale(pane)` once. Small and large windows with the same aspect share a composition. Choose a compact group or scrolling when the actual text, touch targets or secondary pane would be too small. A change to window size, system UI, fold pose or display can change the result while the same screen stays open.

## Apply hardware and system geometry

| Current window condition | Placement |
| --- | --- |
| Clear display or flat inner folding display | Paint the whole window. Lay out foreground in the available area. A flat hinge creates a division only when the platform reports a separating feature. |
| Center notch, island or corner hole | Compare each group's artwork and hit envelope with the reported regions. Move the intersecting group below the region with clearance. Keep the other groups at their verified anchors. |
| Outer display with a side camera | Use the full window for the scene and background. Place Settings, Return and other available actions in a vertical column along the reported physical edge. Start below the camera when the column intersects it. A camera-free portion of that side stays usable. Preserve the column's order when the camera region expands. |
| Permanent system bar, gesture edge or unknown camera footprint | Keep affected interactive groups inside the corresponding safe edge. With only an inset rectangle, use the entire reported safe rectangle for essential foreground until clearance is known. Re-evaluate when precise regions become available. |
| Separating hinge or tabletop pose | Use the reported division to split the active window. Keep each control, dialog, gesture surface and text group within one usable pane. A single flow uses the largest pane, with a configurable tie preference. A two-pane flow can use both. The background retains the full window. |
| Split view, floating window or external display | Use the application's current window and intersect its insets and regions with that window. Run the same selection again after resizing or moving. |

For iPhone Duo, use the full outer and inner window. On the upright outer display, put the right action column below the right camera and preserve background pixels to the right edge. On the inner display, read the current camera and fold regions afresh. Honor the physical side through language changes. On rotation or in Split View the reserved side can change. Apple documents edge-to-edge presentation when built with the iOS 27.1 SDK or later and provides `UIViewReservedRegion` for custom interfaces. A compatibility-sized player window bounds what a Unity layout can draw. [Apple design guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo) · [Apple SDK and full-screen behavior](https://developer.apple.com/videos/play/tech-talks/111461/)

Get Unity pixel rectangles from the active rendering surface. Convert `Screen.safeArea` and `Screen.cutouts` from bottom-left to top-left exactly once. Keep platform regions already in top-left coordinates in that convention. Read window-local native reservations for dynamic overlays or folds when Unity supplies only a rectangle. Android exposes cutout bounds and separating fold state independently. [Unity safe area](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html) · [Android cutouts](https://developer.android.com/develop/ui/views/layout/display-cutout) · [Android folding features](https://developer.android.com/develop/ui/compose/layouts/adaptive/foldables/make-your-app-fold-aware)

## Bind the result

1. Stretch the scene background and modal scrim to the app window. Keep decorative strips attached to their own foreground controls within the selected pane.
2. For each explicitly separating band, call `TrySplit` on the pane it spans. Clip subsequent regions to that pane. Choose the active pane by role, or use `PrimaryPane` for a single flow. A nonseparating crease or camera enters the ordinary obstruction path only if it occludes pixels.
3. Use `Select(pane).body` and `.header` to choose the corresponding authored group styles. Use the active PanelSettings or CanvasScaler to convert the returned pixel geometry into local units. Bind controls and retain navigation state independently of the style.
4. For an edge column, measure its complete artwork and hit rectangles, including spacing. Start from `ActionLane(pane, safeArea, exclusions, rightEdge)`. Anchor the candidate column to that lane's physical edge. Pass its bounds to `TryPlaceBelow` with all current occlusions and `Scale(pane)`. Apply the returned origin to the whole column. If placement fails, try a narrower approved group, the other available lane, or scrolling before exposing its controls. Preserve the gameplay view underneath.
5. Recalculate on actual geometry changes, including an inset-only change. Keep focus and the active page or game state. Discard the previous display's geometry when the window transfers.

For a Toolkit screen with one physical-right action column, an existing geometry callback can bind the config like this. The same pixel rectangle can drive a Canvas RectTransform through its owning CanvasScaler.

```csharp
var profile = policy.Select(pane);
root.EnableInClassList("hud-second-row", profile.header == "second-row");
Rect lane = policy.ActionLane(pane, safeArea, exclusions, rightEdge: true);
Rect column = new(lane.xMax - margin - groupWidth, lane.yMin + margin, groupWidth, groupHeight);
if (policy.TryPlaceBelow(lane, column, exclusions, policy.Scale(pane), out Rect placed))
    PositionActionGroupInPanel(placed);
else
    ShowCompactActionGroup(lane);
```

The screen implements the last two calls with its existing group and input bindings. Measure the union of its controls before placement and keep the scrim and artwork on the stretched root.

## Accept a new screen

Replay the five compositions and the applicable overlays through the real screen. Capture a full-resolution reference and a smaller same-aspect case. Include a side camera, expanded camera, clear return, flat inner display, active division and a resized window. Exercise Settings or the equivalent action at the obstructed edge, then its return path. Measure full-window background coverage, all four edges, complete group bounds and actual input hits. Include adjacent readouts, menus and footer actions. Where the scene contains a world-space interaction, verify that its rendered bounds remain visible and usable in the active pane after a fold and return. Check the [language matrix](layout-and-input.md#verify-each-locale). Record whether each input came from a real window, measured device replay or synthetic geometry. A named device's published resolution identifies its framebuffer and supplies no camera rectangle.
