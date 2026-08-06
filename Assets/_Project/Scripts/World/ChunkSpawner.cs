using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Бесконечная трасса. Держит впереди игрока фиксированное количество чанков,
/// а те, что остались далеко позади, возвращает в пул и переиспользует.
/// Ни одного Instantiate после старта — только Get/Release.
///
/// Куда вешать: на пустой GameObject "ChunkSpawner" в сцене.
/// В инспекторе: перетащить Player в поле Player и префабы чанков в Chunk Prefabs.
/// </summary>
public class ChunkSpawner : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Transform player;
    [SerializeField] private Chunk[] chunkPrefabs;

    [Header("Настройки трассы")]
    [Tooltip("Сколько чанков держать впереди игрока.")]
    [SerializeField] private int chunksAhead = 4;

    [Tooltip("На сколько юнитов позади игрока чанк должен уехать, чтобы уйти в пул.")]
    [SerializeField] private float despawnDistanceBehind = 30f;

    [Tooltip("С какой координаты Z начинается первый чанк. Отрицательное значение — чтобы под игроком был пол на старте.")]
    [SerializeField] private float startZ = -30f;

    [Tooltip("Сколько копий каждого чанка создать заранее, до старта забега.")]
    [SerializeField] private int prewarmPerPrefab = 2;

    private readonly Dictionary<Chunk, ObjectPool> _pools = new Dictionary<Chunk, ObjectPool>();
    private readonly List<Chunk> _active = new List<Chunk>();
    private readonly List<Chunk> _candidates = new List<Chunk>();

    private Transform _poolRoot;
    private float _nextSpawnZ;
    private Chunk _lastPrefab;
    private PlayerController _playerController;

    public int ActiveChunkCount => _active.Count;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("[ChunkSpawner] Не назначен Player.");
            enabled = false;
            return;
        }

        if (chunkPrefabs == null || chunkPrefabs.Length == 0)
        {
            Debug.LogError("[ChunkSpawner] Не назначено ни одного префаба чанка.");
            enabled = false;
            return;
        }

        _playerController = player.GetComponent<PlayerController>();

        _poolRoot = new GameObject("ChunkPool").transform;
        _poolRoot.SetParent(transform, false);

        foreach (Chunk prefab in chunkPrefabs)
        {
            if (prefab == null) continue;
            if (_pools.ContainsKey(prefab)) continue;

            _pools[prefab] = new ObjectPool(prefab.gameObject, _poolRoot, prewarmPerPrefab);
        }

        _nextSpawnZ = startZ;
        for (int i = 0; i < chunksAhead + 1; i++) SpawnNext();
    }

    private void Update()
    {
        float playerZ = player.position.z;

        // Досыпаем чанки впереди.
        float horizon = playerZ + chunksAhead * AverageChunkLength();
        int guard = 0;
        while (_nextSpawnZ < horizon && guard++ < 32) SpawnNext();

        // Убираем то, что уехало за спину.
        while (_active.Count > 0)
        {
            Chunk oldest = _active[0];
            float chunkEndZ = oldest.transform.position.z + oldest.Length;
            if (chunkEndZ > playerZ - despawnDistanceBehind) break;

            Despawn(oldest);
        }
    }

    private float AverageChunkLength()
    {
        return chunkPrefabs[0] != null ? chunkPrefabs[0].Length : 30f;
    }

    private void SpawnNext()
    {
        Chunk prefab = PickPrefab();
        if (prefab == null) return;

        GameObject instance = _pools[prefab].Get();
        instance.transform.SetPositionAndRotation(new Vector3(0f, 0f, _nextSpawnZ),
                                                  Quaternion.identity);

        Chunk chunk = instance.GetComponent<Chunk>();
        chunk.SourcePrefab = prefab;
        chunk.OnSpawned();

        _active.Add(chunk);
        _nextSpawnZ += chunk.Length;
        _lastPrefab = prefab;
    }

    private void Despawn(Chunk chunk)
    {
        _active.RemoveAt(0);
        chunk.OnDespawned();

        if (chunk.SourcePrefab != null && _pools.TryGetValue(chunk.SourcePrefab, out ObjectPool pool))
            pool.Release(chunk.gameObject);
        else
            chunk.gameObject.SetActive(false);
    }

    /// <summary>
    /// Выбираем чанк среди тех, что уже разблокированы по дистанции.
    /// Один и тот же кусок два раза подряд не берём — иначе трасса выглядит
    /// как копипаста.
    /// </summary>
    private Chunk PickPrefab()
    {
        float distance = _playerController != null ? _playerController.Distance : 0f;

        _candidates.Clear();
        foreach (Chunk prefab in chunkPrefabs)
        {
            if (prefab == null) continue;
            if (prefab.UnlockAtDistance > distance) continue;
            if (prefab == _lastPrefab && chunkPrefabs.Length > 1) continue;

            _candidates.Add(prefab);
        }

        // Ничего не подошло — берём всё, что открыто, даже если это повтор.
        if (_candidates.Count == 0)
        {
            foreach (Chunk prefab in chunkPrefabs)
            {
                if (prefab != null && prefab.UnlockAtDistance <= distance) _candidates.Add(prefab);
            }
        }

        if (_candidates.Count == 0) return chunkPrefabs[0];

        float totalWeight = 0f;
        foreach (Chunk candidate in _candidates) totalWeight += candidate.Weight;

        float roll = Random.value * totalWeight;
        foreach (Chunk candidate in _candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0f) return candidate;
        }

        return _candidates[_candidates.Count - 1];
    }
}
