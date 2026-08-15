import argparse
import sys
from mathutils import Vector
import bpy


def scene_bounds(meshes):
    points = []
    for obj in meshes:
        for corner in obj.bound_box:
            points.append(obj.matrix_world @ Vector(corner))
    if not points:
        raise RuntimeError("no mesh bounds to render")
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return lo, hi


def look_at(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


def script_args():
    argv = sys.argv
    if '--' in argv:
        argv = argv[argv.index('--') + 1:]
    else:
        argv = []
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    return parser.parse_args(argv)


def main():
    args = script_args()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=args.input)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError('FBX contains no mesh objects')

    lo, hi = scene_bounds(meshes)
    center = (lo + hi) * 0.5
    size = hi - lo
    radius = max(size.x, size.y, size.z) * 0.5
    if radius <= 0:
        radius = 1.0

    # Neutral studio setup. This renders the actual generated FBX; it does not
    # generate or repaint the asset.
    world = bpy.data.worlds.new('World')
    world.use_nodes = True
    bg = world.node_tree.nodes.get('Background')
    bg.inputs['Color'].default_value = (0.055, 0.055, 0.055, 1.0)
    bg.inputs['Strength'].default_value = 0.65
    bpy.context.scene.world = world

    camera_data = bpy.data.cameras.new('Camera')
    camera = bpy.data.objects.new('Camera', camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = center + Vector((radius * 2.35, -radius * 2.35, radius * 1.45))
    camera.data.lens = 55
    look_at(camera, center)
    bpy.context.scene.camera = camera

    def add_area(name, location, energy, size_value):
        light_data = bpy.data.lights.new(name=name, type='AREA')
        light_data.energy = energy
        light_data.shape = 'DISK'
        light_data.size = size_value
        light = bpy.data.objects.new(name, light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        look_at(light, center)

    add_area('Key', center + Vector((radius * 2.0, -radius * 1.5, radius * 2.2)), 1200, radius * 1.4)
    add_area('Fill', center + Vector((-radius * 1.5, -radius * 0.7, radius * 1.0)), 650, radius * 1.8)
    add_area('Rim', center + Vector((0, radius * 2.0, radius * 1.8)), 900, radius * 1.2)

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = args.output
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.look = 'AgX - Medium High Contrast'

    bpy.ops.render.render(write_still=True)
    print(args.output)


if __name__ == '__main__':
    main()
