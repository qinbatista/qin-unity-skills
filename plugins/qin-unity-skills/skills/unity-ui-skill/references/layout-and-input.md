# Responsive layout and input

## Fit and anchor

- Bind the reference design dimensions and actual viewport before applying scale. When the project uses uniform viewport fit, use `min(width / referenceWidth, height / referenceHeight)` once for both axes. Keep the project's existing calibrated PanelSettings or CanvasScaler as the scale owner and account for it during geometry conversion.
- Use parent-relative layout, flex and explicit edge constraints for Toolkit. Scale the visible control, hit region, offsets and gaps together. Preserve each group's declared viewport or safe-area anchors. Keep full-bleed backing independent of foreground and hit geometry.
- Read safe-area and obstruction data in the active display coordinate system and convert it once into panel or parent units. Distinguish viewport dimensions, logical units, platform density and insets. Move only affected groups under the actual screen policy, and re-evaluate after resize, orientation or host changes.
- Preserve clear regions, aligned controls, useful touch sizes and readable content. Allow scrolling where content can overflow and keep primary actions reachable. Check real glyphs, icon interiors and actual clipped regions using long translations, font fallbacks and the project's supported text direction.

## Check buttons and destination pages

1. Enter through the actual scene or formal route and let the native layout settle before reading the current hit bounds. Deliver real press and release input, or the intended keyboard action, through the active input path.
2. Observe immediate feedback, release or cancellation, the resulting action and any destination page or popup. Pressed, selected, focused and disabled states each reflect their own contract. Keep visual feedback inside the stable hit region.
3. Exercise back or close, modal input blocking, final scroll items and repeated reopen. Use fresh output after any language, inset or size change and verify the destination remains usable in the same flow.
4. Record which runtime and geometry produced each screenshot and input result. Label simulated insets or a test host explicitly when they supply the evidence.
