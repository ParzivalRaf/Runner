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
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning) return;

        CoinsThisRun += amount;
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
