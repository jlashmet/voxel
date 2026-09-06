# R05 — Execution checklist

Budget: 45–60 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Inventory CPU/GPU payload, page tables, mirror/directory, lane scratch, staging, draw buffers and far caches; cite allocation sites and avoid double-counting.
- [ ] Reconcile displayed committed bytes with actual allocations; calculate PC/Console/Mobile-HE configured totals with explicit assumptions and units.
- [ ] Record actual versus required detail/view/load radii, world extent, build/streaming budgets and unavailable platform evidence. A Mac result is not PC DX12/Vulkan, console or phone proof.
- [ ] Deliver resource headroom/unknowns and the next bounded measurement. Preserve ≤6/7/9 ms rendering, 0.20 ms extraction build, ≤0.5 ms main-thread streaming, geometry caps and two-hour ±2% world-memory targets.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
