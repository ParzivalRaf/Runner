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

        GameManager.Instance.GameOver();
    }
}
