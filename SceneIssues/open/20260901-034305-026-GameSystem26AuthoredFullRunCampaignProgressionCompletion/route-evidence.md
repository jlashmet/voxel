# GameSystem26 canonical route evidence

## Evidence policy

Hard ordering comes only from recovered positive `sceneFinished` / `waitForScene` dependencies. Legacy filenames, inferred quest labels, candidate geography and negative guards are not chronology. Where the canonical voxel campaign connects otherwise disconnected recovered components, the bridge is an explicit authored design decision below rather than a recovered-order claim.

Primary provenance:
- `References/MountingForce/SOURCE_MANIFEST.yaml` -> `jlashmet/mounting-force@agent/original-world-content-inventory`.
- `References/MountingForce/contracts/world-progression-handoff.yaml` -> verified positive dependency policy.
- upstream `docs/world-progression-chains.yaml` source commit `e9a46d9efdcbe27552780c827a078c3f9c1dded0`.

## Current production opening

`KnownOpeningCampaignContent` owns the normal `NewGame` opening: intro, Kentridge well quest, travel objective, Awon/Medrare consequences, Medrare joining, Flame grant, and `medrare-to-church`. It uses Story plus unified Progression and has no campaign-local objective state or terminal outcome.

## Canonical completion spine

1. **Existing opening -> Kentridge church.** Recovered hard edge: `medrare-to-church` (Medrare house lower) -> `angel-give-quest` (Kentridge church).
2. **Church -> Rorik conflict.** **Authored bridge.** The recovered graph does not prove chronology between the church component and `RorikDefeated`; the voxel campaign explicitly selects the church charge as the canonical lead into the recovered Kentridge Rorik conflict.
3. **Rorik -> Moordell.** Recovered hard edges: `RorikDefeated` -> `AttackMoordell`, `Moordell-Distribution`, and `moordell-distribution`.
4. **Moordell -> Rossdam.** Recovered hard edges: `moordell-distribution` -> `discuss-to-rossdam` and `rossdam-battle-start`.
5. **Rossdam -> Logan lead.** Recovered hard edge: `rossdam-battle-end` -> `kentridge-ask-mayor-logan` (plus optional `rorik-joins`).
6. **Logan lead -> castle.** **Authored bridge around recovered anchors.** The source separately verifies `kentridge-logan-battle-end` -> `logan-castle-battle-start`; the voxel route explicitly makes the mayor lead point at that recovered Logan conflict.
7. **Castle terminal.** Recovered final subchain: `logan-castle-lower-battle-end` -> `logan-castle-lower-logan-hole`; imported reference guidance records the following transition to Rossdam. The voxel campaign maps completion of this recovered terminal chain to the configured System15 success outcome rather than a boss/scene flag.

## Optional content

Not required for canonical completion: Kentridge well quest, Rorik rejoin, Hightown recruitment/side chains, farmer/gnome/angel branch, graveyard/Rita branch, Fairy Village and Orc Village side content, child rescues, thief-password/mountain branch, shops/secrets and other isolated recovered scenes. Their omission must not block the terminal outcome.

## Semantic gap list

- Story currently has no owning-domain encounter-resolution event/condition. The recovered route contains battle completion anchors, so System26 needs a narrow `EncounterResolved` Story fact sourced from `Game.Encounters.Api`, not a direct combat setter.
- Story currently has no terminal Outcome semantic effect. System15 already owns `OutcomePolicyRouter`; System26 needs a narrow effect that observes a configured `OutcomeConditionRef`, allowing the router/resolver to commit exactly once.
- No new chapter/current-phase state is required. Existing site/NPC/cutscene events and unified Progression cover the remaining route transitions.

## External prerequisite

System25 separate-process multiplayer validation is not present on current master: `tools/player-validation.py` remains single-process. Shared multiplayer progression/outcome proof (T26-043) must reuse System25 when it lands; System26 will not create an alternate network harness.
