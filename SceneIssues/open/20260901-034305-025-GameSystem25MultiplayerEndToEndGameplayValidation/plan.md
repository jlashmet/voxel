# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build, separate authority/client processes, production formation/entry, identity and baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every required tasks.md item remains binding.

**Ownership:** shared validation infrastructure and Kentridge composition; dependencies 06/07/08/14 plus authoritative gameplay modules. Application/Sessions/GameplayReplication/Continuity retain ownership. No parallel authority, fake networking, direct socket injection, or privileged scenario mutation. System24 is related work, not a prerequisite.

## Current state / next discriminator

- Feature head before this planning update: `ce141db1517eed0fe0b08e9dc4b445f6a654310b`. T25-010A implementation is `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`; 17 authored unit cases and Application player assertions exist but are not validated yet.
- **H1:** an Active party with a locally unsynchronized member causes premature graph composition. Inspection found `GameSessionOrchestrator.Prepare` fails with `BindingsNotReady` when graph bindings are false; the current Application fixture permits Prepare before readiness, masking this boundary mismatch.
- **H2:** readiness can instead drop after successful composition/new-game initialization; Application must retain its existing StartingSession -> GameplayReady wait for that distinct case.
- **Next experiment:** module-owned Application regression using the real `GameSessionOrchestrator` with bounded readiness inputs. Distinguish unready-before-Prepare from unready-after-EnterRunning; verify no early composition, one startup after local synchronization, and no Start command from clients. Also reject disconnected/expired/left local membership as a startup signal.
- Selected narrow correction, subject to that discriminator: observe Active plus matching connected, gameplay-ready local membership before planning; still let Orchestration validate bindings and final readiness. Replace only the joined-start portion of Application player validation with production Orchestration, retaining unrelated validation behavior. Module root: `Assets/Game/Application`; owned scene/scenario: `Validation/ApplicationFrontendValidation.*`. This is lifecycle boundary proof, not network topology or visual-finish evidence.
- T25-010 remains substantive: Kentridge always composes campaign authority locally; the inspected UTP EVENT dispatcher lacks admission/party-intent routing. Implement real production integration rather than generating a separate client authority.

## CI / remaining gates

Exact request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3` (sole parent/source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`), run `34002524305`, job `101403824713`, remains **queued**. Do not replace it while queued/running. Further source changes require their own later exact-SHA validation; this request cannot prove them. Prior diagnostics run `33995470352` succeeded; historical harness evidence is in tasks.md.

Local Git fetch again failed DNS resolution. Connected GitHub reads/writes work; refreshed master is `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

Complete T25-010A validation, production host/two-client topology, gameplay/recovery cases and Release scenarios. Keep all unproven checkboxes open. Only after all required gates pass: close this issue, merge current master, PR + auto-merge, verify required affected gate and closed issue on master.

**Cost:** Application lifecycle/tests/player validation only for the immediate correction. One attempt per successful formation; unchanged timing/capture budgets. Network smoke is authority plus two clients; expensive cases remain release-tier.
