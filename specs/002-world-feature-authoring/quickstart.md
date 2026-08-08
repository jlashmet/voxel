# Quickstart: World Feature Authoring

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

## The one idea

A region generates its slice of the world knowing only the seed, the catalogue, and its own
coordinates. Regions stream in any order, get evicted, and regenerate — so nothing may depend on a
neighbour, an accumulated structure, or a global pass.

Everything else in this design follows from that sentence. If a proposal needs to know what
another region did, it is wrong for this codebase, however natural it looks.

## How a castle gets into the world

```text
1. hash(seed, "castle", cellCoord)  ->  is there a castle in this cell? where? what parameters?
2. Region asks: which cells within a castle-footprint of me could reach into me?
3. For each, recompute step 1. Same hash, same answer, on any machine, in any order.
4. Evaluate the shape program with those parameters  ->  a list of boxes, cylinders, prisms.
5. Clip those primitives to this region's bounds. Rasterise into the brickmap.
```

Four regions each run this and each get their own quarter of the castle. They never talk. The
seams line up because both sides computed the same primitives from the same numbers.

## Why primitives instead of voxels

A shape program emits *primitives*, not voxels. That indirection buys three things:

- **Sub-volume evaluation is cheap.** Generating one region's sliver of a castle costs the
  primitives that overlap it, not the whole castle.
- **The far field gets something to draw.** Primitives rasterise at any resolution, so distant
  castles appear without materialising any voxels.
- **Validation can prove things.** Because control flow is bounded, the maximum primitive count
  and maximum extent are computable ahead of time rather than discovered at runtime.

## Where to look

| Question | File |
|---|---|
| What is being built and why | [spec.md](./spec.md) |
| Why the design is shaped this way | [research.md](./research.md) |
| What the data is | [data-model.md](./data-model.md) |
| What a designer authors | [contracts/catalogue-format.md](./contracts/catalogue-format.md) |
| What a shape program can do | [contracts/shape-program.md](./contracts/shape-program.md) |
| What the engine surfaces guarantee | [contracts/module-interfaces.md](./contracts/module-interfaces.md) |
| Numbers | `specs/001-destructible-voxel-engine/device-matrix.md` (authoritative) |

## Rules that will bite you

1. **No float anywhere in generation.** Not in the catalogue, not in placement, not in the
   evaluator. Probabilities are integers out of 65536. A float that reaches generation is a
   cross-platform divergence that no single client can detect.
2. **Declared footprint is a promise, not a hint.** It bounds the neighbourhood scan for every
   region in the world. Content outside it is a validation failure.
3. **Features cannot see each other or the brickmap.** Contested space is settled by precedence.
   A program that inspects what is already there would depend on generation order.
4. **Order independence is the acceptance test.** Generate a block of regions in shuffled orders;
   the worlds must be byte-identical. This catches almost every mistake this design can make.
5. **Shape is derived; only ownership and protection are stored.** If you find yourself wanting to
   store where something is, re-read rule 1 of the one idea.

## First run through the code

Milestone order in [plan.md](./plan.md) is also the reading order:

1. `ShapeProgram` + `PrimitiveRasteriser` — a definition stamped at a fixed coordinate.
2. `PlacementLattice` + `CandidateScan` — where things go, and the order-independence test.
3. `TerrainAdaptation` — meeting the ground without a step at region borders.

## Running anything in Unity

Use `tools/unity-run.sh`. Never invoke the Unity binary directly, and check whether an editor is
already open — the wrapper will refuse, and it refuses for a reason recorded in `CLAUDE.md`.

Note that batchmode play-mode tests do not exercise the editor lifecycle. Anything touching
`OnEnable`, domain reload, or GPU resource lifetime needs an EditMode test that loops the
lifecycle.
