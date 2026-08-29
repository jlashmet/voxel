# Plan

## Observed behavior and acceptance
`issue.json` defines a feature-only WorldBuilder assignment with no recorded captures. `VoxelShowcase` must gain a natural walkable cave mouth, long gentle descending route with organic width/height/direction variation, huge irregular cavern, multiple geological formation categories, a reachable aged stone ruin with exactly two grounded statues, localized supported torch lighting, and preserved deep darkness. Final validation requires production-path behavioral regression, exact built-app scene harness coverage, visual review, and blast-radius/cost checks.

`SceneIssues/feature-readme.md` is absent on current `master`; `AGENTS.md` points to `SceneIssues/README.md` as the available workflow authority, so that workflow plus the assignment contract is being followed.

## Evidence and discriminators
- Live branch head before this ledger refresh was `ac98f8a316d6b323cbc421ce8e877352b99f6504`; current `master` was `47f3733237fb7a289daaa0ca5dae1e5059ed4bff`, with the feature 12 commits ahead / 64 behind.
- The branch already contains reusable `Game.Structures` cavern/ruin authoring plus `VoxelShowcase` runtime/offline integration. It builds a deep host sleeve, runs shared `CaveAuthoring`, selects a reachable terminal by traversal semantics, authors a large irregular cavern, natural cave decoration, an ancient masonry ruin, and exactly two dark-stone humanoids.
- The current production cave configuration deliberately sets `TurnChancePercent = 0`, and the composer rejects nonzero turn chance because its deep-host sleeve assumes a straight primary route. Side transition chambers widen the route but do not make the required walk itself change direction.
- Shared `CaveNetworkAuthoringCore` supports deterministic turns, but changing that config alone would invalidate the host-sleeve/preload assumptions and risk moving the terminal unpredictably. The smaller safe fix is to keep the authoritative straight network identity and add deterministic authored dog-leg transition sections in the reusable cavern composer, with clearance/reachability regression.
- The generic cave entrance is a rectangular cross-section carve. Add an asymmetric reusable mouth treatment around the surface transition rather than a showcase-only mesh/debug hole.
- Current supported mine-cave lantern planning runs only in the destination cavern. Extend the same supported lantern/light semantics to a small number of deterministic descent waypoints; do not use emission-only voxels as a substitute for local lights.
- Existing `Game.Structures.Tests/UndergroundCavernRuinAuthoringTests` validates the helper with a counting backend, but acceptance also requires an authoritative production WorldBuilder/`VoxelShowcase` regression and built-player evidence.

## Selected fix
1. Extend `UndergroundCavernRuinAuthoring` with an asymmetric natural mouth and deterministic dog-leg transition geometry while retaining the shared cave network as the reachability/identity source and keeping the deep host bounded.
2. Add sparse supported route lantern fixtures/light requests and combine them with cavern lanterns under a strict small cap.
3. Add a narrow PlayMode production acceptance test that executes the real `ShowcaseWorld` cavern generation path and checks production outputs, plus strengthen the existing focused authoring regression for the newly exposed invariants.
4. Keep changes confined to cavern authoring, its `VoxelShowcase` integration, and assignment-owned regressions/metadata; do not alter generic cave generation unless a proven invariant requires it.
5. Measure write/light cost, merge current master before final CI, run one exact-SHA PlayMode request with built-player `scene_issue` capture, review the rendered evidence, then complete pending/closed metadata and fast-forward master non-force.

## Blast radius / cost hypotheses to validate
- Shared-system blast radius should remain local because the reusable composer is opt-in and currently consumed by `VoxelShowcase`; generic cave-network behavior should remain unchanged.
- Main cost is one-time authored voxel writes plus region preloads; added mouth/dog-leg/lantern geometry should be small relative to the existing 55,000,000-write feature budget. Local lights must remain single-digit to bound draw/shadow cost.

## Remaining gates
See `tasks.md`; no feature closure until every checkbox and every `issue.json` acceptance criterion is validated.
