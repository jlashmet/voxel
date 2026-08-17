from pathlib import Path


def replace_once(path_text, old, new, label):
    path = Path(path_text)
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one source match, found {count}")
    path.write_text(text.replace(old, new, 1))


replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderBridge.cs",
    '''        private static event System.Action s_WorldReleasing;\n\n        internal static void RegisterWorldReleaseHandler(System.Action handler) =>\n            s_WorldReleasing += handler;\n''',
    '''        private static event System.Action s_WorldReleasing;\n        private static VoxelRenderPass s_ActivePass;\n\n        /// <summary>\n        /// The render pass that most recently executed through URP. This is diagnostics-only:\n        /// tests may inspect production-visible entries without trying to discover renderer-data\n        /// assets through Resources, but cannot replace or drive scheduler ownership.\n        /// </summary>\n        internal static VoxelRenderPass ActivePass => s_ActivePass;\n\n        internal static void RegisterActivePass(VoxelRenderPass pass) =>\n            s_ActivePass = pass;\n\n        internal static void UnregisterActivePass(VoxelRenderPass pass)\n        {\n            if (ReferenceEquals(s_ActivePass, pass)) s_ActivePass = null;\n        }\n\n        internal static void RegisterWorldReleaseHandler(System.Action handler) =>\n            s_WorldReleasing += handler;\n''',
    "render bridge active pass",
)

replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs",
    '''        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)\n        {\n            VoxelRenderBridge.SurfacePassRecordCount++;\n''',
    '''        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)\n        {\n            // Register on actual execution, not feature construction. Projects can contain several\n            // renderer-data assets; the fidelity gate must inspect the pass URP really invoked.\n            VoxelRenderBridge.RegisterActivePass(this);\n            VoxelRenderBridge.SurfacePassRecordCount++;\n''',
    "render pass execution registration",
)

replace_once(
    "Assets/VoxelEngine/Rendering/Runtime/RenderFeature/VoxelRenderPass.cs",
    '''        public void Dispose()\n        {\n            VoxelRenderBridge.UnregisterWorldReleaseHandler(ReleaseWorldResources);\n            _scheduler?.Dispose();\n''',
    '''        public void Dispose()\n        {\n            VoxelRenderBridge.UnregisterActivePass(this);\n            VoxelRenderBridge.UnregisterWorldReleaseHandler(ReleaseWorldResources);\n            _scheduler?.Dispose();\n''',
    "render pass diagnostic cleanup",
)

replace_once(
    "Assets/Tests/PlayMode/LodVisualFidelityTests.cs",
    '''            VoxelRenderFeature renderFeature = FindActiveVoxelRenderFeature();\n            Assert.NotNull(renderFeature,\n                "Could not inspect the production voxel renderer used by VoxelShowcase.");\n            Assert.NotNull(renderFeature.Pass);\n\n            typeof(VoxelShowcase)\n''',
    '''            // Force one production URP submission before taking the diagnostics handle.\n            // Renderer features are project assets and are not reliably discoverable through\n            // Resources in batchmode; the bridge records the pass URP actually executed.\n            RenderUrpCamera(camera);\n            yield return null;\n            VoxelRenderPass renderPass = VoxelRenderBridge.ActivePass;\n            Assert.NotNull(renderPass,\n                "Could not inspect the production voxel render pass used by VoxelShowcase.");\n\n            typeof(VoxelShowcase)\n''',
    "fidelity active pass acquisition",
)

replace_once(
    "Assets/Tests/PlayMode/LodVisualFidelityTests.cs",
    '''                            observedStepMask = VisibleSourceStepMaskAt(\n                                renderFeature.Pass, centre, VoxelSize);\n''',
    '''                            observedStepMask = VisibleSourceStepMaskAt(\n                                renderPass, centre, VoxelSize);\n''',
    "fidelity visible step inspection",
)

replace_once(
    "Assets/Tests/PlayMode/LodVisualFidelityTests.cs",
    '''        private static VoxelRenderFeature FindActiveVoxelRenderFeature()\n        {\n            VoxelRenderFeature[] features = Resources.FindObjectsOfTypeAll<VoxelRenderFeature>();\n            for (int i = 0; i < features.Length; i++)\n                if (features[i] != null && features[i].Pass != null)\n                    return features[i];\n            return null;\n        }\n\n''',
    '''''',
    "remove unreliable renderer feature discovery",
)
