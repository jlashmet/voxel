# PropShowcase implementation plan

## Acceptance and observed baseline
The acceptance target remains the issue's exact 529 deterministic leaves across 18 named families, production-derived identity/provenance, real voxel/thin/procedural/world-object realization, bounded browser staging, renderer-derived draw/triangle readouts, module-local validation, exact-SHA built-player traversal/performance proof, and production-quality visual evidence.

Audit against current `origin/master` `51797c954490425964e602d6bb2252a0d7a7c5aa` found a blocking production-surface mismatch:
- `Assets/Game/Structures/Runtime/DecorationAssemblyGenerator.cs` and `Assets/Game/Structures/Api/WorldObjectCatalog.cs`, both named as authoritative inputs by the issue, do not exist on current master/feature history.
- The actual `WorldObjectKind` surface in `WorldObjectModel.cs` has 48 concrete values, not the required canonical-world-object family count of 82.
- `DecorationProceduralMeshRequest` carries id/family/bounds/facing/variant but no primitive or material identity; the separate content mesh request supports only a cylinder primitive. Therefore the required cylinder/capsule realization preserving canonical material semantics cannot be inferred without inventing policy.
- The removed branch draft reached 529 only as 440 legacy decoration IDs + 25 presets + 8 mine cave + 8 natural cave + 48 world objects. That grouping conflicts with the issue's 18-family counts and provenance contract, so it was not acceptable partial implementation.

## Hypotheses and result
1. **A newer master commit supplies the named catalogue/generator surfaces.** Falsified: latest master is the unrelated residency merge above, and exact path-history queries are empty.
2. **Current renamed APIs are acceptance-equivalent.** Falsified for the required contract: the concrete world-object enum exposes 48 rather than 82, and the procedural request lacks required primitive/material semantics.

## Selected approach / ownership
Do not create a showcase-only registry, synthetic missing identities, or heuristic renderer. Keep production catalogue/descriptor/realization authority in `Game.Structures`; `PropShowcase` remains an integration consumer only. The issue stays `open` until the required production surfaces exist or the authoritative issue is revised to identify existing canonical equivalents and counts. Resume from those production APIs, then add Structures-owned validation before top-level showcase evidence.

## Blast radius / current implementation baseline
No production behavior should change while blocked. Stale branch-only catalogue/test code was removed through `b761de2738eabc46af4488a775d7be46c27d287f`; subsequent changes are SceneIssue bookkeeping only.

## Remaining gates
All catalogue, realization, module-validation, browser, exhaustive player traversal/performance, visual-inspection, exact-SHA CI, closure, master-sync, PR, and auto-merge gates remain required and blocked on the production prerequisite above.
