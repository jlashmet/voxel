# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate OS processes: formation/entry, authoritative shared gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persisted rehost, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. Binding dependencies are systems 06/07/08/14, representative authoritative gameplay modules, and shared validation architecture. Production authority stays in Application/Sessions/GameplayReplication/Continuity and domain owners; validation drives public inputs and observes immutable semantic read state. No alternate transport, direct socket attachment, gameplay mutation seam, or test-only networking runtime.

## Observed state / hypotheses

- **H1 supported:** existing production public surfaces provide the ownership boundaries: Application routes Host/Join through `ISessionFormationService`; Sessions owns durable identity; `SessionNetworkAdmissionAdapter` binds transient network actors; replication/Continuity expose current/recovery state. Concrete composition still needs implementation and proof.
- **H2 rejected:** System24 is related integration, not a binding dependency. System25 must not wait for or copy it.
- Kentridge directly creates `GameSessionOrchestrator` and local identity. T25-010 must implement multiplayer entry through Application + Sessions/provider while preserving domain authority.
- **T25-010A required defect:** `PartySessionApplication.RequestStart` rejects non-leaders, while `ApplicationFlowCoordinator.Update` never observes an active party in FrontEnd. A formed client therefore has no normal transition into local orchestration after its leader starts. Use the existing semantic party projection; validate session/member identity, consume startup once, and retain Orchestration's GameplayReady gate. Do not send a client Start command or grant readiness.
- Harness foundation and immutable diagnostics are implemented. Latest verified request `4fdb3400ef72c2095adcba6353f09c70724dd81a` for feature `5c1256867bcc07278049595d171abf22e2bd1a33` completed successfully in run `33995470352`; no active request was replaced. Historical exact-SHA evidence remains in tasks.md.
- Repository reads resolved feature `7768e4bec1a92989fdb9e66d40e517b4c4be8fc7` and master `ef475182b866eabfe8e1d1a39c82bf7810a03f49`. Local Git fetch is unavailable because this execution environment cannot resolve GitHub; connected GitHub reads/writes are available. Recheck refs before promotion.

## Selected work / remaining gates

1. Implement T25-010A with behavioral regressions for waiting/active parties, foreign or missing identities, single startup, failure, leave and rejoin. Owning module: `Assets/Game/Application`; owned built-player scene: `Validation/ApplicationFrontendValidation.unity`, through its production Application coordinator/frontend view. This module proof is not separate-process multiplayer evidence.
2. Implement T25-010/011 via `RequestHost`/`RequestJoin`, `ISessionFormationService`, real provider/UTP/admission; run authority + two clients from one exact build.
3. Prove identity/baseline, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery and explicit leave. Add structural `Validation/Release/` capacity, join-in-progress, repeated reconnect and persisted rehost cases.
4. Run exact-SHA gates; complete every task and acceptance item; move open to closed; merge latest master; promote only by PR + auto-merge and required `affected` gate.

**Blast radius / cost:** narrow Application lifecycle fix, production Kentridge multiplayer composition and validation tooling. Startup must be bounded to one attempt per successful formation. Smoke remains authority + two clients; expensive cases stay release-tier.
