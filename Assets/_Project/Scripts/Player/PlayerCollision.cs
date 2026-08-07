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
            return;
        }

        // Щит персонажа — последний шанс. Тратится молча, одно столкновение
        // за забег, и препятствие исчезает так же, как под кофе.
        if (CharacterManager.Instance != null && CharacterManager.Instance.TryConsumeShield())
        {
            obstacle.gameObject.SetActive(false);
            return;
        }

        GameManager.Instance.GameOver();
    }
}
