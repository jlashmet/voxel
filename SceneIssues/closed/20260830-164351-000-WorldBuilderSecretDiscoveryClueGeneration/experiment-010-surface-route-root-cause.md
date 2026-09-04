# Experiment 010 — Gallery natural-route root cause

## Acceptance symptom

The dedicated and Gallery built-player captures passed runtime/file-count gates but did not visually prove a natural discovery route. The Gallery natural frame showed terrain/foliage without a readable cave entrance; the breakable frame showed an isolated wall. This remained true after materially different replay/presentation fixes, so another camera-only change was prohibited until a minimal root cause was isolated.

## Competing hypotheses

1. **Camera ownership/framing defect:** the production cave and clue trail are physically connected, but the validation camera is aimed poorly or loses ownership.
2. **Authoring-semantic defect:** the surface clue trail and the generated cave are physically unrelated, so no framing can show a natural route between them.

## Discriminator

Inspect the production `CaveNetworkAuthoringCore` path selected by the Gallery composition.

- `CaveGenerationRequest.Underground(...)` creates `CaveEntranceMode.Underground`.
- `CarveEntrance` only clears the supplied entrance elevation; it does not connect an underground anchor to terrain.
- `ResolveVerticalDelta` applies `SurfaceDescentSegments` only when `Entrance.Mode == CaveEntranceMode.Surface`.
- Covered-target enforcement is likewise conditional on `Surface` mode.

The Gallery supplied an entrance at `TerrainQuery.HeightAt(...) - 48`, selected `Underground`, set `SurfaceDescentSegments = 0`, and separately coated approach clues on the terrain surface. Therefore the production cave started roughly 48 voxels below the clues and had no authored surface connection. Hypothesis 2 is confirmed; camera-only fixes cannot satisfy natural discovery acceptance.

## Minimal correction

Keep production cave, pocket, clue, storage, and rendering APIs unchanged. Scene composition now:

- anchors the cave mouth at terrain surface + 1 voxel,
- uses `CaveGenerationRequest.Standalone(...)` (`Surface` mode),
- descends six production segments at eight voxels per segment, reaching approximately the previously proven 48-voxel pocket cover before the covered network,
- retains sparse coating-only surface clues and the existing production pocket/clue abstractions.

A focused behavioral regression asserts that the first production segment is physically carved at the expected descending floor and that the main route reaches the covered depth with reachable traversal candidates. The old `Underground` configuration fails this discriminator because surface-descent logic is never active.
