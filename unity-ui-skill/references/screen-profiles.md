# Choosing a screen layout

Start with the [shared configuration](../assets/screen-layouts.json) and its [Unity reader](../assets/ScreenLayoutPolicy.cs). Give each screen one geometry owner and bind its existing groups to the selected composition. Keep these inputs as a pair under the receiving project's UI owner. Import the JSON as a TextAsset, bind it once and call `ScreenLayoutPolicy.Read`. Both UI Toolkit and Canvas can use the result.

## Choose a composition

Select from the current app window or usable pane. The supplied breakpoints are editable design defaults. Device density, brand, model, marketing size and the orientation sensor are unnecessary for selection. Read the first inclusive aspect limit in the configuration, with the final entry covering the rest. A square window uses the wide-portrait composition.

| Profile | Body | Header and actions |
| --- | --- | --- |
| Tall portrait | Extend the background and scrollable content vertically. Keep persistent actions within reach. | Use one row at the authored scale when the painted groups fit. |
| Portrait | Use one content column between the header and bottom actions. | Align clear corner actions and status on their authored row. |
| Wide portrait or square | Center a readable content column. Use the extra width for the scene, grid or margins. | Use a second status row under the corner controls by default. The confirmed Duo outer display uses its single-row override. |
| Landscape | Arrange compatible body regions side by side. | Fit the status and actions to the usable width. Reflow if the measured groups collide. |
| Ultrawide | Keep the primary interaction in a bounded central area. Extend scenery and optional secondary content into the wings. | Anchor persistent controls to their declared reachable region. Preserve readable text and touch sizes. |

Project the authored geometry through `Scale(pane)` once. Small and large windows with the same aspect share a composition. Choose a compact group or scrolling when the actual text, touch targets or secondary pane would be too small. A change to window size, system UI, fold pose or display can change the result while the same screen stays open.

## Apply hardware and system geometry

| Current window condition | Placement |
| --- | --- |
| Clear display | Paint the whole window and fit foreground inside its safe area. |
| Center notch, island or corner hole | Compare each group's artwork and hit envelope with the reported regions. Retain unobstructed anchors. Shift the affected corner action inward on its row. Reflow a centered group if its measured envelope cannot fit. |
| Confirmed iPhone Duo outer display | Request landscape for full-screen games. A counterclockwise turn of the upright phone puts the lens at the upper left. Start the avatar, resource counters and Settings or Return on one row to the right of that reservation, using the lens centerline. A clockwise turn puts the lens at the lower right. Keep the top row at its normal safe anchor and test the footer controls beside the aperture. Fill the screen with proportional scenery in both rotations. |
| Confirmed iPhone Duo inner display | With the under-display camera inactive, the top corner stays clear and the right action uses its normal safe-area anchor. Keep avatar, stamina, currency and Settings or Return in one row. Center the resource pair independently of the corner actions. Choose portrait or landscape from the active window. Honor active system regions and a separating fold from that window. |
| Permanent system bar, gesture edge or unknown camera footprint | Keep affected interactive groups inside the corresponding safe edge. With only an inset rectangle, use the entire reported safe rectangle for essential foreground until clearance is known. Re-evaluate when precise regions become available. |
| Separating hinge or tabletop pose | Use the reported division to split the active window. Keep each control, dialog, gesture surface and text group within one usable pane. A single flow uses the largest pane, with a configurable tie preference. A two-pane flow can use both. The background retains the full window. |
| Split view, floating window or external display | Use the application's current window and intersect its insets and regions with that window. Run the same selection again after resizing or moving. |

The supplied configuration uses `duoOuterHeader` set to `single-row-at-camera` and `duoOuterOrientation` set to `landscape`. The row contains the existing header roles. Gold Rush supplies the avatar, stamina, currency and action. Projects retain their authored player orientation when the orientation field is omitted. The native adapter identifies the actual panel and converts the physical aperture into current-window pixels. At startup and on display transfers, apply `TryDuoRotation` to the player. Enable both landscape directions on the outer display and include portrait on the inner display. Restore the project orientation when leaving these panels. On iOS the receiving game can check the exact hardware identifier and the native dimensions of the Unity window scene’s screen. A same-sized view on another phone or an external screen follows ordinary placement. A split inner viewport uses its actual pane and its confirmed display identity. Re-evaluate the adapter whenever the active display or window moves. The inner display selects wide portrait in a tall window and landscape in a wide window. Refresh geometry on every display, pose, rotation and Split View change. Apple specifies a 1398 × 2034 outer panel and an 1878 × 2670 inner panel. The inner capture raster is 2007 × 2853. A native adapter that identifies the internal display by its pixel dimensions must recognize both inner pairs in either order. Use the actual window width and height for layout. A native player built with the iOS 27.1 SDK or later can use the full display. [Apple design guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo) · [Physical display sizes](https://www.apple.com/iphone-duo/specs/) · [App Store screenshot sizes](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/) · [Fullscreen SDK behavior](https://developer.apple.com/videos/play/tech-talks/111461/)

The outer portrait raster is 1398 × 2034. The first Apple mechanical sheet gives a 6.07 mm aperture on a 77.17 × 112.28 mm active area, with its center 69.26 mm from the active left edge and 7.91 mm from the active top edge. The rounded upright pixel rectangle is `(1200, 88, 110, 110)`. [Apple mechanical drawing, sheet 1](https://developer.apple.com/download/files/accessories/dimensional-drawings/iphone-duo.pdf#page=2)

Use this one upright aperture for both landscape turns. In top-left coordinates, a counterclockwise turn maps a point `(x, y)` to `(y, W - x)`. A clockwise turn maps it to `(H - y, x)`. Transform all four edges of the aperture. On the 2034 × 1398 raster, the rounded aperture rectangles are `(88, 88, 110, 110)` at the upper left and `(1836, 1200, 110, 110)` at the lower right. In UIKit, convert from the active screen’s `fixedCoordinateSpace` into the actual Unity view. Use the current UIKit occlusion for expanded system UI, keeping the physical lens center as the alignment reference. [UIKit reserved regions](https://developer.apple.com/documentation/uikit/uiview/reservedregion)

Enable `duoInnerHeader` with `single-row-in-safe-area` and resolve it through `UsesDuoInnerSingleRow`. Start both inner orientations from the current pane's safe top. Fit the full row uniformly, center the resource pair and keep the two corner actions inside the same safe bounds. Clear the previous display's camera marker, camera lane and camera-derived offsets as soon as the inner display becomes active, even if the window size stays the same. Use the inner camera's reserved region only when the platform reports it active. In production, the outer hardware supplies its own visible lens. Keep the black circle in the confirmed outer-display preview fixture. [Apple reserved regions](https://developer.apple.com/documentation/uikit/uiview/reservedregion)

Get Unity pixel rectangles from the active rendering surface. Convert `Screen.safeArea` and `Screen.cutouts` from bottom-left to top-left exactly once. Keep platform regions already in top-left coordinates in that convention. Read window-local native reservations for dynamic overlays or folds when Unity supplies only a rectangle. Android exposes cutout bounds and separating fold state independently. [Unity safe area](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html) · [Android cutouts](https://developer.android.com/develop/ui/views/layout/display-cutout) · [Android folding features](https://developer.android.com/develop/ui/compose/layouts/adaptive/foldables/make-your-app-fold-aware)

## Bind the result

1. Stretch the scene background and modal scrim to the app window. Keep decorative strips attached to their own foreground controls within the selected pane.
2. For each explicitly separating band, call `TrySplit` on the pane it spans. Clip subsequent regions to that pane. Choose the active pane by role, or use `PrimaryPane` for a single flow. A nonseparating crease or camera enters the ordinary obstruction path only if it occludes pixels.
3. Use `Select(pane).body` and `.header` to choose the corresponding authored group styles. Use the active PanelSettings or CanvasScaler to convert the returned pixel geometry into local units. Bind controls and retain navigation state independently of the style.
4. Use `BodyAroundCorner` when a precise localized obstruction explains a side inset. It preserves a persistent side exclusion and returns the available body width. For a top corner action, include both its painting and hit bounds. Start with the physical edge from `TopRowLane` when precise local exclusions are available. Intersect the available row with the space left by stationary peers and any persistent system bars. Use `TryPlaceInRow` to find the nearest clear X on the authored row. For a confirmed Duo outer display, obtain the header lane from `TryDuoOuterHeader` first. Apply its width only to top foreground groups. The header starts at the top and its controls use the returned physical lens center Y. Place all four controls on this centerline. Reserve the complete painted and interactive corner bounds first, then center the resource pair in the gap. Reduce surplus gaps and counter widths as the reservation grows. Use the authored compact row on particularly narrow spans. Keep both totals and their hit targets visible on this row. An unexplained side inset remains the edge for interactive content.
5. Recalculate on actual geometry changes, including an inset-only change. Keep focus and the active page or game state. Discard the previous display's geometry when the window transfers.

A Toolkit geometry callback can bind an authored physical-right top action as follows. The same pixel rectangle can drive a Canvas RectTransform through its owning CanvasScaler.

```csharp
var profile = policy.Select(pane);
Rect lane = policy.TopRowLane(pane, safeArea, exclusions, true);
float groupTop = authoredTop;
bool hasDuoHeader = policy.TryDuoOuterHeader(pane, safeArea, exclusions, confirmedDuoOuterAperture, out Rect header, out float centerY);
bool inlineResources = hasDuoHeader && policy.DuoOuterSingleRow;
root.EnableInClassList("hud-second-row", !inlineResources && profile.header == "second-row");
if (hasDuoHeader)
{
    lane = header;
    groupTop = centerY - groupHeight * 0.5f;
    AlignAvatarCenterY(centerY);
}
lane.xMin = Mathf.Max(lane.xMin, stationaryPeersRightEdge);
Rect candidate = new(lane.xMax - margin - groupWidth, groupTop, groupWidth, groupHeight);
if (policy.TryPlaceInRow(lane, candidate, exclusions, policy.Scale(pane), true, out Rect placed))
{
    PositionActionGroupInPanel(placed);
    if (inlineResources) FitResourcePairBetween(avatarBounds.xMax, placed.xMin, centerY);
}
else
    ShowCompactActionGroup(lane);
```

Supply an empty aperture on all other hardware and windows. Compute bounds in one coordinate space and reserve the complete artwork and input envelope. Keep the background and modal scrim on the stretched root.

## Accept a new screen

Replay the five compositions and relevant overlays through the real screen. Capture a full-resolution reference and a smaller same-aspect case. Measure background coverage and each group's visible and hit bounds. Exercise enabled actions, their destinations and returns. Compare adjacent controls, footer actions and world-space content within the active pane. Check the [language matrix](layout-and-input.md#verify-each-locale). Label the evidence with its active window, display identity, orientation and geometry source.

### Capture each iPhone Duo display separately

Capture four full frames of the real UI, labeled by display, orientation and scene. Use the upright drawing to verify the physical origin of the aperture. The landscape game captures use the two rotated poses.

| Capture | Verify |
| --- | --- |
| Outer landscape, lens at upper left | Turn the upright aperture counterclockwise. The top row starts to the right of the current reservation and aligns with the lens center. Verify avatar, resources, Settings or Return and the full scene. |
| Outer landscape, lens at lower right | Turn the upright aperture clockwise. The normal top row stays clear. Check the footer artwork and hit targets beside the lens, then tap the last available navigation control. |
| Inner portrait | The inactive camera leaves a clear corner. The right action uses its normal safe-area anchor and the resource pair is centered. Inspect the portrait body and footer. |
| Inner landscape | The inactive camera leaves a clear corner and the right action uses its normal safe-area anchor. Verify the centered resource pair and the landscape body and footer. |

Draw the black circle in the confirmed outer hardware preview only. The native display supplies its own visible lens. Compare both landscape poses in the same game, then switch from outer to inner and rotate portrait → landscape → portrait. Compare a same-size transfer carrying stale outer geometry with a clean inner window. Check the final pixels and normal inner action anchor. Compare an ordinary window of the same size and exercise any active inner occlusion and separating fold separately. Verify Settings and Return artwork and taps in their owning scenes. [Apple display guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo)
