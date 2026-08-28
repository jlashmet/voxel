# Experiment 017 — ramp exact-snapshot admission

## Question
Can smoothing new solid-build admission reduce the measured exact-snapshot player-frame spikes without hiding visible geometry or degrading convergence?

## Evidence before the experiment
Per-frame profiling identified `Voxel.Surface.Snapshot` as the dominant synchronous worker overrun. A test-only 12-to-8 in-flight comparison reduced snapshot/worker spikes but did not pass the unchanged traversal gate, so concurrency was an amplifier rather than a proven root fix.

## Variant A — completion-count ramp
Production commit `36eeeace938f801063bd0aa6d57074c3bacaf9b2` exposed at most `previous RunningSolidJobs + 1` converging build slots.

Exact request on source `a15d241c32f4bbbf8809178df516c1582c5fc2dd`, workflow run `33094983575`, job `98597415661`, failed the behavioral regression at movement frame 5 because `VisibleSolidChunks == 0`. The same artifact's settled replay later ran roughly 238–400 FPS. This variant reduced burst pressure but could collapse toward one slot when short jobs completed between frames, starving near-field publication.

## Variant B — explicit monotonic ramp
Commit `203788c90ee0ab82d9fed5a1d9dfb317c0d039d8` kept explicit ramp state, starting with two slots and exposing one additional slot per rendered frame while previous metrics reported missing visible solids.

Exact request `agent-2-192751-final-monotonic-ramp-20260827-1017` used source `684dd2b42791958f5d6b69aaaf1ef8e60a7b3c9a`; workflow run `33097677149`, job `98606760823`. Coverage no longer failed early, but the unchanged traversal gate remained red: p95 **20.73 ms**, p99 **25.10 ms**, max **94.36 ms**. Its saved-pose replay settled around roughly **97–119 FPS**, materially worse than Variant A and prior settled runs.

## Decision
Reject scheduler-admission tuning as the closing production fix. Commit `a737f6aadea0bedef2dc5afe3394b238bb6fddca` restores the pre-ramp `VoxelRenderPass` admission behavior so the feature branch does not retain either known regression.

The evidence supports the architectural next step already documented in `gpu-v2-next-step.md`: remove the CPU exact-snapshot boundary for a narrow GPU-supported near-field path using the existing GPU Transvoxel backend and persistent brick-mirror concepts. Do not relax the traversal or coverage assertions.

## Blast radius / cost conclusion
Both attempted changes touched shared solid-render admission only, but their runtime trade-off is unacceptable: one risks visible starvation; the other increases traversal time and cuts settled replay throughput. The production code is therefore restored before further work.
