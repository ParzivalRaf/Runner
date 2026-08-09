using System;
using UnityEngine;

/// <summary>
/// Одна точка входа для «сочности»: тряска камеры, толчок FOV и хитстоп.
///
/// Зачем прослойка, а не вызывать CameraFollow напрямую: удар — это всегда
/// несколько вещей сразу (тряхнуть, дёрнуть FOV, подморозить время).
/// Держать их согласованными проще, когда сила задаётся одним числом
/// в одном месте, а не собирается заново на каждом месте вызова.
///
/// Куда вешать: на объект GameManager.
/// </summary>
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Хитстоп")]
    [Tooltip("Во сколько раз замедляется время в момент удара. 0 не ставим: " +
             "полная остановка выглядит как зависание игры, а не как удар.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float hitStopScale = 0.06f;

    [Tooltip("Сколько РЕАЛЬНЫХ секунд длится заморозка на столкновении.")]
    [SerializeField] private float crashHitStop = 0.11f;

    [Header("Сила толчков")]
    [SerializeField] private float crashShake = 1f;
    [SerializeField] private float nearMissShake = 0.30f;
    [SerializeField] private float powerUpShake = 0.22f;
    [SerializeField] private float landShake = 0.10f;

    private bool _hitStopActive;
    private float _hitStopEndsAt;

    private void Awake()
    {
        Instance = this;

        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------- события

    /// <summary>
    /// «Оглянись». Персонаж бежит спиной к камере, поэтому лица его в забеге
    /// не видно вообще никогда. Этот сигнал разворачивает голову к игроку
    /// на секунду — ровно в те моменты, когда он и так смотрит на экран.
    ///
    /// Событие статическое: модель персонажа создаётся и уничтожается заново
    /// при каждой смене героя, и подписываться ей проще на общий сигнал,
    /// чем каждый раз искать этот объект в сцене.
    ///
    /// Слушателей может не быть вовсе — если у персонажа нет модели
    /// или разворот выключен галочкой. Это нормально.
    /// </summary>
    /// Число — сколько секунд держать голову обёрнутой.
    public static event Action<float> OnGlanceBack;

    private static void GlanceBack(float hold) => OnGlanceBack?.Invoke(hold);

    /// <summary>Игрок врезался. Самый сильный удар в игре.</summary>
    public void Crash()
    {
        Shake(crashShake);
        PunchFov(-1.2f);          // внутрь: кадр сжимается, как от удара под дых
        HitStop(crashHitStop);

        // Держим долго: сразу после удара камера наезжает на лицо,
        // и голова должна оставаться повёрнутой всё это время.
        GlanceBack(3f);
    }

    /// <summary>Игрок прошёл впритирку мимо препятствия.</summary>
    public void NearMiss()
    {
        Shake(nearMissShake);
        PunchFov(0.5f);
        GlanceBack(0.55f);
    }

    /// <summary>Подобран бонус.</summary>
    public void PowerUp()
    {
        Shake(powerUpShake);
        PunchFov(1f);             // наружу: кадр распахивается
    }

    /// <summary>Приземление после прыжка. Слабое, но копится в ощущение веса.</summary>
    public void Land() => Shake(landShake);

    // -------------------------------------------------------------- basics

    public void Shake(float amount)
    {
        if (cameraFollow != null) cameraFollow.AddShake(amount);
    }

    public void PunchFov(float amount)
    {
        if (cameraFollow != null) cameraFollow.PunchFov(amount);
    }

    /// <summary>
    /// Подморозить время на заданное число реальных секунд.
    ///
    /// Отсчёт идёт по unscaledTime: масштаб времени как раз и сломан,
    /// обычный Time.time во время хитстопа почти не идёт, и заморозка
    /// растянулась бы навсегда.
    /// </summary>
    public void HitStop(float realSeconds)
    {
        if (realSeconds <= 0f) return;

        // Во время паузы время уже остановлено осмысленно — не мешаем.
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Paused) return;

        _hitStopActive = true;
        _hitStopEndsAt = Time.unscaledTime + realSeconds;

        Time.timeScale = hitStopScale;
    }

    private void Update()
    {
        if (!_hitStopActive) return;

        // Игрок успел поставить паузу прямо в момент удара. Пауза главнее:
        // отпускаем хитстоп, но НЕ трогаем timeScale, иначе снимем паузу.
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Paused)
        {
            _hitStopActive = false;
            return;
        }

        if (Time.unscaledTime < _hitStopEndsAt) return;

        _hitStopActive = false;
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Снять всё немедленно. Вызывает GameManager перед новым забегом:
    /// иначе рестарт сразу после смерти начнётся в замедленном времени.
    /// </summary>
    public void ResetRun()
    {
        if (_hitStopActive)
        {
            _hitStopActive = false;
            Time.timeScale = 1f;
        }

        if (cameraFollow != null)
        {
            // SnapToTarget обнуляет и тряску, и толчок FOV.
            cameraFollow.SnapToTarget();
        }
    }
}
