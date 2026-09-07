# R01 — Execution checklist

Budget: 45–60 minutes active work, excluding queued CI. Follow [plan.md](plan.md).

- [ ] Record source SHA, baked-world identity, device/API, resolution/FOV, tier, actual ring radii, vsync/frame cap and backend counts. Do not use the divergent local branch as master evidence.
- [ ] Reuse existing player orchestration for settled and moving intervals; retain poses, logs and frame-time samples. Do not add a benchmark renderer or alter quality settings to improve results.
- [ ] Report frame p50/p95/p99, available CPU/GPU/render time, build backlog/convergence and upload traffic. Mark unavailable split timings unknown; never substitute inverse FPS for GPU time.
- [ ] Inspect matching captures for holes/seams/material artifacts and rank bottlenecks. Compare available measurements with the device matrix; historical 400 FPS remains unverified.
- [ ] Record findings, artifact provenance and limitations; update the concise plan with material results and falsified hypotheses. A test failure may be the investigation result, never a successful validation claim.
- [ ] Review the issue-only diff and complete documentation checks. Close only if investigation acceptance is satisfied; otherwise retain the blocker in `open/`. Follow the canonical PR/merge workflow and verify the result on master.
