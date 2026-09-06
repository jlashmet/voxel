using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using Object = UnityEngine.Object;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class PropShowcaseResourceSnapshotTests
    {
        [UnityTest]
        public IEnumerator SnapshotCountsInactiveOwnedResourcesUntilDestroyActuallyRuns()
        {
            var root = new GameObject("Nonvisual resource accounting fixture");
            try
            {
                PropShowcaseResourceSnapshot baseline = PropShowcaseResourceSnapshot.Capture(root);
                var child = new GameObject("Owned inactive resource");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshRenderer>();
                child.AddComponent<BoxCollider>();
                child.AddComponent<Light>();
                child.SetActive(false);
                PropShowcaseResourceSnapshot live = PropShowcaseResourceSnapshot.Capture(root);
                Assert.That(live.Transforms, Is.EqualTo(baseline.Transforms + 1));
                Assert.That(live.Renderers, Is.EqualTo(baseline.Renderers + 1));
                Assert.That(live.Colliders, Is.EqualTo(baseline.Colliders + 1));
                Assert.That(live.Lights, Is.EqualTo(baseline.Lights + 1));
                Assert.That(live.HasSameOwnedObjects(in baseline), Is.False);

                Object.Destroy(child);
                PropShowcaseResourceSnapshot pending = PropShowcaseResourceSnapshot.Capture(root);
                Assert.That(pending.HasSameOwnedObjects(in baseline), Is.False,
                    "Clearing an owner's dictionary is not evidence of deferred object retirement.");
                yield return null;
                yield return null;
                PropShowcaseResourceSnapshot retired = PropShowcaseResourceSnapshot.Capture(root);
                Assert.That(retired.HasSameOwnedObjects(in baseline), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator OrphanedMeshIsVisibleEvenWhenOwnedObjectCountsMatch()
        {
            var root = new GameObject("Nonvisual native mesh accounting fixture");
            Mesh mesh = null;
            try
            {
                PropShowcaseResourceSnapshot baseline = PropShowcaseResourceSnapshot.Capture(root);
                mesh = new Mesh { name = "Nonvisual orphaned mesh fixture" };
                PropShowcaseResourceSnapshot allocated = PropShowcaseResourceSnapshot.Capture(root);
                Assert.That(allocated.HasSameOwnedObjects(in baseline), Is.True);
                Assert.That(allocated.GlobalMeshes, Is.EqualTo(baseline.GlobalMeshes + 1),
                    "A native mesh need not belong to a GameObject to remain allocated.");
                Object.Destroy(mesh);
                yield return null;
                yield return null;
                PropShowcaseResourceSnapshot retired = PropShowcaseResourceSnapshot.Capture(root);
                Assert.That(retired.GlobalMeshes, Is.EqualTo(baseline.GlobalMeshes));
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SnapshotRejectsMissingOwnerInsteadOfReportingEmptySuccess()
        {
            Assert.Throws<ArgumentNullException>(() => PropShowcaseResourceSnapshot.Capture(null));
        }
    }
}
