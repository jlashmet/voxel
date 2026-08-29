# Plan

## Current-system inventory
- `Game.WorldBuilder.Api` is the engine-free semantic-contract layer; `Game.WorldBuilder.Runtime` owns deterministic planning/resolution.
- Authoritative physical output already flows through `IStructureAuthoringSession` in the WorldBuilder voxel assembly, including the shared town-architecture authoring path.
- The current tree has no active production structural-socket solver and no current `SocketType.cs`; the issue's `SlotSpec` / `ShapeOp.CallSlot` / historical WorldArt socket names are predecessor contracts to reconcile rather than APIs to duplicate blindly.
- Fine prop placement remains outside this feature; any active decoration-socket path stays the decoration handoff rather than becoming the structural solver.

## Canonical design
1. Add one engine-free typed structural socket contract in `Game.WorldBuilder.Api`: stable piece/socket ids, semantic role flags, cardinal facing, integer voxel transform, reserved clearance, capacity/cardinality, support requirements, required/optional semantics, bounded piece cost/extent, and explicit decoration handoff metadata.
2. Add one deterministic `StructuralSocketComposer` in `Game.WorldBuilder.Runtime`. It will resolve a root recipe/catalogue with stable seed ordering, mutually compatible roles/facing, clearance/overlap checks, support probes, attachment capacity, required/optional behavior, cycle/depth/child/cost/extent budgets, and fail-closed actionable diagnostics.
3. Make the immutable result an inspectable attachment graph with stable hash/provenance: semantic structure id, child piece ids/transforms, parent/child socket ids, accepted links, rejected candidates/reasons, aggregate bounds and budget counters.
4. Retire competing dormant structural composition deliberately: do not create a second CallSlot-style runtime. If no current production CallSlot implementation exists at this head, document that the new composer is the canonical successor; if one is found during implementation, route or remove it in-scope.
5. Realize compiled children through shared WorldBuilder voxel authoring (`IStructureAuthoringSession`) so bridge/castle/cliff/building pieces are normal voxel/collision/storage content and each child keeps an independent bounded footprint. Structural pieces may expose decoration handoff sockets, but decoration clutter remains delegated.

## Proving showcase
- Add a deterministic structural-composition showcase path reachable from the exact `WorldbuildingGalleryShowcase` built scene.
- Bridge: two gorge/cliff anchors, multi-piece deck/spans/supports crossing multiple logical regions, road/traversal continuation at both ends, full CharacterMotor crossing and a wide gorge view.
- Castle: reusable wall runs, >=2 towers and gatehouse, continuous traversable gate/wall joins, generic wall-continuation sockets, explicit incompatible-module rejection.
- Cliff settlement: terrain/support-derived anchors at multiple elevations with platform/building pieces and traversable stair/ramp/short bridge, rejecting unsupported candidates.
- Building detail: facade and roof structural attachment sockets with >=2 style variants; micro-detail remains normal architecture/decor authoring.

## Verification
- Focused production behavioral tests cover deterministic graph/hash, allowed seeded variant, compatibility, facing, required/optional behavior, clearance/overlap, support, capacity, recursion/cycle protection, child/count/cost/extent budgets, diagnostic provenance, decoration handoff, and authoritative voxel child realization.
- Built-application harness must validate actual CharacterMotor traversal of the bridge, gate and vertical connection, plus player-height seam views and wide shots of all four cases. Inspect every durable full-resolution frame before closure.
- Measure and record composition time, child count, aggregate primitive/voxel-authoring cost, logical region span/streaming footprint, bounded memory-model cost and render/triangle proxy for bridge/castle without raising global budgets.

## Blast radius / cost guardrails
- New contracts are additive and engine-free; existing WorldBuilder generation remains unchanged unless explicitly routed through the structural composer.
- No permanent per-piece GameObjects and no global budget increases.
- Solver work is bounded by explicit max depth, child count, piece cost and extent; deterministic candidate ordering avoids generation-order dependence.
- Existing decoration placement remains the fine-grained prop layer.
