# Experiment 003 — sparse upper structure bake

**Hypothesis.** The explicit fixed-altitude coverage fix is semantically correct, but run `33300355968` exceeded the 240 s wrapper because the newly requested upper dragon region followed `GenerateRegionBlocking`: it synthesized a complete terrain region and then evaluated the full mountain catalogue even though the region is otherwise empty sky and only the dragon structure can author solid output there.

**Discriminator.** Exact request `b68865a45a70...` compiled successfully and its artifact logged a completed/imported `200 regions, 18.2 MiB` startup image with content signature `0x7C8A5152`; the wrapper terminated the Unity process at about 241 s during shutdown. The requested acceptance test therefore never ran. This is a bake-cost failure, not a compile, route, dialogue, or runtime-streaming failure.

**Selected action.** Keep runtime streaming on the existing full `FeatureRegionBuild` path. Add an optional shared `FixedAltitudeStructures` build scope and use it only from bake-only explicit-structure materialisation when the required vertical region is absent after the normal startup surface pass. Existing/resident regions still take `GenerateRegionBlocking`.

**Behavioral proof.** `ShowcaseBakeExplicitStructureCoverageTests.SparseUpperDragonStructureBuildMatchesFullCatalogueSemanticOutput` builds the actual mountain catalogue into canonical-empty upper storage with both the full and scoped builders. It requires identical authoritative semantic hash and serialized snapshot bytes, exactly one scoped considered/rasterized structure instance, and exactly one resident requested upper region. The existing planner regression still requires exactly the dragon-owned lower/upper layers and rejects broad landform/headroom sky residency. Both regressions are invoked by the final single-filter acceptance.

**Blast radius.** The default `FeatureRegionBuild` scope remains `All`; runtime streaming, terrain generation, movement, collision, encounter, gallery baking, feature ordering, and normal resident-region bake semantics are unchanged. The new scope is selected only by the offline sparse fixed-structure coverage path.

**Next gate.** Re-run the exact-parent Mountain Dragon final PlayMode request on the single allowed `ci-test/fixes/agent-4` transport. Require a source-matched fresh bake under 240 s / 14 GB, Unity reopen, green final acceptance, and complete built-player evidence before closure.
