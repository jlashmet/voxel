using Unity.Mathematics;
using VoxelEngine.Storage.Api;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime.Emitters
{
    /// <summary>Integer-only architectural curved primitives.</summary>
    public static class CurvedPrimitiveEmitter
    {
        public static Primitive RoundedBox(int3 min, int3 size, int radius, byte material,
                                           ushort style, PrimitiveMode mode, int order,
                                           byte coating = Coatings.None,
                                           byte extrusionAxis = 3)
        {
            int3 max = min + math.max(size, 1) - 1;
            int limit = math.cmin(math.max(size, 1)) >> 1;
            return new Primitive
            {
                Shape = PrimitiveShape.RoundedBox, Mode = mode, Material = material,
                SurfaceStyle = style, Coating = coating, Order = order,
                Axis = extrusionAxis,
                A = min, B = max, Radius = math.clamp(radius, 0, limit)
            };
        }

        public static Primitive Ellipsoid(int3 centre, int3 radii, byte material, ushort style,
                                          PrimitiveMode mode, int order, byte coating = Coatings.None)
        {
            radii = math.max(radii, 1);
            return new Primitive
            {
                Shape = PrimitiveShape.Ellipsoid, Mode = mode, Material = material,
                SurfaceStyle = style, Coating = coating, Order = order,
                A = centre - radii, B = centre + radii, C = centre, D = radii
            };
        }

        public static Primitive Frustum(int3 baseCentre, int height, int baseRadius,
                                        int topRadius, byte axis, byte material, ushort style,
                                        PrimitiveMode mode, int order, byte coating = Coatings.None)
        {
            int3 extent = new int3(math.max(baseRadius, topRadius));
            extent[axis] = math.max(1, height) - 1;
            int3 min = baseCentre - new int3(math.max(baseRadius, topRadius));
            min[axis] = baseCentre[axis];
            int3 max = baseCentre + extent;
            return new Primitive
            {
                Shape = PrimitiveShape.Frustum, Mode = mode, Material = material,
                SurfaceStyle = style, Coating = coating, Axis = axis, Order = order,
                Direction = 1,
                A = min, B = max, C = baseCentre,
                Radius = math.max(0, baseRadius), InnerRadius = math.max(0, topRadius)
            };
        }

        public static Primitive Annulus(int3 centre, int outerRadius, int innerRadius, int depth,
                                        byte axis, bool half, byte material, ushort style,
                                        PrimitiveMode mode, int order, byte coating = Coatings.None)
        {
            int3 extent = new int3(math.max(1, outerRadius));
            int3 min = centre - extent;
            int3 max = centre + extent;
            min[axis] = centre[axis] - math.max(1, depth) / 2;
            max[axis] = min[axis] + math.max(1, depth) - 1;
            return new Primitive
            {
                Shape = PrimitiveShape.Annulus, Mode = mode, Material = material,
                SurfaceStyle = style, Coating = coating, Axis = axis,
                Direction = 1,
                Profile = half ? PrismProfile.Arch : PrismProfile.Gable, Order = order,
                A = min, B = max, C = centre,
                Radius = math.max(1, outerRadius),
                InnerRadius = math.clamp(innerRadius, 0, math.max(0, outerRadius - 1))
            };
        }

        /// <summary>
        /// Annular wedge between two integer direction vectors in the radial plane. Directions
        /// must be counter-clockwise and span no more than 180 degrees.
        /// </summary>
        public static Primitive ArcWedge(int3 centre, int outerRadius, int innerRadius, int depth,
                                         byte axis, int2 startDirection, int2 endDirection,
                                         byte material, ushort style, PrimitiveMode mode, int order,
                                         byte coating = Coatings.None)
        {
            Primitive p = Annulus(centre, outerRadius, innerRadius, depth, axis, false,
                                  material, style, mode, order, coating);
            p.Shape = PrimitiveShape.ArcWedge;
            p.StartDirection = startDirection;
            p.EndDirection = endDirection;
            return p;
        }

        public static bool Contains(in Primitive p, int3 voxel)
        {
            switch (p.Shape)
            {
                case PrimitiveShape.RoundedBox: return RoundedBoxContains(in p, voxel);
                case PrimitiveShape.Ellipsoid: return EllipsoidContains(in p, voxel);
                case PrimitiveShape.Frustum: return FrustumContains(in p, voxel);
                case PrimitiveShape.Annulus: return AnnulusContains(in p, voxel);
                case PrimitiveShape.ArcWedge: return ArcWedgeContains(in p, voxel);
                default: return false;
            }
        }

        /// <summary>
        /// Returns an authored signed distance for curved boundaries in Q4 voxels, positive
        /// inside the solid annular shell. Arc-wedge angular planes are
        /// deliberately excluded: adjacent voussoirs form one structural annulus, so those planes
        /// are material/joint boundaries rather than exterior solid boundaries.
        /// </summary>
        public static bool TryBoundaryDistanceQ4(in Primitive p, int3 voxel, out int distanceQ4)
        {
            switch (p.Shape)
            {
                case PrimitiveShape.RoundedBox:
                    distanceQ4 = RoundedBoxDistanceQ4(in p, voxel);
                    return true;
                case PrimitiveShape.Ellipsoid:
                    distanceQ4 = EllipsoidDistanceQ4(in p, voxel);
                    return true;
                case PrimitiveShape.Frustum:
                    distanceQ4 = FrustumDistanceQ4(in p, voxel);
                    return true;
                case PrimitiveShape.Annulus:
                case PrimitiveShape.ArcWedge:
                    distanceQ4 = AnnulusDistanceQ4(in p, voxel);
                    return true;
                default:
                    distanceQ4 = 0;
                    return false;
            }
        }

        private static int AnnulusDistanceQ4(in Primitive p, int3 voxel)
        {
            int axisA = (p.Axis + 1) % 3;
            int axisB = (p.Axis + 2) % 3;
            long da = voxel[axisA] - p.C[axisA];
            long db = voxel[axisB] - p.C[axisB];
            int radialQ4 = IntegerSqrt((da * da + db * db) << 8);

            // The radial zero must coincide with the membership test, which includes a centre
            // when r <= Radius. Biasing these by half a cell -- correct for the flat depth and
            // half-plane terms below, where a box face does sit half a cell beyond the last
            // included centre -- put the analytic surface at r = Radius + 0.5 instead. Centres in
            // that half-voxel band then read as inside while occupancy called them empty, so the
            // rasteriser's sign check discarded their samples and the edge fell back to the flat
            // Planar constant. Which centres land in the band depends on angle, so the crossing
            // moved with angle: a staircase on any curved surface.
            int outer = (p.Radius << 4) - radialQ4;
            int inner = p.InnerRadius > 0
                ? radialQ4 - (p.InnerRadius << 4)
                : int.MaxValue;
            int depth = math.min(voxel[p.Axis] - p.A[p.Axis],
                                 p.B[p.Axis] - voxel[p.Axis]) * 16 + 8;
            int distanceQ4 = math.min(outer, math.min(inner, depth));

            if (p.Shape == PrimitiveShape.Annulus && p.Profile == PrismProfile.Arch)
            {
                int upAxis = p.Axis == 1 ? axisB : 1;
                int halfPlane = (voxel[upAxis] - p.C[upAxis]) * 16 + 8;
                distanceQ4 = math.min(distanceQ4, halfPlane);
            }

            distanceQ4 = math.clamp(distanceQ4, -127, 127);
            return distanceQ4;
        }

        private static int RoundedBoxDistanceQ4(in Primitive p, int3 voxel)
        {
            // Exact rounded-box SDF evaluated at cell centres. Bounds describe included centre
            // coordinates, so the analytic envelope extends half a cell beyond them.
            int3 centreTwice = p.A + p.B;
            int3 innerHalfQ4 = math.max((p.B - p.A - p.Radius * 2) * 8, 0);
            int3 q = math.abs(voxel * 2 - centreTwice) * 8 - innerHalfQ4;
            int3 outside = math.max(q, 0);
            if (p.Axis <= 2) outside[p.Axis] = 0;
            int outsideQ4 = IntegerSqrt((long)outside.x * outside.x
                                      + (long)outside.y * outside.y
                                      + (long)outside.z * outside.z);
            int maximumQ4 = p.Axis <= 2
                ? math.max(q[(p.Axis + 1) % 3], q[(p.Axis + 2) % 3])
                : math.cmax(q);
            int insideQ4 = math.min(maximumQ4, 0);
            int signedOutsideQ4 = outsideQ4 + insideQ4 - (p.Radius * 16 + 8);
            if (p.Axis <= 2)
            {
                int capQ4 = math.min(voxel[p.Axis] - p.A[p.Axis],
                                     p.B[p.Axis] - voxel[p.Axis]) * 16 + 8;
                return math.clamp(math.min(-signedOutsideQ4, capQ4), -127, 127);
            }
            return math.clamp(-signedOutsideQ4, -127, 127);
        }

        private static int EllipsoidDistanceQ4(in Primitive p, int3 voxel)
        {
            int3 d = voxel - p.C;
            int3 r = math.max(p.D, 1);
            const long scale = 1L << 20;
            long normalizedSquared = (long)d.x * d.x * scale / ((long)r.x * r.x)
                                   + (long)d.y * d.y * scale / ((long)r.y * r.y)
                                   + (long)d.z * d.z * scale / ((long)r.z * r.z);
            int normalizedQ10 = IntegerSqrt(normalizedSquared);
            int approximate = (1024 - normalizedQ10) * math.cmin(r) / 64 + 8;
            return math.clamp(approximate, -127, 127);
        }

        private static int FrustumDistanceQ4(in Primitive p, int3 voxel)
        {
            int length = math.max(1, p.B[p.Axis] - p.A[p.Axis]);
            int along = math.clamp(voxel[p.Axis] - p.C[p.Axis], 0, length);
            int radius = p.Radius + (p.InnerRadius - p.Radius) * along / length;
            int axisA = (p.Axis + 1) % 3;
            int axisB = (p.Axis + 2) % 3;
            long da = voxel[axisA] - p.C[axisA];
            long db = voxel[axisB] - p.C[axisB];
            int radialQ4 = IntegerSqrt((da * da + db * db) << 8);
            int radial = (radius << 4) + 8 - radialQ4;
            int cap = math.min(voxel[p.Axis] - p.A[p.Axis],
                               p.B[p.Axis] - voxel[p.Axis]) * 16 + 8;
            return math.clamp(math.min(radial, cap), -127, 127);
        }

        private static int IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            ulong n = (ulong)value;
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > n) bit >>= 2;
            while (bit != 0)
            {
                if (n >= result + bit)
                {
                    n -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }
            return (int)result;
        }

        private static bool RoundedBoxContains(in Primitive p, int3 v)
        {
            if (math.any(v < p.A) || math.any(v > p.B)) return false;
            int3 innerMin = p.A + p.Radius;
            int3 innerMax = p.B - p.Radius;
            int3 nearest = math.clamp(v, innerMin, innerMax);
            int3 d = v - nearest;
            if (p.Axis <= 2) d[p.Axis] = 0;
            return (long)d.x * d.x + (long)d.y * d.y + (long)d.z * d.z
                   <= (long)p.Radius * p.Radius;
        }

        private static bool EllipsoidContains(in Primitive p, int3 v)
        {
            int3 d = v - p.C;
            int3 r = math.max(p.D, 1);
            const long scale = 1L << 20;
            long sum = (long)d.x * d.x * scale / ((long)r.x * r.x)
                     + (long)d.y * d.y * scale / ((long)r.y * r.y)
                     + (long)d.z * d.z * scale / ((long)r.z * r.z);
            return sum <= scale;
        }

        private static bool FrustumContains(in Primitive p, int3 v)
        {
            if (math.any(v < p.A) || math.any(v > p.B)) return false;
            int length = math.max(1, p.B[p.Axis] - p.A[p.Axis]);
            int along = p.Direction < 0
                ? p.C[p.Axis] - v[p.Axis]
                : v[p.Axis] - p.C[p.Axis];
            int radius = p.Radius + (p.InnerRadius - p.Radius) * along / length;
            int axisA = (p.Axis + 1) % 3;
            int axisB = (p.Axis + 2) % 3;
            long da = v[axisA] - p.C[axisA];
            long db = v[axisB] - p.C[axisB];
            return da * da + db * db <= (long)radius * radius;
        }

        private static bool AnnulusContains(in Primitive p, int3 v)
        {
            int axisA = (p.Axis + 1) % 3;
            int axisB = (p.Axis + 2) % 3;
            if (v[p.Axis] < p.A[p.Axis] || v[p.Axis] > p.B[p.Axis]) return false;
            long da = v[axisA] - p.C[axisA];
            long db = v[axisB] - p.C[axisB];
            long distance = da * da + db * db;
            if (distance > (long)p.Radius * p.Radius
                || distance < (long)p.InnerRadius * p.InnerRadius) return false;
            if (p.Profile != PrismProfile.Arch) return true;
            // Architectural half-annuli keep world/local up when their depth axis is rotated
            // between X and Z. A vertical extrusion retains the radial-basis interpretation.
            return p.Axis == 1 ? v[axisB] >= p.C[axisB] : v.y >= p.C.y;
        }

        private static bool ArcWedgeContains(in Primitive p, int3 v)
        {
            int axisA = (p.Axis + 1) % 3;
            int axisB = (p.Axis + 2) % 3;
            if (v[p.Axis] < p.A[p.Axis] || v[p.Axis] > p.B[p.Axis]) return false;
            long x = v[axisA] - p.C[axisA];
            long y = v[axisB] - p.C[axisB];
            long distance = x * x + y * y;
            if (distance > (long)p.Radius * p.Radius
                || distance < (long)p.InnerRadius * p.InnerRadius) return false;
            long startCross = (long)p.StartDirection.x * y - (long)p.StartDirection.y * x;
            long endCross = x * p.EndDirection.y - y * p.EndDirection.x;
            if (startCross < 0 || endCross < 0) return false;

            return true;
        }
    }
}
