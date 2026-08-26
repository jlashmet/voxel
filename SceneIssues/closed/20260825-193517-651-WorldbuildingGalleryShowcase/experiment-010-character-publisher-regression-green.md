# Experiment 010 — character publisher regression green

**Hypothesis**

Adding grass-interactor lifecycle hooks to the standard character equipment controller should preserve the controller's existing equipment behavior on the fully master-integrated feature head.

**What was performed**

- Integrated source commit: `02994672139fa9fdf667c365789aa31ae58fed06`.
- CI request commit: `bac56d007cd48689a4b5bfb8977ad7cd1e16cbc9` on `ci-test/fixes/agent-8`.
- GitHub Actions run `32938694637`, job `98084996351`.
- Requested EditMode test `MountingForce.Game.Composition.CharacterEquipment.Tests.CharacterEquipmentControllerTests.Controller_EquipsById_ReplacesSameSlot_AndUnequips`.

**Result**

The job completed successfully. Unity returned status 0 and the workflow reported `Executed 1 test case(s).`; `ci/single-test` was published as success. Artifact `single-test-32938694637` was uploaded as id `9595771731`.

**What was learned**

The standard character component can own register/publish/unregister hooks for grass displacement without regressing its established equipment replace/unequip behavior. The master integration also remained compatible with this game-to-rendering dependency.

**Next**

Replay the original Worldbuilding Gallery scene-issue fixture through the real standalone player using temporary CI routing, inspect the original circled region, then remove the temporary routing and finish with a durable focused green request.
