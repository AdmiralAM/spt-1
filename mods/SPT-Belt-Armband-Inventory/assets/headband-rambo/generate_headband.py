import bpy
import math
import struct
import zlib
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "generated"
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

red = bpy.data.materials.new("HeadBand_Rambo_Red")
red.diffuse_color = (0.12, 0.001, 0.002, 1.0)
red.use_nodes = True
bsdf = red.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value = (0.22, 0.003, 0.006, 1.0)
bsdf.inputs["Roughness"].default_value = 0.9
bsdf.inputs["Sheen Weight"].default_value = 0.05
bsdf.inputs["Specular IOR Level"].default_value = 0.12


def create_texture_maps(size=512):
    albedo_pixels, normal_pixels, roughness_pixels = bytearray(), bytearray(), bytearray()
    for y in range(size):
        for x in range(size):
            warp = math.sin(x * math.tau / 5.0)
            weft = math.sin(y * math.tau / 4.0)
            broad = math.sin((x + y) * math.tau / 73.0) * 0.5 + math.sin((x - y) * math.tau / 119.0) * 0.5
            fiber = 0.84 + 0.012 * warp + 0.010 * weft + 0.025 * broad
            albedo_pixels.extend((int(255 * 0.56 * fiber), int(255 * 0.022 * fiber), int(255 * 0.030 * fiber), 255))
            nx = 0.045 * math.cos(x * math.tau / 5.0)
            ny = 0.040 * math.cos(y * math.tau / 4.0)
            nz = math.sqrt(max(0.0, 1.0 - nx * nx - ny * ny))
            normal_pixels.extend((int(255 * (0.5 + nx * 0.5)), int(255 * (0.5 + ny * 0.5)), int(255 * (0.5 + nz * 0.5)), 255))
            value = 0.88 + 0.035 * warp * weft
            byte_value = int(255 * value)
            roughness_pixels.extend((byte_value, byte_value, byte_value, 255))

    def write_png(path, pixels):
        def chunk(kind, data):
            return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
        rows = b"".join(b"\x00" + bytes(pixels[y * size * 4:(y + 1) * size * 4]) for y in range(size))
        payload = b"\x89PNG\r\n\x1a\n"
        payload += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
        payload += chunk(b"IDAT", zlib.compress(rows, 9))
        payload += chunk(b"IEND", b"")
        path.write_bytes(payload)

    for pixels, filename in (
        (albedo_pixels, "headband_rambo_red_albedo.png"),
        (normal_pixels, "headband_rambo_red_normal.png"),
        (roughness_pixels, "headband_rambo_red_roughness.png"),
    ):
        write_png(OUT / filename, pixels)

    albedo = bpy.data.images.load(str(OUT / "headband_rambo_red_albedo.png"))
    normal = bpy.data.images.load(str(OUT / "headband_rambo_red_normal.png"))
    roughness = bpy.data.images.load(str(OUT / "headband_rambo_red_roughness.png"))
    normal.colorspace_settings.name = "Non-Color"
    roughness.colorspace_settings.name = "Non-Color"

    albedo_node = red.node_tree.nodes.new("ShaderNodeTexImage")
    albedo_node.image = albedo
    red.node_tree.links.new(albedo_node.outputs["Color"], bsdf.inputs["Base Color"])
    normal_node = red.node_tree.nodes.new("ShaderNodeTexImage")
    normal_node.image = normal
    normal_map = red.node_tree.nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value = 0.18
    red.node_tree.links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    red.node_tree.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    roughness_node = red.node_tree.nodes.new("ShaderNodeTexImage")
    roughness_node.image = roughness
    red.node_tree.links.new(roughness_node.outputs["Color"], bsdf.inputs["Roughness"])

    return albedo, normal, roughness


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
        center = Vector((offset + math.sin(t * 3.2) * 0.004, 0.069 + t * 0.008, 0.112 - length * t))
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

bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1, location=(0, 0.071, 0.112))
knot = bpy.context.object
knot.name = "HeadBand_Knot"
knot.scale = (0.026, 0.011, 0.010)
knot.rotation_euler = (math.radians(8), math.radians(-12), math.radians(18))
knot.data.materials.append(red)
bpy.ops.object.shade_smooth()

for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(False)

# Make the ribbon, knot and both tails one inspectable mesh, then generate a
# stable UV map and center the actual geometry around the rotation pivot.
bpy.ops.object.select_all(action="DESELECT")
mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
for obj in mesh_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = band
bpy.ops.object.join()
band = bpy.context.object
band.name = "HeadBand_Rambo_Red"
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.02)
bpy.ops.object.mode_set(mode="OBJECT")

world_corners = [band.matrix_world @ Vector(corner) for corner in band.bound_box]
center = sum(world_corners, Vector()) / 8.0
for vertex in band.data.vertices:
    vertex.co -= center
band.location = Vector((0.0, 0.0, 0.0))

create_texture_maps()

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
    path_mode="COPY",
    embed_textures=False,
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

for label, position in (
    ("front", (0.31, -0.35, 0.24)),
    ("side", (0.42, 0.0, 0.18)),
    ("back", (0.27, 0.36, 0.23)),
):
    camera.location = position
    camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(OUT / f"headband_rambo_red_preview_{label}.png")
    bpy.ops.render.render(write_still=True)
