# 26 Authored full-run campaign progression & completion — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** production campaign Story/Progression content under composition/content assemblies; reuse existing Story plus system 11 Progression. No generic GameLoop/Chapter runtime.
**Execution rule:** recover/author only evidence-backed progression. Gameplay produces facts, Progression evaluates goals, Story chooses consequences, system 15 commits terminal outcome.

## Evidence and route definition

- [x] **T26-001 — Inventory current production campaign content.** Mapped `KnownOpeningCampaignContent`, Story rules/events/effects, unified objectives/quests, cutscenes, NPC/site bindings, persistence snapshot, and the opening-only endpoint in `route-evidence.md`.
- [x] **T26-002 — Inventory recovered source evidence beyond opening.** Recorded normalized regions/sites and verified upstream positive dependency chains separately from inferred filename/quest-label guidance in `route-evidence.md`.
- [x] **T26-003 — Define one evidence-backed completion route.** Canonical opening -> church -> Rorik/Moordell/Rossdam/Logan-castle terminal spine is documented, with disconnected-component bridges explicitly labeled authored design rather than recovered chronology.
- [x] **T26-004 — Mark optional content.** Optional recovered branches are listed in `route-evidence.md` and are explicitly non-gating.
- [x] **T26-005 — Identify missing semantic vocabulary.** Existing site/NPC/cutscene/Progression semantics plus the owning Encounter resolution fact and system-15 outcome-condition seam cover the route; no generic chapter/game-loop vocabulary is needed.

## Campaign content decomposition

- [x] **T26-010 — Decompose `KnownOpeningCampaignContent.Build` responsibilities.** `KnownOpeningCampaignSlice` owns opening world/roles/objectives/cutscenes/rules while the public compatibility surface remains intact.
- [x] **T26-011 — Avoid premature chapter abstraction.** Opening and continuation are plain content helpers; no chapter/slice interface or runtime phase owner was introduced.
- [x] **T26-012 — Author/recover the next progression slice.** `RecoveredCampaignContinuationSlice` adds source-backed semantic sites/NPCs/objectives/cutscenes/encounters through owning APIs.
- [x] **T26-013 — Continue authored slices to terminal route.** The continuation advances deterministically through Rorik/Moordell/Rossdam/Logan facts to one terminal condition; optional branches remain non-gating.
- [x] **T26-014 — Keep geography separate from progression.** Site/NPC/encounter facts feed Story rules; there is no `CurrentChapter`, map-index increment, or phase counter.
- [x] **T26-015 — Extend Story event/condition vocabulary minimally.** Encounter completion is represented by `EncounterResolved` sourced from a resolved owning `EncounterSnapshot`; existing semantics cover all other route transitions.
- [x] **T26-016 — Extend Story effects minimally.** Terminal Story emits only `ObserveOutcomeCondition`; no damage/inventory/world/transport/presentation setter was added.
- [x] **T26-017 — Integrate system 11 unified Progression.** CampaignRuntime continues to project quest/standalone objective truth through `ProgressionRuntime` and its persisted snapshot; no campaign-local objective store was added.

## Terminal outcome / lifecycle

- [x] **T26-020 — Define authored terminal rule.** The Logan-hole completion rule emits the stable `campaign:logan-castle-lower-logan-hole-complete` outcome condition; no boss/scene flag directly resolves the run.
- [x] **T26-021 — Route terminal request through system 15.** Exact request `ea8df281d99daa58f695ce5a90267ed89136ffea`, source `eb35eec2fc0bc8a426dfb9118ac3292b444bb5f4`, run `34192079529` drove the production full-run player to `KENTRIDGE_FULL_RUN_OUTCOME disposition=Success outcome=main-campaign-complete revision=1`, proving the authored terminal condition resolves exactly once through System15.
- [x] **T26-022 — Integrate frontend aftermath.** The same exact standalone player routed the resolved System15 result through `ApplicationFlowCoordinator`, emitted `KENTRIDGE_FULL_RUN_APPLICATION_OUTCOME PASS`, and returned through normal frontend/session teardown before final validation PASS.
- [x] **T26-023 — Verify ordinary losses remain nonterminal unless authored.** `CampaignOutcomeIntegrationTests` proves an unresolved/failed encounter does not resolve System15; only the mapped authored condition does.

## Fast semantic route proof

- [x] **T26-030 — Build engine-independent canonical route test.** `CanonicalCampaignRouteTests` starts the production authored campaign at `NewGame` and drives public semantic runtime/domain facts to terminal outcome.
- [x] **T26-031 — Prohibit privileged progression shortcuts in the test.** The route uses CampaignRuntime public facts, real cutscene completion ticks and the production `EncounterRegistry`; no direct completion/grant/outcome/private mutation path is used.
- [x] **T26-032 — Assert every route milestone.** The test records opening and multiple later objective/cutscene/encounter milestones and asserts exactly one immutable `GameOutcomeResolved`.
- [x] **T26-033 — Add dead-end regression.** The canonical driver reports the last semantic milestone when a required authored transition cannot advance.
- [x] **T26-034 — Verify optional content does not gate canonical completion.** The optional well quest is deliberately left incomplete while the canonical route still reaches the terminal outcome.

## Persistence / multiplayer / built-player proof

- [x] **T26-040 — Choose a meaningful mid-run restore point.** The canonical route captures after multiple post-opening consequences with unified Progression/cutscene/party/spell truth present.
- [x] **T26-041 — Restore through systems 16/14.** `KentridgeSessionPersistenceBridge` routes campaign semantic capture/restore through System16 `SessionPersistenceService`; exact-SHA run `33941421358` passed fresh-graph resume coverage.
- [x] **T26-042 — Continue canonical route after restore.** The same exact-SHA regression proves restored current progression and completed one-shot history in a distinct System14 graph without replaying `NewGame` or historical cutscenes.
- [ ] **T26-043 — Verify shared multiplayer progression/outcome.** **BLOCKED external prerequisite:** System25 owns production Sessions/provider admission, authority plus two separate clients, identity/baseline convergence, gameplay contention/conservation, combat/vitality, shared progression, reconnect/current-state recovery, explicit leave and release proof. Its authoritative checklist still has those separate-process acceptance items incomplete. Do not create an alternate System26 transport/process harness or duplicate campaign authority in clients.
- [x] **T26-044 — Add canonical built-player full-run scenario.** `KentridgeFullRunCampaignValidation.unity` exact standalone run `34192079529` entered through the shipped full-run bootstrap and reached every opening -> Rorik/Moordell/Rossdam/Logan -> terminal System15/frontend milestone plus `KENTRIDGE_FULL_RUN_VALIDATION PASS`.
- [x] **T26-045 — Make full-run scenario milestone-driven.** The paired scenario passed by observing production semantic milestone log assertions; no correctness sleep or private progression setter was introduced.
- [ ] **T26-046 — Classify full-run validation appropriately.** Fast semantic route remains module/domain coverage; the dedicated standalone player is the slow feature proof and the normal Kentridge player is the shipped integration consumer. Keep unchecked until the remaining automatic WorldBuilder and normal Kentridge player targets pass and evidence is reviewed.
- [x] **T26-058 — Wire the authored full campaign into the production Kentridge player through hierarchy-aware real-world realization.** `KentridgePlayableSlice` boots `AuthoredFullRunCampaignContent` through `KentridgePlayableFullRunBootstrap`; exact rich opening identities overlay the hierarchy-aware full realization while continuation geography comes from the recovered `WorldHierarchyPlan`/`TopDownWorldPhysicalPlan`. Exact standalone run `34192079529` reached `physical-world-ready settlements=11 npcs=12` and completed the entire authored route through System15/frontend.
- [ ] **T26-059 — Add WorldBuilder-owned top-down physical-world built-player validation.** WorldBuilder's player-visible macro planner/reservation/terrain/roads/towns/water realization is exercised by `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/` through the production physical planner, reservation adapter, voxel/water catalogues, `ShowcaseWorld`, streaming and rendering composition. Exact r25 run `34192079529` passed catalogue validation plus Moordell and Rossdam publication coverage, then the water survey retained roughly 595-629 visible missing-solid chunks until harness end. Root cause was validation geometry: it rendered the full two-region streamed radius while the offset camera could expose a third unstreamed region near a boundary. The validation now reserves one full 51.2 m region as residency safety margin while retaining semantic generated-region + published-coverage readiness. Keep unchecked until exact standalone water and final PASS.

## Cleanup / close

- [x] **T26-050 — Search for parallel progression/game-loop state.** Repository and feature-diff audit found no `CurrentChapter`, `CurrentPhase`, generic campaign phase counter, final-boss completion authority, or campaign-local objective store; System11 Progression remains the objective authority.
- [x] **T26-051 — Search Story effects for domain-god operations.** Story event/effect/runtime audit found only semantic objective/quest/cutscene/party/spell/outcome-condition coordination; no direct vitality, inventory, world, transport, scene-load, or presentation mutation path exists.
- [x] **T26-052 — Verify recovered-map ordering claims.** Pinned MountingForce progression evidence directly verifies each claimed hard edge; church->Rorik and mayor-lead->Logan remain explicitly labeled authored bridges, and filenames/inferred quest labels are not treated as chronology.
- [x] **T26-055 — Declare direct assembly dependencies exposed by new public seams.** Repeated exact-SHA compiler failures were reduced to explicit asmdef boundaries; Kentridge tests reference Campaign/Persistence API contracts directly, and affected WorldBuilder/Playable/top-level consumers reference System15 Outcomes directly instead of relying on transitive references.
- [x] **T26-056 — Give Story its owning module validation surface.** Added `Game.Story.Tests` headless EditMode coverage for completed-vs-failed `EncounterResolved` matching and `ObserveOutcomeCondition` dispatch; exact-SHA proof exists.
- [x] **T26-057 — Make existing System26-owned suites discoverable by the PR selector.** Exact request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, source `e31528947add430f39588a7d3fda98db40589974`, run `34008635270` passed `Game.Composition.Kentridge.Tests`, `Game.Story.Tests`, and selector regressions; see `ci-evidence-0498efba.md`.
- [ ] **T26-053 — Run automatic domain/campaign tests plus built-player full-run gate.** Earlier requests repaired stale test and T26-059 compile-boundary defects. Exact r25 request `ea8df281d99daa58f695ce5a90267ed89136ffea`, source `eb35eec2fc0bc8a426dfb9118ac3292b444bb5f4`, run `34192079529` passed the complete dedicated Kentridge full-run player, then failed independently at T26-059 water publication coverage after Moordell/Rossdam passed. Final gate still requires a fresh exact-SHA automatic run with T26-059 water/final PASS and normal Kentridge integration, plus T26-043's external multiplayer evidence.
- [ ] **T26-054 — Close with end-to-end semantic proof.** Closure requires all remaining exact player/domain evidence, authoritative System25 multiplayer progression/outcome proof, completed metadata, open->closed move, and final PR + auto-merge `affected` gate.
