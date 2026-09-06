# Experiment 015 — Rossdam forest dry detour

## Hypothesis
After the accepted Rossdam lake moved south, `forest -> fighting-area-1` needs explicit dry-ground route intent. Adding the same `GoAround` semantics used by other Rossdam routes should preserve the lake footprint and restore graph traversal.

## Action / source
Source `d87a0663fc09dea5f17cdda363c2b1598fcdeffc` adds `forest -> fighting-area-1` → `rossdam-lake` `GoAround` with 75 dm clearance and a focused PlayMode regression requiring a geography-constrained route whose full corridor stays outside the lake. Exact request `240115b522f041909dcf8686e7253262e1672deb`; run `33302489798`.

## Result
Product red. `KentridgeMacroWorldPhysicalRealizationTests.MacroGraphRealizesSettlementsTerrainAwareRoadsAndGeographyThroughProductionWorldBuilder` throws `InvalidOperationException: No dry detour could be built around region 'rossdam-lake'` from `TopDownWorldPhysicalPlanner.BuildAround`. Standalone build/player exit cleanly over 60 s, but focused acceptance fails and capture telemetry reports `SURFACE visible=0`.

## Discriminator
Authoritative layout places `forest=(0,2)`, `fighting-area-1=(0,3)`, `overworld-moordell=(1,3)`, and `overworld-to-rossdam=(-1,4)` at 800 dm per cell. Fixed-seed Rossdam variation resolves the current lake centre to `(-302,+2595)dm` relative to Kentridge with half-extents `450 x 225 dm`. A 36 dm road expands blocking bounds to `468 x 243 dm`; `fighting-area-1=(0,+2400)dm` is therefore inside the blocker (`dx=302`, `dz=195`). Hypothesis A is proven; the shared detour solver is behaving correctly.

## Verdict / next step
The inbound forest route must not be given a semantic detour around water that engulfs its endpoint. Keep exact `900 x 450 x 24 dm` dimensions and move the lake northwest to nominal offsets `X=-400, Z=-155` (fixed-seed resolved centre `(-402,+2650)dm`). This dries `fighting-area-1` (`dz=250 > 243`), keeps `fighting-area-1 -> fighting-area-2` genuinely lake-constrained, and still blocks the direct Rossdam Manhattan route on its `x=-800` vertical leg (`dx=398 < 468`). Remove the accidental inbound forest constraint and replace its regression with endpoint-dry + inbound-dry + outbound/Rossdam-constrained assertions.