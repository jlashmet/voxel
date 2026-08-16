# Character Factory CI

The CI smoke test deliberately runs one real category pipeline end to end on the self-hosted macOS runner.

Current fixture: `fixtures/sunlit_cleric_staff.jpg`, a small crop derived from the user's **Sunlit Cleric by the Waterfall** reference image. The smoke test routes it through `WeaponPipeline`, so CI exercises:

1. local Hunyuan3D-2mv image-to-mesh inference,
2. raw GLB export,
3. headless Blender rigid-part processing,
4. FBX export,
5. manifest/runtime metadata generation, and
6. a second headless Blender import to prove the emitted FBX contains a mesh.

Hunyuan source, Python environment, and Hugging Face model downloads are cached outside the checkout on the persistent self-hosted runner. Texture generation is intentionally not installed or exercised; the Apple-Silicon smoke gate covers shape generation only.
