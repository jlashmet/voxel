# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest steering: prioritize startup castle/house time-to-visible alongside FPS; imperfect water appearance is acceptable for now.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid source steps1/2/4/8 have GPU implementations. GPU water is active and its CPU cache/job/shader path is deleted. Full solid retirement, production coverage and visual acceptance remain open.

## Current retirement and lifetime work

`f3dd24bea` removed the CPU arena/draw path;468 rendering tests, material regression and both
players passed. Showcase238/142 FPS stationary/walking, CPU4.06/7.065ms,330 missing-visible,
zero allocation failures/evictions. No meaningful speed gain from arena retirement; terrain/far
finish remains unacceptable. Module memory262MB;19 finalizer warnings persisted.

The unused CPU workspace and obsolete allocation tests are deleted; meshing regressions await GPU migration.

Shutdown stopped deferred water disposal callbacks. Suspected omitted batch releases were falsified; no batch leak established.

Selected fix: water stays subscribed to Application.quitting until physical resource release;
only that exit handler drains readbacks after logical disposal. Frame/scene-change disposal
remains asynchronous.466/466 rendering tests pass(27s). Both new exit tests fail without the
drain. Module48s/seven captures and Showcase180s/12 captures pass, buffer warnings19→0.
Module frame p95/p99 5.680/6.821ms. Showcase229/142 FPS, CPU4.29/6.995ms,315 missing,
zero allocation failures/evictions. Screenshots retained castle/fort; terrain/far finish still
unacceptable. No speed gain claimed; exact sources/hashes accompany both players.

## Startup performance next

User reports castle/house fill-in much slower than CPU; prioritize actual time-to-visible and FPS.
No hidden content/loading-screen workaround. CPU world generation stalls first frame14.9s;
render requests begin15s, first publication19s, near publications462 by26s. Measure generation
and renderer fill-in separately.

H1: nearby discovery is skipped on initial camera placement, leaving resident background order
in control. Confirmed code only calls immediate camera discovery after a move with a previous
window. H2: global one-stage-per-frame extraction/coarse summaries limit subsequent throughput.

Selected fix: queue initial camera region then resident neighbours before the background sweep;
preserve distant demand and avoid duplicate initial scans. Two behavioral tests failed before;
468/468 rendering tests pass after. Module48s/seven captures passes all lifecycle/edit/far checks.
Showcase45s/30 captures: generation14.9s unchanged; first logged publication after generation
~5.1→2.1s, previous462-near milestone ~11.1→9.1s. One run, coarse1s logs and different capture
cadence: not full readiness or steady-FPS acceptance. Reviewed18.3/24.3/44.4s; castle still fills
incrementally, terrain/far quality unacceptable. Exact sources/timing script retained.

Next: reduce source coverage/recovery and GPU queue/stage latency without increasing frame
budgets or unsafe queue depth; profile generation separately. Latest full-run FPS229/142 precedes
this scheduling change. Current-source repeated full benchmark/integration remain. Dead-helper
cleanup is secondary; CPU oracles/bridge and retained-profile regression remain outstanding.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
