# PropShowcase tasks

## Audit and prerequisite
- [x] Fetch current origin refs; read `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md`; inspect the assigned issue, production catalogue/presentation code, and branch diff.
- [x] Compare the existing branch draft with the authoritative acceptance contract and remove the stale showcase-only catalogue/tests whose 529 total used the wrong legacy family composition.
- [x] Isolate the production prerequisite mismatch: the issue-named `DecorationAssemblyGenerator.cs` and `Structures/Api/WorldObjectCatalog.cs` are absent from current master/history; the actual `WorldObjectKind` has 48 concrete values rather than the required 82; current procedural requests do not carry the primitive/material semantics required for cylinder/capsule realization.

**Blocker:** Do not substitute fabricated identities, a manual 18-family showcase registry, guessed primitive/material policy, or a showcase-only world-object renderer. The following acceptance work remains blocked until the required production surfaces are present or the authoritative issue identifies acceptance-equivalent existing APIs/counts.

## Production catalogue and realization
- [ ] **BLOCKED** Implement production-derived discovery of exactly 529 leaves across all 18 issue-specified family counts, with family-unique readable display names, stable identity/configuration/provenance/parent grouping, deduplication, and required exclusions.
- [ ] **BLOCKED** Resolve every prepared/descriptor-derived entry through concrete production generator/descriptor semantics and canonical world-object entries through the production world-object catalogue/presentation runtime; fail explicitly on unresolved/placeholder-only entries.
- [ ] **BLOCKED** Add the narrow reusable Structures Runtime consumer/cache for emitted procedural cylinder/capsule requests while preserving canonical material and metadata semantics.
- [ ] **BLOCKED** Add focused regressions for exact catalogue/family parity, uniqueness/provenance, unresolved-entry failure, mixed realization backends, representative world-object fixture/furniture/surface/household realization, and renderer/mesh-derived geometry costs.

## Module-local validation
- [ ] **BLOCKED** Identify every changed player-visible/runtime module after implementation and create/update focused validation scenes under each owning module's `<Module>/Validation/` path using the real production authoring/material/presentation path.
- [ ] **BLOCKED** Add module-local executable `*.player-scenario.json` only where runtime actions/captures/assertions are required; do not add manual registration metadata.

## PropShowcase integration
- [ ] **BLOCKED** Create `Assets/Game/Showcase/Scenes/PropShowcase.unity` and its browser controller with a compact bounded physical subset, scene-local lighting/framing/grounding, display-name-first selected panel, stable identity/config/provenance, concise legend, and deterministic keyboard/mouse plus ordinary joystick/gamepad previous/next/back controls.
- [ ] **BLOCKED** Realize displayed entries only through shipped voxel-stamp, thin-surface, procedural-mesh, material, and canonical world-object presentation paths; add no showcase-owned decorative stand-ins.
- [ ] **BLOCKED** Derive selected and visible-scene draw/triangle counts from active nearby renderers/batches so hidden catalogue entries do not contribute.

## Built-player acceptance and completion
- [ ] **BLOCKED** Add the standalone PropShowcase player scenario that traverses every top-level family, asserts exact 529/family counts, exercises keyboard and joystick-equivalent controls, captures all issue-required representative families, verifies no unresolved/placeholder entries and selected triangles > 0, and samples >=600 warm frames with p99 <= 12 ms.
- [ ] **BLOCKED** Run exact-feature-SHA targeted CI through `ci-test/fixes/agent-9` without replacing queued/running work; inspect all required artifacts and runtime/counter assertions.
- [ ] **BLOCKED** Directly inspect exact built-player visual evidence and classify it `production-quality`; add/fix any demonstrated clipping, intersection, grounding, material, framing, labeling, transition, or readability defects before closure.
- [ ] **BLOCKED** Review the final diff and every acceptance criterion; fill `resolutionSummary`, `regressionTest`, and `fixCommit`, set `status: fixed`/`resolvedUtc`, and move only this issue `open -> closed` after all required gates are green.
- [ ] **BLOCKED** Merge current `origin/master` into `fixes/agent-9`, push the final feature branch, open/update the PR to `master`, enable auto-merge, and monitor the required `affected` gate until merged and the closed issue is visible on `origin/master`.
