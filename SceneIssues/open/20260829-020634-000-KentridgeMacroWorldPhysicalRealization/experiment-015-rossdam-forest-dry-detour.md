# Experiment 015 — Rossdam forest dry detour

## Hypothesis
After the accepted Rossdam lake moved south, `forest -> fighting-area-1` needs explicit dry-ground route intent. Adding the same `GoAround` semantics used by other Rossdam routes should preserve the lake footprint and restore graph traversal.

## Action / source
Source `d87a0663fc09dea5f17cdda363c2b1598fcdeffc` adds `forest -> fighting-area-1` → `rossdam-lake` `GoAround` with 75 dm clearance and a focused PlayMode regression requiring a geography-constrained route whose full corridor stays outside the lake. Exact request `240115b522f041909dcf8686e7253262e1672deb`; run `33302489798`.

## Result
Product red. `KentridgeMacroWorldPhysicalRealizationTests.MacroGraphRealizesSettlementsTerrainAwareRoadsAndGeographyThroughProductionWorldBuilder` throws `InvalidOperationException: No dry detour could be built around region 'rossdam-lake'` from `TopDownWorldPhysicalPlanner.BuildAround`. Standalone build/player exit cleanly over 60 s, but focused acceptance fails and capture telemetry reports `SURFACE visible=0`.

## Verdict / next step
The semantic solution is necessary but not sufficient. Do not rerun this SHA and do not change clearance blindly. Discriminate: (A) lake now contains a `forest`/`fighting-area-1` endpoint corridor, making any dry detour impossible, versus (B) endpoints are dry and the reusable four-candidate rectangular detour solver is too restrictive. Resolve authoritative node centres and lake bounds first; preserve the accepted `900 x 450 x 24 dm` lake and genuine direct-Rossdam obstruction when selecting the next fix.