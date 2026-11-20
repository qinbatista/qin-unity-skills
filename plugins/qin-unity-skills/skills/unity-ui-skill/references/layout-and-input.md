# Responsive layout and input

## Fit and anchor

- Bind the reference design dimensions and actual viewport before applying scale. When the project uses uniform viewport fit, use `min(width / referenceWidth, height / referenceHeight)` once for both axes. Keep the project's existing calibrated PanelSettings or CanvasScaler as the scale owner and account for it during geometry conversion.
- Use parent-relative layout, flex and explicit edge constraints for Toolkit. Scale the visible control, hit region, offsets and gaps together. Preserve each group's declared viewport or safe-area anchors. Keep full-bleed backing independent of foreground and hit geometry.
- Read safe-area and obstruction data in the active display coordinate system and convert it once into panel or parent units. Distinguish viewport dimensions, logical units, platform density and insets. Move only affected groups under the actual screen policy, and re-evaluate after resize, orientation or host changes.
- Preserve clear regions, aligned controls, useful touch sizes and readable content. Allow scrolling where content can overflow and keep primary actions reachable. Check real glyphs, icon interiors and actual clipped regions using long translations, font fallbacks and the project's supported text direction.

## Check buttons and destination pages

1. Identify the source page and the expected action, destination, exits and effect on data and selection. Enter through the actual scene or formal route and let layout settle. Re-read the currently mounted hit bounds after remount, resize, inset or language changes. Convert through the current panel or device and use real press/release, touch, gamepad or keyboard input as appropriate. Read the actual focused target when testing navigation submission.
2. Observe a held press frame when feedback is part of the contract, then release, cancel, drag or time a long press as specified. Use a stable hit container and restore the actual original visual transform on exit. Test focus loss, disable, detach and repeated press where relevant. Check the owning action count, modal barrier and current target. Keep pressed, selected, focused and disabled states independent and retain clear confirmation when reduced motion is enabled.
3. Exercise the normal destination route and each close, back or reopen path. Check the destination's title, text, fixed actions, errors or empty state, final scroll item, and touch and focus barriers. Check restored selection, scroll and focus and watch for duplicate actions or subscriptions. Verify destination adaptation through the same real flow using the relevant [screen cases](screen-adaptation.md).
4. Record which runtime and geometry produced each screenshot and input result. Label simulated insets or a test host explicitly when they supply the evidence.

## Localized content

Use the existing localization owner, stable keys, plural and number formats and game-owned fonts. Inventory changes across static, dynamic, disabled, loading, empty and failure states. Verify the actual glyphs in their painted slot, including fallback, outlines, accents, units and mixed direction where supported. Honor the screen's font range, fitting and wrap rules and retain important text at the minimum supported scale. Keep unaffected control geometry fixed during text-only fitting under that contract. Switch language while the interface is open and verify the current page, state and inputs again after layout settles.
