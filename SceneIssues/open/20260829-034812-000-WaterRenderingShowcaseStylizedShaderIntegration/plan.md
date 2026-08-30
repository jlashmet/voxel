# Plan

## Goal / acceptance
Finish the resumed stylized-water feature with one reusable production renderer and exact built-player proof. Still, river, and waterfall must use canonical voxel storage/extraction plus one renderer-owned `Hidden/VoxelEngine/WaterSurface`; no scene-local water mesh/material fork. Built evidence must visibly show distinct motion and a convincing waterfall: downward flow, turbulence/aeration, irregular breakup, lip/edge/base foam, and mist/spray. `.github/test-request.json` stays off `fixes/agent-9`.

`SceneIssues/feature-readme.md` is absent; follow `AGENTS.md` and canonical `SceneIssues/README.md`.

## Current findings
- Pre-repair exact run `33323151755` passed focused tests/build/launch but visually failed the waterfall.
- Repaired exact run `33324084398` on source `3b3a55c...` also completed green. Direct review of its converged close waterfall frames still shows the cliff, upper lip, and lower pool but no readable falling vertical sheet. Measured post-startup windows were ~1.2–2.8 ms average frame time, ~697.8 MiB allocated, ~846–848 MiB reserved; FrameTimingManager CPU/GPU values remained unavailable.
- Framing is not the cause: the authored Cascade sheet sits ~2.3 m in front of the cliff and the square-on camera targets it directly.
- If Cascade geometry reaches `WaterSurface.shader` with its installed profile, `Cull Off`, vertical-facing opacity, and no fragment discard should make it visible.
- The water cache itself classifies from `WaterMaterialMask`; Cascade is gameplay-inert (`DestructionClass.None`) while lake/river are spreading. The remaining high-value discriminator is whether the shared solid extractor still classifies liquid/non-liquid from physical simulation semantics, causing inert Cascade to overlap as solid and win the depth test.
- Added arbitrary-ID/remap presentation regression on current branch; it still requires final exact-SHA validation.

## Hypotheses / next discriminator
1. **Primary:** solid extraction still includes presentation-water material `Cascade` because its physical simulation is inert. Overlapping solid depth suppresses the canonical water sheet. Fix must key renderer exclusion to shared presentation water classification, not change gameplay semantics.
2. **Secondary:** inert Cascade surface bricks fail water discovery/admission despite mask-based cache classification. Prove/falsify with focused production-path coverage before changing shader art again.

## Selected work / gates
1. Audit solid density/material classification and add a regression proving an inert presentation-water ID is excluded from solid geometry while remaining eligible for canonical water extraction.
2. Apply the smallest shared classification fix if hypothesis 1 is confirmed; keep Cascade gameplay inert.
3. Re-read/merge latest `master`, review blast radius including the `FlowerBlue = Cascade` alias, then freeze source SHA.
4. Submit one final canonical CI request on `ci-test/fixes/agent-9`; require green regressions/build/launch and directly inspect all exact-built frames plus production-scene evidence.
5. Only after A1–A17 pass: metadata, open → pending → closed, merge latest master, and non-force promote exact feature head.

## Cost / blast radius
Six 32-entry `Vector4` water tables cost 3,072 bytes. `Cull Off` can increase transparent fragments; final evidence must retain budgets and record measured frame/memory/draw implications. Any solid-extractor change must affect only materials installed as water presentation and must not alter collision/destruction/simulation semantics.
