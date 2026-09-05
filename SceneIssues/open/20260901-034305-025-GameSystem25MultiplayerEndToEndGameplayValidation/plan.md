# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate OS processes: formation/entry, authoritative shared gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persisted rehost, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. Production authority stays in `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; validation drives public inputs and observes semantic read state. No alternate transport, gameplay mutation seam, or test-only networking runtime.

## Observed state / hypotheses

- **H1 supported:** System25 is integration validation over existing production systems. `ISessionFormationService`, `IPartySessionQuery`, gameplay-replication read state, and Continuity already cover formation, durable identity, readiness/current state, reconnect, and leave.
- **H2 rejected:** no parallel networking/runtime layer is required.
- Multi-process harness work is complete: build-once roles, kill/relaunch, attempt-scoped ephemeral state with durable role state, source/executable identity verification, monotonic semantic waits, harness-owned role/attempt attribution, and durable artifacts.
- Exact harness correctness: feature `b2d1847ec109eb8be7a631c084bd60230c5932a2` / request `382b9c33a2f4edabd7bf46c598e1ee1d9eea0291` / run `33986100313`; attribution feature `1727641603ba4645a798b3ca246bc8d9130afb95` / request `06585b896b23c38a7c929dd3294211c68ca494a2` / run `33987833161`.
- Current-master compatibility is proven: merge `72fe27a85f458b047d21f1f2325ff768619bca90` incorporated `origin/master` `3654c13f72ed157c53b340443a766795d772f596`; exact feature `94ab660260d9b066f030f702500aba092b321a98` passed request `4625c82ab1710411723aa4afdecc646826f8fe51` in run `33992072202`.
- **External prerequisite blocked:** System24 remains open/unmerged. Its owner has implemented compile disambiguations and player-input-driven combat through `e724343d0f2a2d05631e1305f344ba94f832e94f`, but T24-032 read-only diagnostic plus representative gameplay/save/built-player gates and replacement exact-SHA CI are still incomplete. System25 must not copy or substitute that unmerged production composition.

## Selected work / remaining gates

1. Keep the proven harness stable; do not add speculative infrastructure.
2. Wait for System24 to close and merge through its own workflow.
3. Merge resulting current master, then implement T25-007 by aggregating System24's read-only production surface with roster, replication, and Continuity read APIs.
4. Add Kentridge authority + two-client smoke proof for identity, baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, and explicit leave.
5. Add `Validation/Release/` coverage for capacity, join-in-progress, repeated reconnect, and persisted rehost.
6. Run final exact-SHA built-player validation, complete every task/acceptance item, move `open -> closed`, merge latest master, then promote only by PR + auto-merge and required `affected` gate.

**Blast radius / cost:** validation tooling/workflows and Kentridge validation composition only. Smoke is authority + two clients; expensive cases stay release-tier.
