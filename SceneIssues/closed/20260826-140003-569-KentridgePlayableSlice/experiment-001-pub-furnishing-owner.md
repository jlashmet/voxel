# Experiment 001 — pub furnishing owner

- Hypothesis: the captured empty/under-specified pub is caused by the Pub furnishing path assuming a bar that no production owner actually emits.
- Action/source: traced the saved `KentridgePlayableSlice` pose through settlement role resolution, `KentridgeStructureCompiler`, `KentridgeGrammarVoxelCatalogue`, and `KentridgeHouseInteriorPropCatalogue`; implementation/regression source through `c89774ebeff6ebe9efdd73be5fce901454a136dd`.
- Result: `KentridgeRole.Pub` resolves correctly and already requests warm windows. Shared hospitality furnishing emitted only a bench and said the Pub kept a “pre-existing bar counter,” while the Pub role signature emitted only the exterior hanging sign. No existing resident/NPC/humanoid production system was found.
- Verdict: confirmed furnishing ownership gap; wrong-role and missing-window-treatment hypotheses rejected. Room size is a secondary acceptance adjustment.
- Next: replay the saved pose on the fixed feature head, inspect the rendered bar/staff/seating/windows, then submit the exact fixed SHA to targeted PlayMode CI.
