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
    [SerializeField] private float startSafeDistance = 45f;

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

    private float _lastActionZ = -9999f;
    private bool[] _lastPassableLanes = { true, true, true };

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

        if (coinPrefab != null)
            _coinPool = new ObjectPool(coinPrefab.gameObject, _poolRoot, coinsPerChunk * 3);

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

        // Полосы, свободные во всех рядах этого чанка — по ним пустим монеты.
        bool[] freeForCoins = { true, true, true };

        foreach (int row in rows)
        {
            Transform anchor = points[row * 3 + 1];
            if (anchor == null) continue;

            // Стартовая зона: даём разбежаться, прежде чем швырять препятствия.
            if (anchor.position.z < startSafeDistance) continue;

            string pattern = PickPattern(tier, anchor.position.z);
            if (logPatterns) Debug.Log($"[Obstacles] z={anchor.position.z:F0} tier={tier} → {pattern}");

            if (ObstaclePatterns.RequiresAction(pattern)) _lastActionZ = anchor.position.z;
            _lastPassableLanes = ObstaclePatterns.PassableLanes(pattern);

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
            default: return null;
        }
    }

    private string PickPattern(int tier, float worldZ)
    {
        IReadOnlyList<string> table = ObstaclePatterns.ForTier(tier);
        bool actionAllowed = worldZ - _lastActionZ >= minActionSpacing;

        // Двадцати попыток с запасом хватает: подходящих раскладок в таблице много.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string candidate = table[Random.Range(0, table.Count)];

            if (!actionAllowed && ObstaclePatterns.RequiresAction(candidate)) continue;

            bool[] lanes = ObstaclePatterns.PassableLanes(candidate);
            if (!ObstaclePatterns.SharesLane(_lastPassableLanes, lanes)) continue;

            return candidate;
        }

        // Ничего не подошло — оставляем ряд пустым. Лучше скучный ряд,
        // чем непроходимый.
        return "...";
    }
}
