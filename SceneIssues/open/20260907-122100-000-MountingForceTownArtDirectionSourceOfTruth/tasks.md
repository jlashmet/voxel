# Mounting Force town art direction source-of-truth tasks

Work the next unchecked non-blocked item. Record genuine blockers and continue independent work. Add tasks only when required by acceptance, correctness/reuse boundaries, or a demonstrated visual-quality defect. Do not close with any required checkbox or acceptance criterion incomplete.

## 1. Establish source inventory and provenance

- [ ] Fetch current `origin/master`, record the assigned feature SHA/branch and Unity version, and read `AGENTS.md`, `SceneIssues/README.md`, `SceneIssues/feature-readme.md`, this plan, and `issue.json` before implementation.
- [ ] Create a durable `reference-pack/` layout with a game-wide `index.md` and one `towns/<town>/` directory per verified town. Keep inventories, briefs, prompts/provenance, and concept images discoverable from the index.
- [ ] Inventory authoritative original-game/reference material available in the repository and connected reference sources. Create a provenance ledger that identifies each source and what town/place/style facts it supports.
- [ ] Lock the complete authoritative Mounting Force town list. Do not infer missing towns from current repo art or invent names to fill gaps.
- [ ] For every verified town, lock the exact named/specific buildings, landmarks, public spaces, and relevant infrastructure required by the source material before generating that town's concepts.
- [ ] Record coordinator/user constraints separately from historical-source provenance so an explicit art-direction requirement remains visible without being misrepresented as a recovered original-game fact.
- [ ] Record conflicting or unavailable references as blockers for the affected town and continue independent work on towns whose inventories are complete. Do not lower acceptance or substitute invented content.

## 2. Define the shared Project North Star visual language

- [ ] Write the game-wide style section in `reference-pack/index.md`: handcrafted, heroic, welcoming, storybook high fantasy; beautiful, readable, cohesive, multiplayer-friendly, modular, and built to last.
- [ ] Define shared visual rules for silhouette readability, believable construction, scale/proportion, material separation, environmental grounding, architectural detail density, interior readability, and stylized/non-photoreal presentation.
- [ ] Define explicit anti-goals/anti-drift rules. In particular, do not make every town a palette/roof/material variation of one generic village and do not treat existing repo art as authoritative merely because it already exists.
- [ ] Create a cross-town comparison matrix covering architecture language, primary materials, palette, culture/economy, terrain/environment integration, signature motifs, landmark language, and infrastructure character.
- [ ] Define concept-generation recordkeeping so every generated image can be traced to its town brief, reference sources, prompt/iteration notes, and repository file.

## 3. Write and lock each town brief

- [ ] For every town, create a written brief covering identity/theme, architecture language, construction logic, materials, palette, culture/economy, environmental integration, signature motifs, landmarks, public-space character, and reference provenance.
- [ ] Include the town's exact required building/landmark inventory and identify which items must receive dedicated concept coverage versus which can be represented within wider scenes.
- [ ] State how the town must differ visually from the other verified towns while still obeying the common Project North Star language.
- [ ] Cover town-appropriate roads/paths, bridges, walls/gates, fountains/plazas, markets, docks, farms/edge structures, and civic/religious/commercial/residential architecture when required by the inventory.
- [ ] Do not begin a town's production concept set until its inventory and brief are complete enough to constrain the result.

## 4. Produce the required concept set for every town

- [ ] Commit at least one production-quality overall/elevated town view per town.
- [ ] Commit at least two production-quality exterior building concepts per town.
- [ ] Commit at least two production-quality interior building concepts per town.
- [ ] Commit at least one production-quality street/plaza/public-space concept per town.
- [ ] Ensure the set visibly covers the town-specific named/specific structures, landmarks, and infrastructure required by the locked inventory; add additional concepts when six images are insufficient to cover acceptance.
- [ ] Keep character concepts and gameplay UI out of scope. Supplied character imagery may inform stylization only and must not become a deliverable category.
- [ ] Use the documented binary-transport workflow when required for image payload size, and verify reconstructed image size/hash before accepting the repository artifact.
- [ ] Store the accepted images and their prompt/provenance notes under the town's `reference-pack/towns/<town>/` directory and link them from the town brief/index.

## 5. Perform direct visual-quality and cross-town review

- [ ] After the first two verified town concept sets exist, perform the plan's discriminating experiment: inspect them side-by-side for shared-language coherence versus town-specific differentiation, record the result in `plan.md`, and tighten the appropriate rules before scaling if either relationship fails.
- [ ] Inspect every accepted image directly for silhouette, composition, believable proportions/construction, material separation, grounding, environmental integration, repetition, spatial readability, and obvious placeholder/blockout qualities.
- [ ] Classify visual evidence using the repository quality bar. Only `production-quality` passes this feature; `acceptable but improvable`, `prototype/blockout quality`, and `unacceptable` remain open work.
- [ ] After a visual rejection, name the failed visual relationship and add the necessary correction to this checklist before broad regeneration or style changes.
- [ ] Review all towns together as a comparison grid/matrix. Confirm each town is immediately distinguishable while the complete set still reads as one game rather than unrelated art packs.
- [ ] Re-check the full set for generic-style collapse, especially repeated roof shapes, identical palettes/material hierarchies, copied civic spaces, or motifs that erase the original town identity.

## 6. Package the downstream architecture handoff

- [ ] Complete `reference-pack/index.md` with links to every town, source ledger, comparison matrix, palette/material/motif summary, and explicit anti-drift guidance.
- [ ] For every town, provide a downstream implementation checklist derived from the locked inventory: ordinary structure archetypes, significant landmarks that may warrant dedicated implementation work, material/texture language, interior/exterior reference links, and critical silhouette/massing requirements. Do not implement the 3D structures in this feature.
- [ ] Verify every authoritative town and required place is accounted for by a brief and accepted concept evidence, or is explicitly blocked by an unavailable prerequisite that prevents closure.
- [ ] Confirm no current repo screenshot or legacy concept has silently been promoted to source-of-truth status without passing this feature's provenance and visual-quality process.

## 7. Validation, bookkeeping, and closure

- [ ] Review the final diff for scope, missing deliverables, accidental runtime changes, orphaned binary-transfer parts, and broken reference links.
- [ ] If the final diff remains documentation/concept-art only, record that no runtime module or module-local validation scene is affected; do not invent a Unity scene merely to create a test. If any production/runtime behavior changes, update `plan.md`/this checklist first and satisfy the owning module's tests, module-local validation scene, built-player evidence, and exact-SHA gates.
- [ ] Follow the SceneIssue exact-SHA/PR workflow appropriate to the final diff and report exactly what was validated; do not treat a skipped/zero-test/failing gate as success.
- [ ] Complete every acceptance criterion and checkbox, update `issue.json` resolution fields, move this issue from `open/` to `closed/`, merge current `origin/master` into the feature branch, and use the required PR + auto-merge promotion path.
- [ ] Confirm the closed SceneIssue and complete reference pack are visible on `origin/master` before declaring the assignment complete.
