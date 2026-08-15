using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Runtime-only orchestration for the interactive arch authoring path. Application
    /// code supplies stable values through Composition; concrete feature emitters,
    /// rasterisation, retained-profile mutation and weathering remain private here.
    /// </summary>
    public static class ArchBayAuthoringPipeline
    {
        public static void Author(
  IRegionReadSource reads,
  IRegionMutationStore mutations,
  IMaterialAuthoringCatalogue materials,
  int clearSpan,
  int pierHeight,
  int ringThickness,
  int depth,
  int voussoirCount,
  int shoulderWidth,
  int topMargin,
  int faceRecess,
  int plinthHeight,
  int impostHeight,
  int damage,
  int damageScale,
  uint seed,
  int profileJointHalfWidthQ4,
  int profileBevelQ4,
  int profileProjectionQ4,
  int profileDepthQ4,
  byte stoneMaterial,
  ushort surfaceStyle,
  byte weatheringCoating,
  byte weatheringCoverage,
  int writeBudget,
  out IProfileBlockReadSource profiles,
  out int width,
  out int height)
        {
  if (reads == null) throw new ArgumentNullException(nameof(reads));
  if (mutations == null) throw new ArgumentNullException(nameof(mutations));
  if (materials == null) throw new ArgumentNullException(nameof(materials));
  if (writeBudget <= 0) throw new ArgumentOutOfRangeException(nameof(writeBudget));

  var profileStore = new ProfileBlockStore();
  var arch = new ArchFeatureDefinition
  {
      ClearSpan = clearSpan,
      PierHeight = pierHeight,
      RingThickness = ringThickness,
      Depth = depth,
      VoussoirCount = voussoirCount,
      JointRecessDepth = 1,
      ProfileJointHalfWidthQ4 = (byte)profileJointHalfWidthQ4,
      ProfileBevelQ4 = (byte)profileBevelQ4,
      ProfileProjectionQ4 = (byte)profileProjectionQ4,
      ProfileDepthQ4 = (byte)profileDepthQ4,
      StoneMaterial = stoneMaterial,
      PierStyle = surfaceStyle,
      RingStyle = surfaceStyle,
  };
  var bay = new ArchBayFeatureDefinition
  {
      Arch = arch,
      ShoulderWidth = shoulderWidth,
      TopMargin = topMargin,
      FaceRecess = faceRecess,
      PlinthHeight = plinthHeight,
      ImpostHeight = impostHeight,
      Damage = (ArchRuinDamage)damage,
      DamageSeed = seed,
      DamageScale = (byte)damageScale,
  };
  int3 origin = new(-bay.Width / 2, 0, 0);
  using (var primitives = new NativeList<Primitive>(
   bay.Metadata.MaxPrimitives, Allocator.Temp))
  {
      if (!bay.Emit(origin, primitives, profileStore))
throw new InvalidOperationException("Arch parameters did not emit.");
      RasterResult result = PrimitiveRasteriser.Rasterise(
primitives.AsArray(), origin, origin + bay.Metadata.Footprint,
reads, mutations);
      if (result.BudgetExceeded)
throw new InvalidOperationException("Arch exceeded the feature budget.");
  }

  var brush = new VoxelBrush(reads, mutations, materials, writeBudget);
  MasonryWeathering.CoatExposedSurfaces(
      ref brush,
      origin - 2,
      bay.Metadata.Footprint + 4,
      weatheringCoating,
      seed,
      weatheringCoverage,
      dripPasses: 0);

  profiles = profileStore;
  width = bay.Width;
  height = bay.Height;
        }
    }
}
