# Plan

## Defect / acceptance
Downstream worldbuilding lacked one provenance-backed visual source of truth, allowing generic placeholder styling to drift into canon. Acceptance requires a frozen authoritative settlement roster, pre-concept named-place inventory, written brief per town, six environment concepts per town, and a game-wide comparison/anti-drift reference.

Existing voxel-town visuals are explicitly non-authoritative. This assignment changes reference docs/art only; no final in-engine town implementation, gameplay, character or UI work is in scope.

## Competing hypotheses / discriminator
1. **Original-source identity is authoritative but modern art direction is missing.** `jlashmet/mounting-force` should establish settlement names/functions; Project North Star supplies the new visual language.
2. **A later source supersedes that canon.** `jlashmet/MountingForce2` could replace the roster/places.
3. **Count/category-complete concept sheets are sufficient.** Falsified if they read as schematic/blockout art rather than implementation-guiding concepts.

Pinned source comparison supports hypothesis 1: the original project at `f37512e0175f46e14ad2d7aeb5ecea290af14a14` establishes Kentridge, Hightown, Moordell, Rossdam, Orc Village/Orc-Town and Fairy Village; the later repo at `5e111d5a72e441824311b4a3a176f72e9c86f653` does not supersede them. Cambridge is coordinator-canonical only; its water-town requirement is source-backed by this SceneIssue and added venue names are labeled derived. Initial schematic studies were rejected, so category count alone is falsified.

## Selected fix / material result
Freeze the seven-settlement roster and inventories before concept production; separate historical facts from derived Project North Star choices; commit per-town briefs plus six separately addressable environment concept roles and a game-wide comparison/catalog. The final packs distinguish towns through topology, silhouette, materials, economy/culture and infrastructure rather than recoloring one generic kit.

## Regression / built-scene evidence
The final master→feature diff is SceneIssue-local Markdown/SVG reference content only. Exact targeted CI confirmed `hasProductionChanges=false` and `hasValidationWork=false`, so there is no executable/runtime owner to regress and built-player evidence is **not applicable** under `issue-readme.md`. `WorldbuildingGalleryShowcase` remains context only and is explicitly non-authoritative for visual acceptance.

## Validation result / remaining gates
Exact source `4ee3148f36c916928a1600aba9bb5a5f65408554` passed request `22fe801f55fc6ac5ae65248d9d4260ae77f52ad8`, workflow `34204143335`, with `ci/single-test=success`; artifact `10050980169` records the diff/validation plan. Every acceptance clause and checklist item is complete. Perform atomic open→closed bookkeeping, merge current `origin/master`, then promote through the normal PR + auto-merge path and verify the closed assignment on master.
