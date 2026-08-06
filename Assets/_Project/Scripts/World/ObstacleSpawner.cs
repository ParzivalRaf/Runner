using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Расставляет препятствия по точкам чанка и следит, чтобы трасса всегда
/// оставалась проходимой — не только внутри одного ряда, но и между рядами
/// и между соседними чанками.
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

    [Header("Настройки")]
    [Tooltip("Первые столько метров забега — без препятствий, чтобы игрок успел взяться за телефон.")]
    [SerializeField] private float startSafeDistance = 45f;

    [Tooltip("Минимальное расстояние между рядами, требующими прыжка/подката.")]
    [SerializeField] private float minActionSpacing = 22f;

    [Tooltip("Сколько препятствий каждого типа создать заранее.")]
    [SerializeField] private int prewarmPerPrefab = 8;

    [Tooltip("Печатать в консоль каждую раскладку — полезно при отладке генератора.")]
    [SerializeField] private bool logPatterns = false;

    private readonly Dictionary<Obstacle, ObjectPool> _pools = new Dictionary<Obstacle, ObjectPool>();
    private readonly Dictionary<Chunk, List<GameObject>> _spawned =
        new Dictionary<Chunk, List<GameObject>>();

    private Transform _poolRoot;
    private float _lastActionZ = -9999f;
    private bool[] _lastPassableLanes = { true, true, true };

    private void Awake()
    {
        _poolRoot = new GameObject("ObstaclePool").transform;
        _poolRoot.SetParent(transform, false);

        CreatePool(blockPrefab);
        CreatePool(jumpPrefab);
        CreatePool(slidePrefab);
    }

    private void CreatePool(Obstacle prefab)
    {
        if (prefab == null || _pools.ContainsKey(prefab)) return;
        _pools[prefab] = new ObjectPool(prefab.gameObject, _poolRoot, prewarmPerPrefab);
    }

    /// <summary>Заполнить чанк препятствиями. Вызывать после того, как чанк уже поставлен на место.</summary>
    public void Populate(Chunk chunk, float distance)
    {
        Transform[] points = chunk.SpawnPoints;
        if (points == null || points.Length < 9) return;

        int tier = ObstaclePatterns.TierForDistance(distance);
        int rowCount = ObstaclePatterns.RowsForTier(tier);

        // При одном ряду ставим его в середину чанка, при двух — по краям.
        int[] rows = rowCount == 1 ? new[] { 1 } : new[] { 0, 2 };

        var list = GetList(chunk);

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
                Obstacle prefab = PrefabFor(pattern[lane]);
                if (prefab == null) continue;

                Transform point = points[row * 3 + lane];
                if (point == null) continue;

                GameObject instance = _pools[prefab].Get();
                instance.transform.SetParent(chunk.transform, false);
                instance.transform.SetPositionAndRotation(point.position, Quaternion.identity);

                list.Add(instance);
            }
        }
    }

    /// <summary>Вернуть все препятствия чанка обратно в пулы.</summary>
    public void Clear(Chunk chunk)
    {
        if (!_spawned.TryGetValue(chunk, out List<GameObject> list)) return;

        foreach (GameObject instance in list)
        {
            Obstacle obstacle = instance.GetComponent<Obstacle>();
            Obstacle key = FindPoolKey(obstacle);

            if (key != null) _pools[key].Release(instance);
            else instance.SetActive(false);
        }

        list.Clear();
    }

    // ---------------------------------------------------------------------

    private List<GameObject> GetList(Chunk chunk)
    {
        if (!_spawned.TryGetValue(chunk, out List<GameObject> list))
        {
            list = new List<GameObject>();
            _spawned[chunk] = list;
        }
        return list;
    }

    private Obstacle FindPoolKey(Obstacle obstacle)
    {
        if (obstacle == null) return null;

        // Экземпляры из пула сохраняют тип препятствия — по нему и находим пул.
        if (blockPrefab != null && obstacle.ObstacleKind == Obstacle.Kind.Block) return blockPrefab;
        if (jumpPrefab != null && obstacle.ObstacleKind == Obstacle.Kind.JumpOver) return jumpPrefab;
        if (slidePrefab != null && obstacle.ObstacleKind == Obstacle.Kind.SlideUnder) return slidePrefab;

        return null;
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
