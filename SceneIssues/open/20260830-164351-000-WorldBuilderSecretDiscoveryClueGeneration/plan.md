# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` still requires representative `WorldbuildingGalleryShowcase` examples; prior explicit user direction removed/prohibited this feature-specific Gallery integration. Acceptance may not be weakened, so that conflict remains the sole closure blocker.

## Hypotheses / material results

- Canonical hidden-secret selection already existed; deterministic route/readability/clue planning was the missing layer. Stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, route identity, canonical discovery idempotence, and explicit bypass semantics are implemented and behaviorally covered.
- The dedicated production-path SecretDiscovery scene validates generated cave composition, reusable semantic clue anchors, a deterministic sparse fracture clue, production destruction, and traversal into the hidden pocket. Exact run `33537413920` passed the focused fracture regression.
- Two repeated requested-filter zero-match failures were isolated as the post-merge optional persistent requested-test infrastructure defect (`experiment-014`); no third retry was made.
- A workflow-green run with blank near-surface voxel output was visually rejected. Merging the CPU renderer fallback restored cave/fracture rendering, but exposed a teardown lifecycle defect; `experiment-015` isolated it to renderer cleanup after all WorldBuilder evidence completed.
- After merging renderer lifecycle fixes, exact targeted run `33801222778` passed WorldBuilder EditMode, dedicated SecretDiscovery built player, Kentridge integration, and exact SceneIssue replay. Full-resolution 9/12/15 second frames show the non-glowing sparse fracture; 18/21 second frames show the breached route and reachable hidden interior; logs retain the expected 35 clue voxels and 607 destroyed voxels with clean teardown.
- The exact `WorldbuildingGalleryShowcase` replay reaches a usable rendered state without runtime exceptions, but its capture does not prove representative SecretDiscovery examples are present there.
- Master `431b7a5b501a8e1160d4b8ec90aeaa1752f72881` introduced the PR-based SceneIssue promotion/full-app gate and was merged through `51fd1d3dbd440823a0f11448804126e2e6e6e3cf`.
- Current master `c7774f8f3455481f003898bbe473789348cd4f66` adds the completed GameSystem09 inventory/Kentridge integration and removes the repository `.github/test-request.json` template. Those paths do not overlap this assignment's product implementation. Two-parent merge `6a48636f8134af2936c44912e73abd95151cc455` incorporates that exact master while preserving only this assignment's WorldBuilder/CaveWorldBuilder/Showcase and SceneIssue changes; compare reports `behind_by=0` with current master as merge base.

## Selected direction / remaining gates

Keep visual development in `Assets/Game/WorldBuilder/Validation/SecretDiscovery/` and use only production authoring/rendering/destruction paths. Do not add placeholder geometry, emissive secret markers, parallel interaction/discovery state, or hide cleanup failures.

The sole product acceptance blocker remains the instruction/acceptance conflict: representative generated SecretDiscovery examples must be visible and understandable in `WorldbuildingGalleryShowcase`, but feature-specific Gallery integration was explicitly prohibited. Do not mark that criterion complete or close while this conflict remains.

If the conflict is explicitly resolved, restore/implement the representative Gallery example through the production feature path, visually validate it at gameplay scale, run a fresh exact-SHA targeted gate on `ci-test/fixes/agent-5`, complete closure bookkeeping, merge current master, then follow the current repository workflow: open/update the feature PR to `master`, enable auto-merge, and require the `affected` PR gate plus canonical Kentridge full-application test to pass before considering the assignment complete.
