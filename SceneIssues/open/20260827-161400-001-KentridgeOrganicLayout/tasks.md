# Tasks: Organic Kentridge Layout

**Branch**: `feature/kentridge-organic-layout`  
**Plan**: [kentridge-organic-layout-plan.md](./kentridge-organic-layout-plan.md)  
**Goal**: remove authored roads and explicit town connection edges from Kentridge; place a coherent settlement from spatial intent, infer circulation from the realized layout, and preserve gameplay reachability validation.

## Task format

`- [ ] KOL### [P?] Description — primary files`

`[P]` means the task can proceed in parallel after its phase prerequisites are complete.

---

## Phase 0 — Baseline and blast-radius proof

**Exit gate:** current Kentridge behavior is reproducibly captured before changing the model.

- [ ] KOL001 Record the current Kentridge planning inputs/outputs for at least seeds `0`, `1`, `42`, and one production/showcase seed: town centre, street/plaza data, all 17 stable role IDs, plot positions, orientations, access targets, and architecture envelopes — `Assets/Game/WorldBuilder/Generation/Content/Kentridge/`, `Assets/Tests/`
- [ ] KOL002 Add a deterministic test helper that serializes the semantic `SettlementPlan` into a stable integer/text fingerprint so model migrations can prove intentional versus accidental changes — `Assets/Tests/`, `Assets/Game/WorldBuilder/Generation/Core/SettlementModel.cs`
- [ ] KOL003 Inventory every consumer of `PlannedStreet`, `StreetKind`, `SiteAccessKind.Street`, `PlannedSiteAccess.TargetId`, `SettlementRoadFacingPlacement`, and `SettlementPlotLayout.Along*Street`; classify each as planning, architecture/orientation, traversal facts, voxel geometry, dressing, test, or dead compatibility code — entire repo, durable result appended to this file
- [ ] KOL004 Inventory every Kentridge voxel stage whose semantics depend on roads/sidewalks/frontage paths/access paths, including the stages assembled by `KentridgeCombinedVoxelCatalogueCanonical.Core.cs`; identify which can become generic circulation, which should disappear, and which are unrelated terrain/architecture passes — `Assets/Game/WorldBuilder/Generation/Voxel/`
- [ ] KOL005 Capture the current campaign-site resolution behavior for `ReachableFrom`, `DifferentSiteFrom`, spawn capability, NPC placement, cutscene staging, and secret resolution using the production opening campaign — `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/`, `Assets/Game/Composition/Campaign/Content/`
- [ ] KOL006 [P] Record baseline generation cost and catalogue sizes for the selected Kentridge seeds so the rewrite cannot silently create unbounded candidate/path work — existing performance/parity harnesses and device-matrix budgets

---

## Phase 1 — Make site access independent of streets

**Exit gate:** the old street-based Kentridge can run unchanged physically while all downstream consumers use a street-independent public-access representation.

- [ ] KOL007 Define a street-independent site entrance/access value in Core containing a deterministic world-plan point and public-facing orientation, without a required network target ID — `Assets/Game/WorldBuilder/Generation/Core/SettlementModel.cs`
- [ ] KOL008 Separate structure orientation from the term `FrontageDirection`; retain the same four deterministic quarter-turn values so architecture byte output can remain unchanged during compatibility migration — `SettlementModel.cs`, architecture integration
- [ ] KOL009 Change `BuildingPlot`/`PlannedSite` so their required public access is the site entrance itself; make street/plaza provenance optional compatibility metadata rather than semantic identity — `SettlementModel.cs`
- [ ] KOL010 Add constructor validation for entrance points/orientations and reject production named sites with missing public access once compatibility conversion is enabled — `SettlementModel.cs`
- [ ] KOL011 [P] Add Core tests proving the new access/orientation types contain no renderer, voxel-material, floating-point, or Kentridge-specific data — `Assets/Tests/`
- [ ] KOL012 Implement a compatibility adapter that derives the new entrance/access from the current street-facing Kentridge plot without changing any plot position or building rotation — `SettlementRoadFacingPlacement.cs` / `SettlementPlotLayout.cs`
- [ ] KOL013 Update architecture handoff to orient doors/facades from the new site orientation/access rather than from a street object or street ID — `Assets/Game/WorldBuilder/Generation/Architecture/`, `KentridgeDefinition.StructureIntent`
- [ ] KOL014 Update physical site-realization facts to expose exact entrance/access positions independently of street provenance — `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/`
- [ ] KOL015 Update campaign site-candidate facts and NPC/cutscene placement consumers to read the new site access representation; do not reconstruct entrances from role/archetype names — `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/`
- [ ] KOL016 Add a regression proving old Kentridge seeds produce identical plot positions, orientations, architecture shape programs, and site realization coordinates through the compatibility path — `Assets/Tests/`

---

## Phase 2 — Add generic settlement spatial intent

**Exit gate:** Core can describe where settlement elements prefer to exist without roads or pairwise `Connect` declarations.

- [ ] KOL017 Add a bounded settlement planning envelope/area type using deterministic integer coordinates; it must support inclusion/clearance queries without polygon floating point — `Assets/Game/WorldBuilder/Generation/Core/`
- [ ] KOL018 Add generic area/zone intent for district affinity (`Civic`, `Market`, `Residential`, `Working`, `Noble`) without requiring a road graph; allow multiple weighted influence areas rather than one hard rectangular district — Core settlement planning
- [ ] KOL019 Generalize `PlannedPlaza` into an open-space concept or add a sibling `PlannedOpenSpace` that can represent market squares, forecourts, courtyards, greens, and intentional gaps — `SettlementModel.cs`
- [ ] KOL020 Add site placement constraints/preferences for bounded distance-to-centre, area affinity, edge preference, open-space adjacency, prominence/open-front preference, terrain slope, clearance, and separation; do **not** add pairwise circulation/connectivity authoring — Core settlement planning
- [ ] KOL021 Add deterministic hard-vs-soft constraint semantics: hard constraints must reject a candidate; soft preferences contribute integer score only — Core settlement planning
- [ ] KOL022 Add stable tie-breaking rules based on seed + role/site identity, never iteration order or managed collection ordering — Core settlement planning
- [ ] KOL023 [P] Add validation that planning bounds, candidate counts, search radius, retry/backtrack counts, and score ranges are explicitly bounded — Core + tests
- [ ] KOL024 [P] Add unit tests for each new spatial predicate and score term, including negative coordinates and equal-score ties — `Assets/Tests/`

---

## Phase 3 — Build the deterministic organic site placer

**Exit gate:** a roadless planner can place Kentridge's named sites and open spaces validly for a multi-seed test set.

- [ ] KOL025 Implement deterministic integer candidate generation across the bounded town envelope; candidate generation must be a pure function of `(seed, settlement id, site id, bounds)` and independent of call order — Core settlement planning
- [ ] KOL026 Integrate terrain sampling into candidate evaluation using the authoritative CPU/integer terrain facts; reject excessive slope/invalid support before scoring aesthetics — Core + terrain sampler boundary
- [ ] KOL027 Implement an occupancy/clearance structure for placed footprints and open spaces so overlap tests are deterministic and bounded — Core settlement planning
- [ ] KOL028 Define a stable placement ordering based on constraint hardness/footprint difficulty while preserving role identity; document that ordering is an algorithmic choice, not semantic identity — Core planner
- [ ] KOL029 Implement bounded candidate scoring and selection for named landmarks/sites — Core planner
- [ ] KOL030 Implement bounded fallback/backtracking for unsatisfied hard constraints; fail with a diagnostic result after the configured bound rather than searching indefinitely — Core planner
- [ ] KOL031 Resolve a public entrance candidate for each placed structure based on surrounding free space/open-space exposure, not a street; reject entrances blocked by another footprint or invalid terrain — Core planner
- [ ] KOL032 Resolve the building's quarter-turn orientation from the chosen entrance/public side, keeping architecture deterministic — Core planner + architecture handoff
- [ ] KOL033 Place open spaces before/with landmarks where required so a church forecourt or market square is an intentional void rather than leftover space — Core planner
- [ ] KOL034 Add anonymous-building/fabric placement that fills suitable remaining settlement space according to district/density policy without blocking named entrances or open spaces — Core settlement planning, existing composition policy
- [ ] KOL035 Add multi-seed planner tests asserting: all stable role IDs exist once, no footprint overlaps, all entrances are clear, open-space clearance is preserved, slope limits hold, and planning terminates within configured bounds — `Assets/Tests/`
- [ ] KOL036 Add determinism tests that repeat the same seed under different candidate/site evaluation iteration orders and produce the same semantic plan fingerprint — `Assets/Tests/`

---

## Phase 4 — Rewrite Kentridge authoring as town character, not a map

**Exit gate:** `KentridgeTownPlanner` contains no fixed street axes and no `AlongHorizontalStreet` / `AlongVerticalStreet` placement for named sites.

- [ ] KOL037 Replace `MainSpineId`, `MarketStreetId`, `ResidentialStreetId`, `EastServiceLaneId` and their fixed X/Z/width constants with a bounded Kentridge settlement envelope plus high-level centre/terrain policy — `KentridgeTownPlanner.cs`
- [ ] KOL038 Preserve the stable `KentridgeRole` enum values exactly; add a regression that changing generation order cannot renumber or remap content roles — `KentridgeDefinition.cs`, tests
- [ ] KOL039 Express the market square as an important open-space/centre intent, but allow its exact bounded placement/shape to participate in deterministic planning rather than forcing all topology through its coordinates — Kentridge content planning
- [ ] KOL040 Express the church as civic/prominent/near-centre/open-front intent without specifying a road, side of road, or explicit connection — `KentridgeTownPlanner.cs`
- [ ] KOL041 Express mayor house as civic and appropriately near the civic centre/church area without requiring it to be opposite the church — `KentridgeTownPlanner.cs`
- [ ] KOL042 Express inn/pub/shops as market/high-activity/open-space-affine intent without explicit routes or fixed frontage coordinates — `KentridgeTownPlanner.cs`
- [ ] KOL043 Express named residences as residential affinity plus bounded individual variation; keep named identity but remove fixed positions and ±0.8m-only jitter as the main source of variation — `KentridgeTownPlanner.cs`
- [ ] KOL044 Express warehouse as working/edge-accessible intent and mansion as noble/quieter/lower-density intent, without service-lane coordinates — `KentridgeTownPlanner.cs`
- [ ] KOL045 Keep the well associated with the market civic space semantically, but do not make its placement imply a road graph — `KentridgeTownPlanner.cs`
- [ ] KOL046 Rework `SettlementCompositionPolicy` inputs so density, lot envelopes, palettes, landmark rarity/open-space preferences remain useful without street frontage assumptions — `KentridgeTownPlanner.cs`, Core composition policy
- [ ] KOL047 Delete Kentridge's use of `SettlementRoadFacingPlacement` once the new planner passes all Phase 3/4 tests; leave generic street helpers intact until repo-wide usage is proven — Kentridge content only
- [ ] KOL048 Add semantic snapshot tests that assert Kentridge's authored definition contains no street IDs, street widths, fixed street axes, or explicit site-to-site `Connect` edges — tests should inspect typed plan/intents, not source strings

---

## Phase 5 — Infer circulation from the realized settlement

**Exit gate:** the planner produces a deterministic traversable circulation result from geometry and terrain, with no authored Kentridge edges.

- [ ] KOL049 Define generic circulation terminals from realized facts: public site entrances, open-space access regions, settlement boundary/arrival opportunities, and required vertical transition points; Kentridge content does not enumerate edges — Core/runtime settlement planning
- [ ] KOL050 Discover candidate local movement relationships from geometric proximity/free-space visibility using bounded deterministic integer queries — circulation planner
- [ ] KOL051 Reject candidate movement relationships that intersect occupied footprints, protected open-space obstacles, impossible slopes, or blocked vertical transitions — circulation planner
- [ ] KOL052 Build a sparse deterministic circulation skeleton that connects the settlement's usable public fabric while avoiding a road-grid aesthetic; use stable graph construction/tie-breakers and add limited loops to avoid a single tree-shaped town — circulation planner
- [ ] KOL053 Route skeleton edges through free space with a bounded integer path search/cost field that prefers natural gaps and modest grades; no floating-point navigation truth — circulation planner
- [ ] KOL054 Treat plazas/courtyards/open ground as traversable areas rather than drawing synthetic paths across them — circulation planner
- [ ] KOL055 Detect tight passages and classify them as alleys/passages; detect steep valid transitions and classify them as stairs/ramps/terrace links; classification is derived, not Kentridge-authored — circulation planner
- [ ] KOL056 Merge nearby/co-linear route segments and remove redundant micro-paths so inferred circulation does not become visual spaghetti — circulation planner
- [ ] KOL057 Compute per-segment contextual width/intensity from surrounding density/open-space context; do not restore `MainRoad/Secondary/Service` as authored hierarchy — circulation planner
- [ ] KOL058 Produce a traversal-facts representation suitable for gameplay reachability that is independent of visible surface material or rendering — Core/composition boundary
- [ ] KOL059 Add tests proving every named public entrance participates in the settlement's traversable public component for valid seeds, or generation fails explicitly when it cannot — `Assets/Tests/`
- [ ] KOL060 Add determinism/order-independence tests for inferred circulation graph and routed geometry — `Assets/Tests/`

---

## Phase 6 — Realize circulation as terrain/voxels

**Exit gate:** Kentridge contains no mandatory road/sidewalk rendering path; circulation appears as context-sensitive walkable geometry.

- [ ] KOL061 Map current Kentridge combined-catalogue road-related stages to replacement semantics: keep unrelated terrain/architecture stages untouched; mark road surface, sidewalks, frontage paths, urban access and street dressing for replace/remove decisions — `KentridgeCombinedVoxelCatalogueCanonical.Core.cs`, `Assets/Game/WorldBuilder/Generation/Voxel/`
- [ ] KOL062 Add a circulation voxel catalogue/input that consumes inferred route/open-space geometry rather than `PlannedStreet` — `Assets/Game/WorldBuilder/Generation/Voxel/`
- [ ] KOL063 Implement terrain grading/support for inferred walkable segments with bounded edits that do not flatten the town into road corridors — circulation voxel generation
- [ ] KOL064 Implement optional path surfacing for segments whose context calls for it; allow valid circulation to remain natural ground — circulation voxel generation
- [ ] KOL065 Implement alley/passages without sidewalks and ensure building entrances meet the traversable surface cleanly — circulation + architecture integration
- [ ] KOL066 Implement stair/ramp/terrace transition realization from inferred vertical-link classification, preserving discrete collision occupancy — circulation voxel generation
- [ ] KOL067 Rework market/open-space surfacing so the square/courtyards are areas, not widened road intersections — Kentridge open-space voxel generation
- [ ] KOL068 Replace street-specific dressing inputs with context derived from district, open space, nearby entrances, density and circulation intensity — Kentridge dressing catalogues
- [ ] KOL069 Decide whether `MaterialRole.RoadSurface` should be generalized/aliased to a circulation/ground-surface role; avoid a breaking rename unless it meaningfully removes semantic leakage — `SettlementModel.cs`, material adapter, voxel catalogue
- [ ] KOL070 Add seam/order-independence tests proving circulation rasterized region-by-region is byte-identical to equivalent whole-area generation — voxel parity tests

---

## Phase 7 — Preserve WorldBuilder gameplay semantics

**Exit gate:** campaign constraints remain semantic and are validated against realized town traversability, not authored streets.

- [ ] KOL071 Replace street-specific settlement traversal facts with generic settlement traversal facts sourced from inferred circulation/free-space realization — `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/`
- [ ] KOL072 Update `SettlementPlanWorldBuilderFacts` / site-candidate fact adapters to expose district, open-space, entrance, capability and traversal data without requiring street IDs — composition integration
- [ ] KOL073 Update `ReachableFrom` resolution to query the generic realized traversal graph/component; preserve the campaign API and `TraversalProfile` semantics — WorldBuilder runtime/composition
- [ ] KOL074 Prove `DifferentSiteFrom` and other non-traversal site constraints are unchanged by the migration — campaign planning tests
- [ ] KOL075 Prove player spawn site selection, NPC site assignment, cutscene actor staging and secret placement continue to bind to exact realized site facts rather than archetype/name guesses — composition tests
- [ ] KOL076 Add a negative campaign test where a deliberately invalid generated layout has an unreachable required destination and planning/realization rejects it with a useful diagnostic — WorldBuilder integration tests
- [ ] KOL077 Add production opening-campaign regression coverage across several seeds; all authored semantic requirements must resolve without Kentridge-specific connection declarations — campaign integration tests

---

## Phase 8 — Remove obsolete Kentridge street machinery

**Exit gate:** no Kentridge runtime/generation path depends on authored roads; generic street support is retained only if another settlement genuinely uses it.

- [ ] KOL078 Re-run the Phase 0 street-consumer inventory after migration and identify remaining non-Kentridge users — repo-wide
- [ ] KOL079 Remove Kentridge-only road constants, street construction, street-facing plot helpers, road-width/setback inputs, and compatibility translation paths now unused — Kentridge planner/core adapters
- [ ] KOL080 Remove or rename Kentridge voxel catalogues/passes whose only purpose was authored roads/sidewalks/frontage paths; do not delete reusable generic feature infrastructure — `Assets/Game/WorldBuilder/Generation/Voxel/`
- [ ] KOL081 If `PlannedStreet`, `StreetKind`, or `SettlementRoadFacingPlacement` still serve Hightown/other content, leave them as optional settlement-style primitives and document that Kentridge does not use them; otherwise delete them with compile/test proof — Core + consumers
- [ ] KOL082 Remove `SiteAccessKind.Street`/legacy target-ID plumbing only after no active consumer requires it; keep migration code out of the final architecture — Core/composition
- [ ] KOL083 Update XML comments/docs that currently describe `SettlementPlan` as containing streets and "street-facing plots" — Core/model docs
- [ ] KOL084 Update World Feature Authoring architecture notes/examples to describe settlement intent → placement → inferred circulation → voxel realization and explicitly state that authored connectivity is a gameplay constraint, not Kentridge layout content — `specs/002-world-feature-authoring/`

---

## Phase 9 — Quality, visual, determinism, performance, and CI gates

**Exit gate:** the roadless Kentridge is demonstrably valid, deterministic, performant, and visually better before merge.

- [ ] KOL085 Build a multi-seed invariant suite (minimum 32 representative seeds) covering bounded completion, unique named roles, no overlap, terrain support, clear entrances, open-space preservation, public-component participation, and campaign resolution — `Assets/Tests/`
- [ ] KOL086 Add same-seed replay tests across fresh planner instances and altered evaluation order; semantic plans and inferred circulation must be byte/fingerprint identical — `Assets/Tests/`
- [ ] KOL087 Add different-seed diversity metrics that prove variation is meaningful (positions/orientations/fabric/circulation change) while hard Kentridge semantic invariants remain true; do not assert exact aesthetic coordinates — tests
- [ ] KOL088 Run existing feature-generation region-order/sub-volume parity tests against catalogues containing inferred Kentridge circulation — parity tests
- [ ] KOL089 Add rendered regression captures for several seeds from consistent viewpoints: town overview, market/open space, residential fabric, working edge, and at least one vertical transition — showcase/test scene workflow
- [ ] KOL090 Review captures specifically for grid/road artifacts, path spaghetti, inaccessible-looking doors, excessive flattening, repetitive spacing, dead courtyards, and implausible stairs; record only actionable failures in the plan
- [ ] KOL091 Measure planner candidate evaluations, backtracking, circulation nodes/edges, route-search work, catalogue sizes, voxels written, and generation time against Phase 0 baseline and authoritative budgets — performance harness/device matrix
- [ ] KOL092 Add hard budget assertions where an existing authoritative numeric budget applies; otherwise document observed cost before proposing any new budget — tests/specs
- [ ] KOL093 Review final diff for accidental changes below architecture/shape-program/rasterizer boundaries and for any newly introduced floating-point authoritative world state — repo-wide
- [ ] KOL094 Run the smallest relevant EditMode/PlayMode suites through `tools/unity-run.sh` only when local Unity use is appropriate; record exact passing tests in the plan — local validation
- [ ] KOL095 Create the exact targeted-CI request on `ci-test/feature/kentridge-organic-layout` from the final feature SHA following `AGENTS.md`; monitor that exact request rather than creating retries/temporary branches — CI
- [ ] KOL096 After green targeted CI, update the concise plan with selected final design, material results, falsified assumptions, and remaining gates; keep this task list as the implementation history/checklist — specs

---

## Recommended implementation sequence

Do not begin with visual path generation. The risk-retiring order is:

1. **KOL001–016** — decouple semantic site access while preserving today's world exactly.
2. **KOL017–036** — prove a bounded deterministic roadless site placer in Core.
3. **KOL037–048** — move Kentridge authoring onto spatial intent and remove fixed streets from the town definition.
4. **KOL049–060** — infer traversability/circulation from the realized settlement.
5. **KOL061–070** — turn inferred circulation into terrain/voxel presentation.
6. **KOL071–077** — prove campaign semantics against realized traversability.
7. **KOL078–096** — remove compatibility code and complete visual/performance/CI validation.

The first major checkpoint is after **KOL048**: Kentridge should already be a valid roadless semantic settlement plan before any effort is spent making inferred paths pretty.