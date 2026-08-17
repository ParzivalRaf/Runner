"""
Запекание затенения в текстуры для набора моделей Campus Rush.

ЗАЧЕМ. Модели набора приходят с плоскими цветами: у каждой части свой
материал, внутри — один цвет. Читается это как пластилин: щели, углубления
окон и стыки панелей не видно, потому что в них нет тени.

Обычно это лечат эффектом SSAO — он считает то же затенение каждый кадр
и стоит проценты кадра на телефоне. Здесь оно считается ОДИН раз и
рисуется внутрь текстуры. В игре стоит ноль.

Побочно: десять материалов схлопываются в один. Один вызов отрисовки
на модель вместо десяти.

ЧЕГО НЕ ДАЁТ. Затемнения на земле ПОД предметом: оно живёт внутри модели,
а не под ней. И, разумеется, ничего нарисованного от руки.

КАК ЗАПУСКАТЬ. Блендер не нужен как программа — он ставится модулем:
    pip install bpy==4.2.23
    python3 bake_ao_kit.py
Пути SRC и OUT — в первых строках.

ВАЖНО ПРО ОСИ. Импортёр OBJ в bpy НЕ переводит оси: файл приходит как есть.
Поэтому здесь ничего не поворачивается, и экспорт возвращает файл в той же
системе координат. Проверено сверкой габаритов и числа треугольников
до и после: расхождений ноль. Имена частей (nose_shell, top_plate)
сохраняются намеренно — по ним RunnerArtSetTools определяет разворот.
"""
import bpy, os, math
SRC="/mnt/user-data/uploads/CampusRush/Assets/_Project/Models/Kits/NewDesign"
OUT="/root/work/baked"; os.makedirs(OUT,exist_ok=True)
BIG={"building_near_a","building_near_b","clock_tower","train_car","ramp_to_train"}
NAMES=["banner","barrier","bench","building_near_a","building_near_b","clock_tower",
       "coin","lamp_post","locker","planter","ramp_to_train","slide_gate","train_car","tree"]

def mats(objs):
    seen=[]
    for o in objs:
        for s in o.material_slots:
            if s.material and s.material.use_nodes and s.material not in seen:
                seen.append(s.material)
    return seen

def bake_one(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc=bpy.context.scene
    sc.render.engine='CYCLES'; sc.cycles.device='CPU'
    sc.cycles.samples=48; sc.cycles.use_denoising=False
    w=bpy.data.worlds.new("W"); sc.world=w; w.use_nodes=True; w.light_settings.distance=0.7

    bpy.ops.wm.obj_import(filepath=os.path.join(SRC,name+".obj"))
    objs=[o for o in bpy.context.selected_objects if o.type=='MESH']   # ИМЕНА ЧАСТЕЙ СОХРАНЯЕМ
    for m in mats(objs):
        n=m.node_tree.nodes.get("Principled BSDF")
        if n:
            n.inputs["Roughness"].default_value=1.0; n.inputs["Metallic"].default_value=0.0
            if "Specular IOR Level" in n.inputs: n.inputs["Specular IOR Level"].default_value=0.0

    res=1024 if name in BIG else 512
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    # многообъектный edit: развёртка пакуется в ОДИН общий атлас на всю модель
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.03)
    bpy.ops.object.mode_set(mode='OBJECT')

    ao=bpy.data.images.new(name+"_ao",res,res)
    for m in mats(objs):
        nd=m.node_tree.nodes.new('ShaderNodeTexImage'); nd.image=ao; nd.name="AO_T"
        nd.location=(-1100,300); m.node_tree.nodes.active=nd
    sc.cycles.bake_type='AO'
    bpy.ops.object.bake(type='AO', margin=max(4,res//64), use_clear=True)

    for m in mats(objs):
        nt=m.node_tree; bsdf=nt.nodes.get("Principled BSDF"); t=nt.nodes.get("AO_T")
        if not bsdf or not t: continue
        base=tuple(bsdf.inputs["Base Color"].default_value)
        g=nt.nodes.new('ShaderNodeGamma'); g.inputs['Gamma'].default_value=2.2; g.location=(-820,300)
        nt.links.new(t.outputs['Color'], g.inputs['Color'])
        mx=nt.nodes.new('ShaderNodeMixRGB'); mx.blend_type='MULTIPLY'
        mx.inputs['Fac'].default_value=1.0; mx.location=(-560,300)
        mx.inputs['Color1'].default_value=base
        nt.links.new(g.outputs['Color'], mx.inputs['Color2'])
        nt.links.new(mx.outputs['Color'], bsdf.inputs['Base Color'])

    diff=bpy.data.images.new(name,res,res)
    for m in mats(objs):
        nd=m.node_tree.nodes.new('ShaderNodeTexImage'); nd.image=diff; nd.name="D_T"
        nd.location=(-1100,-300); m.node_tree.nodes.active=nd
    sc.cycles.bake_type='DIFFUSE'
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=max(4,res//64), use_clear=True)
    diff.filepath_raw=os.path.join(OUT,name+".png"); diff.file_format='PNG'; diff.save()

    # один материал на всю модель: один вызов отрисовки вместо десяти
    shared=bpy.data.materials.new(name); shared.use_nodes=True
    nt=shared.node_tree; bsdf=nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value=1.0; bsdf.inputs["Metallic"].default_value=0.0
    if "Specular IOR Level" in bsdf.inputs: bsdf.inputs["Specular IOR Level"].default_value=0.0
    t=nt.nodes.new('ShaderNodeTexImage')
    t.image=bpy.data.images.load(os.path.join(OUT,name+".png")); t.interpolation='Closest'
    nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
    for o in objs:
        o.data.materials.clear(); o.data.materials.append(shared)

    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.wm.obj_export(filepath=os.path.join(OUT,name+".obj"),
                          export_selected_objects=True, export_materials=True,
                          export_uv=True, export_normals=True, path_mode='COPY',
                          forward_axis='NEGATIVE_Z', up_axis='Y', apply_modifiers=True)
    print("OK",name,res,"частей:",len(objs))

for n in NAMES:
    try: bake_one(n)
    except Exception as e: print("FAIL",n,repr(e))
