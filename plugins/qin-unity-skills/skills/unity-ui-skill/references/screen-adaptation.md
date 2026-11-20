# Screen adaptation

## Scale and geometry ownership

Use the game's approved design baseline in each supported orientation, including its declared 4K projection when applicable. Record visible and hit bounds, order, group centers, anchors, artwork sizes, offsets, gaps and text metrics in that baseline. Keep design pixels, layout units, active viewport pixels and capture dimensions distinct.

For a uniform-fit screen, use a single multiplier `min(viewportWidth / referenceWidth, viewportHeight / referenceHeight)` on both axes. Scale dimensions, hit areas, anchor offsets and gaps once. Account for the effective PanelSettings or CanvasScaler scale when converting to local units. Use the active window geometry as the scale input. Use platform density for unit conversion and touch ergonomics. Preserve aspect and balance and place independent full-bleed backing in its own viewport layer. Declare the intended behavior when the minimum supported viewport cannot fit a functional group.

## Obstructions and orientation

Measure the real active display, window, safe area and available cutout or gesture bounds. Convert origin, axes and units through one existing layout owner. Treat safe-area insets and full viewport dimensions as different inputs. Compare each group's visible and hit bounds with relevant obstructions, keeping clear viewport-anchored corners or tool rows in their authored positions where the screen contract calls for them. Apply each inset once across nested parents. Recalculate from actual bounds after resize, rotation, display switch or inset change.

Keep separate evidence for requested physical devices, compatible simulator or emulator models, Editor replays of measured geometry and synthetic profiles. Record the source of measurements and the active display or fold pose. A profile name identifies a fixture and actual device geometry needs a runtime readback. On portrait-only projects verify the installed artifact remains upright when the device rotates. Include separate low and high pixel counts at the same aspect, valid different aspects, independently varied insets and the affected live transitions. Compare pixel rounding, anchor offsets, whole-world projection if relevant, hit geometry and frame composition in the same state.

## Painted bounds and readable content

Read the active font, fallback, outline and shadow and observe actual glyph ink within the rendered surface. Frame geometry, alpha bounds and the usable text region can differ. Check rounded edges, decorative icons, clipped content and label baselines. Project progress-track fill and overlay text into the painted track's own space, accounting for fit padding, trim and transparent borders. Compare empty, intermediate and full states. Verify equivalent groups across affected screens at matching inputs and give each intentional variant its declared slot or style.

Measure the usable size, complete joystick travel or other hit envelope, title and fixed actions, final scroll item and text readability. Check the longest supported translations and required content extremes in the actual host. Follow the same active flow through size, orientation or language changes where supported and verify state, focus, input and alignment settle after returning. Record screenshots and input outcomes with the actual runtime, revision and geometry.
