# Experiment 002 — camera-space falsifier

**Hypothesis:** the bespoke meshes are visually absent because their world-authored vertices are parented to the movable Hero Arch Camera, not because the new leaf/flower representation failed to build.

**Action / evidence:** inspected successful pre-merge exact-SHA run `33047792106` from source `a9cf4118029ceeec8acaa465fc293edca70244b7`. Its saved Hero Arch replay is essentially bare even though the structural regression passed. `ArchReferenceGrowth` creates `Arch Reference Hero Growth` as a camera child with local identity, while all ivy/flower vertices are authored around world `(x≈-2..2, y≈0..8, z≈-0.1)`.

**Result:** confirmed. The saved camera transform is therefore applied a second time to the hero mesh. The runtime representation exists but is displaced off the masonry.

**Fix:** arm a scene-specific one-shot pre-cull anchor that detaches only the hero root to world identity before ArchLookdev culling, then unsubscribes. Regression now reproduces the captured camera transform and requires the world-identity root plus the existing representation/asymmetry/budget contracts.

**Cost / blast radius:** ArchLookdev only; one camera callback until the first successful anchor, then zero steady-state work. Shared vegetation and other scenes are unchanged.
