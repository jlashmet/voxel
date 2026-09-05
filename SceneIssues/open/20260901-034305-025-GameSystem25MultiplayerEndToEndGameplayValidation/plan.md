# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate OS processes: formation/entry, authoritative shared gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persisted rehost, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. Binding dependencies are systems 06/07/08/14, representative authoritative gameplay modules, and shared validation architecture. Production authority stays in Application/Sessions/GameplayReplication/Continuity and domain owners; validation drives public inputs and observes immutable semantic read state. No alternate transport, direct socket attachment, gameplay mutation seam, or test-only networking runtime.

## Observed state / hypotheses

- **H1 supported:** System25 can compose existing production public surfaces. `ApplicationFlowCoordinator` routes Host/Join through `ISessionFormationService`; Sessions owns durable member/slot/character identity; `SessionNetworkAdmissionAdapter` binds transient network actors; replication and Continuity expose semantic current/recovery state.
- **H2 rejected:** System24 is related integration work, not a binding dependency. Earlier plan text that blocked System25 on System24 was too strict and is removed.
- Current Kentridge playable composition directly creates `GameSessionOrchestrator` and a local session identity, bypassing Application/formation. T25-010 must replace that multiplayer entry behavior with the production Application + Sessions/provider path while preserving domain authority.
- Multi-process harness work is complete: build-once roles, kill/relaunch, attempt-scoped ephemeral state with durable role state, source/executable identity verification, monotonic semantic waits, harness-owned attribution, and durable artifacts.
- Exact harness correctness is green in runs `33986100313` and `33987833161`. Prior-master compatibility feature `94ab660260d9b066f030f702500aba092b321a98` passed exact request `4625c82ab1710411723aa4afdecc646826f8fe51` in run `33992072202`.
- Current `origin/master` is `cd77b927dbe463171f6cef86bb268a31ae8df4e4`; its changes since the last merged master are unrelated to System25 surfaces, so another compatibility merge is not required yet.

## Selected work / remaining gates

1. Implement T25-007 as a System25 read-only semantic diagnostic aggregate over roster identity, replication revision/readiness/current projections, and Continuity recovery. Copy immutable values only; expose no transport handles or mutation methods.
2. Implement T25-010/011 through production Application + `ISessionFormationService` and real provider/UTP/session admission, then run authority + two clients as separate processes from one exact build.
3. Prove identity/baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, and explicit leave.
4. Add structural `Validation/Release/` coverage for configured capacity, join-in-progress, repeated reconnect, and persisted rehost.
5. Run final exact-SHA built-player validation, complete every task/acceptance item, move `open -> closed`, merge latest master, then promote only by PR + auto-merge and required `affected` gate.

**Blast radius / cost:** validation tooling plus the narrow production Kentridge multiplayer composition boundary. Smoke is authority + two clients; expensive cases stay release-tier.
