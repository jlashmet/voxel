# Experiment 005 — view-dependent convergence

## Question
Why did exact run `33129981847` emit fallback-gray clean evidence although the behavioral regression and replay passed?

## Discriminator
The earlier test waited before teleporting the camera for a one-shot render. The real-player replay instead pins the saved scene-issue camera continuously; at that pose it materializes the authored gate. Existing showcase screenshot tests likewise free the camera before moving acceptance viewpoints.

## Result
Run `33130356493` pinned the exact saved camera for 12 seconds. The behavioral regression and real-player replay passed, and the clean 1928x836 frame no longer showed fallback terrain: with the opened leaves swung inward, the very close original pose looks through the doorway at inner masonry. The standalone replay at the same pose shows the closed timber/iron gate, confirming camera ownership and convergence. The original pose therefore cannot make retained open leaves judgeable by itself.

## Action / falsifier
Keep `verification-final.png` at the exact capture pose and add full-resolution diagonal detail views from outside the gatehouse, biased to each hinge. Reject if either retained timber/iron leaf or the clear centre still cannot be inspected. The run itself exceeded the five-minute job limit because of a cold 202-second showcase bake and remains infrastructure-failed, not green evidence.
