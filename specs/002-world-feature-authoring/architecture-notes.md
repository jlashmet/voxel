# Architecture Notes: World Feature Authoring

Reasoning that outgrew [plan.md](./plan.md), and measurements taken during implementation.

---

## Why terrain had to change before anything else

`TerrainGenerator.SampleSurfaceHeight` reduced its inputs modulo the region edge:

```csharp
int rx = ((x % RegionEdge) + RegionEdge) & RegionEdgeMask;
```

Every region therefore held **identical terrain**. Every determinism test passed, because they
compared a region against itself — and terrain that repeats is perfectly deterministic. The bug
was invisible to the entire test suite because nothing compared *two different places*.

That mattered more for this feature than for the engine as it stood. Placement rules read ground
height and slope to decide where a village goes; on a tiling sampler, every region gets its
village in the same relative spot. The result is a world that is wrong rather than a world that
fails, which is the most expensive kind.

`TerrainSampler` is now the single terrain function, world-continuous and integer, and
`TerrainGenerator` forwards to it rather than carrying a second copy of the noise.

### What was deliberately not fixed

Terrain remains **brick-resolution**: a brick is uniform or empty, and the surface steps in 0.8 m.
Voxel-resolution terrain needs a mixed brick per surface column — roughly 4,000 pool slots per
region — and six existing test files call `Generate` with 4,096-slot pools.

The tempting fix was to allocate mixed bricks when the pool has room and fall back to uniform when
it does not. That is wrong for a reason worth recording: **pool capacity is tiered by device
class**. Terrain that depends on pool capacity is terrain that differs between a phone and a PC,
which is precisely what Constitution IV exists to prevent. Generation must not consult a budget
that tiering is allowed to move.

So `Generate` now allocates nothing at all, and voxel-resolution terrain is left to the streaming
work that can budget for it. `TerrainSampler` already answers at voxel resolution, so placement
and terrain adaptation are not limited by the generator's granularity.

---

## Where the float ban is actually enforced

The constitution names an analyzer rule. There is no analyzer in this project, and adding one is a
build-infrastructure change with its own scope. `IntegerOnlyGenerationTests` enforces the same rule
by scanning source text in `Core/Features` and `Core/Terrain`.

This is weaker in principle — it reads text rather than a syntax tree, and can be defeated by a
type alias or a generic — and equivalent in practice for the failure it exists to catch, which is
someone reaching for `Mathf.Sqrt` in a placement filter. It also carries a second test asserting
the guarded directories still exist, because a guard silently watching a renamed directory reports
success forever.

---

## Measurements

*(Recorded as milestones complete. Empty until T058.)*

| Measurement | Target | Observed | Task |
|---|---|---|---|
| Feature generation per region | 8 ms | — | T058 |
| Candidates scanned per region | ≤ 512 | — | T058 |
| Primitives per region | ≤ 4096 | — | T058 |
| Authoring time for a new feature type | < 30 min | — | T111 |

---

## Judgement gates

Two tasks are deliberately not code, because two of the plan's risks are "the tests pass and it
looks wrong".

**T040 — is parametric output good enough?** Verdict: *not yet taken.* Requires US1's evaluator.

**T066 — does terrain adaptation look right on real ground?** Verdict: *not yet taken.* Requires
US3.

---

## Open questions

- **Ownership lifetime.** Instance state is session-scoped by inheritance from the existing
  persistence decision, so a player who claims a castle loses it when the session ends. Recorded
  as plan risk 6; it is a product question, not a technical one.
- **Cave portal aesthetics.** Portals anchored to a lattice may read as grid-aligned. Jitter and
  depth-varying probability are planned (T082); whether that is enough is a look problem nobody
  has seen yet.
