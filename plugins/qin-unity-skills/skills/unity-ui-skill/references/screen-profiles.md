# Choosing a screen layout

Start with the [shared configuration](../assets/screen-layouts.json) and its [Unity reader](../assets/ScreenLayoutPolicy.cs). Give each screen one geometry owner and bind its existing groups to the selected composition. Keep these inputs as a pair under the receiving project's UI owner. Import the JSON as a TextAsset, bind it once and call `ScreenLayoutPolicy.Read`. Both UI Toolkit and Canvas can use the result.

## Choose a composition

Select from the current app window or usable pane. The supplied breakpoints are editable design defaults. Device density, brand, model, marketing size and the orientation sensor are unnecessary for selection. Read the first inclusive aspect limit in the configuration, with the final entry covering the rest. A square window uses the wide-portrait composition.

| Profile | Body | Header and actions |
| --- | --- | --- |
| Tall portrait | Extend the background and scrollable content vertically. Keep persistent actions within reach. | Use one row at the authored scale when the painted groups fit. |
| Portrait | Use one content column between the header and bottom actions. | Align clear corner actions and status on their authored row. |
| Wide portrait or square | Center a readable content column. Use the extra width for the scene, grid or margins. | Use the authored top row when it fits. A second status row clears crowding or a centered obstruction. The confirmed Duo outer display uses its single-row override. |
| Landscape | Arrange compatible body regions side by side. | Fit the status and actions to the usable width. Reflow if the measured groups collide. |
| Ultrawide | Keep the primary interaction in a bounded central area. Extend scenery and optional secondary content into the wings. | Anchor persistent controls to their declared reachable region. Preserve readable text and touch sizes. |

Project the authored geometry through `Scale(pane)` once. Small and large windows with the same aspect share a composition. Choose a compact group or scrolling when the actual text, touch targets or secondary pane would be too small. A change to window size, system UI, fold pose or display can change the result while the same screen stays open.

## Apply hardware and system geometry

| Current window condition | Placement |
| --- | --- |
| Clear display | Paint the whole window and fit foreground inside its safe area. |
| Center notch, island or corner hole | Compare each group's artwork and hit envelope with the reported regions. Retain unobstructed anchors. Shift the affected corner action inward on its row. Reflow a centered group if its measured envelope cannot fit. |
| Confirmed iPhone Duo outer display | Upright portrait places the camera at the upper right. Keep avatar, resources and Settings or Return in one top row ending to the left of the reservation, centered vertically on the lens. In counterclockwise landscape, start the row to the right of the upper-left reservation. In clockwise landscape, keep the normal top row and clear the lower-right reservation at the footer. Use proportional full-screen scenery in every pose. |
| Confirmed iPhone Duo inner display | With the under-display camera inactive, the top corner stays clear and the right action uses its normal safe-area anchor. Keep avatar, stamina, currency and Settings or Return in one row. Center the resource pair independently of the corner actions. Choose portrait or landscape from the active window. Honor active system regions and a separating fold from that window. |
| Permanent system bar, gesture edge or unknown camera footprint | Keep affected interactive groups inside the corresponding safe edge. With only an inset rectangle, use the entire reported safe rectangle for essential foreground until clearance is known. Re-evaluate when precise regions become available. |
| Separating hinge or tabletop pose | Use the reported division to split the active window. Keep each control, dialog, gesture surface and text group within one usable pane. A single flow uses the largest pane, with a configurable tie preference. A two-pane flow can use both. The background retains the full window. |
| Split view, floating window or external display | Use the application's current window and intersect its insets and regions with that window. Run the same selection again after resizing or moving. |

Use `duoOuterHeader: single-row-at-camera` for the existing header roles. The supplied `duoOuterOrientation: portrait-and-landscape` lets both built-in panels use upright portrait and both landscape directions. Projects with an outer landscape-only contract can keep `landscape`. Omit the orientation field to keep the project's authored player settings. The receiving game applies `TryDuoRotation` on startup and display transfer and restores its prior player settings when leaving the built-in panels. Verify the allowed orientations in the exported player when changing their support.

On iOS, identify the active panel using the exact hardware identifier and the native dimensions of the Unity window scene's screen. A same-sized view on another phone or an external screen follows ordinary placement. Use the confirmed display and current window for Split View. Refresh geometry on display, pose, rotation and window changes. The physical panels are 1398 × 2034 outer and 1878 × 2670 inner. The 2007 × 2853 inner capture raster can also identify the panel. Recognize both inner pairs in either order and derive the layout from the current Unity window. A player built with the iOS 27.1 SDK or later can use the full display. [Apple design guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo) · [Physical display sizes](https://www.apple.com/iphone-duo/specs/) · [App Store screenshot sizes](https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/) · [Fullscreen SDK behavior](https://developer.apple.com/videos/play/tech-talks/111461/)

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

For a confirmed Duo lower-right aperture, use `DuoOuterTopSafe` before `TopRowLane`. It restores the normal right header anchor when a localized lower reservation accounts for the reported side inset and the upper part of that side is clear. Continue to use the current safe bounds and reservations for the body and footer.

A Toolkit geometry callback can bind an authored physical-right top action as follows. The same pixel rectangle can drive a Canvas RectTransform through its owning CanvasScaler.

```csharp
var profile = policy.Select(pane);
Rect topSafe = policy.DuoOuterTopSafe(pane, safeArea, exclusions, confirmedDuoOuterAperture);
Rect lane = policy.TopRowLane(pane, topSafe, exclusions, true);
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

### Screen adaptation regression

Run the receiving game's existing layout replay from its actual entry. Include a clear window, both sides of the affected aspect breakpoint and the relevant centered or corner regions. Compare a before → changed → restored sequence in the same live state. Check the original artwork proportions, readable text, painted and interactive bounds, and button destinations after each transition.

For Duo, include outer portrait → both landscape turns → outer portrait, and an outer → inner transfer at the same viewport size. Compare the restored anchors with the first visit, and check the same-size inner destination against its clean safe-area baseline. A change in the native reservation alone must also refresh placement. Replay supported locales and panes. Record Editor geometry replay, named simulator observations and physical device results with their actual runtime scope.

## Portrait game UI matrix

Record the gameplay composition, the exported player's supported orientations and the actual range of app windows. For a game targeting iPad or a foldable, include the [wide-window pass](#verify-the-wide-window-of-a-portrait-game) even when the game opens upright. Cover the available wide portrait, near-square or landscape windows created by resizing, unfolding and rotation. Use a layout replay for profiles still awaiting package support. Verify the exported player's rotation and resizing behavior in that runtime. [Apple scene orientation and resizing](https://developer.apple.com/documentation/technotes/tn3192-migrating-your-app-from-the-deprecated-uirequiresfullscreen-key)

Record the active UI hosts, authored reference, languages and page states. For a YoFaAssets-linked game use the 2160 × 3840 portrait acceptance reference and record its logical scale. Replay a smaller 9:16 window, a compact window at the supported minimum, a matched long window clear and under a central obstruction, a corner cutout, and a clear wide portrait such as 1200 × 1600. Vary the bottom gesture inset independently. Add each supported foldable panel and any window or inset change that can occur during play. On iPhone Duo, test the outer right-lens top row and the inner clear corner separately. Include a same-size outer-to-inner transfer if supported.

| UI region | Layout rule | Native acceptance |
| --- | --- | --- |
| Full-screen scenery and world UI | Use proportional cover for the backing. Keep characters and gameplay shapes proportional and give the play surface its own anchor and input region. | Inspect a full frame, the playable bounds and the complete gesture travel at the supported extremes. |
| Header, resources and status | Align the clear corners on their authored line. Fit readable resources in the remaining space, then place the pair below a central obstruction when needed. | Measure painted and hit bounds. Verify corner alignment, full values, reserved-region clearance and the real destinations. |
| Primary action, guidance and progress | Keep the primary action within its content region. Give progress text and its track a common alignment. Give temporary guidance a bounded region. | Verify the action, all relevant progress or completion states, and clearance around the playfield and surrounding controls. |
| Bottom navigation and tools | Anchor to the declared bottom edge, account for the gesture inset once, and move the decorative plate with its controls. | Check both end targets, equal alignment, and availability after an inset-only or display change. Check the system gesture on the device when it overlaps the layout. |
| Content pages, lists and grids | Bound the readable content column. Let variable content scroll inside its region. Keep persistent actions and labels with their owners. | Exercise tab changes, the last reachable item, and return while retaining state. Include empty and long-content cases where reachable. |
| Dialogs, sheets and transient states | Cover the window with the scrim. Fit the interactive panel to the usable region and reserve reachable close and confirm targets. | Open, close and reopen the real panel. Check that it shields underlying actions and preserves loading, error and success semantics. |
| Text and input | Scale artwork and hit targets uniformly. Allocate space for actual localized text. Reflow the owning group at a measured breakpoint. | Inspect original-size glyphs, full numbers and native picks across supported locales. Exercise destination, cancel and return. |

Freeze the receiving game's source, profile fixture and observer. Run each required matrix twice from the actual entry in separate runtime launches. Match scene, page state, locale, viewport, display and obstruction source. Resolve actions through the mounted panel and its actual pick result when an embedded page reuses navigation names. Compare stable painted and interactive geometry within the declared raster tolerance. Record representative child pages and modals with the exact configuration used for each. Keep transient game data and timestamps out of the geometry comparison. Build a review sheet from the current native captures and caption each view with its observed runtime and geometry source.

### Verify the wide window of a portrait game

For edge-docked controls, record the foreground clearance and the backing clearance separately. For a playable subject, compare its visible span with the available space between the same live controls on both displays. Verify the composition when entering gameplay and after the first interaction.

Complete the upright pass and the wide pass independently. For the wide pass, include a clear tablet window such as iPad 4:3, the active unfolded landscape pane, a 16:9 reference and a short ultrawide window. For a YoFaAssets-linked game capture the 3840 × 2160 baseline and a smaller 16:9 window. Compare their proportional scale and anchor distances. For wide windows with system bars or camera apertures, replay the actual side or bottom exclusions. Include both Duo outer landscape turns when supported, then check the inner panel with its own geometry. In the same flow, change the window or active reservation and return to the original layout without losing the current page or gameplay state.

| Region | Placement and acceptance in a wide window |
| --- | --- |
| Scenery and gameplay | Fill the viewport with proportional scenery. Bound the playable area using the available height. Check the camera, character and circular gameplay surface at native size and verify that the surrounding scenery extends into the wings. |
| Top row | Use one row at the authored top anchor when the painted and clickable areas fit. Keep resource values together and reserve each corner action's own space. Check the active display and actual camera edge before moving the affected group. |
| Main content and primary action | Keep primary content in a readable centered area. Move a compatible secondary group into a side column only when the width supports it. Align related label and action centers. Preserve the same reading order and action sequence after a resize. |
| Progress, toolbar and bottom navigation | Give stacked progress and tools a common footer parent. When progress and an action alternate, give them one reserved slot. Measure the track center and its clearance from the toolbar in each visible state. Measure the visible backing against the physical pane edge and the foreground against its usable region. For a centered tool group, measure each adjacent gap at its authored scale across viewport widths. Check every tool’s painted height, vertical centering, badge, touch target and destination. Extend an approved full-width backing with its slice or repeatable middle section. |
| First-use hints and transient banners | Give the hint a bounded slot within the space left by fixed header controls. Trigger it while the longest localized caption and corner action are both visible. Check their painted bounds and submit the corner action before the hint clears. |
| Full pages, lists and dialogs | Constrain reading width and cap height to the available pane. Give the middle section vertical scrolling when needed and retain reachable title, close, return and confirm controls. Enter and exit the representative pages and modal states on both the clear tablet and unfolded pane, in each supported locale. |

Capture full scenes in addition to header crops. Include the tablet and inner-foldable pages in the same runtime used for the main flow. Device labels describe verified geometry. An Editor replay establishes layout and input for those viewports. A named device or simulator Player run establishes the exported window and rotation contract.

### Present portrait and landscape together

For a whole-game adaptation review, use the results of both orientation passes to deliver one image. Put portrait and landscape side by side. Give clear phones, phones with obstructions, clear tablets, and each supported foldable display or pose their own comparable rows. Include both landscape obstruction edges when applicable. Add compact windows and separating folds when they affect the game.

In each case show the game at the tested window size and state what happens to the header, primary content and footer. Label any detail crop or geometry diagram. Show full frames for the main compositions and keep viewport dimensions, overlay markings and the runtime scope on the sheet.

### Capture each iPhone Duo display separately

Capture a full frame per supported pose and scene. Label each with its display, orientation and geometry source. The default policy has five cases.

| Capture | Verify |
| --- | --- |
| Outer upright portrait, lens at upper right | Use the aperture from the upright mechanical drawing. Align the avatar, both resources and Settings or Return with its center Y. Keep painted artwork and hit bounds to its left. Verify proportional full-screen scenery and exercise the right action. |
| Outer landscape, lens at upper left | Turn the upright aperture counterclockwise. The top row starts to the right of the current reservation and aligns with the lens center. Verify avatar, resources, Settings or Return and the full scene. |
| Outer landscape, lens at lower right | Turn the upright aperture clockwise. Compare the top row with the clear landscape baseline while the window reports the camera-side inset. Check the footer artwork and hit targets beside the lens, then tap the last available navigation control. |
| Inner portrait | The inactive camera leaves a clear corner. The right action uses its normal safe-area anchor and the resource pair is centered. Inspect the portrait body and footer. |
| Inner landscape | The inactive camera leaves a clear corner and the right action uses its normal safe-area anchor. Verify the centered resource pair and the landscape body and footer. |

Draw the black circle in the confirmed outer hardware preview only. The native display supplies its own visible lens. Check upright portrait, both landscape turns and the return to upright on the outer display. Then switch to the inner display and rotate portrait → landscape → portrait. Compare a same-size transfer carrying stale outer geometry with a clean inner window. Check the final pixels and normal inner action anchor. Compare an ordinary window of the same size and exercise any active inner occlusion and separating fold separately. Verify Settings and Return artwork and taps in their owning scenes. [Apple display guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo)
