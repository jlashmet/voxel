# WorldBuilder Guild House Tasks

Branch: `agent/worldbuilding-decorations`
Plan: `docs/WORLDBUILDER_GUILD_HOUSES.md`
Signature manifest: `docs/WORLDBUILDER_GUILD_SIGNATURE_MANIFEST.md`

This is the focused guild-house checklist; the general decoration checklist remains the source for shared decoration infrastructure.

## Current source status

- Ten guild identities and room programs are implemented.
- Deterministic room selection and semantic public/private topology are implemented.
- A deterministic spatial allocator maps topology into concrete room blocks.
- Wizards use a multi-floor tower shell grammar; Druids use a broad lodge grammar with an open central courtyard/roof strip; other guilds use baseline hall/hidden-den/chapel-house shell families.
- All ten guild kinds dispatch their selected room roles to existing semantic decoration scene resolvers instead of duplicating room content logic.
- `GuildHouseFurnishedPrototypeAuthoring` provides the full source path: shell -> room spaces -> base scene resolution -> base geometry -> guild-signature layer.
- Physical concealed access is authored for Assassin/Thieves deep rooms and Wizard forbidden archives as corridor-facing disguised partitions/panels. Wizard concealed access receives a small arcane clue.
- Guild-specific exterior dressing is implemented: wizard crystal entry, knight heraldry/hitching, druid standing-stone threshold, ranger field/hitching gear, bard marquee, alchemist chimney/service area, cleric entry fixtures and deliberately mundane Assassin/Thieves presentation.
- IDs **401-440** are implemented as stable guild-signature archetypes and sparsely layered over appropriate rooms. The existing 1-400 library remains the base content vocabulary.
- `GuildHouseRegionPolicy` supplies guild/settlement preference signals without hard-banning unusual combinations; settlement integration still needs to consume those scores.
- `GuildHouseDebugGizmo` visualizes shell bounds, room roles, public/private depth and concealed-access portals in Scene view.
- Source regressions cover deterministic layouts, room non-overlap, all-ten-guild scene dispatch, concealed portal planning, region preference, signature recipe identity/layering and stable placement identity.
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
- [x] **GH011** Implement baseline exterior guild identity: entrance treatment plus guild-appropriate signs/heraldry, threshold magic, hitching/yard/service dressing, or deliberately mundane secret-guild facades.
- [ ] **GH012** Add guild-specific regional weighting for Kentridge, Hightown, Moordell, Rossdam, Fairy Village and Orc Village. (`GuildHouseRegionPolicy` exists; settlement guild placement still needs to consume it.)
- [x] **GH013** Author an end-to-end Wizards Guild source fixture with tower shell plus library, enchanting workshop, ritual room, spell classroom, office/vault/forbidden variants and geometry emission.
- [x] **GH014** Author an end-to-end Druids Lodge source fixture with lodge shell plus garden/grove, shrine, ritual circle, herb workshop/common variants and geometry emission.
- [x] **GH015** Author a Knights Order source path with order/common hall, equipment/training scene, oath shrine, trophy content, office and optional stable.
- [x] **GH016** Author an Assassins Guild source path with deliberately mundane exterior, contract/poison/training rooms and physical concealed vault/hidden access.
- [x] **GH017** Author representative source paths for Adventurers, Thieves, Clerics, Rangers, Bards and Alchemists; roster-wide regression source covers all ten guilds.
- [x] **GH018** Implement append-only guild-signature archetypes IDs 401-440, their presentation backends and sparse per-guild room layering.
- [x] **GH019** Add Scene-view look-dev/debug visualization for guild shell, room roles, public/private depth and concealed connections.
- [ ] **GH020** Execute Unity tests/look-dev and record results separately from source completion.
- [ ] **GH021** Add richer exterior `DecorationSpace` adapters for full guild gardens, stable yards, courtyards and street-facing activity beyond the current baseline exterior authoring.
- [ ] **GH022** Integrate guild-house region preference scores into settlement/world guild placement rather than only exposing the policy API.
