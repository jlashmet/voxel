# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, interruption/reconnect identity continuity, explicit leave, configured capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus Kentridge playable-composition validation. No gameplay authority, alternate transport, or test-only networking runtime. Production semantics remain in `SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; validation drives public player/session inputs and observes read-only semantic truth.

## Observed state / hypotheses

- **H1:** System25 is integration validation over existing production systems. **Supported.** `ISessionFormationService` owns semantic Host/Join; `IPartySessionQuery` exposes durable `GameSessionId`/`PartyMemberId`/`PlayerSlot`/`CharacterId`; `SessionNetworkAdmissionAdapter` binds transient connections from stable slots; `ContinuityCoordinator` rebinds a new runtime connection to the same member and gates recovery on replicated `GameplayReady` revision.
- **H2:** a parallel test networking/runtime layer is needed. **Rejected.** Existing public seams already represent formation, identity, reconnect, readiness, and explicit leave.
- Shared `mode: multiProcess` supports build-once role launch/wait/kill/relaunch, isolated per-role writable state, role/attempt logs, and automatic source-SHA + executable-SHA256 verification before gameplay waits. Harness-owned identity/state/log/run controls cannot be overridden by scenarios. `tests-single.yml` forwards authoritative `HEAD^` into module player validation.
- Player artifacts are isolated per scene/scenario target; multiple smoke/release validations from the same owning module cannot overwrite one another, and each artifact root is recorded in the module-validation summary.
- Binding acceptance requires expensive capacity/JIP/reconnect/rehost coverage outside normal PR smoke. The repository had no slower player lane, so tooling treats `<Module>/Validation/Release/` as a structural release tier: ordinary production diffs exclude it, edits to a release target include it in exact-SHA targeted CI, and generic `player-validation-release.yml` discovers all release targets on schedule/manual dispatch without registration lists.
- **Independent exact-SHA proof:** feature `496e3f9d7a88658029aa332b3596caf86e1cabb2` passed targeted request `bbb63019d00a93b44cff24c7a2b20d7ae12e461e` (run `33936350595`), including all Python/tool regressions for process orchestration, source-SHA propagation, release-tier selection, and per-scenario artifact isolation. No Unity module/player work was selected because this tranche changes validation tooling only.
- **External prerequisite:** System24 still exists only on `fixes/agent-2` and has no PR; current `origin/master` still contains its open baseline. System25 must not copy or substitute its unmerged production composition/diagnostic boundary.

## Selected work

1. Independent harness/release-tier infrastructure is implemented and exact-SHA verified.
2. After System24 lands, merge current master and reuse its production Kentridge entry/read-only diagnostic seam.
3. Add smoke multiplayer validation under Kentridge playable composition (authority + two clients) and release coverage under its `Validation/Release/` subtree.
4. Prove contention/conservation, vitality, progression, interruption/reconnect, explicit leave, capacity/JIP/repeated reconnect, and persisted rehost on exact built SHA.

**Last exact-SHA-verified feature SHA:** `496e3f9d7a88658029aa332b3596caf86e1cabb2`.

**Blast radius / cost:** independent changes are validation tooling/workflows only; production authority is untouched. Normal PR remains authority + two-client smoke; expensive release targets run only when changed for exact-SHA proof and in the scheduled release lane.

**Remaining gates:** System24 prerequisite; smoke/release production scenarios; all semantic acceptance cases; durable built-player artifacts; final exact-SHA proof; closure fields/move; latest-master merge; PR `affected` gate + auto-merge.
