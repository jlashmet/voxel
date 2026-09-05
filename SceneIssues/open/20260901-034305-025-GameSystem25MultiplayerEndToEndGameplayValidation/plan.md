# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. No gameplay authority, alternate transport, or test-only networking runtime. Production semantics remain in `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; validation drives public player/session inputs and observes read-only semantic truth.

## Observed state / hypotheses

- **H1:** System25 is integration validation over existing production systems. **Supported.** `ISessionFormationService` owns semantic Host/Join; `IPartySessionQuery` exposes durable `GameSessionId`/`PartyMemberId`/`PlayerSlot`/`CharacterId`; `SessionNetworkAdmissionAdapter` binds transient connections from stable slots; `ContinuityCoordinator` rebinds a new runtime connection to the same member and gates recovery on replicated `GameplayReady` revision.
- **H2:** a parallel test networking/runtime layer is needed. **Rejected.** Existing public seams already represent formation, identity, reconnect, readiness, and explicit leave.
- Shared `mode: multiProcess` supports build-once role launch/wait/kill/relaunch, isolated per-role writable state, role/attempt logs, and automatic source-SHA + executable-SHA256 verification before gameplay waits. Harness-owned identity/state/log/run controls cannot be overridden by scenarios. `tests-single.yml` forwards authoritative `HEAD^` into module player validation.
- Player artifacts are isolated per scene/scenario target; multiple smoke/release validations from the same owning module cannot overwrite one another, and each artifact root is recorded in the module-validation summary.
- Binding acceptance requires expensive capacity/JIP/reconnect/rehost coverage outside normal PR smoke. Tooling treats `<Module>/Validation/Release/` as a structural release tier: ordinary production diffs exclude it, edits to a release target include it in exact-SHA targeted CI, and generic `player-validation-release.yml` discovers all release targets on schedule/manual dispatch without registration lists.
- **Independent tooling proof:** feature `496e3f9d7a88658029aa332b3596caf86e1cabb2` passed targeted request `bbb63019d00a93b44cff24c7a2b20d7ae12e461e` (run `33936350595`) for process orchestration, source-SHA propagation, release-tier selection, and per-scenario artifact isolation.
- **Build-identity proof complete:** exact feature `73c3bafd0268fcc80c453d33900f6848e7571153` passed request `71b21322f8d3bb776553e5d915bc10d2c0664695` (run `33937957149`). The player self-computed executable SHA-256 `8129fbc54fb83c5ac219204df3c9fd2c7482d9c1225daa87c67447cec9db27ae`, reported the exact source SHA, the harness recorded `identityVerified: true`, and only then accepted `encounter-realization-ready` with three participants. Prior request `c8993bf9615705ccadf69a2904d871dd679f2fb7` failed because macOS `Application.dataPath` was resolved one bundle level incorrectly; artifact evidence identified that exact cause before the fix.
- **External prerequisite:** blocker review against `origin/master` `51797c954490425964e602d6bb2252a0d7a7c5aa` found System24 still unmerged at `fixes/agent-2` `79d4c272954146ef2b06a9ac01a94f112ac4718f`, with no PR and T24-032 read-only diagnostics still unchecked. System24 advanced by integrating residency changes while preserving its Kentridge composition, but System25 must not copy or substitute that unmerged composition/diagnostic boundary.

## Selected work

1. Independent harness, release-tier, artifact-isolation, and exact built-player identity work is complete and exact-SHA verified.
2. After System24 lands, merge current master and reuse its production Kentridge entry/read-only diagnostic seam.
3. Add smoke multiplayer validation under Kentridge playable composition (authority + two clients) and release coverage under `Validation/Release/`.
4. Prove contention/conservation, vitality, progression, interruption/reconnect, explicit leave, capacity/JIP/repeated reconnect, and persisted rehost on exact built SHA.

**Current verified implementation:** `73c3bafd0268fcc80c453d33900f6848e7571153` via request `71b21322f8d3bb776553e5d915bc10d2c0664695`, run `33937957149`; docs-only branch head before this blocker refresh was `9843ee809e96657d1e9cafededa3eb0e1052d307`.

**Blast radius / cost:** validation tooling/workflows plus the owning Kentridge validation surface; production authority is untouched. Normal PR remains authority + two-client smoke; expensive release targets run only when changed for exact-SHA proof and in the scheduled release lane.

**Remaining gates:** System24 prerequisite; read-only composed multiplayer diagnostics; smoke/release production scenarios; all semantic acceptance cases; durable built-player artifacts; final exact-SHA proof; closure fields/move; latest-master merge; PR `affected` gate + auto-merge.
