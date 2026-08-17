# WorldBuilder Guild House Tasks

Branch: `agent/worldbuilding-decorations`
Plan: `docs/WORLDBUILDER_GUILD_HOUSES.md`

This is the focused guild-house checklist; the general decoration checklist remains the source for shared decoration infrastructure.

## Current source status

- Ten guild identities and room programs are implemented.
- Deterministic room selection and semantic public/private topology are implemented.
- A deterministic spatial allocator maps topology into concrete room blocks.
- Wizards use a multi-floor tower shell grammar; Druids use a broad lodge grammar with an open central courtyard/roof strip; other guilds use the baseline hall/hidden-den/chapel-house shell families.
- Concrete guild rooms are bridged into region-aware `DecorationSpace` / `DecorationContext` values.
- All ten guild kinds now dispatch every selected room role to existing semantic decoration scene resolvers instead of duplicating content logic.
- `GuildHouseFurnishedPrototypeAuthoring` provides the full source path: shell -> room spaces -> scene resolution -> geometry emission.
- Physical concealed access is authored for Assassin/Thieves deep rooms and Wizard forbidden archives as corridor-facing disguised partitions/panels. Wizard concealed access receives a small arcane clue.
- IDs 201-260 have a baseline box/voxel authoring emitter, closing the visible-geometry gap for enchanting/guild content in that block.
- `GuildHouseRegionPolicy` supplies guild/settlement preference signals without hard-banning unusual combinations; settlement integration still needs to consume those scores.
- Baseline voxel shell authoring includes region materials and simple entrance/sign treatment. Signature architecture remains a later polish layer.
- Source regressions cover deterministic layouts, room non-overlap, all-ten-guild scene dispatch, secret portal planning, region preference, and stable placement identity.
- Unity/CI execution remains a separate evidence gate.

- [x] **GH001** Document fantasy guild-house design and initial guild roster.
- [x] **GH002** Implement semantic guild identities and reusable room-role vocabulary.
- [x] **GH003** Implement initial programs for Adventurers, Wizards, Knights, Assassins, Druids, Thieves, Clerics, Rangers, Bards and Alchemists.
- [x] **GH004** Reuse canonical decoration IDs 1-400 rather than creating region-specific duplicate objects.
- [x] **GH005** Add source tests for program validity, signature rooms and canonical-ID references.
- [x] **GH006** Implement deterministic required/optional room selection for different shell capacities.
- [x] **GH007** Add deterministic room-selection regression source.
- [x] **GH008** Implement baseline shell/room allocation with public-to-private topology feeding deterministic room blocks.
- [x] **GH009** Implement hidden-room/secret-access semantics and physical concealed partitions for Assassin, Thieves and forbidden Wizard spaces.
- [x] **GH010** Convert selected rooms into real `DecorationSpace` instances and dispatch existing scene/prop resolvers for the full ten-guild roster.
- [ ] **GH011** Implement exterior guild identity: crest/sign, entrance treatment and optional yard/garden/stable. (Baseline sign/entrance treatment exists; guild-specific exteriors remain.)
- [ ] **GH012** Add guild-specific regional weighting for Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village. (`GuildHouseRegionPolicy` exists; settlement guild placement still needs to consume it.)
- [x] **GH013** Author an end-to-end Wizards Guild source fixture with tower shell plus library, enchanting workshop, ritual room, spell classroom, office/vault/forbidden variants and geometry emission.
- [x] **GH014** Author an end-to-end Druids Lodge source fixture with lodge shell plus garden/grove, shrine, ritual circle, herb workshop/common variants and geometry emission.
- [x] **GH015** Author a Knights Order source path with order/common hall, equipment/training scene, oath shrine, trophy content, office and optional stable.
- [ ] **GH016** Author an end-to-end Assassins Guild fixture with mundane facade, hidden contract room, poison workshop and concealed vault. (Interior dispatch and concealed vault/access are complete; deliberately mundane public facade remains.)
- [x] **GH017** Author representative source paths for Adventurers, Thieves, Clerics, Rangers, Bards and Alchemists; roster-wide regression source covers all ten guilds.
- [ ] **GH018** Add append-only guild-signature archetypes at IDs 401+ only where IDs 1-400 cannot express the identity adequately.
- [ ] **GH019** Add look-dev/debug visualization for room roles, public/private depth, hidden connections and guild identity.
- [ ] **GH020** Execute Unity tests/look-dev and record results separately from source completion.
