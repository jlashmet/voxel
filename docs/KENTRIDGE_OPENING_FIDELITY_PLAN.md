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
- [ ] Prove the choreography regression passes on the exact current feature head through `ci/single-test`.
- [ ] Re-run the production Kentridge playable-scene acceptance with the expanded 31-line opening and prove control/camera/movement/story handoff still completes.
- [ ] Keep self-contained Kentridge dialogue/choreography requests from doing unrelated Voxel Showcase bake work in targeted CI.

## Player-facing fidelity

- [x] Keep the opening on the generated Kentridge pub rather than a hand-authored replacement scene.
- [x] Keep the fixed elevated ensemble camera during the pub conversation and character entrances.
- [x] Keep Weldon, Madeline, Steven, and Logan as visible staged participants using the production actor/presentation path.
- [ ] Run the standalone player capture for `KentridgePlayableSlice` on the validated feature head.
- [ ] Inspect the real-player frames for bar/pub staging, four-character readability, camera composition, Logan entrance, dialogue presentation, and post-cutscene handoff.
- [ ] Fix any visual/staging fidelity defects found in capture and repeat the smallest relevant validation until green.

## Generated pub bar and semantic staging

- [x] Inspect the generated `Pub` role geometry and confirm the current role signature creates an exterior hanging sign but no interior bar/counter or seating.
- [ ] Trace the production `InteriorGatheringArea` projection and actor stage anchors through the generated Kentridge site geometry.
- [ ] Add a deterministic generated pub bar/counter at the WorldGen/architecture layer rather than synthesizing scene-local furniture.
- [ ] Add seating/stool geometry where it improves the intended Weldon/Madeline/Steven bar staging without blocking the doorway, Logan's entrance path, or cutscene movement.
- [ ] Keep actor placement semantic: align the existing authored stage-point resolution with the generated bar instead of hard-coding final world-space positions in `KentridgePlayableSlice`.
- [ ] Add the smallest focused regression that proves generated Pub geometry contains the intended interior bar invariant and preserves required traversal/open space.
- [ ] Prove that focused generated-pub regression through `ci/single-test` on the exact feature head.
- [ ] Verify the production opening visually places Weldon, Madeline, and Steven at the generated bar before Logan enters.

## Completion gate

- [ ] Review the final feature diff against the architecture/integration constraints.
- [ ] Confirm all relevant targeted CI requests executed non-zero tests and are green.
- [ ] Record the exact validated feature SHA and capture/test evidence here before declaring the Kentridge opening fidelity pass complete.

Validation checkboxes are evidence gates: do not mark them complete merely because the implementation exists.
