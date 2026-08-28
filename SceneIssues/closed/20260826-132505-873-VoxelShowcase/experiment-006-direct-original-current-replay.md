# Experiment 006 — direct original/current replay comparison

## Hypothesis
The `floating mailbox` note refers to the orange-topped east-market street fixture, and the unsupported fixture is absent from the current authoritative saved pose.

## Action
On diagnostic feature `6b4133ebba54ba9efc74c99a28d47019d2297076`, exact request `bd9c2397044ea295c73923affff2a5d2e46d0369` ran as `33123201112`. The assigned regression temporarily copied the immutable original `screenshot-001.png` into the normal single-test artifact beside the fresh-bake replay. The saved camera replay ran at the recorded `1928x836` dimensions.

## Result
The original image visibly contains the orange lantern/post assembly with a bulbous gray foot suspended above the brown shoulder. The current exact-pose replay contains no lamp/mailbox fixture in that reported region. The production-path lamp-support regression also passed.

## Verdict
Confirmed. The reported visual defect is absent in the current authoritative pose, while the permanent regression guards physical support if the authored east-market lamp is generated. Remove the temporary diagnostic export before final CI.
