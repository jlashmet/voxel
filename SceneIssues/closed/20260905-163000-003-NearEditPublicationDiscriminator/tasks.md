# R03 — Execution checklist

Budget: 45–60 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Choose one existing production edit action and record affected voxel/chunk coordinates and starting versions.
- [ ] Collect before/edit/converged standalone frames with version and active-coverage trace using existing diagnostics.
- [ ] Distinguish missed invalidation, rejected stale build, pending replacement, valid empty result and coverage hole. Check visual state against the edited cells.
- [ ] Report first correct visible frame and available latency against ≤150 ms p95 authoritative update and ≤1-frame prediction targets, labeling a single event as a sample rather than a percentile proof. Propose only the proven owner to investigate/fix next.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
