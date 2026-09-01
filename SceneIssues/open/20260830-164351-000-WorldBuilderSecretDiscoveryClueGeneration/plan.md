# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and representative built-player proof. This issue has no captures/marked regions. Per user direction, visual proof is owned entirely by the dedicated module-local Secret Discovery validation scene; there must be no `WorldbuildingGalleryShowcase` secret integration.

## Hypotheses and results

- **Hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` already resolves canonical hidden candidates deterministically.
- **Clue generation needs a second hidden-location solver.** Rejected: route/clue planning consumes canonical `ResolvedSecretPlan` identity.
- **Route/readability/clue planning was missing.** Supported; implemented with stable IDs, semantic anchors, readability/diversity policy, diagnostics, and explicit bypass semantics.
- **Reusable interaction/discovery APIs are unavailable.** Falsified: `WorldObjectSceneRuntime` and canonical `SecretDiscoveryState` are available and integration regression run `33419056074` is green.
- **No production generated secret geometry exists.** Falsified: `CaveSecretPocketAuthoring` creates verified hidden-space/barrier topology and `CaveSecretPocketSecretCandidateProvider` projects that exact geometry into canonical WorldBuilder secret identity. Generated-cave bypass regression run `33420376990` is green.
- **Primitive validation can prove visual acceptance.** Falsified twice; parallel primitive rendering was removed.
- **Production clue evidence can be layered onto verified cave topology without weakening it.** Supported: deterministic normal voxel coating retains solid false-wall occupancy.
- **Gallery framing should be repaired for acceptance.** Superseded by user direction: Gallery integration is now out of scope and has been removed rather than further tuned.

## Selected direction

Use only `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` for visual acceptance. It consumes production voxel storage/terrain, cave generation, secret-pocket composition, clue coating, materials, voxel meshing/rendering, production destruction, and vegetation.

The built-player sequence must tell the whole discovery story rather than show one static exterior frame: exterior entrance -> just-inside entrance -> deeper cave -> clue-bearing wall approach -> close clue/wall view -> destroy the authored false wall -> show the breached route and hidden pocket behind it. Camera poses must derive from authored cave/pocket semantics, not captured-scene coordinates.

## Current work

The dedicated validation controller now drives that deterministic walkthrough and destroys the authored wall through `ShowcaseWorld.Explode`, so the reveal uses the production destruction/change-journal/render path. The player scenario runs 24 seconds with 3-second captures and requires both scene-ready and wall-destroyed log evidence.

A prior exact-SHA discriminator request is still queued on `ci-test/fixes/agent-5`; it must not be replaced. Because the feature head has advanced, that request is now historical evidence only. After it completes, submit one new exact-head request on the same transport.

## Remaining gates

1. Let the existing queued CI request finish untouched.
2. Run the new exact feature head through the sole `ci-test/fixes/agent-5` transport.
3. Inspect every full-resolution dedicated-scene frame. Require readable cave entry progression, visible clue treatment, intact false wall before destruction, a visible production destruction result, and a clear view into the hidden pocket afterward.
4. Validate no runtime/startup exceptions, behavioral regressions, bypass/discovery semantics, and cost/blast radius.
5. Merge current master before final validation/promotion; re-run exact-SHA gates if the head changes.
6. Close only after every acceptance checkbox and built-player proof is green.
