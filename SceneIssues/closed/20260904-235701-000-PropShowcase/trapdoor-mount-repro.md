# Trapdoor mount discriminator

## Evidence and scope
Standalone run `34003328146`, request `e83a7fd822dab1c40d59f0f84ccd65937071fd28`, source `de0aa1fb4221b06f8f63e6f22fc26ffba77defc8`, ends with an upright blue Trapdoor proxy. Existing visual review rejects it. This repair addresses only the demonstrated mount relationship, not the separately unfinished proxy art.

## Competing causes
1. An incorrect closed-state runtime rotation turns a correctly authored floor hatch upright.
2. The neutral catalogue bounds and facing are already those of an upright door.

At inspected source `9697d365c986a070f6a78db2af99e8c0f449df15`, `WorldObjectPresentationPlanner.Plan` leaves a closed trapdoor's rotation and translation at zero. `UnityWorldObjectPresentationSink` directly scales its production proxy to `BaselineBounds.Size`. `WorldObjectCatalogQuery.BaselineSize` groups Trapdoor with Door at `(12,24,4)` and `DecorationShowcaseRealizer.FacingForWorldObject` falls through to `+Z`. This falsifies a closed-pose rotation error and identifies incorrect baseline authoring/mount input. The sink's `PrimitiveFor` and `ColorForKind` independently explain the unfinished blue slab; changing the camera cannot cure that art defect.

## Selected correction and regression surface
Give only the neutral Trapdoor baseline `(12,4,24)` and floor normal `+Y`. The volume, stable catalogue identity and existing open/close action/planner behavior remain unchanged. Explicit bounds supplied by other production world authors are not modified. No shader, material, renderer, simulation rule or performance budget changes.

`WorldObjectShowcaseMountTests` contains six cases: an independent canonical-size consumer; two deterministic realization contexts; open/close pitch and baseline retention; upright Door and SecretDoor regressions. These invoke real production queries, authoring, state actions and planners without source-string assertions or mock geometry. Structures-owned `PropShowcaseProductionValidation` now also creates the actual production hatch proxy, checks horizontal rendered bounds and emits a required scenario marker. Final screenshot review is still mandatory and must continue rejecting featureless proxy art.

## Validation state
Source and API-contract review only at creation. Unity tests and new built-player scenario have not executed. Existing request `57ab96ca508e70a4d768aa5ddefc6b7343bb531c` / run `34007356710` is preserved for earlier source `9697d365`; this correction requires subsequent exact-source CI. Do not close on this implementation record.
