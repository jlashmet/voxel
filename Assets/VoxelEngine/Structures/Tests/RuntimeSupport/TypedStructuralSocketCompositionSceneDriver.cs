using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Structures.Tests.RuntimeSupport
{
    /// <summary>
    /// Player-safe deterministic validation surface for typed structural socket composition.
    /// All attachment decisions and child transforms come from StructuralCompositionPlanner;
    /// this driver only stages the solved results for inspection and reports pass/fail state.
    /// </summary>
    public sealed class TypedStructuralSocketCompositionSceneDriver : MonoBehaviour
    {
        private const uint Seed = 0x51A7C0DEu;
        private const float LaneSpacing = 11f;

        private static readonly StructuralSocketRole[] Roles =
        {
            StructuralSocketRole.BridgeSpan,
            StructuralSocketRole.Tower,
            StructuralSocketRole.Platform,
            StructuralSocketRole.Facade,
        };

        private static readonly string[] Labels =
        {
            "Bridge span",
            "Castle tower",
            "Cliff platform",
            "Civic facade",
        };

        private bool _complete;
        private bool _passed;
        private string _detail = "NOT RUN";

        public bool Complete => _complete;
        public bool Passed => _passed;
        public string Detail => _detail;

        private void Start()
        {
            RunValidation();
        }

        public void RunValidation()
        {
            ClearGeneratedChildren();
            ConfigureView();
            BuildGround();

            FeatureCatalogue catalogue = default;
            NativeList<StructuralInstance> instances = default;
            NativeList<StructuralAttachmentDecision> decisions = default;
            try
            {
                catalogue = BuildCatalogue(Allocator.Temp);
                CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (load != CatalogueLoadResult.Ok)
                {
                    Fail("Catalogue finalise failed: " + load);
                    return;
                }

                instances = new NativeList<StructuralInstance>(Allocator.Temp);
                decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);

                for (int demo = 0; demo < Roles.Length; demo++)
                {
                    int rootDefinitionId = demo * 2;
                    var rootPlacement = new ExplicitPlacement
                    {
                        Position = int3.zero,
                        Orientation = 0,
                    };

                    StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                        in catalogue,
                        Seed + (uint)demo,
                        rootDefinitionId,
                        in rootPlacement,
                        instances,
                        decisions);

                    if (!ValidateAcceptedDemo(demo, in report, instances, decisions, out string error))
                    {
                        Fail(error);
                        return;
                    }

                    StageDemo(demo, in catalogue, instances, decisions);
                }

                if (!ValidateRequiredIncompatibleRejection(ref catalogue, instances, decisions, out string rejectionError))
                {
                    Fail(rejectionError);
                    return;
                }

                _complete = true;
                _passed = true;
                _detail = "PASS: 4 typed compositions + required incompatible rejection";
                Debug.Log("[TypedStructuralSocketComposition] " + _detail);
            }
            catch (Exception exception)
            {
                Fail(exception.GetType().Name + ": " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                if (decisions.IsCreated) decisions.Dispose();
                if (instances.IsCreated) instances.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
            }
        }

        private static bool ValidateAcceptedDemo(
            int demo,
            in StructuralCompositionReport report,
            NativeList<StructuralInstance> instances,
            NativeList<StructuralAttachmentDecision> decisions,
            out string error)
        {
            if (report.Result != StructuralCompositionResult.Ok)
            {
                error = Labels[demo] + " composition result was " + report.Result;
                return false;
            }

            if (report.ChildCount != 1 || instances.Length != 2 || decisions.Length != 1)
            {
                error = Labels[demo] + " expected exactly one solved attachment";
                return false;
            }

            StructuralInstance child = instances[1];
            StructuralAttachmentDecision decision = decisions[0];
            if (!decision.Accepted || decision.Rejection != StructuralAttachmentRejectReason.None)
            {
                error = Labels[demo] + " attachment was not accepted";
                return false;
            }

            if (child.ParentIndex != 0 || child.ParentSocketId != decision.SocketId ||
                !child.AttachmentPosition.Equals(decision.AttachmentPosition) ||
                !child.Position.Equals(decision.Position) ||
                child.Orientation != decision.Orientation)
            {
                error = Labels[demo] + " solved instance disagrees with production attachment decision";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateRequiredIncompatibleRejection(
            ref FeatureCatalogue catalogue,
            NativeList<StructuralInstance> instances,
            NativeList<StructuralAttachmentDecision> decisions,
            out string error)
        {
            SlotSpec original = catalogue.Slots[0];
            SlotSpec incompatible = original;
            incompatible.Offers = 1UL << 31;
            catalogue.Slots[0] = incompatible;

            try
            {
                var rootPlacement = new ExplicitPlacement
                {
                    Position = int3.zero,
                    Orientation = 0,
                };

                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue,
                    Seed,
                    0,
                    in rootPlacement,
                    instances,
                    decisions);

                if (report.Result != StructuralCompositionResult.Incompatible || decisions.Length != 1 ||
                    decisions[0].Accepted ||
                    decisions[0].Rejection != StructuralAttachmentRejectReason.IncompatibleRoleOrTags)
                {
                    error = "Required incompatible socket was not rejected by the production planner";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            finally
            {
                catalogue.Slots[0] = original;
            }
        }

        private static FeatureCatalogue BuildCatalogue(Allocator allocator)
        {
            const int definitionCount = 8;
            const int slotCount = 4;
            const int rootProgramLength = 5;
            const int childProgramLength = 2;
            const int totalProgramLength = 4 * (rootProgramLength + childProgramLength);

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitionCount,
                rules: 0,
                parameters: 0,
                anchors: 0,
                slots: slotCount,
                programLength: totalProgramLength,
                materials: 0,
                explicitPlacements: 0,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int demo = 0; demo < 4; demo++)
            {
                int rootDefinitionId = demo * 2;
                int childDefinitionId = rootDefinitionId + 1;
                ulong tag = 1UL << demo;

                catalogue.Definitions[rootDefinitionId] = Definition(
                    Labels[demo] + " root",
                    pieceId: (uint)(100 + rootDefinitionId),
                    Roles[demo],
                    tag,
                    new int3(4, 2, 4),
                    slotOffset: demo,
                    slotCount: 1,
                    programOffset,
                    rootProgramLength);

                WriteCallSlotProgram(catalogue.Program, programOffset);
                programOffset += rootProgramLength;

                catalogue.Definitions[childDefinitionId] = Definition(
                    Labels[demo] + " child",
                    pieceId: (uint)(100 + childDefinitionId),
                    Roles[demo],
                    tag,
                    new int3(3, 2, 3),
                    slotOffset: 0,
                    slotCount: 0,
                    programOffset,
                    childProgramLength);

                WriteEndProgram(catalogue.Program, programOffset);
                programOffset += childProgramLength;

                catalogue.Slots[demo] = new SlotSpec
                {
                    Name = Labels[demo] + " socket",
                    SocketId = (uint)(1000 + demo),
                    Role = Roles[demo],
                    Offers = tag,
                    Accepts = tag,
                    LocalPosition = new int3(4, 0, 1),
                    Facing = Facing.East,
                    DefinitionId = childDefinitionId,
                    LocalMin = new int3(4, 0, 1),
                    LocalMax = new int3(4, 0, 1),
                    ClearanceMin = int3.zero,
                    ClearanceMax = int3.zero,
                    CountMin = 1,
                    CountMax = 1,
                    Capacity = 1,
                    Spacing = 0,
                    Flags = StructuralSocketFlags.Required,
                };
            }

            return catalogue;
        }

        private static FeatureDefinition Definition(
            string name,
            uint pieceId,
            StructuralSocketRole role,
            ulong tag,
            int3 footprint,
            int slotOffset,
            int slotCount,
            int programOffset,
            int programLength)
        {
            return new FeatureDefinition
            {
                Name = name,
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = footprint,
                MaxSlope = 0,
                Precedence = 1,
                StructuralPiece = new StructuralPieceSpec
                {
                    PieceId = pieceId,
                    Role = role,
                    Offers = tag,
                    Accepts = tag,
                    LocalPosition = int3.zero,
                    Facing = Facing.West,
                    ClearanceMin = int3.zero,
                    ClearanceMax = int3.zero,
                },
                SlotOffset = slotOffset,
                SlotCount = slotCount,
                ProgramOffset = programOffset,
                ProgramLength = programLength,
                MaxPrimitives = 0,
            };
        }

        private static void WriteCallSlotProgram(NativeArray<int> program, int offset)
        {
            program[offset + 0] = (int)ShapeOp.CallSlot;
            program[offset + 1] = 0;
            program[offset + 2] = 0;
            program[offset + 3] = (int)ShapeOp.End;
            program[offset + 4] = 0;
        }

        private static void WriteEndProgram(NativeArray<int> program, int offset)
        {
            program[offset + 0] = (int)ShapeOp.End;
            program[offset + 1] = 0;
        }

        private void StageDemo(
            int demo,
            in FeatureCatalogue catalogue,
            NativeList<StructuralInstance> instances,
            NativeList<StructuralAttachmentDecision> decisions)
        {
            Vector3 laneOffset = new Vector3((demo - 1.5f) * LaneSpacing, 0f, 0f);
            GameObject lane = new GameObject(Labels[demo]);
            lane.transform.SetParent(transform, false);
            lane.transform.localPosition = laneOffset;

            for (int i = 0; i < instances.Length; i++)
            {
                StructuralInstance instance = instances[i];
                FeatureDefinition definition = catalogue.Definitions[instance.DefinitionId];
                Vector3 size = new Vector3(definition.Footprint.x, definition.Footprint.y, definition.Footprint.z);
                Vector3 origin = new Vector3(instance.Position.x, instance.Position.y, instance.Position.z);
                CreateBox(lane.transform, i == 0 ? "Root" : "Child", origin + size * 0.5f, size, i == 0 ? 0.35f : 0.7f);
            }

            StructuralAttachmentDecision decision = decisions[0];
            Vector3 socket = new Vector3(
                decision.AttachmentPosition.x,
                decision.AttachmentPosition.y + 0.35f,
                decision.AttachmentPosition.z);
            CreateMarker(lane.transform, "Accepted Socket", socket);
        }

        private static void CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, float brightness)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            Renderer renderer = box.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(brightness, brightness, brightness, 1f);
        }

        private static void CreateMarker(Transform parent, string name, Vector3 localPosition)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = Vector3.one * 0.7f;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.2f, 0.85f, 0.3f, 1f);
        }

        private void BuildGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Validation Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.6f, 2f);
            ground.transform.localScale = new Vector3(48f, 1f, 12f);
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.16f, 0.18f, 0.2f, 1f);
        }

        private static void ConfigureView()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.transform.position = new Vector3(0f, 19f, -29f);
            camera.transform.rotation = Quaternion.Euler(26f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.1f, 1f);

            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject lightObject = new GameObject("Directional Light");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            }
        }

        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
        }

        private void Fail(string detail)
        {
            _complete = true;
            _passed = false;
            _detail = "FAIL: " + detail;
            Debug.LogError("[TypedStructuralSocketComposition] " + _detail);
        }

        private void OnGUI()
        {
            GUIStyle headline = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
            };
            headline.normal.textColor = _passed ? Color.green : (_complete ? Color.red : Color.yellow);

            GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            body.normal.textColor = Color.white;

            GUI.Label(new Rect(24f, 20f, 900f, 38f), "Typed Structural Socket Composition", headline);
            GUI.Label(new Rect(24f, 58f, 1000f, 28f), _detail, body);
            GUI.Label(new Rect(24f, 88f, 1000f, 28f), "Bridge Span     Castle Tower     Cliff Platform     Civic Facade", body);
        }
    }
}
