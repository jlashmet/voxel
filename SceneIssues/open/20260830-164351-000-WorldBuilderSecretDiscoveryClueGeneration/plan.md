# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and representative built-player proof. This issue has no captures/marked regions. Per user direction, visual proof is owned entirely by the dedicated module-local Secret Discovery validation scene; there must be no `WorldbuildingGalleryShowcase` secret integration.

## Hypotheses and results

- **Hidden-secret selection is missing/non-deterministic.** Falsified: production `SecretPlanner` already resolves canonical hidden candidates deterministically.
- **Clue generation needs a second hidden-location solver.** Rejected: route/clue planning consumes canonical `ResolvedSecretPlan` identity.
- **Route/readability/clue planning was missing.** Supported; implemented with stable IDs, semantic anchors, readability/diversity policy, diagnostics, and explicit bypass semantics.
- **Reusable interaction/discovery APIs are unavailable.** Falsified: canonical runtime integration is available and targeted regressions are green.
- **No production generated secret geometry exists.** Falsified: `CaveSecretPocketAuthoring` creates verified hidden-space/barrier topology and projects that exact geometry into canonical WorldBuilder secret identity.
- **Primitive validation can prove visual acceptance.** Falsified twice; parallel primitive rendering was removed.
- **Random coating across the barrier is a readable clue.** Rejected by built-player review: the walkthrough/reveal looked good, but the clue itself was not recognizable without prior knowledge.
- **A sparse deterministic fracture pattern can improve readability without weakening topology.** Current implementation: coat only the cave-facing barrier layer in one continuous branching crack pattern; no carving/filling and the full barrier is revalidated solid afterward.

## Selected direction

Use only `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` for visual acceptance. It consumes production voxel storage/terrain, cave generation, secret-pocket composition, clue coating, materials, voxel meshing/rendering, production destruction, and vegetation.

The built-player sequence tells the complete discovery story: exterior entrance -> just-inside entrance -> deeper cave -> clue-bearing wall approach -> close clue/wall view -> destroy the authored false wall -> show the breached route and hidden pocket behind it. Camera poses derive from authored cave/pocket semantics, not captured-scene coordinates.

## Current work

Exact walkthrough run `33532261836` produced the intended cave-entry, wall-destruction, and reveal sequence. Visual review accepted the overall scene/reveal but identified clue readability as the remaining defect.

The clue presentation now produces a deterministic branching fracture on the cave-facing surface only. The validation consumer uses a dark soot coating for the fracture instead of broad moss speckling. A focused regression proves deterministic placement, cave-face-only presentation, continuous vertical extent, sparse coverage, and unchanged solid false-wall occupancy.

## Remaining gates

1. Run the exact crack-pattern feature head through the sole `ci-test/fixes/agent-5` transport.
2. Inspect every full-resolution dedicated-scene frame. Require the fracture to read as intentional wall damage at approach/close range without looking like a universal marker.
3. Confirm intact false wall before destruction, visible production destruction result, and a clear view into the hidden pocket afterward.
4. Validate no runtime/startup exceptions, behavioral regressions, bypass/discovery semantics, and cost/blast radius.
5. Merge current master before final validation/promotion; re-run exact-SHA gates if the head changes.
6. Close only after every acceptance checkbox and built-player proof is green.
