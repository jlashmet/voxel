# Plan

## Goal / acceptance

Deliver one canonical production voxel-water presentation that uses reusable still/lake, river/stream, and waterfall/rapid semantics, materially adapts the imported Stylized Water package plus the approved `WaterfallReference.shader`, and proves the exact production path in a built `Assets/Scenes/WaterRenderingShowcase.unity` and existing game scenes. The showcase may author terrain/water bodies, select semantic profiles, and control inspection views; it must not own a second water renderer, bespoke production meshes, per-scene shaders/materials, or gameplay authority.

The resumed branch already implements the shared architecture: game material composition contains semantic still/river/waterfall rows; engine extraction classifies water from installed presentation data instead of game IDs; all exposed water faces retain per-vertex material identity; and the render pass owns one shared `Hidden/VoxelEngine/WaterSurface` material. The shader contains shallow/deep response, scene-depth contact foam, animated normal/detail motion, directional river flow, and dedicated waterfall turbulence/aeration/lip/edge/base/mist behavior. Those source changes are not sufficient to close the feature: the 17 acceptance criteria require production-authored portability coverage, a buildable showcase, exact-player visual/motion evidence, existing-scene proof, gameplay compatibility tracing, and measured cost.

## Workflow state

- Durable `plan.md` and `tasks.md` are maintained separately in this assignment folder.
- `SceneIssues/feature-readme.md` is absent on both the feature branch and current `master`; `AGENTS.md` and canonical `SceneIssues/README.md` govern the workflow.
- The prior branch refresh remains in history. On this resume, current `origin/master` was fetched and merged conflict-free into `fixes/agent-9` at `957b798940f008e07fde9ce27225046b2652da81`; master changes did not overlap the feature's water implementation.
- `.github/test-request.json` must never be edited on the feature branch. Exactly one final targeted-CI request may be made on `ci-test/fixes/agent-9`, for the exact final feature SHA, and queued/running CI must not be replaced.

## Hypotheses / discriminators

1. **One canonical renderer can serve every standard water body.** Current source tracing supports this because presentation installation produces a water-material mask and profile arrays consumed by the shared extraction/cache/render pass. Falsify by finding any normal scene/game path that binds a separate legacy water material or bypasses the installed catalogue.
2. **Presentation remains gameplay-neutral.** Still and river reuse the spreading-water simulation row while cascade remains deliberately inert, and rendering consumes presentation independently. Falsify by finding swimming/buoyancy/collision/spreading/streaming/discovery/edit/diagnostic logic that now depends on rendering profile classification rather than authoritative gameplay semantics.
3. **Vertical waterfall geometry can use the same production extraction path.** The current water mesher emits all exposed faces and carries material identity, so a voxel-authored cascade can produce vertical sheets without a plane renderer. Falsify with boundary/negative-coordinate tests or a built cascade that loses faces at brick/chunk seams.
4. **The shared shader survives a real player build.** Renderer-data serialization and active URP selection already reference the water shader, but stripping/compile/resource reliability remains unproven until the exact player build renders the scene without pink/missing materials.
5. **Package/reference behavior can be adapted without importing a second pipeline.** Use the package's wave/depth/foam/flow concepts and the ticket reference's waterfall motion/turbulence/aeration/edge/lip/base/mist character inside the existing URP renderer. Do not replace project URP assets or require editor asset lookup.

## Implementation sequence

1. **Finish path/gameplay audit.** Trace normal material installation/bootstrap ordering, all water classification/gameplay consumers, `VoxelShowcase`, a second production water consumer, and any legacy fallback. Add required findings/work to `tasks.md` immediately.
2. **Close extraction/binding regression gaps.** Add production-path tests for multiple independently authored water materials/profiles, all-face material preservation, reciprocal boundary suppression, negative world coordinates, and actual runtime shader/profile binding. Tests must exercise production code, not merely source strings or names.
3. **Build a thin production-authored showcase.** Add `Assets/Scenes/WaterRenderingShowcase.unity` plus only the minimal scene-side authoring/inspection harness needed to seed voxel terrain and standard water/cascade materials through existing storage/authoring APIs and hand them to the canonical renderer. Arrange broad/deep lake, shallow shoreline, directional river, waterfall/rapid, and cliff/rock/structure contacts. Provide deterministic near/wide/elevated inspection movement without creating a water-specific renderer or gameplay system.
4. **Register the scene in the normal build/harness path.** Preserve existing required scenes, target the assignment's build index 3, and avoid unrelated/project-wide URP changes. If the repository's build tooling has a narrower supported registration mechanism, use that rather than expanding scope.
5. **Validate exact built behavior.** Through the single final exact-SHA CI transport, run the required focused regressions and player-build/scene harness. Capture durable near/wide and time-separated motion evidence for each showcase case, explicitly compare the waterfall to `WaterfallReference.shader`, and launch `VoxelShowcase` plus the selected second production water scene to prove global replacement.
6. **Measure blast radius/cost.** Record actual built-player CPU/GPU/memory/draw/batching/culling/transparent-overdraw/shader-variant observations for large water and waterfall extras. Keep existing device budgets unchanged; the bounded six 32-row `Vector4` profile tables cost 3,072 bytes per installed catalogue.
7. **Close only after all gates.** Check every task and A1–A17 acceptance item, complete pending metadata on the feature branch, follow open → pending → closed workflow, merge current `origin/master` again, push the exact feature head, then non-force promote that exact head to `origin/master`. If master advances, fetch/merge/retry; do not self-select more work.

## Blast radius / cost guardrails

Expected production blast radius is limited to shared material presentation data, water extraction/cache/rendering, game material rows/tests, this assignment's scene/harness/build registration, and assignment metadata/evidence. Preserve existing water streaming/culling and a single render material; no per-water-voxel GameObjects, per-body unique materials, scene shader forks, pipeline replacement, or weakened budgets. Waterfall extras must remain shader/profile data on the same batching path.
