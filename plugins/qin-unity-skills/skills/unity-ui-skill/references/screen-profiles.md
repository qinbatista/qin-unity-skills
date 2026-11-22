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
| Center notch, island or corner hole | Compare each group's artwork and hit envelope with the reported regions. Retain unobstructed anchors. Shift the affected corner action inward on its row. Reflow a centered group if its measured envelope cannot fit. |
| Outer display with a side camera | Paint to the window edges. Keep Settings or Return on its original top row and move its complete envelope horizontally toward the center by the minimum clearance. Keep the opposite corner and resource group at their ordinary anchors. The black appearance gives an existing corner button a solid black face and a recognizable action glyph aligned to its opposite control. Fill the top center and scene to both side edges. Repeat when the camera region expands or contracts. |
| Permanent system bar, gesture edge or unknown camera footprint | Keep affected interactive groups inside the corresponding safe edge. With only an inset rectangle, use the entire reported safe rectangle for essential foreground until clearance is known. Re-evaluate when precise regions become available. |
| Separating hinge or tabletop pose | Use the reported division to split the active window. Keep each control, dialog, gesture surface and text group within one usable pane. A single flow uses the largest pane, with a configurable tie preference. A two-pane flow can use both. The background retains the full window. |
| Split view, floating window or external display | Use the application's current window and intersect its insets and regions with that window. Run the same selection again after resizing or moving. |

For iPhone Duo, keep the outer background full-screen. Read the physical corner and its current reserved region independently of locale. The inner display selects wide portrait in a tall window and landscape in a wide window. Refresh geometry on every display, pose, rotation and Split View change. The App Store accepts a 1398 × 2034 outer portrait capture and 2007 × 2853 or 2853 × 2007 inner captures. A native player built with the iOS 27.1 SDK or later can use the full display. [Apple design guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo) · [App Store screenshot sizes](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/) · [Fullscreen SDK behavior](https://developer.apple.com/videos/play/tech-talks/111461/)

For an upright outer-display preview, Apple’s illustrated static camera is a black circle. Apple’s mechanical drawing gives a 6.07 mm diameter. At 460 ppi this projects to about 110 px (36.6 pt) on the full outer framebuffer. The illustrated display puts its edge about 88 px (29 pt) from the top and right, with about 1 pt of image-reading uncertainty. Use the [solid black aperture artwork](../assets/camera-aperture.svg) in device previews, with picking disabled. The native display supplies the physical aperture. A compact camera can share one solid black backing with a Settings or Return control. Use `TryGetCameraCap` to locate the rounded hardware end, draw a continuous horizontal rounded plate that encloses the button and the cap, and place a readable gear or chevron on the clear side. Keep the complete button hit area clear and the backing noninteractive. Verify continuity and coverage with the physical mask hidden. Expanded system UI uses the plain shifted button. Use the current platform exclusion for control placement, including expanded system UI. A silhouette, a platform exclusion, and a safe-area inset have different bounds. [Apple mechanical drawing, sheet 1](https://developer.apple.com/download/files/accessories/dimensional-drawings/iphone-duo.pdf#page=2) · [Apple outer-display illustrations](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo)

Get Unity pixel rectangles from the active rendering surface. Convert `Screen.safeArea` and `Screen.cutouts` from bottom-left to top-left exactly once. Keep platform regions already in top-left coordinates in that convention. Read window-local native reservations for dynamic overlays or folds when Unity supplies only a rectangle. Android exposes cutout bounds and separating fold state independently. [Unity safe area](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html) · [Android cutouts](https://developer.android.com/develop/ui/views/layout/display-cutout) · [Android folding features](https://developer.android.com/develop/ui/compose/layouts/adaptive/foldables/make-your-app-fold-aware)

## Bind the result

1. Stretch the scene background and modal scrim to the app window. Keep decorative strips attached to their own foreground controls within the selected pane.
2. For each explicitly separating band, call `TrySplit` on the pane it spans. Clip subsequent regions to that pane. Choose the active pane by role, or use `PrimaryPane` for a single flow. A nonseparating crease or camera enters the ordinary obstruction path only if it occludes pixels.
3. Use `Select(pane).body` and `.header` to choose the corresponding authored group styles. Use the active PanelSettings or CanvasScaler to convert the returned pixel geometry into local units. Bind controls and retain navigation state independently of the style.
4. Use `BodyAroundCorner` when a precise localized obstruction explains a side inset. It preserves a persistent side exclusion and returns the available body width. For a top corner action, include both its painting and hit bounds. Start with the physical edge from `TopRowLane` when precise local exclusions are available. Intersect the available row with the space left by stationary peers and any persistent system bars. Keep the authored Y and use `TryPlaceInRow` to find the nearest clear X. If the row has insufficient space, reflow its existing groups or use an approved compact variant. An unexplained side inset remains the edge for interactive content.
5. Recalculate on actual geometry changes, including an inset-only change. Keep focus and the active page or game state. Discard the previous display's geometry when the window transfers.

A Toolkit geometry callback can bind an authored physical-right top action as follows. The same pixel rectangle can drive a Canvas RectTransform through its owning CanvasScaler.

```csharp
var profile = policy.Select(pane);
root.EnableInClassList("hud-second-row", profile.header == "second-row");
Rect lane = policy.TopRowLane(pane, safeArea, exclusions, true, out bool sideCamera);
root.EnableInClassList("camera-action", sideCamera && policy.edgeActions.sideCameraAppearance == "black");
lane.xMin = Mathf.Max(lane.xMin, stationaryPeersRightEdge);
Rect candidate = new(lane.xMax - margin - groupWidth, authoredTop, groupWidth, groupHeight);
if (policy.TryPlaceInRow(lane, candidate, exclusions, policy.Scale(pane), true, out Rect placed))
    PositionActionGroupInPanel(placed);
else
    ShowCompactActionGroup(lane);
```

The screen implements the last two calls with its existing group and input bindings. Measure the union of its controls before placement and keep the scrim and artwork on the stretched root.

## Accept a new screen

Replay the five compositions and the applicable overlays through the real screen. Capture a full-resolution reference and a smaller same-aspect case. Compare the same outer viewport with and without its camera. Verify the black action shares its opposite control’s top and center line, has the minimum X displacement, and leaves its peers stationary. Compare the body width and actual pixels at the top center and right edge across the two inputs. Replay expansion and a clear return. Rotate the inner display portrait → landscape → portrait in one live scene. Check a separating fold and a resized window. Exercise Settings or the equivalent action at the obstructed edge, then its return path. Measure full-window background coverage, all four edges, complete group bounds and actual input hits. Compare adjacent readouts, menus and footer actions against the full painted backing as well as the button hit area. Where the scene contains a world-space interaction, verify that its rendered bounds remain visible and usable in the active pane after a fold and return. Check the [language matrix](layout-and-input.md#verify-each-locale). Record whether each input came from a real window, measured device replay or synthetic geometry. A named device's published resolution identifies its framebuffer and supplies no camera rectangle.
