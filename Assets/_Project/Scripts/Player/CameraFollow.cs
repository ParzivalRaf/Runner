using UnityEngine;

/// <summary>
/// Камера от третьего лица. По Z едет ровно за игроком (иначе на большой
/// скорости он просто уедет из кадра), а по X и Y догоняет со сглаживанием —
/// смена полосы выглядит мягко и не укачивает.
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

    [Tooltip("Сглаживание по вертикали. Меньше — жёстче реакция на прыжок.")]
    [SerializeField] private float verticalSmoothTime = 0.12f;

    [Header("Field of View")]
    [SerializeField] private float baseFov = 60f;
    [SerializeField] private float maxFov = 68f;

    [Tooltip("Скорость игрока, при которой FOV достигает максимума.")]
    [SerializeField] private float fovMaxAtSpeed = 24f;

    [SerializeField] private float fovSmoothTime = 0.5f;

    private Camera _camera;
    private PlayerController _player;

    private float _xVelocity;
    private float _yVelocity;
    private float _fovVelocity;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _player = target != null ? target.GetComponent<PlayerController>() : null;
        SnapToTarget();
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _camera.fieldOfView = baseFov;

        if (target != null) _player = target.GetComponent<PlayerController>();
    }

    private void Start() => SnapToTarget();

    /// <summary>Мгновенно поставить камеру на место, без сглаживания.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        transform.position = target.position + offset;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

        _xVelocity = 0f;
        _yVelocity = 0f;
        _fovVelocity = 0f;

        if (_camera != null) _camera.fieldOfView = baseFov;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        Vector3 position = transform.position;

        position.x = Mathf.SmoothDamp(position.x, desired.x, ref _xVelocity, horizontalSmoothTime);
        position.y = Mathf.SmoothDamp(position.y, desired.y, ref _yVelocity, verticalSmoothTime);
        position.z = desired.z;   // по Z — жёстко, без отставания

        transform.position = position;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

        UpdateFov();
    }

    private void UpdateFov()
    {
        if (_player == null) return;

        float t = Mathf.InverseLerp(0f, fovMaxAtSpeed, _player.CurrentSpeed);
        float desiredFov = Mathf.Lerp(baseFov, maxFov, t);

        _camera.fieldOfView = Mathf.SmoothDamp(_camera.fieldOfView, desiredFov,
                                               ref _fovVelocity, fovSmoothTime);
    }
}
