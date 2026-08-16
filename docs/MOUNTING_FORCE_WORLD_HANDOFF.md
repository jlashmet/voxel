# Mounting Force world-content handoff

## Goal

Use the reverse-engineered original Mounting Force world as a semantic specification for the voxel World's procedural generation, without reproducing the old TMX geometry literally.

The legacy project tells us **what must exist and how it must behave**. The voxel WorldBuilder remains responsible for **where and how it is generated**.

## Repository boundary

The original archaeology remains in `jlashmet/mounting-force` on `agent/original-world-content-inventory`. This voxel branch imports only a curated reference surface under `References/MountingForce/`.

Do not move the raw reference YAML into Unity `Assets/`. When runtime consumption begins, introduce voxel-owned typed models/adapters under the WorldBuilder subsystem and test the translation from reference contracts into those models.

## Imported contract layers

### Hard/verified generation policy

- `References/MountingForce/contracts/world-progression-handoff.yaml`
  - Preserve verified scene dependency ordering and cross-level progression.
  - Do not turn negative guards or actor prerequisites into invented chronology.
- `References/MountingForce/contracts/world-actor-population-handoff.yaml`
  - Preserve actors required by story/event/encounter semantics.
  - Treat ordinary actor coordinates/counts as distribution guidance.
- `References/MountingForce/contracts/world-object-handoff.yaml`
  - Preserve concrete reviewed affordances such as reward chests, trigger volumes, delayed enemy spawns, signs, and specific hazards.
  - Preserve explicit named reward intent while allowing ordinary/random rewards to move.
- `References/MountingForce/contracts/world-visual-handoff.yaml`
  - Preserve intentional visual outliers and palette identity without allowing palette reuse to create false topology.

### Soft generation guidance

- `References/MountingForce/guidance/world-procgen-clusters.yaml`
  - Settlement-centered overlapping topological neighborhoods for locality/scoring.
- `References/MountingForce/guidance/world-inferred-geography.yaml`
  - Dialogue/sign direction hints that may influence layout scoring but never override verified traversal.

## Existing source-grounded world snapshot

At the imported snapshot, the archaeology has established:

- 99 packaged maps.
- 205 valid Warp/Portal objects, 192 unique directed traversal edges, 94 bidirectional pairs, and 4 one-way edges.
- 168 unique story-scene producers, 63 verified positive scene dependencies, and 43 cross-level dependencies.
- 1,523 authored actors selected by verified Objective-C inheritance, with 342 protected semantic/staging/function anchors and 1,181 incidental regeneration candidates.
- 22 explicit character identities.
- 22 runtime sellers.
- 29 curated social/lore entities and 69 curated semantic relationships.
- 30 `waitForDeath` encounters, including 12 mechanically strong major locked actor-encounter candidates and 2 recruitment defeat encounters.

These numbers are provenance/context, not a new voxel schema. Runtime schemas should be designed around capabilities and semantics rather than reproducing the old classes.

## Recommended WorldBuilder integration model

A generated world should be solved in this order:

1. **Global semantic graph**
   - required places/level roles
   - traversal connectivity
   - cross-level story dependencies
   - required named characters and event participants
   - required rewards and major encounters
2. **Local place contracts**
   - interior/hub relationships
   - encounter locks and trigger/spawn requirements
   - actor staging and cinematic clearance
   - required services/functions (seller, request/reward contact, recruitment, etc.)
3. **Geometry generation**
   - terrain, roads, settlements, interiors, caves, dungeons
   - reserve reachable volumes for required event/actor anchors before decoration
4. **Population and decoration**
   - use legacy distributions/profile patterns as scoring priors
   - freely regenerate incidental coordinates/counts where hard semantics do not constrain them
5. **Validation**
   - prove required traversal is reachable
   - prove story prerequisites remain satisfiable
   - prove required actors/events/rewards/functions can be instantiated
   - keep soft geography/palette differences from failing hard validation

## Voxel-owned data model direction

Do **not** model this as one giant `LegacyMap` object or a single place enum. Prefer composable contracts such as:

- `WorldPlaceContract`
- `TraversalRequirement`
- `StoryStateRequirement`
- `CharacterRequirement`
- `EncounterRequirement`
- `ServiceRequirement`
- `RewardRequirement`
- `CinematicRequirement`
- `PaletteGuidance`
- `PopulationGuidance`

A place can then simultaneously be, for example, a settlement interior, commerce location, recruitment site, story gate, and cinematic stage without forcing a brittle single classification.

## Provenance and update workflow

`References/MountingForce/SOURCE_MANIFEST.yaml` records the source branch head and source blob SHA for every imported file.

When more legacy detail is needed:

1. inspect the existing upstream generated artifact first;
2. if the artifact is insufficient, extend the archaeology in `mounting-force` from source evidence;
3. import only the resulting contract/evidence needed by the voxel integration;
4. record its source SHA in `SOURCE_MANIFEST.yaml`;
5. add or update voxel-side typed adapters/tests.

This keeps the original repo responsible for proving legacy facts and the voxel repo responsible for turning those facts into a modern generated game.

## Immediate missing layer: narrative/cutscene contract

The first concrete gap discovered during handoff is ordered cutscene content.

The archaeology can currently prove, for example, that the final Logan sequence contains the dependency:

`logan-castle-lower-battle-end -> logan-castle-lower-logan-hole -> rossdam`

and it can identify participants plus movement/camera/state requirements. It cannot yet reconstruct the complete ordered dialogue/action beat sheet solely from the generator-facing artifacts.

The next legacy-content iteration should therefore add a **narrative/cutscene contract** containing at minimum:

- scene/event ID and place
- prerequisites/guards
- ordered beats
- speaker/actor token per dialogue beat
- dialogue text or semantic intent
- actor movement/facing actions
- camera/zoom actions
- party/reward/state mutations
- transition/end state
- confidence/provenance for every beat

Once that exists, the voxel WorldBuilder can generate a different physical stage while preserving the actual story scene.
