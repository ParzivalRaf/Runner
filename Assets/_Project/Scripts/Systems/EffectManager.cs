using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Партиклы на подбор монеты и на столкновение.
///
/// Работает по тому же правилу, что и всё остальное в игре: ни одного
/// Instantiate во время забега. Эффекты берутся из пула, а возвращаются
/// туда по таймеру — ParticleSystem не умеет сам сообщать, что доиграл,
/// поэтому длительность считаем один раз по префабу.
///
/// Куда вешать: на объект GameManager.
/// В инспекторе: перетащить префабы эффектов из Assets/_Project/Prefabs/Effects.
/// </summary>
public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance { get; private set; }

    [Header("Префабы")]
    [SerializeField] private ParticleSystem coinBurstPrefab;
    [SerializeField] private ParticleSystem crashBurstPrefab;

    [Header("Пул")]
    [Tooltip("Сколько копий каждого эффекта создать заранее.")]
    [SerializeField] private int prewarmCount = 16;

    private struct ActiveEffect
    {
        public GameObject Instance;
        public ObjectPool Pool;
        public float ReleaseAt;
    }

    private Transform _root;
    private ObjectPool _coinPool;
    private ObjectPool _crashPool;

    private float _coinLifetime;
    private float _crashLifetime;

    private readonly List<ActiveEffect> _active = new List<ActiveEffect>();

    private void Awake()
    {
        Instance = this;

        var rootGo = new GameObject("Effects");
        _root = rootGo.transform;

        if (coinBurstPrefab != null)
        {
            _coinPool = new ObjectPool(coinBurstPrefab.gameObject, _root, prewarmCount);
            _coinLifetime = LifetimeOf(coinBurstPrefab);
        }

        if (crashBurstPrefab != null)
        {
            _crashPool = new ObjectPool(crashBurstPrefab.gameObject, _root, prewarmCount);
            _crashLifetime = LifetimeOf(crashBurstPrefab);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Сколько эффект живёт целиком: вспышка плюс самая долгая частица.
    /// Запас в 10% — чтобы не убрать эффект за кадр до конца.
    /// </summary>
    private static float LifetimeOf(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        return (main.duration + main.startLifetime.constantMax) * 1.1f;
    }

    public void PlayCoin(Vector3 position) => Spawn(_coinPool, _coinLifetime, position);

    public void PlayCrash(Vector3 position) => Spawn(_crashPool, _crashLifetime, position);

    private void Spawn(ObjectPool pool, float lifetime, Vector3 position)
    {
        if (pool == null) return;

        GameObject instance = pool.Get();
        instance.transform.SetParent(_root, false);
        instance.transform.position = position;

        var system = instance.GetComponent<ParticleSystem>();
        if (system != null)
        {
            system.Clear();
            system.Play();
        }

        _active.Add(new ActiveEffect
        {
            Instance = instance,
            Pool = pool,
            ReleaseAt = Time.time + lifetime
        });
    }

    private void Update()
    {
        // Идём с конца: удаление из списка не сдвигает ещё не проверенные.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (Time.time < _active[i].ReleaseAt) continue;

            _active[i].Pool.Release(_active[i].Instance);
            _active.RemoveAt(i);
        }
    }

    /// <summary>Убрать все эффекты. Вызывает GameManager перед новым забегом.</summary>
    public void ResetRun()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].Pool.Release(_active[i].Instance);

        _active.Clear();
    }
}
