# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` still requires representative `WorldbuildingGalleryShowcase` examples and exact built Gallery validation; the user later explicitly directed this assignment not to integrate into that Gallery. Acceptance may not be silently weakened, so that conflict is a closure blocker.

## Hypotheses / results

- Canonical hidden-secret selection already existed; route/readability/clue planning was the missing layer. Implemented with stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, and explicit bypass semantics.
- Canonical interactable/discovery integration is available and proven: run `33419056074` is green and revisit/reload/repeated activation remains idempotent.
- Generated cave secret geometry is reusable: `CaveSecretPocketComposition` authors verified barrier/connector/pocket topology and canonical WorldBuilder projection; run `33420376990` is green.
- Primitive/parallel visual proof failed production-quality review and was removed.
- Random moss coating preserved topology but was too subtle as a clue. Replaced with a deterministic cave-face fracture mask. Exact run `33537413920` passed the focused fracture regression plus all automatic module/player/Kentridge gates.

## Selected direction

Visual development stays in `Assets/Game/WorldBuilder/Validation/SecretDiscovery/`, using production terrain/storage, cave generation, material/coating rendering, vegetation, meshing, and destruction. The deterministic walkthrough captures entrance -> cave progression -> fracture-bearing false wall -> production explosion -> opened route -> hidden pocket. No helper mesh, emissive marker, primitive renderer, or capture coordinate is used.

Full-resolution run `33537413920`: 3s entrance, 6s interior, 9/12/15s readable fracture, 18s breached route, 21s hidden pocket. Player log reports `crackVoxels=35` and `607` voxels removed by the production explosion. The first 0s capture is a pre-ready transient and is not evidence.

## Current state / remaining gates

Last exact production source `8cc35bd4dd8d0c34444123a865f555cbde7ca21c` is green via request `9d018001d36b66f2c9002bc683f381d7eb0a5963` / run `33537413920`. Current branch has only durable evidence updates after that SHA.

Independent implementation/regression/player work is complete enough to validate the dedicated scene. Do **not** close while the Gallery acceptance conflict remains. Once resolved, merge current `origin/master`, re-run exact-SHA gates after the merge, inspect full-resolution evidence, complete issue metadata, move `open -> closed`, and push that exact head to `origin/master` non-force.
