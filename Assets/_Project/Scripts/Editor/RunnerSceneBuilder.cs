#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Собирает тестовые сцены этапов одной кнопкой: пол, разметку, игрока,
/// камеру и генератор трассы, со всеми уже проставленными ссылками.
///
/// Меню: Tools → Runner → ...
///
/// Это редакторный скрипт: лежит в папке Editor и в сборку игры не попадает.
/// </summary>
public static class RunnerSceneBuilder
{
    private const string ProjectRoot = "Assets/_Project";
    private const string MaterialsFolder = ProjectRoot + "/Materials";
    private const string ChunksFolder = ProjectRoot + "/Prefabs/Chunks";

    private const float LaneDistance = 2.5f;
    private const float TrackWidth = 12f;
    private const float ChunkLength = 30f;

    // ===================================================================== M1

    [MenuItem("Tools/Runner/M1 — сцена с длинным полом")]
    public static void BuildM1Scene()
    {
        DeleteIfExists("Track");
        DeleteIfExists("ChunkSpawner");
        DeleteIfExists("Player");

        Materials mats = LoadMaterials();

        BuildLongTrack(mats);
        GameObject player = BuildPlayer(mats);
        SetUpCamera(player.transform);
        SetUpLight();

        Finish(player, "Сцена M1 собрана. Жми Play.");
    }

    // ===================================================================== M2

    [MenuItem("Tools/Runner/M2 — бесконечная трасса из чанков")]
    public static void BuildM2Scene()
    {
        DeleteIfExists("Track");
        DeleteIfExists("ChunkSpawner");
        DeleteIfExists("Player");

        Materials mats = LoadMaterials();

        List<Chunk> prefabs = CreateChunkPrefabs(mats);

        GameObject player = BuildPlayer(mats);
        SetUpCamera(player.transform);
        SetUpLight();

        var spawnerGo = new GameObject("ChunkSpawner");
        var spawner = spawnerGo.AddComponent<ChunkSpawner>();

        var so = new SerializedObject(spawner);
        so.FindProperty("player").objectReferenceValue = player.transform;

        SerializedProperty prefabsProp = so.FindProperty("chunkPrefabs");
        prefabsProp.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++)
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        // Подключаем счётчик чанков в отладочный HUD.
        var hud = player.GetComponent<DebugHud>();
        if (hud != null)
        {
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("spawner").objectReferenceValue = spawner;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Finish(player, $"Сцена M2 собрана: {prefabs.Count} типа чанков. Жми Play.");
    }

    // ============================================================== материалы

    private struct Materials
    {
        public Material Ground;
        public Material Marker;
        public Material Rail;
        public Material Player;
        public Material Prop;
    }

    private static Materials LoadMaterials()
    {
        return new Materials
        {
            Ground = GetOrCreateMaterial("M_Ground", new Color(0.20f, 0.22f, 0.26f)),
            Marker = GetOrCreateMaterial("M_Marker", new Color(0.85f, 0.85f, 0.88f)),
            Rail = GetOrCreateMaterial("M_Rail", new Color(0.32f, 0.36f, 0.42f)),
            Player = GetOrCreateMaterial("M_Player", new Color(0.95f, 0.45f, 0.15f)),
            Prop = GetOrCreateMaterial("M_Prop", new Color(0.45f, 0.50f, 0.58f))
        };
    }

    // ============================================================ длинный пол

    private static void BuildLongTrack(Materials mats)
    {
        const float length = 2000f;
        var track = new GameObject("Track");

        GameObject ground = Box("Ground", track.transform,
                                new Vector3(TrackWidth, 1f, length),
                                new Vector3(0f, -0.5f, length * 0.5f - 20f),
                                mats.Ground, keepCollider: true);
        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

        for (int side = -1; side <= 1; side += 2)
        {
            Box(side < 0 ? "Rail_Left" : "Rail_Right", track.transform,
                new Vector3(0.4f, 1.6f, length),
                new Vector3(side * (TrackWidth * 0.5f + 0.2f), 0.8f, length * 0.5f - 20f),
                mats.Rail);
        }

        var markers = new GameObject("Markers");
        markers.transform.SetParent(track.transform);

        int count = Mathf.FloorToInt(length / 20f);
        for (int i = 1; i <= count; i++)
        {
            Box($"Marker_{i * 20}m", markers.transform,
                new Vector3(TrackWidth, 0.02f, 0.35f),
                new Vector3(0f, 0.01f, i * 20f),
                mats.Marker);
        }

        for (int i = -1; i <= 1; i += 2)
        {
            Box(i < 0 ? "LaneLine_Left" : "LaneLine_Right", track.transform,
                new Vector3(0.08f, 0.02f, length),
                new Vector3(i * LaneDistance * 0.5f, 0.005f, length * 0.5f - 20f),
                mats.Marker);
        }
    }

    // ================================================================= чанки

    private static List<Chunk> CreateChunkPrefabs(Materials mats)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ChunksFolder);

        var result = new List<Chunk>
        {
            CreateChunkPrefab("Chunk_Plain", mats, ChunkDecor.None, unlockAt: 0f, weight: 1.2f),
            CreateChunkPrefab("Chunk_Pillars", mats, ChunkDecor.Pillars, unlockAt: 0f, weight: 1f),
            CreateChunkPrefab("Chunk_Arches", mats, ChunkDecor.Arches, unlockAt: 150f, weight: 1f)
        };

        AssetDatabase.SaveAssets();
        return result;
    }

    private enum ChunkDecor { None, Pillars, Arches }

    private static Chunk CreateChunkPrefab(string prefabName, Materials mats, ChunkDecor decor,
                                           float unlockAt, float weight)
    {
        var root = new GameObject(prefabName);
        Chunk chunk = root.AddComponent<Chunk>();

        // Пивот в начале куска, кусок уходит вперёд на ChunkLength.
        float mid = ChunkLength * 0.5f;

        Box("Ground", root.transform,
            new Vector3(TrackWidth, 1f, ChunkLength),
            new Vector3(0f, -0.5f, mid),
            mats.Ground, keepCollider: true);

        for (int side = -1; side <= 1; side += 2)
        {
            Box(side < 0 ? "Rail_Left" : "Rail_Right", root.transform,
                new Vector3(0.4f, 1.6f, ChunkLength),
                new Vector3(side * (TrackWidth * 0.5f + 0.2f), 0.8f, mid),
                mats.Rail);

            Box(side < 0 ? "LaneLine_Left" : "LaneLine_Right", root.transform,
                new Vector3(0.08f, 0.02f, ChunkLength),
                new Vector3(side * LaneDistance * 0.5f, 0.005f, mid),
                mats.Marker);
        }

        // Поперечные полосы — по ним видно скорость.
        for (int i = 0; i < 3; i++)
        {
            float z = 5f + i * 10f;
            Box($"Stripe_{i}", root.transform,
                new Vector3(TrackWidth, 0.02f, 0.35f),
                new Vector3(0f, 0.01f, z),
                mats.Marker);
        }

        BuildDecor(root.transform, decor, mats);

        // Девять точек под будущие препятствия: 3 полосы × 3 ряда.
        var pointsRoot = new GameObject("SpawnPoints");
        pointsRoot.transform.SetParent(root.transform, false);

        var points = new List<Transform>();
        for (int row = 0; row < 3; row++)
        {
            float z = 7.5f + row * 7.5f;
            for (int lane = 0; lane < 3; lane++)
            {
                var point = new GameObject($"SP_r{row}_l{lane}");
                point.transform.SetParent(pointsRoot.transform, false);
                point.transform.localPosition = new Vector3((lane - 1) * LaneDistance, 0f, z);
                points.Add(point.transform);
            }
        }

        var so = new SerializedObject(chunk);
        so.FindProperty("length").floatValue = ChunkLength;
        so.FindProperty("unlockAtDistance").floatValue = unlockAt;
        so.FindProperty("weight").floatValue = weight;

        SerializedProperty spProp = so.FindProperty("spawnPoints");
        spProp.arraySize = points.Count;
        for (int i = 0; i < points.Count; i++)
            spProp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{ChunksFolder}/{prefabName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<Chunk>();
    }

    private static void BuildDecor(Transform parent, ChunkDecor decor, Materials mats)
    {
        switch (decor)
        {
            case ChunkDecor.Pillars:
                for (int i = 0; i < 4; i++)
                {
                    float z = 4f + i * 7.5f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box($"Pillar_{i}_{side}", parent,
                            new Vector3(0.7f, 3.4f, 0.7f),
                            new Vector3(side * 7.3f, 1.7f, z),
                            mats.Prop);
                    }
                }
                break;

            case ChunkDecor.Arches:
                for (int i = 0; i < 2; i++)
                {
                    float z = 9f + i * 12f;

                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box($"ArchPost_{i}_{side}", parent,
                            new Vector3(0.5f, 5f, 0.5f),
                            new Vector3(side * 7f, 2.5f, z),
                            mats.Prop);
                    }

                    Box($"ArchBeam_{i}", parent,
                        new Vector3(14.5f, 0.5f, 0.5f),
                        new Vector3(0f, 5.2f, z),
                        mats.Prop);
                }
                break;
        }
    }

    // ================================================================= игрок

    private static GameObject BuildPlayer(Materials mats)
    {
        // Корень стоит пивотом на полу — так проще считать высоту прыжка.
        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;

        var body = player.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = 0.4f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1f, 0f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        visual.GetComponent<Renderer>().sharedMaterial = mats.Player;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        Box("Nose", visual.transform,
            new Vector3(0.35f, 0.25f, 0.4f),
            new Vector3(0f, 0.35f, 0.55f),
            mats.Player, local: true);

        var swipe = player.AddComponent<SwipeDetector>();
        var controller = player.AddComponent<PlayerController>();
        var hud = player.AddComponent<DebugHud>();

        var so = new SerializedObject(controller);
        so.FindProperty("swipeDetector").objectReferenceValue = swipe;
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("player").objectReferenceValue = controller;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    // ================================================================ камера

    private static void SetUpCamera(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 300f;

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();

        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = target;
        so.ApplyModifiedPropertiesWithoutUndo();

        cam.transform.position = target.position + new Vector3(0f, 4.5f, -6f);
        cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
    }

    private static void SetUpLight()
    {
        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun == null || sun.type != LightType.Directional) return;

        sun.transform.rotation = Quaternion.Euler(45f, 25f, 0f);
        sun.shadows = LightShadows.Soft;
        sun.intensity = 1.1f;
    }

    // ========================================================= вспомогательное

    private static GameObject Box(string name, Transform parent, Vector3 scale, Vector3 position,
                                  Material material, bool keepCollider = false, bool local = false)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localScale = scale;

        if (local || parent != null) box.transform.localPosition = position;
        else box.transform.position = position;

        box.GetComponent<Renderer>().sharedMaterial = material;

        if (!keepCollider) Object.DestroyImmediate(box.GetComponent<Collider>());

        return box;
    }

    private static void Finish(GameObject select, string message)
    {
        Selection.activeGameObject = select;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[RunnerSceneBuilder] {message}");
    }

    private static void DeleteIfExists(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        while (existing != null)
        {
            Object.DestroyImmediate(existing);
            existing = GameObject.Find(objectName);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string leaf = path.Substring(lastSlash + 1);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static Material GetOrCreateMaterial(string assetName, Color color)
    {
        string path = $"{MaterialsFolder}/{assetName}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        EnsureFolder(MaterialsFolder);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader) { color = color };
        material.enableInstancing = true;

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
#endif
