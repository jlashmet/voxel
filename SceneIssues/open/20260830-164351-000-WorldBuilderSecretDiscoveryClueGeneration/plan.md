# Plan — WorldBuilder Secret Discovery Clue Generation

## Defect / acceptance

WorldBuilder needs deterministic `Secret -> Route(s) -> Clue(s)` planning with stable identity, semantic pre-solve evidence, explicit bypass policy, reusable route/discovery integration, and production-quality built-player proof. There are no original captures/marked regions. `issue.json` also requires representative SecretDiscovery examples in `WorldbuildingGalleryShowcase`.

## Hypotheses / material results

- The missing planning layer is implemented: stable IDs, deterministic scoring/tie-breaking, semantic anchors, route identity, discovery idempotence, clue-channel/count rules, and explicit bypass semantics have behavioral coverage.
- Focused `Assets/Game/WorldBuilder/Validation/SecretDiscovery` already proved production cave authoring/rendering/destruction; exact run `33801222778` was green and showed 35 sparse fracture voxels before a 607-voxel production breach.
- Current module ownership is repository-compliant: CaveWorldBuilder, Showcase, and WorldBuilder each own their focused EditMode surface and production-path validation scene.
- Exact run `33835125556` isolated Showcase validation seed drift; C# and serialized validation now use production Gallery seed `0x5EED1234`.
- Gallery camera/framing and missing-authoritative-geometry hypotheses are falsified. Post-bake secret authoring needed bounded content-dirty publication, and the Showcase regression now matches the production Gallery radius/BrickPool tier.
- Exact run `33842982484` showed the remaining authored-breakable void frame was captured while the production renderer was still cold (`visible=48`, `missingMax=647`), then continued converging. The fixed wall-clock delay was therefore an invalid readiness predicate.
- Exact run `33844103873` on source `54bc99fb18524ebb007be081fb9bde858e8b2b6e` selected the correct modules but failed compilation because the new evidence harness directly imported `VoxelEngine.Rendering.Runtime`. Showcase already referenced that assembly; inspection instead falsified direct runtime access as the intended boundary. `RenderingComposition` explicitly owns application-facing renderer diagnostics while keeping `VoxelRenderBridge` private.

## Selected fix / remaining gates

Keep reusable secret/cave/clue behavior in production modules and Gallery-specific placement/presentation in Showcase composition. The bounded post-bake publication remains the product fix. For SceneIssue capture synchronization, pin the production Gallery camera, reset application-owned renderer diagnostics through `RenderingComposition`, and require two consecutive frames with nonzero visible solid chunks and zero missing visible solid chunks before the breakable capture. No renderer-internal budget mutation or new module contract is required.

Run fresh exact-SHA targeted CI from the current feature head. Require CaveWorldBuilder, Showcase, WorldBuilder module tests/scenes, Kentridge integration, and exact SceneIssue replay. Inspect full-resolution built-player captures; `WorldbuildingGalleryShowcase` must visibly show understandable natural and breakable clue language at production quality. Only then complete closure bookkeeping, integrate then-current master, open the final PR, enable auto-merge, and monitor the required `affected` gate through merge.
