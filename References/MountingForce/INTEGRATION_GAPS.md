# Mounting Force world integration gaps

This checklist tracks translation from recovered Mounting Force evidence into voxel-owned WorldBuilder content.

**Naming rule:** normalized ids such as `kentridge-overworld` describe generator/world ownership. They do **not** rename the original maps and are not claims about political/lore regions. Exact legacy map ids remain source evidence. Shared or ambiguous maps stay candidates until traversal/content evidence resolves their ownership.

## Region and settlement normalization

- [x] Normalize six town-centered overworld regions: `kentridge-overworld`, `hightown-overworld`, `moordell-overworld`, `rossdam-overworld`, `fairy-village-overworld`, and `orc-village-overworld`.
- [x] Model Kentridge, Hightown, Moordell, Rossdam, Fairy Village, and Orc Village as settlements within those regions.
- [x] Preserve recovered legacy exterior/settlement map ids separately from semantic WorldBuilder ids.
- [x] Keep shared or ambiguous wilderness/road maps as candidate boundaries instead of inventing hard ownership.
- [x] Replace the opening campaign's old `kentridge-region` semantic id with `kentridge-overworld`.
- [ ] Resolve exact ownership/shared-road semantics of the legacy generic `overworld` and `overworld_big` maps.
- [ ] Identify dedicated exterior maps for Hightown, Fairy Village, and Orc Village if stronger upstream evidence exists.
- [ ] Add biome/population constraints only where recovered evidence supports them.

## Map/site translation

- [ ] Translate recovered town interiors and authored map locations into typed WorldBuilder sites rather than leaving them only as catalog metadata.
- [x] Link the semantic `starting-pub` opening role explicitly to legacy map `kentridge-pub`.
- [ ] Model nested interiors/sublevels (for example Kentridge warehouse lower, Medrare upper/lower, Hightown under-church levels, and Timmy's back room).
- [x] Expose persistent legacy map identity through `CampaignBlueprint.SiteSourceEvidence`, keyed by semantic `SiteRef`, so generated-world/debug tooling can join a resolved site back to its recovered source evidence without making that evidence a generation constraint.

## Topology

- [ ] Translate all 192 recovered directed traversal edges into typed WorldBuilder traversal constraints or an equivalent world-connectivity model.
- [ ] Model shared wilderness/road maps connecting town-centered overworld regions.
- [ ] Preserve validated portals/warps where continuous voxel traversal cannot express the original semantic transition.
- [ ] Add topology validation tests that ensure every hard recovered transition remains reachable.

## Story and progression

- [ ] Preserve all 43 cross-level positive story dependencies.
- [ ] Import/author complete ordered cutscene dialogue and choreography. Until recovered dialogue exists, use the explicit placeholder `Dialogue coming soon.` rather than blocking integration.
- [ ] Represent story-state-gated encounters and typed actor prerequisites (`waitForDeath`, party membership, talked-to state, etc.).
- [ ] Keep isolated scenes isolated; do not invent prerequisites just to form quest chains.

## Actors and generated assets

- [ ] Use placeholder/dummy character visuals for recovered semantic actors whose generated assets do not exist yet.
- [ ] Keep actor semantic identity separate from the placeholder visual so generated assets can replace dummies without changing story/world references.
- [ ] Replace dummy visuals with generated character assets as the character pipeline lands them.
- [ ] Complete unresolved multi-occurrence identity linkage such as Billy/Timmy before relying on identity-wide state.
- [ ] Preserve the known source token mismatch `Bdiff` without silently rewriting it.

## Objects, hazards, encounters, and rewards

- [ ] Represent fixed-item and random/fallback reward chests.
- [ ] Represent `RPGTrigger`, `RPGTriggerAddEnemies`, and `EnemyGroup` behavior.
- [ ] Represent poison/stun projectile hazards and `ConstantFlame` visual objects.
- [ ] Preserve unresolved `orc-village-spike-trap` authored state as explicitly non-constraining until recovered.

## Integration verification

- [x] Add EditMode tests for the normalized recovered-world catalog and six settlement/region pairs.
- [x] Add PlayMode coverage that builds/generates the Kentridge vertical slice from the production campaign blueprint, evaluates all 17 stable Kentridge building roles through the voxel shape program, starts a new game, and plays the opening cutscene through to the travel objective.
- [x] Add EditMode coverage proving semantic sites retain recovered source-map evidence without turning provenance into a generation constraint.
- [ ] Add a recovered-world showcase that can visualize semantic region/site ids alongside legacy map-source ids.
- [ ] Expand the vertical slice town-by-town as missing WorldBuilder capabilities are exposed.
