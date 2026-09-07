# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, physically delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable result under [tasks.md](tasks.md). Preserve authoritative CPU storage/generation/collision/simulation and GPU host orchestration. No hidden content, weaker budgets or reduced distance. Latest user steering: accept imperfect water appearance for now and prioritize performance.

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, base `73989d7ac`. Local harness/tests/screenshots authorized. Last requested push: `origin/fixes/agent-1` at `64b2921a3`; current work stays local.

## Retained results

All solid source steps1/2/4/8 have GPU implementations. GPU water is active and its CPU cache/job/shader path is deleted. Full solid retirement, production coverage and visual acceptance remain open.

## Current solid CPU retirement

`8930403d0` ports GPU faceted merging.57 focused tests and both module/Showcase players passed.
Showcase measured236/140 FPS stationary/walking, CPU4.095/7.00ms; allocation failures/evictions
fell880/880→0/0 and missing-visible631→340. Castle retained, terrain holes/far/vegetation finish
unacceptable. Single-run diagnostics, not benchmark acceptance.

H1: legacy CPU workspace/upload ownership still reserves resources and performs host work despite
GPU publication. H2: source recovery and GPU submission latency dominate moving-camera stalls;
last step8 publication waited19.5s even without geometry allocation failures.

Selected retirement: remove CPU meshing phases, snapshot/pin jobs, managed polygon/profile/coating
emitters and worker workspace allocation from the mixed cache. Preserve bounded demand queues,
clipmap ownership, source/catalogue version rejection and GPU handle publication. No source-data
changes. Backend creation failure cannot launch CPU geometry; temporary source admission failure
keeps demand pending. Remove the obsolete environment fallback switch and startup policy.

Current result: about2,600 lines removed;53 host tests passed. Full rendering assembly audit
initially463/484; retired14 CPU-source wiring cases plus one CPU step4 oracle in favor of real
GPU coverage, preserved shared Storage assertions and corrected stale architecture expectations.
All469 remaining owned tests passed (22s, no skips). Module48s/seven captures passed lifecycle,
edits and far handoff, zero missing/fallback. Allocated memory261.6 versus687.3MB; fort intact,
prototype composition. Both prior/current logs contain19 ComputeBuffer finalizer warnings:
G11 remains open. Exact source evidence is in `gpu-solid-host-module`.

Next: remove Entry upload helpers, scheduler335MB CPU arena/contiguous draw route and rename host
ownership. Delete standalone CPU workspace/jobs/oracles with independent GPU regression coverage.
Add direct GPU retained-profile suppression/backing regression before removing its last CPU
predicate oracle. Then run Showcase and measure full frames again; latest FPS remains236/140.

## Remaining gates

Solid CPU renderer removal, GPU coverage/visual fidelity, G11 retirement/error policy, lifecycle/pressure/edit validation, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete; no completion claim.
