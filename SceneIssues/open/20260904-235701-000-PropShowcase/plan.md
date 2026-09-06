# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, dispose prior state, and prove useful framing/materials/contact plus bounded switching with exact standalone-player evidence. Only production-quality visual evidence passes; no required gate or checkbox is waived.

Current canonical set: 529 entries (440 decorations, 25 reusable presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds). Parameters are variants, not identities. Exclude buildings, terrain, characters, VFX-only records, raw materials and duplicate aliases.

Structures owns enumeration and production presenters. `Assets/Game/Composition/Showcase/SceneRuntime` (`VoxelEngine.Showcase`) owns the browser and `Validation/PropShowcaseMaterialValidation.*`; parent Showcase and top-level scenes are not substitutes. Structures owns `Validation/PropShowcaseProductionValidation.*`. Material wiring also affects `Game.Composition.Materials`. The required CI repair is headless Python orchestration; subprocess behavioral tests apply, not a player scene.

## Current source and exact request
Current implementation: `79b6a2f4261185680ecbeceff7797f71992d35ab` (PlayMode teardown isolation); material fix/lifecycle regression: `de0aa1fb4221b06f8f63e6f22fc26ffba77defc8`.

Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28`, run `34003328146`, remains queued for the older exact source `de0aa1fb`; it has not been replaced. The isolation repair is not included in that immutable request. Master observed at `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; prior merge base `cd77b927dbe463171f6cef86bb268a31ae8df4e4`. Shell fetch fails DNS; remote GitHub refs/files remain accessible. Final master integration is outstanding.

## Visual result
Run `34000107687` showed Merchant Sign from the correct front and Game Table from an elevated three-quarter view, but voxel props had diagnostic rainbow colours. The renderer selects normal coverage for a non-white `SurfaceDebugTint`; the showcase supplied one. Composition now supplies `Color.white`, with a real lifecycle regression. Fresh production-material evidence is still required; do not change geometry based on normal-coverage captures.

## Required-CI discriminator and fix
Hypotheses were product tests leaving invalid scene state versus persistent orchestration overlapping Test Runner teardown. The failed artifact starts the next phase before `IPostBuildCleanup`/scene restoration and then references a deleted temporary scene. `OnRunFinished` queues the next `Execute` without a teardown-completion contract.

The repair isolates each PlayMode phase and requested PlayMode filter in a fresh existing `unity-run.sh` process; persistent EditMode batching and every selected test/player gate are retained. Local verification: 20 focused Python tests pass, including 8 new isolation tests; the baseline fails 14 assertions/subtests. Exact logs, hashes and limits are in `ci-teardown-repro.md`. This is not Unity acceptance.

## Cost and remaining gates
Material configuration adds no per-frame work; CI isolation adds editor startup only. Prior player replay recorded 44 switches and at most one owned presenter, but no usable memory-growth measurements; counts alone do not pass resource acceptance. After the queued request completes, validate the repaired source exactly, inspect representative captures, measure switching/resources against `device-matrix.md`, finish all acceptance/metadata, close open-to-closed, merge master, then PR + auto-merge and verify closure on master.
