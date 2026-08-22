#ifndef VOXEL_BRICK_DENSITY_INCLUDED
#define VOXEL_BRICK_DENSITY_INCLUDED

// Density sampling for the GPU mesher.
//
// This is a deliberate line-by-line port of TransvoxelDensityJob.SampleField. The two meshers must
// agree: a GPU surface that differs from the CPU one for the same voxels is not a faster renderer,
// it is a second renderer, and the drift between them is the recurring look regression this project
// already knows about. The oracle test compares the two outputs directly, so any divergence here is
// a test failure rather than something noticed months later on a rooftop.
//
// Where the CPU reads a NativeArray this reads a packed buffer; the arithmetic is otherwise
// identical, including the tap weights and the order they accumulate in.

// -- authoritative brick payload (the mirror) --------------------------------

StructuredBuffer<uint> _BrickMaterials;        // 4 material bytes per word
StructuredBuffer<uint> _BrickSurfaceSemantics; // 2 surface ushorts per word
StructuredBuffer<uint> _BrickBoundarySamples;  // 4 boundary bytes per word

// One entry per brick in the meshed chunk's neighbourhood, mirroring the CPU brick cache:
//   bits  0..1  kind (0 empty, 1 uniform, 2 mixed)
//   bits  8..15 uniform material
//   bits 16..31 mirror slot for a mixed brick
StructuredBuffer<uint> _BrickCache;

int3 _BrickCacheOrigin;
int _BrickCacheEdge;

// -- catalogues --------------------------------------------------------------

StructuredBuffer<uint> _StyleWords;    // GpuSurfaceCataloguePacking.PackStyle
StructuredBuffer<uint> _JoinWords;     // GpuSurfaceCataloguePacking.PackJoin
StructuredBuffer<uint> _CoatingWords;  // 3 words per coating
StructuredBuffer<uint> _MaterialDefaultStyle;

#define STYLE_COUNT 32
#define JOIN_GROUP_COUNT 16
#define COATING_WORDS 3

#define RECONSTRUCTION_SMOOTH 0
#define RECONSTRUCTION_PLANAR 1
#define RECONSTRUCTION_ROUNDED 2
#define RECONSTRUCTION_SHARP 3
#define RECONSTRUCTION_CUBIC 4

#define COMPATIBILITY_JOIN 0
#define CONTINUITY_DISCONTINUOUS 0

// SurfaceStyles: MaterialDefault is 0 and Smooth is 1, not a sentinel and zero. Getting this
// backwards means the default style is never resolved and every cell reads style 0 instead, which
// is a planar fallback — so smooth terrain silently meshes as though it were architecture.
#define SURFACE_STYLE_MATERIAL_DEFAULT 0
#define SURFACE_STYLE_SMOOTH 1

struct StyleDefinition
{
    uint reconstruction;
    uint curvature;
    uint joinGroup;
    bool preserveSharpFeatures;
};

StyleDefinition LoadStyle(uint styleId)
{
    StyleDefinition style;
    uint packed = styleId < STYLE_COUNT ? _StyleWords[styleId] : 0u;
    style.reconstruction = packed & 0xFFu;
    style.curvature = (packed >> 8) & 0xFFu;
    style.joinGroup = (packed >> 16) & 0xFFu;
    style.preserveSharpFeatures = (packed & (1u << 24)) != 0u;
    return style;
}

struct JoinRule
{
    uint compatibility;
    uint continuity;
    uint blendWidth;
};

JoinRule LoadJoin(uint groupA, uint groupB)
{
    JoinRule join;
    // Out-of-range groups fall back to a sharp seam, matching SurfaceJoinReadRule.SharpSeam.
    if (groupA >= JOIN_GROUP_COUNT || groupB >= JOIN_GROUP_COUNT)
    {
        join.compatibility = 1u;               // Seam
        join.continuity = CONTINUITY_DISCONTINUOUS;
        join.blendWidth = 0u;
        return join;
    }
    uint packed = _JoinWords[groupA * JOIN_GROUP_COUNT + groupB];
    join.compatibility = packed & 0xFu;
    join.continuity = (packed >> 4) & 0xFu;
    join.blendWidth = (packed >> 8) & 0xFFu;
    return join;
}

float CoatingDisplacement(uint surface)
{
    uint coating = (surface >> 16) & 0xFFu;
    uint word1 = _CoatingWords[coating * COATING_WORDS + 1];
    return (word1 & 0xFFu) * (1.0 / 64.0);
}

// -- voxel reads -------------------------------------------------------------

// Matches TransvoxelDensityJob.IsSolidSample. 11 and 16 are non-solid presentation materials.
bool IsSolidSample(uint material)
{
    return material != 0u && material != 11u && material != 16u;
}

float CurvatureFactor(StyleDefinition style)
{
    if (style.reconstruction == RECONSTRUCTION_PLANAR
     || style.reconstruction == RECONSTRUCTION_SHARP
     || style.reconstruction == RECONSTRUCTION_CUBIC)
        return 0.0;
    return style.curvature / 255.0;
}

uint ResolveSurface(uint material, uint surface)
{
    uint style = surface & 0xFFFFu;
    if (style == SURFACE_STYLE_MATERIAL_DEFAULT) style = _MaterialDefaultStyle[material & 0xFFu];
    if (style == SURFACE_STYLE_MATERIAL_DEFAULT) style = SURFACE_STYLE_SMOOTH;
    return (surface & 0xFFFF0000u) | style;
}

// Arithmetic shift for floor division, so the half of the world at negative coordinates does not
// fold onto the origin. HLSL's >> on int is arithmetic, which is what this relies on.
int3 WorldBrickOf(int3 p) { return int3(p.x >> 3, p.y >> 3, p.z >> 3); }

uint ReadMaterial(int3 p, out uint surface, out uint boundary)
{
    surface = 0u;
    boundary = 0u;

    int3 localBrick = WorldBrickOf(p) - _BrickCacheOrigin;
    if ((uint)localBrick.x >= (uint)_BrickCacheEdge
     || (uint)localBrick.y >= (uint)_BrickCacheEdge
     || (uint)localBrick.z >= (uint)_BrickCacheEdge)
        return 0u;

    int brickIndex = localBrick.x
                   + _BrickCacheEdge * (localBrick.y + _BrickCacheEdge * localBrick.z);
    uint entry = _BrickCache[brickIndex];
    uint kind = entry & 0x3u;
    if (kind == 0u) return 0u;
    if (kind == 1u) return (entry >> 8) & 0xFFu;

    uint slot = entry >> 16;
    uint voxel = (uint)((p.x & 7) + 8 * ((p.y & 7) + 8 * (p.z & 7)));

    uint materialWord = _BrickMaterials[slot * 128u + (voxel >> 2)];
    uint material = (materialWord >> ((voxel & 3u) * 8u)) & 0xFFu;

    uint surfaceWord = _BrickSurfaceSemantics[slot * 256u + (voxel >> 1)];
    surface = (surfaceWord >> ((voxel & 1u) * 16u)) & 0xFFFFu;

    uint boundaryWord = _BrickBoundarySamples[slot * 128u + (voxel >> 2)];
    boundary = (boundaryWord >> ((voxel & 3u) * 8u)) & 0xFFu;

    return material;
}

// Matches VoxelBoundarySample. The sample is authored whenever any bit is set; the offset is a
// 6-bit field biased by 32, and the top two bits carry the extrusion axis (0 meaning "all axes").
bool BoundaryIsAuthored(uint packed) { return packed != 0u; }

int BoundarySignedQ3(uint packed)
{
    return BoundaryIsAuthored(packed) ? (int)(packed & 0x3Fu) - 32 : 0;
}

int BoundaryExtrusionAxis(uint packed)
{
    uint code = packed >> 6;
    return code == 0u ? 3 : (int)code - 1;
}

bool BoundaryAppliesAlong(uint packed, int edgeAxis)
{
    int axis = BoundaryExtrusionAxis(packed);
    return BoundaryIsAuthored(packed) && (axis == 3 || edgeAxis != axis);
}

float AddTap(int3 p, float weight, bool centreSolid, StyleDefinition centreStyle,
             inout uint dominantMaterial, inout uint dominantSurface)
{
    uint surface, boundary;
    uint material = ReadMaterial(p, surface, boundary);
    if (!IsSolidSample(material)) return 0.0;

    surface = ResolveSurface(material, surface);
    if (dominantMaterial == 0u)
    {
        dominantMaterial = material;
        dominantSurface = surface;
    }
    if (!centreSolid) return weight;

    StyleDefinition neighbourStyle = LoadStyle(surface & 0xFFFFu);
    JoinRule join = LoadJoin(centreStyle.joinGroup, neighbourStyle.joinGroup);
    if (join.compatibility != COMPATIBILITY_JOIN || join.continuity == CONTINUITY_DISCONTINUOUS)
        return weight;

    // Smooth-compatible neighbours share reconstruction influence. This pairwise rule is what lets
    // curvature propagate without a style deciding unilaterally how its neighbour is rebuilt.
    float neighbourCurvature = CurvatureFactor(neighbourStyle);
    return weight * lerp(1.0, neighbourCurvature, saturate(join.blendWidth * 0.5));
}

float SampleField(int3 p, out uint dominantMaterial, out uint dominantSurface,
                  out uint dominantBoundary)
{
    uint centreSurface, packedBoundary;
    uint centre = ReadMaterial(p, centreSurface, packedBoundary);
    dominantBoundary = packedBoundary;

    bool centreSolid = IsSolidSample(centre);
    centreSurface = ResolveSurface(centre, centreSurface);

    // Must stay identical to TransvoxelDensityJob.SampleField: an authored sample is trusted exactly
    // when its sign agrees with authoritative occupancy. See the comment there for why both the old
    // six-neighbour gate and no gate at all are wrong.
    if (packedBoundary != 0u && centreSolid == (BoundarySignedQ3(packedBoundary) >= 0))
    {
        dominantMaterial = centreSolid ? centre : 0u;
        dominantSurface = centreSolid ? centreSurface : 0u;
        return BoundarySignedQ3(packedBoundary) * 0.125 + CoatingDisplacement(centreSurface);
    }

    StyleDefinition centreStyle = LoadStyle(centreSurface & 0xFFFFu);
    if (centreSolid && (centreStyle.reconstruction == RECONSTRUCTION_PLANAR
                     || centreStyle.reconstruction == RECONSTRUCTION_SHARP
                     || centreStyle.reconstruction == RECONSTRUCTION_CUBIC))
    {
        dominantMaterial = centre;
        dominantSurface = centreSurface;
        return 0.5 + CoatingDisplacement(centreSurface);
    }

    float curvature = CurvatureFactor(centreStyle);
    float centreWeight = lerp(0.55, 0.40, curvature);
    float mass = centreSolid ? centreWeight : 0.0;
    dominantMaterial = centreSolid ? centre : 0u;
    dominantSurface = centreSolid ? centreSurface : 0u;

    float near = 0.06 * curvature;
    mass += AddTap(p + int3( 1, 0, 0), near, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3(-1, 0, 0), near, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0, 1, 0), near, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0,-1, 0), near, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0, 0, 1), near, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0, 0,-1), near, centreSolid, centreStyle, dominantMaterial, dominantSurface);

    float far = 0.04 * curvature;
    mass += AddTap(p + int3( 2, 0, 0), far, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3(-2, 0, 0), far, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0, 2, 0), far, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0,-2, 0), far, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0, 0, 2), far, centreSolid, centreStyle, dominantMaterial, dominantSurface);
    mass += AddTap(p + int3( 0, 0,-2), far, centreSolid, centreStyle, dominantMaterial, dominantSurface);

    return mass - 0.5 + (centreSolid ? CoatingDisplacement(centreSurface) : 0.0);
}

#endif // VOXEL_BRICK_DENSITY_INCLUDED
