using NUnit.Framework;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment.Tests
{
    public sealed class CharacterEquipmentControllerTests
    {
        private GameObject characterRoot;
        private GameObject firstPrefab;
        private GameObject secondPrefab;
        private CharacterPartCatalogue catalogue;

        [TearDown]
        public void TearDown()
        {
            DestroyNow(characterRoot);
            DestroyNow(firstPrefab);
            DestroyNow(secondPrefab);

            if (catalogue != null)
            {
                Object.DestroyImmediate(catalogue);
            }
        }

        [Test]
        public void Catalogue_UsesStablePartIds_AndFirstDuplicateWins()
        {
            firstPrefab = new GameObject("StaffA");
            secondPrefab = new GameObject("StaffB");

            CharacterPartDefinition first = new CharacterPartDefinition(
                "staff.sunlit",
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                firstPrefab,
                "RightHand");
            CharacterPartDefinition duplicate = new CharacterPartDefinition(
                "staff.sunlit",
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                secondPrefab,
                "RightHand");

            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            catalogue.Configure(first, null, duplicate);

            Assert.That(catalogue.TryGetPart("staff.sunlit", out CharacterPartDefinition found), Is.True);
            Assert.That(found, Is.SameAs(first));
            Assert.That(catalogue.TryGetPart(string.Empty, out _), Is.False);
            Assert.That(catalogue.TryGetPart("missing.part", out _), Is.False);
        }

        [Test]
        public void Controller_EquipsById_ReplacesSameSlot_AndUnequips()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out _, out _, out Transform rightHand);
            CharacterEquipmentController controller = characterRoot.AddComponent<CharacterEquipmentController>();
            firstPrefab = new GameObject("StaffA");
            secondPrefab = new GameObject("StaffB");

            CharacterPartDefinition first = new CharacterPartDefinition(
                "staff.sunlit",
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                firstPrefab,
                "RightHand");
            CharacterPartDefinition second = new CharacterPartDefinition(
                "staff.oak",
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                secondPrefab,
                "RightHand");

            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            catalogue.Configure(first, second);
            controller.Configure(catalogue, assembler);

            Assert.That(controller.TryEquipById("staff.sunlit", out GameObject firstInstance), Is.True);
            Assert.That(firstInstance.transform.parent, Is.SameAs(rightHand));

            Assert.That(controller.TryEquipById("staff.oak", out GameObject replacement), Is.True);
            Assert.That(firstInstance == null, Is.True);
            Assert.That(replacement.transform.parent, Is.SameAs(rightHand));
            Assert.That(controller.TryGetEquipped("MainHand", out GameObject current), Is.True);
            Assert.That(current, Is.SameAs(replacement));

            Assert.That(controller.TryUnequipSlot("MainHand"), Is.True);
            Assert.That(replacement == null, Is.True);
            Assert.That(controller.TryGetEquipped("MainHand", out _), Is.False);
        }

        [Test]
        public void Controller_EquipsClothingById_AndRebindsToBaseSkeleton()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out Transform armature, out Transform spine, out _);
            CharacterEquipmentController controller = characterRoot.AddComponent<CharacterEquipmentController>();
            firstPrefab = CreateClothingPrefab("SunlitRobe", "Spine");

            CharacterPartDefinition robe = new CharacterPartDefinition(
                "robe.sunlit",
                "Torso",
                CharacterPartKind.Clothing,
                CharacterPartMountMode.RebindSkeleton,
                firstPrefab);

            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            catalogue.Configure(robe);
            controller.Configure(catalogue, assembler);

            Assert.That(controller.TryEquipById("robe.sunlit", out GameObject equipped), Is.True);
            SkinnedMeshRenderer renderer = equipped.GetComponent<SkinnedMeshRenderer>();
            Assert.That(renderer.bones.Length, Is.EqualTo(1));
            Assert.That(renderer.bones[0], Is.SameAs(spine));
            Assert.That(renderer.rootBone, Is.SameAs(armature));
        }

        [Test]
        public void Controller_UnknownPartId_DoesNotDisplaceCurrentEquipment()
        {
            ModularCharacterAssembler assembler = CreateCharacter(out _, out _, out _);
            CharacterEquipmentController controller = characterRoot.AddComponent<CharacterEquipmentController>();
            firstPrefab = new GameObject("StaffA");

            CharacterPartDefinition staff = new CharacterPartDefinition(
                "staff.sunlit",
                "MainHand",
                CharacterPartKind.Weapon,
                CharacterPartMountMode.Socket,
                firstPrefab,
                "RightHand");

            catalogue = ScriptableObject.CreateInstance<CharacterPartCatalogue>();
            catalogue.Configure(staff);
            controller.Configure(catalogue, assembler);

            Assert.That(controller.TryEquipById("staff.sunlit", out GameObject existing), Is.True);
            Assert.That(controller.TryEquipById("missing.part", out _), Is.False);
            Assert.That(controller.TryGetEquipped("MainHand", out GameObject current), Is.True);
            Assert.That(current, Is.SameAs(existing));
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

        private static GameObject CreateClothingPrefab(string name, string sourceBoneName)
        {
            GameObject prefab = new GameObject(name);
            GameObject sourceRootObject = new GameObject("Armature");
            sourceRootObject.transform.SetParent(prefab.transform, false);

            GameObject sourceBoneObject = new GameObject(sourceBoneName);
            sourceBoneObject.transform.SetParent(sourceRootObject.transform, false);

            SkinnedMeshRenderer renderer = prefab.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = new[] { sourceBoneObject.transform };
            renderer.rootBone = sourceRootObject.transform;
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
