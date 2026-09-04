# 15 Game outcome & completion policy — implementation plan

**Target module:** `Assets/Game/Outcomes/Api` / `Runtime` (`Game.Outcomes.Api`, `Game.Outcomes.Runtime`).
**Implementation baseline:** `origin/master` / `fixes/agent-8` at `e27afc78bb47c2578fbd6b85d1604d588d78d854`; final promotion reconciles the current protected `master` only after assignment-specific exact-SHA validation and closure.

## Observed behavior / acceptance

- `Game.Outcomes.Api` already provided `OutcomeRef`, `Running`/`Resolved`, disposition, snapshot, and `IGameOutcomeQuery`; GameplayReplication already projected that query.
- No `Game.Outcomes.Runtime` existed before this assignment.
- `CombatService` owns battle-local completion and `WinningTeam`; it does not create global game outcome state. `CampaignRuntime`/Story own progression, quest, cutscene, party, and spell effects; they do not terminate the game.
- Repository inventory found no demonstrated direct `Application.Quit`/global game-over owner to migrate. The Outcomes runtime therefore does not infer game completion from combat defeat, encounter completion, scene identity, or technical shutdown.

Acceptance is satisfied by one immutable terminal gameplay result, authored/configured authority only, deterministic duplicate/late handling, exactly-once resolution notification, snapshot/restore and replication seams, and regressions proving ordinary losses and technical shutdown remain nonterminal.

## Architecture / chosen approach

The engine-neutral Outcomes API now carries semantic authority/resolution identities, request/result contracts, coherent snapshot identity, and `GameOutcomeResolved` notification. `Game.Outcomes.Runtime` is a `noEngineReferences` authority that:

1. starts `Running` and accepts only configured `OutcomeAuthorityRef` sources;
2. commits the first accepted request in authoritative call order and never replaces it;
3. treats the winning request idempotently and rejects later competing requests;
4. restores snapshots without replaying historical resolution events;
5. exposes an API-level event/query seam so System 14/downstream composition can observe resolution without Outcomes performing shutdown/UI/save work.

A config-driven `OutcomePolicyRouter` maps semantic `OutcomeConditionRef` values to authored resolution requests. Unmapped facts are inert, proving combat/defeat/shutdown are nonterminal unless composition explicitly configures them.

## Hypotheses / discriminator

- **H1 selected:** existing local combat/Story state can remain untouched; a new Outcomes runtime plus semantic policy seam is sufficient because no current global terminal owner exists.
- **H2 falsified by inventory:** an existing implicit game-over path must be migrated. Combat has only battle-local winner state, Campaign/Story have progression effects, and no direct technical shutdown-to-outcome path was found.

## Validation ownership / blast radius

Affected production assemblies are `Game.Outcomes.Api` and `Game.Outcomes.Runtime`; both are pure headless/domain code with no meaningful scene behavior. Per repository rules, no module-local Unity validation scene applies to Outcomes itself. Module-owned proof lives under `Assets/Game/Outcomes/Tests/EditMode`, while repository dependency selection also validates dependent modules and the canonical integration player. Runtime work is O(1) per resolution request; policy dispatch is linear in the small configured rule list and creates no per-frame work.

## Final validation

- Initial exact-SHA workflow run `33834559402` exposed a real asset-identity defect after relocating Outcomes tests: two moved `.meta` files contained invalid 40-character Git blob SHAs instead of Unity GUIDs. The original valid GUIDs were restored; no acceptance was weakened.
- Replacement exact-SHA request `6f91e7e36484e8bcd4e2f5fcccb6030f4cf0bddb` validated corrected feature SHA `b9d11d54aff204d71b0bea94dc2dd583883a342b` in workflow run `33839483224`.
- Repository-selected EditMode validation passed 32/32 tests with no skips or failures: `Game.Continuity.Tests` 7/7, `Game.GameplayReplication.Tests` 14/14, `Game.Outcomes.Tests` 11/11.
- The canonical `Assets/Scenes/KentridgePlayableSlice.unity` integration player validation completed with 0 assertion failures.
- Total selected validation time was 204.71 seconds, below the repository five-minute targeted-CI budget.
- Final behavioral proof covers nonterminal combat loss, authored success/failure, unauthorized/competing/duplicate requests, technical shutdown, deterministic first-rule ordering, exactly-once notification, and snapshot restore without replay.

All assignment acceptance, cleanup, reuse/boundary, regression, validation, and cost criteria are complete. The SceneIssue can close directly from `open/` to `closed/`, after which the closed feature branch is reconciled with current `master` and promoted only through PR + auto-merge.
