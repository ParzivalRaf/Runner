using System;
using UnityEngine;

/// <summary>
/// Считает монеты за забег и записывает результат в сейв, когда игрок разбился.
/// Здесь же живёт комбо — общий счётчик подряд идущих удачных действий.
///
/// Куда вешать: на объект GameManager.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private PlayerController player;

    [Header("Комбо")]
    [Tooltip("Сколько секунд без монет и уклонений сбрасывают серию.")]
    [SerializeField] private float comboWindow = 1.6f;

    [Tooltip("Начиная с какой длины серия вообще показывается игроку.")]
    [SerializeField] private int comboShowFrom = 3;

    public int CoinsThisRun { get; private set; }
    public bool IsNewDistanceRecord { get; private set; }

    /// <summary>Текущая серия: монеты и проходы впритирку подряд.</summary>
    public int Combo { get; private set; }

    /// <summary>Самая длинная серия за этот забег.</summary>
    public int BestCombo { get; private set; }

    public int ComboShowFrom => comboShowFrom;

    /// <summary>
    /// Серия изменилась. Ноль означает, что её сбросили.
    /// На это подписан ComboDisplay на HUD.
    /// </summary>
    public event Action<int> OnComboChanged;

    private float _lastComboTime = -99f;

    public float BestDistance => SaveSystem.Data.bestDistance;
    public int TotalCoins => SaveSystem.Data.totalCoins;

    private bool _resultSaved;

    // Бонус персонажа к монетам дробный (+10%), а монеты целые. Копим остаток
    // здесь и отдаём его игроку, как только он дорастает до целой монеты —
    // иначе при номинале в 1 монету прибавка всегда округлялась бы в ноль.
    private float _bonusCarry;

    private void Awake()
    {
        Instance = this;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= HandleGameOver;
    }

    private void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver += HandleGameOver;
    }

    /// <summary>Обнулить счётчики перед новым забегом.</summary>
    public void ResetRun()
    {
        CoinsThisRun = 0;
        IsNewDistanceRecord = false;
        _resultSaved = false;
        _bonusCarry = 0f;

        Combo = 0;
        BestCombo = 0;
        _lastComboTime = -99f;
        OnComboChanged?.Invoke(0);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning) return;

        BumpCombo();
        Credit(amount);
    }

    /// <summary>
    /// Игрок прошёл впритирку мимо препятствия. Даём монету: без неё комбо
    /// оставалось бы красивой цифрой, за которой ничего не стоит, и уклоняться
    /// было бы незачем — проще держаться пустой полосы.
    ///
    /// Одна монета, а не больше, намеренно: уклонения должны дополнять
    /// сбор монет, а не заменять его.
    /// </summary>
    public void RegisterNearMiss()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning) return;

        BumpCombo();
        Credit(1);
    }

    /// <summary>Начислить монеты, не трогая серию.</summary>
    private void Credit(int amount)
    {
        CoinsThisRun += amount + CharacterBonusFor(amount);
    }

    private void BumpCombo()
    {
        // Окно считаем по unscaledTime: во время хитстопа обычное время
        // почти стоит, и серия «висела» бы дольше, чем задумано.
        if (Time.unscaledTime - _lastComboTime > comboWindow) Combo = 1;
        else Combo++;

        _lastComboTime = Time.unscaledTime;

        if (Combo > BestCombo) BestCombo = Combo;

        OnComboChanged?.Invoke(Combo);
    }

    private void Update()
    {
        if (Combo == 0) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning) return;
        if (Time.unscaledTime - _lastComboTime <= comboWindow) return;

        Combo = 0;
        OnComboChanged?.Invoke(0);
    }

    /// <summary>Целая часть прибавки персонажа, с переносом остатка на следующий раз.</summary>
    private int CharacterBonusFor(int amount)
    {
        float rate = CharacterManager.Instance != null
            ? CharacterManager.Instance.CoinBonusRate
            : 0f;

        if (rate <= 0f) return 0;

        _bonusCarry += amount * rate;

        int whole = Mathf.FloorToInt(_bonusCarry);
        _bonusCarry -= whole;

        return whole;
    }

    private void HandleGameOver()
    {
        if (_resultSaved) return;
        _resultSaved = true;

        SaveData data = SaveSystem.Data;
        float distance = player != null ? player.Distance : 0f;

        IsNewDistanceRecord = distance > data.bestDistance;
        if (IsNewDistanceRecord) data.bestDistance = distance;

        if (CoinsThisRun > data.bestCoinsInRun) data.bestCoinsInRun = CoinsThisRun;

        data.totalCoins += CoinsThisRun;
        data.runsPlayed++;

        SaveSystem.Save();
    }

    // Телефон могут свернуть в любой момент — на Android это единственный
    // надёжный шанс успеть записать данные.
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveSystem.Save();
    }

    private void OnApplicationQuit() => SaveSystem.Save();
}
