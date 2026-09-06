# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, retire prior state, and prove useful framing/materials/contact plus bounded switching through exact standalone-player evidence. Only production-quality visuals pass; no gate or checkbox is waived.

Canonical set remains 529 entries: 440 registered decorations, 25 presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds. Structures owns enumeration/shared presenters; SceneRuntime owns browser/resource orchestration; Materials owns procedural-material composition. Each runtime owner has a local validation surface. Top-level showcase scenes are integration consumers only.

## Current source and exact failures
Current branch includes shared visual repairs (`2b7e30e1`), compile ownership (`2154840b`), trapdoor mount (`849875e3`), material ownership (`9697d365`), resource instrumentation (`36141bae`) and PlayMode isolation (`79b6a2f4`). Latest observed master is `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; final master merge is outstanding.

Run `34003328146` proved production material mode but failed PlayMode teardown and visually rejected Sign/Hearth/Door/Trapdoor. Run `34007356710` failed compilation; `2154840b` fixed its two assembly-boundary causes. Run `34011392051` then timed out because top-level `Assets/Scenes/PropShowcase.unity` entered unknown-path fallback: the plan expanded to 48 modules, 52 tests and 23 players. Persistent tests had passed before timeout. It also exposed same-module player artifact overwrite. Exact evidence: `review-34011392051.md`.

## Selected fixes
Shared production paths now provide framed painting-family thin surfaces, detailed Door/SecretDoor/Trapdoor mechanism meshes, semantic decoration light/particle presentation, corrected horizontal Trapdoor baseline, and truthful voxel `LOADING`→`READY` publication state. Forge Hearth's canonical voxel geometry is unchanged pending fresh evidence.

CI planning now treats top-level `Assets/Scenes/*.unity` as integration-only while still attaching Kentridge once for production diffs; unknown paths retain broad safe fallback. Player artifact roots are keyed by module plus scene so module-local evidence cannot overwrite itself. Focused Python regressions cover both repaired invariants.

## Remaining gates
Run the latest exact source through `ci-test/fixes/agent-9`; inspect module-local and SceneIssue captures directly. Verify sign, Door/Trapdoor, hearth/effects, readiness, representative framing and three-cycle resources. Fix only demonstrated remaining failures. Then complete issue metadata/checklists, move open→closed, merge current master, open final PR, enable auto-merge and monitor `affected` until closed state is visible on master.
