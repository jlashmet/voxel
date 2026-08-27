# Plan

## Symptom
The marked VoxelShowcase doorway has a large triangular slab protruding through/over the opening.

## Discriminator
The doorway is produced by `ArchitectureVoxelPatterns.FramedArchedOpening`. Its decorative surround can project in front of the wall (`outerZ` / `outerDepth`), while the body and arch opening carves previously covered only the wall body (`origin.z` / `depth`).

Add a focused regression requiring the body and arch carves to cover the same projected Z span as the surround. This directly distinguishes a carve-depth mismatch from unrelated terrain, meshing, or retained-profile hypotheses.

## Evidence
- Pre-fix source `3d947f391bbdcebe76e557e104aa0fc4f5207ab2`: targeted CI run `33035042883` executed exactly one regression and failed: expected carve front Z=28, actual Z=30.
- Production fix `ea5f1432d70dcb1ba4485dcdcae983edbf09cec0`: both body and arch carves use `outerZ` / `outerDepth`.
- Latest integrated source `4f600c33edd9533ce9fc3c407497ebc114dbc673`: focused EditMode run `33080889659` passed.
- Exact same integrated source: saved-pose PlayMode replay run `33081103282` passed. The standalone player reported a verified frozen issue pose, and `verification-final.png` shows the marked doorway area without the former large blocking triangular slab.

## Blast radius / cost
The change is local to `FramedArchedOpening` instruction geometry. It does not change wall placement, unrelated primitives, meshing, terrain, or runtime traversal behavior. It only lengthens the two existing opening carve volumes to include the already-generated surround projection.
