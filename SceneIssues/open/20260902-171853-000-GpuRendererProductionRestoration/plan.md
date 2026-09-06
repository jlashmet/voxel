# GPU renderer production restoration

## Objective and acceptance

Deliver production-quality `Assets/Scenes/VoxelShowcase.unity` through GPU rendering, delete retired CPU-only rendering, and pursue **1,000 FPS / 1.00 ms whole frame**, or the closest repeatable measured result under [tasks.md](tasks.md). Preserve CPU authority and GPU host orchestration. No hidden content, weaker budgets, reduced distance or permanent CPU fallback. User wants the full GPU path before additional optimization work.

## Retained evidence

Worktree `/private/tmp/voxel-gpu-restoration`, branch `gpu-rendering-agent-1-resume`, HEAD `b07488645`. Local harness/tests/screenshots authorized; last requested push reached `origin/fixes/agent-1` at `64b2921a3`. Current work stays local.

All solid LOD steps have GPU implementations, including conditional step4 thin-feature preservation. A separate coarse classification mismatch is repaired and verified by 35 tests. Directory reclamation/collision fixes preserve the previous GPU-byte ceiling, but large coarse requests still lack bounded source streaming. Latest Showcase completed 180s/11 captures: reviewed 75.2s/150.2s **unacceptable**, grey distant houses and incomplete frontier. Full results in [tasks.md](tasks.md). Water and legacy CPU renderer removal remain open.

## Current far-material repair

H1: grey houses are retained far proxies whose material identity was collapsed. H2: missing GPU coverage alone explains the appearance. Inspection finds `GeometryFor` retains all additive primitives while `PresentationFor` selects only the first primitive’s material. A wall/roof bake regression confirms one drawable surface instead of two (`far-material-slots-before.xml`, one failed, exit 2). Coverage pressure can retain these proxies, but cannot restore their discarded material separation.

Selected repair: immutable render-ready geometry carries resolved presentation slots; each primitive selects a slot. Composition groups matching material/style/coating identities. Rendering builds submeshes and both ordinary/command-buffer instanced draw paths submit each slot with its resolved material. No game material vocabulary enters Rendering.Api, no authoritative state changes, no extra world residency, no CPU voxel extraction.

Verification: 31 far geometry/presentation/handoff tests passed; strengthened four-test presentation suite proves grey-wall/red-roof values and material sharing across geometry identities. Rendering module completed 34s/nine captures; composition module 28s/seven captures. Reviewed 32s far fixtures and 24s generated composition remain prototype/blockout quality. Showcase completed 180s/11 captures/exit 0; reviewed exact 75.2s restores brown roofs versus prior grey houses, confirming H1 for that material loss. Reviewed 75.2s/150.2s remain unacceptable: simplified architecture, terrain and incomplete castle/frontier coverage. Final 62 step4/three step8 publications, 612 missing-visible and 4,659,051 directory refusals.

Next isolate a source footprint that exceeds the mirror’s protected capacity and implement bounded GPU-summary streaming. Material separation does not solve source/summary pressure or world lifetime. Keep distance/selection policy unchanged.

## Remaining gates

Finish coverage and visual fidelity, migrate water, delete CPU-only rendering/oracles, and complete G11 retirement/error policy. Validate edits, pressure, lifecycle, module/integration players and repeated frame/memory workloads. G01–G27 remain incomplete. Full GPU completion precedes broader optimization.
