# 26 Authored full-run campaign progression & completion — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** production campaign Story/Progression content under composition/content assemblies; reuse existing Story plus system 11 Progression. No generic GameLoop/Chapter runtime.
**Execution rule:** recover/author only evidence-backed progression. Gameplay produces facts, Progression evaluates goals, Story chooses consequences, system 15 commits terminal outcome.

## Evidence and route definition

- [x] **T26-001 — Inventory current production campaign content.** Mapped `KnownOpeningCampaignContent`, Story rules/events/effects, unified objectives/quests, cutscenes, NPC/site bindings, persistence snapshot, and the opening-only endpoint in `route-evidence.md`.
- [x] **T26-002 — Inventory recovered source evidence beyond opening.** Recorded normalized regions/sites and verified upstream positive dependency chains separately from inferred filename/quest-label guidance in `route-evidence.md`.
- [x] **T26-003 — Define one evidence-backed completion route.** Canonical opening -> church -> Rorik/Moordell/Rossdam -> Logan-castle terminal spine is documented, with disconnected-component bridges explicitly labeled authored design rather than recovered chronology.
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
- [ ] **T26-021 — Route terminal request through system 15.** CampaignRuntime and Kentridge session graph expose the canonical system-15 observer/query seams, and focused integration proves exactly-once policy resolution. **BLOCKED external prerequisite:** production Unity full-run composition requires the hierarchy-aware macro-world realization still open in `20260829-020634-000-KentridgeMacroWorldPhysicalRealization`; the current one-region Kentridge planner correctly rejects the multi-region authored campaign.
- [ ] **T26-022 — Integrate frontend aftermath.** Systems 14/23 already consume `IGameOutcomeQuery` and present `ApplicationScreen.Outcome` without campaign-owned scene loads. **BLOCKED with T26-021:** the production full-run graph cannot supply the real System15 query until the multi-region campaign can be physically composed.
- [x] **T26-023 — Verify ordinary losses remain nonterminal unless authored.** `CampaignOutcomeIntegrationTests` proves an unresolved/failed encounter does not resolve System15; only the mapped authored condition does.

## Fast semantic route proof

- [x] **T26-030 — Build engine-independent canonical route test.** `CanonicalCampaignRouteTests` starts the production authored campaign at `NewGame` and drives public semantic runtime/domain facts to terminal outcome.
- [x] **T26-031 — Prohibit privileged progression shortcuts in the test.** The route uses CampaignRuntime public facts, real cutscene completion ticks and the production `EncounterRegistry`; no direct completion/grant/outcome/private mutation path is used.
- [x] **T26-032 — Assert every route milestone.** The test records opening and multiple later objective/cutscene/encounter milestones and asserts exactly one immutable `GameOutcomeResolved`.
- [x] **T26-033 — Add dead-end regression.** The canonical driver reports the last semantic milestone when a required authored transition cannot advance.
- [x] **T26-034 — Verify optional content does not gate canonical completion.** The optional well quest is deliberately left incomplete while the canonical route still reaches the terminal outcome.

## Persistence / multiplayer / built-player proof

- [x] **T26-040 — Choose a meaningful mid-run restore point.** The canonical route captures after multiple post-opening consequences with unified Progression/cutscene/party/spell truth present.
- [ ] **T26-041 — Restore through systems 16/14.** `KentridgeSessionPersistenceBridge` now routes campaign semantic capture/restore through System16 `SessionPersistenceService`, and the Kentridge module owns a fresh-graph System14 restore regression. **Remaining:** exact-SHA CI validation of that regression.
- [ ] **T26-042 — Continue canonical route after restore.** Fresh-graph regression verifies restored current progression and completed one-shot history without replaying `NewGame`/historical cutscenes. **Remaining:** exact-SHA CI validation together with T26-041.
- [ ] **T26-043 — Verify shared multiplayer progression/outcome.** **BLOCKED external prerequisite:** System25 remains open on current master; reuse its infrastructure when it lands. Do not create an alternate transport/process harness.
- [ ] **T26-044 — Add canonical built-player full-run scenario.** **BLOCKED external prerequisite:** current Kentridge physical planner intentionally accepts one Kentridge region, while `20260829-020634-000-KentridgeMacroWorldPhysicalRealization` is still open. Do not weaken the single-region invariant or fake later regions.
- [ ] **T26-045 — Make full-run scenario milestone-driven.** Blocked with T26-044; when available, use bounded semantic waits only.
- [ ] **T26-046 — Classify full-run validation appropriately.** Fast semantic route remains affected-module coverage; final slow full-run classification awaits the production full-run scenario.

## Cleanup / close

- [x] **T26-050 — Search for parallel progression/game-loop state.** Repository and feature-diff audit found no `CurrentChapter`, `CurrentPhase`, generic campaign phase counter, final-boss completion authority, or campaign-local objective store; System11 Progression remains the objective authority.
- [x] **T26-051 — Search Story effects for domain-god operations.** Story event/effect/runtime audit found only semantic objective/quest/cutscene/party/spell/outcome-condition coordination; no direct vitality, inventory, world, transport, scene-load, or presentation mutation path exists.
- [x] **T26-052 — Verify recovered-map ordering claims.** Pinned MountingForce progression evidence directly verifies each claimed hard edge; church->Rorik and mayor-lead->Logan remain explicitly labeled authored bridges, and filenames/inferred quest labels are not treated as chronology.
- [ ] **T26-053 — Run automatic domain/campaign tests plus built-player full-run gate.** Exact-SHA domain/module CI is pending for the latest branch; built-player full-run and shared multiplayer portions remain blocked by T26-043/T26-044.
- [ ] **T26-054 — Close with end-to-end semantic proof.** Fast semantic proof exists, but closure requires the real production full-run path and all blocked acceptance gates above.
