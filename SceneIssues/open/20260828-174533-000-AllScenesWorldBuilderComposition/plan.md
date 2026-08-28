# Plan: WorldBuilder-only scene composition

## Evidence / hypotheses

- Architecture capture has no screenshots, frames, poses, or annotations; there are no visual marked regions to replay. Evidence is production scene/bootstrap ownership plus runtime CI.
- Audit found scene-owned backend composition in Kentridge/Showcase/lookdev paths. Pure GUID-preserving source relocation did **not** remove direct generator selection, rejecting the hypothesis that location/naming alone was the defect.
- Selected boundary: scenes may own camera/lighting/UI/input/metrics, but generated environment intent enters through WorldBuilder semantic recipes/town authoring; reusable composition owns concrete storage/catalogue/generator choices.

## Fix / discriminator

- Added `WorldEnvironmentSpec` recipes and shared Showcase resolution while preserving distinct small/fortified/gallery compositions.
- Routed both Kentridge and Hightown through `WorldBuilderTownAuthoring`; preserved opaque backend plans and serialized scene GUID/assembly compatibility.
- Behavioral EditMode coverage executes both production town recipes, checks distinct results, semantic Showcase recipes, and supplements with a scene-source backend guard.
- Final request on source `406a30415537634d9b122287a516e952e5ef1fda`, run `33201049016`: real macOS player **built/launched successfully**, but focused production Kentridge acceptance failed: `first-destination` not reachable from `starting-pub` under `NormalParty`.
- Discriminator confirmed stale traversal facts: current Kentridge intentionally has zero legacy `Streets` and inferred `Routes`, while `SettlementStreetTraversalFacts` read only streets and rejected `SiteAccessKind.Route`.
- Product fix through `5c87db87921c4a8b25dbaaf0b9866cecb98ce375`: shared traversal now graphs both streets and arbitrary inferred route segments, splits exact integer intersections/endpoints, and binds access by declared path id. Regression keeps Kentridge at zero streets and requires pub-to-every-site reachability through production traversal.

## Blast radius / cost / remaining gate

- Legacy orthogonal street handling remains supported; route support is additive and deterministic. No per-frame work changes.
- Graph construction is bounded by settlement path segments: pairwise exact-intersection preprocessing plus existing shortest-path lookup; Kentridge has 16 short three-point routes.
- No other capture or feature-branch CI request file is changed. The consumed final CI transport is left untouched; corrected exact-SHA focused/built-player green remains the only promotion gate.
