# Plan

## Observed behavior / acceptance
- The canonical production path remains `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; no parallel structural solver will be introduced.
- Core composition, validation, deterministic graph/hash identity, bounded recursion/cost, authoritative descendant rasterization, support metadata, decoration handoff, and the four proving catalogues are implemented with focused regressions.
- Exact run `33314706183` is mechanically green on source `945ec3fe05798096e6177b1eba1a213be4b47e7b`: the focused PlayMode regression passed, the standalone player passed all three production `CharacterMotor` traversals, all four proof metrics were valid, and eight full-resolution structural frames were emitted.
- Manual full-resolution art review does **not** satisfy final visual acceptance yet. `AGENTS.md` requires shipped/AAA-quality critique rather than placeholder-box approval. The bridge wide view does not convincingly show a substantial gorge/river between terrain masses; the cliff context is visually shallow; castle treatment is still primitive; and the facade/roof variants read as raw box masses rather than reusable high-detail architecture treatment.

## Selected rework
1. Preserve solver/runtime contracts, budgets, stable socket IDs, and authoritative child composition unchanged unless a visual fix genuinely requires catalogue geometry.
2. Reuse existing authoritative voxel architecture/detail helpers where available; do not add presentation-only meshes/GameObjects or a second composition system.
3. Improve the bridge proof/capture so a player-height and wide establishing view clearly reads as a monumental crossing with grounded abutments/piers and meaningful terrain/water/gorge context.
4. Improve castle, cliff settlement, and facade/roof proving geometry with intentional hierarchy, silhouette, material separation, and repeated architectural language while keeping meso-scale typed sockets structural rather than micro-detail attachment points.
5. Reframe the existing built-player audit captures to prove the required seams and context without sparse/unloaded-looking terrain dominating the frame.
6. Re-run the same focused PlayMode + exact built-player targeted-CI transport from the final exact feature SHA, then inspect every durable full-resolution structural frame again before accepting the visual gate.

## Current evidence / cost baseline
- Run `33314706183`: bridge planning `0.014 ms`, castle planning `0.005 ms`; bridge `5 children / 12 primitives / 6,073,200 conservative voxels / 3 regions`; castle `4 / 11 / 5,072,000 / 3`; cliff `3 / 6 / 2,537,600 / 6`; facade/roof aggregate `8 / 22 / 10,923,840 / 3`.
- Total proof authoring was `895.63 ms`; `20 children`, `51 primitives`, `24,606,640` conservative voxel budget, `15` visited regions, `40` rasterized instances, `15,907,368` written voxels, `2,622,212 KiB` reported allocation model, and `63,629,472` render-proxy triangles.
- Traversal evidence is mechanically green: bridge `115 m -> 1.25 m`, gate `17.6 m -> 1.223 m`, cliff vertical route `42 m -> 1.317 m`.
- Treat these as a baseline only; final cost/visual acceptance must be re-measured after content rework.

## Blast radius guardrails
- Keep changes scoped to this gallery proof, its reusable authoritative-voxel presentation/detail helpers, and its existing audit/capture path.
- Do not raise composition ceilings, scan budgets, `CharacterMotor` tolerances, global terrain/ramp semantics, or unrelated generation/render behavior.
- Do not edit `.github/test-request.json` on `fixes/agent-5`; use only the existing `ci-test/fixes/agent-5` transport after the feature head is ready and no prior request is active.
- Only after a green exact-SHA regression + built-player run **and** successful full-resolution art review: finish cost/blast-radius evidence, complete pending metadata, move open -> pending -> closed with `status=fixed` / `resolvedUtc`, merge current `origin/master`, and non-force push the exact feature head to `origin/master`.