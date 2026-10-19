# Simulation follows display refresh, with a Console 30 preset

Status: accepted

The original game targets 30 fps, but its top-level update consumes a variable
delta (`DeltaSecondsClamp = 0.1`), so the native build runs simulation at the
selected display rate (60/120/144/240) and keeps a Console 30 preset for parity
testing. Fixed 30 Hz simulation with interpolated presentation was rejected as
the default because it adds substantial interpolation work and gives no
input-latency benefit; it remains a fallback if validation exposes
frame-rate-dependent logic. Handling may therefore differ subtly at higher
rates, which is accepted as normal for a PC racer.

Consequence: rexglue paces guest vblank at a configurable refresh (24–240 Hz,
default 60). Reaching true 60+ fps may require guest tick/refresh patches or
mid-asm hooks, as comparable projects have needed; M2.5 establishes the exact
mechanism for Alive.

Update (measured): Alive performs **two guest vblank waits per frame**, so guest
FPS = guest refresh / 2. With guest refresh 120 Hz the game measured ~60 guest
fps (`VdSwap` telemetry). Host presentation is decoupled — it ran at ~298 fps in
the same session, so host/overlay FPS is not the game rate. The port therefore
maps a target FPS to guest refresh = target × 2; the SDK refresh clamp was
raised from 240 to 1000 Hz for the 144 and 240 presets.
