using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Exact local geometry for one hidden-space realization. The Voxel backend consumes these same
    /// bounds to emit physical voxels; higher-level adapters consume Realization for topology facts.
    /// </summary>
    public sealed class KentridgeHiddenSpaceGeometry
    {
        public SiteHiddenSpaceRealization Realization { get; }
        public bool OnRightSide { get; }

        public KentridgeHiddenSpaceGeometry(
            SiteHiddenSpaceRealization realization,
            bool onRightSide)
        {
            Realization = realization ?? throw new ArgumentNullException(nameof(realization));
            OnRightSide = onRightSide;
        }
    }

    /// <summary>
    /// Kentridge-specific architectural realization for optional hidden side cavities. It deliberately
    /// does not mutate StructureIntent or StructureForm: settlement massing stays unchanged and this
    /// optional pass consumes only unused space inside the already-reserved site envelope.
    ///
    /// The cavity shares the host's ground-floor side wall. The voxel pass cuts a doorway-sized opening
    /// through that wall and immediately refills it with the same host-wall material, creating a visually
    /// matching, non-corner, removable false-wall span. Removing that span reveals the otherwise sealed
    /// cavity without deleting a load-bearing corner or timber frame.
    /// </summary>
    public static class KentridgeHiddenSpacePlanner
    {
        public const int RoomOuterWidthDm = 24;
        public const int RoomOuterDepthDm = 20;
        public const int EnvelopeEdgeMarginDm = 4;
        public const int FalseWallWidthDm = 8;
        public const int FalseWallRearInsetDm = 6;

        private const int MainShellFrontInsetDm = 10;

        public static IReadOnlyList<KentridgeHiddenSpaceGeometry> Resolve(
            BuildingPlot plot,
            uint seed,
            SiteHiddenSpaceRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.RoleId != plot.RoleId)
                throw new ArgumentException(
                    "Hidden-space request role does not match the supplied Kentridge plot.",
                    nameof(request));
            if (request.Entrance != HiddenSpaceEntranceKind.BreakableMatchingWall)
                return Array.Empty<KentridgeHiddenSpaceGeometry>();
            if (request.TargetCount == 0)
                return Array.Empty<KentridgeHiddenSpaceGeometry>();

            StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, KentridgeDefinition.Theme, seed);
            if (!form.IsGenerated)
                return Array.Empty<KentridgeHiddenSpaceGeometry>();

            ArchitectureTheme theme = KentridgeDefinition.Theme;
            int wall = theme.WallThicknessDm;
            int extension = RoomOuterWidthDm - wall;
            int x0 = (intent.EnvelopeDm.X - form.WidthDm) / 2;
            int z0 = MainShellFrontInsetDm;
            int roomZ = z0 + form.DepthDm - RoomOuterDepthDm;

            // The false-wall span is deliberately near the rear but ends before the 2*beam rear timber
            // frame. The normal side window sits at half-depth and therefore cannot expose this cavity.
            int falseWallZ = roomZ + FalseWallRearInsetDm;
            if (falseWallZ + FalseWallWidthDm > z0 + form.DepthDm - 2 * theme.BeamWidthDm)
                return Array.Empty<KentridgeHiddenSpaceGeometry>();

            int leftClearance = x0;
            int rightClearance = intent.EnvelopeDm.X - (x0 + form.WidthDm);
            bool leftAllowed = leftClearance >= extension + EnvelopeEdgeMarginDm;
            bool rightAllowed = rightClearance >= extension + EnvelopeEdgeMarginDm;

            if (form.Footprint == FootprintForm.SideWing)
            {
                // Never overlap the generated service/workshop wing. Use only the opposite side.
                if (form.WingOnRight) rightAllowed = false;
                else leftAllowed = false;
            }

            if (!leftAllowed && !rightAllowed)
                return Array.Empty<KentridgeHiddenSpaceGeometry>();

            var result = new List<KentridgeHiddenSpaceGeometry>(Math.Min(2, request.TargetCount));
            bool rightFirst = StableHash(seed, request.RequestId, plot.RoleId) % 2u == 0u;
            if (rightFirst)
            {
                TryAdd(true, rightAllowed, rightClearance, request, form, theme, x0, roomZ, falseWallZ, result);
                TryAdd(false, leftAllowed, leftClearance, request, form, theme, x0, roomZ, falseWallZ, result);
            }
            else
            {
                TryAdd(false, leftAllowed, leftClearance, request, form, theme, x0, roomZ, falseWallZ, result);
                TryAdd(true, rightAllowed, rightClearance, request, form, theme, x0, roomZ, falseWallZ, result);
            }

            return result;
        }

        private static void TryAdd(
            bool onRight,
            bool allowed,
            int clearanceDm,
            SiteHiddenSpaceRequest request,
            StructureForm form,
            ArchitectureTheme theme,
            int x0,
            int roomZ,
            int falseWallZ,
            List<KentridgeHiddenSpaceGeometry> result)
        {
            if (!allowed || result.Count >= request.TargetCount) return;

            int wall = theme.WallThicknessDm;
            int roomX = onRight
                ? x0 + form.WidthDm - wall
                : x0 - (RoomOuterWidthDm - wall);
            int falseWallX = onRight
                ? x0 + form.WidthDm - wall
                : x0;

            string side = onRight ? "right" : "left";
            string candidateId = request.RequestId + "/" + side;
            var roomBounds = new HiddenSpaceBoundsDm(
                roomX,
                theme.FoundationHeightDm,
                roomZ,
                RoomOuterWidthDm,
                theme.FloorHeightDm,
                RoomOuterDepthDm);
            var entranceBounds = new HiddenSpaceBoundsDm(
                falseWallX,
                theme.FoundationHeightDm,
                falseWallZ,
                wall,
                theme.DoorHeightDm,
                FalseWallWidthDm);

            int spare = clearanceDm - (RoomOuterWidthDm - wall + EnvelopeEdgeMarginDm);
            int quality = Math.Min(10000, 8200 + Math.Max(0, spare) * 25);
            var entrance = new HiddenSpaceEntranceRealization(
                candidateId + "/false-wall",
                HiddenSpaceEntranceKind.BreakableMatchingWall,
                entranceBounds,
                separatesHiddenSpaceBeforeOpen: true,
                grantsNormalTraversalAfterOpen: true,
                isStructurallyCritical: false,
                supportsRemoval: true,
                matchesHostSurface: true);
            var realization = new SiteHiddenSpaceRealization(
                request.RequestId,
                request.RoleId,
                candidateId,
                HiddenSpaceVolumeKind.SideCavity,
                roomBounds,
                hiddenFromNormalTraversal: true,
                qualityBasisPoints: quality,
                entrance: entrance);
            result.Add(new KentridgeHiddenSpaceGeometry(realization, onRight));
        }

        private static uint StableHash(uint seed, string value, int roleId)
        {
            unchecked
            {
                uint hash = 2166136261u ^ seed ^ ((uint)(roleId + 1) * 0x9E3779B9u);
                string text = value ?? string.Empty;
                for (var i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }
    }
}
