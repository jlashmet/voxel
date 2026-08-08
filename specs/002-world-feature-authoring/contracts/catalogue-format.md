# Contract: Catalogue Format

**Feature**: [../spec.md](../spec.md) · **Data model**: [../data-model.md](../data-model.md)
**Audience**: world designers (FR-001 — authoring must need no engine change)

A catalogue is authored as text, compiled to an immutable blob at world load, and validated before
anything generates. This is the surface a designer works with; everything below it is engine.

## Example

```yaml
catalogue: greenvale
version: 1

definitions:
  - id: cottage
    kind: structure
    footprint: [96, 80, 96]          # voxels; 9.6 m x 8 m x 9.6 m
    basePlane: lowestGround
    maxSlope: 3                       # rise per 8 run
    precedence: 100
    materials: [stone, wood, glass]
    parameters:
      width:      { min: 48, max: 88, quantum: 8 }
      depth:      { min: 48, max: 88, quantum: 8 }
      wallHeight: { min: 24, max: 40, quantum: 4 }
      roofPitch:  { min: 4,  max: 12, quantum: 2 }
      hasPorch:   { min: 0,  max: 1 }
    anchors:
      - { name: door,  face: south, atGround: true }
      - { name: hearth, centre: true }
    shape:
      - foundation: { material: stone, depth: 8 }
      - walls:      { material: stone, height: wallHeight, thickness: 4 }
      - opening:    { face: south, width: 12, height: 20, anchor: door }
      - roof:       { material: wood, pitch: roofPitch, overhang: 4 }
      - if: hasPorch
        then:
          - porch:  { face: south, depth: 16, material: wood }

  - id: village
    kind: structure
    footprint: [512, 128, 512]
    precedence: 90
    slots:
      - { name: houses, definition: cottage, count: [4, 9], spacing: 64 }

placement:
  - definition: village
    cellEdge: 512
    attemptsPerCell: 1
    acceptProbability: 12000          # out of 65536
    altitude: [180, 320]
    maxSlope: 3
    minSpacing: 768
    exclude: [caveMouth, protectedZone]

  - definition: cottage
    explicit:
      - { at: [2048, 0, 3072], orientation: north, parameters: { width: 72 } }
```

## Rules the format enforces

1. **Integers only.** Every number is an integer in voxels or in a declared unit. There is no
   float syntax, because a float in a catalogue would become a float in generation
   (Constitution I).
2. **Ranges are declared.** A parameter without `min` and `max` is a validation error. Quantum
   defaults to 1.
3. **Footprint is a promise.** Content outside it is a validation error, not a clip at runtime.
4. **Probabilities are integers out of 65536.** `0.18` cannot be written; `12000` can.
5. **Slots are acyclic.** A definition cannot contain itself, directly or transitively.
6. **Materials are named**, resolved against the world palette at compile time. An unknown name is
   a load failure.

## Compilation

```text
catalogue.yaml --parse--> definitions --compile--> ShapeProgram blobs --validate--> Catalogue
                                                                          │
                                                                  fails loudly, before
                                                                  any world generation
```

Compilation is offline or at world load, never per region. The compiled blob is what ships and
what both server and client hash to agree they share a world.

## Validation report

Validation produces a report a designer can act on, not a stack trace:

```text
cottage: parameter combination (width=48, roofPitch=12) produces a roof 26 voxels tall,
         exceeding footprint height 80 by 6.        [FR-009, degenerate combination]
village: slot 'houses' spacing 64 with count 9 needs 576 voxels, footprint is 512.
manor:   material 'thatch' is not in the world palette.
```

## What a designer cannot do

- Place a feature by writing voxels directly. Shapes are parametric (Q3 decision).
- Make a feature indestructible except through protected status, which is instance state and not a
  catalogue property.
- Make a feature appear on some devices and not others (Constitution IV).
- Depend on another feature's presence. Features do not see each other; contested space is
  resolved by `precedence` alone.
