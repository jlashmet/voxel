# Mounting Force legacy world reference

This directory is the voxel project's imported, source-derived reference surface for the original Mounting Force world.

## Ownership

- **Original source of truth:** `jlashmet/mounting-force`, branch `agent/original-world-content-inventory`.
- **Voxel integration branch:** `agent/mounting-force-content-handoff`.
- Files here are snapshots of generator-facing contracts and guidance, not a fork of the original Objective-C/TMX game.
- Raw imported YAML stays outside Unity `Assets/` so Unity does not treat archaeology data as runtime assets.

## Layout

- `contracts/` — compact, generator-facing rules backed by verified source evidence.
- `guidance/` — useful inferred locality/geography guidance that must not override hard traversal/story constraints.
- `SOURCE_MANIFEST.yaml` — exact source branch/commit and source blob SHAs for imported files.

## Consumption rule

WorldBuilder code should not depend directly on the original TMX/Objective-C layout. The intended flow is:

1. Read these legacy contracts as reference/input.
2. Translate them into voxel-owned typed WorldBuilder content models.
3. Generate new geometry while preserving hard semantics: traversal reachability, story dependencies, required actor/event anchors, rewards, encounter behavior, and intentional visual identity.
4. Treat incidental coordinates, ordinary population placement, palette co-use, and inferred geography as soft guidance.

## Why the exhaustive indexes are not copied yet

The archaeology branch contains much larger exhaustive indexes (`world-story-graph.yaml`, `world-generator-level-contracts.yaml`, actor/object inventories, event implementation cross-references, etc.). They remain in the original repo until the voxel integration needs them. Pulling them on demand keeps this repo focused while retaining full provenance.

## Current known gap

The current artifacts preserve scene dependencies, participants, state effects, movement/camera requirements, and level transitions, but do **not yet preserve complete ordered cutscene dialogue/choreography**. For example, the final Logan chain can identify `logan-castle-lower-battle-end -> logan-castle-lower-logan-hole -> rossdam`, but not reconstruct the full spoken/dramatic sequence from the imported contract alone. Filling that narrative/cutscene layer is a next-step integration task.
