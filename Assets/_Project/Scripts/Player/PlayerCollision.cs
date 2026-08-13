using UnityEngine;

/// <summary>
/// Ловит столкновения игрока с препятствиями.
///
/// Работает потому, что у игрока кинематический Rigidbody, а у препятствий
/// коллайдеры с галочкой Is Trigger — этого достаточно, чтобы Unity вызвала
/// OnTriggerEnter, хотя физически ничто никуда не отталкивается.
///
/// Куда вешать: на объект Player.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Мягкий контакт с поездом")]
    [Tooltip("Сколько боковых касаний поезда подряд заканчивают забег.")]
    [SerializeField] private int trainBrushesBeforeGameOver = 3;

    [Tooltip("Через сколько секунд чистого бега серия боковых касаний сбрасывается.")]
    [SerializeField] private float trainBrushWindow = 3f;

    [Tooltip("Защита от нескольких сигналов одного и того же касания.")]
    [SerializeField] private float trainBrushCooldown = 0.65f;

    private PlayerController _player;
    private int _trainBrushes;
    private float _lastTrainBrushAt = -999f;
    private float _ignoreTrainBrushesUntil = -999f;

    private void Awake() => _player = GetComponent<PlayerController>();

    private void OnTriggerEnter(Collider other)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning) return;
        if (GameManager.Instance.GodMode) return;

        Obstacle obstacle = other.GetComponentInParent<Obstacle>();
        if (obstacle == null) return;

        // Под кофе игрок проламывается сквозь препятствие, а не умирает.
        // Объект просто выключаем: в пул его всё равно вернёт ObstacleSpawner,
        // а при следующем Get он включится обратно.
        if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsInvincible)
        {
            obstacle.gameObject.SetActive(false);

            // Проламывание сквозь препятствие обязано ощущаться как удар,
            // иначе кофе выглядит так, будто препятствия просто исчезли.
            if (GameFeel.Instance != null) GameFeel.Instance.Shake(0.35f);
            if (EffectManager.Instance != null)
                EffectManager.Instance.PlayCrash(transform.position + Vector3.up);

            return;
        }

        // У состава различаем лобовой удар и касание борта. Спереди поезд
        // по-прежнему опасен, а при попытке перестроиться в занятый ряд игрок
        // получает предупреждение, тряску и возврат в безопасную полосу.
        // Третья ошибка в короткой серии уже считается настоящим столкновением.
        if (obstacle.ObstacleKind == Obstacle.Kind.Train && IsTrainSideBrush(obstacle))
        {
            if (TryHandleTrainSideBrush(obstacle)) return;
        }

        // Щит персонажа — последний шанс. Одно столкновение за забег,
        // и препятствие исчезает так же, как под кофе. Раньше это
        // происходило совершенно молча: игрок не понимал, что его спасли,
        // и в следующий раз рассчитывал на щит, которого уже нет.
        if (CharacterManager.Instance != null && CharacterManager.Instance.TryConsumeShield())
        {
            obstacle.gameObject.SetActive(false);

            if (GameFeel.Instance != null)
            {
                GameFeel.Instance.Shake(0.6f);
                GameFeel.Instance.HitStop(0.07f);
            }

            if (EffectManager.Instance != null)
                EffectManager.Instance.PlayCrash(transform.position + Vector3.up);

            return;
        }

        // Эффект ставим здесь, а не в GameManager: только тут известно,
        // где именно игрок встретился с препятствием.
        if (EffectManager.Instance != null)
            EffectManager.Instance.PlayCrash(transform.position + Vector3.up);

        // Хитстоп ДО GameOver: заморозка должна начаться в кадре удара,
        // а не после того, как всплыл экран проигрыша.
        if (GameFeel.Instance != null) GameFeel.Instance.Crash();

        GameManager.Instance.GameOver();
    }

    private bool IsTrainSideBrush(Obstacle train)
    {
        // В центре вагона — лобовой удар. Внешняя часть его ширины доступна
        // только при перестроении рядом с составом, то есть это касание борта.
        float lateralDistance = Mathf.Abs(transform.position.x - train.transform.position.x);
        return lateralDistance > Obstacle.TrainMetrics.Width * 0.25f;
    }

    /// <summary>
    /// Возвращает true, если контакт прощён. При третьем ударе возвращает
    /// false, чтобы ниже сработала обычная смерть и все её эффекты.
    /// </summary>
    private bool TryHandleTrainSideBrush(Obstacle train)
    {
        if (Time.time < _ignoreTrainBrushesUntil) return true;

        if (Time.time - _lastTrainBrushAt > trainBrushWindow)
            _trainBrushes = 0;

        _lastTrainBrushAt = Time.time;
        _ignoreTrainBrushesUntil = Time.time + trainBrushCooldown;
        _trainBrushes++;

        if (_trainBrushes >= Mathf.Max(1, trainBrushesBeforeGameOver))
            return false;

        if (_player != null) _player.BounceAwayFromTrain(train.transform.position.x);

        if (GameFeel.Instance != null)
        {
            // В CameraFollow сила тряски возводится в квадрат, поэтому
            // прежние 0.20–0.26 почти не читались. Боковой удар должен быть
            // заметным предупреждением, но всё ещё слабее смертельного 1.0.
            GameFeel.Instance.Shake(0.45f + _trainBrushes * 0.15f);
            GameFeel.Instance.PunchFov(-0.30f);
        }

        MobileHaptics.Light();
        return true;
    }

    /// <summary>Сбрасывает серию ударов перед новым забегом.</summary>
    public void ResetRun()
    {
        _trainBrushes = 0;
        _lastTrainBrushAt = -999f;
        _ignoreTrainBrushesUntil = -999f;
    }
}
