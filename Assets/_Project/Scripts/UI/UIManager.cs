using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Включает нужную панель под текущее состояние игры и обновляет тексты.
/// Кнопки подписываются на свои методы в Awake — в инспекторе достаточно
/// перетащить сами кнопки, ничего настраивать в OnClick не надо.
///
/// Магазин и настройки живут «поверх» меню и не входят в GameState:
/// это просто две панели, которые можно открыть и закрыть.
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
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Меню")]
    [SerializeField] private Text menuBestText;
    [SerializeField] private Text menuCoinsText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button settingsButton;

    [Header("HUD")]
    [SerializeField] private Text hudDistanceText;
    [SerializeField] private Text hudCoinsText;
    [SerializeField] private Button pauseButton;

    [Tooltip("Корни полосок бонусов, по одному на PowerUpType.")]
    [SerializeField] private GameObject[] powerUpBarRoots = new GameObject[4];

    [Tooltip("Заполняющиеся части полосок. Масштабируются по X.")]
    [SerializeField] private RectTransform[] powerUpBarFills = new RectTransform[4];

    [Header("Пауза")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseMenuButton;

    [Header("Game Over")]
    [SerializeField] private Text gameOverTitleText;
    [SerializeField] private Text gameOverStatsText;
    [SerializeField] private Button gameOverRestartButton;
    [SerializeField] private Button gameOverMenuButton;

    [Header("Магазин")]
    [SerializeField] private Text shopCoinsText;
    [SerializeField] private Text[] shopNameTexts = new Text[3];
    [SerializeField] private Text[] shopEffectTexts = new Text[3];
    [SerializeField] private Button[] shopBuyButtons = new Button[3];
    [SerializeField] private Text[] shopBuyLabels = new Text[3];
    [SerializeField] private Button shopCloseButton;

    [Header("Настройки")]
    [SerializeField] private Button musicButton;
    [SerializeField] private Text musicLabel;
    [SerializeField] private Button soundButton;
    [SerializeField] private Text soundLabel;
    [SerializeField] private Button vibrationButton;
    [SerializeField] private Text vibrationLabel;
    [SerializeField] private Button resetButton;
    [SerializeField] private Text resetLabel;
    [SerializeField] private Button settingsCloseButton;

    [Header("Ссылки")]
    [SerializeField] private PlayerController player;
    [SerializeField] private ScoreManager score;

    private float _resetConfirmUntil;

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

        Bind(shopButton, OpenShop);
        Bind(shopCloseButton, CloseOverlays);
        Bind(settingsButton, OpenSettings);
        Bind(settingsCloseButton, CloseOverlays);

        for (int i = 0; i < shopBuyButtons.Length; i++)
        {
            int index = i;   // копия для замыкания
            if (shopBuyButtons[i] == null) continue;

            shopBuyButtons[i].onClick.AddListener(() => BuyUpgrade(index));
        }

        Bind(musicButton, ToggleMusic);
        Bind(soundButton, ToggleSound);
        Bind(vibrationButton, ToggleVibration);
        Bind(resetButton, ResetProgress);
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

        CloseOverlays();
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

        UpdatePowerUpBars();
    }

    // ------------------------------------------------------------ состояния

    private void HandleStateChanged(GameState state)
    {
        Show(menuPanel, state == GameState.Menu);
        Show(hudPanel, state == GameState.Running || state == GameState.Paused);
        Show(pausePanel, state == GameState.Paused);
        Show(gameOverPanel, state == GameState.Dead);

        if (state != GameState.Menu) CloseOverlays();

        if (state == GameState.Menu) RefreshMenu();
        if (state == GameState.Dead) RefreshGameOver();
        if (state == GameState.Running) UpdatePowerUpBars();
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

    // ---------------------------------------------------------- полоски бонусов

    private void UpdatePowerUpBars()
    {
        PowerUpManager manager = PowerUpManager.Instance;

        for (int i = 0; i < powerUpBarRoots.Length; i++)
        {
            GameObject root = powerUpBarRoots[i];
            if (root == null) continue;

            bool active = manager != null && manager.IsActive((PowerUpType)i);
            if (root.activeSelf != active) root.SetActive(active);

            if (!active) continue;

            RectTransform fill = i < powerUpBarFills.Length ? powerUpBarFills[i] : null;
            if (fill == null) continue;

            float fraction = manager.Fraction((PowerUpType)i);
            fill.localScale = new Vector3(fraction, 1f, 1f);
        }
    }

    // -------------------------------------------------------------- магазин

    private void OpenShop()
    {
        // Меню прячем целиком: панели полупрозрачные, и иначе кнопки меню
        // просвечивают сквозь магазин.
        Show(menuPanel, false);
        Show(shopPanel, true);
        Show(settingsPanel, false);
        RefreshShop();
    }

    private void RefreshShop()
    {
        if (shopCoinsText != null) shopCoinsText.text = $"Монет: {SaveSystem.Data.totalCoins}";

        UpgradeKind[] kinds = UpgradeShop.All;

        for (int i = 0; i < kinds.Length && i < shopNameTexts.Length; i++)
        {
            UpgradeKind kind = kinds[i];
            int level = UpgradeShop.GetLevel(kind);

            if (shopNameTexts[i] != null)
                shopNameTexts[i].text = $"{UpgradeShop.GetName(kind)}   {level}/{UpgradeShop.MaxLevel}";

            if (shopEffectTexts[i] != null)
                shopEffectTexts[i].text = UpgradeShop.GetEffect(kind);

            int price = UpgradeShop.GetPrice(kind);
            bool maxed = price < 0;

            if (shopBuyLabels[i] != null)
                shopBuyLabels[i].text = maxed ? "МАКС" : price.ToString();

            if (shopBuyButtons[i] != null)
                shopBuyButtons[i].interactable = !maxed && UpgradeShop.CanBuy(kind);
        }
    }

    private void BuyUpgrade(int index)
    {
        UpgradeKind[] kinds = UpgradeShop.All;
        if (index < 0 || index >= kinds.Length) return;

        UpgradeShop.Buy(kinds[index]);
        RefreshShop();
        RefreshMenu();
    }

    // ------------------------------------------------------------- настройки

    private void OpenSettings()
    {
        Show(menuPanel, false);
        Show(settingsPanel, true);
        Show(shopPanel, false);
        RefreshSettings();
    }

    private void RefreshSettings()
    {
        SaveData data = SaveSystem.Data;

        if (musicLabel != null) musicLabel.text = $"Музыка: {OnOff(data.musicEnabled)}";
        if (soundLabel != null) soundLabel.text = $"Звуки: {OnOff(data.soundEnabled)}";
        if (vibrationLabel != null) vibrationLabel.text = $"Вибрация: {OnOff(data.vibrationEnabled)}";

        if (resetLabel != null)
            resetLabel.text = Time.unscaledTime < _resetConfirmUntil
                ? "ТОЧНО? НАЖМИ ЕЩЁ РАЗ"
                : "СБРОСИТЬ ПРОГРЕСС";
    }

    private static string OnOff(bool value) => value ? "вкл" : "выкл";

    private void ToggleMusic()
    {
        SaveSystem.Data.musicEnabled = !SaveSystem.Data.musicEnabled;
        SaveSystem.Save();
        RefreshSettings();
    }

    private void ToggleSound()
    {
        SaveSystem.Data.soundEnabled = !SaveSystem.Data.soundEnabled;
        SaveSystem.Save();
        RefreshSettings();
    }

    private void ToggleVibration()
    {
        SaveSystem.Data.vibrationEnabled = !SaveSystem.Data.vibrationEnabled;
        SaveSystem.Save();
        RefreshSettings();
    }

    /// <summary>Сброс в два нажатия: случайно потерять прогресс не получится.</summary>
    private void ResetProgress()
    {
        if (Time.unscaledTime >= _resetConfirmUntil)
        {
            _resetConfirmUntil = Time.unscaledTime + 4f;
            RefreshSettings();
            return;
        }

        _resetConfirmUntil = 0f;
        SaveSystem.ResetProgress();

        RefreshSettings();
        RefreshMenu();
        RefreshShop();
    }

    private void CloseOverlays()
    {
        Show(shopPanel, false);
        Show(settingsPanel, false);
        _resetConfirmUntil = 0f;

        bool inMenu = GameManager.Instance == null ||
                      GameManager.Instance.State == GameState.Menu;

        if (inMenu)
        {
            Show(menuPanel, true);
            RefreshMenu();
        }
    }

    // -------------------------------------------------------------- кнопки

    public void OnPlayPressed() => GameManager.Instance?.StartRun();
    public void OnPausePressed() => GameManager.Instance?.Pause();
    public void OnResumePressed() => GameManager.Instance?.Resume();
    public void OnRestartPressed() => GameManager.Instance?.StartRun();
    public void OnMenuPressed() => GameManager.Instance?.GoToMenu();
}
