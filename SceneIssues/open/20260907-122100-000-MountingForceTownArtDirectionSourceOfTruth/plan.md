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
Final validation is a master→feature diff-scope audit. If it contains only this assigned SceneIssue’s Markdown/SVG reference artifacts, no executable/runtime or serialized scene behavior changed, so there is no behavioral owner to regress and built-player evidence is **not applicable** under `issue-readme.md`. `WorldbuildingGalleryShowcase` remains context only and is explicitly non-authoritative for visual acceptance. If any runtime/scene path appears in the diff, this exception is void and the required module-local/built-player gates become mandatory.

## Remaining gates
Audit every `expected` clause against committed artifacts; verify exact diff scope; submit the final feature SHA through `ci-test/fixes/agent-2`; record green exact-SHA evidence; close open→closed with fixed metadata; merge current master; PR + auto-merge; monitor required `affected` gate until merged and the closed issue is visible on master.
