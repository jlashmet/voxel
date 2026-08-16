from __future__ import annotations

from math import radians

import bpy
from mathutils import Quaternion, Vector


# A deliberately small, deterministic first-pass animation library for the
# Character Factory canonical skeleton. These clips are not intended to replace
# authored animation. Their job is to make every generated character immediately
# playable/previewable without requiring Mixamo, a browser, or manual retargeting.
REQUIRED_BONES = (
    "Hips",
    "Spine",
    "Chest",
    "Neck",
    "Head",
    "LeftUpperArm",
    "LeftLowerArm",
    "LeftHand",
    "RightUpperArm",
    "RightLowerArm",
    "RightHand",
    "LeftUpperLeg",
    "LeftLowerLeg",
    "LeftFoot",
    "RightUpperLeg",
    "RightLowerLeg",
    "RightFoot",
)

CLIP_NAMES = (
    "Idle",
    "Walk",
    "Run",
    "Cast",
    "StaffAttack",
)


def _require_bones(armature: bpy.types.Object) -> None:
    missing = [name for name in REQUIRED_BONES if armature.pose.bones.get(name) is None]
    if missing:
        raise RuntimeError(
            "Canonical armature is missing animation bones: " + ", ".join(missing)
        )


def _clear_pose(armature: bpy.types.Object) -> None:
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion((1.0, 0.0, 0.0, 0.0))
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def _local_axis_for_armature_axis(
    pose_bone: bpy.types.PoseBone,
    axis: tuple[float, float, float],
) -> Vector:
    # PoseBone quaternion rotations are expressed in the bone's local rest basis.
    # Convert a requested armature-space axis into that basis so the same helper
    # works for bones whose rest directions differ (notably left/right arms).
    rest_rotation = pose_bone.bone.matrix_local.to_3x3()
    local_axis = rest_rotation.inverted() @ Vector(axis)
    if local_axis.length_squared < 1e-10:
        raise RuntimeError(f"Degenerate rotation axis for bone {pose_bone.name}")
    return local_axis.normalized()


def _axis_quaternion(
    pose_bone: bpy.types.PoseBone,
    axis: tuple[float, float, float],
    degrees: float,
) -> Quaternion:
    return Quaternion(
        _local_axis_for_armature_axis(pose_bone, axis),
        radians(degrees),
    )


def _rotate(
    pose_bone: bpy.types.PoseBone,
    rotations: list[tuple[tuple[float, float, float], float]],
) -> None:
    result = Quaternion((1.0, 0.0, 0.0, 0.0))
    for axis, degrees in rotations:
        result = _axis_quaternion(pose_bone, axis, degrees) @ result
    pose_bone.rotation_quaternion = result.normalized()


def _arms_relaxed(armature: bpy.types.Object, swing_degrees: float = 0.0) -> None:
    left = armature.pose.bones["LeftUpperArm"]
    right = armature.pose.bones["RightUpperArm"]
    # The canonical bind pose is a T-pose. Drop each arm toward the body, then
    # optionally swing it around the character's lateral axis for locomotion.
    _rotate(left, [((0.0, 1.0, 0.0), 78.0), ((1.0, 0.0, 0.0), swing_degrees)])
    _rotate(right, [((0.0, 1.0, 0.0), -78.0), ((1.0, 0.0, 0.0), swing_degrees)])

    _rotate(armature.pose.bones["LeftLowerArm"], [((0.0, 1.0, 0.0), -8.0)])
    _rotate(armature.pose.bones["RightLowerArm"], [((0.0, 1.0, 0.0), 8.0)])


def _locomotion_pose(
    armature: bpy.types.Object,
    left_leg: float,
    right_leg: float,
    left_knee: float,
    right_knee: float,
    arm_swing: float,
    hips_z: float,
    chest_twist: float,
) -> None:
    _clear_pose(armature)
    _arms_relaxed(armature, swing_degrees=arm_swing)

    _rotate(armature.pose.bones["LeftUpperLeg"], [((1.0, 0.0, 0.0), left_leg)])
    _rotate(armature.pose.bones["RightUpperLeg"], [((1.0, 0.0, 0.0), right_leg)])
    _rotate(armature.pose.bones["LeftLowerLeg"], [((1.0, 0.0, 0.0), left_knee)])
    _rotate(armature.pose.bones["RightLowerLeg"], [((1.0, 0.0, 0.0), right_knee)])
    _rotate(armature.pose.bones["Chest"], [((0.0, 0.0, 1.0), chest_twist)])
    armature.pose.bones["Hips"].location.z = hips_z


def _insert_key(
    armature: bpy.types.Object,
    frame: int,
    animated_bones: tuple[str, ...] = REQUIRED_BONES,
) -> None:
    for name in animated_bones:
        bone = armature.pose.bones[name]
        bone.keyframe_insert(
            data_path="rotation_quaternion",
            frame=frame,
            group=name,
        )
        if name == "Hips":
            bone.keyframe_insert(data_path="location", frame=frame, group=name)


def _new_action(armature: bpy.types.Object, name: str) -> bpy.types.Action:
    action = bpy.data.actions.get(name)
    if action is not None:
        bpy.data.actions.remove(action)
    action = bpy.data.actions.new(name=name)
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    return action


def _finish_action(action: bpy.types.Action, loop: bool) -> None:
    action["characterFactoryLoop"] = bool(loop)

    # Blender 5.x moved Actions to the layered/slotted animation model and no
    # longer exposes Action.fcurves directly. Key insertion above already gives
    # us usable interpolation, so curve tweaking is an optional compatibility
    # enhancement rather than a requirement for exporting the clips. Keep the
    # legacy path for Blender 4.x and simply leave Blender 5.x defaults intact.
    curves = getattr(action, "fcurves", None)
    if curves is None:
        return

    for curve in curves:
        for point in curve.keyframe_points:
            point.interpolation = "BEZIER"
            if loop:
                point.handle_left_type = "AUTO_CLAMPED"
                point.handle_right_type = "AUTO_CLAMPED"


def _idle(armature: bpy.types.Object) -> None:
    action = _new_action(armature, "Idle")
    for frame, hips_z, chest_pitch, head_yaw in (
        (1, 0.0, -0.8, -1.2),
        (30, 0.012, 1.0, 1.2),
        (60, 0.0, -0.8, -1.2),
    ):
        _clear_pose(armature)
        _arms_relaxed(armature)
        armature.pose.bones["Hips"].location.z = hips_z
        _rotate(armature.pose.bones["Chest"], [((1.0, 0.0, 0.0), chest_pitch)])
        _rotate(armature.pose.bones["Head"], [((0.0, 0.0, 1.0), head_yaw)])
        _insert_key(armature, frame)
    _finish_action(action, loop=True)


def _walk(armature: bpy.types.Object) -> None:
    action = _new_action(armature, "Walk")
    poses = (
        (1, 26.0, -26.0, 8.0, 34.0, -18.0, 0.0, -3.0),
        (7, 0.0, 0.0, 24.0, 24.0, 0.0, 0.032, 0.0),
        (13, -26.0, 26.0, 34.0, 8.0, 18.0, 0.0, 3.0),
        (19, 0.0, 0.0, 24.0, 24.0, 0.0, 0.032, 0.0),
        (25, 26.0, -26.0, 8.0, 34.0, -18.0, 0.0, -3.0),
    )
    for values in poses:
        frame, ll, rl, lk, rk, arm, z, twist = values
        _locomotion_pose(armature, ll, rl, lk, rk, arm, z, twist)
        _insert_key(armature, frame)
    _finish_action(action, loop=True)


def _run(armature: bpy.types.Object) -> None:
    action = _new_action(armature, "Run")
    poses = (
        (1, 42.0, -36.0, 10.0, 58.0, -34.0, 0.0, -5.0),
        (5, 8.0, -8.0, 34.0, 38.0, -8.0, 0.055, -1.0),
        (9, -36.0, 42.0, 58.0, 10.0, 34.0, 0.0, 5.0),
        (13, -8.0, 8.0, 38.0, 34.0, 8.0, 0.055, 1.0),
        (17, 42.0, -36.0, 10.0, 58.0, -34.0, 0.0, -5.0),
    )
    for values in poses:
        frame, ll, rl, lk, rk, arm, z, twist = values
        _locomotion_pose(armature, ll, rl, lk, rk, arm, z, twist)
        _insert_key(armature, frame)
    _finish_action(action, loop=True)


def _cast(armature: bpy.types.Object) -> None:
    action = _new_action(armature, "Cast")
    for frame, reach, lift, chest_pitch in (
        (1, 0.0, 0.0, 0.0),
        (10, 40.0, 8.0, -3.0),
        (18, 74.0, 18.0, -7.0),
        (28, 82.0, 24.0, -9.0),
        (40, 0.0, 0.0, 0.0),
    ):
        _clear_pose(armature)
        if frame in (1, 40):
            _arms_relaxed(armature)
        else:
            left = armature.pose.bones["LeftUpperArm"]
            right = armature.pose.bones["RightUpperArm"]
            # Swing both arms from the bind-pose sides toward the character's
            # forward direction (-Y in the canonical fixture), with a slight lift.
            _rotate(left, [((0.0, 0.0, 1.0), -reach), ((1.0, 0.0, 0.0), -lift)])
            _rotate(right, [((0.0, 0.0, 1.0), reach), ((1.0, 0.0, 0.0), -lift)])
            _rotate(armature.pose.bones["LeftLowerArm"], [((1.0, 0.0, 0.0), 18.0)])
            _rotate(armature.pose.bones["RightLowerArm"], [((1.0, 0.0, 0.0), 18.0)])
        _rotate(armature.pose.bones["Chest"], [((1.0, 0.0, 0.0), chest_pitch)])
        _insert_key(armature, frame)
    _finish_action(action, loop=False)


def _staff_attack(armature: bpy.types.Object) -> None:
    action = _new_action(armature, "StaffAttack")
    for frame, right_forward, right_lift, chest_yaw in (
        (1, 0.0, 0.0, 0.0),
        (8, -36.0, 58.0, -12.0),
        (14, 48.0, -18.0, 18.0),
        (22, 76.0, -34.0, 24.0),
        (32, 0.0, 0.0, 0.0),
    ):
        _clear_pose(armature)
        _arms_relaxed(armature)
        if frame not in (1, 32):
            right = armature.pose.bones["RightUpperArm"]
            _rotate(
                right,
                [
                    ((0.0, 0.0, 1.0), right_forward),
                    ((1.0, 0.0, 0.0), right_lift),
                ],
            )
            _rotate(
                armature.pose.bones["RightLowerArm"],
                [((1.0, 0.0, 0.0), 22.0)],
            )
            _rotate(
                armature.pose.bones["Chest"],
                [((0.0, 0.0, 1.0), chest_yaw)],
            )
        _insert_key(armature, frame)
    _finish_action(action, loop=False)


def add_gameplay_animation_set(armature: bpy.types.Object) -> tuple[str, ...]:
    if armature.type != "ARMATURE":
        raise RuntimeError("Gameplay animations require an armature object")

    _require_bones(armature)
    _idle(armature)
    _walk(armature)
    _run(armature)
    _cast(armature)
    _staff_attack(armature)

    # Leave the scene in a neutral bind pose. The FBX exporter will bake every
    # action independently because export_fbx(..., bake_anim=True) enables
    # bake_anim_use_all_actions.
    _clear_pose(armature)
    armature.animation_data.action = bpy.data.actions.get("Idle")
    print("gameplay animations: " + ", ".join(CLIP_NAMES), flush=True)
    return CLIP_NAMES
