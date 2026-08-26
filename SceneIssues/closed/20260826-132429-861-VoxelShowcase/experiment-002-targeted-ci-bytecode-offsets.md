# Experiment 002 — Targeted CI bytecode offsets

## Hypothesis

The thin-pane production change should satisfy the new orientation regression: the reveal should keep the authored wall depth while the glazing should become a centered thin pane on either horizontal wall-normal axis.

## Performed

Ran the focused EditMode test through `ci-test/fixes/agent-3` using request `agent3-20260826-scene-132429861-glazing-86b6b547-01` at CI commit `b74e3755bc029b638a52488cabfde85c32a85cb5` (feature source `86b6b547aaa5dddf3cca05d4e0d44353613e35ce`). GitHub Actions run `32992091952` executed:

`VoxelEngine.Tests.EditMode.ArchitectureVoxelPatternTests.GlazedOpeningUsesThinCenteredPaneAcrossFacadeOrientations`

## Result

Failed. Unity reported:

`The X-normal reveal must retain the full wall depth. Expected: 3 But was: 30`

The value `30` is the test input's Z coordinate, not a generated reveal extent. Inspection of `ArchitectureShapeProgramBuilder.Op` showed that shape bytecode always inserts a reserved zero word after the opcode, so the new regression was reading operands one slot too early. The production geometry was not implicated by this failure.

## Learned

For `EmitBox`, the instruction layout is `op, reserved, x, y, z, sx, sy, sz, material, surface, coating, mode`. `EmitRoundedBox` follows the same prefix with the radius inserted before material. The first regression additions had the same off-by-one error for reveal/pane position and extent fields.

## Next

Correct only the regression operand offsets on `fixes/agent-3`, keep the production thin-pane implementation unchanged, reset `ci-test/fixes/agent-3` to the corrected feature source, issue a new unique targeted request, and require green `ci/single-test` before starting the exact SceneIssue replay.
