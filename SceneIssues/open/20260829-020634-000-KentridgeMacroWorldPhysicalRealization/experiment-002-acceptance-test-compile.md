# Experiment 002 — acceptance-test compile

## Hypothesis
After fixing the evidence-driver namespace import, the repaired exact-SHA gate will compile far enough to execute the focused production acceptance and built-player replay.

## Action / source
- Feature source SHA: `5f0e455666bce8ef013f6948963994988f71d4d8`
- CI request SHA: `b5fb7f689538fc0b868c4dc0cbd051be5b28e5e1`
- Workflow run: `33231300309`
- Same focused PlayMode target plus assigned 60-second Kentridge scene replay.

## Result
Product compile failure before test/player execution:
`KentridgeMacroWorldPhysicalProductionAcceptanceTests.cs(84,13): CS0246 TopDownWorldLayout could not be found`.
The evidence driver compiled past its previous error; the remaining missing import was isolated to the new test file. The real-player build stopped on the same repository script-compilation failure, so it produced no visual evidence.

## Verdict / next step
Product failure, not infrastructure. Add `using Game.WorldBuilder.Api` to the acceptance test and keep the feature open. Reuse the assigned `ci-test/fixes/agent-6` transport only after the completed red run; do not promote metadata until a repaired exact-SHA test and built-player replay are green.
