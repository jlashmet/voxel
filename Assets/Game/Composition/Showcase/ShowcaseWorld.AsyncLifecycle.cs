using System;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Cancels and joins world-owned background authoring before presentation clears the
        /// renderer binding or disposes Storage. The showcase scene calls this from OnDisable so
        /// one world's global render teardown cannot accidentally retire another world's castle
        /// worker during rapid PlayMode scene transitions.
        /// </summary>
        public void StopBackgroundWork()
        {
            if (_castleBuild is IDisposable disposable)
                disposable.Dispose();
            _castleBuild = null;
        }
    }
}
