using System.Diagnostics;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Re-runs only the deterministic structural planner for one already-authored gallery proof.
        /// This does not rasterise or mutate voxels; it exists so built-player evidence can report
        /// case-local composition cost instead of conflating it with scene startup and rendering.
        /// </summary>
        public double AuditWorldbuildingGalleryStructuralPlanningMilliseconds(int index)
        {
            int proof = NormalizeStructuralProofIndex(index);
            switch (proof)
            {
                case 0:
                {
                    BridgeSite bridge = FindBridgeSite();
                    int3 root = new(bridge.X + 450, bridge.DeckY, bridge.Z);
                    using FeatureCatalogue catalogue = CreateBridgeCatalogue(root);
                    return TimePlan(in catalogue, "bridge");
                }
                case 1:
                {
                    int3 origin = new(-2900,
                        TerrainQuery.HeightAt(-2900, 120, Seed) + 2, 120);
                    using FeatureCatalogue catalogue = CreateCastleCatalogue(origin);
                    return TimePlan(in catalogue, "castle");
                }
                case 2:
                {
                    CliffSite cliff = FindCliffSite();
                    int3 origin = new(cliff.X, cliff.LowY + 4, cliff.Z);
                    using FeatureCatalogue catalogue = CreateCliffCatalogue(origin, cliff.Rise);
                    return TimePlan(in catalogue, "cliff settlement");
                }
                default:
                {
                    int facadeY = TerrainQuery.HeightAt(-2500, 1180, Seed) + 2;
                    int3 first = new(-2500, facadeY, 1180);
                    using FeatureCatalogue civic = CreateFacadeCatalogue(first, ornate: false);
                    using FeatureCatalogue ornate = CreateFacadeCatalogue(first + new int3(300, 0, 0), ornate: true);
                    return TimePlan(in civic, "facade civic") + TimePlan(in ornate, "facade ornate");
                }
            }
        }

        private double TimePlan(in FeatureCatalogue catalogue, string proofName)
        {
            var timer = Stopwatch.StartNew();
            StructuralCompositionReport plan = Plan(in catalogue, 0);
            timer.Stop();
            RequireOk(proofName, in plan);
            return timer.Elapsed.TotalMilliseconds;
        }
    }
}
