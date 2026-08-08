# Contract: Shape Program

**Feature**: [../spec.md](../spec.md) · **Research**: [R-003](../research.md)
**Version**: 1

A shape program is a flat array of integer opcodes. Given resolved parameters it emits
**primitives**, never voxels. This indirection is what makes a feature evaluable over an arbitrary
sub-volume (FR-008), which is what makes region-local generation possible.

## Evaluation semantics

```text
Evaluate(program, parameters, origin, orientation, terrainHeightFn) -> Primitive[]
```

1. **Pure.** The same inputs produce the same output on every platform. No float, no allocation,
   no global state, no clock, no ambient randomness. Any randomness comes from a seeded integer
   stream derived from the instance id.
2. **Total.** Every parameter combination inside the declared ranges produces a valid primitive
   list. Validation proves this ahead of time; the evaluator does not check at runtime.
3. **Bounded.** A program emits at most `MaxPrimitivesPerInstance` primitives. Exceeding it is a
   validation failure, not a runtime truncation.
4. **Footprint-respecting.** Every emitted primitive lies inside the declared footprint,
   translated to `origin` and rotated by `orientation`.

## Register model

A program has a small fixed register file of `int` values. Registers are initialised from the
resolved parameters, plus:

| Register | Meaning |
|---|---|
| `R_BASE` | Base plane altitude, chosen by the definition's `BasePlaneRule`. |
| `R_SEED` | Instance seed, for within-instance variation. |
| `R_SLOT` | Slot index when evaluated as a composed child. |

## Opcode set

Operands are integers or register references. Coordinates are in voxels, local to the instance
origin before orientation is applied.

### Emit

| Opcode | Operands | Emits |
|---|---|---|
| `EMIT_BOX` | min(x,y,z), size(x,y,z), material, mode | Axis-aligned box |
| `EMIT_CYLINDER` | centre(x,z), baseY, radius, height, axis, material, mode | Cylinder on a cardinal axis |
| `EMIT_PRISM` | min(x,y,z), size(x,y,z), profile, material, mode | Extruded profile (gable, shed, arch) |
| `EMIT_CAPSULE_CHAIN` | pointCount, radius, material, mode | Tunnel segment chain, points from registers |
| `EMIT_RAMP` | min(x,y,z), size(x,y,z), direction, material, mode | Wedge, for stairs and terrain skirts |

`mode` is `Fill`, `Carve`, or `FillIfEmpty`.

### Control

| Opcode | Operands | Effect |
|---|---|---|
| `REPEAT` | count, strideX, strideY, strideZ, bodyLength | Repeats the next `bodyLength` opcodes with a translation |
| `IF_RANGE` | register, min, max, bodyLength | Executes the body when the register is in range |
| `PUSH_TRANSFORM` | offset(x,y,z), rotation | Pushes a local frame |
| `POP_TRANSFORM` | — | Pops |
| `CALL_SLOT` | slotIndex | Evaluates the definition bound to a composition slot |

Control flow is **structured and bounded**: no jumps, no loops with computed trip counts, so a
program's maximum primitive count is statically computable. That is what makes validation able to
prove boundedness rather than test for it.

### Query

| Opcode | Operands | Effect |
|---|---|---|
| `SAMPLE_GROUND` | destRegister, offsetX, offsetZ | Terrain height at a footprint-relative point |
| `DRAW_RANGE` | destRegister, min, max | Seeded integer draw from the instance stream |
| `SET_ANCHOR` | anchorIndex, x, y, z, facing | Records a resolved anchor |

`SAMPLE_GROUND` is the only opcode that reads the world, and it reads a pure function of position,
so it stays region-local.

## Prohibited by construction

- Reading the brickmap. A program cannot see voxels, including those written by other features.
  Interaction between features is resolved by precedence, not by inspection.
- Reading player alterations. Generation describes the unaltered world; alterations are an overlay
  applied afterwards (FR-033).
- Unbounded iteration, recursion outside `CALL_SLOT`, and any float operand.

## Versioning

`ShapeProgram.Version` accompanies the catalogue. Adding an opcode is a version bump. A world
refuses to load a catalogue whose version its evaluator does not implement, rather than silently
skipping unknown opcodes — a skipped opcode is a divergent world.
