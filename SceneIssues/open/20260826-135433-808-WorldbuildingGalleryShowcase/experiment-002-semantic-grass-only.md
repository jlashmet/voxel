# Experiment 002 — semantic-Grass-only pixel sprite

**Hypothesis:** The marked region is semantic `VegetationKind.Grass`, so a dedicated shape-5 camera-facing three-blade pixel sprite will remove the dark radial-card bars without changing other tuft species.

**Action / source:** Added the grass-only discriminator and batchmode-safe framebuffer regression on exact source `1c02387f952e5eaef5845bafde12b73fcb9759f7`; request `8523bc98a3ddc857d1d937d59db50fb158e83993`, run `33044213942`.

**Result:** Regression passed and saved-pose replay completed, but direct inspection of `verification-final.png` still showed tall dark vertical stalks in the original marked circle.

**Verdict:** Falsified. The synthetic semantic-Grass invariant was correct but did not cover the runtime owners visible in the capture.

**Next:** Inspect the catalogue and target the full grass-like presentation bucket demonstrated by the replay.
