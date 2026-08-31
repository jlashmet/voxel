# Experiment 001 — CallSlot predecessor discriminator

**Hypothesis:** The existing `FeatureDefinition` / `SlotSpec` / `ShapeOp.CallSlot` path is the intended structural-composition mechanism, but its production evaluator is incomplete.

**Action / source:** Inspected feature head `d897e1c5cd4987a24498f489a95fbdd3c4c13784`: `FeatureDefinition`, `AnchorSpec` (`SlotSpec`), `FeatureCatalogue`, `FeatureCatalogueBuilder`, `FeatureCatalogueComposer`, `ShapeOps`, and `ShapeProgram.Run`.

**Result:** `FeatureDefinition` reserves slot ranges; the catalogue owns a blittable `Slots` pool; catalogue composition rebases child definition ids; bytecode exposes `CallSlot(slotIndex)` and `RegisterSlot`. Runtime `ShapeProgram.Run` reaches `CallSlot` and immediately `break`s, so the active production opcode is a no-op. Legacy `SlotSpec` only supplies a fixed child definition, local box, count range and spacing. It lacks typed compatibility, facing, support, required/optional semantics and diagnostics. `ComputeHash` also omits `Slots` despite catalogue hash being world identity.

**Verdict:** Hypothesis supported, with an important qualification: evaluator completion alone is insufficient. The predecessor contract itself must be generalized, but it remains the correct canonical ownership boundary. A separate WorldBuilder structural solver would duplicate the existing production mechanism.

**Next:** Extend the existing slot contract and production `CallSlot` path, include all generation-affecting socket data in catalogue identity, and prove the first bounded child-composition behavior before showcase integration.
