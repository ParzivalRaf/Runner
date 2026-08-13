import bpy
import os


MEN_SOURCE = "/Users/Rafael/Desktop/CampusRush/ArtSource/Quaternius_Official/Ultimate_Modular_Men/Blends/Humans_Master.blend"
WOMEN_SOURCE = "/Users/Rafael/Desktop/CampusRush/ArtSource/Quaternius_Official/Ultimate_Modular_Women/Blends/All together.blend"
OUTPUT = "/Users/Rafael/Desktop/CampusRush/Assets/Resources/CampusRush/Characters"


def select_character(prefix):
    bpy.ops.object.select_all(action="DESELECT")
    armature = bpy.data.objects.get("CharacterArmature")
    if armature is None:
        raise RuntimeError("CharacterArmature is missing")

    armature.hide_set(False)
    armature.hide_viewport = False
    armature.hide_render = False
    armature.select_set(True)

    selected_meshes = []
    for obj in bpy.data.objects:
        if obj.type != "MESH" or not obj.name.startswith(prefix + "_"):
            continue
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.select_set(True)
        selected_meshes.append(obj)

    if not selected_meshes:
        raise RuntimeError(f"No meshes found for {prefix}")

    bpy.context.view_layer.objects.active = armature
    return armature, selected_meshes


def export_character(prefix, filename):
    select_character(prefix)
    path = os.path.join(OUTPUT, filename)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        path_mode="AUTO",
        embed_textures=False,
    )
    print("CAMPUS_RUSH_EXPORTED", path)


os.makedirs(OUTPUT, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=MEN_SOURCE)
export_character("Casual2", "CR_Rookie.fbx")
export_character("Adventurer", "CR_PE.fbx")
export_character("Suit", "CR_Principal.fbx")

bpy.ops.wm.open_mainfile(filepath=WOMEN_SOURCE)
export_character("Formal", "CR_Math.fbx")
export_character("Worker", "CR_Chem.fbx")

with open("/private/tmp/campus_characters_done.txt", "w", encoding="utf-8") as marker:
    marker.write("rookie\npe\nprincipal\nmath\nchem\n")

print("CAMPUS_RUSH_CHARACTER_EXPORT_COMPLETE")
