# WorldBuilder Guild House Tasks

Branch: `agent/worldbuilding-decorations`
Plan: `docs/WORLDBUILDER_GUILD_HOUSES.md`

This is the focused guild-house checklist; the general decoration checklist remains the source for shared decoration infrastructure.

## Current source status

- Ten guild identities and room programs are implemented.
- Deterministic room selection and semantic public/private topology are implemented.
- A deterministic spatial allocator now maps topology into concrete room blocks.
- Wizards use a multi-floor tower shell grammar; Druids use a broad lodge grammar with an open central courtyard/roof strip.
- Concrete guild rooms are bridged into region-aware `DecorationSpace` / `DecorationContext` values, but room-specific decoration dispatch is not yet complete.
- Baseline voxel shell authoring exists for hall/tower/lodge prototypes, including region materials and a simple entrance sign treatment.
- Source regressions cover deterministic layouts, room non-overlap, valid Wizard decoration spaces and exterior Druid garden spaces.
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
- [ ] **GH010** Convert selected rooms into real `DecorationSpace` instances and dispatch existing scene/prop resolvers. (`DecorationSpace` conversion is complete; resolver dispatch remains.)
- [ ] **GH011** Implement exterior guild identity: crest/sign, entrance treatment and optional yard/garden/stable. (Baseline sign/entrance treatment exists; guild-specific exteriors remain.)
- [ ] **GH012** Add guild-specific regional weighting for Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village.
- [ ] **GH013** Author an end-to-end Wizards Guild fixture with library, enchanting workshop, ritual room and spell classroom. (Tower shell + room spaces exist; furnishing dispatch remains.)
- [ ] **GH014** Author an end-to-end Druids Lodge fixture with garden/grove, shrine, ritual circle and herb workshop. (Lodge shell + room spaces exist; furnishing dispatch remains.)
- [ ] **GH015** Author an end-to-end Knights Order fixture with order hall, equipment room, oath shrine and stable.
- [ ] **GH016** Author an end-to-end Assassins Guild fixture with mundane facade, hidden contract room, poison workshop and concealed vault.
- [ ] **GH017** Author representative Adventurers, Thieves, Clerics, Rangers, Bards and Alchemists fixtures.
- [ ] **GH018** Add append-only guild-signature archetypes at IDs 401+ only where IDs 1-400 cannot express the identity adequately.
- [ ] **GH019** Add look-dev/debug visualization for room roles, public/private depth, hidden connections and guild identity.
- [ ] **GH020** Execute Unity tests/look-dev and record results separately from source completion.
