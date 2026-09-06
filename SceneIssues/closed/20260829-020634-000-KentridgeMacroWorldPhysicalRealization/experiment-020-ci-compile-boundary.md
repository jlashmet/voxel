# Experiment 020 — CI compile boundary

Exact CI run `33365639199` for feature source `d65083eee98cdb3cfcc819852f6ac26a90c515d0` completed before runtime with a product compile failure. The diagnostic had been moved out of `Composition.Api`, but `VoxelEngine.Composition` still referenced `VoxelRenderPass` directly. That public base-type chain requires `Unity.RenderPipelines.Universal.Runtime` (`ScriptableRenderPass`), which the composition assembly intentionally does not reference.

The correction removed the concrete `VoxelRenderPass` type dependency. It obtains the active pass as `object` from the existing bridge and reflects the private scheduler field from the runtime instance type, preserving a read-only bounded diagnostic without adding a URP dependency to composition or changing renderer behavior/budgets.

Exact CI run `33366810706` for feature source `56599849ee29fb1d5c46e9af821caf1d358d2cc4` proves that renderer boundary now compiles. Compilation then stopped in the Kentridge-only evidence adapter because it named `TopDownWorldSettlementPlan` directly even though the existing `TopDownWorldPhysicalPlan.TryGetSettlement` API already supplies the concrete settlement/building shape. The narrow correction infers that API-owned type locally and copies only the four semantic building centres/extents into a private evidence value, avoiding any new shared reference or policy.

Experiment 020 remains incomplete until the exact corrected source runs and emits the Rossdam per-building frustum/publication telemetry. No visual fix is justified before that discriminator.
