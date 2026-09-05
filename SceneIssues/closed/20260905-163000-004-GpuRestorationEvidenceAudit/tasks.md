# R04 — Execution checklist

Budget: 30–45 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Map restoration claims and feature/transport SHAs to current master; record which fixes are actually merged.
- [ ] Inspect the most relevant existing density source-step and prepared two-origin batch results, compiler status, test counts and whole-workflow conclusions.
- [ ] Audit eligible/unsupported/failure fallback policy and captured GPU counts; distinguish GPU-supported geometry from the whole world.
- [ ] Deliver a supported/failed/unknown table and a precise next experiment if needed. Do not modify the existing owner’s files, retry its CI, or reopen previously falsified syntax experiments.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
