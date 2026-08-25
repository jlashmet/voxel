# SceneIssue 033053 — tree collision and shooting

## Goal

Restore the VoxelShowcase tree gameplay contract: nearby trees must block player movement and the
same authored trees must participate in the shooting/destruction path so a valid shot can break
them.

## Scope

- `Assets/Scenes/VoxelShowcase.unity` tree population and the shared runtime tree representation it uses.
- Player/world collision registration for showcase trees.
- Projectile/hitscan damage/destruction registration for showcase trees.
- A focused deterministic regression that guards both behaviors without depending on screenshot pixels.

## Constraints

- Work only on capture `20260825-033053-588-VoxelShowcase` on `fixes/agent-6`.
- Preserve the original screenshot and all capture metadata; the capture has no circles, so the whole frame is evidence.
- Do not edit `.github/test-request.json` on `fixes/agent-6`; use only `ci-test/fixes/agent-6` for targeted CI requests.
- Record every replay, diagnostic, test, and fix attempt immediately as a numbered experiment Markdown file.
- Commit production/test changes before the separate terminal issue-bookkeeping commit.

## Initial evidence

- The capture note reports two regressions at once: the pictured trees do not break when shot, and all trees no longer collide with the player.
- Because rendering still shows the trees while two independent gameplay interactions are absent, a shared tree runtime/authoring registration path is a stronger starting hypothesis than a visual-generation failure.
- The captured scene is `Assets/Scenes/VoxelShowcase.unity`; there are no circled subregions, so the issue is behavioral rather than a localized pixel artifact.

## Acceptance criteria

1. Identify the concrete runtime representation used by VoxelShowcase trees and the proven reason it is omitted from collision and/or damage handling.
2. Add a focused regression that executes a nonzero test count and proves a representative showcase tree has player-blocking collision and can receive/destruct from a shot.
3. Apply the smallest production fix that restores both behaviors without converting unrelated foliage or distant presentation-only instances into gameplay objects.
4. Targeted `ci/single-test` validation passes on `ci-test/fixes/agent-6` for the exact feature commit containing the fix.
5. Replay verification of the original capture is recorded after the fix, with provenance and a clear result, before the capture is closed.

## Work

- [x] Read `CLAUDE.md`, `AGENTS.md`, `SceneIssues/README.md`, the manifest, note, capture metadata, and circle state.
- [ ] Trace VoxelShowcase tree creation, collision registration, and shooting/destruction handling.
- [ ] Record the root-cause experiment before changing production behavior.
- [ ] Add the smallest direct regression for the proven invariant.
- [ ] Implement the minimal production fix.
- [ ] Push production/test work and validate the focused regression through targeted CI.
- [ ] Replay-verify the original capture and record final evidence.
- [ ] Review the final diff, complete issue bookkeeping, move the entire capture to `SceneIssues/closed/`, and push the separate resolution commit.
