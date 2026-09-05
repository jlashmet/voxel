# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** prove the production packaged-player multiplayer loop with separate processes: formation/entry, shared authoritative gameplay, rejection diagnostics, interruption/reconnect identity continuity, explicit leave, configured-capacity/join-in-progress/repeated reconnect, persistence/rehost policy, and durable exact-SHA evidence.

**Ownership / architecture:** shared built-player validation infrastructure plus a module-owned multiplayer scenario. No new gameplay authority, transport, or test-only networking runtime. The production seams are split across `Assets/Game/SessionOrchestration`, `Sessions`, `GameplayReplication`, and `Continuity`; the original `Assets/Scripts/Networking` ownership guess was false. Scenario orchestration may launch/stop processes and wait on read-only semantic milestones, but gameplay mutations must enter through production public input/session APIs.

## Observed state and hypotheses

- Shared module validation discovers paired `<Module>/Validation/*.unity` + `*.player-scenario.json` automatically and executes them through `tools/player-validation.py`.
- `tools/player_process_orchestrator.py` already built once, isolated per-role writable state, captured role logs, verified source/binary identity, and waited on semantic milestones, but was not connected to `player-validation.py` and could not sequence reconnect/join-in-progress lifecycles.
- **H1:** System25 mainly needs integration validation over existing production systems. **Supported** for process orchestration and semantic contracts.
- **H2:** a parallel test networking/runtime layer is needed. **Rejected** by repo architecture and existing Sessions/GameplayReplication/Continuity APIs.
- Discriminating prerequisite: the production composed built-player entry from System24 must exist before the multiplayer scene can honestly drive the full gameplay loop. On current master, `20260901-034305-024-GameSystem24ProductionComposedBuiltPlayerVerticalSlice` remains open, so full end-to-end acceptance is externally blocked.

## Selected work

1. Route `mode: multiProcess` through the canonical player-validation entrypoint.
2. Keep role/process lifecycle generic and deterministic: launch, bounded wait, terminate/kill, relaunch; preserve isolated durable role state across transport attempts.
3. Prove harness behavior with Python regressions and exact-SHA CI.
4. When System24 lands on master, merge it, create/update the correct module-owned production validation scene/scenario, and complete gameplay/reconnect/rehost evidence without duplicating composition.

**Current feature SHA:** `86446087feed82ed775a03349e7fe362833eab40`.

**Remaining gates:** tool regressions; System24 production entry prerequisite; module-local standalone multiplayer scenario; all gameplay/continuity/rejection/capacity/rehost checks; exact-SHA built-player evidence; closure bookkeeping; latest-master merge; PR `affected` gate + auto-merge.