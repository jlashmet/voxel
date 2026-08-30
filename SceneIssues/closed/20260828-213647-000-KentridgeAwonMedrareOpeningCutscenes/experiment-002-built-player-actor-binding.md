# Experiment 002 — Built-player campaign validation failure

**Hypothesis:** the exact scene fails before rendering because the recovered Medrare dialogue added a required actor that production composition does not bind.

**Action / source:** inspected run `33282767733` for feature `add68da73422a7f4d339793cdcfabccdb63bf4e0`; player log throws `Campaign blueprint contains validation errors` from `BlueprintCompiler.Compile`. Compared `MedrareJoinDefinition.RequiredActors` implied by its Weldon/Medrare dialogue with `KnownOpeningCampaignContent` bindings.

**Result:** production bound Medrare only; the recovered lines make Weldon required too. The focused story tests did not compile the campaign graph, so they missed the invalid actor binding.

**Verdict:** confirmed product failure. Bind Weldon to `PlayerSlot.First` for the Medrare join and add a focused regression that compiles the actual campaign blueprint before exercising the source gate.

**Next:** run one post-fix exact-SHA request and require both the compiler-path regression and built-player `KENTRIDGE_OPENING result=PASS` evidence.
