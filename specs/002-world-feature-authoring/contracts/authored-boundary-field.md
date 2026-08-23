# Contract: Authored Boundary Field

**Feature**: [../spec.md](../spec.md) · **Data model**: [../data-model.md](../data-model.md)
**Version**: 1

A primitive contributes two things to a voxel: **occupancy** (is this cell solid) and an
**authored boundary sample** (where inside the cell does the surface actually sit). Occupancy is
authoritative gameplay state. The boundary sample is presentation refinement — it moves a rendered
vertex within its cell and never changes what is solid.

This document states the rules that keep those two consistent. Every rule below was derived from a
measured failure, cited inline. Violating one does not produce a subtle regression; it produces a
visible staircase on any curved surface, which is the defect this contract exists to prevent.

## The invariant

> **An authored sample's sign must agree with the occupancy of the cell that carries it.**

`VoxelBoundarySample.SignedQ3` is positive inside solid and negative outside. For every cell:

```text
cell.IsSolid  ==  (cell.Boundary.SignedQ4 >= 0)
```

Guarded by `VoxelSurfaceArchitectureTests.CurvedPrimitiveRasterizationPreservesAuthoredBoundaryDistance`,
which asserts it across a whole primitive halo rather than at hand-picked cells.

Both sides enforce it. `PrimitiveRasteriser.RasteriseBoundaryHalo` refuses to write a contradicting
sample; `TransvoxelDensityJob.SampleField` and its HLSL mirror refuse to read one. The read side is
what makes trusting the sample safe everywhere else.

## Rule 1 — a shape's distance zero must coincide with its membership test

If `Contains(p, v)` admits a cell, `TryBoundaryDistanceQ4(p, v)` must be `>= 0` for that cell, and
negative for every cell it rejects. The two functions describe the same surface and must agree on
where it is.

This is the rule that was broken. `AnnulusContains` admits a centre when `r <= Radius`, while
`AnnulusDistanceQ4` biased its radial terms by half a cell and put the zero at `r = Radius + 0.5`:

```csharp
int outer = (p.Radius << 4) + 8 - radialQ4;   // wrong: zero at Radius + 0.5
int outer = (p.Radius << 4) - radialQ4;       // right: zero at Radius
```

Centres in the half-voxel band between the two read as inside their own boundary while occupancy
called them empty. Their samples were discarded, those edges fell back to the flat `Planar`
constant, and **which** centres landed in the band depended on the angle at which the circle met
the lattice. A crossing that moves with angle is a staircase.

Measured on `CsgFieldCoherenceTests` (one box fill, one cylinder carve), the spread of crossing
positions over every in-plane edge:

| | crossing spread |
|---|---|
| biased radial zero | 0.737 voxels |
| aligned radial zero | 0.172 voxels |
| analytic control (`CylinderRimDiagnosticTests`) | 0.065 voxels |

### The half-cell bias is correct for flat terms

Do not remove it from the depth or half-plane terms in `AnnulusDistanceQ4`. A box face genuinely
does sit half a cell beyond the last included centre, because a box occupies whole cells and its
bounds are inclusive integers. A radius is a continuous quantity compared directly against `r`, so
it takes no bias. The distinction is between *bounds* and *magnitudes*, not between shapes.

## Rule 2 — one surface, one primitive

The per-voxel sample is a single signed scalar. Where several primitives write halos near one
surface, adjacent cells can hold distances measured to *different* surfaces, and interpolating
between them puts the crossing somewhere that belongs to neither.

Composition is `min`/`max` in primitive order, resolved greedily in
`PrimitiveRasteriser.RasteriseBoundaryHalo`: a fill keeps the larger distance, a carve the smaller.
That is the correct union/subtraction rule pointwise, but it means the stored scalar describes
whichever surface is *nearest*, not the one you happen to be looking at.

Consequence worth knowing rather than fixing: along the arch springline (`y == centre.y`) the
`PrismProfile.Arch` half-plane term wins the `min` for every cell, so the whole row reads `±0.5` and
the intrados position is not recoverable there.

```text
[cap] +X lattice ray z=6: x11:e-4 x12:e-4 ... x16:S4 x17:S4 x18:S4 ...
[cap] +Y lattice ray z=6: y14:e-12 y15:e-4 y16:S4 y17:S12     <- clean ramp
```

This is correct for a union SDF and is not a defect. It does mean **a curved surface that must read
cleanly should not have another authored surface passing within about two voxels of it.**

## Rule 3 — a distance function must match its own occupancy

`ArcWedge` fills an angular slice, but `AnnulusDistanceQ4` deliberately omits the angular planes:
adjacent voussoirs form one structural annulus, so those planes are material joints, not exterior
boundaries. Each wedge therefore writes a full-annulus halo.

That is intentional and, when measured, harmless — re-authoring the arch ring as one `Annulus` fill
with the wedges demoted to `PaintSolid` produced a byte-identical field. Recorded here because it
looks like a bug and was investigated as one twice.

The general rule still stands for new shapes: if a primitive's distance function describes a
different surface than its `Contains`, say so in a comment and prove the difference does not reach
the rendered surface.

## Rule 4 — paint is not geometry

`PaintSolid` and `PaintSurface` change material over occupancy someone else established. A boundary
halo written from them would overwrite the signed distance of the surface that exists with the
distance to a shape that was never cut.

`SurfaceDetail` is already excluded from `RasteriseBoundaryHalo`. Extending that exclusion to the
paint modes is **suspected-correct but unverified** — it was implemented, measured to change
nothing on the arch, and reverted rather than landed on reasoning alone. Treat it as an open
question, not a settled rule.

## How to check a curved surface

Do not render it and look. A half-voxel positional error is a handful of pixels and easily
misread; several confident wrong diagnoses in this area came from exactly that.

Measure the crossing position instead. `ArchCrossingStabilityTests` and `CsgFieldCoherenceTests`
show the pattern: iterate the lattice, take every in-plane solid→empty edge near the surface,
interpolate the density as `TransvoxelDensityJob` would, and record where the crossing lands. A
correct surface gives a tight distribution; a staircase gives scatter approaching half a voxel.

```text
[csg] fill + carve: crossings=84 min=9.939 max=10.111 SPREAD=0.172 voxels
```

Two rules for that harness, both learned the hard way:

- **Iterate the lattice, never a trig ray.** Sampling `cos(θ)·r` rounds two adjacent radii onto the
  same cell — at 45° with `r=16`, `11.3` and `10.6` both round to `11`. The duplicate reading looks
  exactly like a doubled field value and will send you after a defect that is not there.
- **Build the minimal case.** One box fill plus one cylinder carve reproduced the full defect in
  twelve seconds. The 133-primitive arch was not needed, and the noise from moss, voussoir profiles
  and damage actively hid the signal.

## Open

- Overdraw between the greedy and continuous paths on extrusion caps. `FacetedMaskJob` emits Z
  faces for a cap disc that `TransvoxelTopologyJob` also contours. An ownership rule — the greedy
  path skips any `Planar` cell the continuous path claims — was verified in isolation
  (`CylinderOwnershipDiagnosticTests`: 124 emitted faces to 60, nothing left at or beyond the wall
  radius) but did not visibly change the arch. Real, not dominant.
- Residual banding on the arch soffit, tracking the voussoir joint recesses. Unexamined.
