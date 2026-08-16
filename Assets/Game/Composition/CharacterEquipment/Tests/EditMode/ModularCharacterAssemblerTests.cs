using NUnit.Framework;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Tests
{
    public sealed class ModularCharacterAssemblerTests
    {
        private GameObject characterRoot;
        private GameObject firstPrefab;
        private GameObject secondPrefab;

        [TearDown]
        public void TearDown()
        {
            DestroyNow(characterRoot);
            DestroyNow(firstPrefab);
            DestroyNow(secondPrefab);
        }

        [Test]
        public void SocketWeapon_AttachesToExactSocket_ReplacesSlot_AndDisablesAnimator()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out Transform armature, out _, out Transform rightHand);
            firstPrefab = new GameObject("StaffA");
            firstPrefab.AddComponent<Animator>();
            secondPrefab = new GameObject("StaffB");

            CharacterPartDefinition firstDefinition = new CharacterPartDefinition(
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                firstPrefab,
                "RightHand");

            Assert.That(assembler.TryEquip(firstDefinition, out GameObject firstInstance), Is.True);
            Assert.That(firstInstance.transform.parent, Is.SameAs(rightHand));
            Assert.That(firstInstance.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(firstInstance.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(firstInstance.GetComponent<Animator>().enabled, Is.False);

            CharacterPartDefinition replacementDefinition = new CharacterPartDefinition(
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                secondPrefab,
                "RightHand");

            Assert.That(assembler.TryEquip(replacementDefinition, out GameObject replacement), Is.True);
            Assert.That(firstInstance == null, Is.True);
            Assert.That(replacement.transform.parent, Is.SameAs(rightHand));
            Assert.That(assembler.TryGetEquipped("MainHand", out GameObject current), Is.True);
            Assert.That(current, Is.SameAs(replacement));
            Assert.That(armature, Is.Not.Null);
        }

        [Test]
        public void SocketWeapon_AppliesDescriptorLocalTransform()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out _, out _, out Transform rightHand);
            firstPrefab = new GameObject("OffsetStaff");
            Vector3 position = new Vector3(0.12f, -0.04f, 0.31f);
            Vector3 euler = new Vector3(8f, 22f, -15f);
            Vector3 scale = new Vector3(1.1f, 0.9f, 1.05f);

            CharacterPartDefinition definition = new CharacterPartDefinition(
                "staff.offset",
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                firstPrefab,
                "RightHand",
                position,
                euler,
                scale);

            Assert.That(assembler.TryEquip(definition, out GameObject instance), Is.True);
            Assert.That(instance.transform.parent, Is.SameAs(rightHand));
            Assert.That(instance.transform.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(instance.transform.localRotation, Quaternion.Euler(euler)), Is.LessThan(0.001f));
            Assert.That(instance.transform.localScale, Is.EqualTo(scale));
        }

        [Test]
        public void Clothing_RebindsEveryBoneToBaseSkeleton_WithoutMutatingBaseRenderer()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out Transform armature, out Transform spine, out _);
            SkinnedMeshRenderer baseRenderer = characterRoot.AddComponent<SkinnedMeshRenderer>();
            baseRenderer.bones = new[] { spine };
            baseRenderer.rootBone = armature;
            Transform[] originalBaseBones = baseRenderer.bones;
            Transform originalBaseRoot = baseRenderer.rootBone;

            firstPrefab = CreateClothingPrefab("Robe", "Spine", out _, out _);
            firstPrefab.AddComponent<Animator>();
            CharacterPartDefinition definition = new CharacterPartDefinition(
                "Torso",
                CharacterPartKind.Clothing,
                CharacterPartMountMode.RebindSkeleton,
                firstPrefab);

            Assert.That(assembler.TryEquip(definition, out GameObject equipped), Is.True);
            SkinnedMeshRenderer equippedRenderer = equipped.GetComponent<SkinnedMeshRenderer>();
            Assert.That(equippedRenderer.bones.Length, Is.EqualTo(1));
            Assert.That(equippedRenderer.bones[0], Is.SameAs(spine));
            Assert.That(equippedRenderer.rootBone, Is.SameAs(armature));
            Assert.That(equipped.GetComponent<Animator>().enabled, Is.False);

            Assert.That(baseRenderer.bones, Is.EqualTo(originalBaseBones));
            Assert.That(baseRenderer.rootBone, Is.SameAs(originalBaseRoot));
            Assert.That(baseRenderer.enabled, Is.True);
            Assert.That(characterRoot.activeSelf, Is.True);
        }

        [Test]
        public void MissingClothingBone_DoesNotDisplaceCurrentSlotItem()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out _, out _, out _);
            firstPrefab = CreateClothingPrefab("GoodRobe", "Spine", out _, out _);
            CharacterPartDefinition goodDefinition = new CharacterPartDefinition(
                "Torso",
                CharacterPartKind.Clothing,
                CharacterPartMountMode.RebindSkeleton,
                firstPrefab);

            Assert.That(assembler.TryEquip(goodDefinition, out GameObject existing), Is.True);

            secondPrefab = CreateClothingPrefab("BadRobe", "MissingBone", out _, out _);
            CharacterPartDefinition badDefinition = new CharacterPartDefinition(
                "Torso",
                CharacterPartKind.Clothing,
                CharacterPartMountMode.RebindSkeleton,
                secondPrefab);

            Assert.That(assembler.TryEquip(badDefinition, out _), Is.False);
            Assert.That(assembler.TryGetEquipped("Torso", out GameObject current), Is.True);
            Assert.That(current, Is.SameAs(existing));
            Assert.That(existing.activeSelf, Is.True);
        }

        [Test]
        public void MissingSocket_DoesNotDisplaceCurrentSlotItem()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out _, out _, out _);
            firstPrefab = new GameObject("StaffA");
            secondPrefab = new GameObject("StaffB");

            CharacterPartDefinition goodDefinition = new CharacterPartDefinition(
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                firstPrefab,
                "RightHand");
            Assert.That(assembler.TryEquip(goodDefinition, out GameObject existing), Is.True);

            CharacterPartDefinition badDefinition = new CharacterPartDefinition(
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                secondPrefab,
                "NoSuchSocket");

            Assert.That(assembler.TryEquip(badDefinition, out _), Is.False);
            Assert.That(assembler.TryGetEquipped("MainHand", out GameObject current), Is.True);
            Assert.That(current, Is.SameAs(existing));
            Assert.That(existing.activeSelf, Is.True);
        }

        [Test]
        public void AmbiguousBaseBoneName_RejectsMountInsteadOfChoosingArbitrarily()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out Transform armature, out _, out _);
            GameObject duplicate = new GameObject("Spine");
            duplicate.transform.SetParent(armature, false);
            assembler.RebuildSkeletonIndex();

            firstPrefab = CreateClothingPrefab("Robe", "Spine", out _, out _);
            CharacterPartDefinition definition = new CharacterPartDefinition(
                "Torso",
                CharacterPartKind.Clothing,
                CharacterPartMountMode.RebindSkeleton,
                firstPrefab);

            Assert.That(assembler.TryEquip(definition, out _), Is.False);
            Assert.That(assembler.TryGetEquipped("Torso", out _), Is.False);
        }

        private ModularCharacterAssembler CreateCharacter(
            out Transform armature,
            out Transform spine,
            out Transform rightHand)
        {
            characterRoot = new GameObject("Character");
            GameObject armatureObject = new GameObject("Armature");
            armatureObject.transform.SetParent(characterRoot.transform, false);
            armature = armatureObject.transform;

            GameObject spineObject = new GameObject("Spine");
            spineObject.transform.SetParent(armature, false);
            spine = spineObject.transform;

            GameObject rightHandObject = new GameObject("RightHand");
            rightHandObject.transform.SetParent(spine, false);
            rightHand = rightHandObject.transform;

            ModularCharacterAssembler assembler = characterRoot.AddComponent<ModularCharacterAssembler>();
            assembler.SkeletonRoot = armature;
            return assembler;
        }

        private static GameObject CreateClothingPrefab(
            string name,
            string sourceBoneName,
            out Transform sourceRoot,
            out Transform sourceBone)
        {
            GameObject prefab = new GameObject(name);
            GameObject sourceRootObject = new GameObject("Armature");
            sourceRootObject.transform.SetParent(prefab.transform, false);
            sourceRoot = sourceRootObject.transform;

            GameObject sourceBoneObject = new GameObject(sourceBoneName);
            sourceBoneObject.transform.SetParent(sourceRoot, false);
            sourceBone = sourceBoneObject.transform;

            SkinnedMeshRenderer renderer = prefab.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { sourceBone };
            renderer.rootBone = sourceRoot;
            return prefab;
        }

        private static void DestroyNow(GameObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
