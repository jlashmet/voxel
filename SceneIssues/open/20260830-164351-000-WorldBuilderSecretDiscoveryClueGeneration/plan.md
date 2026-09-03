# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` still requires representative `WorldbuildingGalleryShowcase` examples and exact built Gallery validation; the user explicitly directed this assignment not to integrate the feature into that Gallery. Acceptance may not be silently weakened, so that conflict remains a closure blocker.

## Hypotheses / results

- Canonical hidden-secret selection already existed; route/readability/clue planning was the missing layer. Implemented with stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, and explicit bypass semantics.
- Canonical interactable/discovery integration is available and proven: run `33419056074` is green and revisit/reload/repeated activation remains idempotent.
- Generated cave secret geometry is reusable: `CaveSecretPocketComposition` authors verified barrier/connector/pocket topology and canonical WorldBuilder projection; run `33420376990` is green.
- Primitive/parallel visual proof failed production-quality review and was removed.
- Random moss coating preserved topology but was too subtle as a clue. Replaced with a deterministic cave-face fracture mask. Exact run `33537413920` passed the focused fracture regression plus the then-current module/player/Kentridge gates.
- Current repository convention no longer uses `*.module-validation.json`; the dedicated scene/scenario and module-local tests are convention-discovered. The obsolete registration file was removed after merging current master.
- Post-merge runs `33654878544` and `33714475042` both failed only because the new optional persistent requested-test path selected zero cases. Experiment 014 falsified a stale test name: the source still compiles, the requested phase discovers the 918-case EditMode tree, and the exact method name is unchanged from prior green run `33537413920`. The master merge changed push requests from direct CLI `-testFilter` execution to `VoxelCiPersistentTestRunner` / `Filter.testNames`; that optional path is the isolated infrastructure defect.

## Selected direction

Visual development stays in `Assets/Game/WorldBuilder/Validation/SecretDiscovery/`, using production terrain/storage, cave generation, material/coating rendering, vegetation, meshing, and destruction. The deterministic walkthrough captures entrance -> cave progression -> fracture-bearing false wall -> production explosion -> opened route -> hidden pocket. No helper mesh, emissive marker, primitive renderer, or capture coordinate is used.

Full-resolution run `33537413920`: 3s entrance, 6s interior, 9/12/15s readable fracture, 18s breached route, 21s hidden pocket. Player log reports `crackVoxels=35` and `607` voxels removed by the production explosion. The first 0s capture is a pre-ready transient and is not evidence.

## Current state / remaining gates

`origin/master` at `b1b69290a59278b0e7caba798641c76a9866aa5c` was merged into the feature branch in merge commit `dee150fa000597d6abe1a2693e3a15d429266fb5`. Conflict resolution moved this assignment's tests into the current module-local `Assets/Game/WorldBuilder/Tests/EditMode/` assembly layout. After the merge, the obsolete module-validation registration was removed in `eec43c913620425c36f65380efb04d3882c92390`.

Next discriminator is an exact-SHA CI retry with **no optional requested test**. The current workflow defines that focused request as an optional extra gate; required validation is the convention-derived WorldBuilder test assembly plus dedicated SecretDiscovery player scene and Kentridge integration. This avoids a proven post-merge optional-filter infrastructure regression without weakening required acceptance. Inspect full-resolution dedicated captures after the run. Do **not** close while the Gallery acceptance conflict remains. If exact-SHA validation is green, record it as independent work complete and remain open on the acceptance blocker.
