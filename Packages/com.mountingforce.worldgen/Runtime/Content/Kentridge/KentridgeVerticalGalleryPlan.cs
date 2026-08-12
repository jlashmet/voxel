using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public readonly struct KentridgeVerticalGalleryRoute
    {
        public readonly string Id;
        public readonly KentridgeUrbanBand Band;
        public readonly DistrictKind District;
        public readonly Int2 ElevationSampleDm;
        public readonly int MinXDm;
        public readonly int MaxXDm;
        public readonly int FrontZDm;
        public readonly int GapCentreXDm;
        public readonly int GapWidthDm;
        public readonly KentridgeUrbanReturnSide ReturnSide;
        public readonly int LowerDoorBelowShelfDm;
        public readonly int GalleryFloorBelowShelfDm;

        public KentridgeVerticalGalleryRoute(
            string id, KentridgeUrbanBand band, DistrictKind district,
            Int2 elevationSampleDm, int minXDm, int maxXDm, int frontZDm,
            int gapCentreXDm, int gapWidthDm, KentridgeUrbanReturnSide returnSide,
            int lowerDoorBelowShelfDm, int galleryFloorBelowShelfDm)
        {
            Id = id;
            Band = band;
            District = district;
            ElevationSampleDm = elevationSampleDm;
            MinXDm = minXDm;
            MaxXDm = maxXDm;
            FrontZDm = frontZDm;
            GapCentreXDm = gapCentreXDm;
            GapWidthDm = gapWidthDm;
            ReturnSide = returnSide;
            LowerDoorBelowShelfDm = lowerDoorBelowShelfDm;
            GalleryFloorBelowShelfDm = galleryFloorBelowShelfDm;
        }

        public int LengthDm => MaxXDm - MinXDm;
        public int RiseDm => LowerDoorBelowShelfDm - GalleryFloorBelowShelfDm;
    }

    public sealed class KentridgeVerticalGalleryPlan
    {
        public IReadOnlyList<KentridgeVerticalGalleryRoute> Routes => _routes;
        private readonly List<KentridgeVerticalGalleryRoute> _routes;
        public KentridgeVerticalGalleryPlan(List<KentridgeVerticalGalleryRoute> routes) { _routes = routes; }
    }

    /// <summary>
    /// Public second-level galleries serve the commercial, residential-upper, and civic terrace
    /// faces. The NobleRidge undercroft remains inhabited but intentionally private: Radcliffe's
    /// outer estate edge does not participate in the public lower-gallery network.
    /// </summary>
    public static class KentridgeVerticalGalleryPlanner
    {
        public const int GalleryDepthDm = 12;
        public const int CornerStairRunDm = 30;

        public static KentridgeVerticalGalleryPlan Build(uint seed)
        {
            KentridgeVerticalFrontagePlan frontage = KentridgeVerticalFrontagePlanner.Build(seed);
            KentridgeUrbanAccessPlan access = KentridgeUrbanAccessPlanner.Build(seed);
            var routes = new List<KentridgeVerticalGalleryRoute>(5);

            for (int i = 0; i < frontage.Zones.Count; i++)
            {
                KentridgeVerticalFrontageZone zone = frontage.Zones[i];
                if (zone.Band == KentridgeUrbanBand.NobleRidge)
                    continue;

                string blockId = StripSuffix(zone.Id, "-vertical-frontage");
                KentridgeUrbanAccessRoute blockAccess = FindAccess(access, blockId + "-access");
                int galleryFloorBelowShelfDm =
                    zone.HeightDm
                    + KentridgeVerticalFrontagePlanner.TopBelowShelfDm
                    - KentridgeVerticalFrontagePlanner.FloorThicknessDm;

                routes.Add(new KentridgeVerticalGalleryRoute(
                    blockId + "-downhill-gallery",
                    zone.Band,
                    zone.District,
                    zone.ElevationSampleDm,
                    zone.MinXDm,
                    zone.MaxXDm,
                    zone.StartDm.Y,
                    zone.GapCentreDm,
                    zone.GapWidthDm,
                    blockAccess.ReturnSide,
                    blockAccess.DoorLevelBelowShelfDm,
                    galleryFloorBelowShelfDm));
            }

            Validate(routes);
            return new KentridgeVerticalGalleryPlan(routes);
        }

        private static KentridgeUrbanAccessRoute FindAccess(KentridgeUrbanAccessPlan plan, string id)
        {
            for (int i = 0; i < plan.Routes.Count; i++)
                if (plan.Routes[i].Id == id) return plan.Routes[i];
            throw new InvalidOperationException("Kentridge vertical gallery is missing block access: " + id);
        }

        private static string StripSuffix(string value, string suffix)
        {
            if (!value.EndsWith(suffix, StringComparison.Ordinal))
                throw new InvalidOperationException("Kentridge vertical gallery could not identify block from frontage: " + value);
            return value.Substring(0, value.Length - suffix.Length);
        }

        private static void Validate(List<KentridgeVerticalGalleryRoute> routes)
        {
            if (routes.Count != 5)
                throw new InvalidOperationException(
                    "Kentridge public downhill galleries must cover five non-noble dense frontages.");

            for (int i = 0; i < routes.Count; i++)
            {
                KentridgeVerticalGalleryRoute route = routes[i];
                if (route.Band == KentridgeUrbanBand.NobleRidge)
                    throw new InvalidOperationException("Noble ridge must remain outside the public gallery network.");
                if (route.LengthDm <= route.GapWidthDm || route.GapWidthDm <= 0)
                    throw new InvalidOperationException("Kentridge downhill gallery has invalid frontage span: " + route.Id);
                if (route.RiseDm <= 0 || route.RiseDm > 16)
                    throw new InvalidOperationException("Kentridge downhill gallery corner stair has implausible rise: " + route.Id);
                if (route.GapCentreXDm <= route.MinXDm || route.GapCentreXDm >= route.MaxXDm)
                    throw new InvalidOperationException("Kentridge downhill gallery gateway escaped frontage: " + route.Id);
            }
        }
    }
}
