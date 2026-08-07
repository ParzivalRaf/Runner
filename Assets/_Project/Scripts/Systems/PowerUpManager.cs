using UnityEngine;

/// <summary>
/// Держит таймеры активных бонусов и применяет их эффекты.
///
/// Эффекты сознательно раздаются отсюда наружу, а не запрашиваются каждой
/// системой у себя: PlayerController ничего не знает про бонусы, ему просто
/// выставляют множители. Так проще добавлять новые бонусы.
///
/// Куда вешать: на объект GameManager.
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private PlayerController player;

    [Header("Базовая длительность, секунды")]
    [SerializeField] private float magnetDuration = 6f;
    [SerializeField] private float coffeeDuration = 6f;
    [SerializeField] private float sneakersDuration = 7f;
    [SerializeField] private float doubleScoreDuration = 8f;

    [Header("Сила эффектов")]
    [Tooltip("Во сколько раз кофе ускоряет бег.")]
    [SerializeField] private float coffeeSpeedMultiplier = 1.6f;

    [Tooltip("Во сколько раз кроссовки увеличивают высоту прыжка.")]
    [SerializeField] private float sneakersJumpMultiplier = 1.8f;

    private const int TypeCount = 4;

    private readonly float[] _remaining = new float[TypeCount];
    private readonly float[] _total = new float[TypeCount];

    /// <summary>Кофе даёт неуязвимость.</summary>
    public bool IsInvincible => IsActive(PowerUpType.Coffee);

    /// <summary>Множитель монет от бонуса ×2.</summary>
    public int CoinMultiplier => IsActive(PowerUpType.DoubleScore) ? 2 : 1;

    private void Awake()
    {
        Instance = this;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsActive(PowerUpType type) => _remaining[(int)type] > 0f;

    public float Remaining(PowerUpType type) => Mathf.Max(0f, _remaining[(int)type]);

    /// <summary>Сколько осталось от полной длительности, 0..1. Для полоски в HUD.</summary>
    public float Fraction(PowerUpType type)
    {
        int i = (int)type;
        if (_total[i] <= 0f) return 0f;

        return Mathf.Clamp01(_remaining[i] / _total[i]);
    }

    public void Activate(PowerUpType type)
    {
        int i = (int)type;

        float duration = BaseDuration(type)
                       + UpgradeShop.BonusSecondsFor(type)
                       + CharacterExtraSeconds();

        // Повторный подбор не складывается, а обновляет таймер целиком —
        // так игрок не может накопить минуту неуязвимости.
        _remaining[i] = duration;
        _total[i] = duration;
    }

    public void ResetRun()
    {
        for (int i = 0; i < TypeCount; i++)
        {
            _remaining[i] = 0f;
            _total[i] = 0f;
        }

        ApplyToPlayer();
    }

    /// <summary>
    /// Прибавка от способности персонажа. В отличие от апгрейдов магазина,
    /// она действует на все четыре бонуса, а не только на магнит и кофе.
    /// </summary>
    private static float CharacterExtraSeconds() =>
        CharacterManager.Instance != null ? CharacterManager.Instance.ExtraPowerUpSeconds : 0f;

    private float BaseDuration(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Magnet: return magnetDuration;
            case PowerUpType.Coffee: return coffeeDuration;
            case PowerUpType.Sneakers: return sneakersDuration;
            case PowerUpType.DoubleScore: return doubleScoreDuration;
            default: return 5f;
        }
    }

    private void Update()
    {
        // Time.deltaTime, а не unscaled: на паузе таймеры бонусов замирают.
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = 0; i < TypeCount; i++)
        {
            if (_remaining[i] <= 0f) continue;
            _remaining[i] = Mathf.Max(0f, _remaining[i] - dt);
        }

        ApplyToPlayer();
    }

    private void ApplyToPlayer()
    {
        if (player == null) return;

        player.ExternalSpeedMultiplier =
            IsActive(PowerUpType.Coffee) ? coffeeSpeedMultiplier : 1f;

        player.ExternalJumpMultiplier =
            IsActive(PowerUpType.Sneakers) ? sneakersJumpMultiplier : 1f;
    }
}
