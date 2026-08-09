using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Движение бегуна: три полосы, прыжок с ручной гравитацией, подкат.
/// Никакой физики Rigidbody — позицию считаем сами, так поведение
/// полностью предсказуемо и одинаково на любом железе.
///
/// Куда вешать: на объект Player (пустой GameObject, пивот на уровне пола).
/// Требует CapsuleCollider и Rigidbody (кинематический) на том же объекте.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Полосы")]
    [Tooltip("Расстояние между центрами соседних полос, юниты.")]
    [SerializeField] private float laneDistance = 2.5f;

    [Tooltip("За сколько секунд игрок переезжает на соседнюю полосу.")]
    [SerializeField] private float laneChangeTime = 0.15f;

    [Header("Бег вперёд")]
    // Стартовая скорость специально высокая: 14 из 24, а не 8.
    // При 8 и разгоне 0.15 до потолка ехать 107 секунд — почти две минуты
    // игрок бежит на трети от того темпа, ради которого игра сделана.
    // Теперь потолок набирается за ~33 секунды, и первые секунды уже бодрые.
    [SerializeField] private float startSpeed = 14f;
    [SerializeField] private float speedGainPerSecond = 0.30f;
    [SerializeField] private float maxSpeed = 24f;

    [Header("Прыжок")]
    [Tooltip("Максимальная высота прыжка, юниты.")]
    [SerializeField] private float jumpHeight = 2.2f;

    [Tooltip("Полное время в воздухе: взлёт + падение, секунды.")]
    [SerializeField] private float jumpAirTime = 0.75f;

    [Tooltip("Во сколько раз гравитация сильнее на спуске. Делает прыжок 'сочнее'.")]
    [SerializeField] private float fallGravityMultiplier = 1.6f;

    [Tooltip("Множитель гравитации при свайпе вниз в воздухе (быстрое падение).")]
    [SerializeField] private float fastFallMultiplier = 3.5f;

    [Header("Подкат")]
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float slidingHeight = 0.9f;
    [SerializeField] private float capsuleRadius = 0.4f;

    [Tooltip("Слои препятствий. Если над головой препятствие, подкат не прервётся.")]
    [SerializeField] private LayerMask obstacleMask = 0;

    [Header("Поверхность под ногами")]
    [Tooltip("На сколько игрок готов подняться, наступив на что-то. Больше — " +
             "начнёт запрыгивать на поезда сбоку вместо того, чтобы разбиться.")]
    [SerializeField] private float stepUpTolerance = 0.35f;

    [Tooltip("Насколько глубоко искать пол под ногами.")]
    [SerializeField] private float surfaceProbeDepth = 8f;

    [Tooltip("Самый крутой уклон, по которому игрок вбегает наверх, как тангенс. " +
             "0.55 это примерно 29 градусов. Пандус к поездам положе — 15.")]
    [SerializeField] private float maxClimbTangent = 0.55f;

    [Header("Ссылки")]
    [Tooltip("Компонент чтения свайпов. Обычно на этом же объекте.")]
    [SerializeField] private SwipeDetector swipeDetector;

    [Tooltip("Дочерний объект с мешем. Сжимается при подкате.")]
    [SerializeField] private Transform visual;

    // --- внутреннее состояние ---

    private CapsuleCollider _capsule;

    private int _currentLane = 1;      // 0 = левая, 1 = центр, 2 = правая
    private float _targetX;

    // Высота мирового пола. Служит запасным ответом, если луч вообще
    // ничего не нашёл: лучше бежать по нулю, чем провалиться в бездну.
    private float _baseGroundY;

    // Высота поверхности, на которой игрок стоит прямо сейчас. Меняется,
    // когда он запрыгивает на поезд и когда спрыгивает обратно.
    private float _surfaceY;

    private float _verticalVelocity;
    private float _riseGravity;
    private float _jumpVelocity;
    private bool _isGrounded = true;
    private bool _isFastFalling;

    private bool _isSliding;
    private float _slideTimer;
    private bool _slideQueuedOnLanding;

    private float _currentSpeed;
    private float _distance;

    private Vector3 _visualBaseScale = Vector3.one;
    private Vector3 _visualBasePosition;

    // --- публичное состояние, пригодится дальше (счёт, камера, анимации) ---

    /// <summary>Множитель скорости от бонусов. Выставляет PowerUpManager.</summary>
    public float ExternalSpeedMultiplier { get; set; } = 1f;

    /// <summary>Множитель ВЫСОТЫ прыжка от бонусов. Выставляет PowerUpManager.</summary>
    public float ExternalJumpMultiplier { get; set; } = 1f;

    /// <summary>
    /// Прибавка к стартовой скорости от способности персонажа, юниты в секунду.
    /// Выставляет GameManager перед забегом. Действует только на старт:
    /// потолок maxSpeed остаётся общим для всех.
    /// </summary>
    public float StartSpeedBonus { get; set; }

    /// <summary>Фактическая скорость с учётом бонусов.</summary>
    public float CurrentSpeed => _currentSpeed * ExternalSpeedMultiplier;

    public float Distance => _distance;
    public bool IsGrounded => _isGrounded;
    public bool IsSliding => _isSliding;
    public int CurrentLane => _currentLane;

    // ---------------------------------------------------------------------

    private void Awake()
    {
        _capsule = GetComponent<CapsuleCollider>();

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        // Интерполяция нужна только когда физика сама двигает тело.
        // Мы пишем Transform вручную каждый кадр, и интерполяция начинает
        // «тянуть» объект к устаревшей позиции — игрок повисает над полом.
        body.interpolation = RigidbodyInterpolation.None;

        if (swipeDetector == null) swipeDetector = GetComponent<SwipeDetector>();

        if (visual != null)
        {
            _visualBaseScale = visual.localScale;
            _visualBasePosition = visual.localPosition;
        }

        RecalculateJumpPhysics();

        _baseGroundY = transform.position.y;
        _surfaceY = _baseGroundY;
        _currentSpeed = startSpeed;
        _currentLane = 1;
        _targetX = LaneToX(_currentLane);

        Vector3 position = transform.position;
        position.x = _targetX;
        transform.position = position;

        ApplyStandingCollider();
    }

    private void OnEnable()
    {
        if (swipeDetector != null) swipeDetector.OnSwipe += HandleSwipe;
    }

    private void OnDisable()
    {
        if (swipeDetector != null) swipeDetector.OnSwipe -= HandleSwipe;
    }

    private void OnValidate()
    {
        // Чтобы значения из инспектора сразу пересчитывались в редакторе.
        jumpHeight = Mathf.Max(0.1f, jumpHeight);
        jumpAirTime = Mathf.Max(0.1f, jumpAirTime);
        fallGravityMultiplier = Mathf.Max(1f, fallGravityMultiplier);
        RecalculateJumpPhysics();
    }

    /// <summary>
    /// Выводим силу толчка и гравитацию из желаемой высоты и времени в воздухе,
    /// чтобы в инспекторе крутить понятные величины, а не абстрактные ускорения.
    /// </summary>
    private void RecalculateJumpPhysics()
    {
        float riseTime = jumpAirTime / (1f + 1f / Mathf.Sqrt(fallGravityMultiplier));
        _riseGravity = 2f * jumpHeight / (riseTime * riseTime);
        _jumpVelocity = _riseGravity * riseTime;
    }

    /// <summary>
    /// Вернуть игрока в стартовое состояние. Вызывает GameManager перед
    /// каждым новым забегом — сцена при этом не перезагружается.
    /// </summary>
    public void ResetRun(float startDistance = 0f)
    {
        ExternalSpeedMultiplier = 1f;
        ExternalJumpMultiplier = 1f;

        _currentSpeed = Mathf.Min(maxSpeed, startSpeed + Mathf.Max(0f, StartSpeedBonus));
        _distance = Mathf.Max(0f, startDistance);

        _currentLane = 1;
        _targetX = LaneToX(_currentLane);

        _verticalVelocity = 0f;
        _isGrounded = true;
        _isFastFalling = false;

        _isSliding = false;
        _slideTimer = 0f;
        _slideQueuedOnLanding = false;

        _surfaceY = _baseGroundY;
        transform.position = new Vector3(_targetX, _baseGroundY, 0f);
        ApplyStandingCollider();
    }

    private void Update()
    {
        // После столкновения игрок замирает на месте.
        if (GameManager.Instance != null && !GameManager.Instance.IsRunning) return;

        float dt = Time.deltaTime;

        UpdateSpeed(dt);
        UpdateLane(dt);

        // Двигаемся вперёд ДО поиска пола: иначе луч щупает то место, где
        // игрок был кадр назад, и на краю поезда он успевает шагнуть в пустоту
        // раньше, чем игра это заметит.
        MoveForward(dt);

        UpdateVertical(dt);
        UpdateSlide(dt);
    }

    // ------------------------------------------------------------- скорость

    private void UpdateSpeed(float dt)
    {
        _currentSpeed = Mathf.Min(maxSpeed, _currentSpeed + speedGainPerSecond * dt);
    }

    private void MoveForward(float dt)
    {
        float step = _currentSpeed * ExternalSpeedMultiplier * dt;
        _distance += step;

        Vector3 position = transform.position;
        position.z += step;
        transform.position = position;
    }

    // ---------------------------------------------------------------- полосы

    private float LaneToX(int lane) => (lane - 1) * laneDistance;

    private void UpdateLane(float dt)
    {
        float speed = laneDistance / Mathf.Max(0.01f, laneChangeTime);

        Vector3 position = transform.position;
        position.x = Mathf.MoveTowards(position.x, _targetX, speed * dt);
        transform.position = position;
    }

    private void ChangeLane(int delta)
    {
        int newLane = Mathf.Clamp(_currentLane + delta, 0, 2);
        if (newLane == _currentLane) return;   // уже на краю — игнорируем

        _currentLane = newLane;
        _targetX = LaneToX(_currentLane);
    }

    // --------------------------------------------------------------- прыжок

    // Ровно столько игрок может «висеть» над поверхностью и всё ещё считаться
    // стоящим. Без этого зазора он срывался бы в падение от любой мелочи.
    private const float GroundStickTolerance = 0.05f;

    private static readonly RaycastHit[] SurfaceHits = new RaycastHit[8];

    // Ответ на вопрос «по этому коллайдеру можно бежать» кэшируется.
    //
    // Зачем: проверка идёт каждый кадр по каждому задетому коллайдеру, и
    // раньше она каждый раз лезла вверх по всей иерархии объекта. На пустой
    // дороге луч задевает один-два коллайдера и это ничего не стоило.
    // На поезде их сразу несколько — вагоны, крыша, пандус, пол под составом,
    // сам игрок — и цена выросла ровно там, где кадров и так меньше всего.
    //
    // Объекты берутся из пула и переиспользуются, поэтому набор коллайдеров
    // конечный и словарь не растёт бесконечно.
    private static readonly Dictionary<Collider, bool> SurfaceLookup = new Dictionary<Collider, bool>(64);

    private static bool IsRunnable(Collider collider)
    {
        if (SurfaceLookup.TryGetValue(collider, out bool known)) return known;

        bool runnable = collider.GetComponentInParent<GroundSurface>() != null;
        SurfaceLookup[collider] = runnable;
        return runnable;
    }

    /// <summary>
    /// Высота поверхности под ногами: пол чанка, крыша поезда, площадка.
    ///
    /// Ключевая деталь — потолок поиска. Поверхность, которая ВЫШЕ ног больше
    /// чем на stepUpTolerance, отбрасывается. Именно поэтому бегущий по земле
    /// игрок не запрыгивает на поезд сбоку сам собой: крыша на высоте 1.8
    /// просто не считается полом, пока игрок не поднялся к ней прыжком.
    /// А врезавшись в борт, он умирает от триггера препятствия, как и должен.
    /// </summary>
    private float FindSurfaceY()
    {
        Vector3 origin = transform.position + Vector3.up * (standingHeight + 0.5f);
        float length = standingHeight + 0.5f + surfaceProbeDepth;

        // QueryTriggerInteraction.Ignore принципиален: препятствия — триггеры,
        // и бежать по ним нельзя. Проходимыми считаются только обычные
        // коллайдеры с меткой GroundSurface.
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, SurfaceHits, length,
                                            ~0, QueryTriggerInteraction.Ignore);

        // Порог подъёма растёт вместе с пройденным за кадр расстоянием.
        // Без этого пандус ломался бы при просадке кадров: на 60 кадрах
        // поверхность поднимается на 0.11 за кадр и порог 0.35 её берёт,
        // а на 20 кадрах — уже на 0.33, и игрок сваливался бы с подъёма
        // ровно в тот момент, когда игре и так плохо.
        //
        // Вертикальный борт вагона этим не пробить: чтобы порог дорос
        // до 1.8, игре надо просесть примерно до восьми кадров в секунду.
        float climbAllowance = CurrentSpeed * Time.deltaTime * maxClimbTangent;
        float ceiling = transform.position.y + stepUpTolerance + climbAllowance;

        float best = _baseGroundY;

        for (int i = 0; i < count; i++)
        {
            if (!IsRunnable(SurfaceHits[i].collider)) continue;

            float y = SurfaceHits[i].point.y;
            if (y > ceiling) continue;      // слишком высоко, туда не наступить
            if (y > best) best = y;         // из подходящих берём самую верхнюю
        }

        return best;
    }

    private void UpdateVertical(float dt)
    {
        _surfaceY = FindSurfaceY();

        Vector3 position = transform.position;

        if (_isGrounded)
        {
            // Поверхность ушла из-под ног — кончился поезд, кончилась эстакада.
            if (position.y > _surfaceY + GroundStickTolerance)
            {
                _isGrounded = false;
                _verticalVelocity = 0f;
            }
            else
            {
                // Прижимаемся к поверхности. Небольшой подъём отрабатывается
                // здесь же как ступенька — отдельного кода для неё не нужно.
                position.y = _surfaceY;
                transform.position = position;
                return;
            }
        }

        float gravity = _riseGravity;
        if (_verticalVelocity < 0f)
            gravity *= _isFastFalling ? fastFallMultiplier : fallGravityMultiplier;

        _verticalVelocity -= gravity * dt;
        position.y += _verticalVelocity * dt;

        // Приземляемся только на спуске. Иначе прыжок с земли на поезд
        // защёлкивался бы на крыше на полпути вверх: крыша становится
        // «полом» уже на высоте 1.45, а игрок в этот момент ещё летит вверх.
        if (_verticalVelocity <= 0f && position.y <= _surfaceY)
        {
            position.y = _surfaceY;
            _verticalVelocity = 0f;
            _isGrounded = true;
            _isFastFalling = false;

            // Приземление само по себе почти незаметно, но без него прыжок
            // ощущается как парение: вверх есть, а возвращения веса нет.
            if (GameFeel.Instance != null) GameFeel.Instance.Land();

            if (_slideQueuedOnLanding)
            {
                _slideQueuedOnLanding = false;
                StartSlide();
            }
        }

        transform.position = position;
    }

    private void Jump()
    {
        if (!_isGrounded) return;

        if (_isSliding) StopSlide(force: true);

        _isGrounded = false;
        _isFastFalling = false;

        // Высота прыжка растёт как квадрат начальной скорости, поэтому
        // множитель высоты ×1.8 — это множитель скорости √1.8.
        _verticalVelocity = _jumpVelocity * Mathf.Sqrt(Mathf.Max(0.01f, ExternalJumpMultiplier));

        if (AudioManager.Instance != null) AudioManager.Instance.PlayJump();
    }

    private void FastFall()
    {
        _isFastFalling = true;
        _slideQueuedOnLanding = true;

        // Если ещё летим вверх — обнуляем подъём, падение начинается сразу.
        if (_verticalVelocity > 0f) _verticalVelocity = 0f;
    }

    // --------------------------------------------------------------- подкат

    private void StartSlide()
    {
        _isSliding = true;
        _slideTimer = slideDuration;
        ApplySlidingCollider();

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySlide();
    }

    private void UpdateSlide(float dt)
    {
        if (!_isSliding) return;

        _slideTimer -= dt;
        if (_slideTimer > 0f) return;

        StopSlide(force: false);
    }

    private void StopSlide(bool force)
    {
        // Если над головой препятствие — продолжаем ехать на корточках,
        // иначе игрок встанет прямо в балку и умрёт не по своей вине.
        if (!force && IsBlockedAbove())
        {
            _slideTimer = 0.1f;
            return;
        }

        _isSliding = false;
        ApplyStandingCollider();
    }

    private static readonly Collider[] OverheadBuffer = new Collider[8];

    /// <summary>
    /// Проверяем, не встанем ли мы головой в балку, если сейчас разогнёмся.
    /// Ищем именно компонент Obstacle, а не слой — так не нужно заводить
    /// отдельный Layer и его легко забыть проставить на префабе.
    /// </summary>
    private bool IsBlockedAbove()
    {
        Vector3 bottom = transform.position + Vector3.up * (slidingHeight + capsuleRadius);
        Vector3 top = transform.position + Vector3.up * (standingHeight - capsuleRadius);
        if (top.y <= bottom.y) return false;

        int mask = obstacleMask == 0 ? ~0 : obstacleMask.value;

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, capsuleRadius * 0.95f,
                                                   OverheadBuffer, mask,
                                                   QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            if (OverheadBuffer[i].GetComponentInParent<Obstacle>() != null) return true;
        }

        return false;
    }

    private void ApplyStandingCollider() => ApplyColliderHeight(standingHeight);

    private void ApplySlidingCollider() => ApplyColliderHeight(slidingHeight);

    private void ApplyColliderHeight(float height)
    {
        _capsule.direction = 1;                 // 1 = ось Y
        _capsule.radius = capsuleRadius;
        _capsule.height = height;
        _capsule.center = new Vector3(0f, height * 0.5f, 0f);

        if (visual == null) return;

        float ratio = height / standingHeight;
        visual.localScale = new Vector3(_visualBaseScale.x,
                                        _visualBaseScale.y * ratio,
                                        _visualBaseScale.z);
        visual.localPosition = new Vector3(_visualBasePosition.x,
                                           _visualBasePosition.y * ratio,
                                           _visualBasePosition.z);
    }

    // ----------------------------------------------------------------- ввод

    private void HandleSwipe(SwipeDetector.Direction direction)
    {
        switch (direction)
        {
            case SwipeDetector.Direction.Left:
                ChangeLane(-1);                 // менять полосу можно и в воздухе
                break;

            case SwipeDetector.Direction.Right:
                ChangeLane(+1);
                break;

            case SwipeDetector.Direction.Up:
                if (_isGrounded) Jump();
                break;

            case SwipeDetector.Direction.Down:
                if (_isGrounded)
                {
                    if (!_isSliding) StartSlide();
                }
                else
                {
                    FastFall();                 // в воздухе — камнем вниз и сразу подкат
                }
                break;
        }
    }

    // ---------------------------------------------------------- отладка в Scene

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int lane = 0; lane < 3; lane++)
        {
            Vector3 origin = new Vector3((lane - 1) * laneDistance,
                                         transform.position.y,
                                         transform.position.z);
            Gizmos.DrawLine(origin, origin + Vector3.forward * 30f);
        }
    }
}
