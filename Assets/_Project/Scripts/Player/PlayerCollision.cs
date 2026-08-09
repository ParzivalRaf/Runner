using UnityEngine;

/// <summary>
/// Ловит столкновения игрока с препятствиями.
///
/// Работает потому, что у игрока кинематический Rigidbody, а у препятствий
/// коллайдеры с галочкой Is Trigger — этого достаточно, чтобы Unity вызвала
/// OnTriggerEnter, хотя физически ничто никуда не отталкивается.
///
/// Куда вешать: на объект Player.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCollision : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsRunning) return;
        if (GameManager.Instance.GodMode) return;

        Obstacle obstacle = other.GetComponentInParent<Obstacle>();
        if (obstacle == null) return;

        // Под кофе игрок проламывается сквозь препятствие, а не умирает.
        // Объект просто выключаем: в пул его всё равно вернёт ObstacleSpawner,
        // а при следующем Get он включится обратно.
        if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsInvincible)
        {
            obstacle.gameObject.SetActive(false);

            // Проламывание сквозь препятствие обязано ощущаться как удар,
            // иначе кофе выглядит так, будто препятствия просто исчезли.
            if (GameFeel.Instance != null) GameFeel.Instance.Shake(0.35f);
            if (EffectManager.Instance != null)
                EffectManager.Instance.PlayCrash(transform.position + Vector3.up);

            return;
        }

        // Щит персонажа — последний шанс. Одно столкновение за забег,
        // и препятствие исчезает так же, как под кофе. Раньше это
        // происходило совершенно молча: игрок не понимал, что его спасли,
        // и в следующий раз рассчитывал на щит, которого уже нет.
        if (CharacterManager.Instance != null && CharacterManager.Instance.TryConsumeShield())
        {
            obstacle.gameObject.SetActive(false);

            if (GameFeel.Instance != null)
            {
                GameFeel.Instance.Shake(0.6f);
                GameFeel.Instance.HitStop(0.07f);
            }

            if (EffectManager.Instance != null)
                EffectManager.Instance.PlayCrash(transform.position + Vector3.up);

            return;
        }

        // Эффект ставим здесь, а не в GameManager: только тут известно,
        // где именно игрок встретился с препятствием.
        if (EffectManager.Instance != null)
            EffectManager.Instance.PlayCrash(transform.position + Vector3.up);

        // Хитстоп ДО GameOver: заморозка должна начаться в кадре удара,
        // а не после того, как всплыл экран проигрыша.
        if (GameFeel.Instance != null) GameFeel.Instance.Crash();

        GameManager.Instance.GameOver();
    }
}
