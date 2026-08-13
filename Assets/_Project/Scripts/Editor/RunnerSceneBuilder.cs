#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

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
    private const string GameScenePath = ProjectRoot + "/Scenes/Game.unity";
    private const string MaterialsFolder = ProjectRoot + "/Materials";
    private const string ChunksFolder = ProjectRoot + "/Prefabs/Chunks";
    private const string ObstaclesFolder = ProjectRoot + "/Prefabs/Obstacles";
    private const string CharactersFolder = ProjectRoot + "/Characters";
    private const string KenneyCityTrainPath = ProjectRoot + "/ThirdParty/KenneyTrain/train-electric-city-b.fbx";

    private const float LaneDistance = 2.5f;
    private const float TrackWidth = 12f;
    private const float ChunkLength = 30f;

    // ===================================================================== M1

    [MenuItem("Tools/Runner/M1 — сцена с длинным полом")]
    public static void BuildM1Scene()
    {
        if (!EnsureGameSceneOpen()) return;

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
        if (!EnsureGameSceneOpen()) return;

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

    // ===================================================================== M3

    [MenuItem("Tools/Runner/M3 — препятствия и Game Over")]
    public static void BuildM3Scene()
    {
        if (!EnsureGameSceneOpen()) return;

        DeleteIfExists("Track");
        DeleteIfExists("ChunkSpawner");
        DeleteIfExists("Player");
        DeleteIfExists("GameManager");

        Materials mats = LoadMaterials();

        List<Chunk> chunkPrefabs = CreateChunkPrefabs(mats);
        Obstacle block = CreateObstaclePrefab("Obstacle_Block", Obstacle.Kind.Block, mats);
        Obstacle jump = CreateObstaclePrefab("Obstacle_Jump", Obstacle.Kind.JumpOver, mats);
        Obstacle slide = CreateObstaclePrefab("Obstacle_Slide", Obstacle.Kind.SlideUnder, mats);

        GameObject player = BuildPlayer(mats);
        SetUpCamera(player.transform);
        SetUpLight();

        // В отладочных сценах M3/M4 интерфейса нет — сразу стартуем забег.
        var m3Manager = new GameObject("GameManager").AddComponent<GameManager>();
        var m3ManagerSo = new SerializedObject(m3Manager);
        m3ManagerSo.FindProperty("skipMenu").boolValue = true;
        m3ManagerSo.ApplyModifiedPropertiesWithoutUndo();

        var spawnerGo = new GameObject("ChunkSpawner");
        var chunkSpawner = spawnerGo.AddComponent<ChunkSpawner>();
        var obstacleSpawner = spawnerGo.AddComponent<ObstacleSpawner>();

        var obstacleSo = new SerializedObject(obstacleSpawner);
        obstacleSo.FindProperty("blockPrefab").objectReferenceValue = block;
        obstacleSo.FindProperty("jumpPrefab").objectReferenceValue = jump;
        obstacleSo.FindProperty("slidePrefab").objectReferenceValue = slide;
        obstacleSo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(chunkSpawner);
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.FindProperty("obstacleSpawner").objectReferenceValue = obstacleSpawner;

        SerializedProperty prefabsProp = so.FindProperty("chunkPrefabs");
        prefabsProp.arraySize = chunkPrefabs.Count;
        for (int i = 0; i < chunkPrefabs.Count; i++)
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = chunkPrefabs[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        var hud = player.GetComponent<DebugHud>();
        if (hud != null)
        {
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("spawner").objectReferenceValue = chunkSpawner;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Finish(player, "Сцена M3 собрана: препятствия, столкновения, рестарт. Жми Play.");
    }

    // ===================================================================== M4

    [MenuItem("Tools/Runner/M4 — монеты, очки и сохранения")]
    public static void BuildM4Scene()
    {
        if (!EnsureGameSceneOpen()) return;

        DeleteIfExists("Track");
        DeleteIfExists("ChunkSpawner");
        DeleteIfExists("Player");
        DeleteIfExists("GameManager");

        Materials mats = LoadMaterials();

        List<Chunk> chunkPrefabs = CreateChunkPrefabs(mats);
        Obstacle block = CreateObstaclePrefab("Obstacle_Block", Obstacle.Kind.Block, mats);
        Obstacle jump = CreateObstaclePrefab("Obstacle_Jump", Obstacle.Kind.JumpOver, mats);
        Obstacle slide = CreateObstaclePrefab("Obstacle_Slide", Obstacle.Kind.SlideUnder, mats);
        Coin coin = CreateCoinPrefab(mats);

        GameObject player = BuildPlayer(mats);
        SetUpCamera(player.transform);
        SetUpLight();

        var managerGo = new GameObject("GameManager");
        var m4Manager = managerGo.AddComponent<GameManager>();
        var scoreManager = managerGo.AddComponent<ScoreManager>();

        var m4ManagerSo = new SerializedObject(m4Manager);
        m4ManagerSo.FindProperty("skipMenu").boolValue = true;
        m4ManagerSo.ApplyModifiedPropertiesWithoutUndo();

        var scoreSo = new SerializedObject(scoreManager);
        scoreSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        scoreSo.ApplyModifiedPropertiesWithoutUndo();

        var spawnerGo = new GameObject("ChunkSpawner");
        var chunkSpawner = spawnerGo.AddComponent<ChunkSpawner>();
        var obstacleSpawner = spawnerGo.AddComponent<ObstacleSpawner>();

        var obstacleSo = new SerializedObject(obstacleSpawner);
        obstacleSo.FindProperty("blockPrefab").objectReferenceValue = block;
        obstacleSo.FindProperty("jumpPrefab").objectReferenceValue = jump;
        obstacleSo.FindProperty("slidePrefab").objectReferenceValue = slide;
        obstacleSo.FindProperty("coinPrefab").objectReferenceValue = coin;
        obstacleSo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(chunkSpawner);
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.FindProperty("obstacleSpawner").objectReferenceValue = obstacleSpawner;

        SerializedProperty prefabsProp = so.FindProperty("chunkPrefabs");
        prefabsProp.arraySize = chunkPrefabs.Count;
        for (int i = 0; i < chunkPrefabs.Count; i++)
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = chunkPrefabs[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        var hud = player.GetComponent<DebugHud>();
        if (hud != null)
        {
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("spawner").objectReferenceValue = chunkSpawner;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Finish(player, "Сцена M4 собрана: монеты, счёт, сохранение рекорда. Жми Play.");
    }

    private static Coin CreateCoinPrefab(Materials mats)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ProjectRoot + "/Prefabs/Pickups");

        var root = new GameObject("Coin");
        Coin coin = root.AddComponent<Coin>();

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;

        // Монета — сплюснутый цилиндр, поставленный на ребро.
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visual.transform.localScale = new Vector3(0.7f, 0.05f, 0.7f);
        visual.GetComponent<Renderer>().sharedMaterial = mats.Coin;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        // Raised center and embossed star make the pickup readable at a
        // glance, matching the hero concept instead of a plain gold disc.
        GameObject inset = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        inset.name = "Inset";
        inset.transform.SetParent(root.transform, false);
        inset.transform.localPosition = new Vector3(0f, 0f, -0.07f);
        inset.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        inset.transform.localScale = new Vector3(0.54f, 0.035f, 0.54f);
        inset.GetComponent<Renderer>().sharedMaterial = mats.Coin;
        Object.DestroyImmediate(inset.GetComponent<Collider>());

        GameObject star = CreateCoinStar(mats.Marker);
        star.transform.SetParent(root.transform, false);
        star.transform.localPosition = new Vector3(0f, 0f, -0.12f);

        var so = new SerializedObject(coin);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{ProjectRoot}/Prefabs/Pickups/Coin.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<Coin>();
    }

    private static GameObject CreateCoinStar(Material material)
    {
        const int points = 10;
        const float depth = 0.055f;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (int face = 0; face < 2; face++)
        {
            float z = face == 0 ? -depth * 0.5f : depth * 0.5f;
            vertices.Add(new Vector3(0f, 0f, z));
            for (int i = 0; i < points; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / points;
                float radius = (i & 1) == 0 ? 0.34f : 0.16f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z));
            }
        }

        for (int i = 0; i < points; i++)
        {
            int next = (i + 1) % points;
            triangles.Add(0); triangles.Add(1 + i); triangles.Add(1 + next);
            int backCenter = points + 1;
            triangles.Add(backCenter); triangles.Add(backCenter + 1 + next); triangles.Add(backCenter + 1 + i);

            int f0 = 1 + i, f1 = 1 + next;
            int b0 = points + 2 + i, b1 = points + 2 + next;
            triangles.Add(f0); triangles.Add(b0); triangles.Add(f1);
            triangles.Add(f1); triangles.Add(b0); triangles.Add(b1);
        }

        var mesh = new Mesh { name = "CoinStarMesh" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Star");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // ================================================================== M6+M7

    [MenuItem("Tools/Runner/M6+M7 — полная игра: интерфейс, бонусы, магазин")]
    public static void BuildM6Scene()
    {
        if (!EnsureGameSceneOpen()) return;

        DeleteIfExists("Track");
        DeleteIfExists("ChunkSpawner");
        DeleteIfExists("Player");
        DeleteIfExists("GameManager");
        DeleteIfExists("UI");
        DeleteIfExists("EventSystem");
        // Витрина — корневой объект, а не ребёнок UI. Если не удалить её
        // отдельно, каждая полная пересборка оставляет ещё одну камеру и
        // RenderTexture в сцене. Это и было источником лишних объектов.
        DeleteIfExists("CharacterLobbyPreview");

        Materials mats = LoadMaterials();

        List<Chunk> chunkPrefabs = CreateChunkPrefabs(mats);
        Obstacle block = CreateObstaclePrefab("Obstacle_Block", Obstacle.Kind.Block, mats);
        Obstacle jump = CreateObstaclePrefab("Obstacle_Jump", Obstacle.Kind.JumpOver, mats);
        Obstacle slide = CreateObstaclePrefab("Obstacle_Slide", Obstacle.Kind.SlideUnder, mats);
        Obstacle train = CreateTrainPrefab(mats);
        GameObject ramp = CreateRampPrefab(mats);
        Coin coin = CreateCoinPrefab(mats);

        var powerUps = new List<PowerUp>
        {
            CreatePowerUpPrefab(PowerUpType.Magnet),
            CreatePowerUpPrefab(PowerUpType.Coffee),
            CreatePowerUpPrefab(PowerUpType.Sneakers),
            CreatePowerUpPrefab(PowerUpType.DoubleScore)
        };

        GameObject player = BuildPlayer(mats);
        player.AddComponent<CoinMagnet>();
        player.AddComponent<NearMissDetector>();
        CameraFollow follow = SetUpCamera(player.transform);
        SetUpLight();

        // --- генератор трассы ---
        var spawnerGo = new GameObject("ChunkSpawner");
        var chunkSpawner = spawnerGo.AddComponent<ChunkSpawner>();
        var obstacleSpawner = spawnerGo.AddComponent<ObstacleSpawner>();

        var obstacleSo = new SerializedObject(obstacleSpawner);
        obstacleSo.FindProperty("blockPrefab").objectReferenceValue = block;
        obstacleSo.FindProperty("jumpPrefab").objectReferenceValue = jump;
        obstacleSo.FindProperty("slidePrefab").objectReferenceValue = slide;
        obstacleSo.FindProperty("trainPrefab").objectReferenceValue = train;
        obstacleSo.FindProperty("rampPrefab").objectReferenceValue = ramp;
        obstacleSo.FindProperty("coinPrefab").objectReferenceValue = coin;

        SerializedProperty powerUpsProp = obstacleSo.FindProperty("powerUpPrefabs");
        powerUpsProp.arraySize = powerUps.Count;
        for (int i = 0; i < powerUps.Count; i++)
            powerUpsProp.GetArrayElementAtIndex(i).objectReferenceValue = powerUps[i];

        obstacleSo.ApplyModifiedPropertiesWithoutUndo();

        var chunkSo = new SerializedObject(chunkSpawner);
        chunkSo.FindProperty("player").objectReferenceValue = player.transform;
        chunkSo.FindProperty("obstacleSpawner").objectReferenceValue = obstacleSpawner;

        SerializedProperty prefabsProp = chunkSo.FindProperty("chunkPrefabs");
        prefabsProp.arraySize = chunkPrefabs.Count;
        for (int i = 0; i < chunkPrefabs.Count; i++)
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = chunkPrefabs[i];

        chunkSo.ApplyModifiedPropertiesWithoutUndo();

        // --- менеджеры ---
        var managerGo = new GameObject("GameManager");
        var gameManager = managerGo.AddComponent<GameManager>();
        var scoreManager = managerGo.AddComponent<ScoreManager>();
        var powerUpManager = managerGo.AddComponent<PowerUpManager>();
        var characterManager = managerGo.AddComponent<CharacterManager>();

        var audioManager = managerGo.AddComponent<AudioManager>();
        var effectManager = managerGo.AddComponent<EffectManager>();
        var gameFeel = managerGo.AddComponent<GameFeel>();
        var screenEffects = managerGo.AddComponent<ScreenEffects>();

        var charSo = new SerializedObject(characterManager);
        charSo.FindProperty("database").objectReferenceValue = EnsureCharacterDatabase();
        charSo.FindProperty("playerVisual").objectReferenceValue = player.transform.Find("Visual");
        charSo.ApplyModifiedPropertiesWithoutUndo();

        // --- звук ---
        RunnerAudioBuilder.EnsureGenerated();

        var audioSo = new SerializedObject(audioManager);
        audioSo.FindProperty("musicClip").objectReferenceValue = LoadClip("Music_Loop");
        audioSo.FindProperty("coinClip").objectReferenceValue = LoadClip("SFX_Coin");
        audioSo.FindProperty("powerUpClip").objectReferenceValue = LoadClip("SFX_PowerUp");
        audioSo.FindProperty("jumpClip").objectReferenceValue = LoadClip("SFX_Jump");
        audioSo.FindProperty("slideClip").objectReferenceValue = LoadClip("SFX_Slide");
        audioSo.FindProperty("crashClip").objectReferenceValue = LoadClip("SFX_Crash");
        audioSo.FindProperty("buttonClip").objectReferenceValue = LoadClip("SFX_Button");
        audioSo.ApplyModifiedPropertiesWithoutUndo();

        // --- партиклы ---
        ParticleSystem coinBurst = CreateBurstPrefab(
            "FX_CoinBurst", mats.Coin,
            count: 8, speed: 3.5f, size: 0.16f, lifetime: 0.5f, gravity: 1.4f);

        ParticleSystem crashBurst = CreateBurstPrefab(
            "FX_CrashBurst", GetOrCreateMaterial("M_Crash", new Color(0.85f, 0.25f, 0.20f)),
            count: 16, speed: 6f, size: 0.22f, lifetime: 0.7f, gravity: 1.8f);

        var effectSo = new SerializedObject(effectManager);
        effectSo.FindProperty("coinBurstPrefab").objectReferenceValue = coinBurst;
        effectSo.FindProperty("crashBurstPrefab").objectReferenceValue = crashBurst;
        effectSo.ApplyModifiedPropertiesWithoutUndo();

        var scoreSo = new SerializedObject(scoreManager);
        scoreSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        scoreSo.ApplyModifiedPropertiesWithoutUndo();

        var powerSo = new SerializedObject(powerUpManager);
        powerSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        powerSo.ApplyModifiedPropertiesWithoutUndo();

        var gameSo = new SerializedObject(gameManager);
        gameSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        gameSo.FindProperty("chunkSpawner").objectReferenceValue = chunkSpawner;
        gameSo.FindProperty("obstacleSpawner").objectReferenceValue = obstacleSpawner;
        gameSo.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        gameSo.FindProperty("powerUpManager").objectReferenceValue = powerUpManager;
        gameSo.FindProperty("characterManager").objectReferenceValue = characterManager;
        gameSo.FindProperty("effectManager").objectReferenceValue = effectManager;
        gameSo.FindProperty("cameraFollow").objectReferenceValue = follow;
        gameSo.FindProperty("gameFeel").objectReferenceValue = gameFeel;
        gameSo.ApplyModifiedPropertiesWithoutUndo();

        var feelSo = new SerializedObject(gameFeel);
        feelSo.FindProperty("cameraFollow").objectReferenceValue = follow;
        feelSo.ApplyModifiedPropertiesWithoutUndo();

        var screenSo = new SerializedObject(screenEffects);
        screenSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
        // Ссылку на Global Volume не проставляем: он живёт в сцене отдельно
        // от сборщика, и ScreenEffects находит его сам в Awake.
        screenSo.ApplyModifiedPropertiesWithoutUndo();

        // Отладочный текст больше не нужен — но оставляем компонент,
        // чтобы можно было включить галочкой Show.
        var hud = player.GetComponent<DebugHud>();
        if (hud != null)
        {
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("spawner").objectReferenceValue = chunkSpawner;
            hudSo.FindProperty("show").boolValue = false;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        BuildUserInterface(player.GetComponent<PlayerController>(), scoreManager, characterManager);

        // Внешний вид применяем последним: он перенастраивает солнце и камеру,
        // которые выше создавались с базовыми значениями.
        RunnerLookBuilder.Apply();

        Finish(player, "Полная сцена собрана: меню, HUD, бонусы, магазин, персонажи, звук, партиклы, " +
                       "постобработка и небо. Жми Play.");
    }

    private static PowerUp CreatePowerUpPrefab(PowerUpType type)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ProjectRoot + "/Prefabs/Pickups");

        string prefabName = $"PowerUp_{type}";
        Color color = UpgradeShop.ColorFor(type);
        Material material = GetOrCreateMaterial($"M_PowerUp{type}", color);

        var root = new GameObject(prefabName);
        PowerUp powerUp = root.AddComponent<PowerUp>();

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.8f;
        trigger.center = Vector3.zero;

        // Куб, повёрнутый на 45° — читается как «кристалл» и отличается
        // от монеты силуэтом, а не только цветом.
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        visual.transform.localScale = Vector3.one * 0.75f;
        visual.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        var so = new SerializedObject(powerUp);
        so.FindProperty("type").enumValueIndex = (int)type;
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{ProjectRoot}/Prefabs/Pickups/{prefabName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<PowerUp>();
    }

    // ============================================================== эффекты

    /// <summary>
    /// Префаб вспышки частиц. Частицы — кубики (Mesh render mode), а не
    /// спрайты: так они рисуются теми же URP-материалами, что и весь мир,
    /// и не требуют отдельного шейдера для партиклов.
    /// </summary>
    private static ParticleSystem CreateBurstPrefab(string prefabName, Material material,
                                                    int count, float speed, float size,
                                                    float lifetime, float gravity)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ProjectRoot + "/Prefabs/Effects");

        var root = new GameObject(prefabName);
        var system = root.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.gravityModifier = gravity;
        main.maxParticles = count * 2;

        // World: частицы не едут за игроком, а остаются там, где вспыхнули.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Частицы уменьшаются к концу жизни — иначе они просто исчезают рывком.
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(
            1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = PrimitiveMesh(PrimitiveType.Cube);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        string path = $"{ProjectRoot}/Prefabs/Effects/{prefabName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<ParticleSystem>();
    }

    /// <summary>Меш встроенного примитива без оставленного в сцене объекта.</summary>
    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);

        return mesh;
    }

    private static AudioClip LoadClip(string assetName) =>
        AssetDatabase.LoadAssetAtPath<AudioClip>($"{ProjectRoot}/Audio/{assetName}.wav");

    // =========================================================== персонажи

    /// <summary>
    /// Создаёт список персонажей, если его ещё нет.
    ///
    /// Уже существующие ассеты НЕ перезаписываются — иначе пересборка сцены
    /// стирала бы переименованных учителей и подставленные модели.
    /// Хочешь начать заново — удали папку Characters руками.
    /// </summary>
    private static CharacterDatabase EnsureCharacterDatabase()
    {
        EnsureFolder(CharactersFolder);

        string databasePath = CharactersFolder + "/CharacterDatabase.asset";

        var existing = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(databasePath);
        if (existing != null) return existing;

        var characters = new List<CharacterData>
        {
            EnsureCharacter("rookie", "Новичок", "Так, я побежал!",
                            new Color(0.72f, 0.74f, 0.78f), price: 0, free: true,
                            CharacterAbility.None, 0f),

            EnsureCharacter("pe", "Физрук", "Ещё круг — и по домам!",
                            new Color(0.40f, 0.80f, 0.45f), price: 400, free: false,
                            CharacterAbility.FastStart, 4f),

            EnsureCharacter("math", "Математичка", "Дистанция — это интеграл скорости.",
                            new Color(0.35f, 0.62f, 0.95f), price: 900, free: false,
                            CharacterAbility.CoinBonus, 0.10f),

            EnsureCharacter("chem", "Химичка", "Не трогай, оно ещё реагирует!",
                            new Color(0.72f, 0.42f, 0.88f), price: 1500, free: false,
                            CharacterAbility.LongerPowerUps, 2f),

            EnsureCharacter("principal", "Директор", "В моей школе не бегают. Кроме сегодня.",
                            new Color(0.90f, 0.35f, 0.35f), price: 2500, free: false,
                            CharacterAbility.Shield, 1f)
        };

        var database = ScriptableObject.CreateInstance<CharacterDatabase>();
        AssetDatabase.CreateAsset(database, databasePath);

        var so = new SerializedObject(database);
        SerializedProperty list = so.FindProperty("characters");
        list.arraySize = characters.Count;

        for (int i = 0; i < characters.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = characters[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        return database;
    }

    private static CharacterData EnsureCharacter(string id, string displayName, string phrase,
                                                 Color tint, int price, bool free,
                                                 CharacterAbility ability, float abilityValue)
    {
        string path = $"{CharactersFolder}/Character_{id}.asset";

        var existing = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<CharacterData>();
        AssetDatabase.CreateAsset(asset, path);

        var so = new SerializedObject(asset);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("catchPhrase").stringValue = phrase;
        so.FindProperty("tint").colorValue = tint;
        so.FindProperty("price").intValue = price;
        so.FindProperty("unlockedByDefault").boolValue = free;
        so.FindProperty("ability").enumValueIndex = (int)ability;
        so.FindProperty("abilityValue").floatValue = abilityValue;
        so.ApplyModifiedPropertiesWithoutUndo();

        return asset;
    }

    // ================================================================== UI

    private const float UIReferenceWidth = 1080f;
    private const float UIReferenceHeight = 1920f;

    private static readonly Color PanelDim = new Color(0.035f, 0.075f, 0.09f, 0.90f);
    private static readonly Color ButtonMain = new Color(0.72f, 0.22f, 0.12f, 1f);
    private static readonly Color ButtonSecondary = new Color(0.035f, 0.25f, 0.29f, 0.98f);
    private static readonly Color CoinGold = new Color(0.98f, 0.65f, 0.12f, 1f);

    private static void BuildUserInterface(PlayerController player, ScoreManager score,
                                           CharacterManager characters)
    {
        // Система событий. Обязательно InputSystemUIInputModule, а не старый
        // StandaloneInputModule: проект переведён на новый Input System,
        // и со старым модулем кнопки просто не нажимаются.
        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        var inputModule = eventSystemGo.AddComponent<InputSystemUIInputModule>();

        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/InputSystem_Actions.inputactions");
        if (actions != null) inputModule.actionsAsset = actions;

        // Canvas.
        var canvasGo = new GameObject("UI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UIReferenceWidth, UIReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        var ui = canvasGo.AddComponent<UIManager>();

        // Контейнер, поджатый под безопасную зону.
        GameObject safeArea = UIObject("SafeArea", canvasGo.transform);
        safeArea.AddComponent<SafeAreaFitter>();

        GameObject menu = BuildMenuPanel(safeArea.transform, out Text menuBest,
                                         out Text menuCoins, out Text menuCharacter,
                                         out Button playButton, out Button charactersButton,
                                         out Button shopButton, out Button settingsButton);

        GameObject hudPanel = BuildHudPanel(safeArea.transform, out Text distanceText,
                                            out Text coinsText, out Text openingGuideText,
                                            out Text shieldText, out Button pauseButton,
                                            out GameObject[] barRoots,
                                            out RectTransform[] barFills);

        GameObject pause = BuildPausePanel(safeArea.transform, out Button resume,
                                           out Button pauseRestart, out Button pauseMenu);

        GameObject over = BuildGameOverPanel(safeArea.transform, out Text overTitle,
                                             out Text overStats, out Button overRestart,
                                             out Button overMenu);

        GameObject shop = BuildShopPanel(safeArea.transform, out Text shopCoins,
                                         out Text[] shopNames, out Text[] shopEffects,
                                         out Button[] shopBuys, out Text[] shopBuyLabels,
                                         out Button shopClose);

        GameObject settings = BuildSettingsPanel(safeArea.transform,
                                                 out Button music, out Text musicLabel,
                                                 out Button sound, out Text soundLabel,
                                                 out Button vibration, out Text vibrationLabel,
                                                 out Button reset, out Text resetLabel,
                                                 out Button settingsClose);

        CharacterPanelRefs charactersUi = BuildCharactersPanel(safeArea.transform);

        // Витрина персонажа рисуется отдельной камерой в RawImage панели.
        // Она создаётся один раз при полной пересборке, а саму маленькую
        // 3D-студию компонент собирает в рантайме далеко от трассы.
        var lobbyPreviewGo = new GameObject("CharacterLobbyPreview");
        var lobbyPreview = lobbyPreviewGo.AddComponent<CharacterLobbyPreview>();
        var previewSo = new SerializedObject(lobbyPreview);
        previewSo.FindProperty("targetImage").objectReferenceValue = charactersUi.Preview;
        previewSo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(ui);
        so.FindProperty("menuPanel").objectReferenceValue = menu;
        so.FindProperty("hudPanel").objectReferenceValue = hudPanel;
        so.FindProperty("pausePanel").objectReferenceValue = pause;
        so.FindProperty("gameOverPanel").objectReferenceValue = over;
        so.FindProperty("shopPanel").objectReferenceValue = shop;
        so.FindProperty("settingsPanel").objectReferenceValue = settings;

        so.FindProperty("charactersPanel").objectReferenceValue = charactersUi.Panel;

        so.FindProperty("menuBestText").objectReferenceValue = menuBest;
        so.FindProperty("menuCoinsText").objectReferenceValue = menuCoins;
        so.FindProperty("menuCharacterText").objectReferenceValue = menuCharacter;
        so.FindProperty("playButton").objectReferenceValue = playButton;
        so.FindProperty("charactersButton").objectReferenceValue = charactersButton;
        so.FindProperty("shopButton").objectReferenceValue = shopButton;
        so.FindProperty("settingsButton").objectReferenceValue = settingsButton;

        so.FindProperty("hudDistanceText").objectReferenceValue = distanceText;
        so.FindProperty("hudCoinsText").objectReferenceValue = coinsText;
        so.FindProperty("openingGuideText").objectReferenceValue = openingGuideText;
        so.FindProperty("shieldText").objectReferenceValue = shieldText;
        so.FindProperty("pauseButton").objectReferenceValue = pauseButton;

        SetArray(so, "powerUpBarRoots", barRoots);
        SetArray(so, "powerUpBarFills", barFills);

        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("pauseRestartButton").objectReferenceValue = pauseRestart;
        so.FindProperty("pauseMenuButton").objectReferenceValue = pauseMenu;

        so.FindProperty("gameOverTitleText").objectReferenceValue = overTitle;
        so.FindProperty("gameOverStatsText").objectReferenceValue = overStats;
        so.FindProperty("gameOverRestartButton").objectReferenceValue = overRestart;
        so.FindProperty("gameOverMenuButton").objectReferenceValue = overMenu;

        so.FindProperty("shopCoinsText").objectReferenceValue = shopCoins;
        SetArray(so, "shopNameTexts", shopNames);
        SetArray(so, "shopEffectTexts", shopEffects);
        SetArray(so, "shopBuyButtons", shopBuys);
        SetArray(so, "shopBuyLabels", shopBuyLabels);
        so.FindProperty("shopCloseButton").objectReferenceValue = shopClose;

        so.FindProperty("musicButton").objectReferenceValue = music;
        so.FindProperty("musicLabel").objectReferenceValue = musicLabel;
        so.FindProperty("soundButton").objectReferenceValue = sound;
        so.FindProperty("soundLabel").objectReferenceValue = soundLabel;
        so.FindProperty("vibrationButton").objectReferenceValue = vibration;
        so.FindProperty("vibrationLabel").objectReferenceValue = vibrationLabel;
        so.FindProperty("resetButton").objectReferenceValue = reset;
        so.FindProperty("resetLabel").objectReferenceValue = resetLabel;
        so.FindProperty("settingsCloseButton").objectReferenceValue = settingsClose;

        so.FindProperty("charactersCoinsText").objectReferenceValue = charactersUi.Coins;
        so.FindProperty("characterLobby").objectReferenceValue = lobbyPreview;
        so.FindProperty("charactersStatusText").objectReferenceValue = charactersUi.Status;
        so.FindProperty("charactersNameText").objectReferenceValue = charactersUi.Name;
        so.FindProperty("charactersAbilityText").objectReferenceValue = charactersUi.Ability;
        so.FindProperty("charactersPhraseText").objectReferenceValue = charactersUi.Phrase;
        so.FindProperty("charactersCountText").objectReferenceValue = charactersUi.Count;
        so.FindProperty("charactersPrevButton").objectReferenceValue = charactersUi.Prev;
        so.FindProperty("charactersNextButton").objectReferenceValue = charactersUi.Next;
        so.FindProperty("charactersActionButton").objectReferenceValue = charactersUi.Action;
        so.FindProperty("charactersActionLabel").objectReferenceValue = charactersUi.ActionLabel;
        so.FindProperty("charactersCloseButton").objectReferenceValue = charactersUi.Close;

        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("score").objectReferenceValue = score;
        so.FindProperty("characters").objectReferenceValue = characters;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Панели разбирает UIManager, но только в Play mode — в Start().
        // Пока игра не запущена, все они включены и наваливаются друг на друга,
        // и Game view выглядит как каша. Оставляем видимым одно меню.
        hudPanel.SetActive(false);
        pause.SetActive(false);
        over.SetActive(false);
        shop.SetActive(false);
        settings.SetActive(false);
        charactersUi.Panel.SetActive(false);
        menu.SetActive(true);
    }

    private static void SetArray(SerializedObject so, string propertyName, Object[] values)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null) return;

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static GameObject BuildMenuPanel(Transform parent, out Text bestText,
                                             out Text coinsText, out Text characterText,
                                             out Button playButton, out Button charactersButton,
                                             out Button shopButton, out Button settingsButton)
    {
        // Главное меню — стартовая карточка игры: вокруг видно трассу, а
        // интерфейс сразу показывает прогресс, выбранного бегуна и действие.
        GameObject panel = UIPanel("MenuPanel", parent, new Color(0.025f, 0.07f, 0.08f, 0.42f));

        UIBlock("TopGlow", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -26f),
                new Vector2(1080f, 10f), new Color(0.98f, 0.64f, 0.14f, 0.95f));
        UIBlock("TitlePlate", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -250f),
                new Vector2(860f, 210f), new Color(0.035f, 0.16f, 0.18f, 0.84f));

        Text title = UIText("Title", panel.transform, "CAMPUS RUSH", 108, TextAnchor.MiddleCenter,
                            new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(900f, 160f));
        title.color = new Color(0.96f, 0.86f, 0.68f);
        AddTextShadow(title, new Color(0.02f, 0.08f, 0.10f, 0.9f), new Vector2(4f, -4f));

        UIText("Subtitle", panel.transform, "БЕГИ  ·  УЧИСЬ  ·  ПОБЕЖДАЙ", 29, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -325f), new Vector2(900f, 60f)).color =
            new Color(0.67f, 0.90f, 0.83f);

        GameObject bestCard = UIBlock("BestCard", panel.transform, new Vector2(0.5f, 0.5f),
                                      new Vector2(-245f, 285f), new Vector2(430f, 132f),
                                      new Color(0.035f, 0.18f, 0.22f, 0.95f));
        AddOutline(bestCard, new Color(0.24f, 0.67f, 0.62f, 0.65f), 2f);
        UIText("BestCaption", bestCard.transform, "ЛУЧШИЙ ЗАБЕГ", 23, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(390f, 42f)).color =
            new Color(0.57f, 0.86f, 0.80f);
        bestText = UIText("Best", bestCard.transform, "Рекорд: 0 м", 40, TextAnchor.MiddleCenter,
                          new Vector2(0.5f, 0.5f), new Vector2(0f, -17f), new Vector2(400f, 62f));

        GameObject coinsCard = UIBlock("CoinsCard", panel.transform, new Vector2(0.5f, 0.5f),
                                       new Vector2(245f, 285f), new Vector2(430f, 132f),
                                       new Color(0.27f, 0.13f, 0.055f, 0.96f));
        AddOutline(coinsCard, new Color(0.95f, 0.72f, 0.2f, 0.8f), 2f);
        UIText("CoinsCaption", coinsCard.transform, "ВСЕГО МОНЕТ", 23, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(390f, 42f)).color =
            new Color(1f, 0.83f, 0.36f);
        coinsText = UIText("Coins", coinsCard.transform, "Монет: 0", 40, TextAnchor.MiddleCenter,
                           new Vector2(0.5f, 0.5f), new Vector2(0f, -17f), new Vector2(400f, 62f));
        coinsText.color = CoinGold;

        GameObject runnerCard = UIBlock("RunnerCard", panel.transform, new Vector2(0.5f, 0.5f),
                                        new Vector2(0f, 110f), new Vector2(850f, 105f),
                                        new Color(0.045f, 0.13f, 0.16f, 0.94f));
        AddOutline(runnerCard, new Color(0.18f, 0.55f, 0.51f, 0.72f), 2f);
        UIText("RunnerCaption", runnerCard.transform, "НА СТАРТЕ", 22, TextAnchor.MiddleLeft,
               new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(240f, 50f)).color =
            new Color(0.66f, 0.86f, 0.82f);
        characterText = UIText("Character", runnerCard.transform, "", 34, TextAnchor.MiddleRight,
                               new Vector2(1f, 0.5f), new Vector2(-270f, 0f), new Vector2(530f, 55f));

        playButton = UIButton("PlayButton", panel.transform, "ВПЕРЁД!", 68, ButtonMain,
                              new Vector2(0.5f, 0.5f), new Vector2(0f, -85f),
                              new Vector2(820f, 180f));
        AddOutline(playButton.gameObject, new Color(0.98f, 0.66f, 0.18f, 0.95f), 3f);
        UIText("PlayHint", playButton.transform, "НАЧАТЬ ЗАБЕГ", 22, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(620f, 42f)).color =
            new Color(0.22f, 0.055f, 0.025f);

        charactersButton = UIButton("CharactersButton", panel.transform, "КОМАНДА", 42,
                                    ButtonSecondary, new Vector2(0.5f, 0.5f),
                                    new Vector2(-230f, -290f), new Vector2(430f, 125f));
        shopButton = UIButton("ShopButton", panel.transform, "МАГАЗИН", 42, ButtonSecondary,
                              new Vector2(0.5f, 0.5f), new Vector2(230f, -290f), new Vector2(430f, 125f));
        settingsButton = UIButton("SettingsButton", panel.transform, "НАСТРОЙКИ", 38,
                                  ButtonSecondary, new Vector2(0.5f, 0.5f), new Vector2(0f, -445f),
                                  new Vector2(540f, 108f));

        UIText("Hint", panel.transform,
               "СВАЙПЫ  ←  →  ↑  ↓",
               24, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1000f, 54f)).color =
            new Color(0.68f, 0.84f, 0.80f);

        return panel;
    }

    /// <summary>
    /// Ссылки на элементы экрана выбора. Структура вместо десятка out-параметров:
    /// список полей будет расти, когда появятся портреты учителей.
    /// </summary>
    private struct CharacterPanelRefs
    {
        public GameObject Panel;
        public Text Coins;
        public RawImage Preview;
        public Text Status;
        public Text Name;
        public Text Ability;
        public Text Phrase;
        public Text Count;
        public Button Prev;
        public Button Next;
        public Button Action;
        public Text ActionLabel;
        public Button Close;
    }

    /// <summary>
    /// Карусель персонажей с живой 3D-витриной. RawImage получает картинку
    /// отдельной камеры: основная камера забега её не видит.
    /// </summary>
    private static CharacterPanelRefs BuildCharactersPanel(Transform parent)
    {
        var refs = new CharacterPanelRefs();

        // Экран выбора должен ощущаться как место перед стартом забега, а не
        // как обычный список. Поэтому здесь свой почти непрозрачный ночной
        // фон, большая витрина и одна ясная главная кнопка снизу.
        GameObject panel = UIPanel("CharactersPanel", parent, new Color(0.018f, 0.025f, 0.07f, 0.97f));
        refs.Panel = panel;

        // Тонкие неоновые линии дают экрану структуру, не требуя картинок
        // или дополнительных ассетов.
        UIBlock("TopGlow", panel.transform, new Vector2(0.5f, 1f), new Vector2(0f, -22f),
                new Vector2(1080f, 8f), new Color(0.48f, 0.25f, 0.95f, 0.88f));
        UIBlock("BottomGlow", panel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 22f),
                new Vector2(1080f, 6f), new Color(0.15f, 0.72f, 0.87f, 0.6f));

        refs.Close = UIButton("CloseButton", panel.transform, "<", 62, ButtonSecondary,
                              new Vector2(0f, 1f), new Vector2(110f, -130f),
                              new Vector2(130f, 100f));

        UIText("BackHint", panel.transform, "НАЗАД", 26, TextAnchor.MiddleLeft,
               new Vector2(0f, 1f), new Vector2(205f, -130f), new Vector2(150f, 60f)).color =
            new Color(0.64f, 0.7f, 0.84f);

        UIText("Title", panel.transform, "ВЫБОР БЕГУНА", 70, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(820f, 100f));
        UIText("Subtitle", panel.transform, "СОБЕРИ СВОЮ КОМАНДУ", 28, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -175f), new Vector2(820f, 54f)).color =
            new Color(0.55f, 0.64f, 0.84f);

        GameObject wallet = UIBlock("Wallet", panel.transform, new Vector2(1f, 1f),
                                    new Vector2(-160f, -130f), new Vector2(250f, 94f),
                                    new Color(0.18f, 0.13f, 0.06f, 0.98f));
        AddOutline(wallet, new Color(0.98f, 0.75f, 0.22f, 0.8f), 2f);
        refs.Coins = UIText("Coins", wallet.transform, "Монет: 0", 34, TextAnchor.MiddleCenter,
                            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(235f, 70f));
        refs.Coins.color = CoinGold;

        // Большая витрина: наружная окантовка, тёмная внутренняя рамка и
        // картинка 3D-камеры. Три слоя не дают модели потеряться на фоне.
        GameObject previewFrame = UIBlock("PreviewFrame", panel.transform,
                                          new Vector2(0.5f, 0.5f), new Vector2(0f, 215f),
                                          new Vector2(960f, 710f), new Color(0.16f, 0.08f, 0.34f, 1f));
        AddOutline(previewFrame, new Color(0.57f, 0.33f, 1f, 0.9f), 3f);

        GameObject previewInset = UIBlock("PreviewInset", previewFrame.transform,
                                          new Vector2(0.5f, 0.5f), Vector2.zero,
                                          new Vector2(936f, 686f), new Color(0.025f, 0.02f, 0.075f, 1f));

        var previewGo = new GameObject("Preview", typeof(RectTransform));
        previewGo.transform.SetParent(previewInset.transform, false);

        var previewRect = (RectTransform)previewGo.transform;
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.one;
        previewRect.offsetMin = new Vector2(8f, 8f);
        previewRect.offsetMax = new Vector2(-8f, -8f);

        refs.Preview = previewGo.AddComponent<RawImage>();
        refs.Preview.color = Color.white;
        refs.Preview.raycastTarget = false;

        UIBlock("PreviewTag", previewFrame.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                new Vector2(360f, 60f), new Color(0.08f, 0.055f, 0.17f, 0.94f));
        UIText("TagText", previewFrame.transform, "ТВОЙ СЛЕДУЮЩИЙ БЕГУН", 24, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(340f, 50f)).color =
            new Color(0.78f, 0.7f, 1f);

        refs.Prev = UIButton("PrevButton", panel.transform, "<", 82, ButtonSecondary,
                             new Vector2(0.5f, 0.5f), new Vector2(-440f, 215f),
                             new Vector2(118f, 148f));
        refs.Next = UIButton("NextButton", panel.transform, ">", 82, ButtonSecondary,
                             new Vector2(0.5f, 0.5f), new Vector2(440f, 215f),
                             new Vector2(118f, 148f));

        refs.Count = UIText("Count", panel.transform, "1 / 1", 32, TextAnchor.MiddleCenter,
                            new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(300f, 52f));
        refs.Count.color = new Color(0.56f, 0.66f, 0.9f);

        GameObject infoCard = UIBlock("InfoCard", panel.transform, new Vector2(0.5f, 0.5f),
                                      new Vector2(0f, -330f), new Vector2(920f, 250f),
                                      new Color(0.055f, 0.065f, 0.14f, 0.98f));
        AddOutline(infoCard, new Color(0.25f, 0.34f, 0.66f, 0.72f), 2f);

        refs.Status = UIText("Status", infoCard.transform, "ГОТОВ К ЗАБЕГУ", 26, TextAnchor.MiddleCenter,
                             new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(780f, 44f));

        refs.Name = UIText("Name", infoCard.transform, "—", 58, TextAnchor.MiddleCenter,
                           new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(860f, 76f));

        UIText("AbilityCaption", infoCard.transform, "ОСОБАЯ СПОСОБНОСТЬ", 22, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(800f, 38f)).color =
            new Color(0.5f, 0.65f, 0.96f);
        refs.Ability = UIText("Ability", infoCard.transform, "", 36, TextAnchor.MiddleCenter,
                              new Vector2(0.5f, 1f), new Vector2(0f, -174f),
                              new Vector2(850f, 56f));
        refs.Ability.color = new Color(0.55f, 0.85f, 0.65f);

        refs.Phrase = UIText("Phrase", infoCard.transform, "", 28, TextAnchor.MiddleCenter,
                             new Vector2(0.5f, 1f), new Vector2(0f, -220f),
                             new Vector2(840f, 54f));
        refs.Phrase.color = new Color(0.75f, 0.78f, 0.86f);
        refs.Phrase.fontStyle = FontStyle.Italic;
        // Длинную фразу переносим по словам, иначе она вылезет за экран.
        refs.Phrase.horizontalOverflow = HorizontalWrapMode.Wrap;

        refs.Action = UIButton("ActionButton", panel.transform, "ВЫБРАТЬ", 52, ButtonMain,
                               new Vector2(0.5f, 0.5f), new Vector2(0f, -570f),
                               new Vector2(760f, 150f));
        refs.ActionLabel = refs.Action.GetComponentInChildren<Text>();

        return refs;
    }

    private static GameObject BuildHudPanel(Transform parent, out Text distanceText,
                                            out Text coinsText, out Text openingGuideText,
                                            out Text shieldText, out Button pauseButton,
                                            out GameObject[] barRoots,
                                            out RectTransform[] barFills)
    {
        GameObject panel = UIObject("HudPanel", parent);

        distanceText = UIText("Distance", panel.transform, "0 м", 92, TextAnchor.MiddleCenter,
                              new Vector2(0.5f, 1f), new Vector2(0f, -110f),
                              new Vector2(700f, 140f));

        // Пивот прямоугольника в его центре, поэтому отступ считаем от края
        // с учётом половины ширины: 60 + 320/2 = 220. Иначе текст уезжает
        // за правый край экрана.
        coinsText = UIText("Coins", panel.transform, "0", 64, TextAnchor.MiddleRight,
                           new Vector2(1f, 1f), new Vector2(-220f, -110f),
                           new Vector2(320f, 100f));
        coinsText.color = CoinGold;

        // Короткая живая подсказка для первых 150 метров. Она стоит ниже
        // счётчика, не закрывает ближайшее препятствие и сама исчезает после
        // первой крыши поезда.
        openingGuideText = UIText("OpeningGuide", panel.transform, "", 32, TextAnchor.MiddleCenter,
                                  new Vector2(0.5f, 1f), new Vector2(0f, -315f),
                                  new Vector2(900f, 64f));
        openingGuideText.color = new Color(0.75f, 0.86f, 1f, 1f);
        AddTextShadow(openingGuideText, new Color(0.05f, 0.02f, 0.15f, 0.95f), new Vector2(2f, -2f));

        // У Директора это не декоративная иконка: число меняется сразу после
        // спасения. Для остальных персонажей компонент скрывает строку.
        shieldText = UIText("Shield", panel.transform, "", 30, TextAnchor.MiddleRight,
                            new Vector2(1f, 1f), new Vector2(-210f, -190f),
                            new Vector2(330f, 56f));
        shieldText.color = new Color(1f, 0.4f, 0.34f, 1f);
        shieldText.gameObject.SetActive(false);

        pauseButton = UIButton("PauseButton", panel.transform, "II", 56, ButtonSecondary,
                               new Vector2(0f, 1f), new Vector2(100f, -110f),
                               new Vector2(130f, 130f));

        // Серия. Стоит под дистанцией по центру: это самое заметное место
        // на экране, а цифра появляется редко и ненадолго, поэтому мешать
        // не будет. Компонент сам подписывается на ScoreManager.
        Text comboText = UIText("Combo", panel.transform, "", 78, TextAnchor.MiddleCenter,
                                new Vector2(0.5f, 1f), new Vector2(0f, -240f),
                                new Vector2(500f, 110f));
        comboText.gameObject.AddComponent<ComboDisplay>();

        // Полоски активных бонусов — столбиком под кнопкой паузы.
        barRoots = new GameObject[4];
        barFills = new RectTransform[4];

        for (int i = 0; i < 4; i++)
        {
            var type = (PowerUpType)i;
            barRoots[i] = UIBar($"Bar_{type}", panel.transform, UpgradeShop.ColorFor(type),
                                UpgradeShop.NameFor(type),
                                new Vector2(0f, 1f),
                                new Vector2(210f, -220f - i * 62f),
                                new Vector2(340f, 46f),
                                out barFills[i]);
            barRoots[i].SetActive(false);
        }

        return panel;
    }

    private static GameObject BuildShopPanel(Transform parent, out Text coinsText,
                                             out Text[] nameTexts, out Text[] effectTexts,
                                             out Button[] buyButtons, out Text[] buyLabels,
                                             out Button closeButton)
    {
        GameObject panel = UIPanel("ShopPanel", parent, PanelDim);

        UIText("Title", panel.transform, "МАГАЗИН", 88, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 130f));

        coinsText = UIText("Coins", panel.transform, "Монет: 0", 54, TextAnchor.MiddleCenter,
                           new Vector2(0.5f, 1f), new Vector2(0f, -370f), new Vector2(900f, 80f));
        coinsText.color = CoinGold;

        nameTexts = new Text[3];
        effectTexts = new Text[3];
        buyButtons = new Button[3];
        buyLabels = new Text[3];

        for (int i = 0; i < 3; i++)
        {
            float rowY = 250f - i * 220f;

            GameObject row = UIObject($"Row_{i}", panel.transform);
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(940f, 190f);
            rowRect.anchoredPosition = new Vector2(0f, rowY);

            var background = row.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.06f);

            nameTexts[i] = UIText("Name", row.transform, "—", 48, TextAnchor.MiddleLeft,
                                  new Vector2(0f, 0.5f), new Vector2(340f, 34f),
                                  new Vector2(620f, 60f));

            effectTexts[i] = UIText("Effect", row.transform, "", 38, TextAnchor.MiddleLeft,
                                    new Vector2(0f, 0.5f), new Vector2(340f, -30f),
                                    new Vector2(620f, 54f));
            effectTexts[i].color = new Color(0.75f, 0.8f, 0.9f);

            buyButtons[i] = UIButton("Buy", row.transform, "0", 46, ButtonMain,
                                     new Vector2(1f, 0.5f), new Vector2(-160f, 0f),
                                     new Vector2(260f, 130f));
            buyLabels[i] = buyButtons[i].GetComponentInChildren<Text>();
        }

        closeButton = UIButton("CloseButton", panel.transform, "НАЗАД", 58, ButtonSecondary,
                               new Vector2(0.5f, 0f), new Vector2(0f, 260f),
                               new Vector2(560f, 150f));

        return panel;
    }

    private static GameObject BuildSettingsPanel(Transform parent,
                                                 out Button music, out Text musicLabel,
                                                 out Button sound, out Text soundLabel,
                                                 out Button vibration, out Text vibrationLabel,
                                                 out Button reset, out Text resetLabel,
                                                 out Button close)
    {
        GameObject panel = UIPanel("SettingsPanel", parent, PanelDim);

        UIText("Title", panel.transform, "НАСТРОЙКИ", 82, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(900f, 130f));

        music = UIButton("MusicButton", panel.transform, "Музыка: вкл", 50, ButtonSecondary,
                         new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(760f, 150f));
        musicLabel = music.GetComponentInChildren<Text>();

        sound = UIButton("SoundButton", panel.transform, "Звуки: вкл", 50, ButtonSecondary,
                         new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(760f, 150f));
        soundLabel = sound.GetComponentInChildren<Text>();

        vibration = UIButton("VibrationButton", panel.transform, "Вибрация: вкл", 50,
                             ButtonSecondary, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                             new Vector2(760f, 150f));
        vibrationLabel = vibration.GetComponentInChildren<Text>();

        reset = UIButton("ResetButton", panel.transform, "СБРОСИТЬ ПРОГРЕСС", 42,
                         new Color(0.60f, 0.22f, 0.22f), new Vector2(0.5f, 0.5f),
                         new Vector2(0f, -260f), new Vector2(760f, 150f));
        resetLabel = reset.GetComponentInChildren<Text>();

        close = UIButton("CloseButton", panel.transform, "НАЗАД", 58, ButtonSecondary,
                         new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(560f, 150f));

        return panel;
    }

    private static GameObject BuildPausePanel(Transform parent, out Button resume,
                                              out Button restart, out Button menu)
    {
        GameObject panel = UIPanel("PausePanel", parent, PanelDim);

        UIText("Title", panel.transform, "ПАУЗА", 96, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(900f, 140f));

        resume = UIButton("ResumeButton", panel.transform, "ПРОДОЛЖИТЬ", 62, ButtonMain,
                          new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(680f, 170f));

        restart = UIButton("RestartButton", panel.transform, "ЗАНОВО", 62, ButtonSecondary,
                           new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(680f, 170f));

        menu = UIButton("MenuButton", panel.transform, "В МЕНЮ", 62, ButtonSecondary,
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(680f, 170f));

        return panel;
    }

    private static GameObject BuildGameOverPanel(Transform parent, out Text title,
                                                 out Text stats, out Button restart,
                                                 out Button menu)
    {
        GameObject panel = UIPanel("GameOverPanel", parent, PanelDim);

        title = UIText("Title", panel.transform, "ВРЕЗАЛСЯ", 92, TextAnchor.MiddleCenter,
                       new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(1000f, 140f));

        stats = UIText("Stats", panel.transform, "0 м\nмонет: 0", 62, TextAnchor.MiddleCenter,
                       new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(900f, 220f));

        restart = UIButton("RestartButton", panel.transform, "ЗАНОВО", 62, ButtonMain,
                           new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(680f, 170f));

        menu = UIButton("MenuButton", panel.transform, "В МЕНЮ", 62, ButtonSecondary,
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(680f, 170f));

        return panel;
    }

    // ------------------------------------------------------- кирпичики UI

    private static Font UIFont =>
        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private static Sprite UIRoundedSprite =>
        AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    private static GameObject UIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go;
    }

    /// <summary>Панель на весь экран с затемняющей подложкой.</summary>
    private static GameObject UIPanel(string name, Transform parent, Color background)
    {
        GameObject panel = UIObject(name, parent);

        var image = panel.AddComponent<Image>();
        image.color = background;
        image.raycastTarget = true;   // ловит тапы, чтобы они не проходили сквозь панель

        return panel;
    }

    /// <summary>
    /// Простой прямоугольник UI с заданной точкой привязки. Нужен для карточек,
    /// рамок и декоративных линий: так лобби остаётся аккуратным без набора
    /// картинок и не зависит от того, импортирован ли сторонний UI-пак.
    /// </summary>
    private static GameObject UIBlock(string name, Transform parent, Vector2 anchor,
                                      Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.AddComponent<Image>();
        image.color = color;
        image.sprite = UIRoundedSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return go;
    }

    /// <summary>Тонкая обводка отделяет карточку от фона на ярком экране.</summary>
    private static void AddOutline(GameObject go, Color color, float distance)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = false;
    }

    /// <summary>Мягкая тень делает заголовок читаемым на ярком фоне трассы.</summary>
    private static void AddTextShadow(Text text, Color color, Vector2 distance)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = false;
    }

    private static Text UIText(string name, Transform parent, string content, int fontSize,
                               TextAnchor anchor, Vector2 pivotAnchor, Vector2 position,
                               Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = pivotAnchor;
        rect.anchorMax = pivotAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = go.AddComponent<Text>();
        text.font = UIFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.color = Color.white;
        text.text = content;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }

    /// <summary>
    /// Полоска таймера бонуса. Заполняющаяся часть растягивается от левого
    /// края через localScale.x — так не нужен ни спрайт, ни Image.Filled.
    /// </summary>
    private static GameObject UIBar(string name, Transform parent, Color color, string label,
                                    Vector2 pivotAnchor, Vector2 position, Vector2 size,
                                    out RectTransform fill)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        var rect = (RectTransform)root.transform;
        rect.anchorMin = pivotAnchor;
        rect.anchorMax = pivotAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var background = root.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.45f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(root.transform, false);

        fill = (RectTransform)fillGo.transform;
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.sizeDelta = new Vector2(size.x, 0f);
        fill.anchoredPosition = Vector2.zero;

        var fillImage = fillGo.AddComponent<Image>();
        fillImage.color = color;
        fillImage.raycastTarget = false;

        UIText("Label", root.transform, label, 32, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 0.5f), Vector2.zero, size);

        return root;
    }

    private static Button UIButton(string name, Transform parent, string label, int fontSize,
                                   Color color, Vector2 pivotAnchor, Vector2 position,
                                   Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = pivotAnchor;
        rect.anchorMax = pivotAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.AddComponent<Image>();
        image.color = color;
        image.sprite = UIRoundedSprite;
        image.type = Image.Type.Sliced;

        // Обводка делает даже простую кнопку читаемой на подвижном фоне.
        AddOutline(go, Color.Lerp(color, Color.black, 0.35f), 2f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.25f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        UIText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 0.5f), Vector2.zero, size);

        return button;
    }

    // ============================================================== материалы

    private struct Materials
    {
        public Material Ground;
        public Material Marker;
        public Material Rail;
        public Material Player;
        public Material Prop;
        public Material Block;
        public Material Jump;
        public Material Slide;
        public Material Coin;
        public Material Train;
        public Material TrainRoof;
        public Material TrainWindow;
        public Material ObstacleBody;
    }

    private static Materials LoadMaterials()
    {
        return new Materials
        {
            Ground = GetOrCreateMaterial("M_Ground", new Color(0.20f, 0.22f, 0.26f)),
            Marker = GetOrCreateMaterial("M_Marker", new Color(0.85f, 0.85f, 0.88f)),
            Rail = GetOrCreateMaterial("M_Rail", new Color(0.32f, 0.36f, 0.42f)),
            Player = GetOrCreateMaterial("M_Player", new Color(0.95f, 0.45f, 0.15f)),
            Prop = GetOrCreateMaterial("M_Prop", new Color(0.45f, 0.50f, 0.58f)),

            // Серые кубы, но подкрашенные: цвет сразу говорит, что делать.
            // Красный — объехать, жёлтый — прыгнуть, синий — подкат.
            Block = GetOrCreateMaterial("M_ObstacleBlock", new Color(0.62f, 0.26f, 0.26f)),
            Jump = GetOrCreateMaterial("M_ObstacleJump", new Color(0.80f, 0.64f, 0.20f)),
            Slide = GetOrCreateMaterial("M_ObstacleSlide", new Color(0.24f, 0.44f, 0.70f)),
            Coin = GetOrCreateMaterial("M_Coin", new Color(0.95f, 0.78f, 0.20f)),

            // Тёмная основа и цветная подсветка делают препятствия похожими
            // на реальные объекты, а не на три ярких куба из прототипа.
            ObstacleBody = GetOrCreateMaterial("M_ObstacleBody", new Color(0.09f, 0.07f, 0.18f)),

            // Бирюзовый: единственный цвет, который не занят ни одним
            // «нельзя» — красным, жёлтым и синим. Поезд не запрещает,
            // он предлагает, и цвет должен это говорить.
            Train = GetOrCreateMaterial("M_Train", new Color(0.16f, 0.62f, 0.58f)),
            TrainRoof = GetOrCreateMaterial("M_TrainRoof", new Color(0.08f, 0.10f, 0.22f)),
            TrainWindow = GetOrCreateMaterial("M_TrainWindow", new Color(0.08f, 0.72f, 0.95f))
        };
    }

    // ============================================================ препятствия

    private static Obstacle CreateObstaclePrefab(string prefabName, Obstacle.Kind kind, Materials mats)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ObstaclesFolder);

        var root = new GameObject(prefabName);
        Obstacle obstacle = root.AddComponent<Obstacle>();

        var so = new SerializedObject(obstacle);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.ApplyModifiedPropertiesWithoutUndo();

        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;

        switch (kind)
        {
            // Высокая тумба: 2.8 юнита — прыжок 2.2 её не берёт, только объезд.
            case Obstacle.Kind.Block:
                trigger.center = new Vector3(0f, 1.4f, 0f);
                trigger.size = new Vector3(1.7f, 2.8f, 0.7f);
                Box("Visual", root.transform, new Vector3(1.7f, 2.8f, 0.7f),
                    new Vector3(0f, 1.4f, 0f), mats.ObstacleBody);
                AddObstacleWarningFace(root.transform, "Block", 1.42f, 1.52f, mats.Block);
                break;

            // Низкий барьер по колено: перепрыгнуть.
            case Obstacle.Kind.JumpOver:
                trigger.center = new Vector3(0f, 0.45f, 0f);
                trigger.size = new Vector3(1.7f, 0.9f, 0.7f);
                Box("Visual", root.transform, new Vector3(1.7f, 0.9f, 0.7f),
                    new Vector3(0f, 0.45f, 0f), mats.ObstacleBody);
                AddObstacleWarningFace(root.transform, "Jump", 0.52f, 0.25f, mats.Jump);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("JumpLeg_" + side, root.transform, new Vector3(0.15f, 0.85f, 0.15f),
                        new Vector3(side * 0.67f, 0.42f, 0f), mats.Jump);
                }
                break;

            // Балка сверху: низ на 1.1, стоя игрок (2.0) задевает, в подкате (0.9) проезжает.
            case Obstacle.Kind.SlideUnder:
                trigger.center = new Vector3(0f, 1.45f, 0f);
                trigger.size = new Vector3(1.7f, 0.7f, 0.7f);
                Box("Visual", root.transform, new Vector3(1.7f, 0.7f, 0.7f),
                    new Vector3(0f, 1.45f, 0f), mats.ObstacleBody);
                AddObstacleWarningFace(root.transform, "Slide", 1.45f, 0.22f, mats.Slide);

                // Стойки — только для читаемости, коллайдеров у них нет.
                for (int side = -1; side <= 1; side += 2)
                {
                    Box($"Post_{side}", root.transform, new Vector3(0.16f, 1.8f, 0.16f),
                        new Vector3(side * 0.93f, 0.9f, 0f), mats.Slide);
                }
                break;
        }

        string path = $"{ObstaclesFolder}/{prefabName}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<Obstacle>();
    }

    /// <summary>
    /// Светящаяся лицевая панель оставляет цветовой язык препятствия, но
    /// даёт ему корпус, кромку и глубину вместо сплошной неоновой коробки.
    /// </summary>
    private static void AddObstacleWarningFace(Transform parent, string name, float y,
                                               float height, Material warningMaterial)
    {
        Box(name + "_Screen", parent, new Vector3(1.34f, height, 0.045f),
            new Vector3(0f, y, -0.375f), warningMaterial);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(name + "_Edge_" + side, parent, new Vector3(0.10f, height + 0.14f, 0.06f),
                new Vector3(side * 0.73f, y, -0.39f), warningMaterial);
        }
    }

    /// <summary>
    /// Поезд. Устроен принципиально не так, как остальные препятствия:
    /// у него ДВА коллайдера с разными задачами.
    ///
    ///   1. Триггер бортов, высотой до KillHeight (1.7) — убивает того,
    ///      кто въехал сбоку.
    ///   2. Обычный коллайдер крыши с меткой GroundSurface на высоте
    ///      RoofHeight (1.8) — по нему игрок бежит.
    ///
    /// Между ними 0.1 юнита зазора. Он и делает всю механику возможной:
    /// вставший на крышу игрок начинается на 1.8 и до триггера не достаёт,
    /// а бегущий по земле занимает 0..2.0 и в триггер попадает.
    /// Если зазор убрать, приземление на крышу станет смертью.
    /// </summary>
    private static Obstacle CreateTrainPrefab(Materials mats)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ObstaclesFolder);

        const float length = Obstacle.TrainMetrics.Length;
        const float width = Obstacle.TrainMetrics.Width;
        const float roof = Obstacle.TrainMetrics.RoofHeight;
        const float kill = Obstacle.TrainMetrics.KillHeight;

        float mid = length * 0.5f;

        var root = new GameObject("Obstacle_Train");
        Obstacle obstacle = root.AddComponent<Obstacle>();

        var so = new SerializedObject(obstacle);
        so.FindProperty("kind").enumValueIndex = (int)Obstacle.Kind.Train;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Убивающий борт.
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, kill * 0.5f, mid);
        trigger.size = new Vector3(width, kill, length);

        // Корпус: если CC0-модель Kenney уже импортирована, используем её
        // вместо старого прямоугольника. Коллайдеры и точная высота крыши
        // остаются нашими, поэтому красивая модель не меняет ни одной
        // проверенной механики поезда.
        if (!AddKenneyCityTrainVisual(root.transform, width, roof, length, mats.Train))
        {
            Box("Body", root.transform, new Vector3(width, roof, length),
                new Vector3(0f, roof * 0.5f, mid), mats.Train);
        }

        // Крыша: тонкая пластина, верх ровно на RoofHeight.
        // Коллайдер НЕ триггер — иначе луч поиска пола её не увидит.
        var roofGo = Box("Roof", root.transform, new Vector3(width, 0.12f, length),
                         new Vector3(0f, roof - 0.06f, mid), mats.TrainRoof,
                         keepCollider: true);
        roofGo.AddComponent<GroundSurface>();

        AddTrainWindows(root.transform, width, roof, length, mats);

        // Небольшие сегменты на крыше добавляют масштаб и скорость, сохраняя
        // тёмную безопасную поверхность вместо огромной белой плоскости.
        for (int panel = 1; panel < 6; panel++)
        {
            Box("RoofPanel_" + panel, root.transform, new Vector3(width * 0.72f, 0.025f, 0.09f),
                new Vector3(0f, roof + 0.014f, panel * (length / 6f)), mats.Marker);
        }

        // Светящаяся полоса по краю крыши. Это не украшение: игрок должен
        // на скорости отличать «сюда можно запрыгнуть» от «сюда нельзя»,
        // и цвет корпуса на таком расстоянии читается плохо.
        for (int side = -1; side <= 1; side += 2)
        {
            Box($"Edge_{side}", root.transform,
                new Vector3(0.12f, 0.16f, length),
                new Vector3(side * (width * 0.5f - 0.06f), roof + 0.04f, mid),
                mats.Marker);
        }

        string path = $"{ObstaclesFolder}/Obstacle_Train.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<Obstacle>();
    }

    private static void AddTrainWindows(Transform parent, float width, float roof,
                                        float length, Materials mats)
    {
        // Окна накладываются поверх CC0-вагона. Благодаря этому модель
        // остаётся узнаваемым поездом даже когда вся сцена перекрашена в
        // единый стиль, а поезд хорошо читается на скорости.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int window = 0; window < 5; window++)
            {
                float z = 1.10f + window * 1.88f;
                Box("Window_" + side + "_" + window, parent,
                    new Vector3(0.035f, 0.62f, 1.14f),
                    new Vector3(side * (width * 0.5f + 0.018f), roof * 0.54f, z), mats.TrainWindow);
            }

            // Кромка внизу визуально отделяет корпус от путей и даёт
            // вагону более тяжёлый, качественный силуэт.
            Box("UnderGlow_" + side, parent, new Vector3(0.055f, 0.08f, length * 0.82f),
                new Vector3(side * (width * 0.5f + 0.025f), 0.28f, length * 0.5f), mats.TrainWindow);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Box("RearLight_" + side, parent, new Vector3(0.30f, 0.18f, 0.045f),
                new Vector3(side * 0.42f, roof * 0.58f, -0.02f), mats.TrainWindow);
        }
    }

    /// <summary>
    /// Подгоняет импортированный вагон точно в игровые габариты 1.7 × 2.6 × 10.
    /// Так можно заменить только картинку поезда, не трогая крышу, пандус и
    /// триггер столкновения, которые уже много раз проверялись в забеге.
    /// </summary>
    private static bool AddKenneyCityTrainVisual(Transform parent, float width, float height,
                                                 float length, Material material)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyCityTrainPath);
        if (source == null) return false;

        GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (visual == null) return false;

        visual.name = "KenneyCityTrain";
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        Bounds sourceBounds = LocalRendererBounds(visual.transform);
        if (sourceBounds.size.x <= 0.001f || sourceBounds.size.y <= 0.001f || sourceBounds.size.z <= 0.001f)
        {
            Object.DestroyImmediate(visual);
            return false;
        }

        Vector3 scale = new Vector3(width / sourceBounds.size.x,
                                    height / sourceBounds.size.y,
                                    length / sourceBounds.size.z);
        visual.transform.localScale = scale;
        visual.transform.localPosition = new Vector3(-sourceBounds.center.x * scale.x,
                                                      -sourceBounds.min.y * scale.y,
                                                      length * 0.5f - sourceBounds.center.z * scale.z);

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        return true;
    }

    /// <summary>Общие границы всех мешей в локальных координатах корня модели.</summary>
    private static Bounds LocalRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            Bounds world = renderer.bounds;
            Vector3 extents = world.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = world.center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 local = root.InverseTransformPoint(corner);

                        if (!hasBounds)
                        {
                            result = new Bounds(local, Vector3.zero);
                            hasBounds = true;
                        }
                        else result.Encapsulate(local);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Пандус: по нему вбегаешь на крышу состава, не прыгая.
    ///
    /// Никакого специального кода для наклона не потребовалось. Игрок ищет
    /// пол лучом вниз, а луч одинаково хорошо попадает и в горизонтальную
    /// плиту, и в наклонную. Подъём просто оказывается чуть выше ног
    /// на каждом кадре, и игрок к нему прижимается — это тот же механизм,
    /// что отрабатывает ступеньку.
    ///
    /// Уклон 1.8 на 6.5 — около 15 градусов. Специально пологий: на скорости
    /// 24 ю/с при 60 кадрах поверхность поднимается на 0.11 за кадр,
    /// а порог шага 0.35. Запас втрое даже при просадке до 20 кадров.
    /// </summary>
    private static GameObject CreateRampPrefab(Materials mats)
    {
        EnsureFolder(ProjectRoot + "/Prefabs");
        EnsureFolder(ObstaclesFolder);

        const float width = Obstacle.TrainMetrics.Width;
        const float top = Obstacle.TrainMetrics.RoofHeight;
        const float run = Obstacle.TrainMetrics.RampRun;
        const float total = Obstacle.TrainMetrics.RampLength;
        const float thickness = 0.3f;

        var root = new GameObject("Train_Ramp");
        root.AddComponent<CampusRampVisual>();

        // --- наклонная часть ---
        float slopeLength = Mathf.Sqrt(run * run + top * top);
        float angle = Mathf.Atan2(top, run) * Mathf.Rad2Deg;

        GameObject slope = Box("Slope", root.transform,
                               new Vector3(width, thickness, slopeLength),
                               Vector3.zero, mats.Train, keepCollider: true);

        // Поворот вокруг X на отрицательный угол поднимает дальний конец:
        // при положительном Unity наклоняет +Z вниз.
        slope.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);

        // Ставим так, чтобы ВЕРХНЯЯ грань шла из (z=0, y=0) в (z=run, y=top).
        // Центр коробки — это середина верхней грани, сдвинутая внутрь
        // на половину толщины по нормали склона.
        float rad = angle * Mathf.Deg2Rad;
        slope.transform.localPosition = new Vector3(
            0f,
            top * 0.5f - thickness * 0.5f * Mathf.Cos(rad),
            run * 0.5f + thickness * 0.5f * Mathf.Sin(rad));

        slope.AddComponent<GroundSurface>();

        // --- ровная площадка до самого вагона ---
        float flat = total - run;
        GameObject cap = Box("Cap", root.transform,
                             new Vector3(width, thickness, flat),
                             new Vector3(0f, top - thickness * 0.5f, run + flat * 0.5f),
                             mats.Train, keepCollider: true);
        cap.AddComponent<GroundSurface>();

        // Светящаяся окантовка тем же материалом, что разметка: игрок уже
        // читает этим цветом «сюда можно встать».
        for (int side = -1; side <= 1; side += 2)
        {
            Box($"Edge_{side}", root.transform,
                new Vector3(0.12f, 0.14f, flat),
                new Vector3(side * (width * 0.5f - 0.06f), top + 0.05f, run + flat * 0.5f),
                mats.Marker);
        }

        string path = $"{ObstaclesFolder}/Train_Ramp.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved;
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

        GameObject ground = Box("Ground", root.transform,
            new Vector3(TrackWidth, 1f, ChunkLength),
            new Vector3(0f, -0.5f, mid),
            mats.Ground, keepCollider: true);

        // Без этой метки луч из-под ног игрока не найдёт вообще ничего,
        // и он побежит по запасной нулевой высоте. Работать будет, но пол
        // перестанет быть настоящим — и первая же эстакада это вскроет.
        ground.AddComponent<GroundSurface>();

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
        player.AddComponent<PlayerCollision>();
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

    private static CameraFollow SetUpCamera(Transform target)
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

        return follow;
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

    /// <summary>
    /// Сборщики работают с одной конкретной сценой игры. Если открыта другая
    /// (или безымянная Untitled — так бывает сразу после того, как Unity
    /// пересобрала Library и забыла, какая сцена была последней), то
    /// SaveOpenScenes в конце показывал диалог «Save Scene As» и предлагал
    /// сохранить мусор куда попало. Теперь сцена открывается явно.
    /// </summary>
    private static bool EnsureGameSceneOpen()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.path == GameScenePath) return true;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
        {
            EditorUtility.DisplayDialog(
                "Runner",
                $"Не найдена сцена {GameScenePath}.\n\n" +
                "Сборщик работает только с ней. Проверь, что проект открыт целиком.",
                "Понятно");
            return false;
        }

        // Безымянную сцену сохранять некуда и незачем: сборщик всё равно
        // соберёт содержимое заново. Именованную — спрашиваем как обычно.
        if (!string.IsNullOrEmpty(active.path) && active.isDirty &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        if (string.IsNullOrEmpty(active.path) && active.isDirty &&
            !EditorUtility.DisplayDialog(
                "Runner",
                "Сейчас открыта несохранённая сцена. Сборщик работает с " +
                GameScenePath + ".\n\nОткрыть её? Несохранённая сцена будет потеряна.",
                "Открыть Game.unity", "Отмена")) return false;

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        return true;
    }

    private static void Finish(GameObject select, string message)
    {
        Selection.activeGameObject = select;

        Scene scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);

        // Явный путь вместо SaveOpenScenes: без него безымянная сцена
        // открывала диалог «куда сохранить».
        if (string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, GameScenePath);
        else EditorSceneManager.SaveScene(scene);

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
