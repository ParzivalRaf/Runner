import bpy


bpy.ops.wm.open_mainfile(
    filepath="/Users/Rafael/Desktop/CampusRush/ArtSource/Quaternius_Official/Ultimate_Modular_Women/Blends/All together.blend"
)

with open("/private/tmp/quaternius_women_objects.txt", "w", encoding="utf-8") as output:
    for obj in bpy.data.objects:
        output.write(f"{obj.type}\t{obj.name}\tparent={obj.parent.name if obj.parent else '-'}\n")
