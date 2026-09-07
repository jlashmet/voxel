# Binary transport through the GitHub connector

Use this when an agent has a local binary but the GitHub connector cannot safely carry the whole file in one request. It is generic: images, models, archives, Unity assets, and other binary files are reconstructed byte-for-byte.

## Why

Large connector payloads can be silently truncated. In the 2026-09-07 validation, an 18,284-character Base64 file reached GitHub as only 6,546 bytes. Four 4,571-byte parts arrived exactly, and a self-hosted macOS workflow reconstructed and committed a verified 2048x2048 image.

The supported path is therefore: Base64 locally -> <=4,096-character text parts -> `READY` trigger -> self-hosted reconstruction -> SHA-256/size verification -> binary commit -> transport cleanup.

## Stage

```bash
python3 tools/binary_transport/stage_binary.py \
  --input /mnt/data/stone-texture.png \
  --target Assets/Art/Textures/stone-texture.png \
  --job-id stone-texture-2k
```

This writes:

```text
.binary-transport/stone-texture-2k/
  manifest.json
  parts/part-000000.b64
  parts/part-000001.b64
  ...
  READY
```

Use `--overwrite` only when deliberately replacing different content at the target path.

## Connector / agent procedure

1. Run `stage_binary.py` against the local generated/downloaded binary.
2. Create `manifest.json` on the feature branch.
3. Create every `parts/part-XXXXXX.b64` in numeric order. Each text request must contain exactly that part, never multiple parts joined together.
4. Prefer verifying GitHub's returned file size against the local part size. Parts are at most 4,096 characters.
5. Create `READY` **last**. It triggers `.github/workflows/binary-transport.yml`.
6. Monitor **Binary Transport**. Success means exact reconstruction, validation, cleanup, commit, and push completed.
7. Confirm the final binary exists at the manifest's `targetPath`.

With a normal Git client, the whole staged directory may be committed atomically. `READY last` matters when a connector creates separate commits.

Do not rename raw binary bytes to `.cs`/`.txt`, and do not send one giant Base64 `create_file` or `create_blob` request. The limitation is transport, not extension.

## Retry and safety

A failure leaves the job directory for diagnosis. Correct it and change `READY`, or manually run **Binary Transport** with its `job_id`.

Reconstruction rejects traversal/absolute paths, targets below `.git`, `.github`, or `.binary-transport`, symlink escapes, missing/non-contiguous parts, oversized parts, invalid Base64, size/SHA mismatches, and accidental replacement of different existing bytes.

A 4,096-character Base64 part carries about 3 KiB. If an asset would require hundreds or thousands of connector calls, prefer normal Git/Git-LFS or another runner-accessible binary source when available.

## Design record

**Acceptance:** preserve exact bytes; use `[self-hosted, macOS]`; no one-off workflow; detect corruption before commit; document a repeatable connector procedure.

**Hypothesis A:** a large direct `create_blob(base64)` would work. **Falsified:** larger payloads were silently truncated.

**Hypothesis B:** small text parts plus runner reconstruction would preserve bytes. **Confirmed:** independently verified parts reconstructed successfully and the runner committed the resulting 2K binary.

**Selected fix:** 4,096-character parts, small manifest, `READY` trigger, exact SHA/size validation, protected target paths, overwrite opt-in, and automatic cleanup.

**Validation:** `tools/tests/test_binary_transport.py` covers exact round-trip, chunk bounds, overwrite protection, unsafe targets, and bounded manifest size. The live GitHub experiment proved runner-side reconstruction and binary commit/push.
