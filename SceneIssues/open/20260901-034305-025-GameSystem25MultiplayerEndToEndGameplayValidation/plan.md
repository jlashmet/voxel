# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. No gameplay authority, alternate transport, or test-only networking runtime. Production semantics remain in `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; validation drives public player/session inputs and observes read-only semantic truth.

## Observed state / hypotheses

- **H1: System25 is integration validation over existing production systems. Supported.** Session formation owns Host/Join; party/session queries expose stable member/slot/character identity; network admission binds transient connections to those identities; Continuity owns reconnect/recovery.
- **H2: a parallel test networking/runtime layer is needed. Rejected.** Public production seams already represent formation, identity, reconnect, readiness, and leave.
- Shared `mode: multiProcess` is complete for build-once role launch/wait/kill/relaunch, isolated role state/logs, automatic source-SHA + executable-SHA256 verification before gameplay waits, bounded semantic milestones, and per-target artifacts.
- Correctness audit tightened reconnect evidence: ephemeral HOME/temp/config/cache are attempt-scoped while only the role durable state root survives relaunch; sequential waits consume milestones monotonically; player payloads cannot override harness-owned role/attempt attribution.
- Release-tier discovery is structural: ordinary production diffs use smoke targets; `<Module>/Validation/Release/` targets run when changed and from the generic scheduled/manual release workflow.
- Exact harness/tooling proof: feature `496e3f9d7a88658029aa332b3596caf86e1cabb2`, request `bbb63019d00a93b44cff24c7a2b20d7ae12e461e`, run `33936350595`.
- Exact build-identity proof: feature `73c3bafd0268fcc80c453d33900f6848e7571153`, request `71b21322f8d3bb776553e5d915bc10d2c0664695`, run `33937957149`; player SHA-256 `8129fbc54fb83c5ac219204df3c9fd2c7482d9c1225daa87c67447cec9db27ae`, `identityVerified: true` before gameplay acceptance.
- **T25-050 complete:** static audit confirms the System25 harness only controls production player processes, identity, state isolation, semantic waits, and logs; it contains no direct socket injection, alternate transport, gameplay mutation authority, or privileged command seam.
- **External prerequisite:** current `origin/master` `a180749ed7c00d28bed6661fc9a3da4c9a9b61fc` still has System24 open. `fixes/agent-2` is `f6b3ace316f7122b48135ea04c0f04078049d9a5`; its representative player route and T24-032 read-only diagnostic remain unchecked. System25 must not copy or substitute that unmerged Kentridge production composition/diagnostic boundary.

## Selected work / remaining gates

1. Finish exact-SHA proof for the independent harness correctness work. Request `382b9c33a2f4edabd7bf46c598e1ee1d9eea0291` targets exact feature `b2d1847ec109eb8be7a631c084bd60230c5932a2` and is queued; it must not be replaced while queued/running. The later role-attribution correction requires one subsequent exact-SHA request after that run completes.
2. After System24 lands, merge current master and reuse its production Kentridge entry/read-only diagnostic seam.
3. Add authority + two-client smoke validation and release scenarios under the owning Kentridge validation surface.
4. Prove contention/conservation, vitality, progression, interruption/reconnect, explicit leave, capacity/JIP/repeated reconnect, and persisted rehost on the exact built SHA.
5. Run repository-selected final exact-SHA validation, inspect durable artifacts, complete closure fields/move, merge latest master, then PR + auto-merge and required `affected` gate.

**Current branch:** harness correctness/docs head `0656b19b4ceccf5d36873ef9f02626ed375aae8d`; current verified product implementation remains `73c3bafd0268fcc80c453d33900f6848e7571153`.

**Blast radius / cost:** validation tooling/workflows plus Kentridge validation composition only; production authority remains untouched. Smoke is authority + two clients; expensive cases stay in release targets.
