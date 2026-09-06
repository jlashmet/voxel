# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, dispose prior state, and prove useful framing/materials/contact plus bounded switching with exact standalone-player evidence. Only production-quality visual evidence passes; no required gate or checkbox is waived.

Current canonical set: 529 entries (440 decorations, 25 reusable presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds). Parameters are variants, not identities. Exclude buildings, terrain, characters, VFX-only records, raw materials and duplicate aliases.

Structures owns enumeration and production presenters. `Assets/Game/Composition/Showcase/SceneRuntime` (`VoxelEngine.Showcase`) owns the browser. Its focused `Validation/PropShowcaseMaterialValidation.*` consumer now exists; parent Showcase and top-level scenes are not substitutes. Structures owns `Validation/PropShowcaseProductionValidation.*`. Material wiring also affects `Game.Composition.Materials`. The bounded CI orchestration repair below is headless Python tooling; subprocess behavioral tests apply, not a player scene.

## Current source and exact request
Material fix and lifecycle regression source: `de0aa1fb4221b06f8f63e6f22fc26ffba77defc8`. Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28`, run `34003328146`, is queued and must not be replaced. Remote master observed at `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; prior merge base is `cd77b927dbe463171f6cef86bb268a31ae8df4e4`. Shell fetch fails DNS; GitHub refs/files are accessible. Final master integration remains required.

## Visual result and selected fix
Run `34000107687` showed Merchant Sign from the correct front and Game Table from an elevated three-quarter view, but voxel props had diagnostic rainbow colours. The renderer selects normal coverage for a non-white `SurfaceDebugTint`; the showcase supplied one. Composition now supplies `Color.white`, with a real lifecycle regression. Await fresh production-material evidence; do not change geometry on the strength of normal-coverage captures.

## Required-CI discriminator
The prior run failed despite a successful standalone replay. Artifact `9979710315` SHA256 `ea8b83b779855c344269b26859f3b4ad8488b6040b702fafe4179724139f8d94` records successful PlayMode case results, then starts the next phase before `IPostBuildCleanup` and `RestoreSceneSetupTask`. It subsequently references the preceding, deleted `InitTestScene`.

Hypotheses: product test leaves invalid scene state versus persistent orchestration overlaps Test Runner teardown. `VoxelCiPersistentTestRunner.OnRunFinished` queues the next `Execute` on a delay call without a teardown-completion contract, matching the log ordering. Isolate PlayMode assemblies and requested PlayMode tests in fresh existing `unity-run.sh` processes; retain persistent EditMode batching, every selected test, zero-match/skip/failure checks and automatic player discovery. Add subprocess behavioral regressions before publishing. Do not invent private Test Runner APIs, custom workflows, test registration or retries while the exact request is queued.

## Remaining gates and cost
The material fix adds no per-frame work. Isolation costs editor startup, not product runtime. After the queued request completes, validate the repaired source exactly, inspect every representative capture, measure switching/resources against `device-matrix.md`, finish acceptance/metadata, close open-to-closed, merge current master, then PR + auto-merge and verify closure on master.
