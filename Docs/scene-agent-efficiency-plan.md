# Scene-agent efficiency plan

## Goal

Keep exact SceneIssue validation below the five-minute targeted-job budget without weakening the
semantic bake, behavioral assertion, saved-pose replay, or evidence gates.

## Scope and constraints

- Keep targeted CI under its five-minute job budget.
- Use the shared targeted-test workflow for exact saved-camera replay rather than per-issue workflow
  files.
- Preserve deterministic integer world output: bake optimizations must prove byte-identical output.
- Do not reduce the eight-region startup image or weaken behavioral/visual assertions.

## Observed behavior and hypotheses

Four recent cold saved-pose requests took 363–378 seconds. Each spent 200–208 seconds baking, 36–38
seconds in the requested test, and 109–125 seconds building/running the player. Cache hits cost under
one second, but the key hashes all rendering and composition sources. A 30-second replay already
produced the required two settled frames at 15.7 and 25.7 seconds.

- **H1:** presentation-only changes unnecessarily invalidate the semantic bake. Discriminator:
  change a rendering-only fixture and prove the semantic fingerprint remains stable while a world
  authoring fixture changes it.
- **H2:** cold generation is serialized across regions. Discriminator: compare bake bytes and
  elapsed time between the existing path and a bounded pipeline that overlaps independent height
  jobs while committing regions in the same order.

Results: H1 confirmed by inspection: the key includes all `Assets/VoxelEngine` and
`Assets/Scenes/Showcase`. H2 confirmed: `MaterialiseStartupDisc` calls
`GenerateRegionBlocking` sequentially and each region immediately waits for its height job.

## Acceptance criteria

- [x] Semantic bake key excludes rendering/tests and changes for tested authoritative inputs.
- [x] Static saved-pose replay defaults to 30 seconds; explicit 20–60 second requests remain valid.
- [x] Development players are cached outside the checkout by exact build-input fingerprint.
- [x] SceneIssue builds omit automatic profiler connection while retaining replay instrumentation.
- [ ] The cold baker pipelines independent height work and produces byte-identical output.
- [ ] Focused tests, exact targeted CI, final diff review, and origin promotion pass.

## Selected fix and remaining gates

Use explicit versioned manifests for semantic-bake and player-build fingerprints, a runner-local
atomic player cache, a 30-second default, and a bounded height-job pipeline that preserves ordered
storage mutation. Add source-level contract tests for routing plus behavioral byte-equivalence and
cache-key tests. Shell cache contracts, YAML parsing, `git diff --check`, and the complete offline
assembly compile passed. Remaining gates are the Unity byte-equivalence test, a measured cold exact
replay, final diff review, and safe master promotion.
