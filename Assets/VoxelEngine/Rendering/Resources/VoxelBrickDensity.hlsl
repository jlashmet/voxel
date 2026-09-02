#ifndef VOXEL_BRICK_DENSITY_INCLUDED
#define VOXEL_BRICK_DENSITY_INCLUDED

// Density sampling for the GPU mesher. This remains a line-by-line semantic port of the CPU
// Transvoxel density job, but production may now resolve world bricks from the persistent GPU
// mirror instead of receiving a CPU-flattened dense brick neighbourhood for every chunk.

StructuredBuffer<uint> _BrickMaterials;        // payload followed by persistent lookup directory
StructuredBuffer<uint> _BrickSurfaceSemantics;
StructuredBuffer<uint> _BrickBoundarySamples;

// Legacy mode: one dense entry per brick in the chunk neighbourhood.
// Persistent mode: entries 0..2 are a tiny classifier-safe header. Values that could look like
// brick kinds are shifted left by two so the existing raw classifier sees all three as empty:
//   [0] masked magic, [1] directory word offset << 2, [2] directory mask << 2.
StructuredBuffer<uint> _BrickCache;

int3 _BrickCacheOrigin;
int _BrickCacheEdge;

StructuredBuffer<uint> _StyleWords;
StructuredBuffer<uint> _JoinWords;
StructuredBuffer<uint> _CoatingWords;
StructuredBuffer<uint> _MaterialDefaultStyle;
uint _SolidWaterMaterialMask;

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
#define AUTHORITATIVE_SOLID_BIT (1u << 26)
#define SURFACE_STYLE_MATERIAL_DEFAULT 0u
#define SURFACE_STYLE_SMOOTH 1u
#define SURFACE_STYLE_MATERIAL_BLEND 16u
#define SURFACE_STYLE_RECONSTRUCTION_MASK 15u

#define PERSISTENT_LOOKUP_MAGIC 0x47505540u
#define DIRECTORY_WORDS_PER_ENTRY 5u
#define DIRECTORY_OCCUPIED 1u

bool IsMaterialBlendSurface(uint surface)
{
    return ((surface & 0xFFFFu) & SURFACE_STYLE_MATERIAL_BLEND) != 0u;
}

uint ReconstructionStyleId(uint surface)
{
    return (surface & 0xFFFFu) & SURFACE_STYLE_RECONSTRUCTION_MASK;
}

uint WithAuthoritativeOccupancy(uint surface, bool solid)
{
    return solid ? surface | AUTHORITATIVE_SOLID_BIT : surface & ~AUTHORITATIVE_SOLID_BIT;
}

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
    styleId &= SURFACE_STYLE_RECONSTRUCTION_MASK;
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
    if (groupA >= JOIN_GROUP_COUNT || groupB >= JOIN_GROUP_COUNT)
    {
        join.compatibility = 1u;
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
    // In material-blend mode the coating byte is the secondary material ID. Treating it as a
    // coating would move otherwise identical geometry whenever that material index happened to
    // coincide with snow/moss/etc. The blend marker makes the two interpretations disjoint.
    if (IsMaterialBlendSurface(surface)) return 0.0;
    uint coating = (surface >> 16) & 0xFFu;
    uint word1 = _CoatingWords[coating * COATING_WORDS + 1];
    return (word1 & 0xFFu) * (1.0 / 64.0);
}

// Matches the shared semantic solid classifier: material IDs are opaque and the installed
// presentation mask decides which renderer materials are water.
bool IsSolidSample(uint material)
{
    if (material == 0u) return false;
    return material >= 32u || (_SolidWaterMaterialMask & (1u << material)) == 0u;
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
    uint authoredStyle = surface & 0xFFFFu;
    uint blendMarker = authoredStyle & SURFACE_STYLE_MATERIAL_BLEND;
    uint style = authoredStyle & SURFACE_STYLE_RECONSTRUCTION_MASK;
    if (style == SURFACE_STYLE_MATERIAL_DEFAULT) style = _MaterialDefaultStyle[material & 0xFFu];
    if (style == SURFACE_STYLE_MATERIAL_DEFAULT) style = SURFACE_STYLE_SMOOTH;
    return (surface & 0xFFFF0000u) | blendMarker | (style & SURFACE_STYLE_RECONSTRUCTION_MASK);
}

uint DecodeSurfaceStorage(uint packedStorage)
{
    uint style = packedStorage & 0x1Fu;
    uint coating = (packedStorage >> 5) & 0x0Fu;
    uint flags = (packedStorage >> 9) & 0x03u;
    uint detail = (packedStorage >> 11) & 0x1Fu;
    return style | (coating << 16) | ((flags | (detail << 3)) << 24);
}

int3 WorldBrickOf(int3 p) { return int3(p.x >> 3, p.y >> 3, p.z >> 3); }

#if !defined(VOXEL_FORCE_DENSE_LOOKUP)
uint HashBrickCoordinate(int3 coordinate)
{
    uint h = asuint(coordinate.x) * 0x8da6b343u;
    h ^= asuint(coordinate.y) * 0xd8163841u;
    h ^= asuint(coordinate.z) * 0xcb1ab31fu;
    h ^= h >> 16;
    h *= 0x7feb352du;
    h ^= h >> 15;
    return h;
}

uint PersistentBrickEntry(int3 coordinate)
{
    uint wordOffset = _BrickCache[1] >> 2;
    uint mask = _BrickCache[2] >> 2;
    uint start = HashBrickCoordinate(coordinate) & mask;

    // The CPU directory inserts with the identical linear-probe rule. Ready regions contain every
    // logical brick, including empty/uniform ones, so normal lookups terminate after very few probes;
    // the full bound exists only to make collision handling exact rather than probabilistic.
    [loop]
    for (uint probe = 0u; probe <= mask; probe++)
    {
        uint slot = (start + probe) & mask;
        uint word = wordOffset + slot * DIRECTORY_WORDS_PER_ENTRY;
        uint state = _BrickMaterials[word + 4u];
        if (state == 0u) return 0u;
        if (state != DIRECTORY_OCCUPIED) continue;
        if (asint(_BrickMaterials[word + 0u]) != coordinate.x
         || asint(_BrickMaterials[word + 1u]) != coordinate.y
         || asint(_BrickMaterials[word + 2u]) != coordinate.z)
            continue;
        return _BrickMaterials[word + 3u];
    }
    return 0u;
}
#endif

uint ReadMaterial(int3 p, out uint surface, out uint boundary)
{
    surface = 0u;
    boundary = 0u;

    int3 worldBrick = WorldBrickOf(p);
    uint entry = 0u;
#if defined(VOXEL_FORCE_DENSE_LOOKUP)
    int3 localBrick = worldBrick - _BrickCacheOrigin;
    if ((uint)localBrick.x >= (uint)_BrickCacheEdge
     || (uint)localBrick.y >= (uint)_BrickCacheEdge
     || (uint)localBrick.z >= (uint)_BrickCacheEdge)
        return 0u;
    int brickIndex = localBrick.x
                   + _BrickCacheEdge * (localBrick.y + _BrickCacheEdge * localBrick.z);
    entry = _BrickCache[brickIndex];
#elif defined(VOXEL_FORCE_PERSISTENT_LOOKUP)
    entry = PersistentBrickEntry(worldBrick);
#else
    [branch]
    if (_BrickCache[0] == PERSISTENT_LOOKUP_MAGIC)
    {
        entry = PersistentBrickEntry(worldBrick);
    }
    else
    {
        int3 localBrick = worldBrick - _BrickCacheOrigin;
        if ((uint)localBrick.x >= (uint)_BrickCacheEdge
         || (uint)localBrick.y >= (uint)_BrickCacheEdge
         || (uint)localBrick.z >= (uint)_BrickCacheEdge)
            return 0u;
        int brickIndex = localBrick.x
                       + _BrickCacheEdge * (localBrick.y + _BrickCacheEdge * localBrick.z);
        entry = _BrickCache[brickIndex];
    }
#endif

    uint kind = entry & 0x3u;
    if (kind == 0u) return 0u;
    if (kind == 1u) return (entry >> 8) & 0xFFu;

    uint slot = entry >> 16;
    uint voxel = (uint)((p.x & 7) + 8 * ((p.y & 7) + 8 * (p.z & 7)));

    uint materialWord = _BrickMaterials[slot * 128u + (voxel >> 2)];
    uint material = (materialWord >> ((voxel & 3u) * 8u)) & 0xFFu;

    uint surfaceWord = _BrickSurfaceSemantics[slot * 256u + (voxel >> 1)];
    uint packedStorage = (surfaceWord >> ((voxel & 1u) * 16u)) & 0xFFFFu;
    surface = DecodeSurfaceStorage(packedStorage);

    uint boundaryWord = _BrickBoundarySamples[slot * 128u + (voxel >> 2)];
    boundary = (boundaryWord >> ((voxel & 3u) * 8u)) & 0xFFu;

    return material;
}

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

    float neighbourCurvature = CurvatureFactor(neighbourStyle);
    return weight * lerp(1.0, neighbourCurvature, saturate(join.blendWidth * 0.5));
}

void ConsiderExposedMaterial(int exposedDistance, bool preferVisibleTopMaterial,
                             uint material, uint surface,
                             inout int bestDistance, inout int bestMaterialDistance,
                             inout bool hasVisibleTopMaterial,
                             inout uint dominantMaterial, inout uint dominantSurface)
{
    bestDistance = min(bestDistance, exposedDistance);
    bool shouldUseMaterial = preferVisibleTopMaterial
                          || (!hasVisibleTopMaterial && exposedDistance < bestMaterialDistance);
    if (!shouldUseMaterial) return;

    if (preferVisibleTopMaterial) hasVisibleTopMaterial = true;
    bestMaterialDistance = exposedDistance;
    dominantMaterial = material;
    dominantSurface = surface;
}

void ConsiderCrossingRay(int3 p, int3 direction, bool preferVisibleTopMaterial, int sourceStep,
                         uint centreMaterial, uint centreSurface,
                         inout int bestDistance, inout int bestMaterialDistance,
                         inout bool hasVisibleTopMaterial,
                         inout uint dominantMaterial, inout uint dominantSurface)
{
    uint farSurface, farBoundary;
    uint farMaterial = ReadMaterial(p + direction * sourceStep, farSurface, farBoundary);
    if (IsSolidSample(farMaterial)) return;

    uint lastMaterial = centreMaterial;
    uint lastSurface = centreSurface;
    for (int distance = 1; distance < sourceStep; distance++)
    {
        uint surface, boundary;
        uint material = ReadMaterial(p + direction * distance, surface, boundary);
        if (!IsSolidSample(material))
        {
            ConsiderExposedMaterial(distance - 1, preferVisibleTopMaterial,
                lastMaterial, lastSurface, bestDistance, bestMaterialDistance,
                hasVisibleTopMaterial, dominantMaterial, dominantSurface);
            return;
        }

        lastMaterial = material;
        lastSurface = ResolveSurface(material, surface);
    }

    ConsiderExposedMaterial(sourceStep - 1, preferVisibleTopMaterial,
        lastMaterial, lastSurface, bestDistance, bestMaterialDistance,
        hasVisibleTopMaterial, dominantMaterial, dominantSurface);
}

int PreferNearestCrossingSurfaceMaterial(int3 p, int sourceStep,
                                         uint centreMaterial, uint centreSurface,
                                         inout uint dominantMaterial,
                                         inout uint dominantSurface)
{
    int bestDistance = sourceStep;
    int bestMaterialDistance = sourceStep;
    bool hasVisibleTopMaterial = false;

    ConsiderCrossingRay(p, int3( 1, 0, 0), false, sourceStep, centreMaterial, centreSurface,
        bestDistance, bestMaterialDistance, hasVisibleTopMaterial, dominantMaterial, dominantSurface);
    ConsiderCrossingRay(p, int3(-1, 0, 0), false, sourceStep, centreMaterial, centreSurface,
        bestDistance, bestMaterialDistance, hasVisibleTopMaterial, dominantMaterial, dominantSurface);
    ConsiderCrossingRay(p, int3(0,  1, 0), true, sourceStep, centreMaterial, centreSurface,
        bestDistance, bestMaterialDistance, hasVisibleTopMaterial, dominantMaterial, dominantSurface);
    ConsiderCrossingRay(p, int3(0, -1, 0), false, sourceStep, centreMaterial, centreSurface,
        bestDistance, bestMaterialDistance, hasVisibleTopMaterial, dominantMaterial, dominantSurface);
    ConsiderCrossingRay(p, int3(0, 0,  1), false, sourceStep, centreMaterial, centreSurface,
        bestDistance, bestMaterialDistance, hasVisibleTopMaterial, dominantMaterial, dominantSurface);
    ConsiderCrossingRay(p, int3(0, 0,-1), false, sourceStep, centreMaterial, centreSurface,
        bestDistance, bestMaterialDistance, hasVisibleTopMaterial, dominantMaterial, dominantSurface);
    return bestDistance;
}

void ConsiderPhaseCrossingRay(int3 p, int3 direction, int sourceStep, bool centreSolid,
                              inout int bestDistance)
{
    uint farSurface, farBoundary;
    uint farMaterial = ReadMaterial(p + direction * sourceStep, farSurface, farBoundary);
    if (IsSolidSample(farMaterial) == centreSolid) return;

    for (int distance = 1; distance < sourceStep; distance++)
    {
        uint surface, boundary;
        uint material = ReadMaterial(p + direction * distance, surface, boundary);
        if (IsSolidSample(material) == centreSolid) continue;
        bestDistance = min(bestDistance, distance - 1);
        return;
    }

    bestDistance = min(bestDistance, sourceStep - 1);
}

int FindNearestCrossingDistance(int3 p, int sourceStep, bool centreSolid)
{
    int bestDistance = sourceStep;
    ConsiderPhaseCrossingRay(p, int3( 1, 0, 0), sourceStep, centreSolid, bestDistance);
    ConsiderPhaseCrossingRay(p, int3(-1, 0, 0), sourceStep, centreSolid, bestDistance);
    ConsiderPhaseCrossingRay(p, int3(0,  1, 0), sourceStep, centreSolid, bestDistance);
    ConsiderPhaseCrossingRay(p, int3(0, -1, 0), sourceStep, centreSolid, bestDistance);
    ConsiderPhaseCrossingRay(p, int3(0, 0,  1), sourceStep, centreSolid, bestDistance);
    ConsiderPhaseCrossingRay(p, int3(0, 0, -1), sourceStep, centreSolid, bestDistance);
    return bestDistance;
}

float SampleField(int3 p, int sourceStep, out uint dominantMaterial, out uint dominantSurface,
                  out uint dominantBoundary)
{
    uint centreSurface, packedBoundary;
    uint centre = ReadMaterial(p, centreSurface, packedBoundary);
    dominantBoundary = packedBoundary;

    bool centreSolid = IsSolidSample(centre);
    centreSurface = ResolveSurface(centre, centreSurface);

    if (packedBoundary != 0u && centreSolid == (BoundarySignedQ3(packedBoundary) >= 0))
    {
        dominantMaterial = centreSolid ? centre : 0u;
        dominantSurface = WithAuthoritativeOccupancy(centreSolid ? centreSurface : 0u, centreSolid);
        return BoundarySignedQ3(packedBoundary) * 0.125 + CoatingDisplacement(centreSurface);
    }

    StyleDefinition centreStyle = LoadStyle(centreSurface & 0xFFFFu);
    if (centreSolid && (centreStyle.reconstruction == RECONSTRUCTION_PLANAR
                     || centreStyle.reconstruction == RECONSTRUCTION_SHARP
                     || centreStyle.reconstruction == RECONSTRUCTION_CUBIC))
    {
        dominantMaterial = centre;
        dominantSurface = WithAuthoritativeOccupancy(centreSurface, true);
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

    float density = mass - 0.5;
    int nearestCrossingDistance = sourceStep;
    if (sourceStep > 1)
    {
        if (centreSolid)
        {
            nearestCrossingDistance = PreferNearestCrossingSurfaceMaterial(
                p, sourceStep, centre, centreSurface, dominantMaterial, dominantSurface);
        }
        else
        {
            nearestCrossingDistance = FindNearestCrossingDistance(p, sourceStep, centreSolid);
        }

        bool densitySignMatchesOccupancy = centreSolid ? density >= 0.0 : density < 0.0;
        if (nearestCrossingDistance < sourceStep && densitySignMatchesOccupancy)
        {
            float phase = (nearestCrossingDistance + 0.5) / sourceStep;
            density = centreSolid ? phase : -phase;
        }
    }

    // Match TransvoxelDensityJob.Execute: presentation identity can extend onto nearby air-centred
    // samples, but the transient occupancy bit always records the authoritative centre voxel.
    dominantSurface = WithAuthoritativeOccupancy(dominantSurface, centreSolid);

    return density + (centreSolid ? CoatingDisplacement(centreSurface) : 0.0);
}

#endif // VOXEL_BRICK_DENSITY_INCLUDED