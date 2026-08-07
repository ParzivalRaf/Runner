using UnityEngine;

/// <summary>
/// Считает монеты за забег и записывает результат в сейв, когда игрок разбился.
///
/// Куда вешать: на объект GameManager.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private PlayerController player;

    public int CoinsThisRun { get; private set; }
    public bool IsNewDistanceRecord { get; private set; }

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
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning) return;

        CoinsThisRun += amount + CharacterBonusFor(amount);
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
