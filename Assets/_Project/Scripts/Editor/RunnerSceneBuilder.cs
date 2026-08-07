#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    private const string MaterialsFolder = ProjectRoot + "/Materials";
    private const string ChunksFolder = ProjectRoot + "/Prefabs/Chunks";
    private const string ObstaclesFolder = ProjectRoot + "/Prefabs/Obstacles";

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

    // ===================================================================== M3

    [MenuItem("Tools/Runner/M3 — препятствия и Game Over")]
    public static void BuildM3Scene()
    {
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

        var so = new SerializedObject(coin);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{ProjectRoot}/Prefabs/Pickups/Coin.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return saved.GetComponent<Coin>();
    }

    // ================================================================== M6+M7

    [MenuItem("Tools/Runner/M6+M7 — полная игра: интерфейс, бонусы, магазин")]
    public static void BuildM6Scene()
    {
        DeleteIfExists("Track");
        DeleteIfExists("ChunkSpawner");
        DeleteIfExists("Player");
        DeleteIfExists("GameManager");
        DeleteIfExists("UI");
        DeleteIfExists("EventSystem");

        Materials mats = LoadMaterials();

        List<Chunk> chunkPrefabs = CreateChunkPrefabs(mats);
        Obstacle block = CreateObstaclePrefab("Obstacle_Block", Obstacle.Kind.Block, mats);
        Obstacle jump = CreateObstaclePrefab("Obstacle_Jump", Obstacle.Kind.JumpOver, mats);
        Obstacle slide = CreateObstaclePrefab("Obstacle_Slide", Obstacle.Kind.SlideUnder, mats);
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
        gameSo.FindProperty("cameraFollow").objectReferenceValue = follow;
        gameSo.ApplyModifiedPropertiesWithoutUndo();

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

        BuildUserInterface(player.GetComponent<PlayerController>(), scoreManager);

        Finish(player, "Полная сцена собрана: меню, HUD, пауза, бонусы, магазин, настройки. Жми Play.");
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

    // ================================================================== UI

    private const float UIReferenceWidth = 1080f;
    private const float UIReferenceHeight = 1920f;

    private static readonly Color PanelDim = new Color(0.05f, 0.06f, 0.09f, 0.82f);
    private static readonly Color ButtonMain = new Color(0.95f, 0.45f, 0.15f, 1f);
    private static readonly Color ButtonSecondary = new Color(0.24f, 0.28f, 0.36f, 1f);
    private static readonly Color CoinGold = new Color(0.98f, 0.82f, 0.28f, 1f);

    private static void BuildUserInterface(PlayerController player, ScoreManager score)
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
                                         out Text menuCoins, out Button playButton,
                                         out Button shopButton, out Button settingsButton);

        GameObject hudPanel = BuildHudPanel(safeArea.transform, out Text distanceText,
                                            out Text coinsText, out Button pauseButton,
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

        var so = new SerializedObject(ui);
        so.FindProperty("menuPanel").objectReferenceValue = menu;
        so.FindProperty("hudPanel").objectReferenceValue = hudPanel;
        so.FindProperty("pausePanel").objectReferenceValue = pause;
        so.FindProperty("gameOverPanel").objectReferenceValue = over;
        so.FindProperty("shopPanel").objectReferenceValue = shop;
        so.FindProperty("settingsPanel").objectReferenceValue = settings;

        so.FindProperty("menuBestText").objectReferenceValue = menuBest;
        so.FindProperty("menuCoinsText").objectReferenceValue = menuCoins;
        so.FindProperty("playButton").objectReferenceValue = playButton;
        so.FindProperty("shopButton").objectReferenceValue = shopButton;
        so.FindProperty("settingsButton").objectReferenceValue = settingsButton;

        so.FindProperty("hudDistanceText").objectReferenceValue = distanceText;
        so.FindProperty("hudCoinsText").objectReferenceValue = coinsText;
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

        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("score").objectReferenceValue = score;
        so.ApplyModifiedPropertiesWithoutUndo();
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
                                             out Text coinsText, out Button playButton,
                                             out Button shopButton, out Button settingsButton)
    {
        GameObject panel = UIPanel("MenuPanel", parent, PanelDim);

        UIText("Title", panel.transform, "RUNNER", 120, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(900f, 180f));

        UIText("Subtitle", panel.transform, "школьный забег", 48, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(900f, 80f));

        bestText = UIText("Best", panel.transform, "Рекорд: 0 м", 52, TextAnchor.MiddleCenter,
                          new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(900f, 80f));

        coinsText = UIText("Coins", panel.transform, "Монет: 0", 52, TextAnchor.MiddleCenter,
                           new Vector2(0.5f, 0.5f), new Vector2(0f, 215f), new Vector2(900f, 80f));
        coinsText.color = CoinGold;

        playButton = UIButton("PlayButton", panel.transform, "ИГРАТЬ", 76, ButtonMain,
                              new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                              new Vector2(700f, 190f));

        shopButton = UIButton("ShopButton", panel.transform, "МАГАЗИН", 54, ButtonSecondary,
                              new Vector2(0.5f, 0.5f), new Vector2(0f, -170f),
                              new Vector2(560f, 140f));

        settingsButton = UIButton("SettingsButton", panel.transform, "НАСТРОЙКИ", 54,
                                  ButtonSecondary, new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, -330f), new Vector2(560f, 140f));

        UIText("Hint", panel.transform,
               "свайпы: влево / вправо / вверх / вниз",
               38, TextAnchor.MiddleCenter,
               new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1000f, 70f));

        return panel;
    }

    private static GameObject BuildHudPanel(Transform parent, out Text distanceText,
                                            out Text coinsText, out Button pauseButton,
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

        pauseButton = UIButton("PauseButton", panel.transform, "II", 56, ButtonSecondary,
                               new Vector2(0f, 1f), new Vector2(100f, -110f),
                               new Vector2(130f, 130f));

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
            Coin = GetOrCreateMaterial("M_Coin", new Color(0.95f, 0.78f, 0.20f))
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
                    new Vector3(0f, 1.4f, 0f), mats.Block);
                break;

            // Низкий барьер по колено: перепрыгнуть.
            case Obstacle.Kind.JumpOver:
                trigger.center = new Vector3(0f, 0.45f, 0f);
                trigger.size = new Vector3(1.7f, 0.9f, 0.7f);
                Box("Visual", root.transform, new Vector3(1.7f, 0.9f, 0.7f),
                    new Vector3(0f, 0.45f, 0f), mats.Jump);
                break;

            // Балка сверху: низ на 1.1, стоя игрок (2.0) задевает, в подкате (0.9) проезжает.
            case Obstacle.Kind.SlideUnder:
                trigger.center = new Vector3(0f, 1.45f, 0f);
                trigger.size = new Vector3(1.7f, 0.7f, 0.7f);
                Box("Visual", root.transform, new Vector3(1.7f, 0.7f, 0.7f),
                    new Vector3(0f, 1.45f, 0f), mats.Slide);

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
