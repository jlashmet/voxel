# Experiment 004 — final CI symbol disambiguation

**Hypothesis**
After exposing `VoxelEngine.Composition.Api` to the PlayMode test assembly, the regression now sees legacy showcase castle symbols that collide with the intended game castle API types.

**Action / source**
Inspect exact failed request `64bbb3a908fc537eb47d14131fa6ab1a6ca32136` for source `b19424cc8e993a1c5e87f41a92bfce6d09a8e47d`, including both bake and real-player compiler logs.

**Result**
The only reported compiler errors are four `CS0104` ambiguities in the storage regression: `CastlePlan` and `CastleLayout` each resolve to both `Game.Structures.Api` and `VoxelEngine.Showcase`. No production compile errors were reported and execution never reached the requested test.

**Verdict**
Qualify the intended game types explicitly with `GameCastlePlan` / `GameCastleLayout` aliases. This is a test-only symbol-resolution correction at `27c3e38a75e7216477e800eb7416f2b3a79083df`; production behavior and runtime dependencies remain unchanged.
