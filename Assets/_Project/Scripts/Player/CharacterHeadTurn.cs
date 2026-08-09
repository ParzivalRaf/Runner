using UnityEngine;

/// <summary>
/// Разворачивает голову персонажа лицом к камере на ярких моментах.
///
/// Зачем это вообще нужно. Камера стоит позади игрока, значит всю игру видно
/// затылок, а лицо — никогда. Для обычного раннера это нормально, но здесь
/// вся затея в том, что бегут узнаваемые люди. Если лицо видно только в меню,
/// шутка не работает там, где играют.
///
/// Решение: голова бежит вперёд как положено, но на проходе впритирку
/// и на столкновении резко оборачивается на игрока и через секунду
/// отворачивается обратно. Лицо появляется ровно тогда, когда игрок и так
/// смотрит на экран, и не мозолит глаза всё остальное время.
///
/// Куда вешать: на корень модели персонажа. Сборщик делает это сам.
/// </summary>
public class CharacterHeadTurn : MonoBehaviour
{
    [Tooltip("Объект головы. Крутится он целиком, вместе с лицом.")]
    [SerializeField] private Transform head;

    [Tooltip("Выключи, если разворот раздражает. Персонаж просто будет " +
             "всегда бежать лицом вперёд.")]
    [SerializeField] private bool enableGlance = true;

    [Tooltip("На сколько градусов оборачивается. 180 — точно в камеру, " +
             "150 — вполоборота, выглядит мягче и менее жутко.")]
    [Range(90f, 180f)]
    [SerializeField] private float glanceAngle = 165f;

    [Tooltip("За сколько секунд голова доворачивается. Меньше — резче.")]
    [SerializeField] private float turnSeconds = 0.10f;

    // Куда голова хочет смотреть прямо сейчас: 0 — вперёд, 1 — на камеру.
    private float _target;
    private float _current;
    private float _holdLeft;

    private void OnEnable() => GameFeel.OnGlanceBack += Glance;
    private void OnDisable() => GameFeel.OnGlanceBack -= Glance;

    private void Awake()
    {
        // Голову ищем сами, если её забыли назначить: модель может быть
        // собрана вручную, а не сборщиком. Ищем вглубь, а не только среди
        // прямых детей — голова лежит внутри Rig.
        if (head == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Head") continue;
                head = t;
                break;
            }
        }
    }

    private void Glance(float holdSeconds)
    {
        if (!enableGlance || head == null) return;

        _target = 1f;
        _holdLeft = Mathf.Max(_holdLeft, holdSeconds);
    }

    private void LateUpdate()
    {
        if (head == null) return;

        // Всё на unscaledDeltaTime: столкновение включает хитстоп, время
        // почти замирает — а обернуться голова должна именно в этот момент,
        // иначе игрок увидит лицо уже на экране проигрыша, когда поздно.
        float dt = Time.unscaledDeltaTime;

        if (_holdLeft > 0f)
        {
            _holdLeft -= dt;
            if (_holdLeft <= 0f) _target = 0f;
        }

        if (Mathf.Approximately(_current, _target) && _current <= 0f) return;

        float step = dt / Mathf.Max(0.01f, turnSeconds);
        _current = Mathf.MoveTowards(_current, _target, step);

        head.localRotation = Quaternion.Euler(0f, glanceAngle * _current, 0f);
    }

    /// <summary>Поставить голову прямо. Вызывается при старте нового забега.</summary>
    public void ResetTurn()
    {
        _current = 0f;
        _target = 0f;
        _holdLeft = 0f;

        if (head != null) head.localRotation = Quaternion.identity;
    }
}
