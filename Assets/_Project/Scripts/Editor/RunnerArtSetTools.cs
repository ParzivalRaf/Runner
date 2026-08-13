using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Заведение чужих наборов моделей в проект и переключение между ними.
///
/// ЗАЧЕМ ОТДЕЛЬНЫЙ ИМПОРТ, А НЕ «положить FBX в Resources».
/// В чужих китах один FBX содержит десятки объектов сразу: в
/// Premium_02_Gameplay_Props лежат и пандус, и барьер, и четырнадцать монет.
/// Игре нужен один объект на роль. Поэтому импорт вытаскивает нужный узел,
/// сажает его на землю, ставит по центру и подгоняет под размер той модели,
/// которая стоит в игре сейчас.
///
/// ПОЧЕМУ ПОДГОНЯЕМ ПО РАЗМЕРУ. Сравнение честное только тогда, когда
/// меняется вид, а не геометрия трассы. Полоса в игре — 2.5 метра
/// (PlayerController.laneDistance), поезд — 1.82 в ширину. Кит собран
/// под полосу 4–4.9 метра: если поставить как есть, поезд перекроет
/// соседние полосы и игра сломается. Поэтому каждая модель масштабируется
/// так, чтобы её ключевой размер совпал с нынешним.
///
/// ПО КАКОЙ ОСИ ПОДГОНЯЕМ. У того, во что игрок врезается, главное —
/// ширина: она должна помещаться в полосу. У фона главное — высота.
/// Отсюда FitAxis у каждой строки таблицы.
///
/// ОСИ. Поворот из FBX запекается в префаб прямо здесь, поэтому в рантайме
/// CampusRushModels.AxisFix для этих наборов возвращает identity. История
/// проблемы с осями — в CampusRushArt.cs и docs/ОСИ_МОДЕЛЕЙ.md.
/// </summary>
public static class RunnerArtSetTools
{
    private const string KitsFolder = "Assets/_Project/Models/Kits";
    private const string SetsFolder = "Assets/Resources/CampusRush/Sets";
    private const string SelectionFile = "Assets/_Project/Scripts/World/ArtSetSelection.cs";
    private const string FullSceneMenu = "Tools/Runner/M6+M7 — полная игра: интерфейс, бонусы, магазин";

    /// <summary>По какому измерению подгонять модель под нынешнюю.</summary>
    private enum FitAxis { Width, Height, Depth }

    private readonly struct Entry
    {
        public readonly ArtRole Role;
        public readonly string File;        // без расширения, внутри папки набора
        public readonly string Anchor;      // имя узла в файле; null — весь файл
        public readonly Vector3 Target;     // габариты нынешней модели, метры
        public readonly FitAxis Fit;

        public Entry(ArtRole role, string file, string anchor, Vector3 target, FitAxis fit)
        {
            Role = role; File = file; Anchor = anchor; Target = target; Fit = fit;
        }
    }

    // Габариты нынешних моделей (замерены по вершинам FBX с учётом импортного
    // поворота, см. CampusRushArt.cs). Под них подгоняется всё остальное.
    private static readonly Vector3 SizeTrain = new(1.82f, 2.64f, 10.07f);
    private static readonly Vector3 SizeRamp = new(1.77f, 4.70f, 10.12f);
    private static readonly Vector3 SizeBlock = new(1.48f, 2.66f, 0.85f);
    private static readonly Vector3 SizeJump = new(1.98f, 1.71f, 0.55f);
    private static readonly Vector3 SizeSlide = new(2.06f, 1.93f, 0.52f);
    private static readonly Vector3 SizeBuildingA = new(7.15f, 6.43f, 7.65f);
    private static readonly Vector3 SizeBuildingB = new(7.70f, 7.30f, 7.00f);
    private static readonly Vector3 SizeTower = new(5.28f, 11.07f, 5.74f);
    private static readonly Vector3 SizeTree = new(3.25f, 3.84f, 2.90f);
    private static readonly Vector3 SizeBanner = new(1.25f, 2.96f, 5.20f);
    private static readonly Vector3 SizeBench = new(2.25f, 1.50f, 0.77f);
    private static readonly Vector3 SizeLamp = new(1.14f, 3.33f, 0.64f);

    private static readonly Dictionary<ArtSet, Entry[]> Recipes = new()
    {
        {
            ArtSet.Premium, new[]
            {
                new Entry(ArtRole.Train,         "Premium_03_Trains",        "Premium_Train_BlueGold",     SizeTrain,     FitAxis.Width),
                new Entry(ArtRole.Ramp,          "Premium_02_Gameplay_Props","Premium_Obstacle_Ramp",      SizeRamp,      FitAxis.Width),
                new Entry(ArtRole.ObstacleBlock, "Premium_02_Gameplay_Props","Premium_Cabinet_Left",       SizeBlock,     FitAxis.Height),
                new Entry(ArtRole.ObstacleJump,  "Premium_02_Gameplay_Props","Premium_Obstacle_Barricade", SizeJump,      FitAxis.Width),
                new Entry(ArtRole.Banner,        "Premium_02_Gameplay_Props","Premium_Campus_Banner",      SizeBanner,    FitAxis.Height),
                new Entry(ArtRole.BuildingA,     "Premium_04_Campus_City",   "Campus_Left_Foreground",     SizeBuildingA, FitAxis.Height),
                new Entry(ArtRole.BuildingB,     "Premium_04_Campus_City",   "Campus_Right_Mid",           SizeBuildingB, FitAxis.Height),
                new Entry(ArtRole.ClockTower,    "Premium_04_Campus_City",   "Campus_Clock_Tower",         SizeTower,     FitAxis.Height),
                new Entry(ArtRole.Tree,          "Premium_05_Sky_Trees",     "Tree_1_0",                   SizeTree,      FitAxis.Height),
            }
        },
        {
            ArtSet.KitBase, new[]
            {
                new Entry(ArtRole.Train,         "02_TRAINS",       "Train_Metro_Blue",   SizeTrain, FitAxis.Width),
                new Entry(ArtRole.Ramp,          "03_OBSTACLES",    "Obstacle_JumpRamp",  SizeRamp,  FitAxis.Width),
                new Entry(ArtRole.ObstacleBlock, "03_OBSTACLES",    "Obstacle_Crate",     SizeBlock, FitAxis.Height),
                new Entry(ArtRole.ObstacleJump,  "03_OBSTACLES",    "Obstacle_Barricade", SizeJump,  FitAxis.Width),
                new Entry(ArtRole.ObstacleSlide, "03_OBSTACLES",    "Obstacle_LowGate",   SizeSlide, FitAxis.Width),
                new Entry(ArtRole.Tree,          "05_STREET_PROPS", "Tree_Crown",         SizeTree,  FitAxis.Height),
                new Entry(ArtRole.Bench,         "05_STREET_PROPS", "Station_Bench_Seat", SizeBench, FitAxis.Width),
                new Entry(ArtRole.Lamp,          "05_STREET_PROPS", "StreetLamp_L_Pole",  SizeLamp,  FitAxis.Height),
            }
        },
        {
            ArtSet.Sketch, new[]
            {
                new Entry(ArtRole.Train,         "train",      null, SizeTrain,     FitAxis.Width),
                new Entry(ArtRole.Ramp,          "ramp",       null, SizeRamp,      FitAxis.Width),
                new Entry(ArtRole.ObstacleBlock, "lockers",    null, SizeBlock,     FitAxis.Height),
                new Entry(ArtRole.ObstacleJump,  "barrier",    null, SizeJump,      FitAxis.Width),
                new Entry(ArtRole.BuildingA,     "brownstone", null, SizeBuildingA, FitAxis.Height),
                new Entry(ArtRole.BuildingB,     "tower",      null, SizeBuildingB, FitAxis.Height),
                new Entry(ArtRole.ClockTower,    "clocktower", null, SizeTower,     FitAxis.Height),
                new Entry(ArtRole.Tree,          "tree",       null, SizeTree,      FitAxis.Height),
                new Entry(ArtRole.Lamp,          "lamppost",   null, SizeLamp,      FitAxis.Height),
            }
        },
    };

    // ------------------------------------------------------------ переключение

    [MenuItem("Tools/Runner/Набор моделей/Свой (Original)")]
    private static void UseOriginal() => Switch(ArtSet.Original);

    [MenuItem("Tools/Runner/Набор моделей/Premium Campus City")]
    private static void UsePremium() => Switch(ArtSet.Premium);

    [MenuItem("Tools/Runner/Набор моделей/Базовый кит (метро)")]
    private static void UseKitBase() => Switch(ArtSet.KitBase);

    [MenuItem("Tools/Runner/Набор моделей/Процедурный (claude)")]
    private static void UseSketch() => Switch(ArtSet.Sketch);

    [MenuItem("Tools/Runner/Набор моделей/Свой (Original)", true)]
    private static bool ValidateOriginal() => Check(ArtSet.Original);
    [MenuItem("Tools/Runner/Набор моделей/Premium Campus City", true)]
    private static bool ValidatePremium() => Check(ArtSet.Premium);
    [MenuItem("Tools/Runner/Набор моделей/Базовый кит (метро)", true)]
    private static bool ValidateKitBase() => Check(ArtSet.KitBase);
    [MenuItem("Tools/Runner/Набор моделей/Процедурный (claude)", true)]
    private static bool ValidateSketch() => Check(ArtSet.Sketch);

    private static bool Check(ArtSet set)
    {
        Menu.SetChecked(MenuPath(set), ArtSetSelection.Active == set);
        return true;
    }

    private static string MenuPath(ArtSet set) => set switch
    {
        ArtSet.Original => "Tools/Runner/Набор моделей/Свой (Original)",
        ArtSet.Premium => "Tools/Runner/Набор моделей/Premium Campus City",
        ArtSet.KitBase => "Tools/Runner/Набор моделей/Базовый кит (метро)",
        _ => "Tools/Runner/Набор моделей/Процедурный (claude)",
    };

    private static void Switch(ArtSet set)
    {
        if (set != ArtSet.Original && !SetIsImported(set))
        {
            if (!EditorUtility.DisplayDialog("Набор не заведён",
                    $"Модели набора «{set}» ещё не разложены по ролям.\n\n" +
                    "Сначала: Tools → Runner → Набор моделей → Импортировать все наборы.",
                    "Импортировать сейчас", "Отмена"))
                return;

            ImportAll();
        }

        File.WriteAllText(SelectionFile, SelectionSource(set));
        AssetDatabase.ImportAsset(SelectionFile);

        EditorPrefs.SetString("Runner.RebuildSceneAfterCompile", set.ToString());
        Debug.Log($"[Наборы] Активен набор {set}. Unity перекомпилирует скрипты, " +
                  "потом сцена пересоберётся сама.");
        AssetDatabase.Refresh();
    }

    private static string SelectionSource(ArtSet set) =>
        "/// <summary>\n" +
        "/// Активный набор моделей. ЭТОТ ФАЙЛ ПЕРЕПИСЫВАЕТСЯ АВТОМАТИЧЕСКИ —\n" +
        "/// пунктом меню Tools → Runner → Набор моделей. Руками править можно,\n" +
        "/// но проще переключить из меню: оно ещё и сцену пересоберёт.\n" +
        "///\n" +
        "/// Константа, а не настройка в сцене, специально: значение попадает\n" +
        "/// в сборку на телефон, так что замерить FPS можно на любом наборе.\n" +
        "/// </summary>\n" +
        "public static class ArtSetSelection\n" +
        "{\n" +
        $"    public const ArtSet Active = ArtSet.{set};\n" +
        "}\n";

    /// <summary>После перекомпиляции дособерём сцену — иначе смена набора не видна.</summary>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void RebuildAfterCompile()
    {
        const string key = "Runner.RebuildSceneAfterCompile";
        if (!EditorPrefs.HasKey(key)) return;
        EditorPrefs.DeleteKey(key);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.ExecuteMenuItem(FullSceneMenu))
                Debug.LogWarning("[Наборы] Не нашёл пункт сборки сцены. Собери вручную: " + FullSceneMenu);
        };
    }

    private static bool SetIsImported(ArtSet set)
    {
        string folder = SetsFolder + "/" + CampusRushModels.SetFolder(set);
        return AssetDatabase.IsValidFolder(folder) &&
               AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Length > 0;
    }

    // ---------------------------------------------------------------- импорт

    [MenuItem("Tools/Runner/Набор моделей/Импортировать все наборы")]
    public static void ImportAll()
    {
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (ArtSet set in Recipes.Keys) ConfigureImporters(set);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        int made = 0, failed = 0;
        foreach (KeyValuePair<ArtSet, Entry[]> pair in Recipes)
        {
            EnsureFolder(SetsFolder + "/" + CampusRushModels.SetFolder(pair.Key));
            foreach (Entry entry in pair.Value)
            {
                if (Extract(pair.Key, entry)) made++;
                else failed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Наборы] Готово: собрано {made} моделей, не вышло {failed}. " +
                  "Переключать — Tools → Runner → Набор моделей.");
    }

    private static void ConfigureImporters(ArtSet set)
    {
        string folder = KitsFolder + "/" + FolderFor(set);
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"[Наборы] Нет папки {folder}. Исходники набора {set} не скопированы.");
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
        }
    }

    private static string FolderFor(ArtSet set) => set switch
    {
        ArtSet.Premium => "Premium",
        ArtSet.KitBase => "Base",
        ArtSet.Sketch => "Sketch",
        _ => "Original",
    };

    private static bool Extract(ArtSet set, Entry entry)
    {
        string folder = KitsFolder + "/" + FolderFor(set);
        GameObject model = LoadModel(folder, entry.File);
        if (model == null)
        {
            Debug.LogWarning($"[Наборы] {set}/{entry.Role}: не нашёл файл {folder}/{entry.File}");
            return false;
        }

        GameObject spawned = UnityEngine.Object.Instantiate(model);
        GameObject container = null;
        try
        {
            spawned.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            spawned.transform.localScale = Vector3.one;

            Transform anchor = entry.Anchor == null ? spawned.transform : FindNode(spawned.transform, entry.Anchor);
            if (anchor == null)
            {
                Debug.LogWarning($"[Наборы] {set}/{entry.Role}: в {entry.File} нет узла «{entry.Anchor}»");
                return false;
            }

            container = new GameObject(entry.Role.ToString());
            container.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (Transform part in PartsFor(spawned.transform, anchor))
                part.SetParent(container.transform, true);

            if (!TryBounds(container, out Bounds bounds))
            {
                Debug.LogWarning($"[Наборы] {set}/{entry.Role}: у узла нет ни одного меша");
                return false;
            }

            // Масштаб — по одному измерению, равномерный: непропорциональное
            // сжатие ломает вид сильнее, чем несовпадение длины.
            float current = entry.Fit switch
            {
                FitAxis.Width => bounds.size.x,
                FitAxis.Height => bounds.size.y,
                _ => bounds.size.z,
            };
            float target = entry.Fit switch
            {
                FitAxis.Width => entry.Target.x,
                FitAxis.Height => entry.Target.y,
                _ => entry.Target.z,
            };
            float scale = current > 0.0001f ? target / current : 1f;
            container.transform.localScale = Vector3.one * scale;

            // Посадка: центр по X/Z в нуле, низ модели на нуле. Игра ставит
            // модели в точку на земле и ждёт именно такой привязки.
            TryBounds(container, out bounds);
            container.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            foreach (Collider collider in container.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);

            string prefabPath = $"{SetsFolder}/{CampusRushModels.SetFolder(set)}/{entry.Role}.prefab";
            PrefabUtility.SaveAsPrefabAsset(container, prefabPath);

            TryBounds(container, out bounds);
            Debug.Log($"[Наборы] {set}/{entry.Role}: {bounds.size.x:F2} × {bounds.size.y:F2} × " +
                      $"{bounds.size.z:F2} м (было {current:F2} по {entry.Fit}, масштаб {scale:F3})");
            return true;
        }
        finally
        {
            // container может содержать сам spawned (когда якоря нет и берётся
            // весь файл), поэтому второй Destroy — только если объект жив.
            if (container != null) UnityEngine.Object.DestroyImmediate(container);
            if (spawned != null) UnityEngine.Object.DestroyImmediate(spawned);
        }
    }

    private static GameObject LoadModel(string folder, string file)
    {
        foreach (string extension in new[] { ".fbx", ".obj", ".FBX", ".OBJ" })
        {
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{file}{extension}");
            if (loaded != null) return loaded;
        }
        return null;
    }

    /// <summary>
    /// Части модели. Если у якорного узла есть дети — берём его целиком.
    /// Если детей нет (в некоторых китах иерархии нет вообще, все объекты
    /// лежат плоским списком), добираем соседей, которые стоят рядом:
    /// так скамейка приходит с ножками, а дерево — со стволом.
    /// </summary>
    private static List<Transform> PartsFor(Transform root, Transform anchor)
    {
        var parts = new List<Transform> { anchor };
        if (anchor.childCount > 0 || anchor == root) return parts;

        if (!TryBounds(anchor.gameObject, out Bounds anchorBounds)) return parts;

        // Высота входит в радиус вполовину: у фонаря столб тонкий, а
        // кронштейн с плафоном отходит вбок примерно на свою высоту.
        // Без этого фонарь приходит голым столбом.
        float radius = Mathf.Max(anchorBounds.extents.x, anchorBounds.extents.z,
                                 anchorBounds.extents.y * 0.5f) * 1.6f + 0.4f;

        foreach (Transform sibling in anchor.parent)
        {
            if (sibling == anchor || sibling.childCount > 0) continue;
            if (!TryBounds(sibling.gameObject, out Bounds siblingBounds)) continue;

            Vector2 a = new(anchorBounds.center.x, anchorBounds.center.z);
            Vector2 b = new(siblingBounds.center.x, siblingBounds.center.z);
            if (Vector2.Distance(a, b) <= radius) parts.Add(sibling);
        }
        return parts;
    }

    private static Transform FindNode(Transform root, string name)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            if (node.name == name) return node;

        // Blender добавляет к дублям суффикс .001 — на случай, если точного нет.
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            if (node.name.StartsWith(name, StringComparison.Ordinal)) return node;

        return null;
    }

    private static bool TryBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { bounds = default; return false; }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
