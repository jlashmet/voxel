# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, rejection diagnostics, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus a module-owned multiplayer scenario. No new gameplay authority, transport, or test-only networking runtime. Production semantics are split across `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`. Orchestration may launch/stop processes and observe read-only semantic milestones; gameplay mutations must enter through production public player/session inputs.

## Observed state and hypotheses

- Module validation auto-discovers paired `<Module>/Validation/*.unity` + `*.player-scenario.json` and routes them through `tools/player-validation.py`; no registration layer is needed.
- **H1:** System25 is mainly integration validation over existing production systems. **Supported** for process lifecycle, durable identity contracts, and replication revision/readiness seams.
- **H2:** a parallel test networking/runtime layer is required. **Rejected** by the existing production APIs and repo architecture.
- The first exact-SHA tool run failed only because the Python 3.14 dynamic-import test did not register its module in `sys.modules`; the targeted import fix is committed. A subsequent review found build identity was scenario-optional; the harness now verifies `sourceSha` + executable SHA-256 automatically on every launch/relaunch before any gameplay wait and rejects identity-only scenarios as zero gameplay proof.
- Discriminating prerequisite: System24 must land the production-composed built-player entry and read-only diagnostic boundary before System25 can honestly drive the full gameplay loop. It remains open on current `origin/master`; do not substitute test composition.
- Repository workflows contain targeted/PR/master gates but no existing scheduled multiplayer player-validation lane. Do not call the post-merge all-EditMode master suite a release scenario tier; resolve T25-044 only when the production scenario exists and an appropriate repository-owned slower lane is identified or acceptance requires adding one.

## Selected work

1. Keep `mode: multiProcess` inside the canonical player-validation entrypoint and generic build-once orchestrator.
2. Keep lifecycle role-driven: launch, bounded semantic wait, terminate/kill, relaunch; preserve isolated durable role state across attempts and exact build identity per process.
3. Validate independent harness work with Python regressions and exact-SHA CI while System24 is unavailable.
4. After System24 lands, merge current master, reuse its production entry/diagnostic seam, add the correct module-owned multiplayer scene/scenario, and complete formation, gameplay convergence, reconnect/leave, capacity/JIP/reconnect, and persisted rehost evidence.

**Current feature SHA before this plan-only commit:** `0b162344ddeb9f05093029a9e441cbd6e029d715`.

**Blast radius / cost:** current changes are validation tooling only; no production authority/runtime module is changed. The PR smoke target remains authority + two clients; expensive capacity/rehost cases must not inflate the normal targeted gate.

**Remaining gates:** current-head tool exact-SHA CI; System24 prerequisite; module-local standalone multiplayer scenario; all gameplay/continuity/rejection/capacity/rehost checks; final exact-SHA built-player evidence; closure bookkeeping; latest-master merge; PR `affected` gate + auto-merge.
