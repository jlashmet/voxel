# SceneIssue 033053 — tree collision and shooting

## Goal

Restore the VoxelShowcase tree gameplay contract: nearby semantic trees must block player movement and the same authored trees must participate in the shooting/destruction path so a valid shot can break them.

## Scope

- `Assets/Scenes/VoxelShowcase.unity` tree population and the shared runtime tree representation it uses.
- Player/world collision registration for semantic showcase trees.
- Projectile/hitscan damage/destruction registration for showcase trees.
- A focused deterministic regression that guards both behaviors without depending on screenshot pixels.

## Constraints

- Work only on capture `20260825-033053-588-VoxelShowcase` on `fixes/agent-6`.
- Preserve the original screenshot and all capture metadata; the capture has no circles, so the whole frame is evidence.
- Do not edit `.github/test-request.json` on `fixes/agent-6`; use only `ci-test/fixes/agent-6` for targeted CI requests.
- Record every replay, diagnostic, test, and fix attempt immediately as a numbered experiment Markdown file.
- Remove temporary CI-only replay wiring before merge.
- Commit production/test work before the separate terminal issue-bookkeeping commit.

## Findings

- `ShowcaseTreePopulation` publishes the normal semantic tree world; projectile damage already crosses `VegetationComposition.TreeDamage.TrySweepImpact` / `ApplyBlast`.
- `CharacterMotor` previously queried only voxel storage, so healthy semantic tree wood was invisible to player collision.
- The fix adds a surviving-wood AABB query at the stable vegetation API boundary and uses it after voxel collision checks. Foliage stays traversable and removed branches stop blocking immediately.
- The capture itself was made against an older startup bake: capture-era `ShowcaseWorld.bytes` was 23,096,216 bytes; the current bake is 11,074,525 bytes and was refreshed after the capture.
- Exact saved-camera replay against the current checked-in bake (run `32999019598`, artifact `9617959964`) shows sky/fog only and no tree geometry while the scene still publishes 36 semantic trees. The old north-field tree visuals therefore no longer exist in the authoritative saved view.
- Final cleanup regression run `33000528711` executed exactly 3 PlayMode cases from fix commit `fdc54b765714c5b6df5787ecc57640be2d356381` and published `ci/single-test: success` on request commit `f3bbe8ab5b55e442fb9422496b79ebbeeb350f76`.
- Current `master` was merged afterward as `56f1b3f6ae5a83dd5583deccee86c7dd4b258581`; the merge changed only scene-agent CI/process files and did not alter production code, tests, scene data, or replay evidence.

## Acceptance criteria

1. Identify the concrete runtime representation used by VoxelShowcase trees and the proven reason it is omitted from collision and/or damage handling.
2. Add a focused regression that executes a nonzero test count and proves a representative showcase tree has player-blocking collision and can receive/destruct from a shot.
3. Apply the smallest production fix that restores both behaviors without converting foliage or distant presentation-only instances into gameplay collision.
4. Targeted `ci/single-test` validation passes on `ci-test/fixes/agent-6` for the exact final feature commit containing the fix and permanent regression.
5. Replay verification of the original capture is recorded after the fix, with provenance and a clear result, before the capture is closed.

## Work

- [x] Read `CLAUDE.md`, `AGENTS.md`, `SceneIssues/README.md`, the manifest, note, capture metadata, and circle state.
- [x] Trace VoxelShowcase tree creation, collision registration, and shooting/destruction handling.
- [x] Record the root-cause experiment before changing production behavior.
- [x] Add the smallest direct regression for the proven invariant.
- [x] Implement the minimal production fix.
- [x] Validate the focused permanent regression through targeted CI on an exact feature head (3/3 passed in run `32927755132`).
- [x] Replay-verify the original saved camera against the current authoritative bake and record final evidence (experiment 017 / run `32999019598`).
- [x] Remove temporary capture-specific replay test wiring from the final production/test tree.
- [x] Run the permanent regression again from the final cleanup production/test head and require `ci/single-test` success (3/3 passed in run `33000528711`; experiment 018).
- [x] Integrate current `master` without changing tested gameplay inputs (`56f1b3f6ae5a83dd5583deccee86c7dd4b258581`).
- [ ] Complete terminal `issue.json`, move the entire capture to `SceneIssues/closed/`, and push the separate resolution commit.
- [ ] Promote the integrated terminal branch to `master` non-force and verify terminal remote `master` state.
