# Experiment 041 — module validation assembly boundary

## Result
Exact request `2ed64189b55b1cba3bdb501e5637448f54516f81` / run `33874684110` validated feature source `50e7f50c64bf68ae4aab26a9360ed35bfd751eb6` and failed before any requested regression or player behavior executed.

Repository discovery was correct after the ownership move: it selected the Kentridge macro scene, Showcase feature-residency scene, WorldBuilder macro scene, GPU relocation scene, and integration player. Unity compilation then failed because `KentridgeMacroWorldValidationBootstrap` had moved into `Game.Composition.Kentridge.Playable.Validation` while `KentridgeMacroWorldEvidenceDriver` remains internal to `Game.Kentridge.PlayableSlice`.

## Classification
Validation assembly-boundary compile red only. There is no renderer, relocation, module-player, or SceneIssue replay product signal from this run.

## Selected correction
Keep the evidence driver internal and keep the validation scene/scenario at the convention-discovered module-root `Validation/` path, but move only the path-gated bootstrap helper back into the existing `Game.Kentridge.PlayableSlice` SceneRuntime assembly. This restores the prior internal access boundary without creating a public gameplay API, granting friend access to the validation assembly, or changing production renderer/world behavior.

## Next gate
Run a new exact-SHA request for the corrected feature head through `ci-test/fixes/agent-6`. If compilation succeeds, the GPU relocation discriminator and convention-discovered player scenes become the next authoritative evidence.
