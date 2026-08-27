# Experiment 002 — bounded layout choice

**Hypothesis.** A full straight flight would satisfy rise/headroom but could collide with existing rear furniture in the smallest generated interiors.

**Action / source.** Compared the first straight-flight layout with production Kentridge dimensions/furniture zones. Current generated forms are 66–132 dm deep and 2–3 storeys; furniture is deliberately rear-biased. Reworked the circulation composer to a `StairConfig` switchback in the front half, choosing the side opposite the authored door bias.

**Result.** The two flights plus landing occupy a bounded front shaft; the first-flight opening starts only when headroom fails, the return flight/landing remain open, and upper guards leave the return-flight egress unblocked. Added cost is `stepCount + 7` primitives per floor transition.

**Verdict.** Switchback is the smaller safe production layout. Final production-path regression and saved-pose replay remain the gates.
