# Experiment 002 — working-yard ground ownership

**Hypothesis.** The saved-pose float is not only a missing smooth lamp pole: the captured east-market lamp is also anchored above the sidewalk because its placement uses the macro profile while that sidewalk is the north shoulder of the working-yard district terrace.

**Action / source.** Inspected attempt-1 `verification-final.png` from exact request `6d16727f3651fc041c50f96b459c8c11634765a5` and traced `(1530, 549)` through `KentridgeStreetDressingCatalogue`, `KentridgeUrbanSidewalkCatalogue`, and `KentridgeDistrictTerraceCatalogue`. Showcase seed `1592594996`: macro placement Y = 256; working-yard north-shoulder step Y = 232.

**Result.** The replay still visibly floats the entire pedestal/pole above the painted sidewalk. The point is inside working-yard bounds `(1436..1804, 516..874)` and specifically shoulder step 4/6; the road does not own z=549. The old test proved pole-to-lantern continuity but never ground contact.

**Verdict.** Confirmed. Keep the Planar pole fix, but ground the captured working-yard placement on the same deterministic stepped-shoulder math. Extend the PlayMode regression to evaluate the real working-yard terrace program at the captured column and require lamp origin Y to equal that generated solid surface.

**Current source.** `833395f981abe0055b1392ec95e2454523c451df`.
