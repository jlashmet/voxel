# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, dispose prior state, and prove useful framing/materials/contact plus bounded switching with exact standalone-player evidence. Only production-quality visual evidence passes; no required gate or checkbox is waived.

Current set: 529 entries (440 decorations, 25 reusable presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds). Parameters are variants. Exclude buildings, terrain, characters, VFX-only records, raw materials and duplicate aliases.

Structures owns enumeration/presenters and `Validation/PropShowcaseProductionValidation.*`. `Assets/Game/Composition/Showcase/SceneRuntime` (`VoxelEngine.Showcase`) owns the browser, capture/resource instrumentation and `Validation/PropShowcaseMaterialValidation.*`. Parent Showcase/top-level scenes are not ownership substitutes. Material wiring affects `Game.Composition.Materials`; audit its validation before closure. CI orchestration is headless Python, covered by subprocess tests rather than a scene.

## Current source and immutable request
Resumed `b71d88bf1ec8be72e23bbc54bfaa30e64a75aa77`. Prior implementation: CI isolation `79b6a2f4261185680ecbeceff7797f71992d35ab`; material mode/lifecycle regression `de0aa1fb4221b06f8f63e6f22fc26ffba77defc8`.

Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28`, run `34003328146`, remains queued for `de0aa1fb`. It is untouched and excludes both later repairs. Master remains `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; prior merge base `cd77b927dbe463171f6cef86bb268a31ae8df4e4`. Shell fetch again failed DNS; GitHub refs/files are accessible. Final master integration is outstanding.

## Proven results and discriminators
Run `34000107687` showed corrected sign/table framing but diagnostic rainbow colours. Non-white SurfaceDebugTint selects normal coverage; composition now uses white. Fresh production-quality material evidence remains required; no further art change is justified by those old captures.

The same failed run starts another test phase before PlayMode cleanup restores its temporary scene. Isolating PlayMode phases retains every selected test/player gate. Prior local result: 20 Python orchestration tests passed; baseline failed 14 assertions/subtests. See `ci-teardown-repro.md`; this is not Unity acceptance.

Its 44-switch/peakOwned=1 log cannot distinguish real retirement from cleared dictionaries. The stress loop replaced everything in one frame. New capture-only orchestration repeats the same sampled set three times, yielding between selections and sampling the same settled endpoint. It records startup/synchronous selection cost, allocator totals, resident geometry, actual owned components and global mesh/material counts. Completion requires all cycles; disabling the browser stops the coroutine. Three resource-accounting regressions are added but not executed in Unity. Details/measurement limits: `resource-validation.md`.

## Cost and remaining gates
Ordinary rendering/content and all budgets are unchanged; diagnostic snapshots allocate only at three capture-cycle boundaries. Short process-wide samples do not satisfy the two-hour world-memory criterion or measure GPU frame time. After the queued request completes, validate the new exact source, inspect all representative captures, assess usable resource/cost data against device-matrix budgets, finish every checklist/metadata item, close open-to-closed, merge master, then PR + auto-merge and verify closure on master.
