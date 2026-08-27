# Experiment 005 — upper correction ownership

## Hypothesis
The remaining upper-circle rectangle is caused by the higher-precedence terrace surface-correction pass claiming the entire expanded `upper-shoulder` footprint, not by the terrace ramp or streaming.

## Action and source
Fresh-bake replay run `33031105049` exercised feature source `9a57364c3bbc5061ef25a3115c6ef08bcf2bc81d` after WorldBuilder was added to the bake fingerprint. The prior experiment had changed the correction footprint material from Dirt to Moss while leaving its rectangle unchanged.

## Result
The lower marked boundary improved, while the upper marked region retained the same large axis-aligned rectangle but it was now grass-colored. Telemetry remained stable (`missingMax=0`). This is the expected discriminator if the correction footprint itself owns the artifact.

## Verdict
Supported. Material choice is not the defect; full transition-footprint ownership is. Urban corrections should repair only the built core and leave transition shoulders to the district terrace/circulation layers. Non-urban natural-ground corrections remain unchanged.

## Next gate
Run the focused ownership regression on the new source, then replay the saved camera from a fresh WorldBuilder-aware bake. Both circles must be visually clean before promotion.
