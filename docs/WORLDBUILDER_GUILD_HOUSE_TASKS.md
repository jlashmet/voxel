# WorldBuilder Guild House Tasks

Branch: `agent/worldbuilding-decorations`
Plan: `docs/WORLDBUILDER_GUILD_HOUSES.md`

This is the focused guild-house checklist; the general decoration checklist remains the source for shared decoration infrastructure.

## Current source status

- Ten guild identities and room programs are implemented.
- Deterministic room selection and semantic public/private topology are implemented.
- A deterministic spatial allocator maps topology into concrete room blocks.
- Wizards use a multi-floor tower shell grammar; Druids use a broad lodge grammar with an open central courtyard/roof strip.
- Concrete guild rooms are bridged into region-aware `DecorationSpace` / `DecorationContext` values.
- Wizard and Druid room roles dispatch to existing semantic scene resolvers instead of duplicating their content logic.
- `GuildHouseFurnishedPrototypeAuthoring` provides a complete source path for the first two guilds: shell -> room spaces -> scene resolution -> geometry emission.
- IDs 201-260 now also have a baseline box/voxel authoring emitter, closing the visible-geometry gap for enchanting/guild content in that block.
- Baseline voxel shell authoring includes region materials and simple entrance/sign treatment. Signature architecture remains a later polish layer.
- Source regressions cover deterministic layouts, room non-overlap, valid Wizard/Druid spaces, semantic scene dispatch and stable placement identity.
- Unity/CI execution remains a separate evidence gate.

- [x] **GH001** Document fantasy guild-house design and initial guild roster.
- [x] **GH002** Implement semantic guild identities and reusable room-role vocabulary.
- [x] **GH003** Implement initial programs for Adventurers, Wizards, Knights, Assassins, Druids, Thieves, Clerics, Rangers, Bards and Alchemists.
- [x] **GH004** Reuse canonical decoration IDs 1-400 rather than creating region-specific duplicate objects.
- [x] **GH005** Add source tests for program validity, signature rooms and canonical-ID references.
- [x] **GH006** Implement deterministic required/optional room selection for different shell capacities.
- [x] **GH007** Add deterministic room-selection regression source.
- [x] **GH008** Implement baseline shell/room allocation with public-to-private topology feeding deterministic room blocks.
- [ ] **GH009** Implement hidden-room/secret-access semantics for Assassin, Thieves and forbidden Wizard spaces in physical shell connectivity.
- [ ] **GH010** Convert selected rooms into real `DecorationSpace` instances and dispatch existing scene/prop resolvers for the full guild roster. (Conversion is complete; Wizard/Druid dispatch is complete; remaining guild dispatch is open.)
- [ ] **GH011** Implement exterior guild identity: crest/sign, entrance treatment and optional yard/garden/stable. (Baseline sign/entrance treatment exists; guild-specific exteriors remain.)
- [ ] **GH012** Add guild-specific regional weighting for Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village. (Region contexts/materials are threaded through Wizard/Druid prototypes; broader guild-specific preference tuning remains.)
- [x] **GH013** Author an end-to-end Wizards Guild source fixture with tower shell plus library, enchanting workshop, ritual room, spell classroom, office/vault variants and geometry emission.
- [x] **GH014** Author an end-to-end Druids Lodge source fixture with lodge shell plus garden/grove, shrine, ritual circle, herb workshop/common variants and geometry emission.
- [ ] **GH015** Author an end-to-end Knights Order fixture with order hall, equipment room, oath shrine and stable.
- [ ] **GH016** Author an end-to-end Assassins Guild fixture with mundane facade, hidden contract room, poison workshop and concealed vault.
- [ ] **GH017** Author representative Adventurers, Thieves, Clerics, Rangers, Bards and Alchemists fixtures.
- [ ] **GH018** Add append-only guild-signature archetypes at IDs 401+ only where IDs 1-400 cannot express the identity adequately.
- [ ] **GH019** Add look-dev/debug visualization for room roles, public/private depth, hidden connections and guild identity.
- [ ] **GH020** Execute Unity tests/look-dev and record results separately from source completion.
