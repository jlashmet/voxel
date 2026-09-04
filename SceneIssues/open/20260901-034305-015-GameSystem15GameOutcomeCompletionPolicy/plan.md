# 15 Game outcome & completion policy — implementation plan

**Target module:** `Assets/Game/Outcomes/Api` / `Runtime` (`Game.Outcomes.Api`, `Game.Outcomes.Runtime`).
**Current baseline:** `origin/master` / `fixes/agent-8` at `e27afc78bb47c2578fbd6b85d1604d588d78d854`.

## Observed behavior / acceptance

- `Game.Outcomes.Api` already provides `OutcomeRef`, `Running`/`Resolved`, disposition, snapshot, and `IGameOutcomeQuery`; GameplayReplication already projects that query.
- No `Game.Outcomes.Runtime` exists yet.
- `CombatService` owns battle-local completion and `WinningTeam`; it does not create global game outcome state. `CampaignRuntime`/Story own progression, quest, cutscene, party, and spell effects; they do not currently terminate the game.
- Repository search found no demonstrated direct `Application.Quit`/global game-over owner to migrate. The new runtime must therefore not infer game completion from combat defeat, encounter completion, scene identity, or technical shutdown.

Acceptance remains: one immutable terminal gameplay result, authored/configured authority only, deterministic duplicate/late handling, exactly-once resolution notification, snapshot/restore and replication seams, ordinary losses and technical shutdown nonterminal.

## Architecture / chosen approach

Extend the engine-neutral Outcomes API with semantic authority/resolution identities, request/result contracts, richer snapshot identity, and `GameOutcomeResolved` notification. Add `Game.Outcomes.Runtime` as a `noEngineReferences` authority that:

1. starts Running and accepts only configured `OutcomeAuthorityRef` sources;
2. commits the first accepted request in authoritative call order and never replaces it;
3. treats the winning request idempotently and rejects later competing requests;
4. restores snapshots without replaying historical resolution events;
5. exposes an API-level event/query seam so System 14 can observe resolution without Outcomes performing shutdown/UI/save work.

Add a small config-driven policy router from semantic `OutcomeConditionRef` values to authored resolution requests. Unmapped facts are inert, proving combat/defeat/shutdown are nonterminal unless composition configures them.

## Hypotheses / discriminator

- **H1 selected:** existing local combat/Story state can remain untouched; a new Outcomes runtime plus semantic policy seam is sufficient because no current global terminal owner exists.
- **H2 falsified by inventory:** an existing implicit game-over path must be migrated. Combat has only battle-local winner state, Campaign/Story have progression effects, and no direct technical shutdown-to-outcome path was found.

## Validation ownership / blast radius

Affected production assemblies are `Game.Outcomes.Api` and new `Game.Outcomes.Runtime`; both are pure headless/domain code with no meaningful scene behavior. Per repository rules, no module-local Unity validation scene applies. Proof will live in module-owned `Assets/Game/Outcomes/Tests` EditMode coverage plus repository-selected dependent and PR gates. Runtime work is O(1) per resolution request; policy dispatch is linear in the small configured rule list and creates no per-frame work.

## Remaining gates

Implement contracts/runtime/policy, add focused regressions for success/failure/nonterminal/duplicate/restore behavior, complete boundary audit, run exact-SHA targeted CI, then close and promote through PR + auto-merge.
