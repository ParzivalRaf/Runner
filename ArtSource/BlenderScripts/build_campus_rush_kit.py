import math
import os

import bpy


OUTPUT_DIR = "/Users/Rafael/Desktop/CampusRush/Assets/Resources/CampusRush"
SOURCE_BLEND = "/Users/Rafael/Desktop/CampusRush/ArtSource/CampusRush_Kit.blend"


PALETTE = {
    "Cream": (0.92, 0.82, 0.66, 1.0),
    "Terracotta": (0.72, 0.22, 0.12, 1.0),
    "Cobalt": (0.05, 0.25, 0.62, 1.0),
    "Teal": (0.02, 0.43, 0.39, 1.0),
    "Gold": (0.96, 0.59, 0.10, 1.0),
    "Ink": (0.045, 0.075, 0.12, 1.0),
    "Wood": (0.39, 0.18, 0.08, 1.0),
    "Leaf": (0.08, 0.43, 0.24, 1.0),
    "Glass": (0.30, 0.70, 0.78, 1.0),
}


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for block in list(datablocks):
            datablocks.remove(block)


def material(name):
    existing = bpy.data.materials.get("CR_" + name)
    if existing:
        return existing
    mat = bpy.data.materials.new("CR_" + name)
    mat.diffuse_color = PALETTE[name]
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = PALETTE[name]
    bsdf.inputs["Roughness"].default_value = 0.58
    bsdf.inputs["Metallic"].default_value = 0.08 if name in ("Ink", "Gold") else 0.0
    return mat


def finish_object(obj, color, bevel=0.06):
    obj.data.materials.append(material(color))
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        modifier = obj.modifiers.new("Soft_Edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    return obj


def rounded_box(name, location, dimensions, color, bevel=0.06, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    return finish_object(obj, color, min(bevel, min(dimensions) * 0.35))


def cylinder(name, location, radius, depth, color, vertices=16, rotation=(0, 0, 0), bevel=0.035):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                        location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, color, bevel)


def sphere(name, location, radius, color, subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    return finish_object(obj, color, 0.02)


def export_model(filename):
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUTPUT_DIR, filename + ".fbx"),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="AUTO",
    )


def build_locker():
    reset_scene()
    rounded_box("Body_Terracotta", (0, 0, 1.33), (1.48, 0.76, 2.66), "Terracotta", 0.12)
    rounded_box("Door_Cream", (0, -0.395, 1.34), (1.27, 0.055, 2.38), "Cream", 0.025)
    rounded_box("Header_Cobalt", (0, -0.43, 2.30), (1.05, 0.04, 0.28), "Cobalt", 0.025)
    for x in (-0.34, 0.0, 0.34):
        rounded_box("Vent_Ink", (x, -0.435, 1.91), (0.22, 0.035, 0.055), "Ink", 0.012)
    rounded_box("Handle_Gold", (0.48, -0.445, 1.25), (0.09, 0.05, 0.30), "Gold", 0.02)
    rounded_box("Foot_Ink_L", (-0.52, 0, 0.08), (0.25, 0.70, 0.16), "Ink", 0.04)
    rounded_box("Foot_Ink_R", (0.52, 0, 0.08), (0.25, 0.70, 0.16), "Ink", 0.04)
    export_model("CR_Locker")


def build_hurdle():
    reset_scene()
    rounded_box("Foot_Ink_L", (-0.74, 0, 0.08), (0.34, 0.72, 0.16), "Ink", 0.05)
    rounded_box("Foot_Ink_R", (0.74, 0, 0.08), (0.34, 0.72, 0.16), "Ink", 0.05)
    rounded_box("Post_Cobalt_L", (-0.70, 0, 0.48), (0.16, 0.20, 0.86), "Cobalt", 0.045)
    rounded_box("Post_Cobalt_R", (0.70, 0, 0.48), (0.16, 0.20, 0.86), "Cobalt", 0.045)
    rounded_box("Bar_Cream", (0, 0, 0.83), (1.62, 0.24, 0.22), "Cream", 0.07)
    rounded_box("Stripe_Terracotta_L", (-0.43, -0.13, 0.83), (0.23, 0.035, 0.15), "Terracotta", 0.018,
                rotation=(0, math.radians(24), 0))
    rounded_box("Stripe_Terracotta_R", (0.43, -0.13, 0.83), (0.23, 0.035, 0.15), "Terracotta", 0.018,
                rotation=(0, math.radians(24), 0))
    export_model("CR_Hurdle")


def build_slide_gate():
    reset_scene()
    rounded_box("Post_Teal_L", (-0.94, 0, 0.92), (0.18, 0.34, 1.84), "Teal", 0.065)
    rounded_box("Post_Teal_R", (0.94, 0, 0.92), (0.18, 0.34, 1.84), "Teal", 0.065)
    rounded_box("Beam_Terracotta", (0, 0, 1.76), (2.04, 0.46, 0.34), "Terracotta", 0.10)
    rounded_box("Panel_Cream", (0, -0.25, 1.76), (1.17, 0.035, 0.19), "Cream", 0.018)
    for x in (-0.42, 0.42):
        rounded_box("Warning_Gold", (x, -0.275, 1.76), (0.22, 0.025, 0.10), "Gold", 0.018,
                    rotation=(0, math.radians(24), 0))
    export_model("CR_SlideGate")


def build_bench():
    reset_scene()
    rounded_box("Seat_Wood", (0, 0, 0.58), (2.25, 0.72, 0.18), "Wood", 0.08)
    rounded_box("Back_Cream", (0, 0.29, 1.12), (2.25, 0.16, 0.78), "Cream", 0.07,
                rotation=(math.radians(-7), 0, 0))
    for x in (-0.82, 0.82):
        rounded_box("Leg_Ink", (x, 0, 0.30), (0.16, 0.52, 0.58), "Ink", 0.05)
    export_model("CR_Bench")


def build_planter():
    reset_scene()
    bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.62, radius2=0.48, depth=0.78,
                                    location=(0, 0, 0.39))
    finish_object(bpy.context.object, "Terracotta", 0.045).name = "Pot_Terracotta"
    cylinder("Trunk_Wood", (0, 0, 1.22), 0.12, 1.22, "Wood", vertices=10, bevel=0.025)
    sphere("Leaf_Teal_A", (0, 0, 1.90), 0.57, "Leaf", 2)
    sphere("Leaf_Teal_B", (-0.33, 0.03, 1.68), 0.39, "Teal", 2)
    sphere("Leaf_Green_C", (0.35, -0.05, 1.72), 0.42, "Leaf", 2)
    export_model("CR_Planter")


def build_lamp():
    reset_scene()
    cylinder("Base_Ink", (0, 0, 0.10), 0.32, 0.20, "Ink", vertices=16)
    cylinder("Post_Cobalt", (0, 0, 1.70), 0.09, 3.25, "Cobalt", vertices=12)
    rounded_box("Arm_Cobalt", (0.30, 0, 3.14), (0.68, 0.12, 0.12), "Cobalt", 0.04)
    rounded_box("Lamp_Cream", (0.58, 0, 2.94), (0.48, 0.42, 0.28), "Cream", 0.08)
    rounded_box("Light_Gold", (0.58, -0.22, 2.91), (0.32, 0.025, 0.13), "Gold", 0.01)
    export_model("CR_Lamp")


def build_archway():
    reset_scene()
    rounded_box("Pillar_Cream_L", (-2.35, 0, 1.85), (0.50, 0.72, 3.70), "Cream", 0.12)
    rounded_box("Pillar_Cream_R", (2.35, 0, 1.85), (0.50, 0.72, 3.70), "Cream", 0.12)
    rounded_box("Header_Terracotta", (0, 0, 3.58), (5.15, 0.78, 0.58), "Terracotta", 0.16)
    rounded_box("Sign_Cobalt", (0, -0.43, 3.58), (2.25, 0.06, 0.33), "Cobalt", 0.035)
    sphere("Badge_Gold", (0, -0.49, 3.58), 0.20, "Gold", 2)
    export_model("CR_Archway")


def main():
    builders = (build_locker, build_hurdle, build_slide_gate,
                build_bench, build_planter, build_lamp, build_archway)
    for builder in builders:
        builder()

    # Preserve the final authored source in case the meshes need manual polish.
    bpy.ops.wm.save_as_mainfile(filepath=SOURCE_BLEND)
    print("Campus Rush kit exported to", OUTPUT_DIR)


if __name__ == "__main__":
    main()
