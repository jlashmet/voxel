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
- **Harness correctness exact proof:** feature `b2d1847ec109eb8be7a631c084bd60230c5932a2` passed request `382b9c33a2f4edabd7bf46c598e1ee1d9eea0291` in run `33986100313`; automatic module validation and artifact publication both succeeded. This closes T25-004 and T25-008. T25-009 was committed afterward and remains intentionally open for a later exact-SHA proof.
- **T25-050 complete:** static audit confirms the System25 harness only controls production player processes, identity, state isolation, semantic waits, and logs; it contains no direct socket injection, alternate transport, gameplay mutation authority, or privileged command seam.
- **External prerequisite:** System24 remains unmerged. Its exact feature `f6b3ace316f7122b48135ea04c0f04078049d9a5`, request `64296df9f805d2690942d5f302e07a32f3a8b823`, run `33984774287` reached the real validator but failed during automatic module validation because Unity reported script compiler errors. System25 must not copy or substitute that unmerged Kentridge production composition/diagnostic boundary.

## Selected work / remaining gates

1. Keep the independent harness fixes intact. T25-004/T25-008 are exact-SHA proven; T25-009 will be included in the next current-head exact-SHA proof rather than treated as covered by the older green SHA.
2. After System24 is corrected, closed, and merged, merge current master and reuse its production Kentridge entry/read-only diagnostic seam.
3. Add authority + two-client smoke validation and release scenarios under the owning Kentridge validation surface.
4. Prove contention/conservation, vitality, progression, interruption/reconnect, explicit leave, capacity/JIP/repeated reconnect, and persisted rehost on the exact built SHA.
5. Run repository-selected final exact-SHA validation, inspect durable artifacts, complete closure fields/move, merge latest master, then PR + auto-merge and required `affected` gate.

**Current branch:** harness audit/evidence head advances from `51e876c88e152906a3c715359c59c0473e2c60f2`; the next branch head includes only Scene25 evidence bookkeeping on top. Current fully exact-proven harness feature is `b2d1847ec109eb8be7a631c084bd60230c5932a2`.

**Blast radius / cost:** validation tooling/workflows plus Kentridge validation composition only; production authority remains untouched. Smoke is authority + two clients; expensive cases stay in release targets.
