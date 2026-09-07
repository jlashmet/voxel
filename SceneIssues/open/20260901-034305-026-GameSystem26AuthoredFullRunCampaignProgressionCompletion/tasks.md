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
- [ ] **T26-021 — Route terminal request through system 15.** Production full-run composition now owns the System15 observer/query seam and the dedicated built-player route drives the authored terminal condition; keep unchecked until exact-SHA player proof confirms exactly one success outcome.
- [ ] **T26-022 — Integrate frontend aftermath.** The dedicated full-run player validation routes the resolved System15 result through `ApplicationFlowCoordinator`, asserts `ApplicationScreen.Outcome`, then normal return-to-frontend teardown; keep unchecked until exact-SHA built-player proof.
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
- [ ] **T26-044 — Add canonical built-player full-run scenario.** `KentridgeFullRunCampaignValidation.unity` now enters through the shipped full-run bootstrap and drives opening -> Rorik/Moordell/Rossdam/Logan -> terminal System15/frontend milestones; keep unchecked until exact-SHA standalone-player proof.
- [ ] **T26-045 — Make full-run scenario milestone-driven.** The paired scenario uses bounded semantic milestone log assertions rather than correctness sleeps; keep unchecked until exact-SHA execution proves the scenario.
- [ ] **T26-046 — Classify full-run validation appropriately.** Fast semantic route remains module/domain coverage; the dedicated standalone player is the slow feature proof and the normal Kentridge player is the shipped integration consumer. Keep unchecked until both required exact player targets pass and evidence is reviewed.
- [ ] **T26-058 — Wire the authored full campaign into the production Kentridge player through hierarchy-aware real-world realization.** `KentridgePlayableSlice` now boots `AuthoredFullRunCampaignContent` through `KentridgePlayableFullRunBootstrap`; exact rich opening identities overlay the hierarchy-aware full realization while continuation geography comes from the recovered `WorldHierarchyPlan`/`TopDownWorldPhysicalPlan`. The shipped scene asserts `IsFullRun` and logs `KENTRIDGE_AUTHORED_FULL_RUN_READY`. Keep unchecked until exact behavioral/player validation passes.
- [ ] **T26-059 — Add WorldBuilder-owned top-down physical-world built-player validation.** WorldBuilder's player-visible macro planner/reservation/terrain/roads/towns/water realization was affected, while its existing `Validation/SecretDiscovery` scene does not exercise that path. Added `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/` using the production physical planner, reservation adapter, voxel/water catalogues, `ShowcaseWorld`, streaming and rendering composition with semantic Moordell/Rossdam/water milestones. Require automatic exact-SHA discovery and standalone-player PASS before checking.

## Cleanup / close

- [x] **T26-050 — Search for parallel progression/game-loop state.** Repository and feature-diff audit found no `CurrentChapter`, `CurrentPhase`, generic campaign phase counter, final-boss completion authority, or campaign-local objective store; System11 Progression remains the objective authority.
- [x] **T26-051 — Search Story effects for domain-god operations.** Story event/effect/runtime audit found only semantic objective/quest/cutscene/party/spell/outcome-condition coordination; no direct vitality, inventory, world, transport, scene-load, or presentation mutation path exists.
- [x] **T26-052 — Verify recovered-map ordering claims.** Pinned MountingForce progression evidence directly verifies each claimed hard edge; church->Rorik and mayor-lead->Logan remain explicitly labeled authored bridges, and filenames/inferred quest labels are not treated as chronology.
- [x] **T26-055 — Declare direct assembly dependencies exposed by new public seams.** Repeated exact-SHA compiler failures were reduced to explicit asmdef boundaries; Kentridge tests reference Campaign/Persistence API contracts directly, and affected WorldBuilder/Playable/top-level consumers reference System15 Outcomes directly instead of relying on transitive references.
- [x] **T26-056 — Give Story its owning module validation surface.** Added `Game.Story.Tests` headless EditMode coverage for completed-vs-failed `EncounterResolved` matching and `ObserveOutcomeCondition` dispatch; exact-SHA proof exists.
- [x] **T26-057 — Make existing System26-owned suites discoverable by the PR selector.** Exact request `0498efba7629b09f93cfc00a4c12fcdd8ecfa1ed`, source `e31528947add430f39588a7d3fda98db40589974`, run `34008635270` passed `Game.Composition.Kentridge.Tests`, `Game.Story.Tests`, and selector regressions; see `ci-evidence-0498efba.md`.
- [ ] **T26-053 — Run automatic domain/campaign tests plus built-player full-run gate.** Request `3796acf10f04fe266413f0956ebbd59e954336ad`, source `d6dd7a8100841b997b064afefc4d1e0fca19e333`, run `34164164923`, job `101871803067` failed before test execution on one stale test contract (`AuthoredTownPlan.BackendPlan`); fixed after artifact `10034721892`. Final gate now requires a new exact-SHA automatic run including Kentridge full-run, WorldBuilder T26-059 and normal Kentridge integration, plus T26-043's external multiplayer evidence.
- [ ] **T26-054 — Close with end-to-end semantic proof.** Closure requires all remaining exact player/domain evidence, authoritative System25 multiplayer progression/outcome proof, completed metadata, open->closed move, and final PR + auto-merge `affected` gate.
