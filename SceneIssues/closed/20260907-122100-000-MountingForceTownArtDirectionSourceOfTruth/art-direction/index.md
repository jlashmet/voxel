# Mounting Force — Mountain Forest Town Art Direction Source of Truth

## Frozen roster
This package covers the six Mountain Forest towns named by the coordinator/reference work:

1. Kentridge
2. Hightown
3. Moordell
4. Rossdam
5. Cambridge
6. Wharfington

The original project also packages Orc Town/Orc Village and Fairy Village, but those are separate settlements rather than members of this Mountain Forest town package. They are therefore intentionally excluded from the six-town acceptance set.

## Provenance and confidence
Primary historical source: `jlashmet/mounting-force` at `f37512e0175f46e14ad2d7aeb5ecea290af14a14` (`MountingForce.xcodeproj/project.pbxproj`, `Art/*`, story/cut-scene files). Secondary check: `jlashmet/MountingForce2` at `5e111d5a72e441824311b4a3a176f72e9c86f653`; it did not yield a competing Kentridge/Hightown/Cambridge town canon. Project North Star imagery supplies the new shared visual language, not old-game factual place inventory.

Historical confidence differs by town. Kentridge/Hightown/Moordell/Rossdam have direct old-project scene/map/story evidence. Cambridge is coordinator-canonical as a water town but no matching old-project filename was recovered in the connected archive. Wharfington is coordinator/reference-canonical but likewise lacks a recovered named interior set in the pinned project. For Cambridge and Wharfington, this pack therefore labels newly authored places as **derived** and does not pretend they are old-game facts.

## Shared Project North Star environment language
- **Storybook, not photoreal.** Broad readable shapes, slightly exaggerated roofs/towers/doorways, softened edges, selective hand-crafted irregularity, and painterly material breakup.
- **Modern 3D interpretation of old identity.** Preserve theme, economic role, landmark hierarchy, and material/color spirit; do not reproduce 2D sprite proportions or tile-for-tile layouts.
- **Readable hierarchy.** From a distance: terrain silhouette, landmark silhouette, district massing, major road/water network. Up close: doors/windows/signage, market goods, tools, vegetation, props.
- **Warmly inhabited.** Even poorer places use intentional craft, maintenance patches, gardens, smoke, lamps, storage, and public gathering spaces rather than generic ruin dressing.
- **Material families are stylized and chunky:** dressed/field stone, plaster, timber, slate/tile/shingle, forged metal, copper/bronze, canvas, glass/crystal, water/vegetation where town-specific.
- **Interiors match exteriors.** Structural material, roof pitch, craft/economy, and palette must carry through; interiors are not generic tavern rooms pasted into each town.
- **No UI or character concepting.** People may appear only as tiny scale/context figures; the subject is environment/architecture.

## Town comparison matrix
| Town | Immediate read | Palette | Dominant materials | Economy/culture | Terrain/infrastructure | Signature anti-confusion motif |
|---|---|---|---|---|---|---|
| Kentridge | Welcoming market/civic town | cream, warm timber, terracotta, muted blue, garden green | plaster, timber, fieldstone, tile | mixed trades, local civic life, food/supply pressure | walkable lanes, central well/market, church/inn/warehouse | blue-painted civic accents + warm half-timber massing around a well/market |
| Hightown | Lean upland working town under pressure | slate, weathered brown, muted ochre, moss green | rough stone, dark timber, slate | working families, ration/distribution hardship, church community | terraced grades, retaining walls, steep stairs/lanes | vertical slate-roof terraces and severe church/distribution silhouettes |
| Moordell | Wealthy ordered imperial/noble town | pale stone, burgundy, gold, deep green, blue-gray slate | dressed stone, fine plaster, carved timber, metalwork | lords/nobles, favored supplies, formal civic power | broad planned streets, formal square, walls/gates, ordered gardens | axial civic planning + burgundy/gold noble heraldic rhythm |
| Rossdam | Hard-edged trade/arms town | iron gray, brick red, charcoal, tan leather, brass | heavy stone, timber, forged iron, tile | armor/weapon/magic commerce, military/trade energy | broad merchant street, fortified-feeling gateways, loading courts | repeated forge/ironwork silhouettes and red-brass trade signage |
| Cambridge | Water town | sea green, weathered blue, pale stone, copper/verdigris, warm wood | waterfront stone, timber piles/decks, copper, blue slate | boats, ferries, fishing/water trade, civic waterworks | docks, quays, canals/inlets, bridges, water crossings | water must dominate every overall/public composition; bridge-and-quay skyline |
| Wharfington | Forest-edge river/wharf exchange town | forest green, river blue, amber wood, gray stone, muted rust | timber, fieldstone, shingle, rope/canvas | forest goods, river/road exchange, crafts | wharf/landing, forest road junction, timber yards, modest bridge | timber wharf/warehouse rhythm merging directly into dense forest edge |

## Anti-drift rules
1. Never use the current voxel town look as evidence that a style is correct; this pack supersedes it as art-direction reference.
2. A town must remain recognizable in grayscale silhouette and in an unlabelled material/palette crop.
3. Do not give every town the same half-timber house with a palette swap. Roof pitch, wall-to-roof ratio, street width, foundation type, civic silhouette, and edge treatment must differ.
4. Do not add canals/docks to non-water towns merely for visual interest; Cambridge owns the strong water-town identity and Wharfington uses a smaller working wharf/forest exchange identity.
5. Moordell must read materially richer and more ordered than Kentridge/Hightown. Hightown must read more vertical/austere than Kentridge.
6. Rossdam trade buildings should read heavier and more industrial/martial than Kentridge shops.
7. Every concept must map to the locked inventory in `town-briefs.md`; source-backed and derived places remain clearly distinguished.

## Required concept slots
For **each** frozen-roster town the committed concept set contains exactly the minimum acceptance categories, with additional infrastructure visible where appropriate:
- `overall.png` — elevated/overall town view
- `exterior-01.png` — source-backed or explicitly derived building exterior
- `exterior-02.png` — second building exterior
- `interior-01.png` — first building interior
- `interior-02.png` — second building interior
- `public-space.png` — street/plaza/dock/public-space view

See `town-briefs.md` for the locked pre-concept inventory and exact shot subject for every file.
