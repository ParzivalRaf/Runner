using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Наполняет чанк содержимым: препятствия и дорожки монет. Следит, чтобы
/// трасса всегда оставалась проходимой — не только внутри одного ряда,
/// но и между рядами и между соседними чанками.
///
/// Три правила, которые здесь выполняются:
///   1. В каждом ряду есть хотя бы одна проходимая полоса (это гарантирует
///      сама таблица ObstaclePatterns).
///   2. У соседних рядов есть общая проходимая полоса — можно проехать
///      вообще не перестраиваясь.
///   3. Ряды, требующие прыжка или подката, стоят не ближе minActionSpacing
///      друг к другу: на максимальной скорости 24 ю/с прыжок длится 0.75 с,
///      то есть 18 юнитов, и два прыжка подряд физически не успеть.
///
/// Куда вешать: на тот же объект, что и ChunkSpawner.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Префабы препятствий")]
    [SerializeField] private Obstacle blockPrefab;
    [SerializeField] private Obstacle jumpPrefab;
    [SerializeField] private Obstacle slidePrefab;

    [Header("Поезда")]
    [SerializeField] private Obstacle trainPrefab;

    [Tooltip("Пандус, по которому вбегаешь на крышу. Без Obstacle: " +
             "он не убивает, это просто наклонный пол.")]
    [SerializeField] private GameObject rampPrefab;

    [Tooltip("Вероятность, что в полосе начнётся состав.")]
    [Range(0f, 1f)]
    [SerializeField] private float trainChance = 0.5f;

    [Tooltip("Вероятность, что состав начнётся с пандуса, а не с глухого " +
             "борта. Высокая намеренно: крыша задумана как маршрут, " +
             "а не как трюк на реакцию.")]
    [Range(0f, 1f)]
    [SerializeField] private float rampChance = 0.7f;

    [Tooltip("Сколько чанков подряд может тянуться один состав. " +
             "Отсюда берутся длинные пробежки по крышам.")]
    [SerializeField] private int trainRunMinChunks = 1;
    [SerializeField] private int trainRunMaxChunks = 3;

    [Tooltip("Сколько чанков полоса обязана пустовать после конца состава, " +
             "прежде чем в ней разрешат начать следующий.\n\n" +
             "Ноль здесь ломал картинку: состав кончался, и в том же месте " +
             "встык начинался новый — со своим пандусом. Внешне это один " +
             "длинный поезд, у которого посреди крыши провал и въезд " +
             "снизу. Игрок бежал по крыше и падал в дыру без причины.\n\n" +
             "Один чанк это 30 юнитов — примерно секунда на полной скорости. " +
             "Хватает, чтобы два состава читались как два разных поезда " +
             "и чтобы было куда спрыгнуть.")]
    [SerializeField] private int trainGapChunks = 1;

    [Tooltip("С какой ступени сложности начинают появляться поезда. " +
             "На нулевой не надо: игрок ещё не понял базовые правила.")]
    [SerializeField] private int trainMinTier = 1;

    [Tooltip("Вероятность дорожки монет на крыше поезда.")]
    [Range(0f, 1f)]
    [SerializeField] private float roofCoinChance = 0.85f;

    [Header("Монеты")]
    [SerializeField] private Coin coinPrefab;

    [Tooltip("Сколько монет в дорожке внутри одного чанка.")]
    [SerializeField] private int coinsPerChunk = 10;

    [Tooltip("Высота монеты над полом.")]
    [SerializeField] private float coinHeight = 1f;

    [Tooltip("Вероятность, что в чанке вообще появится дорожка монет.")]
    [Range(0f, 1f)]
    [SerializeField] private float coinChance = 0.75f;

    [Header("Бонусы")]
    [SerializeField] private PowerUp[] powerUpPrefabs;

    [Tooltip("Вероятность, что в чанке появится бонус.")]
    [Range(0f, 1f)]
    [SerializeField] private float powerUpChance = 0.16f;

    [Tooltip("Высота бонуса над полом.")]
    [SerializeField] private float powerUpHeight = 1.2f;

    [Header("Настройки")]
    [Tooltip("Первые столько метров забега — без препятствий, чтобы игрок успел взяться за телефон.")]
    // 20, а не 45: на стартовой скорости 14 это полторы секунды.
    // Сорок пять метров пустой дороги — пять секунд, за которые игрок
    // успевает решить, что игра медленная.
    [SerializeField] private float startSafeDistance = 20f;

    [Header("Поставленное начало забега")]
    [Tooltip("Первый забег не отдаём случайности: за первые 150 метров игрок обязательно собирает монеты, объезжает блок, прыгает, берёт бонус и вбегает на крышу поезда. После этого включается обычный бесконечный генератор.")]
    [SerializeField] private bool useAuthoredOpening = true;

    [Tooltip("До какой координаты Z работает поставленное начало. Должно заканчиваться на границе чанка.")]
    [SerializeField] private float authoredOpeningEndZ = 180f;

    [Tooltip("Минимальное расстояние между рядами, требующими прыжка/подката.")]
    [SerializeField] private float minActionSpacing = 22f;

    [Tooltip("Сколько объектов каждого типа создать заранее.")]
    [SerializeField] private int prewarmPerPrefab = 8;

    [Tooltip("Доля балок, которые можно пройти только подкатом. Остальные " +
             "можно пройти и прыжком, и подкатом — игрок сам выбирает.")]
    [Range(0f, 1f)]
    [SerializeField] private float slideOnlyChance = 0.5f;

    // Одновременно на трассе находятся несколько чанков. Эти минимумы
    // покрывают худшие штатные раскладки, чтобы во время забега пул не
    // создавал новые объекты и не давал микрофриз на телефоне.
    private const int MinRegularObstaclePool = 20;
    private const int MinTrainPool = 24;
    // Одновременно живут до шести чанков. В худшем случае у каждого есть
    // дорожка на земле и дорожка на крыше: 6 × 10 × 2 = 120 монет.
    // 128 оставляет небольшой запас и не заставляет пул расти в забеге.
    private const int MinCoinPool = 128;
    private const int MinPowerUpPool = 6;
    // Две полосы из трёх. Это правило проверено симуляцией на 180 000 рядов:
    // непроходимых ситуаций ноль. Ограничение в одну полосу вдвое сокращало
    // присутствие поездов, а вертикальный слой — главное приобретение сессии 3.
    private const int MaxActiveTrainLanes = 2;

    [Tooltip("Печатать в консоль каждую раскладку — полезно при отладке генератора.")]
    [SerializeField] private bool logPatterns = false;

    /// <summary>Объект из пула вместе с пулом, в который его нужно вернуть.</summary>
    private struct PooledItem
    {
        public GameObject Instance;
        public ObjectPool Pool;
    }

    private readonly Dictionary<Obstacle, ObjectPool> _obstaclePools =
        new Dictionary<Obstacle, ObjectPool>();

    private readonly Dictionary<Chunk, List<PooledItem>> _spawned =
        new Dictionary<Chunk, List<PooledItem>>();

    private readonly Dictionary<PowerUp, ObjectPool> _powerUpPools =
        new Dictionary<PowerUp, ObjectPool>();

    private ObjectPool _coinPool;
    private Transform _poolRoot;

    private ObjectPool _rampPool;

    private float _lastActionZ = -9999f;
    private bool[] _lastPassableLanes = { true, true, true };

    // Сколько ЧАНКОВ ещё тянется состав в каждой полосе. Именно это поле
    // делает пробежку по крышам длинной: состав не заканчивается на границе
    // чанка, а продолжается в следующем, вагон встык к вагону.
    private readonly int[] _trainChunksLeft = new int[3];

    // Сколько ближайших РЯДОВ полоса обязана остаться пустой.
    // Ставится там, где состав кончился: игрок спрыгивает с высоты 1.8,
    // это около 7.2 юнита на максимальной скорости, а ряды стоят через 7.5.
    // Без запаса он приземлялся бы прямо в препятствие.
    private readonly int[] _laneClearRows = new int[3];

    // Сколько ближайших ЧАНКОВ полоса обязана остаться без составов.
    // Ставится в момент, когда состав кончился. Это не то же самое, что
    // _laneClearRows: тот запрет про обычные препятствия и живёт один ряд,
    // а этот — только про поезда и живёт целыми чанками.
    private readonly int[] _trainGapLeft = new int[3];

    private bool _initialized;

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        bool regularPoolsReady = (blockPrefab == null || _obstaclePools.ContainsKey(blockPrefab))
                              && (jumpPrefab == null || _obstaclePools.ContainsKey(jumpPrefab))
                              && (slidePrefab == null || _obstaclePools.ContainsKey(slidePrefab))
                              && (trainPrefab == null || _obstaclePools.ContainsKey(trainPrefab));
        if (_initialized && _poolRoot != null && regularPoolsReady) return;

        // Unity can reload scripts while Play Mode stays alive. Non-serialized
        // dictionaries are then empty, while _initialized may retain its old
        // value. Rebuild the complete pool set instead of later indexing a
        // missing prefab and stopping the endless run.
        _obstaclePools.Clear();
        _powerUpPools.Clear();
        _coinPool = null;
        _rampPool = null;

        Transform stalePool = transform.Find("ObstaclePool");
        if (stalePool != null) Destroy(stalePool.gameObject);

        _initialized = true;

        _poolRoot = new GameObject("ObstaclePool").transform;
        _poolRoot.SetParent(transform, false);

        CreateObstaclePool(blockPrefab, MinRegularObstaclePool);
        CreateObstaclePool(jumpPrefab, MinRegularObstaclePool);
        CreateObstaclePool(slidePrefab, MinRegularObstaclePool);
        CreateObstaclePool(trainPrefab, MinTrainPool);

        if (coinPrefab != null)
            _coinPool = new ObjectPool(coinPrefab.gameObject, _poolRoot,
                                       Mathf.Max(MinCoinPool, coinsPerChunk * 10));

        if (rampPrefab != null)
            _rampPool = new ObjectPool(rampPrefab, _poolRoot, 4);

        if (powerUpPrefabs != null)
        {
            foreach (PowerUp prefab in powerUpPrefabs)
            {
                if (prefab == null || _powerUpPools.ContainsKey(prefab)) continue;
                _powerUpPools[prefab] = new ObjectPool(prefab.gameObject, _poolRoot,
                                                        MinPowerUpPool);
            }
        }
    }

    /// <summary>Забыть историю раскладок перед новым забегом.</summary>
    public void ResetRun()
    {
        EnsureInitialized();

        _lastActionZ = -9999f;
        _lastPassableLanes = new[] { true, true, true };

        for (int lane = 0; lane < 3; lane++)
        {
            _trainChunksLeft[lane] = 0;
            _laneClearRows[lane] = 0;
            _trainGapLeft[lane] = 0;
        }
    }

    private void CreateObstaclePool(Obstacle prefab, int minimumCapacity)
    {
        if (prefab == null || _obstaclePools.ContainsKey(prefab)) return;
        _obstaclePools[prefab] = new ObjectPool(prefab.gameObject, _poolRoot,
                                                 Mathf.Max(prewarmPerPrefab, minimumCapacity));
    }

    // ---------------------------------------------------------------------

    /// <summary>Наполнить чанк. Вызывать после того, как чанк уже поставлен на место.</summary>
    public void Populate(Chunk chunk, float distance)
    {
        EnsureInitialized();

        Transform[] points = chunk.SpawnPoints;
        if (points == null || points.Length < 9) return;

        int tier = ObstaclePatterns.TierForDistance(distance);
        int[] rows = ObstaclePatterns.RowsForTier(tier) == 1 ? new[] { 1 } : new[] { 0, 2 };

        List<PooledItem> list = GetList(chunk);

        // Первые секунды задают отношение к игре. Раньше генератор честно
        // оставлял старт безопасным, но из-за случайности мог показать монеты,
        // прыжок и крышу поезда только через несколько забегов. Здесь это
        // превращено в короткую сцену, которая обучает действиями, а не окном
        // с текстом. Важно: она заканчивается ДО включения рандома, поэтому
        // не вмешивается в его гарантии проходимости.
        if (useAuthoredOpening && chunk.transform.position.z >= 0f
                              && chunk.transform.position.z < authoredOpeningEndZ)
        {
            PopulateAuthoredOpening(chunk, list);
            return;
        }

        // Составы решаются ОДИН раз на весь чанк и занимают полосу целиком.
        // Иначе игрок, бегущий по крыше, вылетал бы лбом в препятствие,
        // поставленное в той же полосе следующим рядом: спрыгнуть с высоты
        // 1.8 занимает около 0.3 секунды, а ряды стоят в 7.5 юнитах —
        // на максимальной скорости это те же 0.3 секунды. Не успеть.
        bool[] trainLanes = UpdateTrainRuns(tier, chunk, list);

        // Полосы, свободные во всех рядах этого чанка — по ним пустим монеты.
        bool[] freeForCoins = { true, true, true };
        for (int lane = 0; lane < 3; lane++)
            if (trainLanes[lane]) freeForCoins[lane] = false;

        foreach (int row in rows)
        {
            Transform anchor = points[row * 3 + 1];
            if (anchor == null) continue;

            // Стартовая зона: даём разбежаться, прежде чем швырять препятствия.
            if (anchor.position.z < startSafeDistance) continue;

            string pattern = PickPattern(tier, anchor.position.z, trainLanes);
            if (logPatterns) Debug.Log($"[Obstacles] z={anchor.position.z:F0} tier={tier} → {pattern}");

            // Один ряд читается как одна команда: все его балки либо низкие
            // (прыжок ИЛИ подкат), либо высокие (только подкат). Если выбрать
            // вариант отдельно для каждой полосы, игрок увидит одинаковые
            // символы, но получил бы разные правила — это нечестно.
            Obstacle.SlideVariant slideVariant = Random.value < slideOnlyChance
                ? Obstacle.SlideVariant.SlideOnly
                : Obstacle.SlideVariant.JumpOrSlide;

            if (ObstaclePatterns.RequiresAction(pattern)) _lastActionZ = anchor.position.z;
            _lastPassableLanes = ObstaclePatterns.PassableLanes(pattern);

            // Обещание «полоса останется пустой» тратится ровно на один ряд.
            for (int lane = 0; lane < 3; lane++)
                if (_laneClearRows[lane] > 0) _laneClearRows[lane]--;

            for (int lane = 0; lane < 3; lane++)
            {
                if (pattern[lane] != '.') freeForCoins[lane] = false;

                Obstacle prefab = PrefabFor(pattern[lane]);
                if (prefab == null) continue;

                Transform point = points[row * 3 + lane];
                if (point == null) continue;

                ObjectPool pool = _obstaclePools[prefab];
                GameObject instance = pool.Get();
                instance.transform.SetParent(chunk.transform, false);
                instance.transform.SetPositionAndRotation(point.position, Quaternion.identity);

                if (prefab == slidePrefab)
                {
                    Obstacle slide = instance.GetComponent<Obstacle>();
                    if (slide != null) slide.ConfigureSlideVariant(slideVariant);
                }

                list.Add(new PooledItem { Instance = instance, Pool = pool });
            }
        }

        PlaceCoins(chunk, freeForCoins, list);
        PlacePowerUp(chunk, freeForCoins, list);
    }

    // --------------------------------------------------- поставленное начало

    /// <summary>
    /// Один сильный первый маршрут вместо лотереи:
    ///
    /// 0–30   — цепочка монет по центру;
    /// 30–60  — красный блок заставляет сменить полосу;
    /// 60–90  — игрок возвращается в центр и прыгает через жёлтый барьер;
    /// 90–150 — магнит прямо перед пандусом, затем длинная крыша с монетами;
    /// 150–180 — спокойное приземление перед обычным генератором.
    ///
    /// Все опасные объекты ставятся теми же пулами и префабами, что и
    /// случайные. Поэтому у начала нет отдельной физики, которую можно было
    /// бы случайно сломать при следующей правке препятствий.
    /// </summary>
    private void PopulateAuthoredOpening(Chunk chunk, List<PooledItem> list)
    {
        float z = chunk.transform.position.z;

        if (z < 30f)
        {
            PlaceCoinLine(chunk, lane: 1, localStart: 3f, localEnd: 27f, count: coinsPerChunk, list);
            _lastPassableLanes = new[] { true, true, true };
            return;
        }

        if (z < 60f)
        {
            // Красный блок по центру. С обеих сторон лежат одинаковые монеты:
            // игрок получает мгновенное подтверждение, что выбрал правильный
            // путь, но не вынужден угадывать «правильную» сторону.
            PlaceObstacleAt(chunk, row: 0, lane: 1, blockPrefab, list);
            PlaceCoinLine(chunk, lane: 0, localStart: 13f, localEnd: 26f, count: 4, list);
            PlaceCoinLine(chunk, lane: 2, localStart: 13f, localEnd: 26f, count: 4, list);
            _lastPassableLanes = new[] { true, false, true };
            return;
        }

        if (z < 90f)
        {
            // Две жёлтые преграды возвращают игрока из боковой полосы в центр,
            // а следующая центральная уже просит настоящий прыжок. Между ними
            // ровно один ряд: это коротко, но не требует двух действий сразу.
            PlaceObstacleAt(chunk, row: 0, lane: 0, jumpPrefab, list);
            PlaceObstacleAt(chunk, row: 0, lane: 2, jumpPrefab, list);
            PlaceObstacleAt(chunk, row: 1, lane: 1, jumpPrefab, list);
            PlacePowerUpOfType(chunk, PowerUpType.Magnet, lane: 1, localZ: 25f, list);
            _lastActionZ = z + 15f;
            _lastPassableLanes = new[] { true, false, true };
            return;
        }

        if (z < 120f)
        {
            // Пандус и дорожка монет объясняют крышу без стрелок и без
            // остановки игры. Это первый настоящий «вау»-момент забега.
            PlaceTrainRun(chunk, lane: 1, withRamp: true, list, forceRoofCoins: true);
            _lastPassableLanes = new[] { true, false, true };
            return;
        }

        if (z < 150f)
        {
            PlaceTrainRun(chunk, lane: 1, withRamp: false, list, forceRoofCoins: true);

            // После этой крыши центр нельзя сразу забить препятствием или
            // новым поездом: игроку нужно честно приземлиться.
            _laneClearRows[1] = 1;
            _trainGapLeft[1] = Mathf.Max(0, trainGapChunks);
            _lastPassableLanes = new[] { true, false, true };
            return;
        }

        // Последний чанк начала намеренно пустой: игра даёт секунду после
        // крыши, а затем в 180 м начинает уже полноценную случайную гонку.
        _lastPassableLanes = new[] { true, true, true };
    }

    /// <summary>Поставить один обычный объект в точку ряда, не дублируя код пула.</summary>
    private void PlaceObstacleAt(Chunk chunk, int row, int lane, Obstacle prefab, List<PooledItem> list)
    {
        if (prefab == null || !_obstaclePools.TryGetValue(prefab, out ObjectPool pool)) return;
        if (row < 0 || lane < 0 || lane > 2) return;

        Transform[] points = chunk.SpawnPoints;
        int index = row * 3 + lane;
        if (points == null || index >= points.Length || points[index] == null) return;

        GameObject instance = pool.Get();
        instance.transform.SetParent(chunk.transform, false);
        instance.transform.SetPositionAndRotation(points[index].position, Quaternion.identity);
        list.Add(new PooledItem { Instance = instance, Pool = pool });
    }

    /// <summary>Рисует надёжную дорожку монет для поставленного начала.</summary>
    private void PlaceCoinLine(Chunk chunk, int lane, float localStart, float localEnd,
                               int count, List<PooledItem> list)
    {
        if (_coinPool == null || count <= 0) return;

        float laneX = (lane - 1) * 2.5f;
        float step = count > 1 ? (localEnd - localStart) / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            GameObject coin = _coinPool.Get();
            coin.transform.SetParent(chunk.transform, false);
            coin.transform.SetPositionAndRotation(
                new Vector3(laneX, coinHeight, chunk.transform.position.z + localStart + step * i),
                Quaternion.identity);
            list.Add(new PooledItem { Instance = coin, Pool = _coinPool });
        }
    }

    /// <summary>Поставить конкретный бонус, а не бросать кубик прямо перед обучением.</summary>
    private void PlacePowerUpOfType(Chunk chunk, PowerUpType type, int lane, float localZ,
                                    List<PooledItem> list)
    {
        if (powerUpPrefabs == null) return;

        for (int i = 0; i < powerUpPrefabs.Length; i++)
        {
            PowerUp prefab = powerUpPrefabs[i];
            if (prefab == null || prefab.Type != type) continue;
            if (!_powerUpPools.TryGetValue(prefab, out ObjectPool pool)) return;

            GameObject instance = pool.Get();
            instance.transform.SetParent(chunk.transform, false);
            instance.transform.SetPositionAndRotation(
                new Vector3((lane - 1) * 2.5f, powerUpHeight,
                            chunk.transform.position.z + localZ), Quaternion.identity);
            list.Add(new PooledItem { Instance = instance, Pool = pool });
            return;
        }
    }

    // --------------------------------------------------------------- составы

    /// <summary>
    /// Продлевает уже идущие составы, начинает новые и раскладывает вагоны.
    /// Возвращает полосы, занятые составом в этом чанке.
    ///
    /// Два жёстких правила, на которых всё держится:
    ///   1. Максимум ОДНА полоса с поездом. По обе стороны от состава остаётся
    ///      наземный путь, поэтому игрок не получает обязательную смерть,
    ///      оказавшись не на той стороне состава.
    ///   2. Составы не должны съесть ВСЕ полосы, проходимые в предыдущем ряду.
    ///      Хотя бы одна обязана остаться, иначе следующему ряду не с чем
    ///      будет разделить полосу и игрока заставят перестроиться там,
    ///      где он не успеет.
    /// </summary>
    private bool[] UpdateTrainRuns(int tier, Chunk chunk, List<PooledItem> list)
    {
        var lanes = new bool[3];
        if (trainPrefab == null) return lanes;

        float chunkZ = chunk.transform.position.z;

        // Сколько проходимых в предыдущем ряду полос ещё свободны от составов.
        //
        // Продолжающиеся составы тут не в счёт: если в полосе идёт состав,
        // то и в прошлом чанке он там шёл, а значит эта полоса в предыдущем
        // ряду проходимой не была.
        //
        // Раньше здесь стоял запрет только на ЕДИНСТВЕННУЮ проходимую полосу.
        // Этого не хватало: при двух проходимых полосах два состава спокойно
        // занимали обе. Симуляция ловила такое примерно раз на сто рядов.
        int shareLeft = 0;
        for (int lane = 0; lane < 3; lane++)
            if (_lastPassableLanes[lane]) shareLeft++;

        int active = 0;
        for (int lane = 0; lane < 3; lane++)
            if (_trainChunksLeft[lane] > 0) active++;

        for (int lane = 0; lane < 3; lane++)
        {
            bool continuing = _trainChunksLeft[lane] > 0;
            bool withRamp = false;

            if (continuing)
            {
                _trainChunksLeft[lane]--;
            }
            else
            {
                // Полоса ещё «остывает» после предыдущего состава. Пока пауза
                // не вышла, новый поезд тут не начинается — иначе он встанет
                // встык к предыдущему и они прочитаются как один длинный
                // состав с провалом и пандусом посередине крыши.
                if (_trainGapLeft[lane] > 0)
                {
                    _trainGapLeft[lane]--;
                    continue;
                }

                // Занять последнюю полосу, по которой можно было приехать
                // из предыдущего ряда, нельзя ни при каких условиях.
                bool eatsShare = _lastPassableLanes[lane];
                if (eatsShare && shareLeft <= 1) continue;

                bool allowed = tier >= trainMinTier
                            && active < MaxActiveTrainLanes
                            && chunkZ >= startSafeDistance
                            && Random.value <= trainChance;

                if (!allowed) continue;

                _trainChunksLeft[lane] =
                    Random.Range(trainRunMinChunks, trainRunMaxChunks + 1) - 1;

                withRamp = _rampPool != null && Random.value <= rampChance;
                active++;
                if (eatsShare) shareLeft--;
            }

            lanes[lane] = true;
            PlaceTrainRun(chunk, lane, withRamp, list);

            // Состав кончается в этом чанке. Дальше два запрета:
            //   ряд после него свободен от препятствий — игроку надо куда-то
            //   приземлиться, спрыгнув с крыши;
            //   и целый чанк свободен от новых составов — чтобы поезда
            //   не слипались в один бесконечный с пандусами посередине.
            if (_trainChunksLeft[lane] == 0)
            {
                _laneClearRows[lane] = 1;
                _trainGapLeft[lane] = Mathf.Max(0, trainGapChunks);
            }
        }

        return lanes;
    }

    /// <summary>
    /// Выкладывает вагоны встык от начала чанка до конца. Первый слот может
    /// занять пандус — тогда вагонов на один меньше.
    /// </summary>
    private void PlaceTrainRun(Chunk chunk, int lane, bool withRamp, List<PooledItem> list,
                               bool forceRoofCoins = false)
    {
        if (!_obstaclePools.TryGetValue(trainPrefab, out ObjectPool pool)) return;

        const float segment = Obstacle.TrainMetrics.Length;

        float laneX = (lane - 1) * 2.5f;
        float chunkZ = chunk.transform.position.z;
        int slots = Mathf.Max(1, Mathf.RoundToInt(chunk.Length / segment));

        int first = 0;

        if (withRamp)
        {
            GameObject ramp = _rampPool.Get();
            ramp.transform.SetParent(chunk.transform, false);
            ramp.transform.SetPositionAndRotation(
                new Vector3(laneX, 0f, chunkZ), Quaternion.identity);

            list.Add(new PooledItem { Instance = ramp, Pool = _rampPool });
            first = 1;
        }

        for (int slot = first; slot < slots; slot++)
        {
            GameObject car = pool.Get();
            car.transform.SetParent(chunk.transform, false);
            car.transform.SetPositionAndRotation(
                new Vector3(laneX, 0f, chunkZ + slot * segment), Quaternion.identity);

            list.Add(new PooledItem { Instance = car, Pool = pool });
        }

        PlaceRoofCoins(chunk, lane, withRamp, list, forceRoofCoins);
    }

    /// <summary>
    /// Дорожка монет по крыше состава, а на первом чанке — прямо по пандусу.
    ///
    /// Монеты на подъёме не украшение, а указатель: игрок не обязан понимать
    /// заранее, что по наклонной плите можно вбежать наверх. Цепочка монет,
    /// уходящая вверх, объясняет это без единого слова обучения.
    /// </summary>
    private void PlaceRoofCoins(Chunk chunk, int lane, bool withRamp, List<PooledItem> list,
                                bool force = false)
    {
        if (_coinPool == null || coinsPerChunk <= 0) return;
        if (!force && Random.value > roofCoinChance) return;

        float laneX = (lane - 1) * 2.5f;
        float chunkZ = chunk.transform.position.z;
        float roof = Obstacle.TrainMetrics.RoofHeight;
        float run = Obstacle.TrainMetrics.RampRun;

        int count = Mathf.Max(3, coinsPerChunk);
        float step = (chunk.Length - 2f) / (count - 1);

        for (int i = 0; i < count; i++)
        {
            float localZ = 1f + step * i;

            // Высота монеты повторяет профиль поверхности под ней.
            float surface = withRamp && localZ < run ? roof * (localZ / run) : roof;

            GameObject coin = _coinPool.Get();
            coin.transform.SetParent(chunk.transform, false);
            coin.transform.SetPositionAndRotation(
                new Vector3(laneX, surface + coinHeight, chunkZ + localZ),
                Quaternion.identity);

            list.Add(new PooledItem { Instance = coin, Pool = _coinPool });
        }
    }

    /// <summary>Вернуть всё содержимое чанка обратно в пулы.</summary>
    public void Clear(Chunk chunk)
    {
        if (!_spawned.TryGetValue(chunk, out List<PooledItem> list)) return;

        foreach (PooledItem item in list)
        {
            if (item.Pool != null) item.Pool.Release(item.Instance);
            else if (item.Instance != null) item.Instance.SetActive(false);
        }

        list.Clear();
    }

    // ---------------------------------------------------------------- монеты

    private void PlaceCoins(Chunk chunk, bool[] freeLanes, List<PooledItem> list)
    {
        if (_coinPool == null || coinsPerChunk <= 0) return;
        if (Random.value > coinChance) return;

        // Полоса, свободная во всех рядах чанка.
        int chosenLane = PickFreeLane(freeLanes);
        if (chosenLane < 0) return;   // весь чанк перекрыт прыжковым рядом — без монет

        float laneX = (chosenLane - 1) * 2.5f;
        float startZ = chunk.transform.position.z + 3f;
        float step = (chunk.Length - 6f) / Mathf.Max(1, coinsPerChunk - 1);

        for (int i = 0; i < coinsPerChunk; i++)
        {
            GameObject coin = _coinPool.Get();
            coin.transform.SetParent(chunk.transform, false);
            coin.transform.SetPositionAndRotation(
                new Vector3(laneX, coinHeight, startZ + step * i), Quaternion.identity);

            list.Add(new PooledItem { Instance = coin, Pool = _coinPool });
        }
    }

    // --------------------------------------------------------------- бонусы

    private void PlacePowerUp(Chunk chunk, bool[] freeLanes, List<PooledItem> list)
    {
        if (_powerUpPools.Count == 0) return;
        if (Random.value > powerUpChance) return;

        int lane = PickFreeLane(freeLanes);
        if (lane < 0) return;

        PowerUp prefab = powerUpPrefabs[Random.Range(0, powerUpPrefabs.Length)];
        if (prefab == null || !_powerUpPools.TryGetValue(prefab, out ObjectPool pool)) return;

        GameObject instance = pool.Get();
        instance.transform.SetParent(chunk.transform, false);
        instance.transform.SetPositionAndRotation(
            new Vector3((lane - 1) * 2.5f, powerUpHeight,
                        chunk.transform.position.z + chunk.Length * 0.5f),
            Quaternion.identity);

        list.Add(new PooledItem { Instance = instance, Pool = pool });
    }

    /// <summary>Равномерно выбирает одну из свободных полос. -1, если свободных нет.</summary>
    private static int PickFreeLane(bool[] freeLanes)
    {
        int chosen = -1;
        int seen = 0;

        for (int lane = 0; lane < 3; lane++)
        {
            if (!freeLanes[lane]) continue;

            seen++;
            if (Random.Range(0, seen) == 0) chosen = lane;
        }

        return chosen;
    }

    // ---------------------------------------------------------- вспомогательное

    private List<PooledItem> GetList(Chunk chunk)
    {
        if (!_spawned.TryGetValue(chunk, out List<PooledItem> list))
        {
            list = new List<PooledItem>();
            _spawned[chunk] = list;
        }
        return list;
    }

    private Obstacle PrefabFor(char symbol)
    {
        switch (symbol)
        {
            case 'B': return blockPrefab;
            case 'J': return jumpPrefab;
            case 'S': return slidePrefab;

            // 'T' сюда намеренно не попадает. Поезд длиной 13 юнитов не
            // ставится в точку ряда, как остальные препятствия, — его кладёт
            // PlaceTrain один раз на весь чанк.
            default: return null;
        }
    }

    /// <summary>
    /// Приводит раскладку из таблицы к реальности этого чанка:
    /// в полосах с составом ставит 'T', а в полосах, которым велено
    /// оставаться пустыми (только что кончился состав), — точку.
    /// </summary>
    private string Adapt(string pattern, bool[] trainLanes)
    {
        char[] lanes = pattern.ToCharArray();

        for (int lane = 0; lane < 3; lane++)
        {
            if (trainLanes[lane]) lanes[lane] = 'T';
            else if (_laneClearRows[lane] > 0) lanes[lane] = '.';
        }

        return new string(lanes);
    }

    private string PickPattern(int tier, float worldZ, bool[] trainLanes)
    {
        IReadOnlyList<string> table = ObstaclePatterns.ForTier(tier);
        bool actionAllowed = worldZ - _lastActionZ >= minActionSpacing;

        // Двадцати попыток с запасом хватает: подходящих раскладок в таблице много.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string candidate = Adapt(table[Random.Range(0, table.Count)], trainLanes);

            if (!actionAllowed && ObstaclePatterns.RequiresAction(candidate)) continue;
            if (!ObstaclePatterns.HasTwoClearGroundLanesAroundTrains(candidate)) continue;

            bool[] lanes = ObstaclePatterns.PassableLanes(candidate);

            // Состав мог занять последнюю свободную полосу — в таблице такой
            // раскладки быть не могло, а после подстановки может.
            if (!ObstaclePatterns.HasPassableLane(lanes)) continue;
            if (!ObstaclePatterns.SharesLane(_lastPassableLanes, lanes)) continue;

            return candidate;
        }

        // Ничего не подошло — оставляем ряд пустым. Лучше скучный ряд,
        // чем непроходимый. С одним составом это всегда две свободные полосы,
        // а UpdateTrainRuns не даёт составу съесть последний путь из прошлого
        // ряда.
        return Adapt("...", trainLanes);
    }
}
