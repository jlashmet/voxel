using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Physical placement facts for one already-selected secret. WorldBuilder selection remains purely
    /// semantic; Composition joins the stable candidate/entrance ids back to backend realization facts.
    /// </summary>
    public sealed class ResolvedSecretWorldGeometry
    {
        public ResolvedSecretPlan Secret { get; }
        public RealizedWorldBounds HiddenSpaceBounds { get; }
        public RealizedWorldBounds EntranceBounds { get; }
        public RealizedWorldPoint ContainerFloorPoint { get; }

        public ResolvedSecretWorldGeometry(
            ResolvedSecretPlan secret,
            RealizedWorldBounds hiddenSpaceBounds,
            RealizedWorldBounds entranceBounds,
            RealizedWorldPoint containerFloorPoint)
        {
            Secret = secret ?? throw new ArgumentNullException(nameof(secret));
            if (hiddenSpaceBounds.UnitsPerDecimetre != entranceBounds.UnitsPerDecimetre
                || hiddenSpaceBounds.UnitsPerDecimetre != containerFloorPoint.UnitsPerDecimetre)
                throw new ArgumentException(
                    "Resolved secret geometry must use one consistent integer world scale.");

            HiddenSpaceBounds = hiddenSpaceBounds;
            EntranceBounds = entranceBounds;
            ContainerFloorPoint = containerFloorPoint;
        }
    }

    /// <summary>
    /// Joins WorldBuilder's selected semantic secret to exact backend geometry. Missing realization is
    /// a hard integration error: gameplay must never invent a wall or chest position after generation.
    /// </summary>
    public static class SecretWorldGeometryResolver
    {
        public static ResolvedSecretWorldGeometry Resolve(
            ResolvedSecretPlan secret,
            IHiddenSpaceRealizationFacts facts)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            if (facts == null) throw new ArgumentNullException(nameof(facts));

            RealizedWorldBounds hiddenSpace;
            if (!facts.TryGetCandidateBounds(secret.Candidate.Id, out hiddenSpace))
                throw new InvalidOperationException(
                    "Selected secret candidate '" + secret.Candidate +
                    "' has no physical hidden-space realization.");

            RealizedWorldBounds entrance;
            if (!facts.TryGetEntranceBounds(secret.EntranceId, out entrance))
                throw new InvalidOperationException(
                    "Selected secret entrance '" + secret.EntranceId +
                    "' has no physical world realization.");

            if (hiddenSpace.UnitsPerDecimetre != entrance.UnitsPerDecimetre)
                throw new InvalidOperationException(
                    "Selected secret candidate and entrance were realized at different world scales.");

            RealizedWorldPoint containerPoint = hiddenSpace.CentreFloor();
            return new ResolvedSecretWorldGeometry(
                secret,
                hiddenSpace,
                entrance,
                containerPoint);
        }
    }
}
