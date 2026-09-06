using System;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using UnityEngine;

namespace Game.Structures.Validation
{
    /// <summary>
    /// Module-local player-visible proof that the Structures-owned PropShowcase catalogue/realizer
    /// and reusable procedural/thin presenters operate without the integration showcase scene.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PropShowcaseProductionValidation : MonoBehaviour, IDecorationProceduralMaterialResolver
    {
        private DecorationProceduralMeshPresenter _procedural;
        private DecorationThinSurfacePresenter _thin;
        private UnityWorldObjectPresentationSink _worldObjects;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            var cameraComponent = GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.48f, 0.55f, 0.61f, 1f);
            cameraComponent.fieldOfView = 42f;
            cameraComponent.transform.position = new Vector3(2.8f, 2.2f, -5.2f);
            cameraComponent.transform.LookAt(new Vector3(0f, 0.85f, 0f));

            GameMaterialComposition.Install();
            DecorationContext context = Context();
            DecorationShowcaseEntry[] entries = DecorationShowcaseCatalog.CreateEntries();
            if (entries.Length != 529)
                throw new InvalidOperationException($"Structures PropShowcase catalogue count was {entries.Length}, expected 529.");

            int realized = 0;
            DecorationPlacement proceduralPlacement = default;
            DecorationPlacement thinPlacement = default;
            WorldObjectPresentationPlan trapdoorPlan = default;
            for (int i = 0; i < entries.Length; i++)
            {
                DecorationShowcaseEntry entry = entries[i];
                if (!DecorationShowcaseRealizer.TryCreate(in entry, in context, out DecorationShowcaseRealization realization) ||
                    !realization.IsWellFormed)
                    throw new InvalidOperationException($"Structures PropShowcase failed to realize {entry.StableId}.");
                realized++;

                if (realization.Kind == DecorationShowcaseRealizationKind.Decoration)
                {
                    if (!proceduralPlacement.IsWellFormed &&
                        realization.Decoration.Backend == DecorationRenderBackend.ProceduralMesh)
                        proceduralPlacement = realization.Decoration;
                    if (!thinPlacement.IsWellFormed &&
                        realization.Decoration.Backend == DecorationRenderBackend.ThinSurface)
                        thinPlacement = realization.Decoration;
                }
                else if (realization.Kind == DecorationShowcaseRealizationKind.WorldObject &&
                         realization.WorldObject.Descriptor.Kind == WorldObjectKind.Trapdoor)
                {
                    WorldObjectDescriptor descriptor = realization.WorldObject.Descriptor;
                    if (descriptor.Facing.x != 0 || descriptor.Facing.y != 1 || descriptor.Facing.z != 0 ||
                        descriptor.Bounds.Size.y >= descriptor.Bounds.Size.x ||
                        descriptor.Bounds.Size.y >= descriptor.Bounds.Size.z)
                        throw new InvalidOperationException("Structures PropShowcase trapdoor was not floor-mounted.");
                    trapdoorPlan = WorldObjectPresentationPlanner.Plan(in realization.WorldObject);
                }
            }

            if (!proceduralPlacement.IsWellFormed || !thinPlacement.IsWellFormed || !trapdoorPlan.IsWellFormed)
                throw new InvalidOperationException("Structures PropShowcase validation found no procedural/thin/trapdoor representatives.");

            _procedural = new GameObject("Procedural Presenter").AddComponent<DecorationProceduralMeshPresenter>();
            _procedural.transform.position = new Vector3(-1.4f, 0f, 0f);
            DecorationProceduralMeshRequest[] procedural =
                DecorationProceduralMeshHookPlanner.Collect(new[] { proceduralPlacement });
            if (procedural.Length != 1 || !_procedural.TryPresent(in procedural[0], this))
                throw new InvalidOperationException("Structures PropShowcase procedural presenter failed.");

            _thin = new GameObject("Thin Presenter").AddComponent<DecorationThinSurfacePresenter>();
            _thin.transform.position = new Vector3(1.4f, 0f, 0f);
            if (!_thin.TryPresent(new[] { thinPlacement }, in context, this))
                throw new InvalidOperationException("Structures PropShowcase thin presenter failed.");

            // Reuse the shipped dynamic-object presenter. The validation does not construct a
            // replacement hatch; its primitive art remains an explicitly unaccepted product defect.
            _worldObjects = new GameObject("World Object Presenter").AddComponent<UnityWorldObjectPresentationSink>();
            _worldObjects.transform.position = new Vector3(-0.6f, 0f, -1.6f);
            _worldObjects.CreateOrUpdate(in trapdoorPlan);
            Renderer trapdoorRenderer = _worldObjects.GetComponentInChildren<Renderer>();
            if (trapdoorRenderer == null || !trapdoorRenderer.enabled ||
                trapdoorRenderer.bounds.size.y >= trapdoorRenderer.bounds.size.x ||
                trapdoorRenderer.bounds.size.y >= trapdoorRenderer.bounds.size.z)
                throw new InvalidOperationException("Structures PropShowcase presenter did not preserve horizontal trapdoor bounds.");
            Debug.Log($"STRUCTURES_PROP_SHOWCASE_VALIDATION trapdoor floorMounted=True proxy={_worldObjects.ProxyCount}");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Neutral Validation Floor";
            floor.transform.position = new Vector3(0f, -0.015f, 0f);
            floor.transform.localScale = new Vector3(0.65f, 1f, 0.65f);
            if (TryResolve(GameMaterialIds.DarkStone, out Material floorMaterial))
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

            var lightObject = new GameObject("Validation Key Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light key = lightObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.color = new Color(1f, 0.95f, 0.86f, 1f);

            Debug.Log(
                $"STRUCTURES_PROP_SHOWCASE_VALIDATION start count={entries.Length} realized={realized}");
            Debug.Log(
                $"STRUCTURES_PROP_SHOWCASE_VALIDATION complete procedural={_procedural.ActiveCount} thin={(_thin.HasActiveSurface ? 1 : 0)}");
        }

        public bool TryResolve(byte materialId, out Material material) =>
            GameMaterialComposition.TryGetProceduralMaterial(materialId, out material);

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            _procedural?.Clear();
            _thin?.Clear();
            _worldObjects?.Clear();
            if (_worldObjects != null) Destroy(_worldObjects.gameObject);
            _worldObjects = null;
        }

        private static DecorationContext Context() => new DecorationContext
        {
            WorldSeed = 0x50525031u,
            StructureId = 0x50525032u,
            SpaceId = 0x50525033u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Rustic, 17u),
            StructureKind = DecorationStructureKind.House,
            SpaceKind = DecorationSpaceKind.Storage,
            Wealth = DecorationWealthTier.Comfortable,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior,
        };
    }
}
