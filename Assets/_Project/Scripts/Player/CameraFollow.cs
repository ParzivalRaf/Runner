using UnityEngine;

/// <summary>
/// Камера от третьего лица. По Z едет ровно за игроком (иначе на большой
/// скорости он просто уедет из кадра), а по X и Y догоняет со сглаживанием —
/// смена полосы выглядит мягко и не укачивает.
///
/// Умеет трястись и дёргать FOV: этим пользуются столкновения, near-miss
/// и подбор бонусов. Толчки запрашиваются снаружи через AddShake и PunchFov.
///
/// Куда вешать: на Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Цель")]
    [SerializeField] private Transform target;

    [Header("Положение")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -6f);

    [Tooltip("Наклон камеры вниз, градусы.")]
    [SerializeField] private float pitch = 15f;

    [Header("Сглаживание")]
    [Tooltip("Насколько мягко камера догоняет игрока по горизонтали (секунды).")]
    [SerializeField] private float horizontalSmoothTime = 0.15f;

    [Tooltip("Сглаживание по вертикали В ПОЛЁТЕ. Меньше — жёстче реакция " +
             "на прыжок.")]
    [SerializeField] private float verticalSmoothTime = 0.12f;

    [Tooltip("Сглаживание по вертикали, когда игрок стоит на поверхности.\n\n" +
             "Отдельное число появилось из-за пандусов. Прыжок — это действие " +
             "игрока, и его приятно смягчить. Въезд на крышу — это рельеф: " +
             "игрок поднимается на 2.6 примерно за треть секунды, и мягкая " +
             "камера всё это время висит ниже него и потом догоняет рывком. " +
             "Ощущается как подтормаживание, хотя кадры не падают.\n\n" +
             "Поэтому по земле и по крыше камера едет почти жёстко, " +
             "а мягкость остаётся только на прыжки.")]
    [SerializeField] private float groundedVerticalSmoothTime = 0.05f;

    [Header("Field of View")]
    // Диапазон расширен с 60–68 до 58–74. Раньше разница почти не читалась:
    // восемь градусов на весь разгон — это ничто. Растянутый FOV на скорости —
    // самый дешёвый способ дать ощущение скорости, дешевле любых партиклов.
    [SerializeField] private float baseFov = 58f;
    [SerializeField] private float maxFov = 74f;

    [Tooltip("Скорость игрока, при которой FOV ещё минимальный.")]
    [SerializeField] private float fovMinAtSpeed = 14f;

    [Tooltip("Скорость игрока, при которой FOV достигает максимума.")]
    [SerializeField] private float fovMaxAtSpeed = 24f;

    [SerializeField] private float fovSmoothTime = 0.5f;

    [Tooltip("На сколько градусов FOV дёргается от толчка силой 1. Возвращается сам.")]
    [SerializeField] private float fovPunchScale = 6f;

    [Header("Наезд на лицо после смерти")]
    // Забег кончается за долю секунды, и до этого лицо персонажа мелькало
    // только на миг. Поэтому после столкновения камера подъезжает к голове
    // и застывает: игрок наконец видит, кем он играл, и на кого именно
    // он налетел лбом. Голова в этот момент уже развёрнута к камере.
    [Tooltip("Выключи, если наезд мешает быстро перезапускаться.")]
    [SerializeField] private bool deathCam = true;

    [Tooltip("На каком расстоянии позади головы встаёт камера.")]
    [SerializeField] private float deathCamDistance = 2.4f;

    [Tooltip("Высота лица над полом. У фигурки голова примерно на 1.62.")]
    [SerializeField] private float deathCamFaceHeight = 1.62f;

    [Tooltip("FOV в конце наезда. Меньше — сильнее приближение.")]
    [SerializeField] private float deathCamFov = 40f;

    [Tooltip("Пауза перед началом наезда. Даёт хитстопу и тряске отыграть " +
             "удар, прежде чем камера поедет.")]
    [SerializeField] private float deathCamDelay = 0.12f;

    [Tooltip("За сколько секунд камера доезжает.")]
    [SerializeField] private float deathCamMoveTime = 0.40f;

    [Header("Тряска")]
    [Tooltip("Максимальный сдвиг камеры при полной тряске, юниты.")]
    [SerializeField] private float shakePositionAmount = 0.45f;

    [Tooltip("Максимальный поворот камеры при полной тряске, градусы.")]
    [SerializeField] private float shakeRotationAmount = 2.2f;

    [Tooltip("Частота дрожания. Больше — мельче дрожь.")]
    [SerializeField] private float shakeFrequency = 22f;

    [Tooltip("За сколько секунд полная тряска затухает до нуля.")]
    [SerializeField] private float shakeDuration = 0.55f;

    private Camera _camera;
    private PlayerController _player;

    private float _xVelocity;
    private float _yVelocity;
    private float _fovVelocity;

    // Положение и FOV храним отдельно от того, что реально стоит на камере.
    // Иначе сглаживание на следующем кадре прочитает собственный результат
    // вместе с тряской и толчком, и получится нарастающая обратная связь:
    // камера начнёт уползать сама от себя.
    private Vector3 _basePosition;

    // Поворот без тряски. Тряска накладывается поверх него, а не вместо:
    // иначе она затирала бы разворот камеры во время наезда на лицо
    // и возвращала кадр в обычный горизонт прямо посреди наезда.
    private Quaternion _baseRotation = Quaternion.identity;

    private float _smoothedFov;

    // Тряска хранится как «травма» 0..1, а сила берётся как её квадрат.
    // Так слабые толчки почти не заметны, а сильные читаются резко —
    // при линейном затухании всё выглядит вялым дрожанием.
    private float _trauma;
    private float _shakeSeed;

    private float _fovPunch;
    private float _fovPunchVelocity;

    private bool _deathCamActive;
    private float _deathCamTime;
    private Vector3 _deathCamFrom;
    private Quaternion _deathCamFromRotation;
    private float _deathCamFromFov;

    // ------------------------------------------------------------- наружу

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _player = target != null ? target.GetComponent<PlayerController>() : null;
        SnapToTarget();
    }

    /// <summary>
    /// Тряхнуть камеру. amount 0..1: 0.25 — подобрал бонус,
    /// 0.45 — прошёл впритирку, 1 — врезался.
    /// Толчки складываются, но не могут превысить единицу.
    /// </summary>
    public void AddShake(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
    }

    /// <summary>
    /// Резко дёрнуть FOV и отпустить. Положительное значение — кадр
    /// «распахивается», как выдох. Хорошо ложится на подбор кофе.
    /// </summary>
    public void PunchFov(float amount)
    {
        _fovPunch += amount * fovPunchScale;
    }

    // ------------------------------------------------------------ жизненный цикл

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.fieldOfView = baseFov;
        _smoothedFov = baseFov;

        // Своя точка отсчёта в шуме на каждый запуск: иначе все тряски
        // за сессию дрожат по одной траектории, и это заметно.
        _shakeSeed = Random.Range(0f, 1000f);

        if (target != null) _player = target.GetComponent<PlayerController>();
    }

    private void Start()
    {
        SnapToTarget();

        // Подписываемся в Start, а не в Awake: GameManager к этому моменту
        // точно успел проснуться и записать себя в Instance.
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver += StartDeathCam;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= StartDeathCam;
    }

    /// <summary>
    /// Запомнить, откуда камера поедет, и начать наезд на лицо.
    /// </summary>
    private void StartDeathCam()
    {
        if (!deathCam) return;

        _deathCamActive = true;
        _deathCamTime = 0f;
        _deathCamFrom = transform.position;
        _deathCamFromRotation = transform.rotation;
        _deathCamFromFov = _camera != null ? _camera.fieldOfView : baseFov;
    }

    /// <summary>Мгновенно поставить камеру на место, без сглаживания.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        _basePosition = target.position + offset;

        transform.position = _basePosition;
        _baseRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.rotation = _baseRotation;

        _xVelocity = 0f;
        _yVelocity = 0f;
        _fovVelocity = 0f;

        _trauma = 0f;
        _fovPunch = 0f;
        _fovPunchVelocity = 0f;

        _smoothedFov = baseFov;
        if (_camera != null) _camera.fieldOfView = baseFov;

        _deathCamActive = false;
        _deathCamTime = 0f;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (_deathCamActive)
        {
            UpdateDeathCam();
            ApplyShake();
            return;
        }

        Vector3 desired = target.position + offset;

        _basePosition.x = Mathf.SmoothDamp(_basePosition.x, desired.x,
                                           ref _xVelocity, horizontalSmoothTime);
        // На земле и на крыше — почти жёстко, в полёте — мягко.
        // Переключается только постоянная времени, само состояние сглаживания
        // непрерывно, поэтому в момент отрыва и приземления рывка нет.
        float verticalSmooth = _player != null && _player.IsGrounded
            ? groundedVerticalSmoothTime
            : verticalSmoothTime;

        _basePosition.y = Mathf.SmoothDamp(_basePosition.y, desired.y,
                                           ref _yVelocity, verticalSmooth);
        _basePosition.z = desired.z;   // по Z — жёстко, без отставания

        _baseRotation = Quaternion.Euler(pitch, 0f, 0f);

        transform.position = _basePosition;
        transform.rotation = _baseRotation;

        ApplyShake();
        UpdateFov();
    }

    /// <summary>
    /// Сдвигает и подкручивает камеру поверх уже посчитанного положения.
    ///
    /// Шум Перлина, а не Random: случайные числа каждый кадр дают белый шум,
    /// от которого картинка мерцает. Перлин непрерывен — получается именно
    /// тряска, а не рябь.
    ///
    /// Всё на unscaledDeltaTime: тряска обязана доигрывать во время хитстопа,
    /// когда Time.timeScale почти ноль. Иначе удар замирает вместе с камерой
    /// и весь смысл теряется.
    /// </summary>
    private void ApplyShake()
    {
        if (_trauma <= 0f) return;

        float strength = _trauma * _trauma;
        float t = Time.unscaledTime * shakeFrequency;

        // Разные точки входа в шум по каждой оси — иначе камера ходит
        // строго по диагонали.
        float nx = Mathf.PerlinNoise(_shakeSeed, t) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_shakeSeed + 17f, t) * 2f - 1f;
        float nz = Mathf.PerlinNoise(_shakeSeed + 41f, t) * 2f - 1f;

        transform.position = _basePosition
                           + new Vector3(nx, ny, 0f) * (shakePositionAmount * strength);

        transform.rotation = _baseRotation
                           * Quaternion.Euler(0f, 0f, nz * shakeRotationAmount * strength);

        _trauma = Mathf.Max(0f, _trauma - Time.unscaledDeltaTime / Mathf.Max(0.01f, shakeDuration));
    }

    /// <summary>
    /// Подвозит камеру к лицу и останавливается там.
    ///
    /// Камера остаётся ПОЗАДИ игрока, а не облетает его спереди. Так надо:
    /// в момент удара голова уже развернулась назад, к камере. Облёт вперёд
    /// показал бы затылок ровно тогда, когда мы хотим показать лицо.
    ///
    /// Время нескалированное: удар включает хитстоп, и на обычном времени
    /// камера бы застряла на месте вместе со всем остальным.
    /// </summary>
    private void UpdateDeathCam()
    {
        _deathCamTime += Time.unscaledDeltaTime;

        float t = Mathf.Clamp01((_deathCamTime - deathCamDelay)
                                / Mathf.Max(0.01f, deathCamMoveTime));
        t = Mathf.SmoothStep(0f, 1f, t);

        Vector3 face = target.position + Vector3.up * deathCamFaceHeight;
        Vector3 to = face + new Vector3(0f, 0.10f, -deathCamDistance);

        _basePosition = Vector3.Lerp(_deathCamFrom, to, t);
        _baseRotation = Quaternion.Slerp(
            _deathCamFromRotation,
            Quaternion.LookRotation(face - to, Vector3.up),
            t);

        transform.position = _basePosition;
        transform.rotation = _baseRotation;

        if (_camera != null)
            _camera.fieldOfView = Mathf.Lerp(_deathCamFromFov, deathCamFov, t);
    }

    private void UpdateFov()
    {
        if (_player == null) return;

        // Отсчёт от реальной стартовой скорости, а не от нуля: медленнее
        // старта игрок не бежит никогда, и при отсчёте от нуля треть
        // диапазона FOV просто не использовалась.
        float t = Mathf.InverseLerp(fovMinAtSpeed, fovMaxAtSpeed, _player.CurrentSpeed);
        float desiredFov = Mathf.Lerp(baseFov, maxFov, t);

        _smoothedFov = Mathf.SmoothDamp(_smoothedFov, desiredFov, ref _fovVelocity, fovSmoothTime);

        _fovPunch = Mathf.SmoothDamp(_fovPunch, 0f, ref _fovPunchVelocity, 0.18f,
                                     Mathf.Infinity, Time.unscaledDeltaTime);

        _camera.fieldOfView = _smoothedFov + _fovPunch;
    }
}
