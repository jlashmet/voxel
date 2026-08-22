# Kentridge opening fidelity plan

This checklist is the branch-local execution plan for `agent/kentridge-integration-cleanup`.

It is subordinate to `AGENTS.md`, `CLAUDE.md`, `docs/WORLDBUILDER_RUNTIME_INTEGRATION.md`, and `docs/MOUNTING_FORCE_WORLD_HANDOFF.md`. The legacy Mounting Force source defines story/cutscene truth; the voxel project may regenerate physical layout but must not invent or silently rewrite recovered narrative facts.

## Source recovery and authored content

- [x] Re-read the repository instructions and the existing Kentridge/WorldBuilder integration contracts before changing the opening.
- [x] Recover the original opening choreography from `jlashmet/mounting-force/Opening.m`.
- [x] Recover the original 31-line opening dialogue from the Mounting Force `Art/Opening.txt` source.
- [x] Extend `jlashmet/mounting-force` archaeology with a verified source-grounded Kentridge opening cutscene contract.
- [x] Import that exact contract into `References/MountingForce/contracts/` and record its upstream blob SHA/head in `SOURCE_MANIFEST.yaml`.
- [x] Preserve the recovered speaker order as explicit cutscene actor metadata rather than embedding speaker names in presentation text.
- [x] Preserve the original dialogue split around Logan's entrance: lines 1-10, Logan line 11, then lines 12-31 after the group turns to him.
- [x] Preserve the recovered door sound, waits, Weldon entrance, facing changes, and Logan approach in authored choreography.
- [x] Identify the player-side opening actor as Weldon so presentation does not label him generically as `Lead`.

## Regression coverage

- [x] Add a focused regression covering all 31 recovered lines, exact speaker order, and stable cue mapping.
- [x] Add a focused regression covering the recovered 10 / 1 / 20 choreography split and the action ordering around Logan's entrance.
- [x] Keep artifact-quota failures from masking successful non-visual targeted tests while retaining strict artifact upload for visual proof runs.
- [x] Prove the dialogue/source regression passes through `ci/single-test` on feature code SHA `63f26d56e84b9fcacb2e697ae79c7ff932120ccc` (`ci-test` request commit `e898ec255808a53f00c6c26057536926fde565d4`, Actions run `32548348388`).
- [x] Prove the choreography regression passes on the exact current feature head through `ci/single-test`. Feature code SHA `9725c849123b7a8b7e3f6612a74c3ca6b8713033`; request commit `5243b2c361f33788e145ca7d71d004849ce05007`; Actions run `32553176476`; one requested PlayMode test executed and passed.
- [x] Re-run the production Kentridge playable-scene acceptance with the expanded 31-line opening and prove control/camera/movement/story handoff still completes.
  - Focused production-opening acceptance is green on feature code SHA `03f7dafc4060f7f8e28ad56a695aa35e527f6763` (`ci-test` request commit `9cc01fa6988a150a033653689eb6d2893fd0033d`, Actions run `32555582288`); one requested PlayMode test executed and passed.
  - Broader production acceptance is green on feature code SHA `4acb706986bb044fecbc34e4224d8f22551c759f` (`ci-test` request commit `74f7d179d4e072ef1f11c9c9fc95379ad2f880be`, Actions run `32558213387`); the one requested PlayMode test executed and passed through the full opening, control handoff, physical pub exit, destination interaction/cutscene, and rescue flow. The workflow job remains red after the successful test/capture because the strict visual artifact upload failed, so visual/all-green completion gates remain open.
- [x] Keep self-contained Kentridge dialogue/choreography requests from doing unrelated Voxel Showcase bake work in targeted CI. Actions run `32553176476` shows `Bake Voxel Showcase startup world` skipped while the choreography test executed successfully.

## Player-facing fidelity

- [x] Keep the opening on the generated Kentridge pub rather than a hand-authored replacement scene.
- [x] Keep the fixed elevated ensemble camera during the pub conversation and character entrances.
- [x] Keep Weldon, Madeline, Steven, and Logan as visible staged participants using the production actor/presentation path.
- [x] Run the standalone player capture for `KentridgePlayableSlice` on the validated feature head.
  - Exact-head evidence: feature SHA `f5e0175db3a7785444d8c9fabcfc0e36acf61a83`; `ci-test` request commit `a79d75e0abf124fa7a20556e2821a38fd9d04bf2`; Actions run `32559162545`, job `96997974992`. The one requested production PlayMode test passed, the standalone real-player capture passed, and preview emission passed. Strict visual artifact upload then failed only because GitHub artifact storage quota was exhausted, so frame-inspection and all-green completion gates remain open.
- [x] Add a focused production-camera readability regression that measures the actual rendered actor bounds at dialogue line 1 and Logan's recovered line 11, with a 4% viewport safety margin and 12% minimum rendered body height.
  - Diagnostic feature SHA `d14d8bf5de9ef7c5d275764d85af70ceb6fc11e5`, request commit `98c6899ba28fa712a5d4059a41e170df834cf503`, Actions run `32564427089`, job `97010861256` reached the intended line-11 gate and proved a real defect: Madeline's rendered bounds reached viewport X `0.0364`, outside the required `0.04` left-edge margin.
- [x] Prove the renderer-bounds camera readability regression green after the minimal semantic camera correction.
  - The correction keeps world coordinates out of the cutscene: the gathered trio uses 0.7 m semantic clearance, Logan stops in `SiteInterior` instead of the camera-adjacent entrance approach, and the production camera widens from 70 to 71 degrees. Feature code SHA `64c4554e64572399536fb188663d14109b16dd7f`; request commit `dc3041cb8eca6759be190b2077539f4ce2c8260a`; Actions run `32566559337`, job `97015900701`; exactly one requested PlayMode test executed and passed in 46s. The run also exposed 199s of unrelated Showcase bake work, which was subsequently removed from this self-contained validation path.
- [x] Inspect the real-player frames for bar/pub staging, four-character readability, camera composition, Logan entrance, dialogue presentation, and post-cutscene handoff.
  - Exact-head real-player evidence: feature SHA `55fff1c249f4c10f1600faeda5281f0163c79ae9`; evidence request commit `fa3ceccac0e9f5112e45e0d63bb7acc982f8e9cc`; Actions run `32566867562`, job `97016656754`. All four persisted frames at approximately 39s, 59s, 79s, and 89s were inspected. The edge-clipping defect is gone and dialogue remains readable, but the cast is still too small/far while the counter and near floor dominate the frame, so the player-facing ensemble composition remains a proven defect and the fix gate stays open.
- [x] Replace the opening's oblique camera-near-plane roof cutaway with the renderer's bounded world-voxel cutaway path.
  - Full-resolution standalone-player frames on feature SHA `30265b3e5c3c21edc87a6dc551a27c07183fa824` proved the near-plane approach was sectioning foreground terrain, pub geometry, furniture, and actors in view space. The actor/foundation Y alignment was separately traced and ruled out as the cause.
  - Feature SHA `3b22621072ca755811ad9fc848810e433822ffb7` keeps the camera near plane at its normal `0.05`, activates the existing `RenderingComposition.SetCutaway(...)` only over the generated Pub footprint above the retained ground-floor walls, and makes the already-plumbed `_Cutaway*` shader constants actually discard that bounded voxel volume in `SmoothSurface.shader`.
  - Scene state was then corrected through feature SHA `26a3367c7c452e7fd3d0892d9d5998c9d4431c76` so `KentridgePlayableSlice.unity` explicitly serializes the intended `m_CutawayHeightMetres: 2.8`; the net scene diff from the prior plan commit is exactly that one added YAML line.
  - The focused regression now rejects camera-plane slicing and verifies the cutaway remains bounded to the generated Pub envelope and clears before gameplay.
  - Static follow-up on the current branch confirms the cutaway Y conversion shares the production camera's explicit `floorFocus + 0.9m` contract, so subtracting `0.9m` recovers the generated ground-floor elevation before converting to voxel/decimetre space.
- [ ] Prove the rewritten cutaway through the focused production presentation regression on the exact implementation SHA.
  - First request: feature SHA `3b22621072ca755811ad9fc848810e433822ffb7`; request commit `550da399c1c2e9c8ff67f44d38fd8b09119d570d`; Actions run `32584455241`, job `97058624644`. The requested Unity test did **not** execute: the runner's safety guard found the developer Unity editor and AssetImportWorker processes active under `/Users/jlashmet/code/voxel` for the full 60-second guard window, so `Run requested test` was skipped. This is an infrastructure/editor-contention blocker, not a test result; no fidelity checkbox is earned by it.
  - Second request attempt: code remains unchanged from implementation SHA `26a3367c7c452e7fd3d0892d9d5998c9d4431c76`; request commit `66c536117b67f0c1c375ebc4080afcdaee674166`; Actions run `32585596856`, job `97061366895`. The same safety guard again found the live developer Unity editor plus two AssetImportWorkers for `/Users/jlashmet/code/voxel` through the full 60-second window, so the requested PlayMode test again executed zero tests. The later capture step only reported that this test has no configured player-capture profile; it produced no screenshot evidence and does not advance the gate.
- [ ] Fix any visual/staging fidelity defects found in capture and repeat the smallest relevant validation until green.
  - Current implementation candidate is the bounded world-voxel cutaway code plus explicit production-scene height serialization through feature SHA `26a3367c7c452e7fd3d0892d9d5998c9d4431c76`. Treat it as in-progress until the focused production presentation regression executes non-zero tests and passes, then a fresh standalone-player capture from that implementation state is inspected at full resolution.
  - Validation boundary: entrance/carve/anchor alignment remains green through Actions run `32563463855`; no visual-fidelity checkbox is earned by that structural proof.
  - Next evidence gate: re-run `KentridgeOpeningPresentationTests.OpeningUsesOverheadRoofCutawayAndFadesAroundHandoff` on exact implementation SHA `26a3367c7c452e7fd3d0892d9d5998c9d4431c76` once the shared Unity editor is clear; only if it is green, run and inspect a fresh standalone-player Kentridge capture before deciding whether the composition/cutaway fix is complete.

## Generated pub bar and semantic staging

- [x] Inspect the generated `Pub` role geometry and confirm the current role signature creates an exterior hanging sign but no interior bar/counter or seating.
- [x] Trace the production `InteriorGatheringArea` projection and actor stage anchors through the generated Kentridge site geometry. The generic stage resolver places the gathering region at two-thirds of the architecture-published usable interior depth.
- [x] Add a deterministic generated pub bar/counter at the WorldGen/architecture layer rather than synthesizing scene-local furniture. The active shared-house program now emits a timber counter inside the generated Pub program so placement, frontage rotation, and precedence remain identical to the building.
- [ ] Add seating/stool geometry where it improves the intended Weldon/Madeline/Steven bar staging without blocking the doorway, Logan's entrance path, or cutscene movement.
- [x] Keep actor placement semantic: align the existing authored stage-point resolution with the generated bar instead of hard-coding final world-space positions in `KentridgePlayableSlice`. The counter public face is derived from the same two-thirds-depth gathering strip, with a small interaction gap and bartender circulation behind it.
- [x] Keep the generated Pub's physical front-door opening and published door anchor aligned with the architecture-owned `StructureForm.DoorOffsetDm` by expressing the authored offset through the shared `HouseDoorLayoutConfig.ExplicitOffsets` path rather than a Kentridge-only carve.
- [x] Prove physical Pub door, translated shape-program anchor, and catalogue anchor alignment through `KentridgeGeneratedEntranceAlignmentTests.PubPhysicalDoorAndAnchorsHonorArchitectureDoorOffset`. Feature code SHA `bb181c7e5c8457ff89e60df512c300f2813b9a57`; `ci-test` request commit `3247e0805194c86a2bc335c0d149fd41cee5c7d1`; Actions run `32563463855`, job `97008512484`; one requested EditMode test executed and passed in 60s with the unrelated Showcase bake skipped, and `ci/single-test` is green.
- [x] Add the smallest focused regression that proves generated Pub geometry contains the intended interior bar invariant and preserves required traversal/open space: `ArchitectureGeometryCatalogueTests.KentridgeCombinedCataloguePubHasRearCounterAndOpenFrontAisle`.
- [x] Prove that focused generated-pub regression through `ci/single-test` on the exact feature head. Feature code SHA `aaf4335364991a27b2caa8f51c1c97c47701ffcd`; request commit `27d5ba144d77479ba3177522d66a548016e2b25c`; Actions run `32552387721`; one requested EditMode test executed and passed.
- [ ] Verify the production opening visually places Weldon, Madeline, and Steven at the generated bar before Logan enters.

## Completion gate

- [ ] Review the final feature diff against the architecture/integration constraints.
- [ ] Confirm all relevant targeted CI requests executed non-zero tests and are green.
- [ ] Record the exact validated feature SHA and capture/test evidence here before declaring the Kentridge opening fidelity pass complete.

Validation checkboxes are evidence gates: do not mark them complete merely because the implementation exists.
