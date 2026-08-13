"""
Чинит скелет учителей и переэкспортирует их.

ЗАЧЕМ. Скелет Quaternius собран под IK: стопа — это не звено ноги,
а отдельная «управляющая» кость, привязанная прямо к корню скелета,
чтобы её можно было ставить на землю независимо от бедра и голени.
Рядом с ней висят кости PT.L и PT.R — полюсные цели, они задают,
куда смотрит колено.

Для анимации в Blender это удобно. Для Unity — нет. Режим Humanoid
требует обычную цепочку: бедро → голень → стопа. Из-за того, что
стопа росла из корня, Unity отказывалась собирать аватар и писала:

    Transform 'Hips' is not an ancestor of 'UpperLeg.L'

а персонажи оставались в T-позе.

ЧТО ДЕЛАЕТ СКРИПТ. Перед экспортом привязывает стопы к голеням и
убирает служебные кости IK. Геометрия и веса не трогаются: меняется
только то, кто чей родитель.

ЗАПУСК (Blender закрыт):

    "/Users/Rafael/Desktop/Runner Test/Tools/Blender.app/Contents/MacOS/Blender" \\
      --background --python \\
      "/Users/Rafael/Desktop/CampusRush/ArtSource/BlenderScripts/fix_character_rig.py"

После него в Unity: Tools → Runner → Персонажи — починить скелет (T-поза).
"""

import bpy
import os
import traceback

ART = "/Users/Rafael/Desktop/CampusRush/ArtSource"
MEN_SOURCE = ART + "/Quaternius_Official/Ultimate_Modular_Men/Blends/Humans_Master.blend"
WOMEN_SOURCE = ART + "/Quaternius_Official/Ultimate_Modular_Women/Blends/All together.blend"
OUTPUT = "/Users/Rafael/Desktop/CampusRush/Assets/Resources/CampusRush/Characters"

# Кто к кому должен быть привязан. Слева — кость, справа — её новый родитель.
REPARENT = [
    ("Foot.L", "LowerLeg.L"),
    ("Foot.R", "LowerLeg.R"),
]

# Служебные кости IK. В Unity они не нужны и только мешают.
DROP = ["PT.L", "PT.R"]

LOG = []


def fix_armature():
    """Правит родителей костей. Возвращает список того, что изменилось."""
    armature = bpy.data.objects.get("CharacterArmature")
    if armature is None:
        raise RuntimeError("В файле нет объекта CharacterArmature")

    armature.hide_set(False)
    armature.hide_viewport = False
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")

    bones = armature.data.edit_bones
    changed = []

    for child_name, parent_name in REPARENT:
        child = bones.get(child_name)
        parent = bones.get(parent_name)

        if child is None or parent is None:
            changed.append(f"  ПРОПУЩЕНО {child_name}: кости нет в скелете")
            continue

        was = child.parent.name if child.parent else "ничего"
        if was == parent_name:
            changed.append(f"  {child_name}: уже растёт из {parent_name}")
            continue

        # use_connect=False обязательно: иначе Blender подтянет начало стопы
        # к концу голени и нога поедет. Нам нужна только смена родителя.
        child.use_connect = False
        child.parent = parent
        changed.append(f"  {child_name}: {was} -> {parent_name}")

    for name in DROP:
        bone = bones.get(name)
        if bone is not None:
            bones.remove(bone)
            changed.append(f"  {name}: убрана служебная кость IK")

    bpy.ops.object.mode_set(mode="OBJECT")
    return changed


def export_character(prefix, filename):
    bpy.ops.object.select_all(action="DESELECT")

    armature = bpy.data.objects["CharacterArmature"]
    armature.hide_set(False)
    armature.hide_viewport = False
    armature.select_set(True)

    meshes = 0
    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.name.startswith(prefix + "_"):
            obj.hide_set(False)
            obj.hide_viewport = False
            obj.select_set(True)
            meshes += 1

    if meshes == 0:
        raise RuntimeError(f"Для {prefix} не найдено ни одной меши")

    bpy.context.view_layer.objects.active = armature

    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUTPUT, filename),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        # ВАЖНО: было True. При True экспортёр выкидывал недеформирующие
        # кости и перевешивал их детей куда попало — это и было одной
        # из причин, почему стопа оказывалась у корня.
        use_armature_deform_only=False,
        bake_anim=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        path_mode="AUTO",
        embed_textures=False,
    )
    LOG.append(f"  экспортирован {filename} ({meshes} мешей)")


def process(source, characters):
    bpy.ops.wm.open_mainfile(filepath=source)
    LOG.append(os.path.basename(source) + ":")
    LOG.extend(fix_armature())
    for prefix, filename in characters:
        export_character(prefix, filename)


try:
    os.makedirs(OUTPUT, exist_ok=True)

    process(MEN_SOURCE, [
        ("Casual2", "CR_Rookie.fbx"),
        ("Adventurer", "CR_PE.fbx"),
        ("Suit", "CR_Principal.fbx"),
    ])

    process(WOMEN_SOURCE, [
        ("Formal", "CR_Math.fbx"),
        ("Worker", "CR_Chem.fbx"),
    ])

    report = "СКЕЛЕТ ПОЧИНЕН\n" + "\n".join(LOG)
    print(report)
    with open("/private/tmp/campus_rig_fix_done.txt", "w", encoding="utf-8") as f:
        f.write(report)

except Exception:
    err = traceback.format_exc()
    print(err)
    with open("/private/tmp/campus_rig_fix_error.txt", "w", encoding="utf-8") as f:
        f.write(err)
