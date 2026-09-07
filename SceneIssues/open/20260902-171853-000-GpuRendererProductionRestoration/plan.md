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

The standalone CPU workspace now has no production callers. Remove it and its obsolete sizing/
container-lifetime tests; preserve meshing/summary behavioral coverage until migrated to GPU.

H1: shutdown stops callbacks before deferred water disposal releases its six buffers and13-buffer
arena. H2: batch resource replacement omits disposal. Reading complete batch Dispose falsified
the suspected missing HLOD buffer releases; no replacement leak established.

Selected fix: water stays subscribed to Application.quitting until physical resource release;
only that exit handler drains readbacks after logical disposal. Frame/scene-change disposal
remains asynchronous.466/466 rendering tests pass(27s). Both new exit tests fail without the
drain. Module48s/seven captures and Showcase180s/12 captures pass, buffer warnings19→0.
Module frame p95/p99 5.680/6.821ms. Showcase229/142 FPS, CPU4.29/6.995ms,315 missing,
zero allocation failures/evictions. Screenshots retained castle/fort; terrain/far finish still
unacceptable. No speed gain claimed; exact sources/hashes accompany both players.

## Startup performance next

User reports castle/house fill-in much slower than CPU; prioritize actual time-to-visible, without
hiding content or adding a loading screen. Logs show first request around15s, first publication
around19s, near publication462 by26s; coarse requests still pending. H1: source discovery/recovery
and admission serialize readiness. H2: one global extraction stage per frame plus coarse summary
work limits publication throughput. Add bounded stage/queue timing and early player captures to
discriminate; preserve existing frame budgets and GPU queue safety. Dead-helper removal continues
only where it supports this work. Direct GPU profile coverage is still required before deleting
the retained CPU predicate; other CPU oracles and bridge remain.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
