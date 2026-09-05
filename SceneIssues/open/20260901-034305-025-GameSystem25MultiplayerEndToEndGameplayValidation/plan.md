# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. No gameplay authority, alternate transport, or test-only networking runtime. Production semantics remain in `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; validation drives public player/session inputs and observes read-only semantic truth.

## Observed state / hypotheses

- **H1: System25 is integration validation over existing production systems. Supported.** Session formation owns Host/Join; party/session queries expose stable member/slot/character identity; network admission binds transient connections to those identities; Continuity owns reconnect/recovery.
- **H2: a parallel test networking/runtime layer is needed. Rejected.** Public production seams already represent formation, identity, reconnect, readiness, and leave.
- Shared `mode: multiProcess` is complete for build-once role launch/wait/kill/relaunch, isolated role state/logs, automatic source-SHA + executable-SHA256 verification before gameplay waits, bounded semantic milestones, and per-target artifacts.
- Release-tier discovery is structural: ordinary production diffs use smoke targets; `<Module>/Validation/Release/` targets run when changed and from the generic scheduled/manual release workflow.
- Exact harness/tooling proof: feature `496e3f9d7a88658029aa332b3596caf86e1cabb2`, request `bbb63019d00a93b44cff24c7a2b20d7ae12e461e`, run `33936350595`.
- Exact build-identity proof: feature `73c3bafd0268fcc80c453d33900f6848e7571153`, request `71b21322f8d3bb776553e5d915bc10d2c0664695`, run `33937957149`; player SHA-256 `8129fbc54fb83c5ac219204df3c9fd2c7482d9c1225daa87c67447cec9db27ae`, `identityVerified: true` before gameplay acceptance.
- **T25-050 complete:** static audit confirms the System25 harness only controls production player processes, identity, state isolation, semantic waits, and logs; it contains no direct socket injection, alternate transport, gameplay mutation authority, or privileged command seam.
- **External prerequisite:** `origin/master` `939e9a6f744313d93992b0479d5f6140d774ef42` still has System24 open. `fixes/agent-2` is `abdeb112c5f1c808ba9562304bb6f5e19d8c6b38`; T24-032 remains unchecked. System25 must not copy or substitute that unmerged Kentridge production composition/diagnostic boundary.

## Selected work / remaining gates

1. After System24 lands, merge current master and reuse its production Kentridge entry/read-only diagnostic seam.
2. Add authority + two-client smoke validation and release scenarios under the owning Kentridge validation surface.
3. Prove contention/conservation, vitality, progression, interruption/reconnect, explicit leave, capacity/JIP/repeated reconnect, and persisted rehost on the exact built SHA.
4. Run repository-selected exact-SHA validation, inspect durable artifacts, complete closure fields/move, merge latest master, then PR + auto-merge and required `affected` gate.

**Current branch:** docs/audit head begins at `ce15089f22f6edfd3fc34b76913cd0db735e4d51`; current verified product implementation remains `73c3bafd0268fcc80c453d33900f6848e7571153`.

**Blast radius / cost:** validation tooling/workflows plus Kentridge validation composition only; production authority remains untouched. Smoke is authority + two clients; expensive cases stay in release targets.
