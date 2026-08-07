using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Включает нужную панель под текущее состояние игры и обновляет тексты.
/// Кнопки подписываются на свои методы в Awake — в инспекторе достаточно
/// перетащить сами кнопки, ничего настраивать в OnClick не надо.
///
/// Куда вешать: на объект "UI" (тот же, где Canvas).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Меню")]
    [SerializeField] private Text menuBestText;
    [SerializeField] private Text menuCoinsText;
    [SerializeField] private Button playButton;

    [Header("HUD")]
    [SerializeField] private Text hudDistanceText;
    [SerializeField] private Text hudCoinsText;
    [SerializeField] private Button pauseButton;

    [Header("Пауза")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseMenuButton;

    [Header("Game Over")]
    [SerializeField] private Text gameOverTitleText;
    [SerializeField] private Text gameOverStatsText;
    [SerializeField] private Button gameOverRestartButton;
    [SerializeField] private Button gameOverMenuButton;

    [Header("Ссылки")]
    [SerializeField] private PlayerController player;
    [SerializeField] private ScoreManager score;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (score == null) score = FindFirstObjectByType<ScoreManager>();

        Bind(playButton, OnPlayPressed);
        Bind(pauseButton, OnPausePressed);
        Bind(resumeButton, OnResumePressed);
        Bind(pauseRestartButton, OnRestartPressed);
        Bind(pauseMenuButton, OnMenuPressed);
        Bind(gameOverRestartButton, OnRestartPressed);
        Bind(gameOverMenuButton, OnMenuPressed);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            HandleStateChanged(GameManager.Instance.State);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameState.Running) return;

        if (hudDistanceText != null && player != null)
            hudDistanceText.text = $"{player.Distance:F0} м";

        if (hudCoinsText != null && score != null)
            hudCoinsText.text = score.CoinsThisRun.ToString();
    }

    // ------------------------------------------------------------ состояния

    private void HandleStateChanged(GameState state)
    {
        Show(menuPanel, state == GameState.Menu);
        Show(hudPanel, state == GameState.Running || state == GameState.Paused);
        Show(pausePanel, state == GameState.Paused);
        Show(gameOverPanel, state == GameState.Dead);

        if (state == GameState.Menu) RefreshMenu();
        if (state == GameState.Dead) RefreshGameOver();
    }

    private static void Show(GameObject panel, bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    private void RefreshMenu()
    {
        SaveData data = SaveSystem.Data;

        if (menuBestText != null) menuBestText.text = $"Рекорд: {data.bestDistance:F0} м";
        if (menuCoinsText != null) menuCoinsText.text = $"Монет: {data.totalCoins}";
    }

    private void RefreshGameOver()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return;

        bool record = score != null && score.IsNewDistanceRecord;

        if (gameOverTitleText != null)
            gameOverTitleText.text = record ? "НОВЫЙ РЕКОРД!" : "ВРЕЗАЛСЯ";

        if (gameOverStatsText != null)
        {
            int coins = score != null ? score.CoinsThisRun : 0;
            gameOverStatsText.text = $"{game.LastRunDistance:F0} м\nмонет: {coins}";
        }
    }

    // -------------------------------------------------------------- кнопки

    public void OnPlayPressed() => GameManager.Instance?.StartRun();
    public void OnPausePressed() => GameManager.Instance?.Pause();
    public void OnResumePressed() => GameManager.Instance?.Resume();
    public void OnRestartPressed() => GameManager.Instance?.StartRun();
    public void OnMenuPressed() => GameManager.Instance?.GoToMenu();
}
