import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "generated"
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

red = bpy.data.materials.new("HeadBand_Rambo_Red")
red.diffuse_color = (0.045, 0.0005, 0.001, 1.0)
red.use_nodes = True
bsdf = red.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value = (0.045, 0.0005, 0.001, 1.0)
bsdf.inputs["Roughness"].default_value = 0.9
bsdf.inputs["Sheen Weight"].default_value = 0.05
bsdf.inputs["Specular IOR Level"].default_value = 0.12


def make_band():
    segments, rows = 128, 7
    verts, faces, uvs = [], [], []
    for side in range(2):
        radial = 1.0 + side * 0.008
        for row in range(rows):
            v = row / (rows - 1)
            z = 0.09 + v * 0.052
            for segment in range(segments):
                u = segment / segments
                angle = math.tau * u
                wrinkle = 0.0012 * math.sin(angle * 7 + v * 4.2) + 0.0006 * math.sin(angle * 17 - v * 7)
                x = (0.13 * radial + wrinkle) * math.cos(angle)
                y = (0.07 * radial + wrinkle) * math.sin(angle)
                z_warp = z + 0.0015 * math.sin(angle * 3 + v * 8) + 0.0007 * math.sin(angle * 11)
                verts.append((x, y, z_warp))
                uvs.append((u, v))

    stride = rows * segments
    for side in range(2):
        base = side * stride
        for row in range(rows - 1):
            for segment in range(segments):
                nxt = (segment + 1) % segments
                a = base + row * segments + segment
                b = base + row * segments + nxt
                c = base + (row + 1) * segments + nxt
                d = base + (row + 1) * segments + segment
                faces.append((a, b, c, d) if side else (d, c, b, a))
    for row in (0, rows - 1):
        for segment in range(segments):
            nxt = (segment + 1) % segments
            outer = stride + row * segments
            inner = row * segments
            faces.append((inner + segment, inner + nxt, outer + nxt, outer + segment))

    mesh = bpy.data.meshes.new("HeadBand_Ribbon_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("HeadBand_Ribbon", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(red)
    bevel = obj.modifiers.new("Soft_Cloth_Edges", "BEVEL")
    bevel.width = 0.00045
    bevel.segments = 1
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)
    return obj


def ribbon_tail(name, offset, length, angle):
    width = 0.025
    points = []
    rows = 10
    for row in range(rows):
        t = row / (rows - 1)
        center = Vector((offset + math.sin(t * 3.2) * 0.004, 0.112 + t * 0.012, 0.105 - length * t))
        center.x += angle * t
        local_width = width * (1.0 - 0.18 * t)
        points.extend([center + Vector((-local_width, 0, 0)), center + Vector((local_width, 0, 0))])
    faces = [(r * 2, r * 2 + 1, r * 2 + 3, r * 2 + 2) for r in range(rows - 1)]
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(points, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(red)
    solid = obj.modifiers.new("Cloth_Thickness", "SOLIDIFY")
    solid.thickness = 0.0012
    bevel = obj.modifiers.new("Frayed_Soft_Edges", "BEVEL")
    bevel.width = 0.00035
    bevel.segments = 1
    return obj


band = make_band()
ribbon_tail("HeadBand_Tail_Long", -0.014, 0.155, -0.018)
ribbon_tail("HeadBand_Tail_Short", 0.018, 0.118, 0.014)

bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=(0, 0.116, 0.108))
knot = bpy.context.object
knot.name = "HeadBand_Knot"
knot.scale = (0.024, 0.012, 0.016)
knot.data.materials.append(red)
bpy.ops.object.shade_smooth()

for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "headband_rambo_red.blend"))
bpy.ops.export_scene.fbx(
    filepath=str(OUT / "headband_rambo_red.fbx"),
    use_selection=True,
    apply_unit_scale=True,
    bake_space_transform=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
)

# Transparent inventory-style preview.
bpy.ops.object.camera_add(location=(0.31, -0.35, 0.24))
camera = bpy.context.object
camera.data.lens = 60
direction = Vector((0, 0, 0.09)) - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
bpy.context.scene.camera = camera
bpy.ops.object.light_add(type="AREA", location=(0.15, -0.2, 0.42))
bpy.context.object.data.energy = 90
bpy.context.object.data.shape = "DISK"
bpy.context.object.data.size = 0.45
bpy.ops.object.light_add(type="AREA", location=(-0.25, 0.05, 0.25))
bpy.context.object.data.energy = 45
bpy.context.object.data.size = 0.35
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 512
scene.render.resolution_y = 512
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = True
scene.render.filepath = str(OUT / "headband_rambo_red_icon.png")
scene.view_settings.look = "AgX - Medium High Contrast"
scene.view_settings.exposure = -1.7
bpy.ops.render.render(write_still=True)
