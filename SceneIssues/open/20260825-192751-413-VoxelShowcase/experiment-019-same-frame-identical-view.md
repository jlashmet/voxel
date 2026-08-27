# Experiment 019 — same-frame identical-view preparation

## Hypothesis
The unchanged traversal gate includes an automatic player-frame render followed by explicit `camera.Render()` in the same Unity frame. If true, `VoxelSurfaceScheduler.Prepare` would advance world/change/build state once and then pay another full visibility sweep for the identical explicit render.

## Action / source
A provisional render-pass guard was implemented at `cd0f9b216734e7047fb8bc52ffd93aec37f3c396`: reuse the scheduler result only when `LastAdvancedFrame == Time.frameCount`, camera identity is unchanged, and position/rotation exactly match. Different cameras or changed same-frame poses still called `Prepare`.

## Discriminator
The PlayMode traversal is a coroutine. `yield return null` resumes on the next player frame, so the automatic render and the subsequent explicit `camera.Render()` are not reliably inside one `Time.frameCount` interval. That breaks the causal premise: the guard can optimize duplicate render calls in other situations without necessarily touching the timed traversal that is red.

The previous exact bounded cross-frame visibility reuse also failed at p95 23.40 ms, so there is no evidence that another visibility-cache variant closes this capture.

## Blast radius / cost
The provisional change was render-only and conservative, but an optimization that does not exercise the measured path is still unsupported production complexity.

## Result
Rejected before spending another targeted-CI request. `VoxelRenderPass.cs` was restored byte-for-byte to current `origin/master` in commit `2d6530018fdac5036f4e27aedffc763119a071ce`. No same-frame guard remains in the final candidate.
