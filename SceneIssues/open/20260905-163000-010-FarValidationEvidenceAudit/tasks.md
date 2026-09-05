# R10 — Execution checklist

Budget: 30–45 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Fetch the closed issue’s exact feature/transport/run evidence and verify successful whole-run completion and inspected captures.
- [ ] Map terrain, authoring, feature source, vegetation, materials, lighting, query cadence, edits and handoffs to production owners versus stand-ins.
- [ ] Mark each claimed acceptance criterion supported/unsupported/unknown; inspect actual images, not only log patterns or counts.
- [ ] Deliver a concise evidence-gap matrix and one bounded scene-composition proposal if needed. Record defects in this issue; do not alter the old closed record or treat diagnostic evidence as production acceptance.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
