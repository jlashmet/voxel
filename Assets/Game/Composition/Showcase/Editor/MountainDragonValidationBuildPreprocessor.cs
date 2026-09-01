using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace VoxelEngine.Showcase.Editor
{
    /// <summary>
    /// Supplies the exact reconstructed source mesh only to the module-local Dragon validation
    /// player. Ordinary builds never package this presentation input, and runtime world authority
    /// remains the checked-in sparse voxel bake.
    /// </summary>
    public sealed class MountainDragonValidationBuildPreprocessor :
        IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string ValidationScenePath =
            "Assets/Game/Composition/Showcase/Validation/MountainDragonVoxelization/MountainDragonVoxelValidation.unity";
        public const string ValidationResourceAssetPath =
            "Assets/Resources/VoxelShowcase/MountainDragonSource/mountain_dragon_clean.obj";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsValidationBuild()) return;

            MountainDragonSourceArchive.ReconstructImportedAsset();
            string directory = Path.GetDirectoryName(ValidationResourceAssetPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Mountain Dragon validation resource directory is invalid.");

            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadMainAssetAtPath(ValidationResourceAssetPath) != null)
                AssetDatabase.DeleteAsset(ValidationResourceAssetPath);
            if (!AssetDatabase.CopyAsset(
                    MountainDragonSourceArchive.GeneratedAssetPath,
                    ValidationResourceAssetPath))
            {
                throw new InvalidOperationException(
                    "Failed to stage the exact Mountain Dragon source mesh for validation build.");
            }
            AssetDatabase.ImportAsset(
                ValidationResourceAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsValidationBuild()) return;
            AssetDatabase.DeleteAsset(ValidationResourceAssetPath);
        }

        private static bool IsValidationBuild()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], "-voxelScene", StringComparison.Ordinal) &&
                    string.Equals(args[i + 1], ValidationScenePath, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
