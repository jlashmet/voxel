# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate OS processes: formation/entry, authoritative shared gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persisted rehost, and durable exact-SHA evidence.

**Ownership:** shared validation infrastructure and Kentridge playable composition. Dependencies are systems 06/07/08/14, authoritative gameplay modules, and shared validation architecture. Application/Sessions/GameplayReplication/Continuity and domain owners retain authority. Validation drives public inputs and immutable diagnostics; no alternate transport, socket injection, privileged gameplay mutation, or test-only networking runtime.

## Observations / implementation

- **H1 supported:** production public interfaces provide the ownership boundaries, but concrete cross-process composition still requires implementation. **H2 rejected:** System24 is related integration, not a prerequisite; do not wait for or copy it.
- Kentridge directly prepares a local `GameSessionOrchestrator`. `KentridgeSessionRuntimeGraphFactory.Compose` always creates a campaign authority. T25-010 must not reproduce that authority independently in clients.
- **T25-010A defect and fix:** Sessions Start is leader-only; Application previously had no joined-client transition when the leader started. Feature `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a` observes the existing Active party projection, requires the formed session and local member, consumes one startup attempt, and delegates readiness to Orchestration. It never sends client Start or grants GameplayReady. Failure does not cause per-frame retries; leave/rejoin resets the attempt.
- Added 17 authored NUnit cases in `ApplicationJoinedPartyStartupTests` covering waiting/active parties, stale/missing identities, readiness, single startup, failures, leave/rejoin and explicit host start. Extended the existing Application frontend player scenario with joined-start/leave assertions. These are Application boundary fixtures, not separate-process multiplayer evidence.
- Further T25-010 inspection: `Assets/VoxelEngine/Net/Runtime/Server/ClientEventPacketReceiver.cs` dispatches alteration, region hash/request and gameplay repair only; no session-admission or party-intent route is present there. `ServerNetworkRuntime`/`UtpServerHost` are real transport owners, and `SessionNetworkAdmissionAdapter` bridges authenticated durable membership to network actors. Implement the missing production integration rather than test sockets.

## Exact-SHA validation state

- Source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3`; run `34002524305`; job `101403824713`: **queued at last inspection; no pass or failure yet**. Preserve this request while queued/running. Its sole parent is the source commit; only `.github/test-request.json` differs. Filter: `Game.Application.Tests.ApplicationJoinedPartyStartupTests`.
- Prior diagnostics request `4fdb3400ef72c2095adcba6353f09c70724dd81a`, source `5c1256867bcc07278049595d171abf22e2bd1a33`, run `33995470352` is successful. Historical harness evidence remains in tasks.md; none proves the remaining gameplay scenarios.
- Local Git fetch fails because this environment cannot resolve GitHub. Connected GitHub reads/writes work. Last inspected master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; recheck before integration.

## Remaining execution

1. Inspect the existing queued request through completion; resolve failures without replacing an active run. T25-010A stays unchecked until behavioral and owning `Assets/Game/Application/Validation/ApplicationFrontendValidation.unity` player gates are proven.
2. Implement T25-010/011 through `RequestHost`/`RequestJoin`, real provider/UTP/admission, and authority plus two clients from one build. Continue independent topology work while CI is queued.
3. Complete identity/baseline, contention/conservation, combat/progression, reconnect/current-state recovery, explicit leave, and structural `Validation/Release/` capacity/JIP/repeated-reconnect/persisted-rehost cases.
4. Complete every task and exact-SHA gate, then move open to closed, merge latest master, and promote only by PR plus auto-merge and required `affected` gate. No closure or promotion yet.

**Cost / blast radius:** current code changes only the Application module and its tests/player validation. One startup attempt per successful formation; existing player timing/capture budgets unchanged. Multiplayer smoke remains authority plus two clients; expensive cases remain release-tier.
