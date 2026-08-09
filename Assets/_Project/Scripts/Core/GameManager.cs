using System;
using UnityEngine;

/// <summary>
/// Машина состояний игры и точка, через которую всё перезапускается.
///
/// Забег начинается заново БЕЗ перезагрузки сцены: пулы, префабы и материалы
/// остаются в памяти, поэтому «Заново» срабатывает мгновенно, а не с чёрным
/// экраном на пару секунд. Каждая система умеет сбрасывать себя сама
/// в методе ResetRun.
///
/// Куда вешать: на пустой GameObject "GameManager" в сцене.
/// В инспекторе: перетащить Player, ChunkSpawner, ObstacleSpawner,
/// ScoreManager и Main Camera в соответствующие поля.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ссылки на системы")]
    [SerializeField] private PlayerController player;
    [SerializeField] private ChunkSpawner chunkSpawner;
    [SerializeField] private ObstacleSpawner obstacleSpawner;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private PowerUpManager powerUpManager;
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private EffectManager effectManager;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameFeel gameFeel;

    [Header("Отладка")]
    [Tooltip("Игрок проходит сквозь препятствия. Нужно, чтобы тестировать генератор на длинных дистанциях.")]
    [SerializeField] private bool godMode = false;

    [Tooltip("Сразу начинать забег, не показывая меню. Удобно при отладке механики.")]
    [SerializeField] private bool skipMenu = false;

    public GameState State { get; private set; } = GameState.Menu;

    public bool IsRunning => State == GameState.Running;
    public bool GodMode => godMode;

    /// <summary>Дистанция, на которой закончился последний забег.</summary>
    public float LastRunDistance { get; private set; }

    public event Action<GameState> OnStateChanged;
    public event Action OnRunStarted;
    public event Action OnGameOver;

    private void Awake()
    {
        Instance = this;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (chunkSpawner == null) chunkSpawner = FindFirstObjectByType<ChunkSpawner>();
        if (obstacleSpawner == null) obstacleSpawner = FindFirstObjectByType<ObstacleSpawner>();
        if (scoreManager == null) scoreManager = GetComponent<ScoreManager>();
        if (powerUpManager == null) powerUpManager = GetComponent<PowerUpManager>();
        if (characterManager == null) characterManager = GetComponent<CharacterManager>();
        if (effectManager == null) effectManager = GetComponent<EffectManager>();
        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (gameFeel == null) gameFeel = GetComponent<GameFeel>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        ResetRun();

        if (skipMenu) StartRun();
        else SetState(GameState.Menu);
    }

    // ------------------------------------------------------------ переходы

    public void StartRun()
    {
        ResetRun();
        SetState(GameState.Running);
        OnRunStarted?.Invoke();
    }

    public void Pause()
    {
        if (State != GameState.Running) return;

        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;

        Time.timeScale = 1f;
        SetState(GameState.Running);
    }

    public void GameOver()
    {
        if (State != GameState.Running) return;

        LastRunDistance = player != null ? player.Distance : 0f;
        SetState(GameState.Dead);
        OnGameOver?.Invoke();
    }

    public void GoToMenu()
    {
        Time.timeScale = 1f;
        ResetRun();
        SetState(GameState.Menu);
    }

    private void SetState(GameState next)
    {
        State = next;
        OnStateChanged?.Invoke(next);
    }

    // --------------------------------------------------------------- сброс

    /// <summary>
    /// Возвращает мир в исходное состояние. Порядок важен: сначала игрок
    /// встаёт на нулевую отметку, потом под него заново раскладывается трасса.
    /// </summary>
    private void ResetRun()
    {
        if (obstacleSpawner != null) obstacleSpawner.ResetRun();
        if (powerUpManager != null) powerUpManager.ResetRun();
        if (effectManager != null) effectManager.ResetRun();

        // Персонажа сбрасываем ДО игрока: щит перезаряжается, а прибавка
        // к стартовой скорости должна быть выставлена раньше, чем player
        // прочитает её в своём ResetRun.
        if (characterManager != null)
        {
            characterManager.ResetRun();
            if (player != null) player.StartSpeedBonus = characterManager.StartSpeedBonus;
        }

        // Апгрейд «рывок на старте» просто начисляет фору в метрах:
        // счётчик стартует не с нуля, а значит и сложность сразу выше.
        if (player != null) player.ResetRun(UpgradeShop.HeadStartDistance);

        if (chunkSpawner != null) chunkSpawner.ResetRun();
        if (scoreManager != null) scoreManager.ResetRun();
        if (cameraFollow != null) cameraFollow.SnapToTarget();

        // Последним: снимает недоигравший хитстоп и обнуляет тряску.
        // Без этого рестарт сразу после смерти начинался бы в замедленном
        // времени и с трясущейся камерой.
        if (gameFeel != null) gameFeel.ResetRun();
    }
}
