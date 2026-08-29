# Plan — Kentridge vegetation meadow density

## Scope
Implement only `20260829-015100-000-KentridgeVegetationMeadowDensity` on `fixes/agent-5`. Do not edit scene serialization or `.github/test-request.json` on the feature branch.

## Confirmed current architecture / evidence
- `KentridgeDefinition` exposes additive per-region ecology policy: allowed vegetation, density, deterministic seed, meadow radius, route clearance, slope controls, exclusion classes, and ambient-animal allowlist.
- `KentridgeRegionLife` realizes policy through production surface sampling; no scene-local grass GameObjects or hand-authored scatter coordinates.
- Packed procedural grass expands each semantic grass instance deterministically to 5–15 blades; renderer chunks at 36,000 blades.
- Runtime diagnostic from the built player measures 11,478 semantic grass instances / 114,580 blades total; the connected primary meadow alone measures 5,777 instances / 57,589 blades, 8 grass mesh chunks total, and zero excluded-surface leakage.
- Camera replay metadata is corrected to the current root `Kentridge Player Camera`. Exact-SHA run `33242524673` now passes focused PlayMode, launches the real player, pins the meadow camera, and uploads the expected artifact.
- Mandatory human inspection of that green run disproves the earlier assumption that source-level `_GrassTime` plumbing is sufficient: stationary meadow frames around 29.7 s, 39.7 s, 49.7 s, and 59.7 s show unchanged grass silhouettes/pixels. Visible wind therefore remains a real product defect and blocks acceptance/closure.

## Remaining discriminator / implementation
1. Trace the shared packed-grass render path end-to-end: shader deformation input, `ProceduralVegetationMaterials.ApplyGrassState`, batch-render call frequency, Unity scaled/unscaled time behavior, material/property publication, and built-player shader binding/inclusion.
2. Determine why time-varying wind state does not change rendered blade vertices between stationary frames. Fix the smallest reusable rendering/material/shader seam; do not add a Kentridge-only shader, second animation system, per-frame mesh rebuild, per-blade GameObjects, or per-frame material allocation churn.
3. Add a focused regression that proves wind state advances independently of vegetation population/rebuild and remains deterministic where appropriate.
4. Re-run required validation for the changed shared-rendering module plus Kentridge acceptance. Only after the feature SHA is final, submit one fresh targeted-CI request via `ci-test/fixes/agent-5`.
5. Inspect the exact built-player artifact manually. Require dense player-height meadow coverage, primary meadow `>=3000` rendered blades, zero invalid-surface leakage, and at least two time-separated stationary frames with plainly changed blade silhouettes/poses.
6. Record final cost/blast-radius evidence exposed by the canonical harness, store durable human-inspectable evidence, complete all acceptance/checklist items, then perform pending/closed metadata and promotion strictly in repository order.

## Blast radius / cost
WorldBuilder API changes are additive; non-Kentridge callers keep existing behavior. Runtime ecology changes are confined to Kentridge realization plus one shared deterministic blade-count helper used by the existing renderer. The wind repair may touch the shared packed procedural-grass rendering path, so its blast radius includes every caller of that grass renderer; the implementation must preserve existing batching, avoid new per-frame allocations/material creation/mesh rebuilds, and keep blade deformation on the GPU. The replay correction is assignment-local metadata with zero production runtime cost. Existing built-player evidence is approximately 110 FPS at 114,580 rendered blades; the final post-fix capture must be checked for regression against that available metric.
