# Experiment 005 — view-dependent convergence

## Question
Why did the 12-second real-time settle in exact run `33129981847` still emit fallback-gray clean evidence although the behavioral regression and replay passed?

## Discriminator
The test waited before teleporting the camera for the one-shot render. The real-player replay instead pins the saved scene-issue camera continuously; at that fixed pose it evolves from fallback gray to materialized gate. Existing `CastleScreenshotTests` also frees the Showcase camera before moving acceptance viewpoints.

## Result
A global timing-only hypothesis is falsified. Presentation convergence is view-dependent, so the target view must be held during the settle window.

## Action / falsifier
Put `VoxelShowcase` in fly mode, disable mouse-look, place the exact `Showcase Camera` at the captured pose, hold it for 12 real seconds, then capture 1928x836. Reject if the resulting artifact remains fallback gray or does not make the opened leaves/clear centre judgeable.
