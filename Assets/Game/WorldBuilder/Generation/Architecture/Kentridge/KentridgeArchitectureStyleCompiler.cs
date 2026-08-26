using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Kentridge is now a style plug-in rather than a branch inside the generic architecture
    /// compiler. Other cities can provide the same interface and compose their own registry.
    /// </summary>
    internal sealed class KentridgeArchitectureStyleCompiler :
        IArchitectureStyleCompiler,
        IUrbanFabricGeometryProfileResolver
    {
        public static readonly KentridgeArchitectureStyleCompiler Instance =
            new KentridgeArchitectureStyleCompiler();

        private KentridgeArchitectureStyleCompiler()
        {
        }

        public string StyleId => KentridgeDefinition.Id;

        public StructureForm ResolveStructure(
            StructureIntent intent,
            ArchitectureTheme theme,
            uint seed) =>
            KentridgeStructureCompiler.Resolve(intent, theme, seed);

        public void ValidateStructure(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form)
        {
            // Shared ArchitectureCompiler validation owns identity, dimensions, storeys and the
            // high-level structure envelope. Kentridge currently has no extra named-form invariant.
        }

        public StructureGeometryProfile ResolveGeometry(
            StructureIntent intent,
            StructureForm form) =>
            HumanSettlementGeometryProfileResolver.Instance.Resolve(intent, form);

        public UrbanFabricForm ResolveUrbanFabric(
            UrbanFabricIntent intent,
            uint seed,
            int runIndex,
            int siteIndex) =>
            KentridgeUrbanFabricCompiler.Resolve(intent, seed, runIndex, siteIndex);

        public StructureGeometryProfile ResolveUrbanFabricGeometry(
            UrbanFabricIntent intent,
            UrbanFabricForm form) =>
            UrbanFabricGeometryProfiles.HumanSettlement(intent, form);

        public void ValidateUrbanFabric(
            UrbanFabricIntent intent,
            UrbanFabricForm form) =>
            KentridgeUrbanFabricCompiler.Validate(intent, form);
    }

    /// <summary>
    /// Built-in architecture composition. Applications can use this registry directly or compose
    /// their own registry with additional city/style compilers.
    /// </summary>
    public static class BuiltInArchitectureStyles
    {
        public static readonly ArchitectureStyleRegistry Registry =
            new ArchitectureStyleRegistry(KentridgeArchitectureStyleCompiler.Instance);
    }
}
