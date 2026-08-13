import bpy
import os
import traceback

OUTPUT = "/Users/Rafael/Desktop/CampusRush/Assets/Resources/CampusRush/Characters"

def export(prefix, filename):
    bpy.ops.object.select_all(action="DESELECT")
    armature = bpy.data.objects["CharacterArmature"]
    armature.hide_set(False)
    armature.select_set(True)
    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.name.startswith(prefix + "_"):
            obj.hide_set(False)
            obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUTPUT, filename), use_selection=True,
        object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
        add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", path_mode="AUTO")

try:
    os.makedirs(OUTPUT, exist_ok=True)
    export("Formal", "CR_Math.fbx")
    export("Worker", "CR_Chem.fbx")
    open("/private/tmp/campus_women_done.txt", "w").write("done\n")
except Exception:
    open("/private/tmp/campus_women_error.txt", "w").write(traceback.format_exc())

