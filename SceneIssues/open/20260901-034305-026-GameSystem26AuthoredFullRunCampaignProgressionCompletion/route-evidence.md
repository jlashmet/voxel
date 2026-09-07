# GameSystem26 canonical route evidence

## Evidence policy

Hard ordering comes only from recovered positive `sceneFinished` / `waitForScene` dependencies. Legacy filenames, inferred quest labels, candidate geography and negative guards are not chronology. Where the canonical voxel campaign connects otherwise disconnected recovered components, the bridge is an explicit authored design decision below rather than a recovered-order claim.

Primary provenance:
- `References/MountingForce/SOURCE_MANIFEST.yaml` -> `jlashmet/mounting-force@agent/original-world-content-inventory`; imported reference policy preserves confidence classes and forbids inferred guidance from overriding verified constraints.
- `References/MountingForce/contracts/world-progression-handoff.yaml` -> verified positive dependency policy: only positive scene dependencies are hard progression structure; negative guards, actor prerequisites, and inferred quest labels are not chronology.
- upstream `docs/world-progression-chains.yaml` fetched at repository commit `e9a46d9efdcbe27552780c827a078c3f9c1dded0`; that snapshot declares normalized source commit `3360450ca0fdffe8a2d24578549d00bb1e52b9bd`.

## Current production opening

`KnownOpeningCampaignContent` owns the normal `NewGame` opening: intro, Kentridge well quest, travel objective, Awon/Medrare consequences, Medrare joining, Flame grant, and `medrare-to-church`. It uses Story plus unified Progression and has no campaign-local objective state or terminal outcome. The current Unity `KentridgePlayableSlice` still composes this single-region opening because the Kentridge physical planner deliberately rejects multi-region campaign hierarchies.

## Canonical completion spine

1. **Existing opening -> Kentridge church.** Recovered hard edge: `medrare-to-church` (Medrare house lower) -> `angel-give-quest` (Kentridge church).
2. **Church -> Rorik conflict.** **Authored bridge.** The recovered graph does not prove chronology between the church component and `RorikDefeated`; the voxel campaign explicitly selects the church charge as the canonical lead into the recovered Kentridge Rorik conflict.
3. **Rorik -> Moordell.** Recovered hard edges include `RorikDefeated` -> `Moordell-Distribution` and `RorikDefeated` -> `moordell-distribution`.
4. **Moordell -> Rossdam.** Recovered hard edges: `moordell-distribution` -> `discuss-to-rossdam` and `moordell-distribution` -> `rossdam-battle-start`.
5. **Rossdam -> Logan lead.** Recovered hard edge: `rossdam-battle-end` -> `kentridge-ask-mayor-logan` (plus optional `rorik-joins`).
6. **Logan lead -> castle.** **Authored bridge around recovered anchors.** The source separately verifies `kentridge-logan-battle-end` -> `logan-castle-battle-start`; the voxel route explicitly makes the mayor lead point at that recovered Logan conflict.
7. **Castle terminal.** Recovered hard edge: `logan-castle-lower-battle-end` -> `logan-castle-lower-logan-hole`. The voxel campaign maps completion of this recovered terminal chain to the configured System15 success condition rather than inventing a boss/scene completion flag.

## Optional content

Not required for canonical completion: Kentridge well quest, Rorik rejoin, Hightown recruitment/side chains, farmer/gnome/angel branch, graveyard/Rita branch, Fairy Village and Orc Village side content, child rescues, thief-password/mountain branch, shops/secrets and other isolated recovered scenes. Their omission must not block the terminal outcome.

## Semantic vocabulary result

- System26 added only the missing owning-domain encounter fact: `EncounterResolved`, sourced from a resolved `Game.Encounters.Api` snapshot rather than a combat/story setter.
- System26 added only the missing terminal policy effect: `ObserveOutcomeCondition(OutcomeConditionRef)`. Story publishes the condition; System15 `OutcomePolicyRouter`/`GameOutcomeRuntime` remains the sole terminal authority.
- No chapter/current-phase/game-loop state is required or introduced. Existing site/NPC/cutscene events and unified System11 Progression cover the route transitions.
- Story effects remain semantic coordination only: objective/quest start, cutscene request, party/spell progression, and outcome-condition observation. There is no direct vitality, inventory, world, transport, scene-load, or presentation mutation effect.

## External prerequisites

- **System25 multiplayer validation:** `20260901-034305-025-GameSystem25MultiplayerEndToEndGameplayValidation` remains open on current master. Shared multiplayer progression/outcome proof (T26-043) must reuse it; System26 will not create an alternate network harness.
- **Macro-world physical realization:** `20260829-020634-000-KentridgeMacroWorldPhysicalRealization` remains open on current master. `KentridgeCampaignWorldPlanner` intentionally requires exactly one region/settlement and explicitly directs multi-region campaigns to a hierarchy-aware generator. The authored full run spans Kentridge/Moordell/Rossdam/Logan geography, so T26-021/022 production full-run wiring and T26-044/045 built-player proof remain blocked rather than weakening this invariant or faking remote regions.
