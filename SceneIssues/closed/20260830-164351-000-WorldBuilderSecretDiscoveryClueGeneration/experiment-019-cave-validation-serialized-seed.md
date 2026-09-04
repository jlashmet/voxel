# Experiment 019 — Cave validation serialized seed

## Symptom

Exact-SHA run `33834551887` (source `7c493cecf7d8b46377472b7b3a18fafb45cfb5e6`) still reported the same `Kentridge organic planner exhausted its bounded candidate set for AwonHouse` exception during `CaveWorldBuilderSecretPocketValidation.OnEnable`, even though experiment 018 changed the C# field initializer to production Showcase seed `0x5EED1234`.

The run remained useful: all persistent EditMode phases passed and the standalone requested SceneIssue replay passed, while automatic module validation failed specifically in the CaveWorldBuilder player.

## Root cause isolation

The existing Unity scene serializes `[SerializeField]` values independently of the C# field initializer. Inspecting `CaveWorldBuilderSecretPocketValidation.unity` after the failed run showed:

- `m_Seed: 1128355397` = `0x43415645` (the old validation-only seed)

The production `Assets/Scenes/VoxelShowcase.unity` scene serializes:

- `m_Seed: 1592594996` = `0x5EED1234`

Therefore run `33834551887` never exercised the intended seed change from experiment 018; the scene asset overrode the new initializer with its old serialized value. This explains the byte-for-byte-equivalent bootstrap failure without requiring a second product hypothesis.

## Change

Update only `CaveWorldBuilderSecretPocketValidation.unity` so its serialized `m_Seed` is `1592594996`, matching the shipping VoxelShowcase scene and the validation component's C# default. Production code and production scenes remain unchanged.

## Expected discriminator

A fresh exact-SHA run must no longer enter `ShowcaseWorld` with `0x43415645`. It should either:

1. pass Showcase catalogue construction and reach the CaveWorldBuilder validation's own `CaveWorldBuilder secret validation ready:` / wall-destruction evidence, validating the root cause; or
2. fail with a different, later acceptance symptom that can be attributed to the actual Cave validation path.

Repeating the same `AwonHouse` bootstrap stack after this asset correction would falsify the seed hypothesis and require a smaller reproduction before another fix.
