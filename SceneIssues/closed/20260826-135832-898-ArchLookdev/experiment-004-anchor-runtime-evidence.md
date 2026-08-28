# Experiment 004 — standalone anchor runtime evidence

## Hypothesis
The SRP hook may be registered yet still not execute the world-anchor operation for the saved Hero Arch replay. If the standalone player never runs `AnchorCamera`, the custom hero mesh remains camera-local; its authored negative-Z surface coordinates are behind the camera and the entire 128-leaf/30-flower mass disappears.

## One change
Add one development/editor-only, one-shot diagnostic log immediately after `AnchorCamera` applies world identity. Do not change mesh geometry, material/shader settings, art density, depth, or camera state.

## Discriminator
- `ARCH_REFERENCE_ANCHOR` present in the real-player replay log: lifecycle delivery is proven; investigate surface depth/occlusion next.
- Marker absent while replay pose is verified: SRP delivery is still the defect; do not tune art/depth.

## Validation
Run the existing exact-SHA PlayMode regression plus the original saved scene-issue replay and inspect both the marker and presented pixels. A green unit result alone is not acceptance.
