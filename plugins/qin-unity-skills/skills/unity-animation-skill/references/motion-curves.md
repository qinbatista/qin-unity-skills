# Motion curves for animation, VFX, and shaders

Author continuous visual changes with deliberate nonlinear timing. This applies to AnimationClip tangents, procedural interpolation, particle and VFX Graph properties, and time-driven shader or material values. Choose acceleration, deceleration, anticipation, a hold, overshoot, settling or another response appropriate to the action. Keep static properties static and keep discrete events, visibility and sprite switches stepped. Preserve measured gameplay times, endpoints, legal ranges and synchronized phases. An exact source-parity migration preserves the observed source and identifies any intentionally changed target response before judging parity.

## Design and inspect each channel

1. Start with the pose, force, contact and phase timing. Check the actual response in position, scale, opacity, emission, intensity, speed, distortion, UV movement and rotation. A linear time input can drive a nonlinear response. A straight spatial path can have nonlinear speed.
2. Trace the complete visual chain from the Controller and Clip tangents through particle or VFX age and any independent shader phases. Record each channel's writer, units, time span and chosen shape in the existing recipe. An eased parent fade leaves independent UV, noise, particle and child-motion channels to be inspected on their own.
3. Use a quick start and deceleration for feedback when appropriate, continuous tangents for breathing or swaying, and anticipation and slower recovery when the motion needs weight. Let the object's timing and behavior determine the curve shape. Keep elapsed time, cooldowns and simulation clocks accurate when mapping them to visual progress.
4. Check weighted tangents, interpolation extrema, overshoot, Euler or Quaternion bindings, angle wraps and unwanted reversals. Keep scale, alpha and intensity in their legal ranges. For looping UV or rotation, inspect the actual path through wrap. Measure duration in seconds, including real hold intervals.

## Verify the resulting motion

Sample interior times and neighboring phase boundaries so the resulting values can be distinguished from straight endpoint interpolation. Use the actual component and rendered playback to observe the response. Check the first and last values, velocities and directions at loop seams, including held values and particle or material phase. Observe at least two consecutive wraps where a loop is the result. Verify stopped and restarted state when an action is interruptible. Use the Effects Skill for formation of shape, arc, extent and assembly, together with the curve that times their progress.
