using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Замечает, что игрок прошёл впритирку мимо препятствия и не задел его:
/// перепрыгнул барьер на волосок, проехал под балкой, увернулся в момент
/// смены полосы.
///
/// Зачем это нужно: в раннере почти всё время игрок либо в безопасности,
/// либо мёртв, и между этими состояниями нет ничего. Near-miss даёт третье
/// состояние — «чуть не», — и именно оно превращает уклонение в событие,
/// за которое хочется играть дальше.
///
/// Куда вешать: на объект Player, рядом с PlayerCollision.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(CapsuleCollider))]
public class NearMissDetector : MonoBehaviour
{
    [Header("Зона")]
    [Tooltip("Насколько шире собственного радиуса игрока ловим препятствие, юниты. " +
             "Препятствие в соседней полосе стоит в 1.65 от центра, поэтому " +
             "значение сильно больше 1.2 засчитает вообще любой объезд.")]
    [SerializeField] private float sideMargin = 0.75f;

    [Tooltip("Запас по высоте: перепрыгнул на волосок или проехал под балкой.")]
    [SerializeField] private float verticalMargin = 0.6f;

    [Tooltip("Глубина зоны вдоль трассы. Должна перекрывать путь за один кадр.")]
    [SerializeField] private float depth = 0.5f;

    [Header("Защита от повторов")]
    [Tooltip("Сколько секунд помним засчитанное препятствие, чтобы не " +
             "начислить его же на следующем кадре.")]
    [SerializeField] private float rememberSeconds = 1.5f;

    // 16, а не 8: в зону попадают ещё и пол, монеты и бонусы, а переполнение
    // буфера тихо отрезало бы часть результатов.
    private static readonly Collider[] Buffer = new Collider[16];

    private CapsuleCollider _capsule;

    private struct Credited
    {
        public Obstacle Obstacle;
        public float Time;
    }

    // Список, а не HashSet: элементов единицы, зато записи сами устаревают
    // по времени. С HashSet пришлось бы как-то узнавать, что препятствие
    // уехало в пул, иначе множество росло бы весь забег.
    private readonly List<Credited> _credited = new List<Credited>();

    private void Awake()
    {
        _capsule = GetComponent<CapsuleCollider>();
    }

    private void Update()
    {
        GameManager game = GameManager.Instance;

        // Забег кончился или ещё не начался — чистим память сами.
        // Так не нужна ни ссылка в GameManager, ни подписка на событие:
        // компонент полностью самодостаточен.
        if (game == null || !game.IsRunning)
        {
            if (_credited.Count > 0) _credited.Clear();
            return;
        }

        // В режиме бога игрок проходит сквозь препятствия — «впритирку»
        // там происходит непрерывно и смысла не имеет.
        if (game.GodMode) return;

        // Под кофе препятствия просто ломаются. Это не уклонение.
        if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsInvincible) return;

        Forget();
        Scan();
    }

    private void Forget()
    {
        float cutoff = Time.time - rememberSeconds;

        for (int i = _credited.Count - 1; i >= 0; i--)
        {
            if (_credited[i].Time > cutoff && _credited[i].Obstacle != null) continue;
            _credited.RemoveAt(i);
        }
    }

    private void Scan()
    {
        // Зона строится от текущего коллайдера, а не от констант: в подкате
        // игрок ниже, и зона обязана присесть вместе с ним.
        Vector3 center = transform.position + _capsule.center;

        var halfExtents = new Vector3(_capsule.radius + sideMargin,
                                      _capsule.height * 0.5f + verticalMargin,
                                      depth);

        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, Buffer,
                                               Quaternion.identity, ~0,
                                               QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Obstacle obstacle = Buffer[i].GetComponentInParent<Obstacle>();
            if (obstacle == null) continue;

            // Поезда не считаем. Пока игрок едет по крыше, он всё время
            // находится над бортом вагона, и каждый следующий вагон
            // засчитывался бы как проход впритирку: на составе из шести
            // вагонов это шесть свистов и шесть тряски подряд.
            // А поезд в соседней полосе стоит слишком далеко, чтобы вообще
            // попасть в зону — так что запрет ничего не отнимает.
            if (obstacle.ObstacleKind == Obstacle.Kind.Train) continue;

            if (AlreadyCredited(obstacle)) continue;

            _credited.Add(new Credited { Obstacle = obstacle, Time = Time.time });
            Reward();
        }
    }

    private bool AlreadyCredited(Obstacle obstacle)
    {
        for (int i = 0; i < _credited.Count; i++)
        {
            if (_credited[i].Obstacle == obstacle) return true;
        }
        return false;
    }

    /// <summary>
    /// Намеренно без замедления времени. Слоумо на каждый near-miss — приём
    /// красивый, но на этой плотности препятствий он сработал бы несколько
    /// раз в секунду и превратил бы забег в кашу. Хватает тряски и свиста.
    /// </summary>
    private void Reward()
    {
        if (GameFeel.Instance != null) GameFeel.Instance.NearMiss();
        if (AudioManager.Instance != null) AudioManager.Instance.PlayNearMiss();
        if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterNearMiss();
    }

    private void OnDrawGizmosSelected()
    {
        var capsule = _capsule != null ? _capsule : GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.35f);
        Gizmos.DrawWireCube(transform.position + capsule.center,
                            new Vector3((capsule.radius + sideMargin) * 2f,
                                        capsule.height + verticalMargin * 2f,
                                        depth * 2f));
    }
}
