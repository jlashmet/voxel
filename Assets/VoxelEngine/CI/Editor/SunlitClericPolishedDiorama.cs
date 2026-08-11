using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Purpose-built visual diorama for matching the generated "Sunlit Cleric by the Waterfall"
    /// reference. This is lookdev, not a production character system: it deliberately builds a
    /// stylised smooth character over a chunky, block-derived fantasy ruin so we can judge the
    /// final art direction inside the actual Unity project before committing to asset production.
    /// </summary>
    internal static class SunlitClericPolishedDiorama
    {
        private sealed class Palette
        {
            public Material Skin;
            public Material SkinShadow;
            public Material Hair;
            public Material HairShadow;
            public Material White;
            public Material WhiteShadow;
            public Material Blue;
            public Material Gold;
            public Material Leather;
            public Material LeatherDark;
            public Material EyeWhite;
            public Material Iris;
            public Material Pupil;
            public Material Mouth;
            public Material Stone;
            public Material StoneLight;
            public Material StoneDark;
            public Material Moss;
            public Material MossLight;
            public Material MossDark;
            public Material Trunk;
            public Material Leaves;
            public Material LeavesLight;
            public Material Water;
            public Material WaterLight;
            public Material Cloud;
            public Material Sky;
            public Material Castle;
            public Material CastleRoof;
            public Material CastleWindow;
            public Material FlowerWhite;
            public Material FlowerPink;
            public Material FlowerBlue;
            public Material FlowerYellow;
            public Material FlowerStem;
            public Material Lantern;
        }

        private static GameObject _root;
        private static readonly List<UnityEngine.Object> Owned = new();
        private static Mesh _beveledCube;
        private static int _stoneVariant;

        public static void Build(Camera camera, Vector3 origin)
        {
            if (_root != null) return;

            _root = new GameObject("Sunlit Cleric Polished Diorama") { hideFlags = HideFlags.DontSave };
            Owned.Add(_root);

            Palette p = BuildPalette();
            ConfigureLighting(origin);
            ConfigureCamera(camera, origin);
            CreateBackdrop(origin, p);
            CreateEnvironment(origin, p);
            CreateMadeline(origin, p);
        }

        // ------------------------------------------------------------------
        // Character
        // ------------------------------------------------------------------

        private static void CreateMadeline(Vector3 origin, Palette p)
        {
            var hero = new GameObject("Madeline Polished Lookdev") { hideFlags = HideFlags.DontSave };
            hero.transform.SetParent(_root.transform, false);
            hero.transform.position = origin;
            hero.transform.rotation = Quaternion.Euler(0f, -3f, 0f);
            Owned.Add(hero);

            // Brown underskirt and boots give the white silhouette a readable base.
            Mesh underskirt = BuildFrustum(0.31f, 0.255f, 0.28f, 28, true);
            Owned.Add(underskirt);
            MeshObject("Brown Underskirt", underskirt, p.LeatherDark, hero.transform,
                new Vector3(0f, 0.19f, 0.045f));

            Mesh skirt = BuildFrustum(0.435f, 0.215f, 0.92f, 32, true);
            Owned.Add(skirt);
            MeshObject("White Cleric Skirt", skirt, p.White, hero.transform,
                new Vector3(0f, 0.55f, 0f));

            // A second front panel breaks the cone into layered cloth and carries the gold motif.
            Mesh apron = BuildTaperedPanel(
                new Vector3(-0.17f, 0.94f, -0.235f), new Vector3(0.17f, 0.94f, -0.235f),
                new Vector3(0.28f, 0.17f, -0.355f), new Vector3(-0.28f, 0.17f, -0.355f));
            Owned.Add(apron);
            MeshObject("Front Cleric Apron", apron, p.WhiteShadow, hero.transform, Vector3.zero);

            CapsuleLocal("Apron Gold Left", new Vector3(-0.165f, 0.92f, -0.37f),
                new Vector3(-0.265f, 0.18f, -0.39f), 0.012f, p.Gold, hero.transform);
            CapsuleLocal("Apron Gold Right", new Vector3(0.165f, 0.92f, -0.37f),
                new Vector3(0.265f, 0.18f, -0.39f), 0.012f, p.Gold, hero.transform);
            CapsuleLocal("Apron Gold Center", new Vector3(0f, 0.91f, -0.385f),
                new Vector3(0f, 0.33f, -0.405f), 0.012f, p.Gold, hero.transform);

            // Bodice and layered shoulder mantle.
            GameObject torso = Primitive(PrimitiveType.Sphere, "White Cleric Bodice", p.White, hero.transform);
            torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            torso.transform.localScale = new Vector3(0.38f, 0.37f, 0.265f);

            GameObject mantle = Primitive(PrimitiveType.Sphere, "White Shoulder Mantle", p.White, hero.transform);
            mantle.transform.localPosition = new Vector3(0f, 1.25f, 0.02f);
            mantle.transform.localScale = new Vector3(0.49f, 0.13f, 0.29f);

            GameObject collar = Primitive(PrimitiveType.Sphere, "Light Blue Cleric Collar", p.Blue, hero.transform);
            collar.transform.localPosition = new Vector3(0f, 1.27f, -0.035f);
            collar.transform.localScale = new Vector3(0.39f, 0.090f, 0.255f);

            GameObject belt = Primitive(PrimitiveType.Cylinder, "Leather Belt", p.Leather, hero.transform);
            belt.transform.localPosition = new Vector3(0f, 0.91f, 0f);
            belt.transform.localScale = new Vector3(0.255f, 0.033f, 0.255f);
            GameObject buckle = Primitive(PrimitiveType.Cube, "Gold Belt Buckle", p.Gold, hero.transform);
            buckle.transform.localPosition = new Vector3(0f, 0.91f, -0.275f);
            buckle.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            buckle.transform.localScale = new Vector3(0.105f, 0.105f, 0.026f);

            // Blue interior cloak panels with white/gold outer borders. Wide low panels create the
            // same elegant wing-like silhouette as the painted reference without a full cloth rig.
            CreateCapePanel(hero.transform, -1f, p.Blue, p.White, p.Gold);
            CreateCapePanel(hero.transform, 1f, p.Blue, p.White, p.Gold);

            // Staff-side arm (viewer-left) bends inward to grip the shaft.
            CapsuleLocal("Left Upper Sleeve", new Vector3(-0.235f, 1.16f, -0.02f),
                new Vector3(-0.34f, 1.00f, -0.12f), 0.082f, p.White, hero.transform);
            CapsuleLocal("Left Lower Sleeve", new Vector3(-0.34f, 1.00f, -0.12f),
                new Vector3(-0.455f, 0.89f, -0.18f), 0.070f, p.White, hero.transform);
            GameObject leftHand = Primitive(PrimitiveType.Sphere, "Staff Hand", p.Skin, hero.transform);
            leftHand.transform.localPosition = new Vector3(-0.468f, 0.87f, -0.19f);
            leftHand.transform.localScale = new Vector3(0.095f, 0.115f, 0.090f);

            // Free hand falls naturally at her side.
            CapsuleLocal("Right Upper Sleeve", new Vector3(0.235f, 1.16f, -0.02f),
                new Vector3(0.315f, 0.98f, -0.11f), 0.082f, p.White, hero.transform);
            CapsuleLocal("Right Lower Sleeve", new Vector3(0.315f, 0.98f, -0.11f),
                new Vector3(0.39f, 0.80f, -0.17f), 0.067f, p.White, hero.transform);
            GameObject rightHand = Primitive(PrimitiveType.Sphere, "Relaxed Hand", p.Skin, hero.transform);
            rightHand.transform.localPosition = new Vector3(0.405f, 0.765f, -0.18f);
            rightHand.transform.localScale = new Vector3(0.090f, 0.115f, 0.085f);

            // Small brown boots visible beneath the hem.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject boot = Primitive(PrimitiveType.Sphere, side < 0 ? "Left Boot" : "Right Boot", p.LeatherDark, hero.transform);
                boot.transform.localPosition = new Vector3(side * 0.15f, 0.045f, -0.16f);
                boot.transform.localScale = new Vector3(0.145f, 0.105f, 0.275f);
            }

            // Hip book/pouch with a gold cross.
            GameObject book = Primitive(PrimitiveType.Cube, "Cleric Book", p.Leather, hero.transform);
            book.transform.localPosition = new Vector3(0.31f, 0.76f, -0.245f);
            book.transform.localRotation = Quaternion.Euler(0f, -6f, -7f);
            book.transform.localScale = new Vector3(0.17f, 0.26f, 0.070f);
            GameObject bookVertical = Primitive(PrimitiveType.Cube, "Book Cross Vertical", p.Gold, hero.transform);
            bookVertical.transform.localPosition = new Vector3(0.31f, 0.76f, -0.320f);
            bookVertical.transform.localScale = new Vector3(0.020f, 0.125f, 0.012f);
            GameObject bookHorizontal = Primitive(PrimitiveType.Cube, "Book Cross Horizontal", p.Gold, hero.transform);
            bookHorizontal.transform.localPosition = new Vector3(0.31f, 0.76f, -0.322f);
            bookHorizontal.transform.localScale = new Vector3(0.085f, 0.018f, 0.012f);

            CreateHeadAndHair(hero.transform, p);
            CreateStaff(hero.transform, p);
            CreateRobeOrnament(hero.transform, p);
        }

        private static void CreateHeadAndHair(Transform hero, Palette p)
        {
            // Hair bulk is behind the face, then flat ribbon strands and bangs break the spherical
            // silhouette into the layered wavy hairstyle seen in the reference.
            GameObject hairBack = Primitive(PrimitiveType.Sphere, "Blonde Hair Mass", p.HairShadow, hero);
            hairBack.transform.localPosition = new Vector3(0f, 1.62f, 0.080f);
            hairBack.transform.localScale = new Vector3(0.345f, 0.390f, 0.285f);

            GameObject face = Primitive(PrimitiveType.Sphere, "Madeline Face", p.Skin, hero);
            face.transform.localPosition = new Vector3(0f, 1.60f, -0.035f);
            face.transform.localScale = new Vector3(0.275f, 0.315f, 0.235f);

            GameObject cap = Primitive(PrimitiveType.Sphere, "Blonde Hair Crown", p.Hair, hero);
            cap.transform.localPosition = new Vector3(0f, 1.765f, -0.015f);
            cap.transform.localScale = new Vector3(0.305f, 0.195f, 0.265f);

            // Long wavy side/rear locks.
            Vector3[][] locks =
            {
                new[] { new Vector3(-0.24f,1.77f,-0.03f), new Vector3(-0.34f,1.55f,-0.02f), new Vector3(-0.31f,1.29f,0.02f), new Vector3(-0.43f,1.05f,0.06f) },
                new[] { new Vector3(-0.18f,1.79f,0.05f), new Vector3(-0.29f,1.58f,0.10f), new Vector3(-0.25f,1.33f,0.14f), new Vector3(-0.34f,1.10f,0.16f) },
                new[] { new Vector3(-0.10f,1.82f,0.13f), new Vector3(-0.19f,1.58f,0.19f), new Vector3(-0.16f,1.31f,0.21f), new Vector3(-0.23f,1.02f,0.22f) },
                new[] { new Vector3(0.10f,1.82f,0.13f), new Vector3(0.19f,1.58f,0.19f), new Vector3(0.16f,1.31f,0.21f), new Vector3(0.23f,1.02f,0.22f) },
                new[] { new Vector3(0.18f,1.79f,0.05f), new Vector3(0.29f,1.58f,0.10f), new Vector3(0.25f,1.33f,0.14f), new Vector3(0.34f,1.10f,0.16f) },
                new[] { new Vector3(0.24f,1.77f,-0.03f), new Vector3(0.34f,1.55f,-0.02f), new Vector3(0.31f,1.29f,0.02f), new Vector3(0.43f,1.05f,0.06f) },
            };
            for (int i = 0; i < locks.Length; i++)
            {
                Mesh ribbon = BuildRibbon(locks[i], 0.075f + (i % 2) * 0.010f);
                Owned.Add(ribbon);
                MeshObject($"Wavy Blonde Lock {i}", ribbon, i % 3 == 1 ? p.HairShadow : p.Hair, hero, Vector3.zero);
            }

            // Front bangs.
            Vector3[][] bangs =
            {
                new[] { new Vector3(-0.16f,1.79f,-0.235f), new Vector3(-0.20f,1.70f,-0.262f), new Vector3(-0.16f,1.62f,-0.270f) },
                new[] { new Vector3(-0.08f,1.82f,-0.245f), new Vector3(-0.10f,1.72f,-0.276f), new Vector3(-0.07f,1.64f,-0.282f) },
                new[] { new Vector3(0.00f,1.83f,-0.250f), new Vector3(0.00f,1.72f,-0.282f), new Vector3(0.02f,1.64f,-0.286f) },
                new[] { new Vector3(0.08f,1.82f,-0.245f), new Vector3(0.10f,1.72f,-0.276f), new Vector3(0.07f,1.64f,-0.282f) },
                new[] { new Vector3(0.16f,1.79f,-0.235f), new Vector3(0.20f,1.70f,-0.262f), new Vector3(0.16f,1.62f,-0.270f) },
            };
            for (int i = 0; i < bangs.Length; i++)
            {
                Mesh ribbon = BuildRibbon(bangs[i], 0.050f);
                Owned.Add(ribbon);
                MeshObject($"Blonde Bang {i}", ribbon, p.Hair, hero, Vector3.zero);
            }

            CreateEye(hero, -0.082f, p);
            CreateEye(hero, 0.082f, p);

            CapsuleLocal("Left Eyebrow", new Vector3(-0.135f, 1.675f, -0.270f),
                new Vector3(-0.035f, 1.690f, -0.281f), 0.012f, p.HairShadow, hero);
            CapsuleLocal("Right Eyebrow", new Vector3(0.035f, 1.690f, -0.281f),
                new Vector3(0.135f, 1.675f, -0.270f), 0.012f, p.HairShadow, hero);

            GameObject nose = Primitive(PrimitiveType.Sphere, "Small Nose", p.SkinShadow, hero);
            nose.transform.localPosition = new Vector3(0f, 1.555f, -0.276f);
            nose.transform.localScale = new Vector3(0.025f, 0.030f, 0.020f);

            GameObject mouth = Primitive(PrimitiveType.Sphere, "Friendly Smile", p.Mouth, hero);
            mouth.transform.localPosition = new Vector3(0f, 1.505f, -0.278f);
            mouth.transform.localScale = new Vector3(0.058f, 0.022f, 0.013f);
        }

        private static void CreateEye(Transform hero, float x, Palette p)
        {
            GameObject white = Primitive(PrimitiveType.Sphere, "Anime Eye White", p.EyeWhite, hero);
            white.transform.localPosition = new Vector3(x, 1.610f, -0.267f);
            white.transform.localScale = new Vector3(0.064f, 0.046f, 0.018f);

            GameObject iris = Primitive(PrimitiveType.Sphere, "Brown Iris", p.Iris, hero);
            iris.transform.localPosition = new Vector3(x, 1.608f, -0.282f);
            iris.transform.localScale = new Vector3(0.033f, 0.034f, 0.010f);

            GameObject pupil = Primitive(PrimitiveType.Sphere, "Eye Pupil", p.Pupil, hero);
            pupil.transform.localPosition = new Vector3(x, 1.607f, -0.290f);
            pupil.transform.localScale = new Vector3(0.015f, 0.019f, 0.007f);

            GameObject sparkle = Primitive(PrimitiveType.Sphere, "Eye Highlight", p.EyeWhite, hero);
            sparkle.transform.localPosition = new Vector3(x - 0.010f, 1.621f, -0.296f);
            sparkle.transform.localScale = Vector3.one * 0.008f;
        }

        private static void CreateCapePanel(Transform hero, float side, Material blue, Material white, Material gold)
        {
            Vector3 a = new(side * 0.15f, 1.23f, 0.085f);
            Vector3 b = new(side * 0.34f, 1.08f, 0.105f);
            Vector3 c = new(side * 0.52f, 0.32f, 0.160f);
            Vector3 d = new(side * 0.31f, 0.52f, 0.135f);
            Mesh panel = BuildTaperedPanel(a, b, c, d);
            Owned.Add(panel);
            MeshObject(side < 0 ? "Left Blue Cape" : "Right Blue Cape", panel, blue, hero, Vector3.zero);

            Vector3 outer0 = b + new Vector3(0f, 0f, -0.012f);
            Vector3 outer1 = c + new Vector3(0f, 0f, -0.012f);
            CapsuleLocal(side < 0 ? "Left Cape White Edge" : "Right Cape White Edge",
                outer0, outer1, 0.028f, white, hero);
            CapsuleLocal(side < 0 ? "Left Cape Gold Edge" : "Right Cape Gold Edge",
                outer0 + new Vector3(0f, 0f, -0.020f), outer1 + new Vector3(0f, 0f, -0.020f), 0.009f, gold, hero);
        }

        private static void CreateStaff(Transform hero, Palette p)
        {
            Vector3 bottom = new(-0.475f, 0.10f, -0.205f);
            Vector3 top = new(-0.475f, 1.63f, -0.205f);
            CapsuleLocal("Walnut Staff Shaft", bottom, top, 0.027f, p.LeatherDark, hero);

            Vector3 centre = new(-0.475f, 1.735f, -0.205f);
            const float radius = 0.165f;
            const int segments = 14;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                CapsuleLocal($"Staff Gold Ring {i}",
                    centre + new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f),
                    centre + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f),
                    0.012f, p.Gold, hero);
            }

            CapsuleLocal("Staff Cross Vertical", centre + new Vector3(0f, -0.125f, 0f),
                centre + new Vector3(0f, 0.125f, 0f), 0.012f, p.Gold, hero);
            CapsuleLocal("Staff Cross Horizontal", centre + new Vector3(-0.125f, 0f, 0f),
                centre + new Vector3(0.125f, 0f, 0f), 0.012f, p.Gold, hero);
            CapsuleLocal("Staff Spear Tip", centre + new Vector3(0f, radius, 0f),
                centre + new Vector3(0f, radius + 0.14f, 0f), 0.015f, p.Gold, hero);

            GameObject gem = Primitive(PrimitiveType.Sphere, "Staff Blue Gem", p.Blue, hero);
            gem.transform.localPosition = centre + new Vector3(0f, 0f, -0.020f);
            gem.transform.localScale = Vector3.one * 0.075f;

            // Small cardinal ornaments echo the ornate generated concept without exploding the silhouette.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 dir = new(Mathf.Cos(a), Mathf.Sin(a), 0f);
                CapsuleLocal($"Staff Ornament {i}", centre + dir * (radius - 0.015f),
                    centre + dir * (radius + 0.085f), 0.010f, p.Gold, hero);
            }
        }

        private static void CreateRobeOrnament(Transform hero, Palette p)
        {
            Vector3 centre = new(0f, 0.405f, -0.414f);
            const float r = 0.095f;
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI * 2f / 8f;
                float a1 = (i + 1) * Mathf.PI * 2f / 8f;
                CapsuleLocal($"Robe Sigil {i}", centre + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f),
                    centre + new Vector3(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r, 0f), 0.008f, p.Gold, hero);
            }
            CapsuleLocal("Robe Sigil Vertical", centre + new Vector3(0f, -r, 0f),
                centre + new Vector3(0f, r, 0f), 0.008f, p.Gold, hero);
            CapsuleLocal("Robe Sigil Horizontal", centre + new Vector3(-r, 0f, 0f),
                centre + new Vector3(r, 0f, 0f), 0.008f, p.Gold, hero);
        }

        // ------------------------------------------------------------------
        // Environment
        // ------------------------------------------------------------------

        private static void CreateBackdrop(Vector3 o, Palette p)
        {
            GameObject sky = Primitive(PrimitiveType.Quad, "Painted Blue Sky", p.Sky, _root.transform);
            sky.transform.position = o + new Vector3(0f, 3.9f, 18f);
            sky.transform.localScale = new Vector3(30f, 22f, 1f);

            CreateCloud(o + new Vector3(-2.8f, 3.25f, 13.8f), 1.05f, p.Cloud);
            CreateCloud(o + new Vector3(0.30f, 3.75f, 15.0f), 1.20f, p.Cloud);
            CreateCloud(o + new Vector3(3.0f, 3.20f, 13.5f), 1.00f, p.Cloud);
        }

        private static void CreateEnvironment(Vector3 o, Palette p)
        {
            CreateHeroIsland(o, p);
            CreateLeftRuins(o, p);
            CreateWaterfallGarden(o, p);
            CreateCastle(o, p);
            CreateForegroundDetails(o, p);
        }

        private static void CreateHeroIsland(Vector3 o, Palette p)
        {
            // Chunky stone substrate made from many large beveled pieces, then broad moss pillows.
            for (int z = -1; z <= 2; z++)
            for (int x = -3; x <= 3; x++)
            {
                float nx = x / 3.2f;
                float nz = (z - 0.4f) / 2.5f;
                if (nx * nx + nz * nz > 1.10f) continue;

                float jitterX = Hash(x, z, 2) * 0.09f - 0.045f;
                float jitterZ = Hash(x, z, 3) * 0.09f - 0.045f;
                float y = -0.42f + (Hash(x, z, 7) - 0.5f) * 0.08f;
                Material stone = StoneVariant(p, x + z * 7);
                StoneBlock(o + new Vector3(x * 0.58f + jitterX, y, z * 0.58f + 0.62f + jitterZ),
                    new Vector3(0.68f, 0.48f + Hash(x, z, 9) * 0.13f, 0.68f), stone,
                    Quaternion.Euler(0f, (Hash(x, z, 11) - 0.5f) * 10f, (Hash(x, z, 13) - 0.5f) * 4f));
            }

            CreateMossPatch(o + new Vector3(-0.95f, -0.12f, 0.20f), new Vector3(1.45f, 0.22f, 1.05f), p.Moss);
            CreateMossPatch(o + new Vector3(0.10f, -0.10f, 0.55f), new Vector3(1.80f, 0.24f, 1.20f), p.MossLight);
            CreateMossPatch(o + new Vector3(1.25f, -0.12f, 0.70f), new Vector3(1.25f, 0.21f, 0.95f), p.Moss);
            CreateMossPatch(o + new Vector3(-0.20f, -0.09f, 1.45f), new Vector3(1.65f, 0.22f, 0.95f), p.MossDark);

            // Warm stepping-stone path beneath the character.
            for (int i = 0; i < 7; i++)
            {
                float x = (i % 2 == 0 ? -0.08f : 0.10f) + (Hash(i, 4, 2) - 0.5f) * 0.08f;
                float z = -0.38f + i * 0.34f;
                StoneBlock(o + new Vector3(x, 0.015f, z),
                    new Vector3(0.42f + Hash(i, 5, 1) * 0.10f, 0.080f, 0.30f + Hash(i, 6, 1) * 0.10f),
                    i % 3 == 0 ? p.StoneLight : p.Stone,
                    Quaternion.Euler(0f, (Hash(i, 7, 1) - 0.5f) * 14f, 0f));
            }
        }

        private static void CreateLeftRuins(Vector3 o, Palette p)
        {
            CreateArch(o + new Vector3(-1.72f, 0.02f, 3.10f), 0.92f, 2.55f, p);
            CreateArch(o + new Vector3(-1.50f, -0.08f, 5.70f), 0.50f, 1.45f, p);
            CreateTree(o + new Vector3(-2.55f, -0.14f, 3.75f), 0.62f, p);

            // Ivy drapes from the main arch crown.
            Vector3[] vine =
            {
                o + new Vector3(-1.75f, 2.28f, 2.86f),
                o + new Vector3(-1.62f, 2.05f, 2.82f),
                o + new Vector3(-1.68f, 1.80f, 2.78f),
                o + new Vector3(-1.55f, 1.57f, 2.74f),
            };
            for (int i = 0; i < vine.Length - 1; i++)
                CapsuleWorld("Arch Ivy Vine", vine[i], vine[i + 1], 0.018f, p.MossDark);
            for (int i = 0; i < vine.Length; i++)
                CreateLeafCluster(vine[i] + new Vector3(0f, 0f, -0.02f), 0.10f, i % 2 == 0 ? p.Moss : p.MossLight);

            CreateFlowerAt(o + new Vector3(-2.15f, 2.15f, 2.82f), p.FlowerWhite, p);
            CreateFlowerAt(o + new Vector3(-1.45f, 2.40f, 2.84f), p.FlowerPink, p);
            CreateFlowerAt(o + new Vector3(-2.35f, 1.65f, 2.80f), p.FlowerBlue, p);

            CreateLantern(o + new Vector3(-1.95f, -0.03f, 1.85f), p);
        }

        private static void CreateWaterfallGarden(Vector3 o, Palette p)
        {
            CreateTerrace(o + new Vector3(1.10f, 0.02f, 3.70f), new Vector3(2.25f, 0.72f, 1.85f), p, 0);
            CreateTerrace(o + new Vector3(1.35f, 0.62f, 5.85f), new Vector3(2.35f, 0.88f, 1.90f), p, 1);
            CreateTerrace(o + new Vector3(1.60f, 1.30f, 7.95f), new Vector3(2.40f, 0.98f, 1.95f), p, 2);

            // Foreground water curls along the viewer-right edge, exposing rock blocks through gaps.
            CreateWaterEllipse(o + new Vector3(1.55f, -0.12f, 0.40f), 1.75f, 1.15f, p.Water);
            CreateWaterEllipse(o + new Vector3(1.72f, -0.06f, 1.85f), 1.55f, 0.92f, p.Water);
            CreateWaterEllipse(o + new Vector3(1.52f, 0.08f, 3.05f), 1.30f, 0.72f, p.Water);

            CreateWaterfallRibbon(
                o + new Vector3(1.20f, 1.15f, 3.62f),
                o + new Vector3(1.05f, 0.16f, 3.20f), 0.43f, p);
            CreateWaterfallRibbon(
                o + new Vector3(1.45f, 1.90f, 5.80f),
                o + new Vector3(1.28f, 0.76f, 5.40f), 0.36f, p);
            CreateWaterfallRibbon(
                o + new Vector3(1.72f, 2.62f, 7.90f),
                o + new Vector3(1.53f, 1.50f, 7.45f), 0.30f, p);

            CreateShrub(o + new Vector3(0.35f, 0.46f, 3.45f), 0.45f, p.Moss);
            CreateShrub(o + new Vector3(1.75f, 1.06f, 5.52f), 0.40f, p.MossDark);
            CreateShrub(o + new Vector3(0.95f, 1.78f, 7.55f), 0.42f, p.MossLight);
        }

        private static void CreateCastle(Vector3 o, Palette p)
        {
            Vector3 b = o + new Vector3(1.85f, -1.22f, 11.35f);
            CreateCastleTower(b + new Vector3(0f, 0f, 0f), 0.58f, 2.60f, p);
            CreateCastleTower(b + new Vector3(-0.78f, -0.30f, 0.12f), 0.43f, 1.85f, p);
            CreateCastleTower(b + new Vector3(0.72f, -0.38f, 0.10f), 0.39f, 1.65f, p);
            CreateCastleTower(b + new Vector3(0.38f, 0.62f, 0.18f), 0.33f, 1.65f, p);

            StoneBlock(b + new Vector3(0f, -0.34f, 0f), new Vector3(1.85f, 0.72f, 0.88f), p.Castle, Quaternion.identity);
            CreateMossPatch(b + new Vector3(-0.50f, 0.05f, -0.28f), new Vector3(0.65f, 0.12f, 0.44f), p.MossDark);
        }

        private static void CreateCastleTower(Vector3 basePosition, float radius, float height, Palette p)
        {
            StoneBlock(basePosition + new Vector3(0f, height * 0.5f, 0f),
                new Vector3(radius * 1.70f, height, radius * 1.70f), p.Castle, Quaternion.identity);

            Mesh cone = BuildCone(radius * 1.25f, radius * 0.12f, radius * 1.75f, 12);
            Owned.Add(cone);
            MeshObject("Castle Spire", cone, p.CastleRoof, _root.transform,
                basePosition + new Vector3(0f, height + radius * 0.82f, 0f));

            GameObject window = Primitive(PrimitiveType.Cube, "Castle Blue Window", p.CastleWindow, _root.transform);
            window.transform.position = basePosition + new Vector3(0f, height * 0.62f, -radius * 0.88f);
            window.transform.localScale = new Vector3(radius * 0.28f, radius * 0.52f, 0.025f);
        }

        private static void CreateTerrace(Vector3 centre, Vector3 size, Palette p, int seed)
        {
            int pieces = 4;
            for (int i = 0; i < pieces; i++)
            {
                float x = Mathf.Lerp(-size.x * 0.42f, size.x * 0.42f, i / (float)(pieces - 1));
                float y = (Hash(i, seed, 4) - 0.5f) * 0.08f;
                float z = (Hash(i, seed, 8) - 0.5f) * 0.12f;
                StoneBlock(centre + new Vector3(x, y, z),
                    new Vector3(size.x / pieces * 1.22f, size.y * (0.86f + Hash(i, seed, 9) * 0.16f), size.z * (0.82f + Hash(i, seed, 6) * 0.16f)),
                    StoneVariant(p, seed * 9 + i),
                    Quaternion.Euler((Hash(i, seed, 3) - 0.5f) * 4f, (Hash(i, seed, 5) - 0.5f) * 9f, 0f));
            }

            CreateMossPatch(centre + new Vector3(-size.x * 0.20f, size.y * 0.46f, -0.05f),
                new Vector3(size.x * 0.50f, 0.18f, size.z * 0.68f), p.Moss);
            CreateMossPatch(centre + new Vector3(size.x * 0.23f, size.y * 0.48f + 0.02f, 0.12f),
                new Vector3(size.x * 0.42f, 0.16f, size.z * 0.55f), seed % 2 == 0 ? p.MossLight : p.MossDark);
        }

        private static void CreateArch(Vector3 basePos, float radius, float height, Palette p)
        {
            float block = Mathf.Max(0.27f, radius * 0.31f);
            int pillarCount = Mathf.Max(5, Mathf.RoundToInt(height * 0.64f / (block * 0.88f)));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int y = 0; y < pillarCount; y++)
                {
                    Material stone = StoneVariant(p, y + side * 17);
                    StoneBlock(basePos + new Vector3(side * radius, block * 0.5f + y * block * 0.88f, 0f),
                        new Vector3(block * 1.08f, block, block * 0.90f), stone,
                        Quaternion.Euler(0f, (Hash(y, side, 3) - 0.5f) * 7f, side * (y % 2) * 2f));
                }
            }

            int crown = 13;
            float springY = height * 0.60f;
            for (int i = 0; i < crown; i++)
            {
                float t = i / (float)(crown - 1);
                float angle = Mathf.Lerp(180f, 0f, t) * Mathf.Deg2Rad;
                StoneBlock(basePos + new Vector3(Mathf.Cos(angle) * radius, springY + Mathf.Sin(angle) * radius, 0f),
                    new Vector3(block * 1.06f, block * 0.96f, block * 0.90f), StoneVariant(p, i + 41),
                    Quaternion.Euler(0f, 0f, -Mathf.Cos(angle) * 28f));
            }

            CreateMossPatch(basePos + new Vector3(-radius * 0.75f, springY + radius * 0.80f, -0.10f),
                new Vector3(radius * 0.70f, 0.16f, 0.42f), p.Moss);
            CreateMossPatch(basePos + new Vector3(radius * 0.30f, springY + radius + 0.02f, -0.08f),
                new Vector3(radius * 0.58f, 0.14f, 0.38f), p.MossLight);
        }

        private static void CreateTree(Vector3 basePos, float scale, Palette p)
        {
            CapsuleWorld("Storybook Tree Trunk", basePos,
                basePos + new Vector3(0.18f, 3.8f * scale, 0.08f), 0.24f * scale, p.Trunk);
            CapsuleWorld("Storybook Tree Branch L", basePos + new Vector3(0.09f, 2.35f * scale, 0.03f),
                basePos + new Vector3(-1.00f * scale, 3.35f * scale, 0.12f), 0.145f * scale, p.Trunk);
            CapsuleWorld("Storybook Tree Branch R", basePos + new Vector3(0.11f, 2.45f * scale, 0.03f),
                basePos + new Vector3(1.12f * scale, 3.30f * scale, 0.08f), 0.145f * scale, p.Trunk);

            Vector3 crown = basePos + new Vector3(0.05f, 3.65f * scale, 0.10f);
            Vector3[] offsets =
            {
                new(-1.05f,0.02f,0f), new(-0.55f,0.48f,0.04f), new(0f,0.66f,0f),
                new(0.62f,0.48f,0.04f), new(1.08f,0.04f,0f), new(-0.05f,-0.20f,-0.08f),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject clump = Primitive(PrimitiveType.Sphere, "Storybook Leaf Clump",
                    i % 3 == 1 ? p.LeavesLight : p.Leaves, _root.transform);
                clump.transform.position = crown + offsets[i] * scale;
                clump.transform.localScale = new Vector3(1.25f, 0.92f, 1.02f) * scale;
            }
        }

        private static void CreateForegroundDetails(Vector3 o, Palette p)
        {
            CreateShrub(o + new Vector3(-1.62f, -0.04f, -0.10f), 0.58f, p.MossDark);
            CreateShrub(o + new Vector3(-1.30f, 0.00f, 1.05f), 0.48f, p.Moss);
            CreateShrub(o + new Vector3(1.52f, -0.06f, 0.05f), 0.52f, p.MossDark);
            CreateShrub(o + new Vector3(1.66f, 0.04f, 1.58f), 0.43f, p.MossLight);
            CreateShrub(o + new Vector3(-0.65f, -0.16f, -0.65f), 0.42f, p.MossLight);
            CreateShrub(o + new Vector3(0.55f, -0.16f, -0.72f), 0.40f, p.MossDark);

            CreateFlowerPatch(o + new Vector3(-1.32f, 0.02f, 0.14f), p, 0);
            CreateFlowerPatch(o + new Vector3(1.22f, 0.02f, 0.30f), p, 1);
            CreateFlowerPatch(o + new Vector3(-1.42f, 0.12f, 1.78f), p, 2);
            CreateFlowerPatch(o + new Vector3(0.95f, 0.20f, 2.25f), p, 3);

            // A few chunky loose stones reinforce the destructible physical substrate.
            StoneBlock(o + new Vector3(-2.05f, -0.18f, 0.65f), new Vector3(0.48f, 0.35f, 0.42f), p.Stone, Quaternion.Euler(0f, 18f, 8f));
            StoneBlock(o + new Vector3(2.05f, -0.19f, 0.82f), new Vector3(0.42f, 0.31f, 0.46f), p.StoneLight, Quaternion.Euler(5f, -12f, 0f));
        }

        // ------------------------------------------------------------------
        // Detail helpers
        // ------------------------------------------------------------------

        private static void CreateWaterfallRibbon(Vector3 top, Vector3 bottom, float width, Palette p)
        {
            Vector3 side = Vector3.right;
            Vector3[] centres = new Vector3[11];
            for (int i = 0; i < centres.Length; i++)
            {
                float t = i / (float)(centres.Length - 1);
                Vector3 c = Vector3.Lerp(top, bottom, t);
                c.x += Mathf.Sin(t * 7f) * width * 0.10f + Mathf.Sin(t * 15f) * width * 0.035f;
                c.z -= 0.04f * Mathf.Sin(t * Mathf.PI);
                centres[i] = c;
            }
            Mesh body = BuildWorldRibbon(centres, side, width);
            Owned.Add(body);
            MeshObject("Flowing Waterfall", body, p.Water, _root.transform, Vector3.zero);

            Vector3[] highlightCentres = new Vector3[centres.Length];
            for (int i = 0; i < centres.Length; i++) highlightCentres[i] = centres[i] + new Vector3(-width * 0.14f, 0f, -0.025f);
            Mesh highlight = BuildWorldRibbon(highlightCentres, side, width * 0.20f);
            Owned.Add(highlight);
            MeshObject("Waterfall Sun Streak", highlight, p.WaterLight, _root.transform, Vector3.zero);

            for (int i = 0; i < 5; i++)
            {
                GameObject foam = Primitive(PrimitiveType.Sphere, "Waterfall Foam", p.WaterLight, _root.transform);
                foam.transform.position = bottom + new Vector3((i - 2) * width * 0.16f, 0.02f + (i & 1) * 0.025f, -0.03f);
                foam.transform.localScale = new Vector3(width * 0.30f, 0.07f, 0.16f);
            }
        }

        private static void CreateWaterEllipse(Vector3 centre, float radiusX, float radiusZ, Material material)
        {
            Mesh mesh = BuildEllipse(radiusX, radiusZ, 48);
            Owned.Add(mesh);
            GameObject water = MeshObject("Turquoise Pool", mesh, material, _root.transform, centre);
            water.transform.rotation = Quaternion.identity;
        }

        private static void CreateCloud(Vector3 centre, float scale, Material material)
        {
            Vector3[] offsets =
            {
                new(-1.00f,0f,0f), new(-0.42f,0.38f,0f), new(0.26f,0.45f,0f),
                new(0.92f,0.06f,0f), new(0.10f,-0.20f,-0.05f),
            };
            Vector3[] sizes =
            {
                new(1.12f,0.76f,0.64f), new(1.34f,1.00f,0.76f), new(1.48f,1.04f,0.80f),
                new(1.08f,0.74f,0.62f), new(1.38f,0.60f,0.70f),
            };
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject puff = Primitive(PrimitiveType.Sphere, "Soft Cloud Puff", material, _root.transform);
                puff.transform.position = centre + offsets[i] * scale;
                puff.transform.localScale = sizes[i] * scale;
                Renderer r = puff.GetComponent<Renderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static void CreateMossPatch(Vector3 centre, Vector3 scale, Material material)
        {
            GameObject moss = Primitive(PrimitiveType.Sphere, "Soft Moss Cushion", material, _root.transform);
            moss.transform.position = centre;
            moss.transform.localScale = scale;
        }

        private static void CreateShrub(Vector3 centre, float scale, Material material)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 0.48f;
                GameObject lump = Primitive(PrimitiveType.Sphere, "Rounded Garden Shrub", material, _root.transform);
                lump.transform.position = centre + new Vector3(Mathf.Cos(a) * scale * 0.32f,
                    (i % 3) * scale * 0.06f, Mathf.Sin(a) * scale * 0.22f);
                lump.transform.localScale = new Vector3(scale * 0.62f, scale * 0.42f, scale * 0.54f);
            }
        }

        private static void CreateLeafCluster(Vector3 centre, float scale, Material material)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject leaf = Primitive(PrimitiveType.Sphere, "Ivy Leaf", material, _root.transform);
                leaf.transform.position = centre + new Vector3((i - 1) * scale * 0.45f, (i & 1) * scale * 0.30f, 0f);
                leaf.transform.localScale = new Vector3(scale * 0.70f, scale * 0.30f, scale * 0.20f);
            }
        }

        private static void CreateFlowerPatch(Vector3 centre, Palette p, int seed)
        {
            Material[] petals = { p.FlowerWhite, p.FlowerPink, p.FlowerBlue, p.FlowerWhite, p.FlowerPink, p.FlowerYellow };
            for (int i = 0; i < 7; i++)
            {
                Vector3 q = centre + new Vector3((Hash(i, seed, 1) - 0.5f) * 0.82f, 0f,
                    (Hash(i, seed, 2) - 0.5f) * 0.56f);
                q.y += 0.16f + Hash(i, seed, 3) * 0.12f;
                CapsuleWorld("Flower Stem", q - Vector3.up * q.y + new Vector3(0f, 0.02f, 0f), q,
                    0.010f, p.FlowerStem);
                CreateFlowerBlossom(q, petals[(i + seed) % petals.Length], p.FlowerYellow);
            }
        }

        private static void CreateFlowerAt(Vector3 centre, Material petal, Palette p)
        {
            CreateFlowerBlossom(centre, petal, p.FlowerYellow);
        }

        private static void CreateFlowerBlossom(Vector3 centre, Material petalMaterial, Material centreMaterial)
        {
            GameObject core = Primitive(PrimitiveType.Sphere, "Flower Centre", centreMaterial, _root.transform);
            core.transform.position = centre;
            core.transform.localScale = Vector3.one * 0.040f;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                GameObject petal = Primitive(PrimitiveType.Sphere, "Flower Petal", petalMaterial, _root.transform);
                petal.transform.position = centre + new Vector3(Mathf.Cos(a) * 0.062f, 0f, Mathf.Sin(a) * 0.062f);
                petal.transform.localScale = new Vector3(0.070f, 0.021f, 0.046f);
            }
        }

        private static void CreateLantern(Vector3 basePos, Palette p)
        {
            StoneBlock(basePos + new Vector3(0f, 0.13f, 0f), new Vector3(0.27f, 0.26f, 0.27f), p.Stone, Quaternion.identity);
            GameObject glow = Primitive(PrimitiveType.Cube, "Warm Ruin Lantern", p.Lantern, _root.transform);
            glow.transform.position = basePos + new Vector3(0f, 0.36f, -0.02f);
            glow.transform.localScale = new Vector3(0.14f, 0.21f, 0.14f);
            StoneBlock(basePos + new Vector3(0f, 0.52f, 0f), new Vector3(0.22f, 0.07f, 0.22f), p.StoneLight, Quaternion.Euler(0f, 45f, 0f));
        }

        // ------------------------------------------------------------------
        // Materials / lighting
        // ------------------------------------------------------------------

        private static Palette BuildPalette()
        {
            var p = new Palette
            {
                Skin = Smooth("Madeline Skin", new Color(0.96f, 0.73f, 0.59f), 0.14f),
                SkinShadow = Smooth("Madeline Nose", new Color(0.85f, 0.56f, 0.45f), 0.10f),
                Hair = Smooth("Madeline Blonde", new Color(0.94f, 0.70f, 0.22f), 0.18f),
                HairShadow = Smooth("Madeline Hair Shadow", new Color(0.76f, 0.48f, 0.12f), 0.14f),
                White = Smooth("Cleric Ivory", new Color(0.98f, 0.96f, 0.90f), 0.08f),
                WhiteShadow = Smooth("Cleric Warm Ivory", new Color(0.88f, 0.85f, 0.77f), 0.06f),
                Blue = Smooth("Cleric Light Blue", new Color(0.34f, 0.65f, 0.80f), 0.15f),
                Gold = Smooth("Cleric Gold", new Color(0.91f, 0.64f, 0.13f), 0.24f),
                Leather = Smooth("Warm Leather", new Color(0.32f, 0.16f, 0.065f), 0.10f),
                LeatherDark = Smooth("Dark Walnut", new Color(0.18f, 0.075f, 0.025f), 0.06f),
                EyeWhite = Smooth("Eye White", new Color(0.99f, 0.98f, 0.95f), 0.10f),
                Iris = Smooth("Brown Iris", new Color(0.30f, 0.12f, 0.040f), 0.18f),
                Pupil = Smooth("Eye Pupil", new Color(0.045f, 0.020f, 0.012f), 0.06f),
                Mouth = Smooth("Friendly Mouth", new Color(0.55f, 0.16f, 0.17f), 0.08f),
                Stone = Lookdev("Warm Limestone", "stone", new Color(0.78f, 0.70f, 0.56f), 0.18f),
                StoneLight = Lookdev("Sunlit Limestone", "stone", new Color(0.88f, 0.80f, 0.66f), 0.15f),
                StoneDark = Lookdev("Weathered Limestone", "rock", new Color(0.55f, 0.53f, 0.46f), 0.14f),
                Moss = Smooth("Moss Green", new Color(0.38f, 0.59f, 0.18f), 0.03f),
                MossLight = Smooth("Sunlit Moss", new Color(0.55f, 0.69f, 0.24f), 0.03f),
                MossDark = Smooth("Deep Moss", new Color(0.22f, 0.43f, 0.10f), 0.03f),
                Trunk = Smooth("Tree Bark", new Color(0.29f, 0.16f, 0.07f), 0.04f),
                Leaves = Smooth("Leaf Green", new Color(0.39f, 0.61f, 0.19f), 0.03f),
                LeavesLight = Smooth("Leaf Sunlight", new Color(0.57f, 0.70f, 0.25f), 0.03f),
                Water = Transparent("Turquoise Water", new Color(0.15f, 0.70f, 0.86f, 0.72f), new Color(0.025f, 0.10f, 0.14f)),
                WaterLight = Transparent("Waterfall Foam", new Color(0.94f, 0.99f, 1f, 0.68f), new Color(0.08f, 0.10f, 0.12f)),
                Cloud = Smooth("Soft White Cloud", new Color(0.99f, 0.99f, 0.97f), 0.02f),
                Sky = Smooth("Clear Blue Sky", new Color(0.06f, 0.50f, 0.90f), 0f, new Color(0.020f, 0.10f, 0.18f)),
                Castle = Smooth("Distant Castle Stone", new Color(0.74f, 0.75f, 0.72f), 0.03f),
                CastleRoof = Smooth("Distant Castle Roof", new Color(0.42f, 0.46f, 0.50f), 0.05f),
                CastleWindow = Smooth("Castle Blue Window", new Color(0.17f, 0.37f, 0.52f), 0.10f),
                FlowerWhite = Smooth("Flower White", new Color(0.99f, 0.98f, 0.93f), 0.03f),
                FlowerPink = Smooth("Flower Pink", new Color(0.95f, 0.51f, 0.60f), 0.03f),
                FlowerBlue = Smooth("Flower Blue", new Color(0.44f, 0.69f, 0.92f), 0.03f),
                FlowerYellow = Smooth("Flower Yellow", new Color(1.0f, 0.78f, 0.16f), 0.04f),
                FlowerStem = Smooth("Flower Stem", new Color(0.22f, 0.43f, 0.09f), 0.02f),
                Lantern = Smooth("Lantern Glow", new Color(1.0f, 0.69f, 0.14f), 0.08f, new Color(0.42f, 0.18f, 0.02f)),
            };
            return p;
        }

        private static void ConfigureLighting(Vector3 origin)
        {
            GameObject original = GameObject.Find("Sunlit Cleric Sun");
            if (original != null)
            {
                Light sun = original.GetComponent<Light>();
                if (sun != null)
                {
                    sun.color = new Color(1f, 0.93f, 0.78f);
                    sun.intensity = 1.35f;
                    sun.shadowStrength = 0.52f;
                    sun.shadows = LightShadows.Soft;
                }
                original.transform.rotation = Quaternion.Euler(43f, -34f, 0f);
            }

            var fillObject = new GameObject("Sunlit Cleric Cool Fill") { hideFlags = HideFlags.DontSave };
            fillObject.transform.SetParent(_root.transform, false);
            fillObject.transform.position = origin;
            fillObject.transform.rotation = Quaternion.Euler(28f, 145f, 0f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.64f, 0.80f, 1.0f);
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
            Owned.Add(fillObject);
        }

        private static void ConfigureCamera(Camera camera, Vector3 origin)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.50f, 0.90f, 1f);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 70f;
            camera.transform.position = origin + new Vector3(0.18f, 1.95f, -4.70f);
            camera.transform.LookAt(origin + new Vector3(0.05f, 0.76f, 2.35f));

            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.40f, 0.72f, 0.91f);
            RenderSettings.fogStartDistance = 8.5f;
            RenderSettings.fogEndDistance = 20.5f;
        }

        // ------------------------------------------------------------------
        // Mesh / object helpers
        // ------------------------------------------------------------------

        private static Material Smooth(string name, Color colour, float smoothness = 0.06f, Color? emission = null)
        {
            Shader shader = Shader.Find("VoxelEngine/SunlitSmooth");
            if (shader == null) throw new InvalidOperationException("VoxelEngine/SunlitSmooth shader is missing.");
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            m.SetTexture("_MainTex", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Cull", 2f);
            m.SetFloat("_ZWrite", 1f);
            Owned.Add(m);
            return m;
        }

        private static Material Transparent(string name, Color colour, Color emission)
        {
            Material m = Smooth(name, colour, 0.04f, emission);
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        private static Material Lookdev(string name, string textureName, Color tint, float influence)
        {
            Shader shader = Shader.Find("VoxelEngine/WorldArtLookdev");
            if (shader == null) return Smooth(name, tint, 0.03f);
            var m = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/Stylized/{textureName}_color.png");
            if (texture != null) m.SetTexture("_MainTex", texture);
            m.SetColor("_Tint", tint);
            m.SetFloat("_TextureScale", 0.22f);
            m.SetFloat("_TextureInfluence", influence);
            m.SetFloat("_Smoothness", 0.04f);
            m.SetFloat("_TopLight", 0.16f);
            Owned.Add(m);
            return m;
        }

        private static Material StoneVariant(Palette p, int n)
        {
            int v = Mathf.Abs(n) % 5;
            return v == 0 ? p.StoneLight : v == 1 ? p.StoneDark : p.Stone;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Material material, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent ?? _root.transform, false);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material.renderQueue >= (int)RenderQueue.Transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            Owned.Add(go);
            return go;
        }

        private static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent, Vector3 position)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent ?? _root.transform, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material.renderQueue >= (int)RenderQueue.Transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            Owned.Add(go);
            return go;
        }

        private static void StoneBlock(Vector3 worldPosition, Vector3 scale, Material material, Quaternion rotation)
        {
            GameObject block = MeshObject("Chamfered Storybook Stone", BeveledCube(), material, _root.transform, worldPosition);
            block.transform.rotation = rotation;
            block.transform.localScale = scale;
        }

        private static void CapsuleLocal(string name, Vector3 a, Vector3 b, float radius, Material material, Transform parent)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 0.00001f) return;
            GameObject capsule = Primitive(PrimitiveType.Capsule, name, material, parent);
            capsule.transform.localPosition = (a + b) * 0.5f;
            capsule.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            capsule.transform.localScale = new Vector3(radius * 2f, Mathf.Max(radius, d.magnitude * 0.5f), radius * 2f);
        }

        private static void CapsuleWorld(string name, Vector3 a, Vector3 b, float radius, Material material)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 0.00001f) return;
            GameObject capsule = Primitive(PrimitiveType.Capsule, name, material, _root.transform);
            capsule.transform.position = (a + b) * 0.5f;
            capsule.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            capsule.transform.localScale = new Vector3(radius * 2f, Mathf.Max(radius, d.magnitude * 0.5f), radius * 2f);
        }

        private static Mesh BuildFrustum(float bottomRadius, float topRadius, float height, int segments, bool capped)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float slope = (bottomRadius - topRadius) / Mathf.Max(0.001f, height);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(a);
                float z = Mathf.Sin(a);
                Vector3 n = new Vector3(x, slope, z).normalized;
                vertices.Add(new Vector3(x * bottomRadius, -height * 0.5f, z * bottomRadius));
                vertices.Add(new Vector3(x * topRadius, height * 0.5f, z * topRadius));
                normals.Add(n); normals.Add(n);
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int b0 = i * 2, t0 = b0 + 1, b1 = next * 2, t1 = b1 + 1;
                triangles.Add(b0); triangles.Add(t0); triangles.Add(t1);
                triangles.Add(b0); triangles.Add(t1); triangles.Add(b1);
            }
            if (capped)
            {
                int bc = vertices.Count; vertices.Add(new Vector3(0f, -height * 0.5f, 0f)); normals.Add(Vector3.down);
                int tc = vertices.Count; vertices.Add(new Vector3(0f, height * 0.5f, 0f)); normals.Add(Vector3.up);
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    triangles.Add(bc); triangles.Add(next * 2); triangles.Add(i * 2);
                    triangles.Add(tc); triangles.Add(i * 2 + 1); triangles.Add(next * 2 + 1);
                }
            }
            var mesh = new Mesh { name = "Runtime Stylized Frustum", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildTaperedPanel(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var mesh = new Mesh { name = "Runtime Cloth Panel", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { a, b, c, d, a, d, c, b };
            mesh.triangles = new[] { 0,1,2, 0,2,3, 4,5,6, 4,6,7 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRibbon(IReadOnlyList<Vector3> points, float width)
        {
            int count = points.Count;
            var vertices = new Vector3[count * 2];
            var triangles = new int[(count - 1) * 12];
            for (int i = 0; i < count; i++)
            {
                Vector3 tangent = i == 0 ? points[1] - points[0] : i == count - 1 ? points[count - 1] - points[count - 2] : points[i + 1] - points[i - 1];
                Vector3 side = Vector3.Cross(tangent.normalized, Vector3.forward).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                float taper = Mathf.Lerp(1f, 0.45f, i / (float)(count - 1));
                vertices[i * 2] = points[i] - side * width * taper * 0.5f;
                vertices[i * 2 + 1] = points[i] + side * width * taper * 0.5f;
                if (i < count - 1)
                {
                    int v = i * 2, t = i * 12;
                    triangles[t] = v; triangles[t+1] = v+2; triangles[t+2] = v+1;
                    triangles[t+3] = v+1; triangles[t+4] = v+2; triangles[t+5] = v+3;
                    triangles[t+6] = v+1; triangles[t+7] = v+2; triangles[t+8] = v;
                    triangles[t+9] = v+3; triangles[t+10] = v+2; triangles[t+11] = v+1;
                }
            }
            var mesh = new Mesh { name = "Runtime Hair Ribbon", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildWorldRibbon(IReadOnlyList<Vector3> centres, Vector3 side, float width)
        {
            int count = centres.Count;
            var vertices = new Vector3[count * 2];
            var triangles = new int[(count - 1) * 12];
            for (int i = 0; i < count; i++)
            {
                float w = width * (0.90f + 0.10f * Mathf.Sin(i * 1.7f));
                vertices[i * 2] = centres[i] - side * w * 0.5f;
                vertices[i * 2 + 1] = centres[i] + side * w * 0.5f;
                if (i < count - 1)
                {
                    int v = i * 2, t = i * 12;
                    triangles[t] = v; triangles[t+1] = v+2; triangles[t+2] = v+1;
                    triangles[t+3] = v+1; triangles[t+4] = v+2; triangles[t+5] = v+3;
                    triangles[t+6] = v+1; triangles[t+7] = v+2; triangles[t+8] = v;
                    triangles[t+9] = v+3; triangles[t+10] = v+2; triangles[t+11] = v+1;
                }
            }
            var mesh = new Mesh { name = "Runtime Water Ribbon", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildEllipse(float radiusX, float radiusZ, int segments)
        {
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero; normals[0] = Vector3.up;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radiusX, 0f, Mathf.Sin(a) * radiusZ);
                normals[i + 1] = Vector3.up;
                int next = (i + 1) % segments;
                int t = i * 3;
                triangles[t] = 0; triangles[t+1] = i + 1; triangles[t+2] = next + 1;
            }
            var mesh = new Mesh { name = "Runtime Turquoise Pool", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCone(float bottomRadius, float topRadius, float height, int segments)
        {
            return BuildFrustum(bottomRadius, topRadius, height, segments, true);
        }

        private static Mesh BeveledCube()
        {
            if (_beveledCube != null) return _beveledCube;
            const float h = 0.5f;
            const float inset = 0.405f;
            var vertices = new List<Vector3>(128);
            var normals = new List<Vector3>(128);
            var triangles = new List<int>(192);

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, outward) < 0f) { Vector3 temp = b; b = d; d = temp; n = Vector3.Cross(b - a, c - a).normalized; }
                int s = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
                triangles.Add(s); triangles.Add(s+1); triangles.Add(s+2); triangles.Add(s); triangles.Add(s+2); triangles.Add(s+3);
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, outward) < 0f) { Vector3 temp = b; b = c; c = temp; n = Vector3.Cross(b - a, c - a).normalized; }
                int s = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                triangles.Add(s); triangles.Add(s+1); triangles.Add(s+2);
            }

            Quad(new(h,-inset,-inset), new(h,inset,-inset), new(h,inset,inset), new(h,-inset,inset), Vector3.right);
            Quad(new(-h,-inset,inset), new(-h,inset,inset), new(-h,inset,-inset), new(-h,-inset,-inset), Vector3.left);
            Quad(new(-inset,h,-inset), new(-inset,h,inset), new(inset,h,inset), new(inset,h,-inset), Vector3.up);
            Quad(new(-inset,-h,inset), new(-inset,-h,-inset), new(inset,-h,-inset), new(inset,-h,inset), Vector3.down);
            Quad(new(-inset,-inset,h), new(inset,-inset,h), new(inset,inset,h), new(-inset,inset,h), Vector3.forward);
            Quad(new(inset,-inset,-h), new(-inset,-inset,-h), new(-inset,inset,-h), new(inset,inset,-h), Vector3.back);

            for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                Quad(new(-inset,sy*h,sz*inset), new(inset,sy*h,sz*inset), new(inset,sy*inset,sz*h), new(-inset,sy*inset,sz*h), new(0,sy,sz));
            for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                Quad(new(sx*h,-inset,sz*inset), new(sx*inset,-inset,sz*h), new(sx*inset,inset,sz*h), new(sx*h,inset,sz*inset), new(sx,0,sz));
            for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2)
                Quad(new(sx*h,sy*inset,-inset), new(sx*inset,sy*h,-inset), new(sx*inset,sy*h,inset), new(sx*h,sy*inset,inset), new(sx,sy,0));
            for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2) for (int sz = -1; sz <= 1; sz += 2)
                Tri(new(sx*h,sy*inset,sz*inset), new(sx*inset,sy*h,sz*inset), new(sx*inset,sy*inset,sz*h), new(sx,sy,sz));

            _beveledCube = new Mesh { name = "Runtime Chamfered Storybook Block", hideFlags = HideFlags.DontSave };
            _beveledCube.SetVertices(vertices); _beveledCube.SetNormals(normals); _beveledCube.SetTriangles(triangles, 0); _beveledCube.RecalculateBounds();
            Owned.Add(_beveledCube);
            return _beveledCube;
        }

        private static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}
