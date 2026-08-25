using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Complete renderer-independent architectural policy for one settlement style.
    ///
    /// A city/content package owns an implementation; the generic architecture layer only asks the
    /// selected compiler to resolve detailed forms and low-level geometry intent inside the
    /// high-level settlement envelopes. This keeps adding a new city from requiring another style-id
    /// switch in shared code.
    /// </summary>
    public interface IArchitectureStyleCompiler
    {
        string StyleId { get; }

        StructureForm ResolveStructure(
            StructureIntent intent,
            ArchitectureTheme theme,
            uint seed);

        void ValidateStructure(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form);

        /// <summary>
        /// Resolves renderer-neutral geometry/reconstruction controls after structure massing is
        /// known. A style can therefore change corner profiles, openings, trim and roof treatment
        /// without changing settlement planning or a voxel/mesh backend.
        /// </summary>
        StructureGeometryProfile ResolveGeometry(
            StructureIntent intent,
            StructureForm form);

        UrbanFabricForm ResolveUrbanFabric(
            UrbanFabricIntent intent,
            uint seed,
            int runIndex,
            int siteIndex);

        void ValidateUrbanFabric(
            UrbanFabricIntent intent,
            UrbanFabricForm form);
    }

    /// <summary>
    /// Immutable lookup of architecture style compilers. Compose one registry at a world/content
    /// boundary, then pass it through generation. The registry deliberately contains no Unity or
    /// voxel types, so the same style catalogue can drive voxel, mesh, SDF, or editor realizers.
    /// </summary>
    public sealed class ArchitectureStyleRegistry
    {
        private readonly Dictionary<string, IArchitectureStyleCompiler> _styles;

        public ArchitectureStyleRegistry(params IArchitectureStyleCompiler[] compilers)
        {
            if (compilers == null) throw new ArgumentNullException(nameof(compilers));

            _styles = new Dictionary<string, IArchitectureStyleCompiler>(
                compilers.Length,
                StringComparer.Ordinal);
            for (int i = 0; i < compilers.Length; i++)
            {
                IArchitectureStyleCompiler compiler = compilers[i]
                    ?? throw new ArgumentException(
                        "Architecture style compiler cannot be null.",
                        nameof(compilers));
                if (string.IsNullOrEmpty(compiler.StyleId))
                    throw new ArgumentException(
                        "Architecture style compiler must expose a non-empty style id.",
                        nameof(compilers));
                if (_styles.ContainsKey(compiler.StyleId))
                    throw new ArgumentException(
                        "Architecture style is registered more than once: " + compiler.StyleId,
                        nameof(compilers));
                _styles.Add(compiler.StyleId, compiler);
            }
        }

        public int Count => _styles.Count;

        public bool TryResolve(string styleId, out IArchitectureStyleCompiler compiler)
        {
            if (styleId == null)
            {
                compiler = null;
                return false;
            }

            return _styles.TryGetValue(styleId, out compiler);
        }

        public IArchitectureStyleCompiler Require(string styleId)
        {
            if (TryResolve(styleId, out IArchitectureStyleCompiler compiler))
                return compiler;

            throw new ArgumentException(
                "No architecture compiler is registered for style '" + styleId + "'.",
                nameof(styleId));
        }

        /// <summary>
        /// Returns a new registry containing the current styles plus <paramref name="compiler"/>.
        /// Existing registries never mutate, which keeps generation deterministic when worlds build
        /// concurrently.
        /// </summary>
        public ArchitectureStyleRegistry With(IArchitectureStyleCompiler compiler)
        {
            if (compiler == null) throw new ArgumentNullException(nameof(compiler));

            var compilers = new IArchitectureStyleCompiler[_styles.Count + 1];
            int index = 0;
            foreach (IArchitectureStyleCompiler existing in _styles.Values)
                compilers[index++] = existing;
            compilers[index] = compiler;
            return new ArchitectureStyleRegistry(compilers);
        }
    }
}
