#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Собирает тестовую сцену этапа M1 одной кнопкой: пол, разметку, игрока
/// и камеру, со всеми уже проставленными ссылками в инспекторе.
///
/// Меню: Tools → Runner → Собрать сцену M1
///
/// Это редакторный скрипт: он лежит в папке Editor и в игру не попадает.
/// </summary>
public static class RunnerSceneBuilder
{
    private const string MaterialsFolder = "Assets/_Project/Materials";

    private const float LaneDistance = 2.5f;
    private const float TrackLength = 2000f;
    private const float TrackWidth = 12f;
    private const float MarkerSpacing = 20f;

    [MenuItem("Tools/Runner/Собрать сцену M1")]
    public static void BuildM1Scene()
    {
        DeleteIfExists("Track");
        DeleteIfExists("Player");

        Material groundMat = GetOrCreateMaterial("M_Ground", new Color(0.20f, 0.22f, 0.26f));
        Material markerMat = GetOrCreateMaterial("M_Marker", new Color(0.85f, 0.85f, 0.88f));
        Material railMat = GetOrCreateMaterial("M_Rail", new Color(0.32f, 0.36f, 0.42f));
        Material playerMat = GetOrCreateMaterial("M_Player", new Color(0.95f, 0.45f, 0.15f));

        BuildTrack(groundMat, markerMat, railMat);
        GameObject player = BuildPlayer(playerMat);
        SetUpCamera(player.transform);
        SetUpLight();

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[RunnerSceneBuilder] Сцена M1 собрана. Жми Play.");
    }

    // ------------------------------------------------------------------ трасса

    private static void BuildTrack(Material groundMat, Material markerMat, Material railMat)
    {
        var track = new GameObject("Track");

        // Пол: верхняя грань ровно на y = 0.
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(track.transform);
        ground.transform.localScale = new Vector3(TrackWidth, 1f, TrackLength);
        ground.transform.position = new Vector3(0f, -0.5f, TrackLength * 0.5f - 20f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic |
                                                       StaticEditorFlags.ContributeGI);

        // Бортики по краям — дают ощущение коридора и глубины.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = side < 0 ? "Rail_Left" : "Rail_Right";
            rail.transform.SetParent(track.transform);
            rail.transform.localScale = new Vector3(0.4f, 1.6f, TrackLength);
            rail.transform.position = new Vector3(side * (TrackWidth * 0.5f + 0.2f), 0.8f,
                                                  TrackLength * 0.5f - 20f);
            rail.GetComponent<Renderer>().sharedMaterial = railMat;
            Object.DestroyImmediate(rail.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(rail, StaticEditorFlags.BatchingStatic);
        }

        // Поперечные полосы каждые 20 м — по ним видно скорость.
        var markers = new GameObject("Markers");
        markers.transform.SetParent(track.transform);

        int count = Mathf.FloorToInt(TrackLength / MarkerSpacing);
        for (int i = 1; i <= count; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"Marker_{i * (int)MarkerSpacing}m";
            marker.transform.SetParent(markers.transform);
            marker.transform.localScale = new Vector3(TrackWidth, 0.02f, 0.35f);
            marker.transform.position = new Vector3(0f, 0.01f, i * MarkerSpacing);
            marker.GetComponent<Renderer>().sharedMaterial = markerMat;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(marker, StaticEditorFlags.BatchingStatic);
        }

        // Тонкие линии, размечающие границы полос.
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = i < 0 ? "LaneLine_Left" : "LaneLine_Right";
            line.transform.SetParent(track.transform);
            line.transform.localScale = new Vector3(0.08f, 0.02f, TrackLength);
            line.transform.position = new Vector3(i * LaneDistance * 0.5f, 0.005f,
                                                  TrackLength * 0.5f - 20f);
            line.GetComponent<Renderer>().sharedMaterial = markerMat;
            Object.DestroyImmediate(line.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(line, StaticEditorFlags.BatchingStatic);
        }
    }

    // ------------------------------------------------------------------ игрок

    private static GameObject BuildPlayer(Material playerMat)
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

        // Видимая капсула — дочерний объект, её и будем сплющивать при подкате.
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        visual.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        // Нос, чтобы было видно, где перед.
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Nose";
        nose.transform.SetParent(visual.transform);
        nose.transform.localPosition = new Vector3(0f, 0.35f, 0.55f);
        nose.transform.localScale = new Vector3(0.35f, 0.25f, 0.4f);
        nose.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(nose.GetComponent<Collider>());

        var swipe = player.AddComponent<SwipeDetector>();
        var controller = player.AddComponent<PlayerController>();
        var hud = player.AddComponent<DebugHud>();

        // Проставляем ссылки в инспекторе за тебя.
        var so = new SerializedObject(controller);
        so.FindProperty("swipeDetector").objectReferenceValue = swipe;
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("player").objectReferenceValue = controller;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    // ----------------------------------------------------------------- камера

    private static void SetUpCamera(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
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

    // ------------------------------------------------------------ вспомогательное

    private static void DeleteIfExists(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    private static Material GetOrCreateMaterial(string assetName, Color color)
    {
        string path = $"{MaterialsFolder}/{assetName}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");

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
