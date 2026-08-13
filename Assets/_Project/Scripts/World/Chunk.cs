using UnityEngine;

/// <summary>
/// Один кусок трассы. Пивот — в начале куска (ближний к игроку край),
/// кусок тянется вперёд по +Z ровно на Length юнитов.
///
/// Куда вешать: на корневой объект префаба чанка.
/// </summary>
public class Chunk : MonoBehaviour
{
    [Header("Геометрия")]
    [Tooltip("Длина куска по Z. У всех чанков должна совпадать.")]
    [SerializeField] private float length = 30f;

    [Header("Сложность")]
    [Tooltip("С какой дистанции (метры) этот чанк может выпадать. 0 — с самого старта.")]
    [SerializeField] private float unlockAtDistance = 0f;

    [Tooltip("Относительный шанс выпадения среди доступных чанков.")]
    [SerializeField] private float weight = 1f;

    [Header("Точки для препятствий (этап M3)")]
    [Tooltip("Пустышки, на которых будут появляться препятствия и монеты.")]
    [SerializeField] private Transform[] spawnPoints;

    public float Length => length;
    public float UnlockAtDistance => unlockAtDistance;
    public float Weight => Mathf.Max(0.01f, weight);
    public Transform[] SpawnPoints => spawnPoints;

    /// <summary>Из какого префаба сделан этот экземпляр — нужно, чтобы вернуть его в свой пул.</summary>
    [System.NonSerialized] public Chunk SourcePrefab;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        // Пул создаёт копии чанков ещё до начала забега. Собираем школьные
        // модели именно в этот момент, чтобы первое появление библиотеки или
        // спортзала не дало микрофриз уже во время игры.
        SchoolChunkVisuals.EnsureBuilt(this);
    }

    /// <summary>Вызывается каждый раз, когда чанк достают из пула.</summary>
    public virtual void OnSpawned()
    {
        if (!Application.isPlaying) return;

        // Декорации создаются один раз на экземпляр чанка и потом ездят
        // вместе с ним через пул. Это даёт полноценное окружение без
        // Instantiate/Destroy в каждой новой секции трассы.
        SchoolChunkVisuals.EnsureBuilt(this);
    }

    /// <summary>Вызывается перед возвратом чанка в пул.</summary>
    public virtual void OnDespawned()
    {
        // На M3 здесь будет уборка препятствий обратно в их пулы.
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.forward * length;
        Gizmos.DrawLine(start + Vector3.left * 6f, start + Vector3.right * 6f);
        Gizmos.DrawLine(end + Vector3.left * 6f, end + Vector3.right * 6f);

        if (spawnPoints == null) return;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        foreach (Transform point in spawnPoints)
        {
            if (point != null) Gizmos.DrawWireSphere(point.position + Vector3.up * 0.5f, 0.35f);
        }
    }
}
