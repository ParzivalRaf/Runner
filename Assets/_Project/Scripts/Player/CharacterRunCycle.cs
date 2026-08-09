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

    private PlayerController _player;

    private float _phase;
    private float _air;              // 0 — на земле, 1 — в воздухе
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

        float swing = Mathf.Sin(_phase);

        // Ноги в противофазе друг другу, руки — в противофазе ногам.
        // Без этого фигурка скачет как на скакалке, а не бежит.
        float legRun = swing * legSwing;
        float armRun = swing * armSwing;

        SetPitch(hipLeft, Mathf.LerpAngle(legRun, -jumpTuck, _air));
        SetPitch(hipRight, Mathf.LerpAngle(-legRun, -jumpTuck * 0.55f, _air));

        SetPitch(shoulderLeft, Mathf.LerpAngle(-armRun, -jumpArms, _air));
        SetPitch(shoulderRight, Mathf.LerpAngle(armRun, -jumpArms, _air));

        if (rig != null)
        {
            // Два подскока за шаг: тело поднимается и на левой, и на правой ноге.
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * bobHeight * (1f - _air);
            rig.localPosition = _rigBasePosition + new Vector3(0f, bob, 0f);
        }
    }

    private static void SetPitch(Transform node, float degrees)
    {
        if (node != null) node.localRotation = Quaternion.Euler(degrees, 0f, 0f);
    }
}
