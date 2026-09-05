# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. No gameplay authority, alternate transport, or test-only networking runtime. Production semantics remain in `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; validation drives public player/session inputs and observes read-only semantic truth.

## Observed state / hypotheses

- **H1: System25 is integration validation over existing production systems. Supported.** Session formation owns Host/Join; party/session queries expose stable member/slot/character identity; network admission binds transient connections to those identities; Continuity owns reconnect/recovery.
- **H2: a parallel test networking/runtime layer is needed. Rejected.** Public production seams already represent formation, identity, reconnect, readiness, and leave.
- Shared `mode: multiProcess` owns build-once role launch/wait/kill/relaunch, exact source/executable identity, bounded semantic milestones, role/attempt artifacts, and state isolation.
- **Correctness audit A:** sequential waits could reuse a stale same-name milestone. The runner now consumes milestone events monotonically per process attempt and regression coverage requires distinct convergence revisions.
- **Correctness audit B:** relaunch attempts reused ephemeral HOME/temp/config/cache. The runner now attempt-scopes all ephemeral roots while preserving only the role's durable validation state root across reconnect attempts.
- Release-tier discovery is structural: ordinary production diffs use smoke targets; `<Module>/Validation/Release/` targets run when changed and from the generic scheduled/manual release workflow.
- Exact harness/tooling proof: feature `496e3f9d7a88658029aa332b3596caf86e1cabb2`, request `bbb63019d00a93b44cff24c7a2b20d7ae12e461e`, run `33936350595`.
- Exact build-identity proof: feature `73c3bafd0268fcc80c453d33900f6848e7571153`, request `71b21322f8d3bb776553e5d915bc10d2c0664695`, run `33937957149`; player SHA-256 `8129fbc54fb83c5ac219204df3c9fd2c7482d9c1225daa87c67447cec9db27ae`, `identityVerified: true` before gameplay acceptance.
- **T25-050 complete:** static audit confirms the System25 harness only controls production player processes, identity, state isolation, semantic waits, and logs; it contains no direct socket injection, alternate transport, gameplay mutation authority, or privileged command seam.
- **External prerequisite:** `origin/master` `939e9a6f744313d93992b0479d5f6140d774ef42` still has System24 open and its T24-032 read-only diagnostic gate was last observed unchecked. Agent-2 is actively advancing that prerequisite. System25 must not copy or substitute its unmerged Kentridge production composition/diagnostic boundary.

## Selected work / remaining gates

1. Validate the two independent harness correctness fixes on one exact current feature SHA; do not replace the already queued request while it is pending.
2. After System24 lands, merge current master and reuse its production Kentridge entry/read-only diagnostic seam.
3. Add authority + two-client smoke validation and release scenarios under the owning Kentridge validation surface.
4. Prove contention/conservation, vitality, progression, interruption/reconnect, explicit leave, capacity/JIP/repeated reconnect, and persisted rehost on the exact built SHA.
5. Run repository-selected exact-SHA validation, inspect durable artifacts, complete closure fields/move, merge latest master, then PR + auto-merge and required `affected` gate.

**Current independently completed work:** generic multi-process harness, exact build identity, release-tier discovery, no-test-runtime audit, stale-milestone fix, and relaunch ephemeral-state isolation. The latest two correctness fixes still require exact-SHA proof before their task checkboxes can close.

**Blast radius / cost:** validation tooling/workflows plus Kentridge validation composition only; production authority remains untouched. Smoke is authority + two clients; expensive cases stay in release targets.
