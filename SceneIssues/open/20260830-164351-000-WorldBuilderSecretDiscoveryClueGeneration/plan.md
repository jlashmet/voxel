# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and built-player proof. There are no original captures/marked regions. `issue.json` still requires representative `WorldbuildingGalleryShowcase` examples and exact built Gallery validation; the user explicitly directed this assignment not to integrate the feature into that Gallery. Acceptance may not be silently weakened, so that conflict remains a closure blocker.

## Hypotheses / results

- Canonical hidden-secret selection already existed; route/readability/clue planning was the missing layer. Implemented with stable IDs, semantic anchors, deterministic scoring/tie-breaking, diagnostics, and explicit bypass semantics.
- Canonical interactable/discovery integration is available and proven: run `33419056074` is green and revisit/reload/repeated activation remains idempotent.
- Generated cave secret geometry is reusable: `CaveSecretPocketComposition` authors verified barrier/connector/pocket topology and canonical WorldBuilder projection; run `33420376990` is green.
- Primitive/parallel visual proof failed production-quality review and was removed.
- Random moss coating preserved topology but was too subtle as a clue. Replaced with a deterministic cave-face fracture mask. Exact run `33537413920` passed the focused fracture regression plus the then-current module/player/Kentridge gates.
- Current repository convention no longer uses `*.module-validation.json`; the dedicated scene/scenario and module-local tests are convention-discovered. The obsolete registration file was removed after merging master.
- Post-merge runs `33654878544` and `33714475042` both failed only because the new optional persistent requested-test path selected zero cases. Experiment 014 isolated that infrastructure defect; no third optional-filter retry is allowed.
- Required-gates run `33715584697` was workflow-green but visually rejected because near-surface terrain/cave geometry did not render in the dedicated player even though vegetation and semantic secret evidence existed.
- Merging master `b18d470f66221c7cb6091249f4683c2d994bffec`, including its CPU surface-renderer fallback, produced feature source `731e55c2ed1808c2d6fb21189c96011887bec6e3`. Exact run `33716327931` falsified the hypothesis that near-surface publication still fails: 9/12/15 second full-resolution captures render the cave and sparse branching fracture, and 18/21 second captures render the breached/opened hidden interior.
- That player failed during teardown after all capture/evidence work. `RenderingComposition.ClearWorld()` reached a `NullReferenceException` in `GpuSurfaceMirrorCoordinator.DetachPageArena`, then shutdown segfaulted while disposing CPU transvoxel resources through `VoxelSurfaceScheduler` / `VoxelRenderPass` / URP renderer disposal. Experiment 015 isolated this as a renderer resource-lifecycle defect outside the WorldBuilder acceptance implementation.
- Current master `f5593cc1236ba3963fc5713a11df35292628e97d` now contains direct lifecycle corrections for the isolated failure boundary, including CPU-only GPU arena detach handling plus native lifetime corrections/regressions for Transvoxel workspace and CPU water cache ownership. It is merged into this feature by two-parent merge commit `342d7cf84a5dcfc23c752ffb0f08388605e54af9`.

## Selected direction

Visual development stays in `Assets/Game/WorldBuilder/Validation/SecretDiscovery/`, using production terrain/storage, cave generation, material/coating rendering, vegetation, meshing, and destruction. The deterministic walkthrough captures entrance -> cave progression -> fracture-bearing false wall -> production explosion -> opened route -> hidden pocket. No helper mesh, emissive marker, primitive renderer, or capture coordinate is used.

Do not work around teardown by omitting `RenderingComposition.ClearWorld()` from validation. Shared cleanup intentionally retires renderer-owned world resources before application storage is disposed; bypassing it would hide a production runtime exception and weaken acceptance.

## Current state / remaining gates

The previously external renderer lifecycle prerequisite is now available through `origin/master` and merged. The next non-blocked task is therefore an exact-SHA revalidation through `ci-test/fixes/agent-5` using the convention-derived WorldBuilder tests, SecretDiscovery built player, Kentridge integration, and SceneIssue replay. Do not use the optional requested-test filter because experiment 014 already isolated that path as a separate CI infrastructure regression. Inspect full-resolution captures and teardown logs before accepting the gate.

If the same renderer teardown symptom survives this materially different lifecycle correction, stop and update the minimal lifecycle root cause before another fix or retry.

A second independent closure blocker remains unchanged: required representative `WorldbuildingGalleryShowcase` examples conflict with explicit user direction prohibiting this feature's Gallery integration. Do **not** close or change acceptance until that conflict is resolved.
