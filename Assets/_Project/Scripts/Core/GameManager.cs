using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Состояние забега: бежим или врезались. Пока минимально — на M6 сюда
/// добавятся пауза, меню и нормальный экран Game Over.
///
/// Куда вешать: на пустой GameObject "GameManager" в сцене.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum State { Running, Dead }

    public static GameManager Instance { get; private set; }

    [Tooltip("Сколько секунд после смерти игнорировать нажатия, чтобы не рестартнуть случайно.")]
    [SerializeField] private float restartInputDelay = 0.8f;

    [Header("Отладка")]
    [Tooltip("Игрок проходит сквозь препятствия. Нужно, чтобы тестировать генератор на длинных дистанциях.")]
    [SerializeField] private bool godMode = false;

    /// <summary>Режим неуязвимости. В релизной сборке всегда выключен.</summary>
    public bool GodMode => godMode;

    public State CurrentState { get; private set; } = State.Running;
    public bool IsRunning => CurrentState == State.Running;

    /// <summary>Дистанция, на которой закончился забег.</summary>
    public float LastRunDistance { get; private set; }

    public event Action OnGameOver;

    private float _deathTime;

    private void Awake()
    {
        // Сцена перезагружается целиком, поэтому DontDestroyOnLoad здесь не нужен.
        Instance = this;
        CurrentState = State.Running;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void GameOver()
    {
        if (CurrentState == State.Dead) return;

        CurrentState = State.Dead;
        _deathTime = Time.unscaledTime;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null) LastRunDistance = player.Distance;

        OnGameOver?.Invoke();
    }

    public void Restart()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void Update()
    {
        if (CurrentState != State.Dead) return;
        if (Time.unscaledTime - _deathTime < restartInputDelay) return;

        if (AnyInputThisFrame()) Restart();
    }

    /// <summary>
    /// Именно перечисленные клавиши, а не Keyboard.anyKey: anyKey в редакторе
    /// ловит служебные нажатия и перезапускает забег сам по себе.
    /// </summary>
    private static bool AnyInputThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.enterKey.wasPressedThisFrame ||
             keyboard.rKey.wasPressedThisFrame))
            return true;

        Pointer pointer = Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame) return true;

        return false;
    }
}
