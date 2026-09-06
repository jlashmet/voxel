# Kentridge macro-world physical realization — deferred

## Disposition

Administratively closed at the user's explicit request on 2026-09-05 Pacific time: "lets just close this scene issue. we can revisit later". This supersedes the instruction to continue working until acceptance for this assignment only. This is **deferred, not fixed or accepted**. Do not resume polling, submit new feature CI, or create a follow-up assignment without renewed authorization.

The original criteria remain in `issue.json`. `tasks.md` is the unchanged execution/acceptance snapshot at deferral: unchecked items remain incomplete and checked implementation items describe the archived work, not a new delivery on master. Historical experiment files are retained as evidence, not current work orders.

## Preserved work

Implementation/checkpoint: `62533f5c0b1716c70414eb82d0e2b0def9e99f39` on `fixes/agent-6` before administrative reconciliation. Its merge base with closure-time master is `ed5c6f908361228819b3368bcd8427d4b44d89e3`. Closure-time master is `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

The administrative reconciliation retains the checkpoint as a parent for recovery, adopts master's production tree unchanged, and moves only this assignment's documentation into `SceneIssues/closed/20260829-020634-000-KentridgeMacroWorldPhysicalRealization`. No unvalidated production/test/scene changes are promoted. Recover/review the feature delta between the recorded merge base and checkpoint when revisiting; do not assume merging the old checkpoint will reapply archived code once its ancestry has been recorded by closure.

The checkpoint contains deterministic macro geography, terrain-aware roads, settlement realization, streaming/readiness work, regressions, and four module-local validation surfaces: WorldBuilder `MacroPhysicalWorld`, Showcase `FeatureResidency`, Kentridge `KentridgeMacroWorld`, and Rendering `GpuSurfaceMirrorRelocation`.

## Unresolved evidence

Agent-6 run `33962213806` on source `870c6bd0b9fed9005586945a328a9e5a8ed2f1dd` passed persistent/GPU-relocation regressions. The 180-second Kentridge replay still reached Moordell content readiness without complete published near-surface coverage (`jobs=8 missing=89`). Remaining settlement/geography/traversal captures and final CPU/GPU/streaming/memory costs are not accepted.

Agent-1 run `34003412217` subsequently passed its far-frustum repair automation. That different failure concerns tapered far geometry being replaced by a bounding box; it does not establish that Kentridge's near-surface publication failure is fixed. No new Kentridge acceptance is claimed.

## Revisit, only when explicitly authorized

Reconcile the preserved feature delta with then-current master and renderer ownership. Re-evaluate the actual publication failure rather than assuming the far-frustum repair resolves it. Restore the assignment to `open/` only when authorized, then run required exact-SHA module/player and 180-second Kentridge validation, inspect durable visual evidence against the issue's stated blockout scope, and measure costs without relaxing budgets or readiness. The old CI transport remains untouched; this administrative PR follows normal PR + auto-merge protection.
