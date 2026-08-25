# Plan — Kentridge world-builder ownership

## Goal
Consolidate Kentridge so the showcase and other consumers author the town through one World Builder API, with the legacy Mountain Force World Generation implementation owned by the Game/Voxel Engine architecture rather than a parallel package path.

This capture is the ownership-consolidation step, not the final physical layering step. The intended architectural destination is:

- `Game.WorldBuilder` owns semantic game intent: regions, settlements, sites, NPC placement requirements, quests/objectives, secrets, cutscenes, named towns such as Kentridge/Hightown, and the public authoring API.
- `VoxelEngine.WorldGen` owns reusable physical generation: deterministic spatial/layout primitives, generic town/road/building planning, architecture grammars, footprints/floors/roofs/windows/doors/facades, and voxel realization. This layer must not know Kentridge, Weldon, quests, cutscenes, or other game-specific semantics.
- A narrow Game→VoxelEngine integration boundary translates semantic WorldBuilder requirements into engine-level generation inputs and returns physical realization facts.
- The engine should be capable of generating a convincing anonymous town without depending on Game assemblies; Game assemblies supply why the town exists and which semantic roles/capabilities must be present.

For this issue, moving the former package implementation under `Assets/Game/WorldBuilder/Generation` is an intermediate consolidation that removes the competing package-owned authoring path. Do not broaden this capture into the full `VoxelEngine.WorldGen` extraction unless required to make the ownership boundary correct. Record that extraction as the explicit follow-on architectural goal so the temporary physical placement is not mistaken for the desired end state.

## Scope
- Assigned capture only: `20260825-040805-194-VoxelShowcase`.
- Inventory the current Kentridge, Mountain Force World Generation, and World Builder call paths before changing production code.
- Add a focused regression that proves the canonical authoring path/ownership invariant.
- Make the smallest production change that removes the duplicate/parallel path without changing unrelated scene behavior.
- Preserve a clean future extraction seam between game-semantic WorldBuilder code and reusable VoxelEngine world-generation code.

## Constraints
- Work only on `fixes/agent-1` and `ci-test/fixes/agent-1`.
- Do not create or start another scene capture.
- Preserve the original capture evidence unchanged.
- Connector-only validation uses the repository's targeted CI workflow; no local Unity execution.
- A verified fix is closed only after the focused CI request succeeds and the final diff/architecture is reviewed.

## Acceptance criteria
- Kentridge has one canonical town-authoring implementation/path.
- Legacy Mountain Force World Generation code needed by Kentridge is relocated/owned under the Game/Voxel Engine World Builder architecture rather than remaining a competing package-level authoring system.
- Scene/content consumers invoke town construction through the World Builder API rather than direct legacy package entry points.
- The resulting boundary does not make generic physical generation depend on game-semantic concepts; the documented follow-on is to extract reusable generation/architecture/voxelization to `VoxelEngine.WorldGen` while leaving semantic authoring in `Game.WorldBuilder`.
- A focused regression guards the ownership/API invariant and passes `ci/single-test`.
- Terminal `issue.json` records the verified fix commit and regression test, and the capture moves from `SceneIssues/open/` to `SceneIssues/closed/` in a separate bookkeeping commit.

## Tasks
- [x] Read repository workflow and assigned capture metadata.
- [x] Confirm the assigned persistent feature branch is usable.
- [x] Inventory Kentridge/Mountain Force/World Builder implementations and callers.
- [x] Record the baseline architecture experiment.
- [x] Add or extend a focused regression.
- [x] Implement the primary ownership/API consolidation.
- [x] Migrate remaining stale scene/test callers exposed by Unity compilation.
- [x] Run targeted CI and iterate until green.
- [x] Review the final diff against `CLAUDE.md`, relevant specs, and the Game-vs-VoxelEngine destination above.
- [x] Record verification evidence and resolution details.
- [ ] Move the verified capture to `SceneIssues/closed/` in terminal bookkeeping.

## Final verification
- Verified production/test source commit: `433bbe8ed24ce43627d4ff547d46e53930121f9e`.
- Focused regression: `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary`.
- Targeted CI request commit `c5243ad758f2e6349fb64268dbd3dbc447893616` completed with `ci/single-test = success` in Actions run `32882777952`; exactly one test case executed.
- Exact SceneIssue replay run `32883739976` fresh-baked `Assets/Scenes/VoxelShowcase.unity`, loaded the saved issue/camera at the original 1364×836 resolution, ran for 70 seconds, and completed successfully.
- All six replay frames were inspected. The first frame is still loading; the 24.4s through 64.4s frames are stable and show the expected Kentridge street corridor/architecture. From t=51s through t=70s the surface telemetry remains `visible=544`, `min=544`, `max=544`, `missingMax=0`, with no coverage drops.
- Replay artifact `sceneissue-040805-replay-32883739976` (artifact id `9576969385`, digest `sha256:dd5054c0af40b0c27260e4919e97cb9e29e81ec0e111c6795b6467d6b516f7c2`) bundled the original capture, replay frames, player log, FPS telemetry, and fresh-bake log for inspection.

## Findings
- The capture contains one frame and no circled sub-region; the issue note defines an architectural acceptance condition rather than a localized rendering blemish.
- The former package-owned implementation is consolidated under WorldBuilder ownership and Kentridge construction is forced through `WorldBuilderTownAuthoring` rather than parallel authoring entry points.
- The current `Game/WorldBuilder` tree still mixes semantic WorldBuilder responsibilities with the relocated physical generation backend. That is intentionally temporary for this issue. The documented end state remains `Game.WorldBuilder` semantic intent over reusable `VoxelEngine.WorldGen` physical generation.
