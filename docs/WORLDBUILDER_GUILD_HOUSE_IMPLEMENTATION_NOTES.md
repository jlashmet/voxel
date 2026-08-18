# Guild House Prototype Implementation Notes

The first building-scale guild implementation now exists on `agent/worldbuilding-decorations`.

## Implemented source path

1. `GuildHouseProgramCatalog` defines ten guild identities and semantic room programs.
2. `GuildHouseRoomSelector` chooses required/optional rooms deterministically.
3. `GuildHouseTopologyPlanner` assigns public/private depth and concealed-access semantics.
4. `GuildHouseSpatialPlanner` maps semantic topology into concrete non-overlapping voxel room blocks.
5. `GuildHousePrototypeComposition` converts each room block into a region-aware `DecorationSpace` and `DecorationContext`.
6. `GuildHouseRoomDecorationResolver` currently maps Wizards and Druids onto existing semantic scene resolvers.
7. `GuildHousePrototypeAuthoring` emits the baseline guild shell.
8. `GuildHouseFurnishedPrototypeAuthoring` authors shell plus resolved room geometry.

## First two guilds

### Wizards Guild

Baseline form: multi-floor tower. Existing scenes reused by room role include WizardLibrary, EnchantersWorkshop, RitualChamber, SpellClassroom, ForbiddenArchive and ArcaneGallery. Region context is preserved so the same semantic guild can use Kentridge, Hightown, Moordell, Rossdam, Fairy Village or Orc Village materials/presentation.

### Druids Lodge

Baseline form: broad low lodge with an open central courtyard/roof strip. Existing scenes reused include EnchantedGrove, DruidShrine, FairyClearing and AlchemyLab for the herb/workshop role. Garden spaces are explicitly marked exterior.

## Rendering maturity

The prototype deliberately follows the decoration project's breadth-first policy. Shells and many furnishings begin as box/voxel assemblies. Existing mesh/thin-surface backends remain valid for content that already requests them. Signature architecture and props can later move to curved/SDF/procedural implementations without changing guild program identity or stable decoration IDs.

## Known gaps

- Other eight guilds still need room-to-scene dispatch.
- Assassin/Thieves concealed access is semantic only; physical secret passages/doors are not yet authored.
- Guild-specific exterior crests, yards, stables and gardens need richer composition.
- IDs 401-440 remain reserved signature guild props.
- Source regressions are committed, but Unity/CI execution is still a separate validation gate.
