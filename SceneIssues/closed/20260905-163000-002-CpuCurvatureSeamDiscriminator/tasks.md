# R02 — Execution checklist

Budget: 45–60 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Identify one real curved primitive/terrain surface, source cells, reconstruction mode and adjoining chunk/LOD owners.
- [ ] Inspect relevant behavioral tests and capture same-LOD versus boundary views through production extraction. Reuse evidence/harness first; record missing module-local solid validation as a prerequisite.
- [ ] Localize the first mismatching boundary samples/edges or demonstrate continuous geometry; distinguish cracks, missing publication, faceting and shading.
- [ ] Deliver the minimal reproduction, falsified hypothesis and one proposed fix boundary. No algorithm, ring-size or transition-table changes.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
