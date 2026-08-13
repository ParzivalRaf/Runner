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
    [SerializeField] private GameObject charactersPanel;

    [Tooltip("Через сколько секунд после смерти появляется экран проигрыша. " +
             "Эта пауза нужна, чтобы успел отыграть наезд камеры на лицо. " +
             "Ноль — как было раньше, панель выскакивает мгновенно.")]
    [SerializeField] private float gameOverDelay = 1.3f;

    [Header("Меню")]
    [SerializeField] private Text menuBestText;
    [SerializeField] private Text menuCoinsText;
    [SerializeField] private Text menuCharacterText;
    [SerializeField] private Button playButton;
    [SerializeField] private Button charactersButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button settingsButton;

    [Header("HUD")]
    [SerializeField] private Text hudDistanceText;
    [SerializeField] private Text hudCoinsText;
    [Tooltip("Короткая подсказка только для поставленного начала первого забега.")]
    [SerializeField] private Text openingGuideText;

    [Tooltip("Индикатор щита Директора. Пустой и скрытый у остальных персонажей.")]
    [SerializeField] private Text shieldText;
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

    [Header("Персонажи")]
    [SerializeField] private Text charactersCoinsText;
    [SerializeField] private CharacterLobbyPreview characterLobby;
    [SerializeField] private Text charactersStatusText;
    [SerializeField] private Text charactersNameText;
    [SerializeField] private Text charactersAbilityText;
    [SerializeField] private Text charactersPhraseText;
    [SerializeField] private Text charactersCountText;
    [SerializeField] private Button charactersPrevButton;
    [SerializeField] private Button charactersNextButton;
    [SerializeField] private Button charactersActionButton;
    [SerializeField] private Text charactersActionLabel;
    [SerializeField] private Button charactersCloseButton;

    [Header("Ссылки")]
    [SerializeField] private PlayerController player;
    [SerializeField] private ScoreManager score;
    [SerializeField] private CharacterManager characters;

    private float _resetConfirmUntil;

    /// <summary>Кто сейчас показан в карусели. Не обязательно выбранный.</summary>
    private int _characterIndex;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (score == null) score = FindFirstObjectByType<ScoreManager>();
        if (characters == null) characters = FindFirstObjectByType<CharacterManager>();

        Bind(playButton, OnPlayPressed);
        Bind(pauseButton, OnPausePressed);
        Bind(resumeButton, OnResumePressed);
        Bind(pauseRestartButton, OnRestartPressed);
        Bind(pauseMenuButton, OnMenuPressed);
        Bind(gameOverRestartButton, OnRestartPressed);
        Bind(gameOverMenuButton, OnMenuPressed);

        Bind(charactersButton, OpenCharacters);
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

        Bind(charactersCloseButton, CloseOverlays);
        Bind(charactersPrevButton, ShowPreviousCharacter);
        Bind(charactersNextButton, ShowNextCharacter);
        Bind(charactersActionButton, BuyOrSelectCharacter);

        Bind(musicButton, ToggleMusic);
        Bind(soundButton, ToggleSound);
        Bind(vibrationButton, ToggleVibration);
        Bind(resetButton, ResetProgress);
    }

    /// <summary>
    /// Подписать кнопку и заодно дать ей звук нажатия — чтобы не вспоминать
    /// про него отдельно на каждой новой кнопке.
    ///
    /// Вызывается только из Awake, поэтому снимать старый обработчик не надо:
    /// дублей быть не может.
    /// </summary>
    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButton();
            action();
        });
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
        UpdateOpeningGuide();
        UpdateShieldIndicator();
    }

    // ------------------------------------------------------------ состояния

    private void HandleStateChanged(GameState state)
    {
        Show(menuPanel, state == GameState.Menu);
        Show(hudPanel, state == GameState.Running || state == GameState.Paused);
        Show(pausePanel, state == GameState.Paused);

        if (state != GameState.Menu) CloseOverlays();

        if (state == GameState.Menu)
        {
            // Витрина нужна только на экране выбора. В главном меню она не
            // тратит кадр впустую и не остаётся активной после забега.
            if (characterLobby != null) characterLobby.SetVisible(false);
            RefreshMenu();
        }
        if (state == GameState.Running)
        {
            UpdatePowerUpBars();
            UpdateOpeningGuide();
            UpdateShieldIndicator();
        }

        // Экран проигрыша появляется не сразу.
        //
        // Сразу — значит поверх наезда камеры на лицо, ровно в тот момент,
        // ради которого наезд и делался. Персонажа не видно, кнопка «Заново»
        // уже под пальцем, и игрок жмёт её не глядя.
        //
        // Пауза короткая: секунда с небольшим. Больше — начинает раздражать
        // при быстрых перезапусках, а перезапуск здесь мгновенный и этим ценен.
        if (state == GameState.Dead) ShowGameOverDelayed();
        else CancelGameOverDelay();
    }

    private Coroutine _gameOverRoutine;

    private void ShowGameOverDelayed()
    {
        CancelGameOverDelay();
        _gameOverRoutine = StartCoroutine(GameOverAfterPause());
    }

    private void CancelGameOverDelay()
    {
        if (_gameOverRoutine != null)
        {
            StopCoroutine(_gameOverRoutine);
            _gameOverRoutine = null;
        }

        Show(gameOverPanel, false);
    }

    private System.Collections.IEnumerator GameOverAfterPause()
    {
        // Нескалированное ожидание: в момент удара работает хитстоп,
        // обычное время почти стоит, и на нём пауза растянулась бы.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, gameOverDelay));

        Show(gameOverPanel, true);
        RefreshGameOver();

        _gameOverRoutine = null;
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

        if (menuCharacterText != null)
        {
            CharacterData selected = characters != null ? characters.Selected : null;

            menuCharacterText.text = selected != null ? $"бежит: {selected.DisplayName}" : "";
            menuCharacterText.color = selected != null ? selected.Tint : Color.white;
        }
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

    // -------------------------------------------------------- ясность забега

    /// <summary>
    /// Подсказки живут только в первой короткой постановочной последовательности.
    /// Это не учебник с остановкой игры: текст просто подтверждает то, что
    /// игрок уже видит впереди, и уходит прежде, чем начнётся настоящий раннер.
    /// </summary>
    private void UpdateOpeningGuide()
    {
        if (openingGuideText == null) return;

        float distance = player != null ? player.Distance : 999f;
        string message;

        if (distance < 22f) message = "СОБИРАЙ МОНЕТЫ";
        else if (distance < 52f) message = "ОБЪЕЗЖАЙ КРАСНОЕ";
        else if (distance < 80f) message = "ПРЫГАЙ ЧЕРЕЗ ЖЁЛТОЕ";
        else if (distance < 98f) message = "БЕРИ БОНУС";
        else if (distance < 150f) message = "СЛЕДУЙ ЗА МОНЕТАМИ НА КРЫШУ";
        else message = string.Empty;

        bool visible = !string.IsNullOrEmpty(message);
        if (openingGuideText.gameObject.activeSelf != visible)
            openingGuideText.gameObject.SetActive(visible);

        if (visible) openingGuideText.text = message;
    }

    /// <summary>
    /// Спасение щитом должно быть понятным до удара. Иначе способность
    /// Директора выглядит как случайный баг, а не как причина выбрать героя.
    /// </summary>
    private void UpdateShieldIndicator()
    {
        if (shieldText == null) return;

        int charges = characters != null ? characters.ShieldCharges : 0;
        bool visible = charges > 0;

        if (shieldText.gameObject.activeSelf != visible)
            shieldText.gameObject.SetActive(visible);

        if (visible) shieldText.text = $"ЩИТ  ×{charges}";
    }

    // -------------------------------------------------------------- магазин

    private void OpenShop()
    {
        // Меню прячем целиком: панели полупрозрачные, и иначе кнопки меню
        // просвечивают сквозь магазин.
        Show(menuPanel, false);
        Show(shopPanel, true);
        Show(settingsPanel, false);
        Show(charactersPanel, false);
        if (characterLobby != null) characterLobby.SetVisible(false);
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

    // ------------------------------------------------------------- персонажи

    private void OpenCharacters()
    {
        Show(menuPanel, false);
        Show(charactersPanel, true);
        Show(shopPanel, false);
        Show(settingsPanel, false);

        // Открываем карусель на том, кем сейчас бежим, а не на первом герое.
        if (characters != null && characters.Database != null && characters.Selected != null)
        {
            int index = characters.Database.IndexOf(characters.Selected.Id);
            if (index >= 0) _characterIndex = index;
        }

        if (characterLobby != null) characterLobby.SetVisible(true);
        RefreshCharacters();
    }

    private void ShowPreviousCharacter() => StepCharacter(-1);
    private void ShowNextCharacter() => StepCharacter(+1);

    /// <summary>Карусель закольцована: с последнего листаем на первого.</summary>
    private void StepCharacter(int delta)
    {
        int count = characters != null && characters.Database != null
            ? characters.Database.Count
            : 0;

        if (count <= 0) return;

        _characterIndex = ((_characterIndex + delta) % count + count) % count;
        RefreshCharacters();
    }

    private CharacterData CurrentCharacter =>
        characters != null && characters.Database != null
            ? characters.Database.Get(_characterIndex)
            : null;

    private void RefreshCharacters()
    {
        if (charactersCoinsText != null)
            charactersCoinsText.text = $"Монет: {SaveSystem.Data.totalCoins}";

        int count = characters != null && characters.Database != null
            ? characters.Database.Count
            : 0;

        if (charactersCountText != null)
            charactersCountText.text = count > 0 ? $"{_characterIndex + 1} / {count}" : "";

        CharacterData character = CurrentCharacter;

        if (character == null)
        {
            // Список пуст — не оставляем интерфейс в непонятном состоянии.
            if (charactersNameText != null) charactersNameText.text = "нет персонажей";
            if (charactersStatusText != null) charactersStatusText.text = "СПИСОК ПУСТ";
            if (charactersAbilityText != null) charactersAbilityText.text = "";
            if (charactersPhraseText != null) charactersPhraseText.text = "";
            if (charactersActionButton != null) charactersActionButton.interactable = false;
            if (charactersActionLabel != null) charactersActionLabel.text = "—";
            if (characterLobby != null) characterLobby.ShowCharacter(null, false);
            return;
        }

        bool unlocked = characters.IsUnlocked(character);
        bool isSelected = characters.Selected != null && characters.Selected.Id == character.Id;

        if (charactersStatusText != null)
        {
            charactersStatusText.text = isSelected ? "ВЫБРАН ДЛЯ ЗАБЕГА"
                : unlocked ? "ГОТОВ К ЗАБЕГУ"
                : $"ЗАКРЫТ · {character.Price} МОНЕТ";
            charactersStatusText.color = isSelected ? new Color(0.55f, 0.95f, 0.72f)
                : unlocked ? new Color(0.7f, 0.78f, 0.94f)
                : new Color(0.95f, 0.66f, 0.34f);
        }

        if (charactersNameText != null)
        {
            charactersNameText.text = unlocked ? character.DisplayName : "???";
            charactersNameText.color = unlocked
                ? character.Tint
                : new Color(0.62f, 0.62f, 0.7f);
        }

        if (charactersAbilityText != null)
            charactersAbilityText.text = character.AbilityDescription;

        if (charactersPhraseText != null)
            charactersPhraseText.text = unlocked ? character.CatchPhrase : "";

        if (charactersActionLabel != null)
        {
            if (isSelected) charactersActionLabel.text = "ВЫБРАН";
            else if (unlocked) charactersActionLabel.text = "ВЫБРАТЬ";
            else charactersActionLabel.text = $"КУПИТЬ  {character.Price}";
        }

        if (charactersActionButton != null)
            charactersActionButton.interactable =
                !isSelected && (unlocked || characters.CanBuy(character));

        // Витрина живёт отдельно от UI: она показывает настоящую 3D-модель
        // открытого героя и не выдаёт внешность закрытого до покупки.
        if (characterLobby != null) characterLobby.ShowCharacter(character, unlocked);
    }

    private void BuyOrSelectCharacter()
    {
        CharacterData character = CurrentCharacter;
        if (character == null || characters == null) return;

        if (characters.IsUnlocked(character)) characters.Select(character);
        else characters.Buy(character);

        RefreshCharacters();
        RefreshMenu();
    }

    // ------------------------------------------------------------- настройки

    private void OpenSettings()
    {
        Show(menuPanel, false);
        Show(settingsPanel, true);
        Show(shopPanel, false);
        Show(charactersPanel, false);
        if (characterLobby != null) characterLobby.SetVisible(false);
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

        if (AudioManager.Instance != null) AudioManager.Instance.ApplySettings();

        RefreshSettings();
    }

    private void ToggleSound()
    {
        SaveSystem.Data.soundEnabled = !SaveSystem.Data.soundEnabled;
        SaveSystem.Save();

        if (AudioManager.Instance != null) AudioManager.Instance.ApplySettings();

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

        // Список открытых персонажей обнулился — выбранный мог стать закрытым.
        if (characters != null) characters.ReloadFromSave();
        _characterIndex = 0;

        // Сброс вернул музыку и звук во «включено» — источник об этом не знает.
        if (AudioManager.Instance != null) AudioManager.Instance.ApplySettings();

        RefreshSettings();
        RefreshMenu();
        RefreshShop();
        RefreshCharacters();
    }

    private void CloseOverlays()
    {
        Show(shopPanel, false);
        Show(settingsPanel, false);
        Show(charactersPanel, false);
        _resetConfirmUntil = 0f;

        bool inMenu = GameManager.Instance == null ||
                      GameManager.Instance.State == GameState.Menu;

        if (inMenu)
        {
            Show(menuPanel, true);
            if (characterLobby != null) characterLobby.SetVisible(false);
            RefreshMenu();
        }
        else if (characterLobby != null)
        {
            characterLobby.SetVisible(false);
        }
    }

    // -------------------------------------------------------------- кнопки

    public void OnPlayPressed() => GameManager.Instance?.StartRun();
    public void OnPausePressed() => GameManager.Instance?.Pause();
    public void OnResumePressed() => GameManager.Instance?.Resume();
    public void OnRestartPressed() => GameManager.Instance?.StartRun();
    public void OnMenuPressed() => GameManager.Instance?.GoToMenu();
}
