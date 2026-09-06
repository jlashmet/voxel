using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Pins the user-specified visual input and preserves its exact bytes beside CI evidence.
    /// This checks reference provenance only; it does not assert rendered visual acceptance.
    /// </summary>
    public sealed class NewHouseReferenceSourceTests
    {
        private const string ReferencePath =
            "Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png";
        private const string ReferenceBlob = "6d87b08d4c7c9bddc1705c0f34343aa79bc18423";

        // Fully qualify NUnit: this assembly currently has a namespace-local inert TestAttribute.
        [NUnit.Framework.Test]
        public void ReferenceInput_MatchesPinnedBlob_AndPreservesOriginalForReview()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string inputPath = Path.Combine(projectRoot, ReferencePath);
            Assert.That(File.Exists(inputPath), Is.True, "The authoritative reference must be in the checkout.");
            byte[] bytes = File.ReadAllBytes(inputPath);
            Assert.That(bytes.Length, Is.GreaterThan(8));
            byte[] pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < pngSignature.Length; i++)
                Assert.That(bytes[i], Is.EqualTo(pngSignature[i]), "Reference must remain a PNG.");

            string blob = GitBlobHash(bytes);
            Assert.That(blob, Is.EqualTo(ReferenceBlob),
                "The visual target changed. Confirm the new reference with the user; never silently substitute it.");

            // Reuse the existing targeted-run artifact tree, without changing workflows, scene
            // targets or the rendered scene. The input is deliberately NOT under Screenshots.
            if (!string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true",
                    StringComparison.OrdinalIgnoreCase))
                return;

            string directory = Path.Combine(projectRoot, "Artifacts", "SingleTest", "ReferenceInputs", "NewHouse");
            Directory.CreateDirectory(directory);
            string outputPath = Path.Combine(directory, Path.GetFileName(ReferencePath));
            File.WriteAllBytes(outputPath, bytes);
            Assert.That(GitBlobHash(File.ReadAllBytes(outputPath)), Is.EqualTo(ReferenceBlob));
            var manifest = new ReferenceManifest
            {
                kind = "reference-input-not-player-render",
                repositoryPath = ReferencePath,
                gitBlob = blob,
                byteLength = bytes.Length,
                ciRunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? "",
                ciRequestSha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "",
            };
            File.WriteAllText(Path.Combine(directory, "provenance.json"), JsonUtility.ToJson(manifest, true) + "\n");
            TestContext.WriteLine("NEW_HOUSE_REFERENCE_INPUT path=" + ReferencePath + " blob=" + blob);
        }

        private static string GitBlobHash(byte[] bytes)
        {
            byte[] header = Encoding.ASCII.GetBytes("blob " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\0");
            byte[] objectBytes = new byte[header.Length + bytes.Length];
            Buffer.BlockCopy(header, 0, objectBytes, 0, header.Length);
            Buffer.BlockCopy(bytes, 0, objectBytes, header.Length, bytes.Length);
            using SHA1 sha1 = SHA1.Create();
            return BitConverter.ToString(sha1.ComputeHash(objectBytes)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        private sealed class ReferenceManifest
        {
            public string kind;
            public string repositoryPath;
            public string gitBlob;
            public int byteLength;
            public string ciRunId;
            public string ciRequestSha;
        }
    }
}
