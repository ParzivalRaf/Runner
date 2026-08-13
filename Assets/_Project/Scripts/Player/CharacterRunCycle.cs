using UnityEngine;

/// <summary>
/// Заставляет фигурку бежать: машет руками и ногами, покачивается,
/// поджимает ноги в прыжке.
///
/// Почему без Mixamo и без скачанных моделей. Настоящая анимация требует
/// модели со скелетом, Animator Controller и клипов — это часы работы
/// и обязательный поход за файлами. А фигурка здесь собрана из коробок,
/// и коробкам достаточно покрутить пару узлов синусом: получается ровно тот
/// «блочный бег», который и так выглядит уместно в этой стилистике.
///
/// Темп привязан к реальной скорости игрока, а не к секундам. Поэтому
/// на разгоне фигурка начинает частить сама собой, и разгон читается
/// не только по цифрам и по FOV, но и по ногам.
///
/// Куда вешать: на корень модели персонажа. Сборщик делает это сам.
/// </summary>
public class CharacterRunCycle : MonoBehaviour
{
    [Header("Узлы")]
    [Tooltip("Всё тело целиком. Его покачивает вверх-вниз.")]
    [SerializeField] private Transform rig;

    [SerializeField] private Transform hipLeft;
    [SerializeField] private Transform hipRight;
    [SerializeField] private Transform shoulderLeft;
    [SerializeField] private Transform shoulderRight;

    [Header("Бег")]
    [Tooltip("Сколько юнитов трассы проходит за один полный шаг (левая+правая). " +
             "Меньше — фигурка частит сильнее.")]
    [SerializeField] private float strideLength = 4.5f;

    [Tooltip("Размах ног, градусы.")]
    [SerializeField] private float legSwing = 38f;

    [Tooltip("Размах рук, градусы. Обычно чуть меньше, чем у ног.")]
    [SerializeField] private float armSwing = 30f;

    [Tooltip("На сколько юнитов тело подпрыгивает на каждом шаге.")]
    [SerializeField] private float bobHeight = 0.045f;

    [Header("В воздухе")]
    [Tooltip("Насколько поджимаются ноги в прыжке, градусы.")]
    [SerializeField] private float jumpTuck = 55f;

    [Tooltip("Насколько задираются руки в прыжке, градусы.")]
    [SerializeField] private float jumpArms = 70f;

    [Tooltip("За сколько секунд поза переходит между бегом и полётом.")]
    [SerializeField] private float poseBlend = 0.12f;

    [Header("Подкат")]
    [Tooltip("Насколько поджимаются ноги у блочной фигурки во время подката.")]
    [SerializeField] private float slideCrouch = 70f;

    [Tooltip("Насколько корпус опускается у блочной фигурки во время подката.")]
    [SerializeField] private float slideBodyDrop = 0.35f;

    private PlayerController _player;

    private float _phase;
    private float _air;              // 0 — на земле, 1 — в воздухе
    private float _slide;            // 0 — бежит, 1 — в позе подката
    private Vector3 _rigBasePosition;

    private void Awake()
    {
        // Модель живёт внутри игрока, поэтому контроллер ищем вверх по иерархии.
        _player = GetComponentInParent<PlayerController>();

        if (rig != null) _rigBasePosition = rig.localPosition;
    }

    private void LateUpdate()
    {
        if (_player == null) return;

        float dt = Time.deltaTime;

        // Фаза считается от пройденного расстояния, а не от времени.
        // Так шаг не «плывёт» относительно дороги при смене скорости.
        float speed = Mathf.Max(0f, _player.CurrentSpeed);
        _phase += speed * dt * (2f * Mathf.PI / Mathf.Max(0.1f, strideLength));

        if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f;

        float wantAir = _player.IsGrounded ? 0f : 1f;
        _air = Mathf.MoveTowards(_air, wantAir, dt / Mathf.Max(0.01f, poseBlend));

        float wantSlide = _player.IsSliding ? 1f : 0f;
        _slide = Mathf.MoveTowards(_slide, wantSlide, dt / Mathf.Max(0.01f, poseBlend));

        float swing = Mathf.Sin(_phase);

        // Ноги в противофазе друг другу, руки — в противофазе ногам.
        // Без этого фигурка скачет как на скакалке, а не бежит.
        float legRun = swing * legSwing;
        float armRun = swing * armSwing;

        float leftLeg = Mathf.LerpAngle(legRun, -jumpTuck, _air);
        float rightLeg = Mathf.LerpAngle(-legRun, -jumpTuck * 0.55f, _air);
        SetPitch(hipLeft, Mathf.LerpAngle(leftLeg, slideCrouch, _slide));
        SetPitch(hipRight, Mathf.LerpAngle(rightLeg, slideCrouch, _slide));

        float leftArm = Mathf.LerpAngle(-armRun, -jumpArms, _air);
        float rightArm = Mathf.LerpAngle(armRun, -jumpArms, _air);
        SetPitch(shoulderLeft, Mathf.LerpAngle(leftArm, 35f, _slide));
        SetPitch(shoulderRight, Mathf.LerpAngle(rightArm, 35f, _slide));

        if (rig != null)
        {
            // Два подскока за шаг: тело поднимается и на левой, и на правой ноге.
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * bobHeight * (1f - _air);
            float drop = Mathf.Lerp(0f, -slideBodyDrop, _slide);
            rig.localPosition = _rigBasePosition + new Vector3(0f, bob + drop, 0f);
        }
    }

    private static void SetPitch(Transform node, float degrees)
    {
        if (node != null) node.localRotation = Quaternion.Euler(degrees, 0f, 0f);
    }
}
