# WorldBuilder Guild House Tasks

Branch: `agent/worldbuilding-decorations`
Plan: `docs/WORLDBUILDER_GUILD_HOUSES.md`

This is the focused guild-house checklist; the general decoration checklist remains the source for shared decoration infrastructure.

- [x] **GH001** Document fantasy guild-house design and initial guild roster.
- [x] **GH002** Implement semantic guild identities and reusable room-role vocabulary.
- [x] **GH003** Implement initial programs for Adventurers, Wizards, Knights, Assassins, Druids, Thieves, Clerics, Rangers, Bards and Alchemists.
- [x] **GH004** Reuse canonical decoration IDs 1-400 rather than creating region-specific duplicate objects.
- [x] **GH005** Add source tests for program validity, signature rooms and canonical-ID references.
- [x] **GH006** Implement deterministic required/optional room selection for different shell capacities.
- [x] **GH007** Add deterministic room-selection regression source.
- [ ] **GH008** Implement shell/room allocation with public-to-private depth and adjacency constraints.
- [ ] **GH009** Implement hidden-room/secret-access semantics for Assassin, Thieves and forbidden Wizard spaces.
- [ ] **GH010** Convert selected rooms into real `DecorationSpace` instances and dispatch existing scene/prop resolvers.
- [ ] **GH011** Implement exterior guild identity: crest/sign, entrance treatment and optional yard/garden/stable.
- [ ] **GH012** Add guild-specific regional weighting for Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village.
- [ ] **GH013** Author an end-to-end Wizards Guild fixture with library, enchanting workshop, ritual room and spell classroom.
- [ ] **GH014** Author an end-to-end Druids Lodge fixture with garden/grove, shrine, ritual circle and herb workshop.
- [ ] **GH015** Author an end-to-end Knights Order fixture with order hall, equipment room, oath shrine and stable.
- [ ] **GH016** Author an end-to-end Assassins Guild fixture with mundane facade, hidden contract room, poison workshop and concealed vault.
- [ ] **GH017** Author representative Adventurers, Thieves, Clerics, Rangers, Bards and Alchemists fixtures.
- [ ] **GH018** Add append-only guild-signature archetypes at IDs 401+ only where IDs 1-400 cannot express the identity adequately.
- [ ] **GH019** Add look-dev/debug visualization for room roles, public/private depth, hidden connections and guild identity.
- [ ] **GH020** Execute Unity tests/look-dev and record results separately from source completion.
