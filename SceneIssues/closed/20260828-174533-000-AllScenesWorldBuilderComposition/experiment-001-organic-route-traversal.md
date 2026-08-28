# Experiment 001: organic route traversal

- **Hypothesis:** WorldBuilder campaign validation still reads only legacy `SettlementPlan.Streets`, so modern Kentridge (zero streets, inferred `Routes`) is falsely disconnected.
- **Action/source:** Run `33201049016` on exact source `406a30415537634d9b122287a516e952e5ef1fda`, targeting the production Kentridge launch acceptance; inspect `SettlementStreetTraversalFacts` after the failure.
- **Result:** The built macOS player built/launched successfully, but the focused acceptance failed because `first-destination` was not reachable from `starting-pub` under `NormalParty`. `SettlementStreetTraversalFacts` built segments only from `plan.Streets` and accepted only `Street`/`Plaza` access, while current Kentridge emits zero streets and `SiteAccessKind.Route` for its named sites.
- **Verdict:** Supported. The reachability adapter was stale relative to the organic settlement contract.
- **Next:** Build the traversal graph from both legacy streets and inferred routes, preserve explicit target-id access semantics, and regress pub-to-every-site reachability without restoring authored streets.
