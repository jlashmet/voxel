namespace VoxelEngine.Showcase
{
    /// <summary>
    /// The scripted-camera surface a showcase scene has to expose to be measurable in a player.
    ///
    /// <see cref="ShowcasePlayerHarness"/> used to reach for <see cref="VoxelShowcase"/> by type,
    /// which quietly made "measurable" and "is the original showcase scene" the same thing: the
    /// harness installed nothing at all in any other scene, so a player built from one produced no
    /// frame log, no screenshots, and never quit. Every scene that wants a number implements this
    /// instead.
    ///
    /// The three modes are not interchangeable, and a driver that stubs one out is not measurable
    /// for the defect that mode exists to catch. Walking measures streaming; receding crosses LOD
    /// ring boundaries, which a circular walk never does because it holds distance roughly
    /// constant; surveying looks down from above, which is the only vantage that shows a hole in
    /// terrain, since at eye level the near ground hides everything a missing chunk would expose.
    /// </summary>
    public interface IShowcaseMeasurementDriver
    {
        /// <summary>Walks a scripted loop through synthetic input, not a transform teleport.</summary>
        bool AutoWalk { get; set; }

        /// <summary>Holds a high vantage over the landmark, turning, aimed down at the ground.</summary>
        bool AutoSurvey { get; set; }

        /// <summary>Flies straight back from the landmark, keeping it centred in frame.</summary>
        bool AutoRecede { get; set; }

        float SurveyHeightMetres { get; set; }
        float SurveySpinDegreesPerSecond { get; set; }
        float RecedeSpeedMetresPerSecond { get; set; }
        float RecedeMaxDistanceMetres { get; set; }

        /// <summary>Metres from the camera to the landmark this world is built around.</summary>
        float DistanceToLandmarkMetres { get; }

        /// <summary>
        /// Far-field state for the frame log. The far mesh lifts its vertices over built content,
        /// so a hole that fails to open draws a smooth proxy on top of the voxel building it stands
        /// in for — which reads as a LOD bug and is not one.
        /// </summary>
        string DescribeFarTerrain();
    }
}
