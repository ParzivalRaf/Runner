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

    [Tooltip("Минимальное расстояние между рядами, требующими прыжка/подката.")]
    [SerializeField] private float minActionSpacing = 22f;

    [Tooltip("Сколько объектов каждого типа создать заранее.")]
    [SerializeField] private int prewarmPerPrefab = 8;

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
        if (_initialized) return;
        _initialized = true;

        _poolRoot = new GameObject("ObstaclePool").transform;
        _poolRoot.SetParent(transform, false);

        CreateObstaclePool(blockPrefab);
        CreateObstaclePool(jumpPrefab);
        CreateObstaclePool(slidePrefab);
        CreateObstaclePool(trainPrefab);

        if (coinPrefab != null)
            _coinPool = new ObjectPool(coinPrefab.gameObject, _poolRoot, coinsPerChunk * 4);

        if (rampPrefab != null)
            _rampPool = new ObjectPool(rampPrefab, _poolRoot, 4);

        if (powerUpPrefabs != null)
        {
            foreach (PowerUp prefab in powerUpPrefabs)
            {
                if (prefab == null || _powerUpPools.ContainsKey(prefab)) continue;
                _powerUpPools[prefab] = new ObjectPool(prefab.gameObject, _poolRoot, 1);
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

    private void CreateObstaclePool(Obstacle prefab)
    {
        if (prefab == null || _obstaclePools.ContainsKey(prefab)) return;
        _obstaclePools[prefab] = new ObjectPool(prefab.gameObject, _poolRoot, prewarmPerPrefab);
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

                list.Add(new PooledItem { Instance = instance, Pool = pool });
            }
        }

        PlaceCoins(chunk, freeForCoins, list);
        PlacePowerUp(chunk, freeForCoins, list);
    }

    // --------------------------------------------------------------- составы

    /// <summary>
    /// Продлевает уже идущие составы, начинает новые и раскладывает вагоны.
    /// Возвращает полосы, занятые составом в этом чанке.
    ///
    /// Два жёстких правила, на которых всё держится:
    ///   1. Максимум ДВЕ полосы из трёх. Третья обязана остаться наземной,
    ///      иначе гарантия проходимости превращается в «проходимо, если
    ///      успеешь запрыгнуть».
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
                            && active < 2
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
    private void PlaceTrainRun(Chunk chunk, int lane, bool withRamp, List<PooledItem> list)
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

        PlaceRoofCoins(chunk, lane, withRamp, list);
    }

    /// <summary>
    /// Дорожка монет по крыше состава, а на первом чанке — прямо по пандусу.
    ///
    /// Монеты на подъёме не украшение, а указатель: игрок не обязан понимать
    /// заранее, что по наклонной плите можно вбежать наверх. Цепочка монет,
    /// уходящая вверх, объясняет это без единого слова обучения.
    /// </summary>
    private void PlaceRoofCoins(Chunk chunk, int lane, bool withRamp, List<PooledItem> list)
    {
        if (_coinPool == null || coinsPerChunk <= 0) return;
        if (Random.value > roofCoinChance) return;

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

            bool[] lanes = ObstaclePatterns.PassableLanes(candidate);

            // Состав мог занять последнюю свободную полосу — в таблице такой
            // раскладки быть не могло, а после подстановки может.
            if (!ObstaclePatterns.HasPassableLane(lanes)) continue;
            if (!ObstaclePatterns.SharesLane(_lastPassableLanes, lanes)) continue;

            return candidate;
        }

        // Ничего не подошло — оставляем ряд пустым. Лучше скучный ряд,
        // чем непроходимый. С составами это как минимум одна свободная полоса,
        // и она заведомо была проходима в предыдущем ряду: UpdateTrainRuns
        // не даёт составу начаться в единственной проходимой полосе.
        return Adapt("...", trainLanes);
    }
}
