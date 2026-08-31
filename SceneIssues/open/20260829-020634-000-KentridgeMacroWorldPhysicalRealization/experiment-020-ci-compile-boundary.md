# Experiment 020 — CI compile boundary

Exact CI run `33365639199` for feature source `d65083eee98cdb3cfcc819852f6ac26a90c515d0` completed before runtime with a product compile failure. The diagnostic had been moved out of `Composition.Api`, but `VoxelEngine.Composition` still referenced `VoxelRenderPass` directly. That public base-type chain requires `Unity.RenderPipelines.Universal.Runtime` (`ScriptableRenderPass`), which the composition assembly intentionally does not reference.

The correction at `845b5654e7e2896bca153577c1a998d924f07da3` removes the concrete `VoxelRenderPass` type dependency. It obtains the active pass as `object` from the existing bridge and reflects the private scheduler field from the runtime instance type, preserving a read-only bounded diagnostic without adding a URP dependency to composition or changing renderer behavior/budgets.

Experiment 020 remains incomplete until the exact corrected source runs and emits the Rossdam per-building frustum/publication telemetry. No visual fix is justified before that discriminator.
