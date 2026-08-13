import bpy
import math
import os
import traceback
from mathutils import Vector

OUT = "/Users/Rafael/Desktop/CampusRush/Assets/Resources/CampusRush/HeroKit"
BLEND = "/Users/Rafael/Desktop/CampusRush/ArtSource/CampusRush_HeroKit.blend"

COLORS = {
    "Brick": (0.74, 0.20, 0.10, 1), "BrickLight": (0.92, 0.36, 0.18, 1),
    "Cream": (0.94, 0.82, 0.62, 1), "Stone": (0.78, 0.73, 0.63, 1),
    "Cobalt": (0.025, 0.18, 0.47, 1), "Navy": (0.015, 0.055, 0.12, 1),
    "Teal": (0.025, 0.36, 0.34, 1), "Gold": (1.0, 0.55, 0.035, 1),
    "Glass": (0.05, 0.24, 0.34, 1), "Metal": (0.14, 0.18, 0.22, 1),
    "Red": (0.78, 0.075, 0.035, 1), "Leaf": (0.20, 0.48, 0.14, 1),
    "LeafLight": (0.42, 0.65, 0.18, 1), "Wood": (0.32, 0.12, 0.045, 1),
    "White": (0.96, 0.94, 0.85, 1),
}


def material(name):
    mat = bpy.data.materials.get("CR_" + name)
    if mat: return mat
    mat = bpy.data.materials.new("CR_" + name)
    mat.diffuse_color = COLORS[name]
    mat.roughness = 0.46 if name not in {"Glass", "Metal", "Gold"} else 0.25
    mat.metallic = 0.55 if name in {"Metal", "Gold"} else 0.0
    return mat


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def link_only(obj, coll):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    coll.objects.link(obj)


def box(coll, name, loc, scale, mat, bevel=0.08, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; o.scale = (scale[0]/2, scale[1]/2, scale[2]/2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = o.modifiers.new("SoftEdges", "BEVEL"); mod.width=bevel; mod.segments=2
        bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(material(mat)); link_only(o,coll); return o


def cyl(coll,name,loc,radius,depth,mat,rot=(0,0,0),verts=20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o=bpy.context.object; o.name=name; o.data.materials.append(material(mat)); link_only(o,coll)
    bevel=o.modifiers.new("SoftEdges","BEVEL"); bevel.width=min(radius*.12,.06); bevel.segments=2
    bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_apply(modifier=bevel.name); return o


def sphere(coll,name,loc,scale,mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=loc)
    o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(material(mat));link_only(o,coll);return o


def text_obj(coll,name,text,loc,size,depth,mat,rot=(math.pi/2,0,0)):
    bpy.ops.object.text_add(location=loc,rotation=rot)
    o=bpy.context.object;o.name=name;o.data.body=text;o.data.align_x='CENTER';o.data.align_y='CENTER';o.data.size=size;o.data.extrude=depth;o.data.bevel_depth=.015
    o.data.materials.append(material(mat));link_only(o,coll);return o


def window_grid(coll, front_z, floors, cols, width, start_y, gap_y, mat="Glass"):
    gap_x=width/(cols+1)
    for floor in range(floors):
        y=start_y+floor*gap_y
        for col in range(cols):
            x=-width/2+gap_x*(col+1)
            box(coll,f"Glass_Window_{floor}_{col}",(x,y,front_z),(gap_x*.52,1.0,.10),mat,.035)
            box(coll,f"Cream_Sill_{floor}_{col}",(x,y-.60,front_z-.005),(gap_x*.68,.10,.15),"Cream",.025)


def side_window_grid(coll, side_x, floors, rows, depth, start_y, gap_y):
    gap_z=depth/(rows+1)
    for floor in range(floors):
        y=start_y+floor*gap_y
        for row in range(rows):
            z=gap_z*(row+1)
            box(coll,f"Glass_SideWindow_{side_x}_{floor}_{row}",(side_x,y,z),(.10,1.0,gap_z*.52),"Glass",.035)
            box(coll,f"Cream_SideSill_{side_x}_{floor}_{row}",(side_x,y-.60,z),(.15,.10,gap_z*.68),"Cream",.025)


def building_a():
    c=collection("CR_CampusBuilding_A")
    box(c,"Brick_Main",(0,3.2,3.6),(6.6,6.4,7.2),"Brick",.16)
    box(c,"Cream_Base",(0,.38,3.6),(7.0,.76,7.5),"Stone",.09)
    box(c,"Cream_Cornice",(0,6.25,3.6),(7.15,.35,7.55),"Cream",.08)
    window_grid(c,-.055,3,4,6.0,1.45,1.65)
    window_grid(c,7.255,3,4,6.0,1.45,1.65)
    side_window_grid(c,-3.355,3,4,7.2,1.45,1.65)
    side_window_grid(c,3.355,3,4,7.2,1.45,1.65)
    box(c,"Navy_Door",(0,1.25,-.12),(1.35,2.45,.16),"Navy",.10)
    box(c,"Cream_DoorArch",(0,2.56,-.16),(2.0,.22,.22),"Cream",.07)
    for x in (-3.05,3.05): box(c,"Cream_Corner",(x,3.3,3.6),(.24,5.8,7.28),"Cream",.05)
    return c


def building_b():
    c=collection("CR_CampusBuilding_B")
    box(c,"BrickLight_Main",(0,2.75,3.3),(7.2,5.5,6.6),"BrickLight",.16)
    box(c,"Cream_Base",(0,.32,3.3),(7.55,.64,6.9),"Stone",.08)
    box(c,"Cream_Cornice",(0,5.4,3.3),(7.7,.30,7.0),"Cream",.08)
    window_grid(c,-.055,2,5,6.7,1.55,1.85)
    window_grid(c,6.655,2,5,6.7,1.55,1.85)
    side_window_grid(c,-3.655,2,4,6.6,1.55,1.85)
    side_window_grid(c,3.655,2,4,6.6,1.55,1.85)
    # glass studio roof
    box(c,"Navy_RoofBeam",(0,6.15,3.3),(7.0,.16,6.2),"Navy",.04,rot=(0,0,math.radians(18)))
    for x in (-2.6,-1.3,0,1.3,2.6): box(c,"Glass_RoofPane",(x,5.95,3.3),(1.12,.12,5.8),"Glass",.025,rot=(0,0,math.radians(18)))
    return c


def clocktower():
    c=collection("CR_ClockTower")
    box(c,"Brick_Tower",(0,4.1,2.1),(4.4,8.2,4.2),"Brick",.15)
    box(c,"Cream_Base",(0,.35,2.1),(4.8,.7,4.6),"Stone",.08)
    box(c,"Cream_Cornice",(0,7.25,2.1),(4.9,.35,4.7),"Cream",.08)
    box(c,"Brick_ClockHouse",(0,8.4,2.1),(5.0,2.1,4.8),"BrickLight",.14)
    cyl(c,"White_ClockFace",(0,8.45,-.36),.73,.12,"White",rot=(math.pi/2,0,0),verts=32)
    cyl(c,"Navy_ClockCenter",(0,8.45,-.44),.08,.10,"Navy",rot=(math.pi/2,0,0),verts=16)
    box(c,"Navy_ClockHandV",(0,8.72,-.50),(.07,.56,.07),"Navy",.02)
    box(c,"Navy_ClockHandH",(.22,8.45,-.50),(.45,.07,.07),"Navy",.02)
    box(c,"Navy_Roof",(0,9.72,2.1),(5.3,.45,5.1),"Navy",.10,rot=(0,0,math.radians(8)))
    cyl(c,"Gold_Spire",(0,11.0,2.1),.07,2.4,"Gold",verts=14)
    return c


def tree():
    c=collection("CR_CampusTree")
    cyl(c,"Wood_Trunk",(0,1.45,0),.32,2.9,"Wood",verts=14)
    for i,(x,y,z,s) in enumerate([(-.7,3.0,0,.95),(.6,3.15,.2,1.1),(0,3.75,-.2,1.2),(-.15,4.25,.25,.85)]):
        sphere(c,f"Leaf_Crown_{i}",(x,y,z),(s,s*.85,s),"LeafLight" if i%2 else "Leaf")
    return c


def banner():
    c=collection("CR_CampusBanner")
    cyl(c,"Metal_Pole",(0,2.6,0),.065,5.2,"Metal",verts=16)
    cyl(c,"Gold_Finial",(0,5.35,0),.14,.28,"Gold",verts=16)
    box(c,"Gold_TopBar",(-.52,4.55,0), (1.18,.07,.08),"Gold",.025)
    box(c,"Cobalt_Banner",(-.52,3.55,0),(1.02,1.85,.055),"Cobalt",.035)
    box(c,"Gold_Chevron",(-.52,2.72,-.035),(.72,.12,.04),"Gold",.02,rot=(0,0,math.radians(-15)))
    text_obj(c,"Gold_Emblem","A",(-.52,3.75,-.05),.48,.015,"Gold")
    return c


def train():
    c=collection("CR_CampusTrain")
    box(c,"Cobalt_Body",(0,1.35,5),(1.68,2.45,9.75),"Cobalt",.18)
    box(c,"Cream_Roof",(0,2.52,5),(1.62,.20,9.35),"Cream",.10)
    box(c,"Gold_Nose",(0,1.18,.13),(1.58,1.72,.20),"Gold",.09)
    box(c,"Glass_Windshield",(0,1.88,.02),(1.30,.78,.10),"Glass",.08)
    for x in (-.52,.52): cyl(c,"White_Headlight",(x,.72,-.015),.18,.10,"White",rot=(math.pi/2,0,0),verts=24)
    for side in (-1,1):
        for i in range(5):
            z=1.15+i*1.75
            box(c,f"Glass_Side_{side}_{i}",(side*.855,1.62,z),(.07,.78,1.25),"Glass",.05)
        box(c,f"Gold_Stripe_{side}",(side*.89,.82,5),(.045,.16,9.35),"Gold",.02)
    for z in (2.1,5.0,7.9): box(c,"Metal_RoofVent",(0,2.68,z),(.75,.16,1.05),"Metal",.06)
    box(c,"Metal_Bumper",(0,.35,-.04),(1.35,.16,.18),"Metal",.05)
    return c


def barricade():
    c=collection("CR_Barricade")
    for x in (-.68,.68):
        box(c,"Metal_Post",(x,.72,0),(.15,1.35,.18),"Metal",.05)
        box(c,"Metal_Foot",(x,.08,0),(.62,.16,.55),"Metal",.06)
    box(c,"Red_Beam",(0,1.15,0),(1.72,.62,.34),"Red",.08)
    for x in (-.60,-.20,.20,.60): box(c,"White_Stripe",(x,1.15,-.19),(.23,.50,.05),"White",.02,rot=(0,0,math.radians(-24)))
    for x in (-.58,.58): cyl(c,"Gold_WarningLight",(x,1.58,0),.13,.18,"Gold",verts=20)
    return c


def ramp():
    c=collection("CR_TrainRamp")
    # stepped visual follows the gameplay ramp underneath without changing collisions
    steps=10
    for i in range(steps):
        z=.35+i*.70; h=.13+(i+.5)*(2.46/steps)
        box(c,f"Brick_RampStep_{i}",(0,h/2,z),(1.62,h,.72),"Brick",.045)
    box(c,"Cobalt_LeftRail",(-.82,1.25,3.5),(.13,2.60,7.0),"Cobalt",.05,rot=(math.radians(19),0,0))
    box(c,"Cobalt_RightRail",(.82,1.25,3.5),(.13,2.60,7.0),"Cobalt",.05,rot=(math.radians(19),0,0))
    box(c,"Gold_TopStripe",(0,2.60,7.25),(1.55,.10,5.3),"Gold",.035)
    return c


def to_blender_z_up(c):
    """Геройский набор собран по-юнитевски: вверх — ось Y, вдоль — Z.
    Blender и его FBX-экспортёр ждут, что вверх — Z (так собран базовый
    набор в build_campus_rush_kit.py).

    Из-за расхождения экспортёр вешал на каждый узел Lcl Rotation = -90 по X,
    Unity его применяла, и поезд длиной 9.75 вставал на нос башней высотой
    9.75, а пандус длиной 7 — стеной высотой 7.

    Здесь набор доворачивается на +90 по X и поворот запекается в сетку.
    После этого он лежит в том же соглашении, что и базовый, и приходит
    в Unity ровно. Не забудь поставить CampusRushModels.HeroKitNeedsAxisFix
    в false — иначе компенсация в коде сработает поверх и снова всё уронит.
    """
    objs = [o for o in c.objects]
    if not objs: return
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.transform.rotate(value=math.radians(90), orient_axis='X', orient_type='GLOBAL')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)


def export_collection(c):
    to_blender_z_up(c)
    bpy.ops.object.select_all(action='DESELECT')
    for o in c.objects: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,c.name+".fbx"),use_selection=True,
        object_types={'MESH','OTHER'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,
        bake_anim=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',path_mode='AUTO')


try:
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for c in list(bpy.data.collections):
        if c.name != "Collection": bpy.data.collections.remove(c)
    os.makedirs(OUT,exist_ok=True)
    kits=[building_a(),building_b(),clocktower(),tree(),banner(),train(),barricade(),ramp()]
    for c in kits: export_collection(c)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    open('/private/tmp/campus_hero_kit_done.txt','w').write('\n'.join(c.name for c in kits))
except Exception:
    open('/private/tmp/campus_hero_kit_error.txt','w').write(traceback.format_exc())
    raise
