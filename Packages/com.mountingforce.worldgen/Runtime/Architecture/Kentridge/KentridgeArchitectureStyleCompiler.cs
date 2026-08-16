using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Kentridge is now a style plug-in rather than a branch inside the generic architecture
    /// compiler. Other cities can provide the same interface and compose their own registry.
    /// </summary>
    internal sealed class KentridgeArchitectureStyleCompiler : IArchitectureStyleCompiler
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

        public UrbanFabricForm ResolveUrbanFabric(
            UrbanFabricIntent intent,
            uint seed,
            int runIndex,
            int siteIndex) =>
            KentridgeUrbanFabricCompiler.Resolve(intent, seed, runIndex, siteIndex);

        public void ValidateUrbanFabric(
            UrbanFabricIntent intent,
            UrbanFabricForm form) =>
            KentridgeUrbanFabricCompiler.Validate(intent, form);
    }

    internal static class BuiltInArchitectureStyles
    {
        internal static readonly ArchitectureStyleRegistry Registry =
            new ArchitectureStyleRegistry(KentridgeArchitectureStyleCompiler.Instance);
    }
}
