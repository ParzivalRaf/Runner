import bpy


bpy.ops.wm.open_mainfile(
    filepath="/Users/Rafael/Desktop/CampusRush/ArtSource/Quaternius_Official/Ultimate_Modular_Men/Blends/Humans_Master.blend"
)

with open("/private/tmp/quaternius_objects.txt", "w", encoding="utf-8") as output:
    output.write("COLLECTIONS\n")
    for collection in bpy.data.collections:
        output.write(collection.name + "\n")

    output.write("\nOBJECTS\n")
    for obj in bpy.data.objects:
        output.write(f"{obj.type}\t{obj.name}\tparent={obj.parent.name if obj.parent else '-'}\n")
