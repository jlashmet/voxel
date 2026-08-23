using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.Emitters;
using VoxelEngine.Storage.Api;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    public enum ArchRuinDamage : byte
    {
        Intact = 0,
        BrokenCrown = 1,
        BrokenLeftHaunch = 2,
        BrokenRightHaunch = 3,
        CollapsedShoulder = 4,
    }

    [System.Flags]
    public enum ArchValidationError : ushort
    {
        None = 0,
        InvalidClearSpan = 1 << 0,
        InvalidPierHeight = 1 << 1,
        InvalidRingThickness = 1 << 2,
        InvalidDepth = 1 << 3,
        InvalidVoussoirCount = 1 << 4,
        InvalidJointRecessDepth = 1 << 11,
        UnknownStoneMaterial = 1 << 5,
        UnknownPierStyle = 1 << 6,
        UnknownRingStyle = 1 << 7,
        DisallowedCoating = 1 << 8,
        PrimitiveBudgetExceeded = 1 << 9,
        UnknownCoating = 1 << 10,
    }

    public enum FeatureSocketType : byte
    {
        Foundation = 0,
        SpanEndpoint = 1,
        Crown = 2,
        Wall = 3,
    }

    public struct FeatureSocket
    {
        public FixedString32Bytes Name;
        public FeatureSocketType Type;
        public int3 LocalPosition;
        public Facing Facing;
        public int Size;

        public bool CanConnect(in FeatureSocket other) =>
            Type == other.Type && Size == other.Size && Opposite(Facing) == other.Facing;

        private static Facing Opposite(Facing facing) => facing switch
        {
            Facing.North => Facing.South,
            Facing.South => Facing.North,
            Facing.East => Facing.West,
            Facing.West => Facing.East,
            Facing.Up => Facing.Down,
            _ => Facing.Up,
        };
    }

    /// <summary>Compiled, integer-only architectural arch definition.</summary>
    public struct ArchFeatureDefinition
    {
        public int ClearSpan;
        public int PierHeight;
        public int RingThickness;
        public int Depth;
        public int VoussoirCount;
        public int JointRecessDepth;
        /// <summary>Half-width of retained face joints in Q4 voxels; zero uses the built-in cut-stone default.</summary>
        public byte ProfileJointHalfWidthQ4;
        /// <summary>Front arris bevel in Q4 voxels; zero uses the built-in cut-stone default.</summary>
        public byte ProfileBevelQ4;
        /// <summary>How far the retained face projects ahead of the structural ring, in Q4 voxels.</summary>
        public byte ProfileProjectionQ4;
        /// <summary>Depth of the retained face layer in Q4 voxels; zero uses one voxel.</summary>
        public byte ProfileDepthQ4;
        public byte StoneMaterial;
        public ushort PierStyle;
        public ushort RingStyle;
        public byte Coating;

        public int OuterRadius => ClearSpan / 2 + RingThickness;
        // Integer circles include both radial endpoints, so the footprint is 2r + 1 cells wide.
        public int Width => ClearSpan + RingThickness * 2 + 1;
        public int Height => PierHeight + OuterRadius + 1;

        public bool IsValid => ClearSpan >= 4 && (ClearSpan & 1) == 0
            && PierHeight > 0 && RingThickness > 0 && Depth > 0
            && JointRecessDepth >= 0 && JointRecessDepth < Depth
            && VoussoirCount >= 1 && VoussoirCount <= 32 && StoneMaterial != 0;

        public ArchValidationError Validate<TMaterial, TSurface, TCoating>(
            in TMaterial palette, in TSurface surfaces, in TCoating coatings)
            where TMaterial : struct, IMaterialAuthoringCatalogue
            where TSurface : struct, ISurfaceStyleAuthoringCatalogue
            where TCoating : struct, ICoatingAuthoringCatalogue
        {
            ArchValidationError errors = ArchValidationError.None;
            if (ClearSpan < 4 || (ClearSpan & 1) != 0)
                errors |= ArchValidationError.InvalidClearSpan;
            if (PierHeight <= 0) errors |= ArchValidationError.InvalidPierHeight;
            if (RingThickness <= 0) errors |= ArchValidationError.InvalidRingThickness;
            if (Depth <= 0) errors |= ArchValidationError.InvalidDepth;
            if (JointRecessDepth < 0 || JointRecessDepth >= Depth)
                errors |= ArchValidationError.InvalidJointRecessDepth;
            if (VoussoirCount < 1 || VoussoirCount > 32)
                errors |= ArchValidationError.InvalidVoussoirCount;
            if (PrimitiveCount > FeatureBudget.MaxPrimitivesPerInstance)
                errors |= ArchValidationError.PrimitiveBudgetExceeded;
            if (!palette.IsRegistered(StoneMaterial))
                errors |= ArchValidationError.UnknownStoneMaterial;
            if (!surfaces.IsRegistered(PierStyle))
                errors |= ArchValidationError.UnknownPierStyle;
            if (!surfaces.IsRegistered(RingStyle))
                errors |= ArchValidationError.UnknownRingStyle;
            if (!coatings.IsRegistered(Coating))
                errors |= ArchValidationError.UnknownCoating;
            if (Coating != Coatings.None && palette.IsRegistered(StoneMaterial)
                && (!palette.AllowsCoating(StoneMaterial, Coating)
                    || !coatings.Allows(Coating, StoneMaterial)))
                errors |= ArchValidationError.DisallowedCoating;
            return errors;
        }

        public ArchValidationError Validate(
            IMaterialAuthoringCatalogue palette,
            ISurfaceStyleAuthoringCatalogue surfaces,
            ICoatingAuthoringCatalogue coatings)
        {
            if (palette == null) throw new System.ArgumentNullException(nameof(palette));
            if (surfaces == null) throw new System.ArgumentNullException(nameof(surfaces));
            if (coatings == null) throw new System.ArgumentNullException(nameof(coatings));

            ArchValidationError errors = ArchValidationError.None;
            if (ClearSpan < 4 || (ClearSpan & 1) != 0) errors |= ArchValidationError.InvalidClearSpan;
            if (PierHeight <= 0) errors |= ArchValidationError.InvalidPierHeight;
            if (RingThickness <= 0) errors |= ArchValidationError.InvalidRingThickness;
            if (Depth <= 0) errors |= ArchValidationError.InvalidDepth;
            if (JointRecessDepth < 0 || JointRecessDepth >= Depth) errors |= ArchValidationError.InvalidJointRecessDepth;
            if (VoussoirCount < 1 || VoussoirCount > 32) errors |= ArchValidationError.InvalidVoussoirCount;
            if (PrimitiveCount > FeatureBudget.MaxPrimitivesPerInstance) errors |= ArchValidationError.PrimitiveBudgetExceeded;
            if (!palette.IsRegistered(StoneMaterial)) errors |= ArchValidationError.UnknownStoneMaterial;
            if (!surfaces.IsRegistered(PierStyle)) errors |= ArchValidationError.UnknownPierStyle;
            if (!surfaces.IsRegistered(RingStyle)) errors |= ArchValidationError.UnknownRingStyle;
            if (!coatings.IsRegistered(Coating)) errors |= ArchValidationError.UnknownCoating;
            if (Coating != Coatings.None && palette.IsRegistered(StoneMaterial)
                && (!palette.AllowsCoating(StoneMaterial, Coating)
                    || !coatings.Allows(Coating, StoneMaterial)))
                errors |= ArchValidationError.DisallowedCoating;
            return errors;
        }

        public FeatureDefinition Metadata => new()
        {
            Name = "architectural-arch",
            Kind = FeatureKind.Structure,
            BasePlane = BasePlaneRule.LowestGround,
            Footprint = new int3(Width, Height, Depth),
            MaxSlope = 1,
            MaxPrimitives = PrimitiveCount
        };

        private int PrimitiveCount => VoussoirCount + 2
            + (JointRecessDepth > 0 ? (VoussoirCount - 1) * JointRecessDepth : 0);

        public void GetSockets(NativeList<FeatureSocket> sockets)
        {
            sockets.Add(new FeatureSocket
            {
                Name = "left-foundation", Type = FeatureSocketType.Foundation,
                LocalPosition = new int3(RingThickness / 2, 0, Depth / 2),
                Facing = Facing.Down, Size = RingThickness
            });
            sockets.Add(new FeatureSocket
            {
                Name = "right-foundation", Type = FeatureSocketType.Foundation,
                LocalPosition = new int3(Width - 1 - RingThickness / 2, 0, Depth / 2),
                Facing = Facing.Down, Size = RingThickness
            });
            sockets.Add(new FeatureSocket
            {
                Name = "crown", Type = FeatureSocketType.Crown,
                LocalPosition = new int3(Width / 2, Height - 1, Depth / 2),
                Facing = Facing.Up, Size = RingThickness
            });
            sockets.Add(new FeatureSocket
            {
                Name = "left-span", Type = FeatureSocketType.SpanEndpoint,
                LocalPosition = new int3(RingThickness, PierHeight, Depth / 2),
                Facing = Facing.East, Size = ClearSpan
            });
            sockets.Add(new FeatureSocket
            {
                Name = "right-span", Type = FeatureSocketType.SpanEndpoint,
                LocalPosition = new int3(Width - RingThickness - 1, PierHeight, Depth / 2),
                Facing = Facing.West, Size = ClearSpan
            });
        }

        /// <summary>
        /// Emits piers followed by voussoir wedges. Membership is world-coordinate based, so
        /// clipped region evaluation is identical to whole-feature evaluation. Joint metadata
        /// controls reconstruction and presentation only; it never removes structural voxels.
        /// A genuinely recessed joint must be authored as an explicit carve operation.
        /// </summary>
        public bool Emit(int3 origin, NativeList<Primitive> output) =>
            Emit(origin, output, null);

        public bool Emit(int3 origin, NativeList<Primitive> output,
                         IProfileBlockWriter profileBlocks)
        {
            if (!IsValid) return false;
            int order = output.Length;
            Primitive leftPier = CurvedPrimitiveEmitter.RoundedBox(
                origin, new int3(RingThickness, PierHeight + 1, Depth),
                math.max(1, RingThickness / 4), StoneMaterial, PierStyle,
                PrimitiveMode.Fill, order++, Coating);
            leftPier.SurfaceFlags = StyleFlags(PierStyle);
            output.Add(leftPier);
            Primitive rightPier = CurvedPrimitiveEmitter.RoundedBox(
                origin + new int3(Width - RingThickness, 0, 0),
                new int3(RingThickness, PierHeight + 1, Depth),
                math.max(1, RingThickness / 4), StoneMaterial, PierStyle,
                PrimitiveMode.Fill, order++, Coating);
            rightPier.SurfaceFlags = StyleFlags(PierStyle);
            output.Add(rightPier);

            int3 centre = origin + new int3(Width / 2, PierHeight, Depth / 2);

            for (int i = 0; i < VoussoirCount; i++)
            {
                int2 start = SemicircleDirection(i, VoussoirCount);
                int2 end = SemicircleDirection(i + 1, VoussoirCount);
                Primitive wedge = CurvedPrimitiveEmitter.ArcWedge(
                    centre, OuterRadius, ClearSpan / 2, Depth, 2, start, end,
                    StoneMaterial, RingStyle, PrimitiveMode.Fill, order++, Coating);
                wedge.SurfaceFlags = VoxelSurfaceFlags.IntentionalSeam
                                   | VoxelSurfaceFlags.PreserveFeature;
                // Low detail values are generic per-piece variation; the style presentation row
                // controls their visual amplitude. High values remain available for explicit
                // seams written afterward, so structural material identity never changes.
                wedge.SurfaceDetail = (byte)(2 + PieceVariation(i, VoussoirCount));
                output.Add(wedge);

                if (profileBlocks != null)
                {
                    int projectionQ4 = ProfileProjectionQ4 > 0 ? ProfileProjectionQ4 : 8;
                    int profileDepthQ4 = ProfileDepthQ4 > 0 ? ProfileDepthQ4 : 16;
                    profileBlocks.Add(new ProfileBlock
                    {
                        Centre = centre,
                        InnerRadiusQ4 = ClearSpan * 8,
                        OuterRadiusQ4 = OuterRadius * 16,
                        FrontQ4 = origin.z * 16 - projectionQ4,
                        BackQ4 = origin.z * 16 - projectionQ4 + profileDepthQ4,
                        StartDirection = start,
                        EndDirection = end,
                        Axis = 2,
                        Material = StoneMaterial,
                        SurfaceStyle = RingStyle,
                        Coating = Coating,
                        SurfaceDetail = wedge.SurfaceDetail,
                        JointHalfWidthQ4 = ProfileJointHalfWidthQ4 > 0
                            ? ProfileJointHalfWidthQ4 : (byte)4,
                        BevelQ4 = ProfileBevelQ4 > 0 ? ProfileBevelQ4 : (byte)4,
                    });
                }
            }

            if (JointRecessDepth > 0 && profileBlocks == null)
            {
                // Mark each radial bed without changing structural occupancy. At this resolution
                // a full-cell carve is damage, not a shallow mortar recess; sub-voxel engraving
                // belongs in the authored boundary profile rather than a destructive workaround.
                int innerJointRadius = ClearSpan / 2 + 1;
                for (int i = 1; i < VoussoirCount; i++)
                {
                    int2 direction = SemicircleDirection(i, VoussoirCount);
                    int2 inner = PointOnRadius(direction, innerJointRadius);
                    int2 outer = PointOnRadius(direction, OuterRadius);
                    for (int depth = 0; depth < JointRecessDepth; depth++)
                    {
                        int z = origin.z + depth;
                        Primitive joint = CapsuleChainEmitter.Capsule(
                            new int3(centre.x + inner.x, centre.y + inner.y, z),
                            new int3(centre.x + outer.x, centre.y + outer.y, z),
                            0, StoneMaterial, PrimitiveMode.SurfaceDetail, order++, RingStyle);
                        joint.SurfaceDetail = 31;
                        joint.SurfaceFlags = VoxelSurfaceFlags.IntentionalSeam
                                           | VoxelSurfaceFlags.PreserveFeature;
                        output.Add(joint);
                    }
                }
            }
            return true;
        }

        private static int2 PointOnRadius(int2 direction, int radius)
        {
            long lengthSq = (long)direction.x * direction.x
                          + (long)direction.y * direction.y;
            int length = math.max(1, IntegerSqrt(lengthSq));
            return new int2(DivideRounded(direction.x * radius, length),
                            DivideRounded(direction.y * radius, length));
        }

        private static int DivideRounded(int numerator, int denominator) => numerator >= 0
            ? (numerator + denominator / 2) / denominator
            : (numerator - denominator / 2) / denominator;

        private static int IntegerSqrt(long value)
        {
            ulong n = (ulong)math.max(0L, value);
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
                else result >>= 1;
                bit >>= 2;
            }
            return (int)result;
        }

        private static VoxelSurfaceFlags StyleFlags(ushort style) =>
            style == SurfaceStyles.MasonryJoint
                ? VoxelSurfaceFlags.PreserveFeature
                : VoxelSurfaceFlags.None;

        private static int2 SemicircleDirection(int index, int count)
        {
            // Monotonic integer direction over the upper half-plane. Only direction is used by
            // the wedge predicate; annular radii retain an exact circular intrados/extrados.
            int x = count - index * 2;
            int y = index == 0 || index == count ? 0
                : 4 * index * (count - index) / count;
            return new int2(x, y);
        }

        private static int PieceVariation(int index, int count)
        {
            uint h = (uint)(index + 1) * 0x9E3779B9u ^ (uint)count * 0x85EBCA6Bu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return (int)(h % 12u);
        }
    }

    /// <summary>
    /// Architectural composition around an <see cref="ArchFeatureDefinition"/>. The arch remains
    /// the reusable structural primitive; this definition adds the wall mass, recessed spandrel,
    /// imposts and plinths needed for it to read as masonry rather than a freestanding hoop.
    /// </summary>
    public struct ArchBayFeatureDefinition
    {
        public ArchFeatureDefinition Arch;
        public int ShoulderWidth;
        public int TopMargin;
        public int FaceRecess;
        public int PlinthHeight;
        public int ImpostHeight;
        public ArchRuinDamage Damage;
        public uint DamageSeed;
        public byte DamageScale;

        public int Width => Arch.Width + math.max(0, ShoulderWidth) * 2;
        public int Height => Arch.Height + math.max(0, TopMargin);
        public int Depth => Arch.Depth + 2;

        public FeatureDefinition Metadata => new()
        {
            Name = "architectural-arch-bay",
            Kind = FeatureKind.Structure,
            BasePlane = BasePlaneRule.LowestGround,
            Footprint = new int3(Width, Height, Depth),
            MaxSlope = 1,
            MaxPrimitives = Arch.Metadata.MaxPrimitives + 11 + Veneer.EstimatedPrimitiveCount,
        };

        private BondedBlockVeneerDefinition Veneer => new()
        {
            Size = new int3(Width, Height, 1),
            CoursePitch = 6,
            NominalBlockWidth = 8,
            JointWidth = 1,
            Depth = 1,
            CornerRadius = 1,
            Seed = DamageSeed ^ 0xB5297A4Du,
            Material = Arch.StoneMaterial,
            SurfaceStyle = SurfaceStyles.MasonryJoint,
            Coating = Arch.Coating,
        };

        public ArchValidationError Validate<TMaterial, TSurface, TCoating>(
            in TMaterial palette, in TSurface surfaces, in TCoating coatings)
            where TMaterial : struct, IMaterialAuthoringCatalogue
            where TSurface : struct, ISurfaceStyleAuthoringCatalogue
            where TCoating : struct, ICoatingAuthoringCatalogue
        {
            ArchValidationError errors = Arch.Validate(in palette, in surfaces, in coatings);
            if (Metadata.MaxPrimitives > FeatureBudget.MaxPrimitivesPerInstance)
                errors |= ArchValidationError.PrimitiveBudgetExceeded;
            return errors;
        }

        public bool Emit(int3 origin, NativeList<Primitive> output) =>
            Emit(origin, output, null);

        public bool Emit(int3 origin, NativeList<Primitive> output,
                         IProfileBlockWriter profileBlocks)
        {
            if (!Arch.IsValid) return false;

            int shoulder = math.max(0, ShoulderWidth);
            int topMargin = math.max(1, TopMargin);
            int faceRecess = math.clamp(FaceRecess, 1, math.max(1, Arch.Depth - 2));
            int plinthHeight = math.clamp(PlinthHeight, 2, math.max(2, Arch.PierHeight / 3));
            int impostHeight = math.clamp(ImpostHeight, 2, math.max(2, Arch.PierHeight / 3));
            int3 archOrigin = origin + new int3(shoulder, 0, 1);
            int order = output.Length;

            ushort wallStyle = SurfaceStyles.MasonryJoint;
            VoxelSurfaceFlags masonryFlags = VoxelSurfaceFlags.PreserveFeature;

            // Recessed backing occupies the spandrel and shoulders. Carving the clear opening
            // before the proud ring is emitted leaves a continuous wall behind recessed joints.
            Primitive backing = BoxEmitter.Box(
                new int3(origin.x, origin.y + Arch.PierHeight,
                         archOrigin.z + faceRecess),
                new int3(Width, Arch.OuterRadius + topMargin,
                         math.max(2, Arch.Depth - faceRecess)),
                Arch.StoneMaterial, PrimitiveMode.Fill, order++, wallStyle, Arch.Coating);
            backing.SurfaceFlags = masonryFlags;
            output.Add(backing);

            BondedBlockVeneerDefinition veneer = Veneer;
            veneer.Emit(new int3(origin.x, origin.y, archOrigin.z), output);
            order = output.Length;

            // Close the veneer joints with recessed structural backing. Keeping this separate
            // preserves real block silhouettes while avoiding through-holes in a bonded wall.
            Primitive faceBacking = BoxEmitter.Box(
                new int3(origin.x, origin.y, archOrigin.z + 1),
                new int3(Width, Height, 1), Arch.StoneMaterial,
                PrimitiveMode.FillIfEmpty, order++, wallStyle, Arch.Coating);
            faceBacking.SurfaceFlags = masonryFlags;
            output.Add(faceBacking);

            int backingDepth = Arch.Depth;
            int backingZ = archOrigin.z;
            int3 openingCentre = new(
                archOrigin.x + Arch.Width / 2,
                archOrigin.y + Arch.PierHeight,
                backingZ + backingDepth / 2);
            // Remove only the face veneer across the full ring envelope, then let Arch.Emit
            // restore the structural voussoirs. This produces an actual proud ring over recessed
            // backing rather than relying on a colour border or overlapping coplanar surfaces.
            int3 faceOpeningCentre = openingCentre;
            faceOpeningCentre.z = archOrigin.z + 1;
            output.Add(CurvedPrimitiveEmitter.Annulus(
                faceOpeningCentre, Arch.OuterRadius, 0, 2, 2, true,
                Arch.StoneMaterial, wallStyle, PrimitiveMode.Carve, order++));
            // Integer-circle geometry is 2r + 1 cells wide. Derive the rectangular opening from
            // the actual pier footprints so no one-cell veneer/backing strips survive at either jamb.
            int clearWidth = math.max(1, Arch.Width - Arch.RingThickness * 2);
            output.Add(BoxEmitter.Box(
                new int3(openingCentre.x - clearWidth / 2, origin.y, backingZ),
                new int3(clearWidth, Arch.PierHeight + 1, backingDepth),
                Arch.StoneMaterial, PrimitiveMode.Carve, order++, wallStyle));
            // The rectangular lower opening already owns everything below the spring diameter.
            // Use a full disk here so the deep curved carve contributes only the radial intrados
            // boundary; a half-disk would author a false horizontal boundary at the spring plane.
            output.Add(CurvedPrimitiveEmitter.Annulus(
                openingCentre, Arch.ClearSpan / 2, 0,
                backingDepth, 2, false,
                Arch.StoneMaterial, wallStyle, PrimitiveMode.Carve, order++));

            int sideWidth = shoulder + Arch.RingThickness;
            Primitive leftWall = CurvedPrimitiveEmitter.RoundedBox(
                new int3(origin.x, origin.y, archOrigin.z + faceRecess),
                new int3(sideWidth, Arch.PierHeight + 1,
                         math.max(2, Arch.Depth - faceRecess)),
                1, Arch.StoneMaterial, wallStyle, PrimitiveMode.Fill, order++, Arch.Coating,
                extrusionAxis: 2);
            leftWall.SurfaceFlags = masonryFlags;
            output.Add(leftWall);
            Primitive rightWall = leftWall;
            rightWall.A.x = origin.x + Width - sideWidth;
            rightWall.B.x = origin.x + Width - 1;
            rightWall.Order = order++;
            output.Add(rightWall);

            if (!Arch.Emit(archOrigin, output, profileBlocks)) return false;
            order = output.Length;

            int pierWidth = Arch.RingThickness;
            int leftPierX = archOrigin.x;
            int rightPierX = archOrigin.x + Arch.Width - pierWidth;
            int ornamentWidth = pierWidth + 4;

            Primitive leftPlinth = CurvedPrimitiveEmitter.RoundedBox(
                new int3(leftPierX - 2, origin.y, origin.z),
                new int3(ornamentWidth, plinthHeight, Depth), 1,
                Arch.StoneMaterial, wallStyle, PrimitiveMode.Fill, order++, Arch.Coating,
                extrusionAxis: 2);
            leftPlinth.SurfaceFlags = masonryFlags;
            output.Add(leftPlinth);
            Primitive rightPlinth = leftPlinth;
            rightPlinth.A.x = rightPierX - 2;
            rightPlinth.B.x = rightPlinth.A.x + ornamentWidth - 1;
            rightPlinth.Order = order++;
            output.Add(rightPlinth);

            // Imposts can project into the shoulder like a masonry capital, but projecting into
            // the clear span creates a disconnected-looking shelf at the spring from oblique views.
            int impostY = origin.y + Arch.PierHeight - impostHeight + 1;
            int impostWidth = pierWidth + 2;
            Primitive leftImpost = CurvedPrimitiveEmitter.RoundedBox(
                new int3(leftPierX - 2, impostY, archOrigin.z),
                new int3(impostWidth, impostHeight, Depth - 1), 1,
                Arch.StoneMaterial, wallStyle, PrimitiveMode.Fill, order++, Arch.Coating,
                extrusionAxis: 2);
            leftImpost.SurfaceFlags = masonryFlags;
            output.Add(leftImpost);
            Primitive rightImpost = leftImpost;
            rightImpost.A.x = rightPierX;
            rightImpost.B.x = rightImpost.A.x + impostWidth - 1;
            rightImpost.Order = order++;
            output.Add(rightImpost);

            EmitDamage(origin, archOrigin, ref order, output);
            return true;
        }

        private void EmitDamage(int3 origin, int3 archOrigin, ref int order,
                                NativeList<Primitive> output)
        {
            if (Damage == ArchRuinDamage.Intact) return;
            int scale = math.max(1, DamageScale);
            int jitterX = SignedJitter(DamageSeed, 0x9E3779B9u, 2);
            int jitterY = SignedJitter(DamageSeed, 0x85EBCA6Bu, 1);
            int3 crown = archOrigin + new int3(
                Arch.Width / 2 + jitterX,
                Arch.PierHeight + Arch.OuterRadius + jitterY,
                Arch.Depth / 2);
            ushort style = SurfaceStyles.MasonryJoint;

            if (Damage == ArchRuinDamage.BrokenCrown)
            {
                output.Add(CurvedPrimitiveEmitter.Ellipsoid(
                    crown, new int3(Arch.RingThickness / 2 + scale,
                                    Arch.RingThickness / 2 + scale,
                                    Arch.Depth),
                    Arch.StoneMaterial, style, PrimitiveMode.Carve, order++));
                output.Add(CurvedPrimitiveEmitter.Ellipsoid(
                    crown + new int3(scale + 1, scale, 0),
                    new int3(math.max(2, scale + 1), math.max(2, scale), Arch.Depth),
                    Arch.StoneMaterial, style, PrimitiveMode.Carve, order++));
                return;
            }

            if (Damage == ArchRuinDamage.BrokenLeftHaunch
                || Damage == ArchRuinDamage.BrokenRightHaunch)
            {
                int side = Damage == ArchRuinDamage.BrokenLeftHaunch ? -1 : 1;
                int3 haunch = archOrigin + new int3(
                    Arch.Width / 2 + side * (Arch.ClearSpan * 3 / 8) + jitterX,
                    Arch.PierHeight + Arch.ClearSpan * 3 / 8 + jitterY,
                    Arch.Depth / 2);
                output.Add(CurvedPrimitiveEmitter.Ellipsoid(
                    haunch, new int3(Arch.RingThickness + scale,
                                     Arch.RingThickness / 2 + scale,
                                     Arch.Depth),
                    Arch.StoneMaterial, style, PrimitiveMode.Carve, order++));
                return;
            }

            // A shoulder collapse removes an irregular upper corner but leaves the load-bearing
            // pier and most of the ring readable. Two overlapping explicit cuts avoid a perfectly
            // rectangular silhouette without hiding damage inside a material or shader branch.
            int sideSign = (DamageSeed & 1u) == 0u ? -1 : 1;
            int shoulderWidth = math.max(4, ShoulderWidth + Arch.RingThickness / 2);
            int cutX = sideSign < 0 ? origin.x : origin.x + Width - shoulderWidth;
            output.Add(CurvedPrimitiveEmitter.Ellipsoid(
                new int3(cutX + shoulderWidth / 2,
                         origin.y + Height - math.max(2, TopMargin / 2),
                         origin.z + Depth / 2),
                new int3(shoulderWidth, math.max(3, TopMargin), Depth),
                Arch.StoneMaterial, style, PrimitiveMode.Carve, order++));
        }

        private static int SignedJitter(uint seed, uint salt, int magnitude)
        {
            if (magnitude <= 0) return 0;
            uint h = seed ^ salt;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return (int)(h % (uint)(magnitude * 2 + 1)) - magnitude;
        }
    }
}
