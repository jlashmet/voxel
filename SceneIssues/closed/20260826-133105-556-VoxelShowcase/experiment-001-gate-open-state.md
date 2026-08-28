# Experiment 001 — gate open state

**Hypothesis:** the visual defect is caused by the front-gate mutation deleting the closed leaf without authoring a visible opened state, not by input or renderer failure.

**Action / source:** inspected `VoxelShowcase.HandleKeys`, `TryInteract`, current `ShowcaseWorld.TryOpenCastleFrontGate`, `CastleGatehouseAuthoring`, and the existing `CastleAccessTests.FrontGateOpensForANearbyPlayerAndClearsThePassage` on the pre-fix feature source.

**Result:** E is wired to the production interaction and the capture says the interaction succeeds. `TryOpenCastleFrontGate` builds the closed arch voxel set and calls `ClearVoxelsBulk`. The existing regression then asserts that every voxel in that closed gate volume is `Empty`. The gatehouse has a full-depth 52-voxel-wide empty portcullis arch around the 48-voxel gate, leaving room for opened leaves behind the closed leaf.

**Verdict:** confirmed. The disappearance is the implemented/tested state, not a missed render invalidation.

**Next:** author two bounded angled leaves deeper in the gatehouse so the closed leaf occludes them until E clears it; regression must require visible retained gate material and an empty central passage.
