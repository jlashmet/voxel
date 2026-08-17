using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Reusable functional interaction clusters for procedural structures.</summary>
    public static class WorldObjectMechanismPresets
    {
        public static void AddLeverDoor(WorldObjectAuthoringSession a, uint keyBase,
            DecorationBounds lever, int3 leverFacing, DecorationBounds door, int3 doorFacing, bool locked = false)
        {
            a.Place(keyBase + 0u, WorldObjectKind.Lever, lever, leverFacing);
            a.Place(keyBase + 1u, WorldObjectKind.Door, door, doorFacing,
                defaultState: locked ? WorldObjectStateFlags.Locked : WorldObjectStateFlags.None);
            if (locked)
                a.Connect(keyBase + 0u, WorldObjectSignal.Activated, keyBase + 1u, WorldObjectAction.Unlock);
            else
                a.Connect(keyBase + 0u, WorldObjectSignal.Activated, keyBase + 1u, WorldObjectAction.Open);
        }

        public static void AddSecretRoom(WorldObjectAuthoringSession a, uint keyBase,
            DecorationBounds switchBounds, int3 switchFacing, DecorationBounds wallBounds, int3 wallFacing)
        {
            a.Place(keyBase + 0u, WorldObjectKind.Switch, switchBounds, switchFacing,
                defaultState: WorldObjectStateFlags.Hidden);
            a.Place(keyBase + 1u, WorldObjectKind.SecretDoor, wallBounds, wallFacing,
                defaultState: WorldObjectStateFlags.Hidden);
            a.Connect(keyBase + 0u, WorldObjectSignal.Activated, keyBase + 1u, WorldObjectAction.Reveal);
            a.Connect(keyBase + 0u, WorldObjectSignal.Activated, keyBase + 1u, WorldObjectAction.Open);
        }

        public static void AddPressurePlateTrap(WorldObjectAuthoringSession a, uint keyBase,
            DecorationBounds plate, int3 plateFacing, DecorationBounds trap, int3 trapFacing, WorldObjectKind trapKind)
        {
            a.Place(keyBase + 0u, WorldObjectKind.PressurePlate, plate, plateFacing);
            a.Place(keyBase + 1u, trapKind, trap, trapFacing);
            a.Connect(keyBase + 0u, WorldObjectSignal.Pressed, keyBase + 1u, WorldObjectAction.Trigger);
            a.Connect(keyBase + 0u, WorldObjectSignal.Released, keyBase + 1u, WorldObjectAction.Reset);
        }

        public static void AddPoweredElevator(WorldObjectAuthoringSession a, uint keyBase,
            DecorationBounds generator, int3 generatorFacing, DecorationBounds elevator, int3 elevatorFacing,
            DecorationBounds lowerButton, DecorationBounds upperButton, int stopCount = 2)
        {
            a.Place(keyBase + 0u, WorldObjectKind.Generator, generator, generatorFacing);
            a.Place(keyBase + 1u, WorldObjectKind.Elevator, elevator, elevatorFacing, parameter0: stopCount);
            a.Place(keyBase + 2u, WorldObjectKind.Button, lowerButton, elevatorFacing);
            a.Place(keyBase + 3u, WorldObjectKind.Button, upperButton, -elevatorFacing);
            a.Connect(keyBase + 0u, WorldObjectSignal.Powered, keyBase + 1u, WorldObjectAction.PowerOn);
            a.Connect(keyBase + 0u, WorldObjectSignal.Unpowered, keyBase + 1u, WorldObjectAction.PowerOff);
            a.Connect(keyBase + 2u, WorldObjectSignal.Pressed, keyBase + 1u, WorldObjectAction.MoveToStop, 1);
            a.Connect(keyBase + 3u, WorldObjectSignal.Pressed, keyBase + 1u, WorldObjectAction.MoveToStop, 0);
        }

        public static void AddGatehouse(WorldObjectAuthoringSession a, uint keyBase,
            DecorationBounds lever, int3 leverFacing, DecorationBounds portcullis, int3 gateFacing,
            DecorationBounds drawbridge, DecorationBounds winch)
        {
            a.Place(keyBase + 0u, WorldObjectKind.Lever, lever, leverFacing);
            a.Place(keyBase + 1u, WorldObjectKind.Portcullis, portcullis, gateFacing);
            a.Place(keyBase + 2u, WorldObjectKind.Drawbridge, drawbridge, gateFacing);
            a.Place(keyBase + 3u, WorldObjectKind.Winch, winch, leverFacing);
            a.Connect(keyBase + 0u, WorldObjectSignal.Activated, keyBase + 1u, WorldObjectAction.Open);
            a.Connect(keyBase + 0u, WorldObjectSignal.Deactivated, keyBase + 1u, WorldObjectAction.Close);
            a.Connect(keyBase + 3u, WorldObjectSignal.Activated, keyBase + 2u, WorldObjectAction.Open);
            a.Connect(keyBase + 3u, WorldObjectSignal.Deactivated, keyBase + 2u, WorldObjectAction.Close);
        }

        public static void AddPoweredLights(WorldObjectAuthoringSession a, uint keyBase,
            DecorationBounds switchBounds, int3 switchFacing, DecorationBounds[] lights, int3 lightFacing,
            WorldObjectKind lightKind = WorldObjectKind.Torch)
        {
            a.Place(keyBase, WorldObjectKind.Switch, switchBounds, switchFacing);
            for (int i = 0; i < lights.Length; i++)
            {
                uint key = keyBase + 1u + (uint)i;
                a.Place(key, lightKind, lights[i], lightFacing);
                a.Connect(keyBase, WorldObjectSignal.Activated, key, WorldObjectAction.Activate);
                a.Connect(keyBase, WorldObjectSignal.Deactivated, key, WorldObjectAction.Deactivate);
            }
        }
    }
}
