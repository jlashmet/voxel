# Experiment 012 — authoritative breakable-camera occupancy

## Symptom

Exact-SHA run `33524114702` for feature `3363db9dbfef7aa14fe70fd4f1d6b858e0a2163e` passed focused, module, and standalone gates, but `02-authored-breakable-boundary.png` still showed world underside/sky void instead of the clue-bearing false wall. The logged camera was `(-137.61, 18.64, 20.70)` and target `(-137.60, 18.50, 19.25)`.

This is the same visual symptom after three materially different framing corrections, so experiment 011's camera-distance root cause is no longer sufficient and no further camera tweak is allowed without a behavioral discriminator.

## Competing hypotheses

1. **Placement semantics:** the exact production-computed acceptance eye lies in authoritative solid terrain because terminal/exit-facing assumptions do not identify guaranteed carved camera space.
2. **Presentation invalidation:** authoritative storage at the eye is empty, but Gallery rendering still presents stale pre-authoring terrain after runtime cave mutation.
3. **Composition failure:** the selected cave pocket/barrier is not actually authored in the exact runtime surface-cave topology despite the successful projection/logging.

## Discriminator

`WorldbuildingGallerySecretDiscoveryPhysicalDiscriminatorTests.ExactSurfaceCaveAcceptanceEyeMustOccupyAuthoredEmptyVoxel` now reproduces the exact Gallery surface cave, terrain occupancy, pocket selection, terminal helper, barrier target, and 35% acceptance interpolation. It asserts the rounded eye voxel is empty in authoritative storage.

- If the test fails solid, hypothesis 1 is supported and camera/terminal semantics own the defect.
- If it passes empty while the built screenshot remains invalid, hypothesis 1 is falsified and the next investigation is production renderer/change invalidation; do not alter framing.
- Pocket authoring failure would support hypothesis 3 directly.
