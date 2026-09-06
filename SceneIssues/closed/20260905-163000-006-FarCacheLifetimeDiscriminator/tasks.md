# R06 — Execution checklist

Budget: 30–45 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Trace the query cadence, geometry identity and cache ownership through one production consumer.
- [ ] Reuse existing instrumentation or debugger/profiler observations to measure 300 unchanged frames and one revisit; record exact build/device and mesh/cache counts.
- [ ] Attribute rebuild/allocation/retention to its first owner. Do not infer world-size boundedness from a stationary sample.
- [ ] Deliver a revision/cache lifetime invariant and narrow repair proposal, with measurements or a precise instrumentation blocker. No cache implementation changes.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
